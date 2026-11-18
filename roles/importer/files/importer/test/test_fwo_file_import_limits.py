from __future__ import annotations

import functools
import gzip
import json
import os
import re
import socket
import threading
import time
from typing import TYPE_CHECKING, Any
from unittest.mock import MagicMock

import fwo_file_import
import fwo_globals
import pytest
import requests
import urllib3
from fwo_exceptions import ConfigFileNotFoundError, ConfigFileRejectedError
from models.import_state import ImportState

if TYPE_CHECKING:
    from pathlib import Path

CONFIG_URL = "https://config.example/config.json"
SMALL_CONFIG = b'{"ManagerSet": []}'
TRICKLE_INTERVAL = 0.2
TRICKLE_BYTES = 30
TRICKLE_TOTAL_TIMEOUT = 0.5
TRICKLE_MAX_SECONDS = 3.0
DEADLINE = 0.5
DEADLINE_TOLERANCE = 2.0
SLOW_PART_INTERVAL = 0.05
SLOW_PARTS = 200  # 10 seconds of slow parts, far beyond the deadline
GZIP_HEADER_WITH_FILE_NAME = bytes([0x1F, 0x8B, 0x08, 0x08, 0, 0, 0, 0, 0, 0xFF])
ZLIB_HEADER = bytes([0x78, 0x9C])
EMPTY_STORED_DEFLATE_BLOCK = bytes([0x00, 0x00, 0x00, 0xFF, 0xFF])
REAL_SESSION = requests.Session


class FakeResponse:
    def __init__(
        self,
        status_code: int = 200,
        headers: dict[str, str] | None = None,
        chunks: list[bytes] | None = None,
    ) -> None:
        self.status_code = status_code
        self.headers = headers or {}
        self.chunks = chunks if chunks is not None else [SMALL_CONFIG]
        self.content_read = False

    @property
    def is_redirect(self) -> bool:
        return "Location" in self.headers and self.status_code in (301, 302, 303, 307, 308)

    def __enter__(self) -> FakeResponse:  # noqa: PYI034
        return self

    def __exit__(self, *args: object) -> None:
        return None

    def raise_for_status(self) -> None:
        if self.status_code >= 400:
            raise requests.exceptions.HTTPError(f"{self.status_code} Client Error")

    @property
    def raw(self) -> FakeRaw:
        self.content_read = True
        return FakeRaw(self.chunks)


class FakeRaw:
    """Serves the chunks like urllib3's read1: whatever is available, then b'' at the end."""

    def __init__(self, chunks: list[bytes]) -> None:
        self.chunks = list(chunks)
        self.closed = False

    def close(self) -> None:
        self.closed = True

    def read1(self, amt: int, decode_content: bool) -> bytes:  # noqa: ARG002
        return self.chunks.pop(0) if self.chunks else b""


class FailingRaw:
    def __init__(self, error: Exception) -> None:
        self.error = error

    def read1(self, amt: int, decode_content: bool) -> bytes:  # noqa: ARG002
        raise self.error


class SlowRaw(FakeRaw):
    """Blocks in read1 beyond any deadline, then reports the end of the content, like a connection cut off."""

    def read1(self, amt: int, decode_content: bool) -> bytes:  # noqa: ARG002
        time.sleep(DEADLINE)
        return b""


def serve_once(
    response_head: bytes,
    body_parts: list[bytes],
    interval: float,
    client_gone: threading.Event | None = None,
) -> int:
    """
    Answer one http request with the head and the body parts sent one by one; returns the port.

    With client_gone, the connection is held open after the last part until the client shuts it down.
    """
    server = socket.socket()
    server.bind(("127.0.0.1", 0))
    server.listen(1)

    def serve() -> None:
        connection, _ = server.accept()
        try:
            connection.recv(4096)
            connection.sendall(response_head)
            for part in body_parts:
                time.sleep(interval)
                connection.sendall(part)
            if client_gone is not None:
                connection.recv(1)
                client_gone.set()
        except OSError:
            pass  # the client gave up, as expected for a rejected download
        finally:
            connection.close()
            server.close()

    threading.Thread(target=serve, daemon=True).start()
    return server.getsockname()[1]


class FakeSession:
    response: FakeResponse = FakeResponse()
    get_kwargs: dict[str, Any] = {}  # noqa: RUF012

    def __init__(self) -> None:
        self.verify: bool | str = True
        self.headers: dict[str, str] = {}

    def __enter__(self) -> FakeSession:  # noqa: PYI034
        return self

    def __exit__(self, *args: object) -> None:
        return None

    def get(self, url: str, **kwargs: Any) -> FakeResponse:  # noqa: ARG002
        FakeSession.get_kwargs = kwargs
        return FakeSession.response


@pytest.fixture(autouse=True)
def fake_session(monkeypatch: pytest.MonkeyPatch) -> None:
    FakeSession.response = FakeResponse()
    FakeSession.get_kwargs = {}
    monkeypatch.setattr(fwo_file_import.requests, "Session", FakeSession)
    monkeypatch.setattr(fwo_globals, "verify_certs", True)


def read(file_name: str) -> tuple[dict[str, Any], MagicMock]:
    api_call = MagicMock()
    import_state = ImportState()
    import_state.import_file_name = file_name
    return fwo_file_import.read_file(api_call, import_state), api_call


class TestDownload:
    def test_download_streams_without_redirects_within_time_limits(self) -> None:
        config, _ = read(CONFIG_URL)

        assert config == {"ManagerSet": []}
        assert FakeSession.get_kwargs["allow_redirects"] is False
        assert FakeSession.get_kwargs["stream"] is True
        assert FakeSession.get_kwargs["timeout"] == (
            fwo_file_import.FWO_HTTP_CONNECT_TIMEOUT,
            fwo_file_import.CONFIG_FILE_READ_TIMEOUT,
        )

    def test_redirect_is_rejected_and_reported(self) -> None:
        FakeSession.response = FakeResponse(status_code=302, headers={"Location": "http://127.0.0.1:8880/"})

        with pytest.raises(
            ConfigFileNotFoundError, match=re.escape("redirect to http://127.0.0.1:8880/ is not followed")
        ):
            read(CONFIG_URL)

        assert not FakeSession.response.content_read

    def test_rejection_completes_import_with_reason(self) -> None:
        FakeSession.response = FakeResponse(status_code=302, headers={"Location": "https://elsewhere.example/"})
        api_call = MagicMock()
        import_state = ImportState()
        import_state.import_file_name = CONFIG_URL

        with pytest.raises(ConfigFileNotFoundError):
            fwo_file_import.read_file(api_call, import_state)

        reported_error = api_call.complete_import.call_args.args[1]
        assert isinstance(reported_error, ConfigFileRejectedError)

    def test_declared_size_above_limit_is_rejected_before_reading(self) -> None:
        too_large = str(fwo_file_import.CONFIG_FILE_MAX_BYTES + 1)
        FakeSession.response = FakeResponse(headers={"Content-Length": too_large})

        with pytest.raises(ConfigFileNotFoundError, match="exceeds the limit"):
            read(CONFIG_URL)

        assert not FakeSession.response.content_read

    def test_http_error_is_reported(self) -> None:
        FakeSession.response = FakeResponse(status_code=404)

        with pytest.raises(ConfigFileNotFoundError, match="404"):
            read(CONFIG_URL)


class TestReadBounded:
    def test_returns_content_within_limits(self) -> None:
        assert fwo_file_import.read_bounded([b"ab", b"cd"], max_bytes=4) == b"abcd"

    def test_aborts_just_above_size_limit(self) -> None:
        chunks_read: list[bytes] = []

        def chunks() -> Any:
            for chunk in (b"ab", b"cd", b"e", b"never read"):
                chunks_read.append(chunk)
                yield chunk

        with pytest.raises(ConfigFileRejectedError, match="exceeds the limit of 4 bytes"):
            fwo_file_import.read_bounded(chunks(), max_bytes=4)

        assert b"never read" not in chunks_read

    def test_aborts_after_total_deadline(self, monkeypatch: pytest.MonkeyPatch) -> None:
        clock = iter([0.0, 5.0, 11.0])
        monkeypatch.setattr(fwo_file_import.time, "monotonic", lambda: next(clock))

        with pytest.raises(ConfigFileRejectedError, match="10 seconds"):
            fwo_file_import.read_bounded([b"a", b"b"], max_bytes=100, total_timeout=10)


class TestReceivedChunks:
    def test_total_deadline_is_checked_while_a_slow_server_sends(self) -> None:
        head = f"HTTP/1.1 200 OK\r\nContent-Length: {TRICKLE_BYTES}\r\n\r\n".encode()
        port = serve_once(head, [b"x"] * TRICKLE_BYTES, TRICKLE_INTERVAL)
        started = time.monotonic()

        with requests.get(f"http://127.0.0.1:{port}/", stream=True, timeout=(5, 5)) as response:  # noqa: SIM117
            with pytest.raises(ConfigFileRejectedError, match="exceeds the limit"):
                fwo_file_import.read_bounded(
                    fwo_file_import.iter_received_chunks(response.raw), total_timeout=TRICKLE_TOTAL_TIMEOUT
                )

        assert time.monotonic() - started < TRICKLE_MAX_SECONDS

    def test_compressed_content_is_decoded(self) -> None:
        body = gzip.compress(SMALL_CONFIG)
        head = f"HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: {len(body)}\r\n\r\n".encode()
        port = serve_once(head, [body], 0)

        with requests.get(f"http://127.0.0.1:{port}/", stream=True, timeout=(5, 5)) as response:
            content = fwo_file_import.read_bounded(fwo_file_import.iter_received_chunks(response.raw))

        assert content == SMALL_CONFIG

    @pytest.mark.parametrize(
        ("error", "expected"),
        [
            (urllib3.exceptions.DecodeError("bad gzip"), requests.exceptions.ContentDecodingError),
            (
                urllib3.exceptions.ReadTimeoutError(urllib3.HTTPConnectionPool("127.0.0.1"), "/", "read timed out"),
                requests.exceptions.ConnectionError,
            ),
            (urllib3.exceptions.ProtocolError("connection broken"), requests.exceptions.ChunkedEncodingError),
        ],
    )
    def test_urllib3_errors_are_raised_as_requests_errors(self, error: Exception, expected: type[Exception]) -> None:
        with pytest.raises(expected):
            list(fwo_file_import.iter_received_chunks(FailingRaw(error)))  # type: ignore[arg-type]


@pytest.fixture
def real_session(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(fwo_file_import.requests, "Session", REAL_SESSION)


def slow_server(parts: list[bytes], content_encoding: str = "") -> str:
    encoding_header = f"Content-Encoding: {content_encoding}\r\n" if content_encoding else ""
    head = f"HTTP/1.1 200 OK\r\n{encoding_header}Connection: close\r\n\r\n".encode()
    port = serve_once(head, parts, SLOW_PART_INTERVAL)
    return f"http://127.0.0.1:{port}/"


def assert_download_aborts_at_deadline(url: str) -> None:
    started = time.monotonic()

    with pytest.raises(ConfigFileRejectedError, match=re.escape(f"exceeds the limit of {DEADLINE} seconds")):
        fwo_file_import.download_config_file(url, total_timeout=DEADLINE)

    assert time.monotonic() - started < DEADLINE + DEADLINE_TOLERANCE


@pytest.mark.usefixtures("real_session")
class TestDownloadDeadline:
    def test_slow_plain_download_aborts_at_deadline(self) -> None:
        assert_download_aborts_at_deadline(slow_server([b"x"] * SLOW_PARTS))

    def test_gzip_header_that_never_ends_aborts_at_deadline(self) -> None:
        url = slow_server([GZIP_HEADER_WITH_FILE_NAME] + [b"a"] * SLOW_PARTS, "gzip")

        assert_download_aborts_at_deadline(url)

    def test_deflate_without_content_aborts_at_deadline(self) -> None:
        url = slow_server([ZLIB_HEADER] + [EMPTY_STORED_DEFLATE_BLOCK] * SLOW_PARTS, "deflate")

        assert_download_aborts_at_deadline(url)

    def test_stalled_download_is_cut_off_at_deadline(self) -> None:
        client_gone = threading.Event()
        head = f"HTTP/1.1 200 OK\r\nContent-Length: {len(SMALL_CONFIG)}\r\n\r\n".encode()
        port = serve_once(head, [SMALL_CONFIG[:1]], 0, client_gone)

        assert_download_aborts_at_deadline(f"http://127.0.0.1:{port}/")

        assert client_gone.wait(DEADLINE_TOLERANCE)

    def test_expired_download_is_reported_as_failed_import(self, monkeypatch: pytest.MonkeyPatch) -> None:
        download = functools.partial(fwo_file_import.download_config_file, total_timeout=DEADLINE)
        monkeypatch.setattr(fwo_file_import, "download_config_file", download)

        api_call = MagicMock()
        import_state = ImportState()
        import_state.import_file_name = slow_server([b"x"] * SLOW_PARTS)

        with pytest.raises(ConfigFileNotFoundError, match="exceeds the limit"):
            fwo_file_import.read_file(api_call, import_state)

        reported_error = api_call.complete_import.call_args.args[1]
        assert isinstance(reported_error, ConfigFileRejectedError)

    def test_plain_and_compressed_downloads_succeed(self) -> None:
        for body, encoding in ((SMALL_CONFIG, ""), (gzip.compress(SMALL_CONFIG), "gzip")):
            encoding_header = f"Content-Encoding: {encoding}\r\n" if encoding else ""
            head = f"HTTP/1.1 200 OK\r\n{encoding_header}Content-Length: {len(body)}\r\n\r\n".encode()
            port = serve_once(head, [body], 0)

            assert fwo_file_import.download_config_file(f"http://127.0.0.1:{port}/") == SMALL_CONFIG

    @pytest.mark.parametrize("connection_header", ["", "Connection: close\r\n"])
    def test_streamed_response_provides_its_socket(self, connection_header: str) -> None:
        head = f"HTTP/1.1 200 OK\r\n{connection_header}Content-Length: {len(SMALL_CONFIG)}\r\n\r\n".encode()
        port = serve_once(head, [], 0, threading.Event())

        with requests.get(f"http://127.0.0.1:{port}/", stream=True, timeout=(5, 5)) as response:
            sock = fwo_file_import.duplicate_response_socket(response.raw)

        assert isinstance(sock, socket.socket)
        sock.close()

    def test_stalled_download_closed_by_server_after_response_is_cut_off(self) -> None:
        client_gone = threading.Event()
        head = f"HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: {len(SMALL_CONFIG)}\r\n\r\n".encode()
        port = serve_once(head, [SMALL_CONFIG[:1]], 0, client_gone)

        assert_download_aborts_at_deadline(f"http://127.0.0.1:{port}/")

        assert client_gone.wait(DEADLINE_TOLERANCE)


class TestDeadlineWatchdog:
    def test_finished_download_is_not_cut_off(self) -> None:
        raw = FakeRaw([SMALL_CONFIG])

        assert fwo_file_import.read_within_deadline(raw, DEADLINE) == SMALL_CONFIG  # type: ignore[arg-type]
        time.sleep(DEADLINE * 2)

        assert not raw.closed

    def test_deadline_firing_after_download_finished_is_ignored(self) -> None:
        raw = FakeRaw([])

        with fwo_file_import.DownloadDeadline(raw, DEADLINE_TOLERANCE) as deadline:  # type: ignore[arg-type]
            pass
        deadline.expire()

        assert not deadline.expired.is_set()
        assert not raw.closed

    def test_response_read_from_a_file_has_no_socket(self, tmp_path: Path) -> None:
        with (tmp_path / "body").open("wb") as body:
            raw = MagicMock()
            raw.fileno.return_value = body.fileno()

            assert fwo_file_import.duplicate_response_socket(raw) is None

    def test_response_without_socket_is_closed_at_deadline(self) -> None:
        raw = FakeRaw([])

        with fwo_file_import.DownloadDeadline(raw, 0.01) as deadline:  # type: ignore[arg-type]
            assert deadline.expired.wait(DEADLINE_TOLERANCE)

        assert raw.closed

    def test_content_ending_after_deadline_is_rejected_not_truncated(self) -> None:
        with pytest.raises(ConfigFileRejectedError, match=re.escape("exceeds the limit of 0.01 seconds")):
            fwo_file_import.read_within_deadline(SlowRaw([]), 0.01)  # type: ignore[arg-type]

    def test_errors_before_deadline_are_raised_unchanged(self) -> None:
        error = urllib3.exceptions.ProtocolError("connection broken")

        with pytest.raises(requests.exceptions.ChunkedEncodingError):
            fwo_file_import.read_within_deadline(FailingRaw(error), DEADLINE)  # type: ignore[arg-type]

    def test_errors_after_deadline_are_raised_as_rejection(self) -> None:
        class FailingAfterDeadline(FakeRaw):
            def read1(self, amt: int, decode_content: bool) -> bytes:  # noqa: ARG002
                time.sleep(DEADLINE)
                raise urllib3.exceptions.ProtocolError("connection shut down")

        with pytest.raises(ConfigFileRejectedError, match="exceeds the limit"):
            fwo_file_import.read_within_deadline(FailingAfterDeadline([]), 0.01)  # type: ignore[arg-type]


class TestLocalFile:
    def test_reads_local_file_with_and_without_uri_prefix(self, tmp_path: Path) -> None:
        config_file = tmp_path / "config.json"
        config_file.write_text(json.dumps({"ManagerSet": []}))

        assert read(str(config_file))[0] == {"ManagerSet": []}
        assert read(f"file://{config_file}")[0] == {"ManagerSet": []}

    def test_local_file_above_limit_is_rejected(self, tmp_path: Path) -> None:
        config_file = tmp_path / "config.json"
        config_file.write_text(json.dumps({"ManagerSet": []}))

        with pytest.raises(ConfigFileRejectedError, match="exceeds the limit of 4 bytes"):
            fwo_file_import.read_local_config_file(str(config_file), max_bytes=4)

    def test_empty_file_name_returns_empty_config(self) -> None:
        assert read("")[0] == {}

    def test_invalid_json_is_reported(self, tmp_path: Path) -> None:
        config_file = tmp_path / "config.json"
        config_file.write_text("not json")

        with pytest.raises(ConfigFileNotFoundError, match="unspecified error"):
            read(str(config_file))

    def test_pipe_is_rejected_without_blocking(self, tmp_path: Path) -> None:
        pipe = tmp_path / "config.json"
        os.mkfifo(pipe)

        with pytest.raises(ConfigFileRejectedError, match="is not a regular file"):
            fwo_file_import.read_local_config_file(str(pipe))

    def test_device_behind_a_symlink_is_rejected(self, tmp_path: Path) -> None:
        link = tmp_path / "config.json"
        link.symlink_to("/dev/zero")

        with pytest.raises(ConfigFileNotFoundError, match="is not a regular file"):
            read(str(link))

    def test_missing_file_is_reported(self, tmp_path: Path) -> None:
        with pytest.raises(ConfigFileNotFoundError, match="unspecified error"):
            read(str(tmp_path / "missing.json"))

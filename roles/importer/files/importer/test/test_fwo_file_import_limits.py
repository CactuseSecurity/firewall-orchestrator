from __future__ import annotations

import json
import re
from typing import TYPE_CHECKING, Any
from unittest.mock import MagicMock

import fwo_file_import
import fwo_globals
import pytest
import requests
from fwo_exceptions import ConfigFileNotFoundError, ConfigFileRejectedError
from models.import_state import ImportState

if TYPE_CHECKING:
    from pathlib import Path

CONFIG_URL = "https://config.example/config.json"
SMALL_CONFIG = b'{"ManagerSet": []}'


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

    def iter_content(self, chunk_size: int) -> list[bytes]:  # noqa: ARG002
        self.content_read = True
        return self.chunks


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

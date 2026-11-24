"""
read config from file
"""

import contextlib
import json
import socket
import threading
import time
import traceback
from collections.abc import Iterable, Iterator
from pathlib import Path
from types import TracebackType
from typing import Any, cast

import fwo_globals
import requests
import urllib3
from fwo_api_call import FwoApiCall
from fwo_const import (
    CONFIG_FILE_CHUNK_SIZE,
    CONFIG_FILE_MAX_BYTES,
    CONFIG_FILE_READ_TIMEOUT,
    CONFIG_FILE_TOTAL_TIMEOUT,
    FWO_HTTP_CONNECT_TIMEOUT,
)
from fwo_exceptions import ConfigFileNotFoundError, ConfigFileRejectedError, FwoImporterError
from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import (
    FwConfigManagerListController,
)
from models.import_state import ImportState
from requests.adapters import HTTPAdapter
from urllib3.connection import HTTPConnection

"""
    supported input formats:

    normalized (new from v9 onwards) --> dicts with uid as id

    {
        "ConfigFormat": "NORMALIZED",
        "managers": [
            {
            "ManagerUid": "6ae3760206b9bfbd2282b5964f6ea07869374f427533c72faa7418c28f7a77f2",
            "ManagerName": "MGM NAME",
            "IsGlobal": false,
            "Configs": {
                "action": "INSERT",
                "network_objects": [
                    {

                    }
                }
            }
        ]
    }

    output formats:

    a) NORMALIZED:

    check point
    {
        "users": {},
        "object_tables": [
            {
            "object_type": "hosts",
            "object_chunks": [
                {
                "objects": [
    }


"""


def read_json_config_from_file(fwo_api_call: FwoApiCall, import_state: ImportState) -> FwConfigManagerListController:
    config_json = read_file(fwo_api_call, import_state)

    # try to convert normalized config from file to config object
    try:
        manager_list = FwConfigManagerListController(**config_json)  # TYPING: use model load
        if len(manager_list.ManagerSet) == 0:
            FWOLogger.warning(
                f"read a config file without manager sets from {import_state.import_file_name}, trying native config"
            )
            manager_list.native_config = config_json
        return manager_list
    except Exception:  # legacy stuff from here
        FWOLogger.info(f"could not serialize config {traceback.format_exc()!s}")
        raise FwoImporterError(f"could not serialize config {import_state.import_file_name}")


def read_file(fwo_api_call: FwoApiCall, import_state: ImportState) -> dict[str, Any]:
    if import_state.import_file_name == "":
        return {}
    try:
        if import_state.import_file_name.startswith("http://") or import_state.import_file_name.startswith(
            "https://"
        ):  # get conf file via http(s)
            return json.loads(download_config_file(import_state.import_file_name))
        # reading from local file, without the file uri identifier if given
        return read_local_config_file(import_state.import_file_name.removeprefix("file://"))
    except ConfigFileRejectedError as e:
        FWOLogger.error(f"rejected config file {import_state.import_file_name}: {e.message}")
        fwo_api_call.complete_import(import_state, e)
        raise ConfigFileNotFoundError(e.message) from None
    except requests.exceptions.RequestException as e:
        FWOLogger.error(f"got error while trying to read config file from URL {import_state.import_file_name}: {e!s}")
        fwo_api_call.complete_import(import_state, e)
        raise ConfigFileNotFoundError(str(e)) from None
    except Exception as e:
        FWOLogger.error("unspecified error while reading config file: " + str(traceback.format_exc()))
        fwo_api_call.complete_import(import_state, e)
        raise ConfigFileNotFoundError(f"unspecified error while reading config file {import_state.import_file_name}")


def download_config_file(url: str, total_timeout: float = CONFIG_FILE_TOTAL_TIMEOUT) -> bytes:
    """
    Download a config file without following redirects and within the size and time limits.

    The total time limit covers the proxy and TLS handshakes, the response headers and the body, also while a read is
    blocked, e.g. by a server that stops sending or by compressed data that never decodes to any content (urllib3 keeps
    reading in that case without returning). Resolving the host name and establishing the TCP connection cannot be
    interrupted: they are limited by the resolver and by FWO_HTTP_CONNECT_TIMEOUT per address, and a connection
    established after the deadline is cut off at once.

    A download cut off at the deadline is rejected rather than returned truncated, whatever error the interrupted read
    raised.
    """
    with DownloadDeadline(total_timeout) as deadline:
        try:
            content = fetch_config_file(url, deadline)
        except Exception as error:
            if not deadline.expired.is_set():
                raise
            raise deadline.rejection() from error
        if deadline.expired.is_set():
            raise deadline.rejection()
        return content


def fetch_config_file(url: str, deadline: "DownloadDeadline") -> bytes:
    """
    Fetch a config file over connections the deadline can cut off.

    A redirect is rejected rather than followed, so the importer only ever connects to the
    configured host; the configured URL has to point to the file itself.
    """
    with requests.Session() as session:
        adapter = DeadlineAdapter(deadline)
        session.mount("http://", adapter)
        session.mount("https://", adapter)
        session.headers.update({"Content-Type": "application/json"})
        # set per management by the import state; fail closed when it was never set
        session.verify = (
            fwo_globals.verify_certs
            if fwo_globals.verify_certs is not None
            else fwo_globals.resolve_requests_verify(check_certificates=True)
        )
        with session.get(
            url,
            timeout=(FWO_HTTP_CONNECT_TIMEOUT, CONFIG_FILE_READ_TIMEOUT),
            stream=True,
            allow_redirects=False,
        ) as response:
            if response.is_redirect:
                raise ConfigFileRejectedError(
                    f"HTTP {response.status_code} redirect to {response.headers.get('Location', '')} is not followed, "
                    "configure the final URL of the config file instead"
                )
            response.raise_for_status()
            declared_length = response.headers.get("Content-Length", "")
            if declared_length.isdigit() and int(declared_length) > CONFIG_FILE_MAX_BYTES:
                raise ConfigFileRejectedError(
                    f"declared size of {declared_length} bytes exceeds the limit of {CONFIG_FILE_MAX_BYTES} bytes"
                )
            return read_bounded(iter_received_chunks(response.raw), total_timeout=deadline.total_timeout)


class DownloadDeadline:
    """
    Shuts the connections of a download down when its total time limit is exceeded.

    Shutting a socket down wakes a read blocked on it in another thread, which closing it does not reliably do.
    Duplicates of the sockets are shut down, as urllib3 and http.client hand the original over to the TLS layer or drop
    it while the download is still running. The TLS state of a connection is left to the reading thread.
    """

    def __init__(self, total_timeout: float) -> None:
        self.total_timeout = total_timeout
        self.expired = threading.Event()
        self._sockets: list[socket.socket] = []
        self._lock = threading.Lock()
        self._finished = False
        self._timer = threading.Timer(total_timeout, self.expire)
        self._timer.daemon = True

    def __enter__(self) -> "DownloadDeadline":  # noqa: PYI034 - typing.Self needs Python 3.11
        self._timer.start()
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc_value: BaseException | None,
        traceback_: TracebackType | None,
    ) -> None:
        # under the lock, so no socket is shut down after the download has finished with it
        with self._lock:
            self._finished = True
            self._timer.cancel()
            for sock in self._sockets:
                sock.close()
            self._sockets.clear()

    def attach(self, sock: socket.socket) -> None:
        """Cut the connection of this socket off at the deadline, or at once if the deadline has passed already."""
        with self._lock:
            if self._finished:
                return
            duplicate = socket.fromfd(sock.fileno(), sock.family, sock.type)
            self._sockets.append(duplicate)
            if self.expired.is_set():
                shut_down(duplicate)

    def expire(self) -> None:
        """Cut the download off, unless it has finished already."""
        with self._lock:
            if self._finished:
                return
            self.expired.set()
            for sock in self._sockets:
                shut_down(sock)

    def rejection(self) -> ConfigFileRejectedError:
        return ConfigFileRejectedError(f"config file download exceeds the limit of {self.total_timeout} seconds")


class DeadlineAdapter(HTTPAdapter):
    """Attaches every connection of a session to a deadline, whether it goes to the server directly or via a proxy."""

    def __init__(self, deadline: DownloadDeadline) -> None:
        super().__init__()
        self.deadline = deadline

    def get_connection_with_tls_context(
        self,
        request: requests.PreparedRequest,
        verify: bool | str,
        proxies: dict[str, str] | None = None,
        cert: tuple[str, str] | str | None = None,
    ) -> urllib3.HTTPConnectionPool:
        pool = super().get_connection_with_tls_context(request, verify, proxies, cert)
        connection_class = cast("type[HTTPConnection]", pool.ConnectionCls)
        # urllib3 types the attribute with a protocol its own connection classes do not match
        pool.ConnectionCls = cast("Any", with_deadline(connection_class, self.deadline))
        return pool


def with_deadline(connection_class: type[HTTPConnection], deadline: DownloadDeadline) -> type[HTTPConnection]:
    """
    Return a connection class that attaches its socket to the deadline as soon as the TCP connection is established.

    urllib3 has no public hook at this point: connect() only returns after the proxy and TLS handshakes, which the
    server can delay as well.
    """

    class DeadlineConnection(connection_class):
        def _new_conn(self) -> socket.socket:
            sock = super()._new_conn()
            deadline.attach(sock)
            return sock

    return DeadlineConnection


def shut_down(sock: socket.socket) -> None:
    # an OSError means the connection is closed already, so no read can be blocked on it
    with contextlib.suppress(OSError):
        sock.shutdown(socket.SHUT_RDWR)


def iter_received_chunks(raw: urllib3.response.BaseHTTPResponse) -> Iterator[bytes]:
    """
    Yield the decompressed content of a streamed response as soon as any of it is received.

    Reading fixed-size chunks would wait until a whole chunk has arrived, so a server sending a byte now and then
    could keep a download running far beyond the total time limit. urllib3 errors are raised as the requests
    exceptions that reading through requests would raise.
    """
    try:
        while chunk := raw.read1(CONFIG_FILE_CHUNK_SIZE, decode_content=True):
            yield chunk
    except urllib3.exceptions.DecodeError as error:
        raise requests.exceptions.ContentDecodingError(error) from error
    except urllib3.exceptions.ReadTimeoutError as error:
        raise requests.exceptions.ConnectionError(error) from error
    except urllib3.exceptions.HTTPError as error:
        raise requests.exceptions.ChunkedEncodingError(error) from error


def read_bounded(
    chunks: Iterable[bytes],
    max_bytes: int = CONFIG_FILE_MAX_BYTES,
    total_timeout: float = CONFIG_FILE_TOTAL_TIMEOUT,
) -> bytes:
    """
    Collect the chunks of a download, aborting as soon as the size or time limit is exceeded.

    The chunks are decompressed already, so a compressed response is limited by its real size. The limits are checked
    after every chunk, so the chunks must be yielded as they are received (see iter_received_chunks).
    """
    deadline = time.monotonic() + total_timeout
    content = bytearray()
    for chunk in chunks:
        content.extend(chunk)
        if len(content) > max_bytes:
            raise ConfigFileRejectedError(f"config file exceeds the limit of {max_bytes} bytes")
        if time.monotonic() > deadline:
            raise ConfigFileRejectedError(f"config file download exceeds the limit of {total_timeout} seconds")
    return bytes(content)


def read_local_config_file(filename: str, max_bytes: int = CONFIG_FILE_MAX_BYTES) -> dict[str, Any]:
    """
    Read a config file from the local file system, refusing anything but a regular file and files above the size limit.

    Devices and pipes report no size (/dev/zero never ends, a pipe without writer blocks), and a file can grow after
    its size was checked, so at most one byte more than the limit is read instead of trusting the reported size.
    """
    path = Path(filename)
    if path.exists() and not path.is_file():
        raise ConfigFileRejectedError(f"{filename} is not a regular file")
    with path.open("rb") as config_file:
        content = config_file.read(max_bytes + 1)
    if len(content) > max_bytes:
        raise ConfigFileRejectedError(f"config file exceeds the limit of {max_bytes} bytes")
    return json.loads(content)

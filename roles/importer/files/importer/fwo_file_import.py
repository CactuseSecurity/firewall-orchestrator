"""
read config from file
"""

import contextlib
import json
import os
import socket
import threading
import time
import traceback
from collections.abc import Iterable, Iterator
from pathlib import Path
from types import TracebackType
from typing import Any

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

    A redirect is rejected rather than followed, so the importer only ever connects to the
    configured host; the configured URL has to point to the file itself.
    """
    with requests.Session() as session:
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
            return read_within_deadline(response.raw, total_timeout)


def read_within_deadline(raw: urllib3.response.BaseHTTPResponse, total_timeout: float) -> bytes:
    """
    Read the body of a streamed response, cutting the connection off once the total time limit is exceeded.

    The limit is also enforced while a read is blocked, e.g. by a server that stops sending or by compressed data that
    never decodes to any content (urllib3 keeps reading in that case without returning). A download cut off at the
    deadline is rejected rather than returned truncated, whatever error the interrupted read raised.
    """
    with DownloadDeadline(raw, total_timeout) as deadline:
        try:
            content = read_bounded(iter_received_chunks(raw), total_timeout=total_timeout)
        except Exception as error:
            if not deadline.expired.is_set():
                raise
            raise deadline.rejection() from error
        if deadline.expired.is_set():
            raise deadline.rejection()
        return content


class DownloadDeadline:
    """
    Shuts the connection of a streamed response down when the total time limit of its download is exceeded.

    Shutting the socket down wakes a read blocked on it in another thread, which closing it does not reliably do.
    A duplicate of the socket is shut down, as http.client drops its own reference for connections the server closes
    after the response. The duplicate leaves the TLS state of an encrypted connection to the reading thread.
    """

    def __init__(self, raw: urllib3.response.BaseHTTPResponse, total_timeout: float) -> None:
        self.raw = raw
        self.total_timeout = total_timeout
        self.expired = threading.Event()
        self.sock = duplicate_response_socket(raw)
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
        # under the lock, so the socket is not shut down after the download has finished with it
        with self._lock:
            self._finished = True
            self._timer.cancel()
            if self.sock is not None:
                self.sock.close()

    def expire(self) -> None:
        """Cut the download off, unless it has finished already."""
        with self._lock:
            if self._finished:
                return
            self.expired.set()
            if self.sock is None:
                FWOLogger.warning("config file download has no socket to shut down, closing the response instead")
                self.raw.close()
                return
            # an OSError means the connection is closed already, so no read can be blocked on it
            with contextlib.suppress(OSError):
                self.sock.shutdown(socket.SHUT_RDWR)

    def rejection(self) -> ConfigFileRejectedError:
        return ConfigFileRejectedError(f"config file download exceeds the limit of {self.total_timeout} seconds")


def duplicate_response_socket(raw: urllib3.response.BaseHTTPResponse) -> socket.socket | None:
    """
    Return a duplicate of the socket a streamed response is read from, or None if it has no socket (any more).

    Shutting the duplicate down shuts the connection down for the response as well. The caller closes the duplicate.
    """
    try:
        duplicate_fd = os.dup(raw.fileno())
    except (OSError, ValueError, AttributeError):
        return None
    try:
        return socket.socket(fileno=duplicate_fd)
    except OSError:
        os.close(duplicate_fd)  # not a socket, so nothing a shutdown could wake
        return None


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

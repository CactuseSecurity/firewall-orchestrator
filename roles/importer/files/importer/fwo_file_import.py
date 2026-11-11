"""
read config from file
"""

import json
import time
import traceback
from collections.abc import Iterable, Iterator
from pathlib import Path
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


def download_config_file(url: str) -> bytes:
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
            return read_bounded(iter_received_chunks(response.raw))


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

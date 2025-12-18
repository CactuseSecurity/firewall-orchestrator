"""
read config from file and convert to non-legacy format (in case of legacy input)
"""

import json
import traceback
from typing import Any

import fwo_globals
import requests
from fwconfig_base import ConfFormat
from fwo_api_call import FwoApiCall
from fwo_exceptions import ConfigFileNotFoundError, FwoImporterError
from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.import_state import ImportState

"""
    supported input formats:

    1) legacy normalized old:

    {
        "network_objects": [x,y],
        "service_objects": [a,b,c],
        ...
        "rules": [x,y,z]
    }

    2) normalized (new from v9 onwards) --> dicts with uid as id

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

    3) native legacy formats

    these will we wrapped with the following:

    TODO: need to detect native format from file

    {
        "ConfigFormat": "<NATIVE_FORMAT>_LEGACY",
        "config": configJson
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
            manager_list.ConfigFormat = detect_legacy_format(config_json)
        return manager_list
    except Exception:  # legacy stuff from here
        FWOLogger.info(f"could not serialize config {traceback.format_exc()!s}")
        raise FwoImporterError(f"could not serialize config {import_state.import_file_name} - trying legacy formats")


def detect_legacy_format(config_json: dict[str, Any]) -> ConfFormat:
    result = ConfFormat.NORMALIZED_LEGACY

    if "object_tables" in config_json:
        result = ConfFormat.CHECKPOINT_LEGACY
    elif "domains" in config_json:
        result = ConfFormat.FORTIMANAGER

    return result


def read_file(fwo_api_call: FwoApiCall, import_state: ImportState) -> dict[str, Any]:
    config_json: dict[str, Any] = {}
    r = None
    if import_state.import_file_name == "":
        return config_json
    try:
        if import_state.import_file_name.startswith("http://") or import_state.import_file_name.startswith(
            "https://"
        ):  # get conf file via http(s)
            session = requests.Session()
            session.headers = {"Content-Type": "application/json"}
            session.verify = fwo_globals.verify_certs
            r = session.get(
                import_state.import_file_name,
            )
            if r.ok:
                return json.loads(r.text)
            r.raise_for_status()
        else:  # reading from local file
            if import_state.import_file_name.startswith("file://"):  # remove file uri identifier
                filename = import_state.import_file_name[7:]
            else:
                filename = import_state.import_file_name
            with open(filename) as json_file:
                config_json = json.load(json_file)
    except requests.exceptions.RequestException as e:
        if r is not None:
            FWOLogger.error(
                f"got HTTP status code{r.status_code!s} while trying to read config file from URL {import_state.import_file_name}"
            )
        else:
            FWOLogger.error(f"got error while trying to read config file from URL {import_state.import_file_name}")

        fwo_api_call.complete_import(import_state, e)
        raise ConfigFileNotFoundError(str(e)) from None
    except Exception as e:
        FWOLogger.error("unspecified error while reading config file: " + str(traceback.format_exc()))
        fwo_api_call.complete_import(import_state, e)
        raise ConfigFileNotFoundError(f"unspecified error while reading config file {import_state.import_file_name}")

    return config_json

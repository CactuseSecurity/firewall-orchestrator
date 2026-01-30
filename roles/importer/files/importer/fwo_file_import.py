"""
read config from file
"""

import json
import traceback
from typing import Any, cast

import fwo_globals
import requests
from fwo_api_call import FwoApiCall
from fwo_exceptions import ConfigFileNotFoundError, FwoImporterError
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
    _normalize_rule_fields(config_json)

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


def _normalize_rule_fields(config_json: dict[str, Any]) -> None:
    """
    Normalize legacy/alternate rule field names before validation.
    """
    manager_set = config_json.get("ManagerSet")
    if not isinstance(manager_set, list):
        return
    manager_items: list[Any] = cast("list[Any]", manager_set)
    for manager_any in manager_items:
        if not isinstance(manager_any, dict):
            continue
        manager = cast("dict[str, Any]", manager_any)
        configs = manager.get("Configs")
        if not isinstance(configs, list):
            continue
        config_items: list[Any] = cast("list[Any]", configs)
        for config_any in config_items:
            if not isinstance(config_any, dict):
                continue
            config = cast("dict[str, Any]", config_any)
            rulebases = config.get("rulebases")
            if not isinstance(rulebases, list):
                continue
            rulebase_items: list[Any] = cast("list[Any]", rulebases)
            for rulebase_any in rulebase_items:
                if not isinstance(rulebase_any, dict):
                    continue
                rulebase = cast("dict[str, Any]", rulebase_any)
                rules = rulebase.get("rules")
                if not isinstance(rules, dict):
                    continue
                rule_items: list[Any] = list(cast("dict[Any, Any]", rules).values())
                for rule_any in rule_items:
                    if not isinstance(rule_any, dict):
                        continue
                    rule = cast("dict[str, Any]", rule_any)
                    if "rule_last_change_admin" in rule and "last_change_admin" not in rule:
                        rule["last_change_admin"] = rule.pop("rule_last_change_admin")


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

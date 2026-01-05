# library for API get functions
import json
from typing import Any

import fwo_globals
import requests
from fw_modules.fortiosmanagementREST import fos_const
from fwo_exceptions import FwApiCallFailedError
from fwo_log import FWOLogger

HTTP_OK = 200


def fortios_api_call(api_url: str) -> dict[str, Any]:
    """
    Makes a GET request to the FortiOS REST API and returns the JSON response.

    Args:
        api_url (str): The full URL for the API endpoint.

    Returns:
        dict[str, Any]: The JSON response from the API as a dictionary.

    """
    request_headers = {"Content-Type": "application/json"}

    response = requests.get(api_url, headers=request_headers, verify=fwo_globals.verify_certs)
    if response.status_code != HTTP_OK:
        raise FwApiCallFailedError(
            "error while sending api_call to url '"
            + str(api_url)
            + "' with headers: '"
            + json.dumps(request_headers, indent=2)
            + ", response code: "
            + str(response.status_code)
            + ", response text: "
            + response.text
        )
    result_json = response.json()
    if "results" not in result_json:
        raise FwApiCallFailedError(
            "error while sending api_call to url '"
            + str(api_url)
            + "' with headers: '"
            + json.dumps(request_headers, indent=2)
            + ", results="
            + json.dumps(response.json()["results"], indent=2)
        )

    FWOLogger.debug("api_call to url '" + str(api_url) + "' with headers: '" + json.dumps(request_headers, indent=2), 3)

    return result_json["results"]


def update_config_with_fortios_api_call(native_config: dict[str, Any], api_url: str, result_name: str):
    full_result: list[Any] = []
    result = fortios_api_call(api_url)
    full_result.extend(result)
    if result_name in native_config:  # data already exists - extend
        native_config[result_name].extend(full_result)
    else:
        native_config.update({result_name: full_result})


def get_native_config(fm_api_url: str, sid: str) -> dict[str, Any]:
    """
    Gets the native configuration from the FortiOS REST API.

    Args:
        fm_api_url (str): The base URL for the FortiOS API.
        sid (str): The session ID or access token for authentication.

    Returns:
        dict[str, Any]: The native configuration as a dictionary.

    """
    native_config: dict[str, Any] = {}

    for object_type in fos_const.NW_OBJ_TYPES:
        update_config_with_fortios_api_call(
            native_config, fm_api_url + "/cmdb/" + object_type + "?access_token=" + sid, "nw_obj_" + object_type
        )

    # get service objects:
    for object_type in fos_const.SVC_OBJ_TYPES:
        update_config_with_fortios_api_call(
            native_config,
            fm_api_url + "/cmdb/" + object_type + "?access_token=" + sid,
            "svc_obj_" + object_type,
        )

    # get user objects:
    for object_type in fos_const.USER_OBJ_TYPES:
        update_config_with_fortios_api_call(
            native_config,
            fm_api_url + "/cmdb/" + object_type + "?access_token=" + sid,
            "user_obj_" + object_type,
        )

    add_zone_if_missing(native_config, "global")

    initialize_rulebases(native_config)

    update_config_with_fortios_api_call(
        native_config, fm_api_url + "/cmdb/firewall/policy" + "?access_token=" + sid, "rules"
    )

    process_zones(native_config)

    # TODO: get nat rules

    return native_config


def normalize_zone_name(zone_name: str) -> str:
    if zone_name == "any":
        return "global"
    return zone_name


def add_zone_if_missing(native_config: dict[str, Any], zone_name: str) -> str:
    """
    Adds a zone to the native configuration if it is missing.

    Args:
        native_config (dict[str, Any]): The native configuration dictionary.
        zone_name (str): The name of the zone to add.

    """
    zone_name = normalize_zone_name(zone_name)

    if "zone_objects" not in native_config:  # no zones yet? add empty zone_objects array
        native_config.update({"zone_objects": []})
    if not any(z for z in native_config["zone_objects"] if z.get("zone_name") == zone_name):
        # zone not found - add it
        native_config["zone_objects"].append({"zone_name": zone_name})

    return zone_name


def initialize_rulebases(raw_config: dict[str, Any]):
    for scope in fos_const.RULE_SCOPE:
        if scope not in raw_config:
            raw_config.update({scope: []})


def process_zones(native_config: dict[str, Any]) -> None:
    """
    Processes zones appearing in rules.

    Args:
        native_config (dict[str, Any]): The native configuration dictionary.

    """
    for obj_type in fos_const.NW_OBJ_TYPES:
        for obj in native_config.get(obj_type, []):
            if obj.get("associated-interface"):
                obj["associated-interface"] = [
                    add_zone_if_missing(native_config, iface) for iface in obj["associated-interface"]
                ]
    for rule in native_config.get("rules", []):
        if rule.get("srcintf"):
            rule["srcintf"] = add_zone_if_missing(native_config, rule["srcintf"])
        if rule.get("dstintf"):
            rule["dstintf"] = add_zone_if_missing(native_config, rule["dstintf"])

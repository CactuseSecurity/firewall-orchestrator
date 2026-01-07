# library for API get functions
import json
from typing import Any, TypeVar

import fwo_globals
import requests
from fw_modules.fortiosmanagementREST.fos_models import (
    FortiOSConfig,
    NwObjAddress,
    NwObjAddress6,
    NwObjAddrGrp,
    NwObjAddrGrp6,
    NwObjInternetService,
    NwObjInternetServiceGroup,
    NwObjIpPool,
    NwObjVip,
    Rule,
    SvcObjApplicationGroup,
    SvcObjApplicationList,
    SvcObjCustom,
    SvcObjGroup,
    UserObjGroup,
    UserObjLocal,
    ZoneObject,
)
from fwo_exceptions import FwApiCallFailedError
from fwo_log import FWOLogger
from pydantic import BaseModel, TypeAdapter

T = TypeVar("T", bound=BaseModel)

HTTP_OK = 200


def fortios_api_call(api_url: str) -> list[dict[str, Any]]:
    """
    Makes a GET request to the FortiOS REST API and returns the JSON response.

    Args:
        api_url (str): The full URL for the API endpoint.

    Returns:
        list[dict[str, Any]]: The list of results from the API.

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


def parse_api_results(model_class: type[T], data: list[dict[str, Any]]) -> list[T]:  # noqa: UP047 #TODO: needs python 3.12
    """
    Parse API results into Pydantic model instances.

    Args:
        model_class: The Pydantic model class to parse into.
        data: The raw API response data.

    Returns:
        List of parsed Pydantic model instances.

    """
    adapter = TypeAdapter(list[model_class])
    return adapter.validate_python(data)


def get_native_config(fm_api_url: str, sid: str) -> FortiOSConfig:
    """
    Gets the native configuration from the FortiOS REST API.

    Args:
        fm_api_url (str): The base URL for the FortiOS API.
        sid (str): The session ID or access token for authentication.

    Returns:
        FortiOSConfig: The native configuration.

    """
    native_config = FortiOSConfig()

    # Network objects
    native_config.nw_obj_address.extend(
        parse_api_results(NwObjAddress, fortios_api_call(fm_api_url + "/cmdb/firewall/address?access_token=" + sid))
    )
    native_config.nw_obj_address6.extend(
        parse_api_results(NwObjAddress6, fortios_api_call(fm_api_url + "/cmdb/firewall/address6?access_token=" + sid))
    )
    native_config.nw_obj_addrgrp.extend(
        parse_api_results(NwObjAddrGrp, fortios_api_call(fm_api_url + "/cmdb/firewall/addrgrp?access_token=" + sid))
    )
    native_config.nw_obj_addrgrp6.extend(
        parse_api_results(NwObjAddrGrp6, fortios_api_call(fm_api_url + "/cmdb/firewall/addrgrp6?access_token=" + sid))
    )
    native_config.nw_obj_ippool.extend(
        parse_api_results(NwObjIpPool, fortios_api_call(fm_api_url + "/cmdb/firewall/ippool?access_token=" + sid))
    )
    native_config.nw_obj_vip.extend(
        parse_api_results(NwObjVip, fortios_api_call(fm_api_url + "/cmdb/firewall/vip?access_token=" + sid))
    )
    native_config.nw_obj_internet_service.extend(
        parse_api_results(
            NwObjInternetService, fortios_api_call(fm_api_url + "/cmdb/firewall/internet-service?access_token=" + sid)
        )
    )
    native_config.nw_obj_internet_service_group.extend(
        parse_api_results(
            NwObjInternetServiceGroup,
            fortios_api_call(fm_api_url + "/cmdb/firewall/internet-service-group?access_token=" + sid),
        )
    )

    # Service objects
    native_config.svc_obj_application_list.extend(
        parse_api_results(
            SvcObjApplicationList, fortios_api_call(fm_api_url + "/cmdb/application/list?access_token=" + sid)
        )
    )
    native_config.svc_obj_application_group.extend(
        parse_api_results(
            SvcObjApplicationGroup, fortios_api_call(fm_api_url + "/cmdb/application/group?access_token=" + sid)
        )
    )
    native_config.svc_obj_custom.extend(
        parse_api_results(
            SvcObjCustom, fortios_api_call(fm_api_url + "/cmdb/firewall.service/custom?access_token=" + sid)
        )
    )
    native_config.svc_obj_group.extend(
        parse_api_results(
            SvcObjGroup, fortios_api_call(fm_api_url + "/cmdb/firewall.service/group?access_token=" + sid)
        )
    )

    # User objects
    native_config.user_obj_local.extend(
        parse_api_results(UserObjLocal, fortios_api_call(fm_api_url + "/cmdb/user/local?access_token=" + sid))
    )
    native_config.user_obj_group.extend(
        parse_api_results(UserObjGroup, fortios_api_call(fm_api_url + "/cmdb/user/group?access_token=" + sid))
    )

    add_zone_if_missing(native_config, "global")

    # Rules
    native_config.rules.extend(
        parse_api_results(Rule, fortios_api_call(fm_api_url + "/cmdb/firewall/policy?access_token=" + sid))
    )

    process_zones(native_config)

    # TODO: get nat rules

    return native_config


def normalize_zone_name(zone_name: str) -> str:
    if zone_name == "any":
        return "global"
    return zone_name


def add_zone_if_missing(native_config: FortiOSConfig, zone_name: str) -> str:
    """
    Adds a zone to the native configuration if it is missing.

    Args:
        native_config (FortiOSConfig): The native configuration.
        zone_name (str): The name of the zone to add.

    """
    zone_name = normalize_zone_name(zone_name)

    if not any(z for z in native_config.zone_objects if z.zone_name == zone_name):
        # zone not found - add it
        native_config.zone_objects.append(ZoneObject(zone_name=zone_name))

    return zone_name


def process_zones(native_config: FortiOSConfig) -> None:
    """
    Extracts zones from interfaces in the native configuration, adding them to the zone objects list.

    Args:
        native_config (FortiOSConfig): The native configuration.

    """
    for obj in native_config.nw_obj_address:
        if obj.associated_interface:
            obj.associated_interface = add_zone_if_missing(native_config, obj.associated_interface)
    for obj in native_config.nw_obj_ippool:
        if obj.associated_interface:
            obj.associated_interface = add_zone_if_missing(native_config, obj.associated_interface)
    for rule in native_config.rules:
        for srcintf in rule.srcintf:
            srcintf.name = add_zone_if_missing(native_config, srcintf.name)
        for dstintf in rule.dstintf:
            dstintf.name = add_zone_if_missing(native_config, dstintf.name)

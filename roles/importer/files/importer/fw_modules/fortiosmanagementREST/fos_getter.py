# library for API get functions
import json
from typing import Any, TypeVar, cast

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
from fw_modules.fortiosmanagementREST.fos_zone import normalize_zone_name
from fwo_const import FWO_HTTP_TIMEOUT
from fwo_exceptions import FwApiCallFailedError
from fwo_log import FWOLogger
from fwo_redaction import redact_secret_in_text, redact_sensitive_values
from pydantic import BaseModel, TypeAdapter

T = TypeVar("T", bound=BaseModel)

HTTP_OK = 200


def fortios_api_call(api_url: str, access_token: str) -> list[dict[str, Any]]:
    """
    Makes a GET request to the FortiOS REST API and returns the JSON response.

    Args:
        api_url (str): The full URL for the API endpoint.

    Returns:
        list[dict[str, Any]]: The list of results from the API.

    """
    request_headers = {"Content-Type": "application/json"}

    try:
        response = requests.get(
            api_url,
            headers=request_headers,
            params={"access_token": access_token},
            verify=fwo_globals.verify_certs,
            timeout=FWO_HTTP_TIMEOUT,
        )
    except requests.exceptions.RequestException as request_error:
        # the text of a requests error names the full url including the access token query parameter
        raise FwApiCallFailedError(
            f"error while sending api_call to url '{api_url}' ({type(request_error).__name__})"
        ) from None
    # the firewall or a proxy in between may echo the requested url, including the access token, in its answer
    if response.status_code != HTTP_OK:
        response_text = redact_secret_in_text(response.text, access_token)
        raise FwApiCallFailedError(
            f"error while sending api_call to url '{api_url}', response code: {response.status_code}, "
            f"response text: {response_text}"
        )
    result_json = response.json()
    if "results" not in result_json:
        response_dump = json.dumps(redact_sensitive_values(result_json), indent=2)
        response_dump = redact_secret_in_text(response_dump, access_token)
        raise FwApiCallFailedError(f"error while sending api_call to url '{api_url}', response json: {response_dump}")

    FWOLogger.debug(f"api_call to url '{api_url}'", 3)

    return result_json["results"]


def parse_api_results(model_class: type[T], data: list[dict[str, Any]]) -> list[T]:
    """
    Parse API results into Pydantic model instances.

    Args:
        model_class: The Pydantic model class to parse into.
        data: The raw API response data.

    Returns:
        List of parsed Pydantic model instances.

    """
    # TypeAdapter expects a static TypeForm; list[model_class] is built at runtime, so pin the type explicitly.
    adapter: TypeAdapter[list[T]] = TypeAdapter(cast("Any", list[model_class]))
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
        parse_api_results(NwObjAddress, fortios_api_call(fm_api_url + "/cmdb/firewall/address", sid))
    )
    native_config.nw_obj_address6.extend(
        parse_api_results(NwObjAddress6, fortios_api_call(fm_api_url + "/cmdb/firewall/address6", sid))
    )
    native_config.nw_obj_addrgrp.extend(
        parse_api_results(NwObjAddrGrp, fortios_api_call(fm_api_url + "/cmdb/firewall/addrgrp", sid))
    )
    native_config.nw_obj_addrgrp6.extend(
        parse_api_results(NwObjAddrGrp6, fortios_api_call(fm_api_url + "/cmdb/firewall/addrgrp6", sid))
    )
    native_config.nw_obj_ippool.extend(
        parse_api_results(NwObjIpPool, fortios_api_call(fm_api_url + "/cmdb/firewall/ippool", sid))
    )
    native_config.nw_obj_vip.extend(
        parse_api_results(NwObjVip, fortios_api_call(fm_api_url + "/cmdb/firewall/vip", sid))
    )
    native_config.nw_obj_internet_service.extend(
        parse_api_results(NwObjInternetService, fortios_api_call(fm_api_url + "/cmdb/firewall/internet-service", sid))
    )
    native_config.nw_obj_internet_service_group.extend(
        parse_api_results(
            NwObjInternetServiceGroup,
            fortios_api_call(fm_api_url + "/cmdb/firewall/internet-service-group", sid),
        )
    )

    # Service objects
    native_config.svc_obj_application_list.extend(
        parse_api_results(SvcObjApplicationList, fortios_api_call(fm_api_url + "/cmdb/application/list", sid))
    )
    native_config.svc_obj_application_group.extend(
        parse_api_results(SvcObjApplicationGroup, fortios_api_call(fm_api_url + "/cmdb/application/group", sid))
    )
    native_config.svc_obj_custom.extend(
        parse_api_results(SvcObjCustom, fortios_api_call(fm_api_url + "/cmdb/firewall.service/custom", sid))
    )
    native_config.svc_obj_group.extend(
        parse_api_results(SvcObjGroup, fortios_api_call(fm_api_url + "/cmdb/firewall.service/group", sid))
    )

    # User objects
    native_config.user_obj_local.extend(
        parse_api_results(UserObjLocal, fortios_api_call(fm_api_url + "/cmdb/user/local", sid))
    )
    native_config.user_obj_group.extend(
        parse_api_results(UserObjGroup, fortios_api_call(fm_api_url + "/cmdb/user/group", sid))
    )

    add_zone_if_missing(native_config, "global")

    # Rules
    native_config.rules.extend(parse_api_results(Rule, fortios_api_call(fm_api_url + "/cmdb/firewall/policy", sid)))

    process_zones(native_config)

    # TODO: get nat rules

    return native_config


def add_zone_if_missing(native_config: FortiOSConfig, zone_name: str) -> str:
    """
    Adds a zone to the native configuration if it is missing.

    Args:
        native_config (FortiOSConfig): The native configuration.
        zone_name (str): The name of the zone to add.

    Returns:
        str: The normalized name of the zone that was ensured to exist in the configuration.

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

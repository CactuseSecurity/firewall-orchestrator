import json
from typing import Any

import cifp_service
import cifp_rule
import cifp_network
import cifp_getter
from fwo_log import FWOLogger
from models.import_state import ImportState

# API endpoint constant
FMC_CONFIG_DOMAIN_ENDPOINT = "fmc_config/v1/domain/"

def has_config_changed(_: dict[str, Any], __: ImportState, ___: bool = False) -> bool:
    # dummy - may be filled with real check later on
    return True


def get_config(config2import: dict[str, Any], full_config: dict[str, Any], current_import_id: str, mgm_details: dict[str, Any]) -> int:
    if full_config == {}:   # no native config was passed in, so getting it from Cisco Management
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # no native config was passed in, so getting it from Cisco Management
        cisco_api_url = 'https://' + \
            mgm_details['hostname'] + ':' + \
            str(mgm_details['port']) + '/api'
        session_id, domains = cifp_getter.login(mgm_details["import_credential"]['user'], mgm_details["import_credential"]['secret'],
                                mgm_details['hostname'], mgm_details['port'])
        domain = mgm_details["configPath"]
        if session_id == "":
            FWOLogger.error(
                'Did not succeed in logging in to Cisco Firepower API, no sid returned.')
            return 1
        if domain is None or domain == "":
            FWOLogger.error(
                'Configured domain is null or empty.')
            return 1
        scopes = get_scopes(domain, json.loads(domains))
        if len(scopes) == 0:
            FWOLogger.error(
                "Domain \"" + domain + "\" could not be found. \"" + domain + "\" does not appear to be a domain name or a domain UID.")
            return 1

        get_devices(session_id, cisco_api_url, full_config, scopes, mgm_details["devices"])
        get_objects(session_id, cisco_api_url, full_config, scopes)

        for device in full_config["devices"]:
            cifp_rule.get_access_policy(session_id, cisco_api_url, device)
            ##cifp_rule.getNatPolicy(sessionId, cisco_api_url, full_config, domain, device, limit) TODO

        try:  # logout
            cifp_getter.logout(cisco_api_url, session_id)
        except Exception:
            FWOLogger.warning(
                "logout exception probably due to timeout - irrelevant, so ignoring it")

    # now we normalize relevant parts of the raw config and write the results to config2import dict

    # write normalized networking data to config2import
    # this is currently not written to the database but only used for natting decisions
    # later we will probably store the networking info in the database as well as a basis
    # for path analysis

    # normalize_network_data(full_config, config2import, mgm_details)

    # cifp_user.normalize_users(
    #     full_config, config2import, current_import_id, user_scope)
    cifp_network.normalize_nwobjects(
        full_config, config2import, current_import_id)
    cifp_service.normalize_svcobjects(
        full_config, config2import, current_import_id)
    cifp_rule.normalize_access_rules(
        full_config, config2import, current_import_id)
    # cifp_rule.normalize_nat_rules(
    #     full_config, config2import, current_import_id, jwt=jwt)
    # cifp_network.remove_nat_ip_entries(config2import)
    return 0

def get_all_access_rules(session_id: str, api_url: str, domains: list[dict[str, Any]]) -> list[dict[str, Any]]:
    for domain in domains:
        domain["access_policies"] = cifp_getter.update_config_with_cisco_api_call(session_id, api_url,
            FMC_CONFIG_DOMAIN_ENDPOINT + domain["uuid"] + "/policy/accesspolicies" , parameters={"expanded": True})

        for access_policy in domain["access_policies"]:
            access_policy["rules"] = cifp_getter.update_config_with_cisco_api_call(session_id, api_url,
            FMC_CONFIG_DOMAIN_ENDPOINT + domain["uuid"] + "/policy/accesspolicies/" + access_policy["id"] + "/accessrules", parameters={"expanded": True})
    return domains

def get_scopes(search_domain: str, domains: list[dict[str, Any]]) -> list[str]:
    scopes: list[str] = []
    for domain in domains:
        if domain == domain["uuid"] or domain["name"].endswith(search_domain): # TODO: is the check supposed to be searchDomain == domain["uuid"] ?
            scopes.append(domain["uuid"])
    return scopes

def get_devices(session_id: str, api_url: str, config: dict[str, Any], scopes: list[str], devices: list[dict[str, Any]]) -> None:
    # get all devices
    for scope in scopes:
        config["devices"] = cifp_getter.update_config_with_cisco_api_call(session_id, api_url,
         FMC_CONFIG_DOMAIN_ENDPOINT + scope + "/devices/devicerecords", parameters={"expanded": True})
        for device in config["devices"]:
            if "domain" not in device:
                device["domain"] = scope
    # filter for existent devices
    for cisco_api_device in config["devices"]:
        found = False
        for device in devices:
            if device["name"] == cisco_api_device["name"] or device["name"] == cisco_api_device["id"]:
                found = True
                break
        # remove device if not in fwo api
        if found == False:
            config["devices"].remove(cisco_api_device)
            FWOLogger.info("Device \"" + cisco_api_device["name"] + "\" was found but it is not registered in FWO. Ignoring it.")

def get_objects(session_id: str, api_url: str, config: dict[str, Any], scopes: list[str]) -> None:
    # network objects:
    network_objects: list[dict[str, Any]] = []
    network_obj_groups: list[dict[str, Any]] = []
    # service objects:
    service_objects: list[dict[str, Any]] = []
    service_obj_groups: list[dict[str, Any]] = []
    # user objects:
    user_objects: list[dict[str, Any]] = []
    user_object_groups: list[dict[str, Any]] = []


    # get those objects that exist globally and on domain level
    for scope in scopes:
        # get network objects (groups):
        # for object_type in nw_obj_types:
        network_objects.extend(cifp_getter.update_config_with_cisco_api_call(session_id, api_url,
         FMC_CONFIG_DOMAIN_ENDPOINT + scope + "/object/networkaddresses", parameters={"expanded": True}))
        network_obj_groups.extend(cifp_getter.update_config_with_cisco_api_call(session_id, api_url,
         FMC_CONFIG_DOMAIN_ENDPOINT + scope + "/object/networkgroups", parameters={"expanded": True}))
        # get service objects:
        # for object_type in svc_obj_types:
        service_objects.extend(cifp_getter.update_config_with_cisco_api_call(session_id, api_url,
            FMC_CONFIG_DOMAIN_ENDPOINT + scope + "/object/ports", parameters={"expanded": True}))
        service_obj_groups.extend(cifp_getter.update_config_with_cisco_api_call(session_id, api_url,
            FMC_CONFIG_DOMAIN_ENDPOINT + scope + "/object/portobjectgroups", parameters={"expanded": True}))
        # get user objects:
        user_objects.extend(cifp_getter.update_config_with_cisco_api_call(session_id, api_url,
            FMC_CONFIG_DOMAIN_ENDPOINT + scope + "/object/realmusers", parameters={"expanded": True}))
        user_object_groups.extend(cifp_getter.update_config_with_cisco_api_call(session_id, api_url,
            FMC_CONFIG_DOMAIN_ENDPOINT + scope + "/object/realmusergroups", parameters={"expanded": True}))
        
    
    # network objects:
    config["networkObjects"] = network_objects
    config["networkObjectGroups"] = network_obj_groups
    # service objects:
    config["serviceObjects"] = service_objects
    config["serviceObjectGroups"] = service_obj_groups
    # user objects:
    config["userObjects"] = user_objects
    config["userObjectGroups"] = user_object_groups

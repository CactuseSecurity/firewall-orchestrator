import sys
from common import importer_base_dir
sys.path.append(importer_base_dir + '/ciscofirepowerdomain7ff')
import cifp_service
import cifp_rule
import cifp_network
import cifp_getter
import json
from fwo_log import getFwoLogger


def has_config_changed(full_config, mgm_details, force=False):
    # dummy - may be filled with real check later on
    return True


def get_config(config2import, full_config, current_import_id, mgm_details, limit=1000, force=False, jwt=''):
    logger = getFwoLogger()
    if full_config == {}:   # no native config was passed in, so getting it from Cisco Management
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # no native config was passed in, so getting it from Cisco Management
        cisco_api_url = 'https://' + \
            mgm_details['hostname'] + ':' + \
            str(mgm_details['port']) + '/api'
        sessionId, domains = cifp_getter.login(mgm_details["import_credential"]['user'], mgm_details["import_credential"]['secret'],
                                mgm_details['hostname'], mgm_details['port'])
        domain = mgm_details["configPath"]
        if sessionId == None or sessionId == "":
            logger.ERROR(
                'Did not succeed in logging in to FortiManager API, no sid returned.')
            return 1
        if domain == None or domain == "":
            logger.ERROR(
                'Configured domain is null or empty.')
            return 1
        scopes = getScopes(domain, json.loads(domains))
        if len(scopes) == 0:
            logger.ERROR(
                "Domain \"" + domain + "\" could not be found. \"" + domain + "\" does not appear to be a domain name or a domain UID.")
            return 1

        getDevices(sessionId, cisco_api_url, full_config, limit, scopes, mgm_details["devices"])
        getObjects(sessionId, cisco_api_url, full_config, limit, scopes)

        for device in full_config["devices"]:
            cifp_rule.getAccessPolicy(sessionId, cisco_api_url, full_config, device, limit)
            ##cifp_rule.getNatPolicy(sessionId, cisco_api_url, full_config, domain, device, limit) TODO

        try:  # logout
            cifp_getter.logout(cisco_api_url, sessionId)
        except:
            logger.warning(
                "logout exception probably due to timeout - irrelevant, so ignoring it")

    # now we normalize relevant parts of the raw config and write the results to config2import dict
    # currently reading zone from objects for backward compat with FortiManager 6.x
    # cifp_zone.normalize_zones(full_config, config2import, current_import_id)

    # write normalized networking data to config2import
    # this is currently not written to the database but only used for natting decisions
    # later we will probably store the networking info in the database as well as a basis
    # for path analysis

    # normalize_network_data(full_config, config2import, mgm_details)

    # cifp_user.normalize_users(
    #     full_config, config2import, current_import_id, user_scope)
    cifp_network.normalize_nwobjects(
        full_config, config2import, current_import_id, jwt=jwt, mgm_id=mgm_details['id'])
    cifp_service.normalize_svcobjects(
        full_config, config2import, current_import_id)
    cifp_rule.normalize_access_rules(
        full_config, config2import, current_import_id, mgm_details=mgm_details, jwt=jwt)
    # cifp_rule.normalize_nat_rules(
    #     full_config, config2import, current_import_id, jwt=jwt)
    # cifp_network.remove_nat_ip_entries(config2import)
    return 0

def getAllAccessRules(sessionId, api_url, domains):
    for domain in domains:
        domain["access_policies"] = cifp_getter.update_config_with_cisco_api_call(sessionId, api_url,
            "fmc_config/v1/domain/" + domain["uuid"] + "/policy/accesspolicies" , parameters={"expanded": True}, limit=1000)

        for access_policy in domain["access_policies"]:
            access_policy["rules"] = cifp_getter.update_config_with_cisco_api_call(sessionId, api_url,
            "fmc_config/v1/domain/" + domain["uuid"] + "/policy/accesspolicies/" + access_policy["id"] + "/accessrules", parameters={"expanded": True}, limit=1000)
    return domains

def getScopes(searchDomain, domains):
    scopes = []
    for domain in domains:
        if domain == domain["uuid"] or domain["name"].endswith(searchDomain):
            scopes.append(domain["uuid"])
    return scopes

def getDevices(sessionId, api_url, config, limit, scopes, devices):
    logger = getFwoLogger()
    # get all devices
    for scope in scopes:
        config["devices"] = cifp_getter.update_config_with_cisco_api_call(sessionId, api_url,
         "fmc_config/v1/domain/" + scope + "/devices/devicerecords", parameters={"expanded": True}, limit=limit)
        for device in config["devices"]:
            if not "domain" in device:
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
            logger.info("Device \"" + cisco_api_device["name"] + "\" was found but it is not registered in FWO. Ignoring it.")

def getObjects(sessionId, api_url, config, limit, scopes):
    # network objects:
    config["networkObjects"] = []
    config["networkObjectGroups"] = []
    # service objects:
    config["serviceObjects"] = []
    config["serviceObjectGroups"] = []
    # user objects:
    config["userObjects"] = []
    config["userObjectGroups"] = []

    # get those objects that exist globally and on domain level
    for scope in scopes:
        # get network objects (groups):
        # for object_type in nw_obj_types:
        config["networkObjects"].extend(cifp_getter.update_config_with_cisco_api_call(sessionId, api_url,
         "fmc_config/v1/domain/" + scope + "/object/networkaddresses", parameters={"expanded": True}, limit=limit))
        config["networkObjectGroups"].extend(cifp_getter.update_config_with_cisco_api_call(sessionId, api_url,
         "fmc_config/v1/domain/" + scope + "/object/networkgroups", parameters={"expanded": True}, limit=limit))
        # get service objects:
        # for object_type in svc_obj_types:
        config["serviceObjects"].extend(cifp_getter.update_config_with_cisco_api_call(sessionId, api_url,
            "fmc_config/v1/domain/" + scope + "/object/ports", parameters={"expanded": True}, limit=limit))
        config["serviceObjectGroups"].extend(cifp_getter.update_config_with_cisco_api_call(sessionId, api_url,
            "fmc_config/v1/domain/" + scope + "/object/portobjectgroups", parameters={"expanded": True}, limit=limit))
        # get user objects:
        config["userObjects"].extend(cifp_getter.update_config_with_cisco_api_call(sessionId, api_url,
            "fmc_config/v1/domain/" + scope + "/object/realmusers", parameters={"expanded": True}, limit=limit))
        config["userObjectGroups"].extend(cifp_getter.update_config_with_cisco_api_call(sessionId, api_url,
            "fmc_config/v1/domain/" + scope + "/object/realmusergroups", parameters={"expanded": True}, limit=limit))


# def getZones(sid, fm_api_url, raw_config, adom_name, limit, debug_level):
#     raw_config.update({"zones": {}})

#     # get global zones?

#     # get local zones
#     for device in raw_config['devices']:
#         local_pkg_name = device['package']
#         for adom in raw_config['adoms']:
#             if adom['name']==adom_name:
#                 if local_pkg_name not in adom['package_names']:
#                     logger.error('local rulebase/package ' + local_pkg_name + ' not found in management ' + adom_name)
#                     return 1
#                 else:
#                     cifp_getter.update_config_with_fortinet_api_call(
#                         raw_config['zones'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/obj/dynamic/interface", device['id'], debug=debug_level, limit=limit)

#     raw_config['zones']['zone_list'] = []
#     for device in raw_config['zones']:
#         for mapping in raw_config['zones'][device]:
#             if not isinstance(mapping, str):
#                 if not mapping['dynamic_mapping'] is None:
#                     for dyn_mapping in mapping['dynamic_mapping']:
#                         if 'name' in dyn_mapping and not dyn_mapping['name'] in raw_config['zones']['zone_list']:
#                             raw_config['zones']['zone_list'].append(dyn_mapping['name'])
#                         if 'local-intf' in dyn_mapping and not dyn_mapping['local-intf'][0] in raw_config['zones']['zone_list']:
#                             raw_config['zones']['zone_list'].append(dyn_mapping['local-intf'][0])
#                 if not mapping['platform_mapping'] is None:
#                     for dyn_mapping in mapping['platform_mapping']:
#                         if 'intf-zone' in dyn_mapping and not dyn_mapping['intf-zone'] in raw_config['zones']['zone_list']:
#                             raw_config['zones']['zone_list'].append(dyn_mapping['intf-zone'])

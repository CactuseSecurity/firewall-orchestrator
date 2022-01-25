# todo: find mapping device <--> package
# todo: consolidate nat rules in a single rulebase
# todo: consolidate global and pkg-local rules in a single rulebase
# todo: dealing with consolidated rules?

from curses import raw
import logging, sys, os, json
base_dir = "/usr/local/fworch"
sys.path.append(base_dir + '/importer')
sys.path.append(base_dir + '/importer/fortimanager5ff')
import getter, fmgr_network, fmgr_rule, fmgr_zone, fmgr_service, fmgr_user

scope = ['global', 'adom']

rule_access_scope = ['rules_global_header_v4', 'rules_global_header_v6', 'rules_adom_v4', 'rules_adom_v6', 'rules_global_footer_v4', 'rules_global_footer_v6']
rule_nat_scope = ['rules_global_nat', 'rules_adom_nat']
rule_scope = rule_access_scope + rule_nat_scope

nw_obj_types = ['address', 'address6', 'addrgrp', 'addrgrp6', 'ippool']
svc_obj_types = ['application/list', 'application/group', 'application/categories', 'application/custom', 'firewall/service/custom', 'firewall/service/group']

# build the product of all scope/type combinations
nw_obj_scope = ['nw_obj_' + s1 + '_' + s2 for s1 in scope for s2 in nw_obj_types]
svc_obj_scope = ['svc_obj_' + s1 + '_' + s2 for s1 in scope for s2 in svc_obj_types]

# zone_types = ['zones_global', 'zones_adom']
user_types = ['users_global', 'users_adom']
user_scope = ['user_objects']

def get_config(config2import, full_config, current_import_id, mgm_details, debug_level=0, proxy=None, limit=100, force=False, ssl_verification=None):
    if full_config == {}:   # no native config was passed in, so getting it from FortiManager
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only:   # no native config was passed in, so getting it from FortiManager
        fm_api_url = 'https://' + \
            mgm_details['hostname'] + ':' + str(mgm_details['port']) + '/jsonrpc'
        api_domain = ''
        sid = getter.login(mgm_details['user'], mgm_details['secret'], mgm_details['hostname'],
                        mgm_details['port'], api_domain, debug=debug_level, ssl_verification='', proxy_string='')

        if sid is None:
            logging.ERROR('did not succeed in logging in to FortiManager API, so sid returned')
            return 1
    
    adom_name = mgm_details['configPath']
    if adom_name is None:
        logging.error('no ADOM name set for this management!')
        return 1
    else:
        # full_config = {}
        # # get all custom adoms (only works for latest API versions):
        # q_get_custom_adoms = {"params": [
        #     {"fields": ["name", "oid", "uuid"], "filter": ["create_time", "<>", 0]}]}
        # adoms = getter.fortinet_api_call(
        #     sid, fm_api_url, '/dvmdb/adom', payload=q_get_custom_adoms, debug=debug_level)

        # # get root adom (not covered in custom filter above):
        # q_get_root_adom = {"params": [
        #     {"fields": ["name", "oid", "uuid"], "filter": ["name", "==", "root"]}]}
        # adom_root = getter.fortinet_api_call(
        #     sid, fm_api_url, '/dvmdb/adom', payload=q_get_root_adom, debug=debug_level).pop()
        # adoms.append(adom_root)
        # full_config.update({"adoms": adoms})

        # q_get_adoms = {"params": [
        #      {"fields": ["name", "oid", "uuid"], "filter": ["uuid", "<>", "null"]}]}
        if not parsing_config_only:   # no native config was passed in, so getting it from FortiManager
            q_get_adoms = {"params": [{"fields": ["name", "oid", "uuid"]}]}
            adoms = getter.fortinet_api_call(
                sid, fm_api_url, '/dvmdb/adom', payload=q_get_adoms, debug=debug_level)
        else:
            adoms = full_config['adoms']

        adom_found = False
        for adom in adoms:
            if adom['name'] == adom_name:
                adom_found = True
                # just adding the adom we are interested in for now
                if full_config == {}:   # no native config was passed
                    full_config.update({"adoms": [adom]})
        if not adom_found:
            logging.error('ADOM name ' + adom_name + ' not found on this FortiManager!')
            return 1
        else: 
            if not parsing_config_only:   # no native config was passed in, so getting it from FortiManager
                # get details for each device/policy
                getDeviceDetails(sid, fm_api_url, full_config, mgm_details, debug_level)
                getObjects(sid, fm_api_url, full_config, adom_name, limit, debug_level, scope, nw_obj_types, svc_obj_types)
                # currently reading zone from objects/rules for backward compat with FortiManager 6.x
                #getZones(sid, fm_api_url, full_config, adom_name, limit, debug_level)
                getInterfacesAndRouting(sid, fm_api_url, full_config, adom_name, limit, debug_level)
                getAccessPolicies(sid, fm_api_url, full_config, adom_name, limit, debug_level)
                getNatPolicies(sid, fm_api_url, full_config, adom_name, limit, debug_level)

            # now we normalize relevant parts of the raw config and write the results to config2import dict
            # currently reading zone from objects for backward compat with FortiManager 6.x
            #fmgr_zone.normalize_zones(full_config, config2import, current_import_id)
            fmgr_user.normalize_users(full_config, config2import, current_import_id, user_scope)
            fmgr_network.normalize_nwobjects(full_config, config2import, current_import_id, nw_obj_scope)
            fmgr_service.normalize_svcobjects(full_config, config2import, current_import_id, svc_obj_scope)
            fmgr_rule.normalize_access_rules(full_config, config2import, current_import_id, rule_access_scope)
            fmgr_rule.normalize_nat_rules(full_config, config2import, current_import_id, rule_nat_scope)

    if not parsing_config_only:   # no native config was passed in, logging out
        getter.logout(fm_api_url, sid, ssl_verification='',proxy_string='', debug=debug_level)
    return 0


def getDeviceDetails(sid, fm_api_url, raw_config, mgm_details, debug_level):
    # for each adom get devices
    for adom in raw_config["adoms"]:
        q_get_devices_per_adom = {"params": [{"fields": ["name", "desc", "hostname", "vdom",
                                                            "ip", "mgmt_id", "mgt_vdom", "os_type", "os_ver", "platform_str", "dev_status"]}]}
        devs = getter.fortinet_api_call(
            sid, fm_api_url, "/dvmdb/adom/" + adom["name"] + "/device", payload=q_get_devices_per_adom, debug=debug_level)
        adom.update({"devices": devs})

    # for each adom get packages
    for adom in raw_config["adoms"]:
        pkg_names = []
        packages = getter.fortinet_api_call(
            sid, fm_api_url, "/pm/pkg/adom/" + adom["name"], debug=debug_level)
        for pkg in packages:
            pkg_names.append(pkg['name'])
        adom.update({"packages": packages})
        adom.update({"package_names": pkg_names})
    
    global_pkg_names = []
    global_packages = getter.fortinet_api_call(sid, fm_api_url, "/pm/pkg/global", debug=debug_level)
    for pkg in global_packages:
        global_pkg_names.append(pkg['name'])
    raw_config.update({"global_packages": global_packages})
    raw_config.update({"global_package_names": global_pkg_names})

    devices = []
    device_names = []
    for device in mgm_details['devices']:
        device_names.append(device['name'])
        devices.append(
            {
                'id': device['id'],
                'name': device['name'],
                'global_rulebase': device['global_rulebase_name'],
                'local_rulebase': device['local_rulebase_name'],
                'package': device['package_name']
            }
        )
    raw_config.update({"devices": devices})
    raw_config.update({"device_names": device_names})


def getInterfacesAndRouting(sid, fm_api_url, raw_config, adom_name, limit, debug_level):
    # get network information (also needed for source nat)
    adom_scope = 'adom/'+adom_name
    getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/dynamic/interface", "interfaces", debug=debug_level, limit=limit)

    getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/router/route-map", "route-map", debug=debug_level, limit=limit)

    getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/router/prefix-list", "router-prefix-list", debug=debug_level, limit=limit)

    getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/cli/global/system/route", "router-cli", debug=debug_level, limit=limit)


def getObjects(sid, fm_api_url, raw_config, adom_name, limit, debug_level, scope, nw_obj_types, svc_obj_types):
    # get those objects that exist globally and on adom level
    for s in scope:
        # get network objects:
        for object_type in nw_obj_types:
            if s == 'adom':
                adom_scope = 'adom/'+adom_name
            else:
                adom_scope = s
            getter.update_config_with_fortinet_api_call(
                raw_config, sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/firewall/" + object_type, "nw_obj_" + s + "_" + object_type, debug=debug_level, limit=limit)

        # get service objects:
        # service/custom is an undocumented API call!
        options = []    # options = ['get reserved']
        for object_type in svc_obj_types:
            if s == 'adom':
                adom_scope = 'adom/'+adom_name
            else:
                adom_scope = s
            getter.update_config_with_fortinet_api_call(
                raw_config, sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/" + object_type, "svc_obj_" + s + "_" + object_type, debug=debug_level, limit=limit, options=options)
    
    #    user: /pm/config/global/obj/user/local
    getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/pm/config/global/obj/user/local", "users_local", debug=debug_level, limit=limit)


def getZones(sid, fm_api_url, raw_config, adom_name, limit, debug_level):
    raw_config.update({"zones": {}})

    # get global zones?
 
    # get local zones
    for device in raw_config['devices']:
        local_pkg_name = device['package']
        for adom in raw_config['adoms']:
            if adom['name']==adom_name:
                if local_pkg_name not in adom['package_names']:
                    logging.error('local rulebase/package ' + local_pkg_name + ' not found in management ' + adom_name)
                    return 1
                else:
                    getter.update_config_with_fortinet_api_call(
                        raw_config['zones'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/obj/dynamic/interface", device['id'], debug=debug_level, limit=limit)

    raw_config['zones']['zone_list'] = []
    for device in raw_config['zones']:
        for mapping in raw_config['zones'][device]:
            if not isinstance(mapping, str):
                if not mapping['dynamic_mapping'] is None:
                    for dyn_mapping in mapping['dynamic_mapping']:
                        if 'name' in dyn_mapping and not dyn_mapping['name'] in raw_config['zones']['zone_list']:
                            raw_config['zones']['zone_list'].append(dyn_mapping['name'])
                        if 'local-intf' in dyn_mapping and not dyn_mapping['local-intf'][0] in raw_config['zones']['zone_list']:
                            raw_config['zones']['zone_list'].append(dyn_mapping['local-intf'][0])
                if not mapping['platform_mapping'] is None:
                    for dyn_mapping in mapping['platform_mapping']:
                        if 'intf-zone' in dyn_mapping and not dyn_mapping['intf-zone'] in raw_config['zones']['zone_list']:
                            raw_config['zones']['zone_list'].append(dyn_mapping['intf-zone'])


def getAccessPolicies(sid, fm_api_url, raw_config, adom_name, limit, debug_level):
    #raw_config.update({"rulebases_by_pkg_name": {}})

    # TODO: make sure we have the correct rule order!

    consolidated = '' # '/consolidated'

    # initialize all rule dicts
    for rule_dict in rule_scope:
        raw_config[rule_dict] = {}
    # get global header rulebase:
    for device in raw_config['devices']:
        if device['global_rulebase'] is None or device['global_rulebase'] == '':
            logging.warning('no global rulebase name defined in fortimanager')
        elif device['global_rulebase'] not in raw_config['global_package_names']:
            logging.error('global rulebase/package ' + device['global_rulebase'] + ' not found in fortimanager')
            return 1
        else:
            getter.update_config_with_fortinet_api_call(
                raw_config['rules_global_header_v4'], sid, fm_api_url, "/pm/config/global/pkg/" + device['global_rulebase'] + "/global/header" + consolidated + "/policy", device['package'], debug=debug_level, limit=limit)
            getter.update_config_with_fortinet_api_call(
                raw_config['rules_global_header_v6'], sid, fm_api_url, "/pm/config/global/pkg/" + device['global_rulebase'] + "/global/header" + consolidated + "/policy6", device['package'], debug=debug_level, limit=limit)

    # get local rulebase
    for device in raw_config['devices']:
        local_pkg_name = device['package']
        for adom in raw_config['adoms']:
            if adom['name']==adom_name:
                if local_pkg_name not in adom['package_names']:
                    logging.error('local rulebase/package ' + local_pkg_name + ' not found in management ' + adom_name)
                    return 1
                else:
                    getter.update_config_with_fortinet_api_call(
                        raw_config['rules_adom_v4'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/pkg/" + local_pkg_name + "/firewall" + consolidated + "/policy", device['package'], debug=debug_level, limit=limit)
                    getter.update_config_with_fortinet_api_call(
                        raw_config['rules_adom_v6'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/pkg/" + local_pkg_name + "/firewall" + consolidated + "/policy6", device['package'], debug=debug_level, limit=limit)

    # get global footer rulebase:
    for device in raw_config['devices']:
        if device['global_rulebase'] is None or device['global_rulebase'] == '':
            logging.warning('no global rulebase name defined in fortimanager')
        elif device['global_rulebase'] not in raw_config['global_package_names']:
            logging.error('global rulebase/package ' + device['global_rulebase'] + ' not found in fortimanager')
            return 1
        else:
            getter.update_config_with_fortinet_api_call(
                raw_config['rules_global_footer_v4'], sid, fm_api_url, "/pm/config/global/pkg/" + device['global_rulebase'] + "/global/footer" + consolidated + "/policy", device['package'], debug=debug_level, limit=limit)
            getter.update_config_with_fortinet_api_call(
                raw_config['rules_global_footer_v6'], sid, fm_api_url, "/pm/config/global/pkg/" + device['global_rulebase'] + "/global/footer" + consolidated + "/policy6", device['package'], debug=debug_level, limit=limit)


def getNatPolicies(sid, fm_api_url, raw_config, adom_name, limit, debug_level):
    #raw_config.update({"nat_by_dev_id": {}})

    for device in raw_config['devices']:
        scope = 'global'
        pkg = device['global_rulebase']
        if pkg is not None and pkg != '':   # only read global rulebase if it exists
            for nat_type in ['central/dnat', 'central/dnat6', 'firewall/central-snat-map']:
                getter.update_config_with_fortinet_api_call(
                    raw_config['rules_global_nat'], sid, fm_api_url, "/pm/config/" + scope + "/pkg/" + pkg + '/' + nat_type, device['package'], debug=debug_level, limit=limit)

        scope = 'adom/'+adom_name
        pkg = device['package']
        for nat_type in ['central/dnat', 'central/dnat6', 'firewall/central-snat-map']:
            getter.update_config_with_fortinet_api_call(
                raw_config['rules_adom_nat'], sid, fm_api_url, "/pm/config/" + scope + "/pkg/" + pkg + '/' + nat_type, device['package'], debug=debug_level, limit=limit)


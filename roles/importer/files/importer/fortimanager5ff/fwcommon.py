# todo: find mapping device <--> package
# todo: consolidate nat rules in a single rulebase
# todo: consolidate global and pkg-local rules in a single rulebase
# todo: dealing with consolidated rules?

from curses import raw
import logging, sys, os, json
base_dir = "/usr/local/fworch"
sys.path.append(base_dir + '/importer')
sys.path.append(base_dir + '/importer/fortimanager5ff')
import getter, fmgr_network, fmgr_rule, fmgr_zone


def get_config(config2import, current_import_id, base_dir, mgm_details, secret_filename, rulebase_string, config_filename, debug_level, proxy_string='', limit=100):
    logging.info("found FortiManager")
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
        raw_config = {}
        # get all custom adoms:
        q_get_custom_adoms = {"params": [
            {"fields": ["name", "oid", "uuid"], "filter": ["create_time", "<>", 0]}]}
        adoms = getter.fortinet_api_call(
            sid, fm_api_url, '/dvmdb/adom', payload=q_get_custom_adoms, debug=debug_level)

        # get root adom (not covered in custom filter above):
        q_get_root_adom = {"params": [
            {"fields": ["name", "oid", "uuid"], "filter": ["name", "==", "root"]}]}
        adom_root = getter.fortinet_api_call(
            sid, fm_api_url, '/dvmdb/adom', payload=q_get_root_adom, debug=debug_level).pop()
        adoms.append(adom_root)
        raw_config.update({"adoms": adoms})

        adom_found = False
        for adom in adoms:
            if adom['name'] == adom_name:
                adom_found = True
        if not adom_found:
            logging.error('ADOM name ' + adom_name + ' not found on this FortiManager!')
            return 1
        else: 
            # get details for each device/policy
            getDeviceDetails(sid, fm_api_url, raw_config, mgm_details, debug_level)

            getObjects(sid, fm_api_url, raw_config, adom_name, limit, debug_level)

            getZones(sid, fm_api_url, raw_config, adom_name, limit, debug_level)

            getAccessPolicies(sid, fm_api_url, raw_config, adom_name, limit, debug_level)
            
            getNatPolicies(sid, fm_api_url, raw_config, adom_name, limit, debug_level)

            # now we normalize relevant parts of the raw config and write the results to config2import dict
            fmgr_network.normalize_nwobjects(raw_config, config2import, current_import_id)
            fmgr_zone.normalize_zones(raw_config, config2import, current_import_id)
            fmgr_rule.normalize_rules(raw_config, config2import, current_import_id)

    getter.logout(fm_api_url, sid, ssl_verification='',proxy_string='', debug=debug_level)
    if (debug_level>=2):
        if os.path.exists(config_filename): # delete json file (to enabiling re-write)
            os.remove(config_filename)
        with open(config_filename, "w") as json_data:
            json_data.write(json.dumps(raw_config,indent=2))
    return 0


def getObjects(sid, fm_api_url, raw_config, adom_name, limit, debug_level):
    # get those objects that exist globally and on adom level
    for scope in ['global', 'adom/'+adom_name]:

        # get network objects:
        for object_type in ['address', 'address6', 'addrgrp', 'addrgrp6']:
            getter.update_config_with_fortinet_api_call(
                raw_config, sid, fm_api_url, "/pm/config/"+scope+"/obj/firewall/" + object_type, "network_objects", debug=debug_level, limit=limit)

        # get service objects:
        # service/custom is an undocumented API call!
        options = []    # options = ['get reserved']
        for object_type in ['application/list', 'application/group', 'application/categories', 'application/custom', 'firewall/service/custom', 'firewall/service/group']:
            getter.update_config_with_fortinet_api_call(
                raw_config, sid, fm_api_url, "/pm/config/"+scope+"/obj/" + object_type, "service_objects", debug=debug_level, limit=limit, options=options)
    
    #    user: /pm/config/global/obj/user/local
    getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/pm/config/global/obj/user/local", "users_local", debug=debug_level, limit=limit)


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
    raw_config.update({"v4_rulebases_by_dev_id": {}})
    raw_config.update({"v6_rulebases_by_dev_id": {}})

    consolidated = ''
    # consolidated = '/consolidated'

    # get global header rulebase:
    for device in raw_config['devices']:
        if device['global_rulebase'] is None:
            logging.error('no global rulebase name defined in fortimanager')
            return 1
        elif device['global_rulebase'] not in raw_config['global_package_names']:
            logging.error('global rulebase/package ' + device['global_rulebase'] + ' not found in fortimanager')
            return 1
        else:
            getter.update_config_with_fortinet_api_call(
                raw_config['v4_rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/global/pkg/" + device['global_rulebase'] + "/global/header" + consolidated + "/policy", device['id'], debug=debug_level, limit=limit)
            getter.update_config_with_fortinet_api_call(
                raw_config['v6_rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/global/pkg/" + device['global_rulebase'] + "/global/header" + consolidated + "/policy6", device['id'], debug=debug_level, limit=limit)

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
                        raw_config['v4_rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/pkg/" + local_pkg_name + "/firewall" + consolidated + "/policy", device['id'], debug=debug_level, limit=limit)
                    getter.update_config_with_fortinet_api_call(
                        raw_config['v6_rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/pkg/" + local_pkg_name + "/firewall" + consolidated + "/policy6", device['id'], debug=debug_level, limit=limit)

    # get global footer rulebase:
    for device in raw_config['devices']:
        if device['global_rulebase'] is None:
            logging.error('no global rulebase name defined in fortimanager')
            return 1
        elif device['global_rulebase'] not in raw_config['global_package_names']:
            logging.error('global rulebase/package ' + device['global_rulebase'] + ' not found in fortimanager')
            return 1
        else:
            getter.update_config_with_fortinet_api_call(
                raw_config['v4_rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/global/pkg/" + device['global_rulebase'] + "/global/footer" + consolidated + "/policy", device['id'], debug=debug_level, limit=limit)
            getter.update_config_with_fortinet_api_call(
                raw_config['v6_rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/global/pkg/" + device['global_rulebase'] + "/global/footer" + consolidated + "/policy6", device['id'], debug=debug_level, limit=limit)


def getNatPolicies(sid, fm_api_url, raw_config, adom_name, limit, debug_level):
    raw_config.update({"nat_by_dev_id": {}})
    
    for device in raw_config['devices']:

        for scope in ['global', 'adom/'+adom_name]:

            # todo: this throws warning exceptions for invalid combinations (global with local package names)
            for nat_type in ['central/dnat', 'central/dnat6', 'firewall/central-snat-map']:
                pkg = device['package']
                getter.update_config_with_fortinet_api_call(
                    raw_config['nat_by_dev_id'], sid, fm_api_url, "/pm/config/" + scope + "/pkg/" + pkg + '/' + nat_type, device['id'], debug=debug_level, limit=limit)


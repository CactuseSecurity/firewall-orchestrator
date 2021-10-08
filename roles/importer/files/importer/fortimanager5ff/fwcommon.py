# todo: find mapping device <--> package
# todo: consolidate nat rules in a single rulebase
# todo: consolidate global and pkg-local rules in a single rulebase
# todo: dealing with consolidated rules?

import logging, sys, os, json
base_dir = "/usr/local/fworch"
sys.path.append(base_dir + '/importer')
sys.path.append(base_dir + '/importer/fortimanager5ff')
import getter, fmgr_network


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
            getObjects(sid, fm_api_url, raw_config, adom_name, limit, debug_level)

            # get details for each device/policy
            getDeviceDetails(sid, fm_api_url, raw_config, mgm_details, debug_level)

            getAccessPolicies(sid, fm_api_url, raw_config, adom_name, limit, debug_level)
            
            getNatPolicies(sid, fm_api_url, raw_config, adom_name, limit, debug_level)

            # now we normalize relevant parts of the raw config and write the results to config2import dict
            fmgr_network.normalize_nwobjects(raw_config, config2import, current_import_id)

    getter.logout(fm_api_url, sid, proxy_string=proxy_string, debug=debug_level)
    if (debug_level>=2):
        if os.path.exists(config_filename): # delete json file (to enabiling re-write)
            os.remove(config_filename)
        with open(config_filename, "w") as json_data:
            json_data.write(json.dumps(raw_config,indent=2))
    return 0


def getObjects(sid, fm_api_url, raw_config, adom_name, limit, debug_level):
    # get those objects that exist both globally and on a per-adom level
    for scope in ['global', 'adom/'+adom_name]:
        getter.update_config_with_fortinet_api_call(
            raw_config, sid, fm_api_url, "/pm/config/"+scope+"/obj/firewall/address", "ipv4_objects", debug=debug_level, limit=limit)
        getter.update_config_with_fortinet_api_call(
            raw_config, sid, fm_api_url, "/pm/config/"+scope+"/obj/firewall/address6", "ipv6_objects", debug=debug_level, limit=limit)
        getter.update_config_with_fortinet_api_call(
            raw_config, sid, fm_api_url, "/pm/config/"+scope+"/obj/application/list", "app_list_objects", debug=debug_level, limit=limit)
        getter.update_config_with_fortinet_api_call(
            raw_config, sid, fm_api_url, "/pm/config/"+scope+"/obj/application/group", "app_group_objects", debug=debug_level, limit=limit)
        getter.update_config_with_fortinet_api_call(
            raw_config, sid, fm_api_url, "/pm/config/"+scope+"/obj/application/categories", "app_categories", debug=debug_level, limit=limit)

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
        if '/' in device['rulebase']: # we have to separate global and local rulebase names
            global_rulebase, local_rulebase = device['rulebase'].split('/')
        else: # no global rules exist
            local_rulebase = device['rulebase']
            global_rulebase = None
        devices.append(
            {
                'id': device['id'],
                'name': device['name'],
                'global_rulebase': global_rulebase,
                'local_rulebase': local_rulebase
            }
        )
    raw_config.update({"devices": devices})
    raw_config.update({"device_names": device_names})


def getAccessPolicies(sid, fm_api_url, raw_config, adom_name, limit, debug_level):
    raw_config.update({"v4_rulebases_by_dev_id": {}})
    raw_config.update({"v6_rulebases_by_dev_id": {}})

    consolidated = ''
    # consolidated = '/consolidated'

    # get global header rulebase:
    for device in raw_config['devices']:
        if device['global_rulebase'] not in raw_config['global_package_names']:
            logging.error('global rulebase/package ' + device['global_rulebase'] + ' not found in fortimanager')
            return 1
        else:
            getter.update_config_with_fortinet_api_call(
                raw_config['v4_rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/global/pkg/" + device['global_rulebase'] + "/global/header" + consolidated + "/policy", device['id'], debug=debug_level, limit=limit)
            getter.update_config_with_fortinet_api_call(
                raw_config['v6_rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/global/pkg/" + device['global_rulebase'] + "/global/header" + consolidated + "/policy6", device['id'], debug=debug_level, limit=limit)

    # get local rulebase
    for device in raw_config['devices']:
        local_pkg_name = device['local_rulebase']
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
        if device['global_rulebase'] not in raw_config['global_package_names']:
            logging.error('global rulebase/package ' + device['global_rulebase'] + ' not found in fortimanager')
            return 1
        else:
            getter.update_config_with_fortinet_api_call(
                raw_config['v4_rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/global/pkg/" + device['global_rulebase'] + "/global/footer" + consolidated + "/policy", device['id'], debug=debug_level, limit=limit)
            getter.update_config_with_fortinet_api_call(
                raw_config['v6_rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/global/pkg/" + device['global_rulebase'] + "/global/footer" + consolidated + "/policy6", device['id'], debug=debug_level, limit=limit)


def getNatPolicies(sid, fm_api_url, raw_config, adom_name, limit, debug_level):
    raw_config.update({"snat_by_dev_id": {}})
    raw_config.update({"dnat_by_dev_id": {}})
    # get nat rules for local ruleset - todo: are there any global nat rules?
    for device in raw_config['devices']:
        getter.update_config_with_fortinet_api_call(
            raw_config['snat_by_dev_id'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/pkg/" + device['local_rulebase'] + "/firewall/central-snat-map", device['id'], debug=debug_level, limit=limit)
        getter.update_config_with_fortinet_api_call(
            raw_config['dnat_by_dev_id'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/pkg/" + device['local_rulebase'] + "/firewall/central/dnat", device['id'], debug=debug_level, limit=limit)

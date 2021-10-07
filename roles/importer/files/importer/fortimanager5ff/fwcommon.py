from curses import raw
import logging
import sys
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
        logging.ERROR(
            'did not succeed in logging in to FortiManager API, so sid returned')
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

        # get root adom:
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
            logging.error('ADOM name ' + adom_name +
                          ' not found on this FortiManager!')
            return 1
        else:
            # get those objects that exist globally and adom-scoped
            for scope in ['global', 'adom/'+adom_name]:
                getter.update_config_with_fortinet_api_call(
                    raw_config, sid, fm_api_url, "/pm/config/"+scope+"/obj/firewall/address", "ipv4_objects", debug=debug_level, limit=limit)
                # api_url = "/pm/config/adom/global/obj/firewall/address" # --> error
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
                raw_config, sid, fm_api_url, "/pm/config/global/obj/user/local", "users_local", debug=debug_level)

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
            # todo: find mapping device <--> package
            # todo: consolidate nat rules in a single rulebase
            # todo: consolidate global and pkg-local rules in a single rulebase

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
            raw_config.update({"rulebases_by_dev_id": {}})

            # todo: also pass in and parse the global pkg name(s)
            # get global header rulebase:
            for device in devices:
                global_pkg_name = device['global_rulebase']
                if global_pkg_name not in raw_config['global_package_names']:
                    logging.error('global rulebase/package ' + global_pkg_name + ' not found in fortimanager')
                    return 1
                else:
                    getter.update_config_with_fortinet_api_call(
                        raw_config['rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/global/pkg/myglobpkg/global/header/policy", device['id'], debug=debug_level, limit=limit)

            # todo: find out diff between consolidated and non-consolidated
                # raw_config['rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/global/pkg/myglobpkg/global/header/consolidated/policy", device['id'], debug=debug_level, limit=limit)

            # get local rulebase
            for device in devices:
                local_pkg_name = device['local_rulebase']
                for adom in raw_config['adoms']:
                    if adom['name']==adom_name:
                        if local_pkg_name not in adom['package_names']:
                            logging.error('local rulebase/package ' + local_pkg_name + ' not found in management ' + adom_name)
                            return 1
                        else:
                            getter.update_config_with_fortinet_api_call(
                                raw_config['rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/pkg/" + local_pkg_name + "/firewall/policy", device['id'], debug=debug_level, limit=limit)

            # get global header rulebase:
            for device in devices:
                global_pkg_name = device['global_rulebase']
                if global_pkg_name not in raw_config['global_package_names']:
                    logging.error('global rulebase/package ' + global_pkg_name + ' not found in fortimanager')
                    return 1
                else:
                    getter.update_config_with_fortinet_api_call(
                        raw_config['rulebases_by_dev_id'], sid, fm_api_url, "/pm/config/global/pkg/myglobpkg/global/footer/policy", device['id'], debug=debug_level, limit=limit)


            # get nat rules per pkg per adom
            for adom in raw_config["adoms"]:
                for pkg in adom["packages"]:
                    central_snat_rulebase = getter.fortinet_api_call(
                        sid, fm_api_url, "/pm/config/adom/" + adom_name + "/pkg/" + pkg['name'] + "/firewall/central-snat-map", debug=debug_level)
                    central_dnat_rulebase = getter.fortinet_api_call(
                        sid, fm_api_url, "/pm/config/adom/" + adom['name'] + "/pkg/" + pkg['name'] + "/firewall/central/dnat", debug=debug_level)
                    pkg.update(
                        {"central_snat_rulebase": central_snat_rulebase})
                    pkg.update(
                        {"central_dnat_rulebase": central_dnat_rulebase})

            fmgr_network.normalize_nwobjects(
                raw_config, config2import, current_import_id)

            # autodiscovery parts:

            # # get nat rules per pkg per adom
            # for adom in raw_config["adoms"]:
            #     for pkg in adom["packages"]:
            #         central_snat_rulebase = getter.fortinet_api_call(
            #             sid, fm_api_url, "/pm/config/adom/" + adom['name'] + "/pkg/" + pkg['name'] + "/firewall/central-snat-map", debug=debug_level)
            #         central_dnat_rulebase = getter.fortinet_api_call(
            #             sid, fm_api_url, "/pm/config/adom/" + adom['name'] + "/pkg/" + pkg['name'] + "/firewall/central/dnat", debug=debug_level)
            #         pkg.update({"central_snat_rulebase": central_snat_rulebase})
            #         pkg.update({"central_dnat_rulebase": central_dnat_rulebase})

    getter.logout(fm_api_url, sid, proxy_string=proxy_string,
                  debug=debug_level)

    return 0

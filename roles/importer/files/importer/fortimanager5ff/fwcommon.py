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
        logging.ERROR('did not succeed to login to FortiManager API, so sid returned')
        return 1
    raw_config =  {}
    # get global objects
    getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/pm/config/adom/root/obj/firewall/address", "ipv4_objects", debug=debug_level, limit=limit)
    # api_url = "/pm/config/adom/global/obj/firewall/address" # --> error
    getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/pm/config/adom/root/obj/firewall/address6", "ipv6_objects", debug=debug_level)

    getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/pm/config/global/obj/application/list", "app_list_objects", debug=debug_level)
    getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/pm/config/global/obj/application/group", "app_group_objects", debug=debug_level)
    getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/pm/config/global/obj/application/categories", "app_categories", debug=debug_level)

    #    user: /pm/config/global/obj/user/local
    getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/pm/config/global/obj/user/local", "users_local", debug=debug_level)

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

    # for each adom get devices
    for adom in raw_config["adoms"]:
        q_get_devices_per_adom = {"params": [{"fields": ["name", "desc", "hostname", "vdom",
                                                         "ip", "mgmt_id", "mgt_vdom", "os_type", "os_ver", "platform_str", "dev_status"]}]}
        devs = getter.fortinet_api_call(
            sid, fm_api_url, "/dvmdb/adom/" + adom["name"] + "/device", payload=q_get_devices_per_adom, debug=debug_level)
        adom.update({"devices": devs})

    # for each adom get packages
    for adom in raw_config["adoms"]:
        packages = getter.fortinet_api_call(
            sid, fm_api_url, "/pm/pkg/adom/" + adom["name"], debug=debug_level)
        adom.update({"packages": packages})

    # todo: find mapping device <--> package
    # todo: consolidate nat rules in a single rulebase
    # todo: consolidate global and pkg-local rules in a single rulebase

    # get rulebases per pkg per adom
    for adom in raw_config["adoms"]:
        for pkg in adom["packages"]:
            rulebase = getter.fortinet_api_call(
                sid, fm_api_url, "/pm/config/adom/" + adom['name'] + "/pkg/" + pkg['name'] + "/firewall/policy", debug=debug_level)
            pkg.update({"rulebase": rulebase})

    # get global policies:
    global_header_policy = getter.fortinet_api_call(
        sid, fm_api_url, "/pm/config/global/pkg/default/global/header/consolidated/policy", debug=debug_level)
    raw_config.update({"global_header_policy": global_header_policy})
    global_footer_policy = getter.fortinet_api_call(
        sid, fm_api_url, "/pm/config/global/pkg/default/global/footer/consolidated/policy", debug=debug_level)
    raw_config.update({"global_footer_policy": global_footer_policy})

    # get nat rules per pkg per adom
    for adom in raw_config["adoms"]:
        for pkg in adom["packages"]:
            central_snat_rulebase = getter.fortinet_api_call(
                sid, fm_api_url, "/pm/config/adom/" + adom['name'] + "/pkg/" + pkg['name'] + "/firewall/central-snat-map", debug=debug_level)
            central_dnat_rulebase = getter.fortinet_api_call(
                sid, fm_api_url, "/pm/config/adom/" + adom['name'] + "/pkg/" + pkg['name'] + "/firewall/central/dnat", debug=debug_level)
            pkg.update({"central_snat_rulebase": central_snat_rulebase})
            pkg.update({"central_dnat_rulebase": central_dnat_rulebase})

    fmgr_network.normalize_nwobjects(raw_config, config2import, current_import_id)

    # now dumping results to file
    # with open(config_filename, "w") as configfile_json:
    #     configfile_json.write(json.dumps(config2import))

    getter.logout(fm_api_url, sid, proxy_string=proxy_string,
                  debug=debug_level)
    
    return 0
import json, argparse, logging
import requests, requests.packages
import sys
sys.path.append(r"/usr/local/fworch/importer")
import getter

# parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
# parser.add_argument('-a', '--apihost', metavar='api_host', required=True, help='Check Point R8x management server')
# parser.add_argument('-w', '--password', metavar='api_password_file', default='import_user_secret', help='name of the file to read the password for management server from')
# parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
# parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
# parser.add_argument('-D', '--domain', metavar='api_domain', default='', help='name of Domain in a Multi-Domain Envireonment')
# parser.add_argument('-l', '--layer', metavar='policy_layer_name(s)', required=True, help='name of policy layer(s) to read (comma separated)')
# parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
# parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
# parser.add_argument('-i', '--limit', metavar='api_limit', default='150', help='The maximal number of returned results per HTTPS Connection; default=150')
# parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0') 
# parser.add_argument('-t', '--testing', metavar='version_testing', default='off', help='Version test, [off|<version number>]; default=off') 
# parser.add_argument('-o', '--out', metavar='output_file', required=True, help='filename to write output in json format to')

# args = parser.parse_args()
# if len(sys.argv)==1:
#     parser.print_help(sys.stderr)
#     sys.exit(1)

# api_host = args.apihost
# api_port = args.port
# config_filename = args.out
# with open(args.password, "r") as password_file:
#     api_password = password_file.read().rstrip()
# api_domain = args.domain
# proxy_string = { "http" : args.proxy, "https" : args.proxy }
# offset = 0
# limit = args.limit
# details_level = "full"    # 'standard'
# test_version = args.testing
# base_url = 'https://' + api_host + ':' + api_port
# json_indent=2
# use_object_dictionary = 'false'
#limit="25"

# logging config
# debug_level = int(args.debug)
# common.set_log_level(log_level=debug_level, debug_level=debug_level)
# ssl_verification = getter.set_ssl_verification(args.ssl)

# starttime = int(time.time())
# top level dict start
# sid = getter.login(args.user,api_password,api_host,args.port,api_domain,ssl_verification, proxy_string=proxy_string,debug=debug_level)
# v_url = getter.get_api_url (sid, api_host, args.port, args.user, base_url, limit, test_version,ssl_verification, proxy_string)

# config_json = {}


def get_config(config2import, current_import_id, base_dir, mgm_details, secret_filename, rulebase_string, config_filename, debug_level, proxy_string=''):
    logging.info("found FortiManager")
    fm_api_url = 'https://' + mgm_details['hostname'] + '/:' + mgm_details['port'] + '/jsonrpc'
    api_domain = ''
    sid = getter.login(mgm_details['user'], mgm_details['secret'], mgm_details['hostname'], mgm_details['port'], api_domain, debug=debug_level)
    
        # get global objects
    getter.update_config_with_fortinet_api_call(config2import, sid, fm_api_url, "/pm/config/adom/root/obj/firewall/address", "ipv4_objects", debug=debug_level)
    # api_url = "/pm/config/adom/global/obj/firewall/address" # --> error
    getter.update_config_with_fortinet_api_call(config2import, sid, fm_api_url, "/pm/config/adom/root/obj/firewall/address6", "ipv6_objects", debug=debug_level)

    getter.update_config_with_fortinet_api_call(config2import, sid, fm_api_url, "/pm/config/global/obj/application/list", "app_list_objects", debug=debug_level)
    getter.update_config_with_fortinet_api_call(config2import, sid, fm_api_url, "/pm/config/global/obj/application/group", "app_group_objects", debug=debug_level)
    getter.update_config_with_fortinet_api_call(config2import, sid, fm_api_url, "/pm/config/global/obj/application/categories", "app_categories", debug=debug_level)

    #    user: /pm/config/global/obj/user/local
    getter.update_config_with_fortinet_api_call(config2import, sid, fm_api_url, "/pm/config/global/obj/user/local", "users_local", debug=debug_level)

    # get all custom adoms:
    q_get_custom_adoms = { "params": [ { "fields": ["name", "oid", "uuid"], "filter": ["create_time", "<>", 0] } ] }
    adoms = getter.fortinet_api_call(sid, fm_api_url, '/dvmdb/adom', payload=q_get_custom_adoms, debug=debug_level)

    # get root adom:
    q_get_root_adom = { "params": [ { "fields": ["name", "oid", "uuid"], "filter": ["name", "==", "root"] } ] }
    adom_root = getter.fortinet_api_call(sid, fm_api_url, '/dvmdb/adom', payload=q_get_root_adom, debug=debug_level).pop()
    adoms.append(adom_root)
    config2import.update({ "adoms": adoms })

    # for each adom get devices
    for adom in config2import["adoms"]:
      q_get_devices_per_adom = { "params": [ { "fields": ["name", "desc", "hostname", "vdom", "ip", "mgmt_id", "mgt_vdom", "os_type", "os_ver", "platform_str", "dev_status"] } ] }
      devs = getter.fortinet_api_call(sid, fm_api_url, "/dvmdb/adom/" + adom["name"] + "/device", payload=q_get_devices_per_adom, debug=debug_level)
      adom.update({"devices": devs})

    # for each adom get packages
    for adom in config2import["adoms"]:
      packages = getter.fortinet_api_call(sid, fm_api_url, "/pm/pkg/adom/" + adom["name"], debug=debug_level)
      adom.update({"packages": packages})

    # todo: find mapping device <--> package
    # todo: consolidate nat rules in a single rulebase
    # todo: consolidate global and pkg-local rules in a single rulebase

    # get rulebases per pkg per adom
    for adom in config2import["adoms"]:
      for pkg in adom["packages"]:
        rulebase = getter.fortinet_api_call(sid, fm_api_url, "/pm/config/adom/" + adom['name'] + "/pkg/" + pkg['name'] + "/firewall/policy", debug=debug_level)
        pkg.update({"rulebase": rulebase})

    # get global policies:
    global_header_policy = getter.fortinet_api_call(sid, fm_api_url, "/pm/config/global/pkg/default/global/header/consolidated/policy", debug=debug_level)
    config2import.update({"global_header_policy": global_header_policy})
    global_footer_policy = getter.fortinet_api_call(sid, fm_api_url, "/pm/config/global/pkg/default/global/footer/consolidated/policy", debug=debug_level)
    config2import.update({"global_footer_policy": global_footer_policy})

    # get nat rules per pkg per adom
    for adom in config2import["adoms"]:
      for pkg in adom["packages"]:
        central_snat_rulebase = getter.fortinet_api_call(sid, fm_api_url, "/pm/config/adom/" + adom['name'] + "/pkg/" + pkg['name'] + "/firewall/central-snat-map", debug=debug_level)
        central_dnat_rulebase = getter.fortinet_api_call(sid, fm_api_url, "/pm/config/adom/" + adom['name'] + "/pkg/" + pkg['name'] + "/firewall/central/dnat", debug=debug_level)
        pkg.update({"central_snat_rulebase": central_snat_rulebase})
        pkg.update({"central_dnat_rulebase": central_dnat_rulebase})

    # now dumping results to file
    with open(config_filename, "w") as configfile_json:
        configfile_json.write(json.dumps(config2import))

    getter.logout(fm_api_url, sid, proxy_string=proxy_string, debug=debug_level)

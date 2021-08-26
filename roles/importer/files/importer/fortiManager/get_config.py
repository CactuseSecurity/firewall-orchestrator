#!/usr/bin/python3
# create api user:
# config system admin user
#    edit "apiuser"
#        set password xxx
#        set adom "all_adoms"             
#        set rpc-permit read-write

import common
import getter
import json, argparse, pdb
import requests, requests.packages.urllib3
import time, logging, re, sys
import os

requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('-a', '--apihost', metavar='api_host', required=True, help='Check Point R8x management server')
parser.add_argument('-w', '--password', metavar='api_password_file', default='import_user_secret', help='name of the file to read the password for management server from')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-D', '--domain', metavar='api_domain', default='', help='name of Domain in a Multi-Domain Envireonment')
parser.add_argument('-l', '--layer', metavar='policy_layer_name(s)', required=True, help='name of policy layer(s) to read (comma separated)')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-i', '--limit', metavar='api_limit', default='500', help='The maximal number of returned results per HTTPS Connection; default=500')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0') 
parser.add_argument('-t', '--testing', metavar='version_testing', default='off', help='Version test, [off|<version number>]; default=off') 
parser.add_argument('-o', '--out', metavar='output_file', required=True, help='filename to write output in json format to')

args = parser.parse_args()
if len(sys.argv)==1:
    parser.print_help(sys.stderr)
    sys.exit(1)

api_host = args.apihost
api_port = args.port
config_filename = args.out
with open(args.password, "r") as password_file:
    api_password = password_file.read().rstrip()
api_domain = args.domain
proxy_string = { "http" : args.proxy, "https" : args.proxy }
offset = 0
limit = args.limit
details_level = "full"    # 'standard'
test_version = args.testing
base_url = 'https://' + api_host + ':' + api_port
json_indent=2
use_object_dictionary = 'false'
#limit="25"

# logging config
debug_level = int(args.debug)
common.set_log_level(log_level=debug_level, debug_level=debug_level)
ssl_verification = getter.set_ssl_verification(args.ssl)

starttime = int(time.time())
# top level dict start
sid = getter.login(args.user,api_password,api_host,args.port,api_domain,ssl_verification, proxy_string=proxy_string,debug=debug_level)
v_url = getter.get_api_url (sid, api_host, args.port, args.user, base_url, limit, test_version,ssl_verification, proxy_string)

config_json = {}

# get global objects
getter.update_config_with_fortinet_api_call(config_json, sid, v_url, "/pm/config/adom/root/obj/firewall/address", "ipv4_objects", debug=debug_level)
# api_url = "/pm/config/adom/global/obj/firewall/address" # --> error
getter.update_config_with_fortinet_api_call(config_json, sid, v_url, "/pm/config/adom/root/obj/firewall/address6", "ipv6_objects", debug=debug_level)

getter.update_config_with_fortinet_api_call(config_json, sid, v_url, "/pm/config/global/obj/application/list", "app_list_objects", debug=debug_level)
getter.update_config_with_fortinet_api_call(config_json, sid, v_url, "/pm/config/global/obj/application/group", "app_group_objects", debug=debug_level)
getter.update_config_with_fortinet_api_call(config_json, sid, v_url, "/pm/config/global/obj/application/categories", "app_categories", debug=debug_level)

#    user: /pm/config/global/obj/user/local
getter.update_config_with_fortinet_api_call(config_json, sid, v_url, "/pm/config/global/obj/user/local", "users_local", debug=debug_level)

# get all custom adoms:
q_get_custom_adoms = { "params": [ { "fields": ["name", "oid", "uuid"], "filter": ["create_time", "<>", 0] } ] }
adoms = getter.fortinet_api_call(sid, v_url, '/dvmdb/adom', payload=q_get_custom_adoms, debug=debug_level)

# get root adom:
q_get_root_adom = { "params": [ { "fields": ["name", "oid", "uuid"], "filter": ["name", "==", "root"] } ] }
adom_root = getter.fortinet_api_call(sid, v_url, '/dvmdb/adom', payload=q_get_root_adom, debug=debug_level).pop()
adoms.append(adom_root)
config_json.update({ "adoms": adoms })

# get all devices
# q_get_devices = { "params": [ { "fields": ["name", "desc", "hostname", "vdom", "ip", "mgmt_id", "mgt_vdom", "os_type", "os_ver", "platform_str", "dev_status"] } ] }
# getter.update_config_with_fortinet_api_call(config_json, sid, v_url, "/dvmdb/device", "devices", payload=q_get_devices, debug=debug_level)

# for each adom get devices
for adom in config_json["adoms"]:
  q_get_devices_per_adom = { "params": [ { "fields": ["name", "desc", "hostname", "vdom", "ip", "mgmt_id", "mgt_vdom", "os_type", "os_ver", "platform_str", "dev_status"] } ] }
  devs = getter.fortinet_api_call(sid, v_url, "/dvmdb/adom/" + adom["name"] + "/device", payload=q_get_devices_per_adom, debug=debug_level)
  adom.update({"devices": devs})

# for each adom get packages
for adom in config_json["adoms"]:
  packages = getter.fortinet_api_call(sid, v_url, "/pm/pkg/adom/" + adom["name"], debug=debug_level)
  adom.update({"packages": packages})

# now: find mapping device <--> package

# get rulebases 
# for adom in config_json["adoms"]:
#   for pkg_name in ["mypkg"]:
#     config_json.update({"rulebases_per_adom": {} })
#     api_path = "/pm/config/adom/" + adom['name'] + "/pkg/" + pkg_name + "/firewall/policy"
#     rules = getter.fortinet_api_call(sid, v_url, api_path, debug=debug_level)
#     config_json["devices_per_adom"].update({adom_name: rules})

#for layer in args.layer.split(','):
#    logging.debug ( "get_config - dealing with layer: " + layer )

# now dumping results to file
with open(config_filename, "w") as configfile_json:
    configfile_json.write(json.dumps(config_json))

logout_payload = {
  "id": 1,
  "jsonrpc": "1.0", 
  "method": "exec",
  "params": [ {} ],
}

logout_result = getter.api_call(v_url, 'sys/logout', logout_payload, sid, ssl_verification, proxy_string)
duration = int(time.time()) - starttime
logging.debug ( "fortiManager/get_config - duration: " + str(duration) + "s" )

sys.exit(0)

#!/usr/bin/python3
# create api user
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
sid = getter.login(args.user,api_password,api_host,args.port,api_domain,ssl_verification, proxy_string)
v_url = getter.get_api_url (sid, api_host, args.port, args.user, base_url, limit, test_version,ssl_verification, proxy_string)

config_json = { 'adoms': [] }
get_adoms = {
  "method": "get",
  "params": [{}],
}

# read all rulebases:
for layer in args.layer.split(','):
    logging.debug ( "get_config - dealing with layer: " + layer )

    adoms = getter.api_call(api_host, api_port, v_url, '/dvmdb/adom', get_adoms, sid, ssl_verification, proxy_string)

    config_json['adoms'].append(adoms)

# leaving rules, moving on to objects

with open(config_filename, "w") as configfile_json:
    configfile_json.write(json.dumps(config_json))

logout_payload = {
  "id": 1,
  "jsonrpc": "1.0", 
  "method": "exec",
  "params": [ {} ],
}

logout_result = getter.api_call(api_host, args.port, v_url, 'sys/logout', logout_payload, sid, ssl_verification, proxy_string)
duration = int(time.time()) - starttime
logging.debug ( "fortiManager/get_config - duration: " + str(duration) + "s" )

sys.exit(0)

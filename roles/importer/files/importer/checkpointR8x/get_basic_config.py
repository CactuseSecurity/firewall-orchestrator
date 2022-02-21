#!/usr/bin/python3

import argparse, requests, requests.packages
import time, sys, logging
from common import importer_base_dir
sys.path.append(importer_base_dir)
import common, getter, cpcommon

requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('-a', '--apihost', metavar='api_host', required=True, help='Check Point R8x management server')
parser.add_argument('-w', '--password', metavar='api_password_file', default='import_user_secret', help='name of the file to read the password for management server from')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-D', '--domain', metavar='api_domain', default='', help='name of Domain in a Multi-Domain Envireonment')
parser.add_argument('-l', '--layer', metavar='policy_layer_name(s)', required=True, help='name of policy layer(s) to read (comma separated)')
parser.add_argument('-k', '--package', metavar='policy package name', required=False, help='name of policy package (needed for nat rule retrieval)')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='', help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-i', '--limit', metavar='api_limit', default='150', help='The maximal number of returned results per HTTPS Connection; default=150')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0') 
parser.add_argument('-t', '--testing', metavar='version_testing', default='off', help='Version test, [off|<version number>]; default=off') 
parser.add_argument('-o', '--out', metavar='output_file', required=True, help='filename to write output in json format to')
parser.add_argument('-F', '--force', action='store_true', default=False, help='if set the import will be attempted without checking for changes before')

args = parser.parse_args()
if len(sys.argv)==1:
    parser.print_help(sys.stderr)
    sys.exit(1)

with open(args.password, "r") as password_file:
    api_password = password_file.read().rstrip()

details_level = "full"    # 'standard'
use_object_dictionary = 'false'
debug_level = int(args.debug)
common.set_log_level(log_level=debug_level, debug_level=debug_level)
starttime = int(time.time())
full_config_json = {}

# possible todo: get mgmt_details via API just from mgmt_name and dev_name?
# todo: allow for multiple gateways
mgm_details = {
    'hostname': args.apihost,
    'port': args.port,
    'user': args.user,
    'secret': api_password,
    'configPath': args.domain,
    'devices': [
        {
            'local_rulebase_name': args.layer,
            'global_rulebase_name': None,
            'package_name': args.package
        }
    ]
}

cpcommon.get_basic_config (full_config_json, mgm_details, config_filename=args.out,
    force=args.force, proxy=args.proxy, limit=args.limit, details_level=details_level, test_version=args.testing, debug_level=debug_level, ssl_verification=getter.set_ssl_verification(args.ssl))

duration = int(time.time()) - starttime
logging.debug ( "checkpointR8x/get_config - duration: " + str(duration) + "s" )

sys.exit(0)

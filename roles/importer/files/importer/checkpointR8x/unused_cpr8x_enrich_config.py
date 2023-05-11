#!/usr/bin/python3
import argparse, time
import json
import sys, os
import cp_const

from common import importer_base_dir, set_ssl_verification
sys.path.append(importer_base_dir)
sys.path.append(importer_base_dir + "/checkpointR8x")
from fwo_log import getFwoLogger
from cpcommon import enrich_config


parser = argparse.ArgumentParser(description='Read configuration from Check Point R8x management via API calls')
parser.add_argument('-a', '--apihost', metavar='api_host', required=True, help='Check Point R8x management server')
parser.add_argument('-w', '--password', metavar='api_password_file', default='import_user_secret', help='name of the file to read the password for management server from')
parser.add_argument('-u', '--user', metavar='api_user', default='fworch', help='user for connecting to Check Point R8x management server, default=fworch')
parser.add_argument('-p', '--port', metavar='api_port', default='443', help='port for connecting to Check Point R8x management server, default=443')
parser.add_argument('-D', '--domain', metavar='api_domain', default='', help='name of Domain in a Multi-Domain Envireonment')
parser.add_argument('-l', '--layer', metavar='policy_layer_name(s)', required=True, help='name of policy layer(s) to read (comma separated)')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='', help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-i', '--limit', metavar='api_limit', default='150', help='The maximal number of returned results per HTTPS Connection; default=150')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 4(DEBUG Console) 41(DEBUG File); default=0') 
parser.add_argument('-k', '--package', metavar='package_name', help='name of the package for a gateway - necessary for getting NAT rules')
parser.add_argument('-c', '--configfile', metavar='config_file', required=True, help='filename to read and write config in json format from/to')
parser.add_argument('-n', '--noapi', metavar='mode', default='false', help='if set to true (only in combination with mode=enrich), no api connections are made. Useful for testing only.')

args = parser.parse_args()
if len(sys.argv)==1:
    parser.print_help(sys.stderr)
    sys.exit(1)

with open(args.password, "r") as password_file:
    api_password = password_file.read().rstrip()

debug_level = int(args.debug)
logger = getFwoLogger()
config = {}
starttime = int(time.time())

# possible todo: get mgmt_details via API just from mgmt_name and dev_name?
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

result = enrich_config (config, mgm_details, noapi=False, limit=args.limit, details_level=cp_const.details_level)

duration = int(time.time()) - starttime
logger.debug ( "checkpointR8x/enrich_config - duration: " + str(duration) + "s" )

# dump new json file if config_filename is set
if args.config_filename != None and len(args.config_filename)>1:
    if os.path.exists(args.config_filename): # delete json file (to enabiling re-write)
        os.remove(args.config_filename)
    with open(args.config_filename, "w") as json_data:
        json_data.write(json.dumps(config))

sys.exit(0)

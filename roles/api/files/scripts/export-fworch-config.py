#!/usr/bin/python3
#  export-fworch-config.py: export the full config of the product itself for later import
#  does not contain any firewall config data, just the device config plus fworch user config

import sys, logging
import json, requests, requests.packages, argparse
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
import common, fwo_api

parser = argparse.ArgumentParser(
    description='Export fworch configuration into encrypted json file')
parser.add_argument('-o', '--out', metavar='output_file', required=True, help='filename to write output in json format to')
parser.add_argument('-u', '--user', metavar='user_name', default='admin', help='username for getting fworch config (default=admin')
parser.add_argument('-p', '--password', metavar='password_file', default=base_dir + '/etc/secrets/ui_admin_pwd', help='username for getting fworch config (default=$FWORCH_HOME/etc/secrets/ui_admin_pwd')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0',
                    help='Debug Level: 0=off, 1=send debug to console, 2=send debug to file, 3=keep temporary config files; default=0')
parser.add_argument('-x', '--proxy', metavar='proxy_string',
                    help='proxy server string to use, e.g. http://1.2.3.4:8080')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='',
                    help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')

args = parser.parse_args()
if len(sys.argv) == 1:
    parser.print_help(sys.stderr)
    sys.exit(1)

fwo_config_filename = base_dir + '/etc/fworch.json'
if args.ssl == '' or args.ssl == 'off':
    requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only
debug_level = int(args.debug)
common.set_log_level(log_level=debug_level, debug_level=debug_level)

# read fwo config (API URLs)
with open(fwo_config_filename, "r") as fwo_config:
    fwo_config = json.loads(fwo_config.read())
user_management_api_base_url = fwo_config['middleware_uri']
fwo_api_base_url = fwo_config['api_uri']

method = 'api/AuthenticationToken/Get'
ssl_mode = args.ssl

# authenticate to get JWT
with open(args.password, 'r') as file:
    exporter_pwd = file.read().replace('\n', '')
if 'proxy' in args:
    jwt = fwo_api.login(args.user, exporter_pwd, user_management_api_base_url,
                        method, ssl_verification=ssl_mode, proxy_string=args.proxy)
else:
    jwt = fwo_api.login(args.user, exporter_pwd, user_management_api_base_url,
                        method, ssl_verification=ssl_mode)

config_json = {}
# get device details

mgm_query = """
    query getFullDeviceDetails {
        management {
            mgm_id
            mgm_name
            ssh_hostname
            ssh_port
            ssh_private_key
            ssh_public_key
            ssh_user
            dev_typ_id
            config_path
            do_not_import
            force_initial_import
            hide_in_gui
            importer_hostname
            mgm_comment
            debug_level
            mgm_create
            mgm_update
            last_import_md5_complete_config
        }
        device {
            dev_id
            dev_name
            dev_typ_id
            mgm_id
            local_rulebase_name
            global_rulebase_name
            package_name
            dev_comment
            do_not_import
            force_initial_import
            hide_in_gui
            dev_create
            dev_update
        }
    }
    """
api_call_result = fwo_api.call(fwo_api_base_url, jwt, mgm_query, query_variables={}, role='admin')
if 'data' in api_call_result:
    config_json.update({ 'device_configuration': api_call_result['data'] })
else:
    logging.error('did not succeed in getting device details from API')
    sys.exit(1)

# todo: use a single source for fwo_api between this script and importer
# todo: use a single source for graphql queries between importer, config im/exporter, C#

# todo: get more config data
    # get user related data:
        # ldap servers
        # tenants
        # uiusers including roles & groups & tenants

# todo: encrypt config before writing to file

with open(args.out, 'w') as file:
    file.write(json.dumps(config_json, indent=3))

sys.exit(0)

#!/usr/bin/python3
# fwo-execute-graphql.py: run a graphql query/mutation with variables against the FWO API
# 
import sys
import json, requests, requests.packages, argparse
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
import common_scripts, fwo_api

parser = argparse.ArgumentParser(
    description='Export fworch configuration into encrypted json file')
parser.add_argument('-i', '--input_file', metavar='input_file', required=True, help='filename to read the config to import from')
parser.add_argument('-u', '--user', metavar='user_name', default='admin', help='username for writing fworch config (default=admin')
parser.add_argument('-p', '--password', metavar='password_file', default=base_dir + '/etc/secrets/ui_admin_pwd', help='username for writing fworch config (default=$FWORCH_HOME/etc/secrets/ui_admin_pwd')
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
common_scripts.set_log_level(log_level=debug_level, debug_level=debug_level)

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

with open(args.input_file, 'r') as file:
   query = json.loads(file.read())

# todo: optionally decrypt graphql code

result = fwo_api.call(fwo_api_base_url, jwt, query['query_string'], query['query_variables'], role='admin')

# todo: get more config data
    # get user related data:
        # ldap servers
        # tenants
        # uiusers including roles & groups & tenants

sys.exit(0)

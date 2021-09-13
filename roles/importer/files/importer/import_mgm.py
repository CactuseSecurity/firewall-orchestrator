#!/usr/bin/python3

# add main importer loop in pyhton (also able to run distributed)
#   run import loop every x seconds (adjust sleep time per management depending on the change frequency )
#      import_mgm.py: import a single management (if no import for it is running)
#         lock mgmt for import via FWORCH API call, generating new import_id y
#         check if we need to import (no md5, api call if anything has changed since last import)
#         get complete config (get, enrich, parse)
#         write into json dict write json dict to new table (single entry for complete config)
#         trigger import from json into csv and from there into destination tables
#         release mgmt for import via FWORCH API call (also removing import_id y data from import_tables?)
#         no changes: remove import_control?

import time, datetime
import json
import requests, requests.packages
import importlib
import argparse, logging, socket
from pathlib import Path
import sys
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
import common, fwo_api

parser = argparse.ArgumentParser(
    description='Read configuration from FW management via API calls')
parser.add_argument('-m', '--mgm_id', metavar='management_id',
                    required=True, help='FWORCH DB ID of the management server to import')
parser.add_argument('-c', '--clear', metavar='clear_management', default=False,
                    help='If set the import will delete all data for the given management instead of importing')
parser.add_argument('-f', '--force', metavar='force_import', default=False,
                    help='If set the import will be attempted even if there seem to be no changes.')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0',
                    help='Debug Level: 0=off, 1=send debug to console, 2=send debug to file, 3=keep temporary config files; default=0')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='',
                    help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='',
                    help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-i', '--limit', metavar='api_limit', default='500',
                    help='The maximal number of returned results per HTTPS Connection; default=500')
parser.add_argument('-t', '--testing', metavar='version_testing',
                    default='off', help='Version test, [off|<version number>]; default=off')
parser.add_argument('-o', '--out', metavar='output_file',
                    default=False, help='filename to write output in json format to, "False" if not writing to file')

args = parser.parse_args()
if len(sys.argv) == 1:
    parser.print_help(sys.stderr)
    sys.exit(1)

error_count = 0
importer_user_name = 'importer'  # todo: move to config file?
fwo_config_filename = base_dir + '/etc/fworch.json'
importer_pwd_file = base_dir + '/etc/secrets/importer_pwd'
import_tmp_path = base_dir + '/tmp/import'

start_time = int(time.time())
requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only
debug_level = int(args.debug)
common.set_log_level(log_level=debug_level, debug_level=debug_level)

# read fwo config (API URLs)
with open(fwo_config_filename, "r") as fwo_config:
    fwo_config = json.loads(fwo_config.read())
user_management_api_base_url = fwo_config['middleware_uri']
fwo_api_base_url = fwo_config['api_uri']

method = 'AuthenticateUser'
ssl_mode = args.ssl
proxy_setting = args.proxy

# authenticate to get JWT
with open(importer_pwd_file, 'r') as file:
    importer_pwd = file.read().replace('\n', '')
jwt = fwo_api.login(importer_user_name, importer_pwd, user_management_api_base_url,
                    method, ssl_verification=ssl_mode, proxy_string=proxy_setting)

# get mgm_details (fw-type, port, ip, user credentials):
mgm_details = fwo_api.get_mgm_details(
    fwo_api_base_url, jwt, {"mgmId": int(args.mgm_id)})

# only run if this is the correct import module
if mgm_details['importerHostname'] != socket.gethostname():
    logging.info(
        "we are not responsilble for importing this management - so resting")
    sys.exit(0)

# set import lock
current_import_id = fwo_api.lock_import(
    fwo_api_base_url, jwt, {"mgmId": int(args.mgm_id)})
if current_import_id == -1:
    logging.warning("error while setting import lock for management id " +
                    str(args.mgm_id) + ", import already running?")
    sys.exit(1)

logging.info("start import of management " + str(args.mgm_id) +
             ", import_id=" + str(current_import_id))

full_config_json = {}
config2import = {}
Path(import_tmp_path).mkdir(parents=True, exist_ok=True)

config_filename = import_tmp_path + '/mgm_id_' + \
    str(args.mgm_id) + '_config.json'

with open(config_filename, "w") as json_data:  # create empty config file
    json_data.write(json.dumps(full_config_json))
secret_filename = base_dir + '/tmp/import/mgm_id_' + \
    str(args.mgm_id) + '_secret.txt'
with open(secret_filename, "w") as secret:  # write pwd to disk to avoid passing it as parameter
    secret.write(mgm_details['secret'])

rulebase_string = ''
for device in mgm_details['devices']:
    rulebase_string += device['rulebase'] + ','
rulebase_string = rulebase_string[:-1]  # remove final comma

# pick product-specific importer:
fw_module_name = mgm_details['deviceType']['name'].lower().replace(
    ' ', '') + mgm_details['deviceType']['version']+'.fwcommon'
fw_module = importlib.import_module(fw_module_name)

# get config from FW API and write config to json file "config_filename"
fw_module.get_config(
    config2import, current_import_id, base_dir, mgm_details, secret_filename, rulebase_string, config_filename, debug_level)

# now we import the config via API:
error_count += fwo_api.import_json_config(fwo_api_base_url, jwt, args.mgm_id, {
    "importId": current_import_id, "mgmId": args.mgm_id, "config": config2import})

# todo: if error_count>0:
#    get error from import_control table? and show it

change_count = fwo_api.count_changes_per_import(
    fwo_api_base_url, jwt, current_import_id)

if change_count > 0 or error_count > 0:  # store full config in case of change or error
    with open(config_filename, "r") as json_data:
        full_config_json = json.load(json_data)

    error_count += fwo_api.store_full_json_config(fwo_api_base_url, jwt, args.mgm_id, {
        "importId": current_import_id, "mgmId": args.mgm_id, "config": full_config_json})

stop_time = int(time.time())
stop_time_string = datetime.datetime.now().isoformat()

# delete configs of imports without changes (if no error occured)
if change_count == 0 and error_count == 0:
    error_count += fwo_api.delete_json_config(
        fwo_api_base_url, jwt, {"importId": current_import_id})
    # error_count += fwo_api.delete_import(fwo_api_base_url, jwt, current_import_id)
# finalize remport by unlocking it
error_count += fwo_api.unlock_import(fwo_api_base_url, jwt, int(
    args.mgm_id), stop_time_string, current_import_id, error_count, change_count)


print("import_mgm.py: import no. " + str(current_import_id) + " for management " + str(args.mgm_id) + " ran " +
      str("with" if error_count else "without") + " errors, change_count: " + str(change_count) + ", duration: " +
      str(int(time.time()) - start_time) + "s")

sys.exit(0)

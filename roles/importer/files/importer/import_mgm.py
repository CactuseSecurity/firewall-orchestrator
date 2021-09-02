#!/usr/bin/python3
# master plan import target design

# add main importer loop in pyhton (also able to run distributed)
#   run import loop every x seconds (adjust sleep time per management depending on the change frequency )
#      import a single management (if no import for it is running)
#         lock mgmt for import via FWORCH API call, generating new import_id y
#         check if we need to import (no md5, api call if anything has changed since last import)
#         get complete config (get, enrich, parse)
#         write into json dict write json dict to new table (single entry for complete config)
#         trigger import from json into csv and from there into destination tables
#         release mgmt for import via FWORCH API call (also removing import_id y data from import_tables?)
#         no changes: remove import_control?

import fwo_api
import datetime
import json
import os
import sys
import requests
import requests.packages.urllib3
import time
import argparse
import logging
import common
from symbol import except_clause
base_dir = '/usr/local/fworch'

sys.path.append(base_dir + r"/importer")
# use CACTUS::read_config;

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

error_occured = False
importer_user_name = 'importer'
start_time = int(time.time())

requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

debug_level = int(args.debug)
common.set_log_level(log_level=debug_level, debug_level=debug_level)

user_management_api_base_url = 'https://localhost:8888/'
method = 'AuthenticateUser'
ssl_mode = args.ssl
proxy_setting = args.proxy

# authenticate to get JWT
importer_pwd_file = base_dir + '/etc/secrets/importer_pwd'
with open(importer_pwd_file, 'r') as file:
    importer_pwd = file.read().replace('\n', '')
jwt = fwo_api.login(importer_user_name, importer_pwd, user_management_api_base_url,
                    method, ssl_verification=ssl_mode, proxy_string=proxy_setting)

# set import lock
fwo_api_base_url = 'https://localhost:9443/api/v1/graphql'
query_variables = {"mgmId": int(args.mgm_id)}
current_import_id = fwo_api.lock_import(fwo_api_base_url, jwt, query_variables)
if current_import_id == -1:
    logging.warn("error while setting import lock for management id " +
                 str(args.mgm_id) + ", import already running?")
    sys.exit(1)

logging.info("start import of management " + str(args.mgm_id) +
             ", import_id=" + str(current_import_id))
# from here on we have an import lock and need to unlock it before exiting

mgm_details = fwo_api.get_mgm_details(fwo_api_base_url, jwt, query_variables)[
    'data']['management'][0]  # get mgm_details (fw-type, port, ip, user credentials)

full_config_json = {}
config_filename = base_dir + '/tmp/import/mgm_id_' + \
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

# get config from FW API and write to json file
if mgm_details['deviceType']['name'] == 'Check Point' and mgm_details['deviceType']['version'] == 'R8x':
    logging.info("found Check Point R8x")
    get_config_cmd = "cd " + base_dir + "/importer/checkpointR8x && ./get_config.py -a " + \
        mgm_details['hostname'] + " -u " + mgm_details['user'] + " -w " + \
        secret_filename + " -l \"" + rulebase_string + \
        "\" -o " + config_filename + " -d " + str(debug_level)
    get_config_cmd += " && ./enrich_config.py -a " + mgm_details['hostname'] + " -u " + mgm_details['user'] + " -w " + \
        secret_filename + " -l \"" + rulebase_string + \
        "\" -c " + config_filename + " -d " + str(debug_level)
    os.system(get_config_cmd)

    # now parse all parts (objects, rulebases) and create csv equivalent json config
    import_cmd = "cd " + base_dir + "/importer/checkpointR8x && ./parse_config.py -n -f " + config_filename + " -d " + str(debug_level) + \
        " && ./parse_config.py -f " + config_filename + " -s -d " + str(debug_level) + \
        " && ./parse_config.py -f " + config_filename + \
        " -u -d " + str(debug_level)
    for rulebase in rulebase_string.split(','):
        import_cmd += " && ./parse_config.py -f " + config_filename + \
            " -r \"" + rulebase + "\" -d " + str(debug_level)
    os.system(import_cmd)

if mgm_details['deviceType']['name'] == 'Fortinet' and mgm_details['deviceType']['version'] == '5.x-6.x':
    logging.info("ignoring legacy fortinet devices for now")
if mgm_details['deviceType']['name'] == 'FortiManager':
    logging.info("found fortiManager")
    os.system("fortiManager.parse_config -f " + config_filename)

# read json file and write to FWO database via FWO API
with open(config_filename, "r") as json_data:
    full_config_json = json.load(json_data)

import_result = fwo_api.store_full_json_config(fwo_api_base_url, jwt, args.mgm_id, {
    "importId": current_import_id, "config": full_config_json})

# some possible enhancements:
# if ($clear_whole_mgm_config) -->
# exec_pgsql_cmd_return_value("SELECT show_change_summary($current_import_id)");
# exec_pgsql_cmd_no_result("DELETE FROM import_control WHERE control_id=$current_import_id"); # remove imports with unchanged data
# 'UPDATE import_control SET successful_import=TRUE' . (($changes_found)? ', changes_found=TRUE': '') . ' WHERE control_id=' . $current_import_id;
# `cp -f $fworch_workdir/cfg/*.cfg /var/itsecorg/fw-config/`; # special backup for several configs - dos-box

stop_time = int(time.time())
stop_time_string = datetime.datetime.now().isoformat()

changes_in_import_control = fwo_api.unlock_import(fwo_api_base_url, jwt, int(
    args.mgm_id), stop_time_string, current_import_id, error_occured)
error_occured = (changes_in_import_control != 1)

if error_occured:
    logging.warn("import ran with errors, duration: " +
                 str(int(time.time()) - start_time) + "s")
else:
    logging.debug("import ran without errors, duration: " +
                  str(int(time.time()) - start_time) + "s")
sys.exit(0)

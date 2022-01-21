#!/usr/bin/python3
#      import_mgm.py: import a single management (if no import for it is running)
#         lock mgmt for import via FWORCH API call, generating new import_id y
#         check if we need to import (no md5, api call if anything has changed since last import)
#         get complete config (get, enrich, parse)
#         write into json dict write json dict to new table (single entry for complete config)
#         trigger import from json into csv and from there into destination tables
#         release mgmt for import via FWORCH API call (also removing import_id y data from import_tables?)
#         no changes: remove import_control?

import sys, os, time, datetime
import json, requests, requests.packages, argparse
import importlib, logging, socket
from pathlib import Path
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
import common, fwo_api

parser = argparse.ArgumentParser(
    description='Read configuration from FW management via API calls')
parser.add_argument('-m', '--mgm_id', metavar='management_id',
                    required=True, help='FWORCH DB ID of the management server to import')
parser.add_argument('-c', '--clear', metavar='clear_management',
                    help='If set the import will delete all data for the given management instead of importing')
parser.add_argument('-f', '--force', action='store_true', default=False,
                    help='If set the import will be attempted without checking for changes before')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0',
                    help='Debug Level: 0=off, 1=send debug to console, 2=send debug to file, 3=save noramlized config file; 4=additionally save native config file; default=0. \n' +\
                        'config files are saved to $FWORCH/tmp/import dir')
parser.add_argument('-x', '--proxy', metavar='proxy_string',
                    help='proxy server string to use, e.g. http://1.2.3.4:8080')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='',
                    help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-l', '--limit', metavar='api_limit', default='150',
                    help='The maximal number of returned results per HTTPS Connection; default=150')
parser.add_argument('-i', '--in_file', metavar='config_file_input',
                    help='if set, the config will not be fetched from firewall but read from native json config file specified here')

args = parser.parse_args()
if len(sys.argv) == 1:
    parser.print_help(sys.stderr)
    sys.exit(1)

error_count = common.import_management(
    mgm_id=args.mgm_id, in_file=args.in_file, debug=args.debug, ssl=args.ssl, proxy=args.proxy, \
    force=args.force, limit=args.limit)
sys.exit(error_count)

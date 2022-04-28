#!/usr/bin/python3
import sys, fwo_log, traceback
import argparse
from common import importer_base_dir, import_management
sys.path.append(importer_base_dir)

parser = argparse.ArgumentParser(
    description='Read configuration from FW management via API calls')
parser.add_argument('-m', '--mgm_id', metavar='management_id',
                    required=True, help='FWORCH DB ID of the management server to import')
parser.add_argument('-c', '--clear', action='store_true', default=False,
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
                    help='if set, the config will not be fetched from firewall but read from native json config file specified here; may also be an url.')

args = parser.parse_args()
if len(sys.argv) == 1:
    parser.print_help(sys.stderr)
    sys.exit(1)

logger = fwo_log.getFwoLogger(debug_level=args.debug)

try:
    error_count = import_management(
        mgm_id=args.mgm_id, in_file=args.in_file, debug_level=args.debug, ssl=args.ssl, proxy=args.proxy, \
        force=args.force, limit=args.limit, clearManagementData=args.clear)
except:
    logger.error("import-mgm - error while importing mgm_id=" + str(args.mgm_id) + ": " + str(traceback.format_exc()))
    error_count = 1

sys.exit(error_count)

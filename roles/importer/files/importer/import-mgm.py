#!/usr/bin/python3
import sys, traceback
from fwo_log import getFwoLogger
import argparse
import requests, requests.packages
from common import importer_base_dir, import_management
import fwo_globals, fwo_config
sys.path.append(importer_base_dir)


if __name__ == "__main__": 
    parser = argparse.ArgumentParser(
        description='Read configuration from FW management via API calls')
    parser.add_argument('-m', '--mgmId', metavar='management_id',
                        required=True, help='FWORCH DB ID of the management server to import')
    parser.add_argument('-c', '--clear', action='store_true', default=False,
                        help='If set the import will delete all data for the given management instead of importing')
    parser.add_argument('-f', '--force', action='store_true', default=False,
                        help='If set the import will be attempted without checking for changes or if the importer module is the one defined')
    parser.add_argument('-d', '--debug', metavar='debug_level', default='0',
                        help='Debug Level:  \
                                    0=off, \
                                    1=send debug to console, \
                                    2=send debug to file, \
                                    3=save noramlized config file, \
                                    4=additionally save native config file, \
                                    8=send native config (as read from firewall) to standard out, \
                                    9=send normalized config to standard out, \
                                    (default=0), \
                                    config files are saved to $FWORCH/tmp/import dir')
    parser.add_argument('-v', "--verify_certificates", action='store_true', default = None, 
                        help = "verify certificates")
    parser.add_argument('-s', "--suppress_certificate_warnings", action='store_true', default = None, 
                        help = "suppress certificate warnings")
    parser.add_argument('-l', '--limit', metavar='api_limit', default='150',
                        help='The maximal number of returned results per HTTPS Connection; default=150')
    parser.add_argument('-i', '--in_file', metavar='config_file_input',
                        help='if set, the config will not be fetched from firewall but read from json config (native or normalized) file specified here; may also be an url.')

    args = parser.parse_args()
    if len(sys.argv) == 1:
        parser.print_help(sys.stderr)
        sys.exit(1)

    fwo_config = fwo_config.readConfig()
    fwo_globals.setGlobalValues(verify_certs_in=args.verify_certificates, 
        suppress_cert_warnings_in=args.suppress_certificate_warnings,
        debug_level_in=args.debug)
    if args.suppress_certificate_warnings:
        requests.packages.urllib3.disable_warnings()
    logger = getFwoLogger()

    try:
        error_count = import_management(
            mgmId=args.mgmId, in_file=args.in_file, debug_level_in=args.debug, ssl_verification=args.verify_certificates,
            force=args.force, limit=args.limit, clearManagementData=args.clear, suppress_cert_warnings_in=args.suppress_certificate_warnings)
    except SystemExit:
        logger.error("import-mgm - error while importing mgmId=" + str(args.mgmId)  + ": " + str(traceback.format_exc()))
        error_count = 1
    except:
        logger.error("import-mgm - error while importing mgmId=" + str(args.mgmId) + ": " + str(traceback.format_exc()))
        error_count = 1

    sys.exit(error_count)

#!/usr/bin/env python3

import sys
import traceback
import warnings
from fwo_log import get_fwo_logger
import argparse
import urllib3
from common import importer_base_dir, import_management
import fwo_globals
from fwo_base import init_service_provider, register_global_state
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoApiLoginFailed
from fwo_const import base_dir, importer_base_dir
from model_controllers.import_state_controller import ImportStateController


if importer_base_dir not in sys.path:
    sys.path.append(importer_base_dir)

def get_fwo_jwt(importUser: str, importPwd: str, userManagementApi: str) -> str | None:
    logger = get_fwo_logger()
    try:
        jwt = FwoApi.login(importUser, importPwd, userManagementApi)
        return jwt
    except FwoApiLoginFailed as e:
        logger.error(e.message)
    except Exception:
        logger.error("import-main-loop - unspecified error during FWO API login - skipping: " + str(traceback.format_exc()))


def main(mgmId: int, file: str | None = None, debugLevel: int = 0, verifyCertificates: bool = False, force: bool = False, limit: int = 150, clearManagementData: bool = False, suppressCertificateWarnings: bool = False):
    service_provider = init_service_provider()
    fwo_config = service_provider.get_fwo_config()
    fwo_api_base_url = fwo_config['fwo_api_base_url']
    fwo_major_version = fwo_config['fwo_major_version']
    user_management_api_base_url = fwo_config['user_management_api_base_url']
    fwo_globals.set_global_values(verifyCertificates, suppressCertificateWarnings, debugLevel)
    if suppressCertificateWarnings: urllib3.disable_warnings()

    logger = get_fwo_logger()

    logger.info("import-mgm starting ...")
    if importer_base_dir not in sys.path:
        sys.path.append(importer_base_dir)

    importer_user_name = 'importer'  # todo: move to config file?
    importer_pwd_file = base_dir + '/etc/secrets/importer_pwd'

    try:
            importer_pwd = open(importer_pwd_file).read().replace('\n', '')
    except Exception:
        logger.error("import-main-loop - error while reading importer pwd file")
        raise

    jwt = get_fwo_jwt(importer_user_name, importer_pwd, user_management_api_base_url)
    # check if login was successful - if not, wait and retry
    if jwt is None:
        logger.error("import-mgm - cannot proceed without successful login - exiting")
        return

    fwo_api = FwoApi(fwo_api_base_url, jwt)
    fwo_api_call = FwoApiCall(fwo_api)

    urllib3.disable_warnings()  # suppress ssl warnings only
    verifyCertificates = fwo_api_call.get_config_value(key='importCheckCertificates')=='True'
    suppressCertificateWarnings = fwo_api_call.get_config_value(key='importSuppressCertificateWarnings')=='True'
    if not suppressCertificateWarnings:
        warnings.resetwarnings()

    import_state = ImportStateController.initializeImport(mgmId, jwt, debugLevel, suppressCertificateWarnings, verifyCertificates, force, fwo_major_version, clearManagementData, isFullImport=True)
    register_global_state(import_state)

    import_management(mgmId, fwo_api_call, verifyCertificates, debugLevel, limit, clearManagementData, suppressCertificateWarnings, file)
    

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

    service_provider = init_service_provider()
    fwo_config = service_provider.get_fwo_config()

    fwo_globals.set_global_values(verify_certs_in=args.verify_certificates, 
        suppress_cert_warnings_in=args.suppress_certificate_warnings,
        debug_level_in=args.debug)
    if args.suppress_certificate_warnings:
        urllib3.disable_warnings()
    logger = get_fwo_logger()

    try:
        main(
            int(args.mgmId), 
            args.in_file, 
            int(args.debug), 
            args.verify_certificates,
            args.force, 
            int(args.limit), 
            args.clear, 
            args.suppress_certificate_warnings,
        )
    except Exception:
        logger.error("import-mgm - error while importing mgmId=" + str(args.mgmId) + ": " + str(traceback.format_exc()))

    sys.exit()

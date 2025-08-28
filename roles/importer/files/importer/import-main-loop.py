# main importer loop in python (also able to run distributed)
# run import loop every x seconds (adjust sleep time per management depending on the change frequency )

import traceback
import argparse
import sys
import time
import requests, warnings
import urllib3

from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from model_controllers.management_controller import ManagementController
from common import import_management
from fwo_log import getFwoLogger #, LogLock
import fwo_globals, fwo_config
from fwo_const import base_dir, importer_base_dir
from fwo_exceptions import FwoApiLoginFailed, FwoApiFailedLockImport, FwLoginFailed
from model_controllers.import_state_controller import ImportStateController
from fwo_base import register_services
from services.enums import Services


def get_fwo_jwt(importUser, importPwd, userManagementApi) -> tuple [str, bool]:
    skipping = False
    try:
        jwt = FwoApi.login(importUser, importPwd, userManagementApi)
        return jwt, skipping
    except FwoApiLoginFailed as e:
        logger.error(e.message)
        skipping = True
    except Exception:
        logger.error("import-main-loop - unspecified error during FWO API login - skipping: " + str(traceback.format_exc()))
        skipping = True
        return "", skipping
    return "", skipping


if __name__ == '__main__':
    parser = argparse.ArgumentParser(
        description='Run import loop across all managements to read configuration from FW managements via API calls')
    parser.add_argument('-d', '--debug', metavar='debug_level', default='0',
                        help='Debug Level: 0=off, 1=send debug to console, 2=send debug to file, 3=keep temporary config files; default=0')
    parser.add_argument('-v', "--verify_certificates", action='store_true', default = None, 
                        help = "verify certificates")
    parser.add_argument('-s', "--suppress_certificate_warnings", action='store_true', default = None, 
                        help = "suppress certificate warnings")
    parser.add_argument('-c', '--clear', action='store_true', default=False,
                    help='If set all imports will run once to delete all data instead of importing')
    parser.add_argument('-f', '--force', action='store_true', default=False,
                    help='If set all imports will be run without checking for changes before')

    args = parser.parse_args()

    ### initializing
    fwo_config = fwo_config.readConfig()
    fwo_api_base_url = fwo_config['fwo_api_base_url']
    fwo_major_version = fwo_config['fwo_major_version']
    user_management_api_base_url = fwo_config['user_management_api_base_url']
    fwo_globals.set_global_values(verify_certs_in=args.verify_certificates, 
        suppress_cert_warnings_in=args.suppress_certificate_warnings,
        debug_level_in=args.debug)
    if args.suppress_certificate_warnings: urllib3.disable_warnings()

    debug_level = int(args.debug)
    logger = getFwoLogger()

    logger.info("importer-main-loop starting ...")
    if importer_base_dir not in sys.path:
        sys.path.append(importer_base_dir)
    importer_user_name = 'importer'  # todo: move to config file?
    fwo_config_filename = base_dir + '/etc/fworch.json'
    importer_pwd_file = base_dir + '/etc/secrets/importer_pwd'

    # setting defaults (only as fallback if config defaults cannot be fetched via API):
    api_fetch_limit = 150
    sleep_timer = 90
    jwt = ""
    mgm_ids = []
    mgm_details = {}

    while True:
        if fwo_globals.shutdown_requested:
            logger.info("import-main-loop - shutdown requested. Exiting...")
            raise SystemExit("import-main-loop - shutdown requested")
        skipping = False
        try:
            with open(importer_pwd_file, 'r') as file:
                importer_pwd = file.read().replace('\n', '')
        except Exception:
            logger.error("import-main-loop - error while reading importer pwd file")
            raise

        jwt, skipping = get_fwo_jwt(importer_user_name, importer_pwd, user_management_api_base_url)
        fwo_api = FwoApi(fwo_api_base_url, jwt)
        fwo_api_call = FwoApiCall(fwo_api)

        service_provider = register_services()
        global_state = service_provider.get_service(Services.GLOBAL_STATE)

        urllib3.disable_warnings()  # suppress ssl warnings only
        verify_certificates = fwo_api_call.get_config_value(key='importCheckCertificates')=='True'
        suppress_certificate_warnings = fwo_api_call.get_config_value(key='importSuppressCertificateWarnings')=='True'
        if not suppress_certificate_warnings:
            warnings.resetwarnings()
        
        if not skipping:

            try:
                all_managers_with_submanagers = fwo_api_call.get_mgm_ids({})
            except Exception:
                logger.error(f"import-main-loop - error while getting FW management ids: {str(traceback.format_exc())}")
                skipping = True
                continue

            api_fetch_limit = fwo_api_call.get_config_value(key='fwApiElementsPerFetch')
            sleep_timer = fwo_api_call.get_config_value(key='importSleepTime')
            if api_fetch_limit == None:
                api_fetch_limit = 150
            if sleep_timer == None:
                sleep_timer = 90

            if not skipping:
                for mgm in all_managers_with_submanagers:
                    if fwo_globals.shutdown_requested:
                        logger.info("import-main-loop - shutdown requested. Exiting...")
                        raise SystemExit("import-main-loop - shutdown requested")

                    service_provider = register_services()
                    global_state = service_provider.get_service(Services.GLOBAL_STATE)
                    import_state = ImportStateController.initializeImport(mgm['id'], fwo_api_uri=fwo_api_base_url, jwt=jwt, debugLevel=debug_level, version=fwo_major_version)
                    global_state.import_state = import_state

                    if skipping:
                        continue
                    try:
                        mgm_controller = ManagementController(hostname='', id=int(import_state.MgmDetails.Id), 
                                                uid='', devices={}, name='', deviceTypeName='', deviceTypeVersion='')
                        mgm_details = mgm_controller.get_mgm_details(fwo_api, import_state.MgmDetails.Id)                         
                    except Exception:
                        logger.error("import-main-loop - error while getting FW management details for mgm_id=" + str(import_state.MgmDetails.Id) + " - skipping: " + str(traceback.format_exc()))
                        skipping = True
                    if not skipping and mgm_details["deviceType"]["id"] in (9, 11, 17, 22, 23, 24):  # only handle CPR8x Manager, fortiManager, Cisco MgmCenter, Palo Panorama, Palo FW, FortiOS REST
                        logger.debug("import-main-loop: starting import of mgm_id=" + str(import_state.MgmDetails.Id))
                        try:
                            import_result = import_management(mgmId=import_state.MgmDetails.Id, debug_level_in=debug_level, version=import_state.ImportVersion,
                                clearManagementData=args.clear, force=args.force, limit=int(api_fetch_limit), services_registered=True)
                        except (FwoApiFailedLockImport, FwLoginFailed) as e:
                            logger.info(f"import-main-loop - minor error while importing mgm_id={str(import_state.MgmDetails.Id)}, {str(traceback.format_exc())}") 
                            pass # minor errors for a single mgm, go to next one
                        except Exception: # all other exceptions are logged here
                            logger.error(f"import-main-loop - unspecific error while importing mgm_id={str(import_state.MgmDetails.Id)}, {str(traceback.format_exc())}")
        if args.clear:
            break # while loop                                    
        logger.info("import-main-loop.py: sleeping between loops for " + str(sleep_timer) + " seconds")
        counter=0
        while counter < int(sleep_timer):
            if fwo_globals.shutdown_requested:
                logger.info("import-main-loop - shutdown requested. Exiting...")
                raise SystemExit("import-main-loop - shutdown requested")
            time.sleep(1)
            counter += 1

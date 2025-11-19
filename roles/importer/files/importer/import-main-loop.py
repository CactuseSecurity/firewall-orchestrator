# main importer loop in python (also able to run distributed)
# run import loop every x seconds (adjust sleep time per management depending on the change frequency )

import traceback
import argparse
import sys
import time
import warnings
import urllib3

from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from model_controllers.management_controller import ManagementController, DeviceInfo, ConnectionInfo, CredentialInfo, ManagerInfo, DomainInfo
from common import import_management
from fwo_log import getFwoLogger #, LogLock
import fwo_globals
from fwo_const import base_dir, importer_base_dir
from fwo_exceptions import FwoApiLoginFailed, FwoApiFailedLockImport, FwLoginFailed
from model_controllers.import_state_controller import ImportStateController
from fwo_base import init_service_provider
from services.global_state import GlobalState
from services.enums import Services


def get_fwo_jwt(importUser: str, importPwd: str, userManagementApi: str) -> str | None:
    logger = getFwoLogger()
    try:
        jwt = FwoApi.login(importUser, importPwd, userManagementApi)
        return jwt
    except FwoApiLoginFailed as e:
        logger.error(e.message)
    except Exception:
        logger.error("import-main-loop - unspecified error during FWO API login - skipping: " + str(traceback.format_exc()))



def wait_with_shutdown_check(sleep_time: int):
    counter = 0
    logger = getFwoLogger()
    while counter < sleep_time:
        if fwo_globals.shutdown_requested:
            logger.info("import-main-loop - shutdown requested. Exiting...")
            raise SystemExit("import-main-loop - shutdown requested")
        time.sleep(1)
        counter += 1



def main_loop(verify_certificates: bool | None = None, suppress_certificate_warnings: bool | None = None, debug_level: int = 0, clear: bool = False, force: bool = False):
    service_provider = init_service_provider()
    fwo_config = service_provider.get_service(Services.FWO_CONFIG)
    fwo_api_base_url = fwo_config['fwo_api_base_url']
    fwo_major_version = fwo_config['fwo_major_version']
    user_management_api_base_url = fwo_config['user_management_api_base_url']
    fwo_globals.set_global_values(verify_certs_in=verify_certificates, 
        suppress_cert_warnings_in=args.suppress_certificate_warnings,
        debug_level_in=debug_level)
    if suppress_certificate_warnings: urllib3.disable_warnings()

    
    logger = getFwoLogger()

    logger.info("importer-main-loop starting ...")
    if importer_base_dir not in sys.path:
        sys.path.append(importer_base_dir)
    importer_user_name = 'importer'  # todo: move to config file?
    importer_pwd_file = base_dir + '/etc/secrets/importer_pwd'

    # setting defaults (only as fallback if config defaults cannot be fetched via API):
    api_fetch_limit: int = 150
    sleep_timer: int = 90

    while True:
        wait_with_shutdown_check(0)

        try:
            importer_pwd = open(importer_pwd_file).read().replace('\n', '')
        except Exception:
            logger.error("import-main-loop - error while reading importer pwd file")
            raise

        jwt = get_fwo_jwt(importer_user_name, importer_pwd, user_management_api_base_url)
        # check if login was successful - if not, wait and retry
        if jwt is None:
            wait_with_shutdown_check(sleep_timer)
            continue

        fwo_api = FwoApi(fwo_api_base_url, jwt)
        fwo_api_call = FwoApiCall(fwo_api)

        global_state = service_provider.get_service(Services.GLOBAL_STATE)
        
        urllib3.disable_warnings()  # suppress ssl warnings only
        verify_certificates = fwo_api_call.get_config_value(key='importCheckCertificates')=='True'
        suppress_certificate_warnings = fwo_api_call.get_config_value(key='importSuppressCertificateWarnings')=='True'
        if not suppress_certificate_warnings:
            warnings.resetwarnings()

        try:
            mgm_ids = fwo_api_call.get_mgm_ids()
        except Exception:
            logger.error(f"import-main-loop - error while getting FW management ids: {str(traceback.format_exc())}")
            wait_with_shutdown_check(sleep_timer)
            continue

        api_fetch_limit = int(fwo_api_call.get_config_value(key='fwApiElementsPerFetch') or api_fetch_limit)
        sleep_timer = int(fwo_api_call.get_config_value(key='importSleepTime') or sleep_timer) 

        ## loop through all managements
        for mgm_id in mgm_ids:
            wait_with_shutdown_check(0)

            service_provider = init_service_provider()
            global_state: GlobalState = service_provider.get_service(Services.GLOBAL_STATE)
            import_state = ImportStateController.initializeImport(mgm_id, fwo_api_uri=fwo_api_base_url, jwt=jwt, debugLevel=debug_level, version=fwo_major_version)
            global_state.import_state = import_state

            try:
                mgm_controller = ManagementController(
                    mgm_id=int(import_state.MgmDetails.Id), uid='', devices=[],
                    device_info=DeviceInfo(),
                    connection_info=ConnectionInfo(),
                    importer_hostname='',
                    credential_info=CredentialInfo(),
                    manager_info=ManagerInfo(),
                    domain_info=DomainInfo()
                )
                mgm_details = mgm_controller.get_mgm_details(fwo_api, import_state.MgmDetails.Id)
            except Exception:
                logger.error("import-main-loop - error while getting FW management details for mgm_id=" + str(import_state.MgmDetails.Id) + " - skipping: " + str(traceback.format_exc()))
                wait_with_shutdown_check(sleep_timer)
                continue

            # only handle CPR8x Manager, fortiManager, Cisco MgmCenter, Palo Panorama, Palo FW, FortiOS REST, Cisco Asa, Asa on FirePower
            if mgm_details["deviceType"]["id"] not in (9, 12, 17, 22, 23, 24, 28, 29):
                continue

            logger.debug(f"import-main-loop: starting import of mgm_id={import_state.MgmDetails.Id}")

            try:
                import_management(mgmId=import_state.MgmDetails.Id, debug_level_in=debug_level, version=import_state.ImportVersion,
                    clearManagementData=clear, force=force, limit=int(api_fetch_limit))
            except (FwoApiFailedLockImport, FwLoginFailed):
                logger.info(f"import-main-loop - minor error while importing mgm_id={str(import_state.MgmDetails.Id)}, {str(traceback.format_exc())}") 
                continue # minor errors for a single mgm, go to next one
            except Exception: # all other exceptions are logged here
                logger.error(f"import-main-loop - unspecific error while importing mgm_id={str(import_state.MgmDetails.Id)}, {str(traceback.format_exc())}")
        if clear:
            break



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

    main_loop(
        verify_certificates=args.verify_certificates, 
        suppress_certificate_warnings=args.suppress_certificate_warnings,
        debug_level=int(args.debug),
        clear=args.clear,
        force=args.force
    )
    
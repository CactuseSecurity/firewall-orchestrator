#!/usr/bin/python3
# add main importer loop in pyhton (also able to run distributed)
#   run import loop every x seconds (adjust sleep time per management depending on the change frequency )

import signal
import traceback
import argparse
import sys
import time
import json
import requests, warnings
import fwo_api, common  # from current working dir
from fwo_log import getFwoLogger

# https://stackoverflow.com/questions/18499497/how-to-process-sigterm-signal-gracefully
class GracefulKiller:
    kill_now = False

    def __init__(self):
        signal.signal(signal.SIGINT, self.exit_gracefully)
        signal.signal(signal.SIGTERM, self.exit_gracefully)

    def exit_gracefully(self, *args):
        self.kill_now = True


if __name__ == '__main__':
    parser = argparse.ArgumentParser(
        description='Run import loop across all managements to read configuration from FW managements via API calls')
    parser.add_argument('-d', '--debug', metavar='debug_level', default='0',
                        help='Debug Level: 0=off, 1=send debug to console, 2=send debug to file, 3=keep temporary config files; default=0')
    parser.add_argument('-x', '--proxy', metavar='proxy_string', default='',
                        help='proxy server string to use for FWO API only, e.g. http://1.2.3.4:8080')
    parser.add_argument('-c', '--clear', action='store_true', default=False,
                    help='If set all imports will run once to delete all data instead of importing')
    parser.add_argument('-f', '--force', action='store_true', default=False,
                    help='If set all imports will be run without checking for changes before')


    args = parser.parse_args()
    debug_level = int(args.debug)
    logger = getFwoLogger(debug_level=debug_level)

    logger.info("importer-main-loop starting ...")
    sys.path.append(common.importer_base_dir)
    importer_user_name = 'importer'  # todo: move to config file?
    fwo_config_filename = common.base_dir + '/etc/fworch.json'
    importer_pwd_file = common.base_dir + '/etc/secrets/importer_pwd'

    # if args.suppress_certificate_warnings:
    #     requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

    # setting defaults (only as fallback if config defaults cannot be fetched via API):
    api_fetch_limit = 150
    sleep_timer = 90

    # read fwo config (API URLs)
    try: 
        with open(fwo_config_filename, "r") as fwo_config:
            fwo_config = json.loads(fwo_config.read())
        user_management_api_base_url = fwo_config['middleware_uri']
        fwo_api_base_url = fwo_config['api_uri']
    except:
        logger.error("import-main-loop - error while reading FWO config file")        
        raise

    killer = GracefulKiller()
    while not killer.kill_now:
        # authenticate to get JWT
        skipping = False
        try:
            with open(importer_pwd_file, 'r') as file:
                importer_pwd = file.read().replace('\n', '')
        except:
            logger.error("import-main-loop - error while reading importer pwd file")
            raise

        try:
            jwt = fwo_api.login(importer_user_name, importer_pwd,
                                user_management_api_base_url, ssl_verification=False, proxy=args.proxy)
        except common.FwoApiLoginFailed as e:
            logger.error(e.message)
            skipping = True
        except:
            logger.error("import-main-loop - Unspecified error while logging into FWO API: " + str(traceback.format_exc()))
            skipping = True

        requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only
        verify_certificates = fwo_api.get_config_value(fwo_api_base_url, jwt, key='importCheckCertificates', ssl_verification=False)=='True'
        suppress_certificate_warnings = fwo_api.get_config_value(fwo_api_base_url, jwt, key='importSuppressCertificateWarnings', ssl_verification=verify_certificates)=='True'
        if not suppress_certificate_warnings:
            warnings.resetwarnings()
        import_proxy_string = fwo_api.get_config_value(fwo_api_base_url, jwt, key='importFwProxy', ssl_verification=verify_certificates)
        
        if not skipping:
            try:
                mgm_ids = fwo_api.get_mgm_ids(fwo_api_base_url, jwt, {}, ssl_verification=verify_certificates)
            except:
                logger.error("import-main-loop - error while getting FW management ids: " + str(traceback.format_exc()))
                skipping = True

            try:
                api_fetch_limit = fwo_api.get_config_value(fwo_api_base_url, jwt, key='fwApiElementsPerFetch', ssl_verification=verify_certificates)
                sleep_timer = fwo_api.get_config_value(fwo_api_base_url, jwt, key='importSleepTime', ssl_verification=verify_certificates)
                if api_fetch_limit == None:
                    api_fetch_limit = 150
                if sleep_timer == None:
                    sleep_timer = 90
            except:
                logger.debug("import-main-loop - could not get config values from FWO API - using default values")

            if not skipping:
                for mgm_id in mgm_ids:
                    if killer.kill_now:
                        break
                    if 'id' not in mgm_id:
                        logger.error("import-main-loop - did not get mgm_id: " + str(traceback.format_exc()))
                    else:
                        id = str(mgm_id['id'])
                        # getting a new JWT in case the old one is not valid anymore after a long previous import
                        try:
                            jwt = fwo_api.login(importer_user_name, importer_pwd,
                                                user_management_api_base_url, ssl_verification=verify_certificates, proxy=import_proxy_string)
                        except common.FwoApiLoginFailed as e:
                            logger.error(e.message)
                            skipping = True
                        except:
                            logger.error("import-main-loop - unspecified error during FWO API login - skipping: " + str(traceback.format_exc()))
                            skipping = True
                        if not skipping:
                            try:
                                mgm_details = fwo_api.get_mgm_details(fwo_api_base_url, jwt, {"mgmId": id}, debug_level, ssl_verification=verify_certificates)
                            except:
                                logger.error("import-main-loop - error while getting FW management details for mgm_id=" + str(id) + " - skipping: " + str(traceback.format_exc()))
                                skipping = True
                            if not skipping and mgm_details["deviceType"]["id"] in (9, 11):  # only handle CPR8x and fortiManager
                                logger.debug("import-main-loop: starting import of mgm_id=" + id)
                                try:
                                    import_result = common.import_management(mgm_id=id, debug_level=debug_level, 
                                        clearManagementData=args.clear, force=args.force, limit=str(api_fetch_limit))
                                except (common.FwoApiFailedLockImport, common.FwLoginFailed):
                                    pass # minor errors for a single mgm, go to next one
                                except: # all other exceptions are logged here
                                    logger.error("import-main-loop - unspecific error while importing mgm_id=" + str(id) + ", " +  str(traceback.format_exc()))
        if args.clear:
            break # while loop                                    
        if not killer.kill_now:
            logger.info("import-main-loop.py: sleeping between loops for " + str(sleep_timer) + " seconds")
        counter=0
        while counter < int(sleep_timer) and not killer.kill_now:
            time.sleep(1)
            counter += 1

    logger.info("importer-main-loop exited gracefully.")

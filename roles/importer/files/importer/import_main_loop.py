#!/usr/bin/env python3
# main importer loop in python (also able to run distributed)
# run import loop every x seconds (adjust sleep time per management depending on the change frequency )

import argparse
import sys
import time
import traceback
import warnings

import fwo_globals
import urllib3
from common import import_management  # type: ignore[import-not-found]
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_base import register_global_state
from fwo_const import IMPORTER_BASE_DIR
from fwo_exceptions import FwLoginFailedError, FwoApiFailedLockImportError, FwoApiLoginFailedError
from fwo_log import FWOLogger
from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import (
    ConnectionInfo,
    CredentialInfo,
    DeviceInfo,
    DomainInfo,
    ManagementController,
    ManagerInfo,
)
from services.service_provider import ServiceProvider
from states.global_state import GlobalState


def get_fwo_jwt(import_user: str, import_pwd: str, user_management_api: str) -> str | None:
    try:
        return FwoApi.login(import_user, import_pwd, user_management_api)
    except FwoApiLoginFailedError as e:
        FWOLogger.error(e.message)
    except Exception:
        FWOLogger.error(
            "import_main_loop - unspecified error during FWO API login - skipping: " + str(traceback.format_exc())
        )


def wait_with_shutdown_check(sleep_time: int):
    counter = 0
    while counter < sleep_time:
        if fwo_globals.shutdown_requested:
            FWOLogger.info("import_main_loop - shutdown requested. Exiting...")
            raise SystemExit("import_main_loop - shutdown requested")
        time.sleep(1)
        counter += 1


def import_single_management(
    mgm_id: int,
    fwo_api_call: FwoApiCall,
    verify_certificates: bool,
    api_fetch_limit: int,
    clear: bool,
    suppress_certificate_warnings: bool,
    force: bool,
    fwo_major_version: int,
    sleep_timer: int,
    is_full_import: bool,
    suppress_consistency_check: bool = False,
):
    wait_with_shutdown_check(0)
    import_state = ImportStateController.initialize_import(
        mgm_id,
        fwo_api_call,
        suppress_certificate_warnings,
        verify_certificates,
        force,
        fwo_major_version,
        clear,
        is_full_import,
    )

    register_global_state(import_state)

    try:
        mgm_controller = ManagementController(
            mgm_id, "", [], DeviceInfo(), ConnectionInfo(), "", CredentialInfo(), ManagerInfo(), DomainInfo()
        )
        mgm_details = mgm_controller.get_mgm_details(fwo_api_call.api, mgm_id)
    except Exception:
        FWOLogger.error(
            "import_main_loop - error while getting FW management details for mgm_id="
            + str(mgm_id)
            + " - skipping: "
            + str(traceback.format_exc())
        )
        wait_with_shutdown_check(sleep_timer)
        return

    # only handle CPR8x Manager, fortiManager, Cisco MgmCenter, Palo Panorama, Palo FW, FortiOS REST, Cisco Asa, Asa on FirePower
    if mgm_details["deviceType"]["id"] not in (9, 12, 17, 22, 23, 24, 28, 29):
        return

    FWOLogger.debug(f"import_main_loop: starting import of mgm_id={mgm_id}")

    try:
        import_management(
            mgm_id,
            fwo_api_call,
            verify_certificates,
            api_fetch_limit,
            clear,
            suppress_certificate_warnings,
            suppress_consistency_check=suppress_consistency_check,
        )
    except (FwoApiFailedLockImportError, FwLoginFailedError):
        FWOLogger.info(f"import_main_loop - minor error while importing mgm_id={mgm_id}, {traceback.format_exc()!s}")
        return  # minor errors for a single mgm, go to next one
    except Exception:  # all other exceptions are logged here
        FWOLogger.error(
            f"import_main_loop - unspecific error while importing mgm_id={mgm_id}, {traceback.format_exc()!s}"
        )


def main_loop(
    global_state: GlobalState,
    clear: bool,
    force: bool,
    is_full_import: bool,
):
    wait_with_shutdown_check(0)

    fwo_config = global_state.fwo_config
    jwt = get_fwo_jwt(fwo_config.importer_user_name, fwo_config.importer_password, fwo_config.fwo_user_mgmt_api_uri)
    # check if login was successful - if not, wait and retry
    if jwt is None:
        wait_with_shutdown_check(fwo_config.sleep_timer)
        return

    fwo_api = FwoApi(fwo_config.fwo_api_url, jwt)
    fwo_api_call = FwoApiCall(fwo_api)

    urllib3.disable_warnings()  # suppress ssl warnings only
    verify_certificates = fwo_api_call.get_config_value(key="importCheckCertificates") == "True"
    suppress_certificate_warnings = fwo_api_call.get_config_value(key="importSuppressCertificateWarnings") == "True"
    if not suppress_certificate_warnings:
        warnings.resetwarnings()

    try:
        mgm_ids = fwo_api_call.get_mgm_ids()
    except Exception:
        FWOLogger.error(f"import_main_loop - error while getting FW management ids: {traceback.format_exc()!s}")
        wait_with_shutdown_check(fwo_config.sleep_timer)
        return

    api_fetch_limit = int(fwo_api_call.get_config_value(key="fwApiElementsPerFetch") or fwo_config.api_fetch_size)
    sleep_timer = int(fwo_api_call.get_config_value(key="importSleepTime") or fwo_config.sleep_timer)

    ## loop through all managements
    for mgm_id in mgm_ids:
        import_single_management(
            mgm_id,
            fwo_api_call,
            verify_certificates,
            api_fetch_limit,
            clear,
            suppress_certificate_warnings,
            force,
            fwo_config.major_version,
            fwo_config.sleep_timer,
            is_full_import,
        )

        ServiceProvider().dispose_global_state()

    FWOLogger.info(f"import_main_loop: sleeping for {sleep_timer} seconds until next import cycle")
    wait_with_shutdown_check(sleep_timer)


def main(
    debug_level: int,
    verify_certificates: bool | None = None,
    suppress_certificate_warnings: bool | None = None,
    clear: bool = False,
    force: bool = False,
    is_full_import: bool = False,
):
    FWOLogger(debug_level)

    fwo_globals.set_global_values(verify_certificates, suppress_certificate_warnings)
    if suppress_certificate_warnings:
        urllib3.disable_warnings()

    FWOLogger.info("importer_main_loop starting ...")
    if IMPORTER_BASE_DIR not in sys.path:
        sys.path.append(IMPORTER_BASE_DIR)

    global_state = GlobalState()

    while True:
        main_loop(
            global_state,
            importer_user_name,
            clear,
            force,
            is_full_import,
        )
        if clear:
            break


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Run import loop across all managements to read configuration from FW managements via API calls"
    )
    parser.add_argument(
        "-d",
        "--debug",
        metavar="debug_level",
        default="0",
        help="Debug Level: 0=off, 1=send debug to console, 2=send debug to file, 3=keep temporary config files; default=0",
    )
    parser.add_argument("-v", "--verify_certificates", action="store_true", default=None, help="verify certificates")
    parser.add_argument(
        "-s", "--suppress_certificate_warnings", action="store_true", default=None, help="suppress certificate warnings"
    )
    parser.add_argument(
        "-c",
        "--clear",
        action="store_true",
        default=False,
        help="If set all imports will run once to delete all data instead of importing",
    )
    parser.add_argument(
        "-f",
        "--force",
        action="store_true",
        default=False,
        help="If set all imports will be run without checking for changes before",
    )

    args = parser.parse_args()

    main(
        debug_level=int(args.debug),
        verify_certificates=args.verify_certificates,
        suppress_certificate_warnings=args.suppress_certificate_warnings,
        clear=args.clear,
        force=args.force,
    )

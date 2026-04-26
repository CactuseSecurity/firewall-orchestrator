#!/usr/bin/env python3
# main importer loop in python (also able to run distributed)
# run import loop every x seconds (adjust sleep time per management depending on the change frequency )

import argparse
import sys
import time
import traceback

import fwo_globals
import urllib3
from common import import_management  # type: ignore[import-not-found]
from fwo_const import FWO_CONFIG_FILENAME, IMPORTER_BASE_DIR
from fwo_exceptions import FwLoginFailedError, FwoApiFailedLockImportError
from fwo_log import FWOLogger
from model_controllers.management_controller import (
    ManagementController,
)
from states.global_state import GlobalState
from states.import_state import ImportState


def wait_with_shutdown_check(sleep_time: int):
    counter = 0
    while counter < sleep_time:
        if fwo_globals.shutdown_requested:
            FWOLogger.info("import_main_loop - shutdown requested. Exiting...")
            raise SystemExit("import_main_loop - shutdown requested")
        time.sleep(1)
        counter += 1


def import_single_management(
    global_state: GlobalState,
    import_state: ImportState,
    mgm_id: int,
):
    wait_with_shutdown_check(0)

    try:
        mgm_details = ManagementController.get_mgm_details(global_state.fwo_api, mgm_id)
    except Exception:
        FWOLogger.error(
            "import_main_loop - error while getting FW management details for mgm_id="
            + str(mgm_id)
            + " - skipping: "
            + str(traceback.format_exc())
        )
        wait_with_shutdown_check(global_state.fwo_config_controller.fwo_config.sleep_timer)
        return

    # only handle CPR8x Manager, fortiManager, Cisco MgmCenter, Palo Panorama, Palo FW, FortiOS REST, Cisco Asa, Asa on FirePower
    if mgm_details["deviceType"]["id"] not in (9, 12, 17, 22, 23, 24, 28, 29):
        return

    if "id" not in mgm_details:
        FWOLogger.error(
            f"import_main_loop - mgm_id={mgm_id} has no id in details, skipping import: {traceback.format_exc()!s}"
        )
        return

    FWOLogger.debug(f"import_main_loop: starting import of mgm_id={mgm_details['id']}")

    try:
        import_management(
            global_state=global_state,
            import_state=import_state,
        )
    except (FwoApiFailedLockImportError, FwLoginFailedError):
        FWOLogger.info(
            f"import_main_loop - minor error while importing mgm_id={mgm_details['id']}, {traceback.format_exc()!s}"
        )
        return  # minor errors for a single mgm, go to next one
    except Exception:  # all other exceptions are logged here
        FWOLogger.error(
            f"import_main_loop - unspecific error while importing mgm_id={mgm_details['id']}, {traceback.format_exc()!s}"
        )


def main_loop(
    global_state: GlobalState,
):
    wait_with_shutdown_check(0)

    fwo_config = global_state.fwo_config_controller.fwo_config
    # check if login was successful - if not, wait and retry

    try:
        global_state.login_to_api()
    except Exception:
        FWOLogger.error(f"import_main_loop - error while logging in to API: {traceback.format_exc()!s}")
        wait_with_shutdown_check(fwo_config.sleep_timer)
        return

    urllib3.disable_warnings()  # suppress ssl warnings only

    try:
        mgm_ids = global_state.fwo_api_call.get_mgm_ids()
    except Exception:
        FWOLogger.error(f"import_main_loop - error while getting FW management ids: {traceback.format_exc()!s}")
        wait_with_shutdown_check(fwo_config.sleep_timer)
        return

    api_fetch_limit = int(
        global_state.fwo_api_call.get_config_value(key="fwApiElementsPerFetch") or fwo_config.api_fetch_size
    )
    sleep_timer = int(global_state.fwo_api_call.get_config_value(key="importSleepTime") or fwo_config.sleep_timer)
    global_state.fwo_config_controller.update_settings(sleep_timer=sleep_timer, api_fetch_size=api_fetch_limit)

    ## loop through all managements
    for mgm_id in mgm_ids:
        import_state = ImportState(fwo_api=global_state.fwo_api, fwo_api_call=global_state.fwo_api_call, mgm_id=mgm_id)
        import_single_management(global_state, import_state, mgm_id)

    FWOLogger.info(f"import_main_loop: sleeping for {sleep_timer} seconds until next import cycle")
    wait_with_shutdown_check(sleep_timer)


def main(
    debug_level: int,
    verify_certificates: bool | None = None,
    suppress_certificate_warnings: bool | None = None,
    clear: bool = False,
    force: bool = False,
):
    FWOLogger(debug_level)

    fwo_globals.set_global_values(verify_certificates, suppress_certificate_warnings)
    if suppress_certificate_warnings:
        urllib3.disable_warnings()  # type: ignore[suppress ssl warnings only]

    FWOLogger.info("importer_main_loop starting ...")
    if IMPORTER_BASE_DIR not in sys.path:
        sys.path.append(IMPORTER_BASE_DIR)

    global_state = GlobalState(
        config_filename=FWO_CONFIG_FILENAME,
        force=force,
        clear=clear,
        debug_level=debug_level,
    )

    while True:
        main_loop(
            global_state=global_state,
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
    parser.add_argument(
        "-v",
        "--verify_certificates",
        action="store_true",
        default=None,
        help="verify certificates",
    )
    parser.add_argument(
        "-s",
        "--suppress_certificate_warnings",
        action="store_true",
        default=None,
        help="suppress certificate warnings",
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

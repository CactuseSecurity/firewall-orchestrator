#!/usr/bin/env python3
from __future__ import annotations

# main importer loop in python (also able to run distributed)
# run import loop every x seconds (adjust sleep time per management depending on the change frequency )
import argparse
import json
import sys
import time
import traceback
import warnings

import fwo_globals
import urllib3
from common import import_management  # type: ignore[import-not-found]
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_base import init_service_provider, register_global_state
from fwo_const import BASE_DIR, IMPORTER_BASE_DIR, IMPORTER_DRAINED_EXIT_CODE
from fwo_exceptions import (
    FwLoginFailedError,
    FwoApiFailedLockImportError,
    FwoApiLoginFailedError,
    FwoApiServiceUnavailableError,
    ShutdownRequestedError,
)
from fwo_log import FWOLogger
from fwo_signalling import register_signalling_handlers
from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import (
    ConnectionInfo,
    CredentialInfo,
    DeviceInfo,
    DomainInfo,
    ManagementController,
    ManagerInfo,
)
from orphaned_imports import try_roll_back_orphaned_imports
from services.service_provider import ServiceProvider


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
    # only called while no import holds a lock, which is what makes exiting on a drain request safe here
    def exit_if_shutdown_or_drain_requested():
        if fwo_globals.shutdown_requested:
            FWOLogger.info("import_main_loop - shutdown requested. Exiting...")
            raise SystemExit("import_main_loop - shutdown requested")
        if fwo_globals.drain_requested:
            FWOLogger.info("import_main_loop - drain requested and no import running. Exiting...")
            raise SystemExit(IMPORTER_DRAINED_EXIT_CODE)

    exit_if_shutdown_or_drain_requested()
    counter = 0
    while counter < sleep_time:
        time.sleep(1)
        exit_if_shutdown_or_drain_requested()
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
    suppress_consistency_check: bool = False,
):
    wait_with_shutdown_check(0)
    import_state = ImportStateController.initialize_import(
        mgm_id=mgm_id,
        api_call=fwo_api_call,
        suppress_cert_warnings=suppress_certificate_warnings,
        ssl_verification=verify_certificates,
        force=force,
        version=fwo_major_version,
        is_clearing_import=clear,
    )

    register_global_state(import_state)

    try:
        mgm_controller = ManagementController(
            mgm_id,
            "",
            [],
            DeviceInfo(),
            ConnectionInfo(),
            "",
            CredentialInfo(),
            ManagerInfo(),
            DomainInfo(),
        )
        mgm_details = mgm_controller.get_mgm_details(fwo_api_call.api, mgm_id)
    except FwoApiServiceUnavailableError:
        raise  # the api is gone for every management, not just this one - main_loop ends the cycle
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
            api_fetch_limit,
            clear,
            suppress_consistency_check=suppress_consistency_check,
        )
    except (KeyboardInterrupt, ShutdownRequestedError) as e:
        raise SystemExit("import_main_loop - shutdown requested") from e
    except (FwoApiFailedLockImportError, FwLoginFailedError):
        FWOLogger.info(f"import_main_loop - minor error while importing mgm_id={mgm_id}, {traceback.format_exc()!s}")
        return  # minor errors for a single mgm, go to next one
    except FwoApiServiceUnavailableError:
        FWOLogger.warning(
            f"import_main_loop - FWO API or middleware unavailable while importing mgm_id={mgm_id} "
            "- ending this import cycle"
        )
        raise
    except Exception:  # all other exceptions are logged here
        FWOLogger.error(
            f"import_main_loop - unspecific error while importing mgm_id={mgm_id}, {traceback.format_exc()!s}"
        )


def _imported_last(mgm_ids: list[int], last_mgm_ids: list[int]) -> list[int]:
    """
    Returns mgm_ids with the managements in last_mgm_ids moved to the end, keeping the order otherwise.

    main_loop passes the managements whose import the orphan cleanup just rolled back: an import that
    keeps getting the importer killed (e.g. by MemoryMax / OOMPolicy=kill) would otherwise be retried
    first after every restart and starve every management after it.
    """
    last = set(last_mgm_ids)
    return [mgm_id for mgm_id in mgm_ids if mgm_id not in last] + [mgm_id for mgm_id in mgm_ids if mgm_id in last]


def main_loop(
    importer_pwd_file: str,
    importer_user_name: str,
    user_management_api_base_url: str,
    fwo_api_base_url: str,
    fwo_major_version: int,
    api_fetch_limit: int,
    sleep_timer: int,
    clear: bool,
    force: bool,
    orphan_cleanup_pending: bool = False,
) -> bool:
    """
    Runs one import cycle over all managements.

    orphan_cleanup_pending: while True, unfinished imports of this host's managements are rolled
    back first, and the managements concerned are imported last in this cycle. Returns False once
    that succeeded, otherwise True, so the caller can hand it to the next cycle. An outage of the
    api during the cycle sets it again: the rollback of the interrupted import may have failed.
    """
    wait_with_shutdown_check(0)

    try:
        with open(importer_pwd_file) as f:
            importer_pwd = f.read().replace("\n", "")
    except Exception:
        FWOLogger.error("import_main_loop - error while reading importer pwd file")
        raise

    json_raw = get_fwo_jwt(importer_user_name, importer_pwd, user_management_api_base_url)

    # check if json could be retrieved - if not, wait and retry
    if json_raw is None:
        FWOLogger.error("cannot proceed without successful login - exiting")
        wait_with_shutdown_check(sleep_timer)
        return orphan_cleanup_pending

    jwt: str
    refresh_token: str | None

    try:
        json_data = json.loads(json_raw)
        jwt = json_data["AccessToken"]
        refresh_token = json_data.get("RefreshToken") or None
    except Exception:
        FWOLogger.error("JWT could not be parsed")
        wait_with_shutdown_check(sleep_timer)
        return orphan_cleanup_pending

    fwo_api = FwoApi(fwo_api_base_url, jwt, refresh_token, user_management_api_base_url)
    fwo_api_call = FwoApiCall(fwo_api)
    rolled_back_mgm_ids: list[int] = []
    if orphan_cleanup_pending:
        cleanup_result = try_roll_back_orphaned_imports(fwo_api_call)
        orphan_cleanup_pending = not cleanup_result.complete
        rolled_back_mgm_ids = cleanup_result.rolled_back_mgm_ids

    urllib3.disable_warnings()  # type: ignore[suppress ssl warnings only]
    # the command line can only force certificate checking on, never switch the configured value off
    verify_certificates = (
        fwo_globals.cli_verify_certs or fwo_api_call.get_config_value(key="importCheckCertificates") == "True"
    )
    suppress_certificate_warnings = fwo_api_call.get_config_value(key="importSuppressCertificateWarnings") == "True"
    if not suppress_certificate_warnings:
        warnings.resetwarnings()

    try:
        mgm_ids = _imported_last(fwo_api_call.get_mgm_ids(), rolled_back_mgm_ids)
    except Exception:
        FWOLogger.error(f"import_main_loop - error while getting FW management ids: {traceback.format_exc()!s}")
        wait_with_shutdown_check(sleep_timer)
        return orphan_cleanup_pending

    api_fetch_limit = int(fwo_api_call.get_config_value(key="fwApiElementsPerFetch") or api_fetch_limit)
    sleep_timer = int(fwo_api_call.get_config_value(key="importSleepTime") or sleep_timer)

    ## loop through all managements
    try:
        for mgm_id in mgm_ids:
            init_service_provider()
            try:
                import_single_management(
                    mgm_id,
                    fwo_api_call,
                    verify_certificates,
                    api_fetch_limit,
                    clear,
                    suppress_certificate_warnings,
                    force,
                    fwo_major_version,
                    sleep_timer,
                )
            finally:
                ServiceProvider().reset()
    except FwoApiServiceUnavailableError:
        # every management would fail the same way, and each failure costs a rollback - wait for the
        # next cycle instead of working through the list against a missing api
        FWOLogger.warning(
            "import_main_loop - FWO API or middleware stayed unavailable "
            "- the remaining managements are imported in the next cycle"
        )
        # the rollback of the interrupted import may itself have failed while the api was down, leaving
        # its lock behind - the orphan cleanup removes it after the next successful login
        orphan_cleanup_pending = True

    FWOLogger.info(f"import_main_loop: sleeping for {sleep_timer} seconds until next import cycle")
    wait_with_shutdown_check(sleep_timer)
    return orphan_cleanup_pending


def main(
    debug_level: int,
    verify_certificates: bool | None = None,
    suppress_certificate_warnings: bool | None = None,
    clear: bool = False,
    force: bool = False,
):
    FWOLogger(debug_level)
    # before the first cycle: an unhandled SIGUSR1 would kill the process instead of draining it
    register_signalling_handlers()
    # the handlers raise wherever the process is, so the start up is covered as well as the loop
    try:
        service_provider = init_service_provider()
        fwo_config = service_provider.get_fwo_config()
        fwo_api_base_url = fwo_config["fwo_api_base_url"]
        fwo_major_version = fwo_config["fwo_major_version"]
        user_management_api_base_url = fwo_config["user_management_api_base_url"]
        fwo_globals.set_global_values(verify_certificates, suppress_certificate_warnings)
        fwo_globals.set_cli_verify_certs(verify_certificates)
        if suppress_certificate_warnings:
            urllib3.disable_warnings()  # type: ignore[suppress ssl warnings only]

        FWOLogger.info("importer_main_loop starting ...")
        if IMPORTER_BASE_DIR not in sys.path:
            sys.path.append(IMPORTER_BASE_DIR)
        importer_user_name = "importer"  # move to config file?
        importer_pwd_file = BASE_DIR + "/etc/secrets/importer_pwd"

        # setting defaults (only as fallback if config defaults cannot be fetched via API):
        api_fetch_limit: int = 150
        sleep_timer: int = 90

        # imports of this host still open when the process starts were left behind by a previous process
        orphan_cleanup_pending = True
        while True:
            orphan_cleanup_pending = main_loop(
                importer_pwd_file,
                importer_user_name,
                user_management_api_base_url,
                fwo_api_base_url,
                fwo_major_version,
                api_fetch_limit,
                sleep_timer,
                clear,
                force,
                orphan_cleanup_pending=orphan_cleanup_pending,
            )
            if clear:
                break
    except ShutdownRequestedError:
        # a SIGINT/SIGTERM while starting up or between imports
        raise SystemExit("import_main_loop - shutdown requested") from None


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

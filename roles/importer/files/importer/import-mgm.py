#!/usr/bin/env python3

import argparse
import sys
import traceback
import warnings

import urllib3
from common import IMPORTER_BASE_DIR, import_management
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_base import init_service_provider, register_global_state
from fwo_const import BASE_DIR, IMPORTER_BASE_DIR
from fwo_exceptions import FwoApiLoginFailed
from fwo_log import FWOLogger
from model_controllers.import_state_controller import ImportStateController

if IMPORTER_BASE_DIR not in sys.path:
    sys.path.append(IMPORTER_BASE_DIR)


def get_fwo_jwt(import_user: str, import_pwd: str, user_management_api: str) -> str | None:
    try:
        return FwoApi.login(import_user, import_pwd, user_management_api)
    except FwoApiLoginFailed as e:
        FWOLogger.error(e.message)
    except Exception:
        FWOLogger.error(
            "import-main-loop - unspecified error during FWO API login - skipping: " + str(traceback.format_exc())
        )


def main(
    mgm_id: int,
    file: str | None = None,
    debug_level: int = 0,
    verify_certificates_default: bool = False,
    force: bool = False,
    limit: int = 150,
    clear_management_data: bool = False,
    suppress_certificate_warnings: bool = False,
):
    FWOLogger(debug_level)

    service_provider = init_service_provider()
    fwo_config = service_provider.get_fwo_config()
    verify_certificates = verify_certificates_default
    fwo_api_base_url = fwo_config["fwo_api_base_url"]
    fwo_major_version = fwo_config["fwo_major_version"]
    user_management_api_base_url = fwo_config["user_management_api_base_url"]
    if suppress_certificate_warnings:
        urllib3.disable_warnings()

    FWOLogger.info("import-mgm starting ...")
    if IMPORTER_BASE_DIR not in sys.path:
        sys.path.append(IMPORTER_BASE_DIR)

    importer_user_name = "importer"  # TODO: move to config file?
    importer_pwd_file = BASE_DIR + "/etc/secrets/importer_pwd"

    try:
        importer_pwd = open(importer_pwd_file).read().replace("\n", "")
    except Exception:
        FWOLogger.error("error while reading importer pwd file")
        raise

    jwt = get_fwo_jwt(importer_user_name, importer_pwd, user_management_api_base_url)
    # check if login was successful - if not, wait and retry
    if jwt is None:
        FWOLogger.error("cannot proceed without successful login - exiting")
        return

    fwo_api = FwoApi(fwo_api_base_url, jwt)
    fwo_api_call = FwoApiCall(fwo_api)

    urllib3.disable_warnings()  # suppress ssl warnings only
    verify_certificates = fwo_api_call.get_config_value(key="importCheckCertificates") == "True"
    suppress_certificate_warnings = fwo_api_call.get_config_value(key="importSuppressCertificateWarnings") == "True"
    if not suppress_certificate_warnings:
        warnings.resetwarnings()

    import_state = ImportStateController.initialize_import(
        mgm_id,
        jwt,
        suppress_certificate_warnings,
        verify_certificates,
        force,
        fwo_major_version,
        clear_management_data,
        is_full_import=True,
    )
    register_global_state(import_state)

    import_management(
        mgm_id, fwo_api_call, verify_certificates, limit, clear_management_data, suppress_certificate_warnings, file
    )


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Read configuration from FW management via API calls")
    parser.add_argument(
        "-m", "--mgmId", metavar="management_id", required=True, help="FWORCH DB ID of the management server to import"
    )
    parser.add_argument(
        "-c",
        "--clear",
        action="store_true",
        default=False,
        help="If set the import will delete all data for the given management instead of importing",
    )
    parser.add_argument(
        "-f",
        "--force",
        action="store_true",
        default=False,
        help="If set the import will be attempted without checking for changes or if the importer module is the one defined",
    )
    parser.add_argument(
        "-d",
        "--debug",
        metavar="debug_level",
        default="0",
        help="Debug Level:  \
                                    0=off, \
                                    1=send debug to console, \
                                    2=send debug to file, \
                                    3=save noramlized config file, \
                                    4=additionally save native config file, \
                                    8=send native config (as read from firewall) to standard out, \
                                    9=send normalized config to standard out, \
                                    (default=0), \
                                    config files are saved to $FWORCH/tmp/import dir",
    )
    parser.add_argument("-v", "--verify_certificates", action="store_true", default=None, help="verify certificates")
    parser.add_argument(
        "-s", "--suppress_certificate_warnings", action="store_true", default=None, help="suppress certificate warnings"
    )
    parser.add_argument(
        "-l",
        "--limit",
        metavar="api_limit",
        default="150",
        help="The maximal number of returned results per HTTPS Connection; default=150",
    )
    parser.add_argument(
        "-i",
        "--in_file",
        metavar="config_file_input",
        help="if set, the config will not be fetched from firewall but read from json config (native or normalized) file specified here; may also be an url.",
    )

    args = parser.parse_args()

    try:
        main(
            int(args.mgmId),  # TYPING: this should be snake case
            args.in_file,
            int(args.debug),
            args.verify_certificates,
            args.force,
            int(args.limit),
            args.clear,
            args.suppress_certificate_warnings,
        )
    except Exception:
        FWOLogger.error(
            "import-mgm - error while importing mgmId=" + str(args.mgmId) + ": " + str(traceback.format_exc())
        )

    sys.exit()

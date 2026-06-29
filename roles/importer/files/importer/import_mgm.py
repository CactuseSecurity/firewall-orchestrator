#!/usr/local/fworch/importer/importer-venv/bin/python3
import argparse
import sys
import traceback

import fwo_globals
import urllib3
from common import import_management  # type: ignore[import-not-found]
from fwo_const import FWO_CONFIG_FILENAME, IMPORTER_BASE_DIR
from fwo_log import FWOLogger
from states.global_state import GlobalState
from states.import_state import ImportState

if IMPORTER_BASE_DIR not in sys.path:
    sys.path.append(IMPORTER_BASE_DIR)


def main(
    mgm_id: int,
    file: str | None = None,
    debug_level: int = 0,
    verify_certificates_default: bool = False,
    force: bool = False,
    limit: int = 150,
    clear_management_data: bool = False,
    suppress_certificate_warnings: bool = False,
    suppress_consistency_check: bool = False,
):
    FWOLogger(debug_level)
    FWOLogger.debug("debug level set to " + str(debug_level))

    verify_certificates = verify_certificates_default or False
    if suppress_certificate_warnings:
        urllib3.disable_warnings()

    fwo_globals.set_global_values(verify_certificates, suppress_certificate_warnings)
    FWOLogger.info("import-mgm starting ...")
    if IMPORTER_BASE_DIR not in sys.path:
        sys.path.append(IMPORTER_BASE_DIR)

    global_state = GlobalState(
        config_filename=FWO_CONFIG_FILENAME,
        force=force,
        clear=clear_management_data,
        debug_level=debug_level,
    )

    urllib3.disable_warnings()  # suppress ssl warnings only

    import_state = ImportState(
        fwo_api=global_state.fwo_api, fwo_api_call=global_state.fwo_api_call, mgm_id=mgm_id, input_file=file
    )
    global_state.fwo_config_controller.update_settings(
        suppress_consistency_check=suppress_consistency_check,
        api_fetch_size=limit,
    )

    import_management(
        global_state,
        import_state,
    )


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Read configuration from FW management via API calls")
    parser.add_argument(
        "-m",
        "--mgmId",
        metavar="management_id",
        required=True,
        help="FWORCH DB ID of the management server to import",
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
    parser.add_argument(
        "--suppress_consistency_check",
        action="store_true",
        default=False,
        help="If set, skip FwConfigImportCheckConsistency before importing",
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
            args.suppress_consistency_check,
        )
    except Exception:
        FWOLogger.error(
            "import-mgm - error while importing mgmId=" + str(args.mgmId) + ": " + str(traceback.format_exc())
        )

    sys.exit()

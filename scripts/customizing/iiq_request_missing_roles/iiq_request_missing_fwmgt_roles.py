#!/usr/bin/python3

import argparse
import json
import re
import socket
import sys
import urllib.parse
from datetime import datetime
from pathlib import Path
from typing import Any

import git  # apt install python3-git # or: pip install git
import urllib3

from scripts.customizing.fwo_custom_lib.app_data_basics import transform_app_list_to_dict
from scripts.customizing.fwo_custom_lib.app_data_models import Appip, Owner
from scripts.customizing.fwo_custom_lib.basic_helpers import (
    FWOLogger,
    get_logger,
    read_custom_config,
    read_custom_config_with_default,
)
from scripts.customizing.fwo_custom_lib.read_app_data_csv import extract_app_data_from_csv, extract_ip_data_from_csv
from scripts.customizing.iiq_request_missing_roles.iiq_client import IIQClient

__version__ = "2025-11-20-01"
# "2025-03-24-01" adding support for getting already modelled functions
# "2025-05-20-01" renaming from ldap-import.py to request-missing-fwmgt-roles.py
# "2025-06-23-01" adding A_Tufin_Request TF for all users
# "2025-07-31-01" changing naming scheme of TF to match concept
# "2025-08-01-01" correcting function names and leaving out TF A_Tufin_Request for now
#                 as it does cause errors (not all capitals)
# "2025-08-12-01" adding A_TUFIN_REQUEST (capital letters)
# "2025-10-10-01" fixing a) missing git clone b) iiq_request_body copy issue
# "2025-10-28-01" fixing wrong match string resulting in unneccessary attempt to create already existing roles, leading to false positive errors in statistics
# "2025-11-20-01" rework

fwo_base_dir: str = "/usr/local/fworch/"
fwo_tmp_dir: str = fwo_base_dir + "tmp/iiq_request_missing_fwmgt_roles/"
log_dir: str = "/var/log/fworch/iiq_request_missing_fwmgt_roles"
base_dir_etc: str = fwo_base_dir + "etc/"
cmdb_repo_target_dir: str = fwo_tmp_dir + "cmdb-repo"
default_config_file_name: str = base_dir_etc + "customizingConfig.json"
IPV4_DOT_COUNT: int = 3


def is_valid_ipv4_address(address: str) -> bool:
    try:
        socket.inet_pton(socket.AF_INET, address)
    except AttributeError:  # no inet_pton here, sorry
        try:
            socket.inet_aton(address)
        except OSError:
            return False
        return address.count(".") == IPV4_DOT_COUNT
    except OSError:  # not a valid address
        return False

    return True


def get_owners_from_csv_files(
    csv_owner_file_pattern: str,
    csv_app_server_file_pattern: str,
    repo_target_dir: str,
    ldap_path: str,
    logger: FWOLogger,
    debug_level: int,
    owner_header_patterns: dict[str, str] | None = None,
    ip_header_patterns: dict[str, str] | None = None,
) -> tuple[dict[str, Owner], dict[str, str]]:
    app_list: list[Owner] = []
    re_owner_file_pattern: re.Pattern[str] = re.compile(csv_owner_file_pattern)
    for file_path in Path(repo_target_dir).iterdir():
        if re_owner_file_pattern.match(file_path.name):
            extract_app_data_from_csv(
                file_path.name,
                app_list,
                ldap_path,
                "import-source-dummy",
                Owner,
                logger,
                debug_level,
                base_dir=repo_target_dir,
                column_patterns=owner_header_patterns,
            )

    owner_dict: dict[str, Owner] = transform_app_list_to_dict(app_list)

    re_app_server_file_pattern: re.Pattern[str] = re.compile(csv_app_server_file_pattern)
    for file_path in Path(repo_target_dir).iterdir():
        if re_app_server_file_pattern.match(file_path.name):
            logger.info_if(0, "importing IP data from file %s ...", file_path.name)
            extract_ip_data_from_csv(
                file_path.name,
                owner_dict,
                Appip,
                logger,
                debug_level,
                base_dir=repo_target_dir,
                column_patterns=ip_header_patterns,
            )

    # now only choose those owners which have at least one app server with a non-empty IP assigned
    remove_apps_without_ip_addresses(owner_dict)

    tisos: dict[str, str] = get_tisos_from_owner_dict(owner_dict)
    return owner_dict, tisos


def remove_apps_without_ip_addresses(owner_dict: dict[str, Owner]) -> None:
    apps_to_remove: list[str] = []
    app_key: str
    for app_key, owner in owner_dict.items():
        has_ip: bool = False
        app_server: Appip
        for app_server in owner.app_servers:
            if is_valid_ipv4_address(str(app_server.ip_start)):
                has_ip = True
                break
        if not has_ip:
            apps_to_remove.append(app_key)
    for app_key in apps_to_remove:
        logger.info_if(5, "removing app %s as it has no valid IP address assigned", app_key)
        del owner_dict[app_key]


def get_tisos_from_owner_dict(app_dict: dict[str, Owner]) -> dict[str, str]:
    tisos: dict[str, str] = {}
    app_id: str
    for app_id, owner in app_dict.items():
        if owner.main_user != "":
            tiso: str = owner.main_user.replace("CN=", "")  # remove possible CN= prefix
            if "," in tiso:
                tiso = tiso.split(",")[0]  # take only the user name part before any comma
            tisos[f"{app_id}"] = tiso
        else:
            logger.warning("owner %s has no main user, cannot get TISO", owner.name)
    return tisos


def get_git_repo(git_repo_url: str, git_username: str, git_password: str, repo_target_dir: str) -> None:
    encoded_password: str = urllib.parse.quote(git_password, safe="")
    repo_url: str = "https://" + git_username + ":" + encoded_password + "@" + git_repo_url

    if Path(repo_target_dir).exists():
        # If the repository already exists, open it and perform a pull
        repo: git.Repo = git.Repo(repo_target_dir)
        origin: git.Remote = repo.remotes.origin
        # for DEBUG: do not pull
        origin.pull()
    else:
        git.Repo.clone_from(repo_url, repo_target_dir)


def request_all_roles(
    owner_dict: dict[str, Owner],
    tisos: dict[str, str],
    tiso_orgids: dict[str, str],
    iiq_client: IIQClient,
    stats: dict[str, Any],
    first: int,
    run_workflow: bool,
) -> None:
    counter: int = 0
    # create new groups
    logger.info("creating new groups in iiq")
    app_id_with_prefix: str
    for app_id_with_prefix, owner in owner_dict.items():
        logger.info_if(5, "checking app %s", app_id_with_prefix)
        counter += 1
        tiso: str | None = tisos.get(app_id_with_prefix)
        if tiso is None:
            logger.warning("did not find a TISO for owner %s, skipping group creation", app_id_with_prefix)
            continue
        org_id: str | None = tiso_orgids.get(tiso) if tiso else None
        if org_id is None:
            logger.warning("did not find an OrgId for owner %s, skipping group creation", app_id_with_prefix)
            continue

        app_prefix: str
        app_id: str
        app_prefix, app_id = app_id_with_prefix.split("-")
        # get existing (already modelled) functions for this app to find out, what still needs to be changed in iiq
        if iiq_client.app_functions_exist_in_iiq(app_prefix, app_id, stats):
            logger.info_if(5, "not requesting groups for %s - they already exist", app_id_with_prefix)
            continue

        logger.info_if(5, "requesting groups for %s", app_id_with_prefix)
        iiq_client.request_group_creation(
            app_prefix, app_id, org_id, tiso, owner.name, stats, run_workflow=run_workflow
        )

        # if first parameter is set, only handle the first "first" applications, otherwise handle all
        if first > 0 and counter >= first:
            break


def get_tisos_orgids(tisos: dict[str, str], iiq_client: IIQClient, exit_after_dump: bool = False) -> dict[str, str]:
    tiso_orgids: dict[str, str] = {}
    logger.info_if(0, "getting tiso orgids from iiq")
    tiso: str
    for tiso in set(tisos.values()):
        org_id: str | None = iiq_client.get_org_id(tiso)
        if org_id is not None:
            tiso_orgids[tiso] = org_id
    if len(tiso_orgids.keys()) == 0:
        logger.error("could not resolve a single TISO OrgId, quitting ")
        sys.exit(1)
    else:
        logger.debug_if(2, "tiso_orgids=%s", tiso_orgids)

    if exit_after_dump:
        for tiso_user_id, org_id in tiso_orgids.items():
            sys.stdout.write(f"{tiso_user_id},{org_id}\n")
        sys.exit(0)
    return tiso_orgids


def init_statistics() -> dict[str, Any]:
    stats: dict[str, Any] = {}
    stats_fields: list[str] = [
        "apps_with_running_requests",
        "apps_newly_requested",
        "apps_request_simulated",
        "apps_with_invalid_alfabet_id",
        "apps_with_request_errors",
        "apps_with_unexpected_request_errors",
        "existing_technical_functions",
    ]
    field: str
    for field in stats_fields:
        stats.update({field: [], f"{field}_count": 0})
    return stats


def write_stats_to_file(stats: dict[str, Any], log_dir: str) -> None:
    Path(log_dir).mkdir(parents=True, exist_ok=True)
    # Get current date as YYYY-MM-DD
    date_str: str = datetime.now().strftime("%Y-%m-%d")
    log_file: Path = Path(log_dir) / f"{date_str}_iiq_request.log"
    log_file.write_text(json.dumps(stats, indent=2))


if __name__ == "__main__":
    ALLOWED_STAGE_VALUES: set[str] = {"prod", "test"}
    ALLOWED_RUN_VALUES: set[bool] = {True, False}

    logger: FWOLogger = get_logger()

    parser = argparse.ArgumentParser(description="Read configuration from FW management via API calls")
    parser.add_argument(
        "-c",
        "--config",
        default=default_config_file_name,
        help="Filename of custom config file for modelling imports, default file="
        + default_config_file_name
        + ',\
                        sample config file content: \
                        { \
                            "iiqHostname": "stest.api.example.de",} \
                            "iiqUsername": "iiq-user-id", \
                            "iiqPassword": "iiq-user-pwd", \
                            "gitRepo": "github.example.de/cmdb/app-export", \
                            "gitName": "git-user-1", \
                            "gitPassword": "gituser-1-pwd", \
                            "csvOwnerFilePattern": "NeMo_???_meta.csv", \
                            "csvAppServerFilePattern": "NeMo_???_IP_.*?.csv", \
                            "iiqAppName": "AD EXAMPLEDE", \
                            "userPrefix": "USR" \
                        } \
                        ',
    )
    parser.add_argument(
        "-s", "--suppress_certificate_warnings", action="store_true", default=True, help="suppress certificate warnings"
    )
    parser.add_argument(
        "-d",
        "--debug",
        metavar="debug_level",
        default="0",
        help="set to >1 for debugging and to avoid CMDB git pull (due to permission conflicts in debug mode)",
    )
    parser.add_argument(
        "-f",
        "--first",
        metavar="handle_first_x_apps",
        default="0",
        help="set to value greater than 0 to only handle the first x applications",
    )
    parser.add_argument(
        "-g",
        "--stage",
        metavar="stage_of_workflow",
        choices=ALLOWED_STAGE_VALUES,
        required=True,
        help=f"Specify the stage of your system. Allowed values: {', '.join(ALLOWED_STAGE_VALUES)}",
    )
    parser.add_argument(
        "-r",
        "--run_workflow",
        action="store_true",
        help="Should the created IIQ workflow be run? If left out, the workflow is not started (simulation only).",
    )
    parser.add_argument(
        "-j",
        "--just_dump_tiso_org_ids",
        action="store_true",
        help="Just dump the org_ids of all TISOs as CSV and then exit.",
    )
    parser.add_argument(
        "-o", "--import_from_folder", help="if set, will try to read csv files from given folder instead of git repo"
    )

    args: argparse.Namespace = parser.parse_args()

    csv_owner_file_pattern: str = read_custom_config(args.config, "csvOwnerFilePattern", logger)
    csv_app_server_file_pattern: str = read_custom_config(args.config, "csvAppServerFilePattern", logger)
    owner_header_patterns: dict[str, str] = read_custom_config_with_default(
        args.config, "csvOwnerColumnPatterns", {}, logger
    )
    ip_header_patterns: dict[str, str] = read_custom_config_with_default(args.config, "csvIpColumnPatterns", {}, logger)

    debug: int = int(args.debug)
    logger.configure_debug_level(debug)

    if args.stage == "prod":
        stage: str = ""  # the production instance does not need any extra strings
    else:
        stage = args.stage
    first: int = int(args.first)
    if args.suppress_certificate_warnings:
        urllib3.disable_warnings()

    logger.debug_if(3, f"using config file {args.config}")

    ldap_path: str = read_custom_config(args.config, "ldapPath", logger)
    iiq_hostname: str = read_custom_config(args.config, "iiqHostname", logger)
    iiq_user: str = read_custom_config(args.config, "iiqUsername", logger)
    iiq_password: str = read_custom_config(args.config, "iiqPassword", logger)
    cmdb_exports: list[str] = []

    # get/set template parameters
    iiq_app_name: str = read_custom_config(args.config, "iiqAppName", logger)
    user_prefix: str = read_custom_config(args.config, "userPrefix", logger)
    iiq_client: IIQClient = IIQClient(
        iiq_hostname, iiq_user, iiq_password, iiq_app_name, user_prefix, stage=stage, debug=debug, logger=logger
    )

    if args.import_from_folder:
        csv_file_base_dir: str = args.import_from_folder
    else:
        git_repo_url: str = read_custom_config(args.config, "cmdbGitRepoUrl", logger)
        git_username: str = read_custom_config(args.config, "cmdbGitUsername", logger)
        git_password: str = read_custom_config(args.config, "cmdbGitPassword", logger)
        csv_file_base_dir = cmdb_repo_target_dir
        get_git_repo(git_repo_url, git_username, git_password, cmdb_repo_target_dir)

    logger.info_if(0, "getting owners from file")

    owners: dict[str, Owner]
    tisos: dict[str, str]
    owners, tisos = get_owners_from_csv_files(
        csv_owner_file_pattern,
        csv_app_server_file_pattern,
        csv_file_base_dir,
        ldap_path,
        logger,
        debug,
        owner_header_patterns=owner_header_patterns,
        ip_header_patterns=ip_header_patterns,
    )

    tiso_orgids: dict[str, str] = get_tisos_orgids(tisos, iiq_client, exit_after_dump=args.just_dump_tiso_org_ids)

    stats: dict[str, Any] = init_statistics()

    request_all_roles(owners, tisos, tiso_orgids, iiq_client, stats, first, args.run_workflow)

    if debug > 0:
        logger.debug("Stats: %s", json.dumps(stats, indent=3))

    write_stats_to_file(stats, log_dir)

    sys.exit(0)

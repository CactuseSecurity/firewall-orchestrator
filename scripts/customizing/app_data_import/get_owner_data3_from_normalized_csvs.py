#!/usr/bin/python3
# revision history:
__version__ = "2026-01-08-01"

# breaking change: /usr/local/fworch needs to be in the python path
# just add "export PYTHONPATH="$PYTHONPATH:/usr/local/fworch/"" to /etc/environment
# 2025-11-25-01, initial version
# 2025-12-02-01, adding new fields to the interface:
#   - daysUntilFirstRecertification: int|None (if not set, we assume the same intervals as for normal recertification),
#   - recertificationActive: bool

# reads the main app data from multiple csv files contained in a git repo
# users will reside in external ldap groups with standardized names
# only the main responsible person per app is taken from the csv files
# here app servers will only have ip addresses (no names)

# dependencies:
#   a) package python3-git must be installed
#   b) requires the config items listed in the aprser help to be present in config file /usr/local/orch/etc/secrets/customizingConfig.json

import argparse
import logging
import re
from pathlib import Path

import urllib3

from scripts.customizing.fwo_custom_lib.app_data_basics import transform_app_list_to_dict, write_owners_to_json
from scripts.customizing.fwo_custom_lib.app_data_models import Appip, Owner
from scripts.customizing.fwo_custom_lib.basic_helpers import (
    get_logger,
    read_custom_config,
    read_custom_config_with_default,
)
from scripts.customizing.fwo_custom_lib.git_helpers import read_file_from_git_repo, update_git_repo
from scripts.customizing.fwo_custom_lib.read_app_data_csv import extract_app_data_from_csv, extract_ip_data_from_csv

base_dir: str = "/usr/local/fworch/"
base_dir_etc: str = base_dir + "etc/"
app_data_repo_target_dir: str = base_dir_etc + "cmdb-repo"
recert_repo_target_dir: str = base_dir_etc + "recert-repo"
default_config_file_name: str = base_dir_etc + "secrets/customizingConfig.json"
default_import_source_string: str = "tufinRlm"


if __name__ == "__main__":
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
                            "ldapPath": "dc=example,dc=de", \
                            "gitRepo": "github.example.de/cmdb/app-export", \
                            "gitUsername": "git-user-1", \
                            "gitPassword": "gituser-1-pwd", \
                            "csvOwnerFilePattern": "NeMo_???_meta.csv", \
                            "csvAppServerFilePattern": "NeMo_???_IP_.*?.csv", \
                            "gitRepoOwnersWithActiveRecert": "github.example.de/FWO", \
                            "gitFileOwnersWithActiveRecert": "isolated-apps.txt", \
                            "validAppIdPrefixes": ["app-", "com-"], \
                            "importSource": "tufinRlm" \
                        } \
                        ',
    )
    parser.add_argument(
        "-s", "--suppress_certificate_warnings", action="store_true", default=True, help="suppress certificate warnings"
    )
    parser.add_argument(
        "-f", "--import_from_folder", help="if set, will try to read csv files from given folder instead of git repo"
    )
    parser.add_argument(
        "-l",
        "--limit",
        metavar="api_limit",
        default="150",
        help="The maximal number of returned results per HTTPS Connection; default=50",
    )
    parser.add_argument("-d", "--debug", default=0, help="debug level, default=0")

    args: argparse.Namespace = parser.parse_args()

    if args.suppress_certificate_warnings:
        urllib3.disable_warnings()

    logger: logging.Logger = get_logger(debug_level_in=2)

    # read config
    ldap_path: str = read_custom_config(args.config, "ldapPath", logger)
    git_repo_url_without_protocol: str = read_custom_config(args.config, "gitRepo", logger)
    git_username: str = read_custom_config(args.config, "gitUser", logger)
    git_password: str = read_custom_config(args.config, "gitPassword", logger)
    csv_owner_file_pattern: str = read_custom_config(args.config, "csvOwnerFilePattern", logger)
    csv_app_server_file_pattern: str = read_custom_config(args.config, "csvAppServerFilePattern", logger)
    recert_active_repo_url: str | None = read_custom_config_with_default(
        args.config, "gitRepoOwnersWithActiveRecert", None, logger
    )
    recert_active_file_name: str | None = read_custom_config_with_default(
        args.config, "gitFileOwnersWithActiveRecert", None, logger
    )
    owner_header_patterns: dict[str, str] = read_custom_config_with_default(
        args.config, "csvOwnerColumnPatterns", {}, logger
    )
    ip_header_patterns: dict[str, str] = read_custom_config_with_default(args.config, "csvIpColumnPatterns", {}, logger)
    import_source_string: str = read_custom_config_with_default(
        args.config, "importSource", default_import_source_string, logger
    )
    valid_app_id_prefixes: list[str] | None = read_custom_config_with_default(
        args.config, "validAppIdPrefixes", None, logger
    )

    if args.debug:
        debug_level: int = int(args.debug)
    else:
        debug_level = 0

    #############################################
    # 1. get CSV files from github repo

    import_from_folder: str | None = args.import_from_folder
    if import_from_folder:
        base_dir = import_from_folder
        app_data_repo_target_dir = import_from_folder
    else:
        base_dir = app_data_repo_target_dir
        app_data_repo_url: str = "https://" + git_username + ":" + git_password + "@" + git_repo_url_without_protocol

        repo_updated: bool = update_git_repo(app_data_repo_url, app_data_repo_target_dir, logger)
        if not repo_updated:
            logger.warning("trying to read csv files from folder given as parameter...")

    #############################################
    # 2. get app list with activated recertification

    if recert_active_repo_url and recert_active_file_name:
        recert_repo_url: str = f"https://{git_username}:{git_password}@{recert_active_repo_url}"
        recert_activation_data: str | None = read_file_from_git_repo(
            recert_repo_url,
            recert_repo_target_dir,
            recert_active_file_name,
            logger,
        )
        recert_active_app_list: list[str] = recert_activation_data.splitlines() if recert_activation_data else []
        logger.info("found %s apps with active recertification", len(recert_active_app_list))
    else:
        recert_active_app_list: list[str] = []
        logger.info("no recertification activation source configured; skipping activation of recertification import")

    #############################################
    # 3. get app data from CSV files
    app_list: list[Owner] = []
    re_owner_file_pattern: re.Pattern[str] = re.compile(csv_owner_file_pattern)
    for file_path in Path(app_data_repo_target_dir).iterdir():
        if re_owner_file_pattern.match(file_path.name):
            extract_app_data_from_csv(
                file_path.name,
                app_list,
                ldap_path,
                import_source_string,
                Owner,
                logger,
                debug_level,
                base_dir=base_dir,
                recert_active_app_list=recert_active_app_list,
                column_patterns=owner_header_patterns,
                valid_app_id_prefixes=valid_app_id_prefixes,
            )

    app_dict: dict[str, Owner] = transform_app_list_to_dict(app_list)

    re_app_server_file_pattern: re.Pattern[str] = re.compile(csv_app_server_file_pattern)
    for file_path in Path(app_data_repo_target_dir).iterdir():
        if re_app_server_file_pattern.match(file_path.name):
            if debug_level > 0:
                logger.info("importing IP data from file %s ...", file_path.name)
            extract_ip_data_from_csv(
                file_path.name,
                app_dict,
                Appip,
                logger,
                debug_level,
                base_dir=base_dir,
                column_patterns=ip_header_patterns,
            )

    #############################################
    # 4. write owners to json file using the same and basename path as this script, just replacing .py with .json
    write_owners_to_json(app_dict, __file__, logger=logger)

    #############################################
    # 5. Some statistics
    if debug_level > 0:
        logger.info("total #apps: %s", len(app_dict))
        apps_with_ip: int = 0
        for owner in app_dict.values():
            apps_with_ip += 1 if len(owner.app_servers) > 0 else 0
        logger.info("#apps with ip addresses: %s", apps_with_ip)
        total_ips: int = 0
        for owner in app_dict.values():
            total_ips += len(owner.app_servers)
        logger.info("#ip addresses in total: %s", total_ips)

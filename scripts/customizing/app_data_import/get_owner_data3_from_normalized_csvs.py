#!/usr/bin/python3
# revision history:
__version__ = "2025-12-02-01"

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
import json
import os
import re
import sys
import traceback
from pathlib import Path

import git  # apt install python3-git # or: pip install git
import urllib3

from scripts.customizing.fwo_custom_lib.app_data_basics import transform_app_list_to_dict, transform_owner_dict_to_list
from scripts.customizing.fwo_custom_lib.app_data_models import Appip, Owner
from scripts.customizing.fwo_custom_lib.basic_helpers import get_logger, read_custom_config
from scripts.customizing.fwo_custom_lib.read_app_data_csv import extract_app_data_from_csv, extract_ip_data_from_csv

base_dir = "/usr/local/fworch/"
base_dir_etc = base_dir + "etc/"
app_data_repo_target_dir = base_dir_etc + "cmdb-repo"
recert_repo_target_dir = base_dir_etc + "recert-repo"
default_config_file_name = base_dir_etc + "secrets/customizingConfig.json"
import_source_string = "tufinRlm"


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
                            "gitFileOwnersWithActiveRecert": "isolated-apps.txt" \
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

    args = parser.parse_args()

    if args.suppress_certificate_warnings:
        urllib3.disable_warnings()

    logger = get_logger(debug_level_in=2)

    # read config
    ldap_path = read_custom_config(args.config, "ldapPath", logger)
    git_repo_url_without_protocol = read_custom_config(args.config, "gitRepo", logger)
    git_username = read_custom_config(args.config, "gitUser", logger)
    git_password = read_custom_config(args.config, "gitPassword", logger)
    csv_owner_file_pattern = read_custom_config(args.config, "csvOwnerFilePattern", logger)
    csv_app_server_file_pattern = read_custom_config(args.config, "csvAppServerFilePattern", logger)
    recert_active_repo_url = read_custom_config(args.config, "gitRepoOwnersWithActiveRecert", logger)
    recert_active_file_name = read_custom_config(args.config, "gitFileOwnersWithActiveRecert", logger)

    if args.debug:
        debug_level = int(args.debug)
    else:
        debug_level = 0

    #############################################
    # 1. get CSV files from github repo

    if args.import_from_folder:
        base_dir = args.import_from_folder
    else:
        base_dir = app_data_repo_target_dir
        app_data_repo_url = "https://" + git_username + ":" + git_password + "@" + git_repo_url_without_protocol

        try:
            if os.path.exists(app_data_repo_target_dir):
                # If the repository already exists, open it and perform a pull
                repo = git.Repo(app_data_repo_target_dir)
                origin = repo.remotes.origin
                origin.pull()
            else:
                repo = git.Repo.clone_from(app_data_repo_url, app_data_repo_target_dir)
        except Exception:
            logger.warning(
                "could not clone/pull git repo from "
                + app_data_repo_url
                + ", exception: "
                + str(traceback.format_exc())
            )
            logger.warning("trying to read csv files from folder given as parameter...")

    #############################################
    # 2. get app list with activated recertification

    recert_repo_url = f"https://{git_username}:{git_password}@{recert_active_repo_url}"
    try:
        if os.path.exists(recert_repo_target_dir):
            # If the repository already exists, open it and perform a pull
            repo = git.Repo(recert_repo_target_dir)
            origin = repo.remotes.origin
            origin.pull()
        else:
            repo = git.Repo.clone_from(recert_repo_url, recert_repo_target_dir)
    except Exception:
        logger.warning(
            "could not clone/pull git repo from " + recert_repo_url + ", exception: " + str(traceback.format_exc())
        )

    recert_activation_file = f"{recert_repo_target_dir}/{recert_active_file_name}"
    recert_active_app_list = []
    try:
        with open(recert_activation_file) as f:
            recert_active_app_list = f.read().splitlines()
    except Exception:
        logger.warning(f"could not read {recert_activation_file}, exception: {traceback.format_exc()!s}")

    #############################################
    # 3. get app data from CSV files
    app_list = []
    re_owner_file_pattern = re.compile(csv_owner_file_pattern)
    for file_name in os.listdir(app_data_repo_target_dir):
        if re_owner_file_pattern.match(file_name):
            extract_app_data_from_csv(
                file_name,
                app_list,
                ldap_path,
                import_source_string,
                Owner,
                logger,
                debug_level,
                base_dir=base_dir,
                recert_active_app_list=recert_active_app_list,
            )

    app_dict = transform_app_list_to_dict(app_list)

    re_app_server_file_pattern = re.compile(csv_app_server_file_pattern)
    for file_name in os.listdir(app_data_repo_target_dir):
        if re_app_server_file_pattern.match(file_name):
            if debug_level > 0:
                logger.info(f"importing IP data from file {file_name} ...")
            extract_ip_data_from_csv(file_name, app_dict, Appip, logger, debug_level, base_dir=base_dir)

    #############################################
    # 4. write owners to json file
    path = os.path.dirname(__file__)
    file_out = path + "/" + Path(os.path.basename(__file__)).stem + ".json"
    with open(file_out, "w") as out_fh:
        json.dump(transform_owner_dict_to_list(app_dict), out_fh, indent=3)

    #############################################
    # 5. Some statistics
    if debug_level > 0:
        logger.info(f"total #apps: {len(app_dict)!s}")
        apps_with_ip = 0
        for app_id in app_dict:
            apps_with_ip += 1 if len(app_dict[app_id].app_servers) > 0 else 0
        logger.info(f"#apps with ip addresses: {apps_with_ip!s}")
        total_ips = 0
        for app_id in app_dict:
            total_ips += len(app_dict[app_id].app_servers)
        logger.info(f"#ip addresses in total: {total_ips!s}")

    sys.exit(0)

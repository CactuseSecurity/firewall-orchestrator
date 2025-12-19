#!/usr/bin/python3
# reads the main app data from multiple csv files contained in a git repo
# users will reside in external ldap groups with standardized names
# only the main responsible person per app is taken from the csv files
# this does not use Tufin RLM any longer as a source
# here app servers will only have ip addresses (no names)

# dependencies: 
#   a) package python3-git must be installed
#   b) requires the following config items in /usr/local/orch/etc/secrets/customizingConfig.json

'''
sample config file /usr/local/orch/etc/secrets/customizingConfig.json

{
    "gitRepoUrl": "github.domain.de/CMDB-export",
    "gitusername": "gituser1",
    "gitpassword": "xxx",
    "csvAllOwnerFiles": ["all-apps.csv", "all-infra-services.csv"],
    "csvAppServerFiles": ["app-servers.csv", "com-servers.csv"],
    "ldapPath": "CN={USERID},OU=Benutzer,DC=DOMAIN,DC=DE"
}
'''

from asyncio.log import logger
import argparse
import csv
import json
import logging
import os
from pathlib import Path
import sys
import traceback
import urllib3
from typing import Any

import git  # apt install python3-git # or: pip install git
import requests
from scripts.customizing.fwo_custom_lib.basic_helpers import read_custom_config, get_logger


base_dir: str = "/usr/local/fworch/"
base_dir_etc: str = base_dir + "etc/"
repo_target_dir: str = base_dir_etc + "cmdb-repo"
default_config_file_name: str = base_dir_etc + "secrets/customizingConfig.json"
import_source_string: str = "tufinRlm" # change this to "cmdb-csv-export"? or will this break anything?


def build_dn(user_id: str, ldap_path: str) -> str:
    dn: str = ""
    if len(user_id)>0:
        if '{USERID}' in ldap_path:
            dn = ldap_path.replace('{USERID}', user_id)
        else:
            logger.error("could not find {USERID} parameter in ldapPath " + ldap_path)
    return dn


# adds data from csv file to appData
# order of files in important: we only import apps which are included in files 3 and 4 (which only contain active apps)
# so first import files 3 and 4, then import files 1 and 2^
def _get_csv_columns(contains_ip: bool) -> tuple[int, int, int, int, int | None]:
    if contains_ip:
        return 0, 2, 4, 5, 12
    return 0, 1, 3, 4, None


def _read_csv_rows(csv_file_name: str) -> list[list[str]]:
    try:
        with open(csv_file_name, newline='', encoding="utf-8") as csv_file_handle:
            reader = csv.reader(csv_file_handle)
            return list(reader)[1:]  # Skip headers in first line
    except Exception:
        logger.error("error while trying to read csv file '" + csv_file_name + "', exception: " + str(traceback.format_exc()))
        sys.exit(1)


def _add_app_from_line(
    line: list[str],
    app_data: dict[str, dict[str, Any]],
    app_name_column: int,
    app_id_column: int,
    app_owner_biso_column: int,
    app_owner_tiso_column: int,
) -> bool:
    app_id: str = line[app_id_column]
    app_name: str = line[app_name_column]
    app_main_user: str = line[app_owner_tiso_column]
    main_user_dn: str = build_dn(app_main_user, ldap_path)
    biso_dn: str = build_dn(line[app_owner_biso_column], ldap_path)
    if main_user_dn == '':
        logger.warning('adding app without main user: ' + app_id)
    if app_id in app_data:
        return False
    app_data[app_id] = {
        "app_id_external": app_id,
        "name": app_name,
        "main_user": main_user_dn,
        "BISO": biso_dn,
        "modellers": [],
        "import_source": import_source_string,
        "app_servers": [],
    }
    return True


def _add_ip_from_line(
    line: list[str],
    app_data: dict[str, dict[str, Any]],
    app_id: str,
    app_server_ip_column: int | None,
) -> bool:
    if app_server_ip_column is None:
        return False
    app_server_ip: str = line[app_server_ip_column]
    if app_server_ip is None or app_server_ip == "":
        return False
    if app_server_ip in app_data[app_id]['app_servers']:
        return False
    app_data[app_id]['app_servers'].append({
        "ip": app_server_ip,
        "ip_end": app_server_ip,
        "type": "host",
        "name": f"host_{app_server_ip}"
    })
    return True


def extract_app_data_from_csv_file(csv_file: str, app_data: dict[str, dict[str, Any]], contains_ip: bool) -> None:
    app_name_column, app_id_column, app_owner_biso_column, app_owner_tiso_column, app_server_ip_column = _get_csv_columns(contains_ip)
    csv_file_name: str = repo_target_dir + '/' + csv_file # add directory to csv files
    app_data_from_csv: list[list[str]] = _read_csv_rows(csv_file_name)

    count_skips: int = 0
    for line in app_data_from_csv:
        app_id: str = line[app_id_column]
        if not (app_id.lower().startswith('app-') or app_id.lower().startswith('com-')):
            logger.info(f'ignoring line from csv file: {app_id} - inconclusive appId')
            count_skips += 1
            continue

        if not contains_ip:
            if not _add_app_from_line(
                line,
                app_data,
                app_name_column,
                app_id_column,
                app_owner_biso_column,
                app_owner_tiso_column,
            ):
                logger.debug(f'ignoring line from csv file: {app_id} - inactive?')
                count_skips += 1
        elif app_id in app_data:
            if not _add_ip_from_line(line, app_data, app_id, app_server_ip_column):
                count_skips += 1
        else:
            logger.debug(f'ignoring line from csv file: {app_id} - inactive?')
            count_skips += 1

    logger.info(f"{str(csv_file_name)}: #total lines {str(len(app_data_from_csv))}, skipped: {str(count_skips)}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description='Read configuration from FW management via API calls')
    parser.add_argument('-c', '--config', default=default_config_file_name,
                        help='Filename of custom config file for modelling imports')
    parser.add_argument('-s', "--suppress_certificate_warnings", action='store_true', default = True,
                        help = "suppress certificate warnings")
    parser.add_argument('-l', '--limit', metavar='api_limit', default='150',
                        help='The maximal number of returned results per HTTPS Connection; default=50')

    args: argparse.Namespace = parser.parse_args()

    if args.suppress_certificate_warnings:
        urllib3.disable_warnings()

    logger: logging.Logger = get_logger(debug_level_in=2)

    # read config
    ldap_path: str = read_custom_config(args.config, 'ldapPath', logger=logger)
    git_repo_url: str = read_custom_config(args.config, 'ipamGitRepo', logger=logger)
    git_username: str = read_custom_config(args.config, 'ipamGitUser', logger=logger)
    git_password: str = read_custom_config(args.config, 'gitpassword', logger=logger)
    csv_all_owner_files: list[str] = read_custom_config(args.config, 'csvAllOwnerFiles', logger=logger)
    csv_app_server_files: list[str] = read_custom_config(args.config, 'csvAppServerFiles', logger=logger)

    #############################################
    # 1. get CSV files from github repo
    repo_url: str = "https://" + git_username + ":" + git_password + "@" + git_repo_url
    if os.path.exists(repo_target_dir):
        # If the repository already exists, open it and perform a pull
        repo: git.Repo = git.Repo(repo_target_dir)
        origin: git.Remote = repo.remotes.origin
        origin.pull()
    else:
        repo = git.Repo.clone_from(repo_url, repo_target_dir)

    #############################################
    # 2. get app data from CSV files
    app_data: dict[str, dict[str, Any]] = {}
    csv_file: str
    for csv_file in csv_all_owner_files:
        extract_app_data_from_csv_file(csv_file, app_data, False)
    for csv_file in csv_app_server_files:
        extract_app_data_from_csv_file(csv_file, app_data, True)

    owner_data: dict[str, list[dict[str, Any]]] = { "owners": [] } 

    app_id: str
    for app_id in app_data:
        if app_data[app_id]['app_id_external'] != '':
            owner_data['owners'].append({
                "name": app_data[app_id]['name'],
                "app_id_external": app_data[app_id]['app_id_external'],
                "main_user": app_data[app_id]['main_user'],
                "modellers": app_data[app_id]['modellers'],
                "criticality": app_data[app_id]['criticality'] if 'criticality' in app_data[app_id] else None,
                "import_source": app_data[app_id]['import_source'],
                "app_servers": app_data[app_id]['app_servers']
            })
        else:
            logger.warning(f"App {app_id} has no external app id, skipping...")

    #############################################    
    # 3. write owners to json file
    path: str = os.path.dirname(__file__)
    file_out: str = path + '/' + Path(os.path.basename(__file__)).stem + ".json"
    with open(file_out, "w", encoding="utf-8") as out_fh:
        json.dump(owner_data, out_fh, indent=3)
        
    #############################################    
    # 4. Some statistics
    logger.info(f"total #apps: {str(len(app_data))}")
    apps_with_ip: int = 0
    for app_id in app_data:
        apps_with_ip += 1 if len(app_data[app_id]['app_servers']) > 0 else 0
    logger.info(f"#apps with ip addresses: {str(apps_with_ip)}")
    total_ips: int = 0
    for app_id in app_data:
        total_ips += len(app_data[app_id]['app_servers'])
    logger.info(f"#ip addresses in total: {str(total_ips)}")

    sys.exit(0)

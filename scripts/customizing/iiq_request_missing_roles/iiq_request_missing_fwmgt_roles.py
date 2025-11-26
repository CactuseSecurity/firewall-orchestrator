#!/usr/bin/python3

import sys
import json
import urllib3
import socket
import argparse
import os
import re
import git  # apt install python3-git # or: pip install git
import urllib.parse
from datetime import datetime
from pathlib import Path
from scripts.customizing.fwo_custom_lib.app_data_models import Owner, Appip
from scripts.customizing.fwo_custom_lib.read_app_data_csv import extract_app_data_from_csv, extract_ip_data_from_csv
from scripts.customizing.fwo_custom_lib.basic_helpers import read_custom_config, get_logger
from scripts.customizing.fwo_custom_lib.app_data_basics import transform_app_list_to_dict
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

fwo_base_dir = "/usr/local/fworch/"
fwo_tmp_dir = fwo_base_dir + "tmp/iiq_request_missing_fwmgt_roles/"
log_dir="/var/log/fworch/iiq_request_missing_fwmgt_roles"
base_dir_etc = fwo_base_dir + "etc/"
cmdb_repo_target_dir = fwo_tmp_dir + "cmdb-repo"
default_config_file_name = base_dir_etc + "customizingConfig.json"


def is_valid_ipv4_address(address):
    try:
        socket.inet_pton(socket.AF_INET, address)
    except AttributeError:  # no inet_pton here, sorry
        try:
            socket.inet_aton(address)
        except socket.error:
            return False
        return address.count('.') == 3
    except socket.error:  # not a valid address
        return False

    return True


def get_owners_from_csv_files(csv_owner_file_pattern, csv_app_server_file_pattern, repo_target_dir, ldap_path, logger, debug_level):
    app_list = []
    re_owner_file_pattern = re.compile(csv_owner_file_pattern)
    for file_name in os.listdir(repo_target_dir):
        if re_owner_file_pattern.match(file_name):
            extract_app_data_from_csv(file_name, app_list, ldap_path, "import-source-dummy", Owner, logger, debug_level, base_dir=repo_target_dir)

    owner_dict = transform_app_list_to_dict(app_list)

    re_app_server_file_pattern = re.compile(csv_app_server_file_pattern)
    for file_name in os.listdir(repo_target_dir):
        if re_app_server_file_pattern.match(file_name):
            if debug_level>0:
                logger.info(f"importing IP data from file {file_name} ...")
            extract_ip_data_from_csv(file_name, owner_dict, Appip, logger, debug_level, base_dir=repo_target_dir)

    # now only choose those owners which have at least one app server with a non-empty IP assigned
    remove_apps_without_ip_addresses(owner_dict, debug_level)

    tisos = get_tisos_from_owner_dict(owner_dict)
    return owner_dict, tisos


def remove_apps_without_ip_addresses(owner_dict, debug_level=0):
    apps_to_remove = []
    for app_key in owner_dict:
        owner = owner_dict[app_key]
        has_ip = False
        for app_server in owner.app_servers:
            if app_server.ip_start is not None and is_valid_ipv4_address(str(app_server.ip_start)):
                has_ip = True
                break
        if not has_ip:
            apps_to_remove.append(app_key)
    for app_key in apps_to_remove:
        if debug_level>5:
            logger.info(f"removing app {app_key} as it has no valid IP address assigned")
        del owner_dict[app_key]


def get_tisos_from_owner_dict(app_dict):
    tisos = {}
    for app_id in app_dict:
        owner = app_dict[app_id]
        if owner.main_user is not None and owner.main_user != "":
            tiso = owner.main_user.replace("CN=", "")   # remove possible CN= prefix
            if "," in tiso:
                tiso = tiso.split(",")[0]   # take only the user name part before any comma
            tisos[f"{app_id}"] = tiso
        else:
            logger.warning(f"owner {owner.name} has no main user, cannot get TISO")
    return tisos


def get_git_repo(git_repo_url, git_username, git_password, repo_target_dir):
    encoded_password = urllib.parse.quote(git_password, safe="")
    repo_url = "https://" + git_username + ":" + encoded_password + "@" + git_repo_url

    if os.path.exists(repo_target_dir):
        # If the repository already exists, open it and perform a pull
        repo = git.Repo(repo_target_dir)
        origin = repo.remotes.origin
        # for DEBUG: do not pull
        origin.pull()
    else:
        git.Repo.clone_from(repo_url, repo_target_dir)

def request_all_roles(owner_dict, tisos, tiso_orgids, iiq_client, stats, first, run_workflow, debug=0):
    counter = 0
    # create new groups
    logger.info("creating new groups in iiq")
    for app_id_with_prefix in owner_dict:
        if debug>5:
            logger.info(f"checking app {app_id_with_prefix}")
        counter += 1
        tiso = tisos.get(app_id_with_prefix)
        org_id = tiso_orgids.get(tiso)
        if org_id is None:
            logger.warning("did not find an OrgId for owner " + app_id_with_prefix + ", skipping group creation")
            continue

        app_prefix, app_id = app_id_with_prefix.split("-")
        # get existing (already modelled) functions for this app to find out, what still needs to be changed in iiq
        if iiq_client.app_functions_exist_in_iiq(app_prefix, app_id, stats):
            if debug>5:
                logger.info(f"not requesting groups for {app_id_with_prefix} - they already exist")
            continue

        if debug>5:
            logger.info(f"requesting groups for {app_id_with_prefix}")
        iiq_client.request_group_creation(app_prefix, app_id, org_id, tiso, owner_dict[app_id_with_prefix].name, stats, run_workflow=run_workflow)

        # if first parameter is set, only handle the first "first" applications, otherwise handle all
        if first > 0 and counter >= first: 
            break


def get_tisos_orgids(tisos, iiq_client, exit_after_dump=False):
    tiso_orgids = {}
    if iiq_client.debug>0:
        logger.info("getting tiso orgids from iiq")
    for tiso in set(tisos.values()):
        org_id = iiq_client.get_org_id(tiso)
        if org_id is not None:
            tiso_orgids[tiso] = org_id 
    if len(tiso_orgids.keys())==0:
        logger.error("could not resolve a single TISO OrgId, quitting ")
        sys.exit(1)
    elif iiq_client.debug>2:
        print(tiso_orgids)

    if exit_after_dump:
        for tiso_user_id in tiso_orgids:
            print(f"{tiso_user_id},{tiso_orgids[tiso_user_id]}")
        sys.exit(0)
    return tiso_orgids


def init_statistics():
    stats = {}
    stats_fields = ["apps_with_running_requests", "apps_newly_requested", "apps_request_simulated", "apps_with_invalid_alfabet_id",
        "apps_with_request_errors", "apps_with_unexpected_request_errors", "existing_technical_functions" ]
    for field in stats_fields:
        stats.update({ field: [], f"{field}_count": 0 })
    return stats

def write_stats_to_file(stats, log_dir):
    os.makedirs(log_dir, exist_ok=True)
    # Get current date as YYYY-MM-DD
    date_str = datetime.now().strftime("%Y-%m-%d")
    log_file = f"{log_dir}/{date_str}_iiq_request.log"
    Path(log_file).write_text(json.dumps(stats, indent=2))


if __name__ == "__main__":
    ALLOWED_STAGE_VALUES = {"prod", "test"}
    ALLOWED_RUN_VALUES = {True, False}

    logger = get_logger()

    parser = argparse.ArgumentParser(
        description='Read configuration from FW management via API calls')
    parser.add_argument('-c', '--config', default=default_config_file_name,
                        help='Filename of custom config file for modelling imports, default file='+default_config_file_name+',\
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
                        ')
    parser.add_argument('-s', "--suppress_certificate_warnings", action='store_true', default = True,
                        help = "suppress certificate warnings")
    parser.add_argument('-d', "--debug", metavar='debug_level', default = '0',
                        help = "set to >1 for debugging and to avoid CMDB git pull (due to permission conflicts in debug mode)")
    parser.add_argument('-f', "--first", metavar='handle_first_x_apps', default = '0',
                        help = "set to value greater than 0 to only handle the first x applications")
    parser.add_argument('-g', "--stage", metavar='stage_of_workflow', choices=ALLOWED_STAGE_VALUES,
                        required=True,
                        help=f"Specify the stage of your system. Allowed values: {', '.join(ALLOWED_STAGE_VALUES)}")
    parser.add_argument('-r', "--run_workflow", action="store_true", 
                        help="Should the created IIQ workflow be run? If left out, the workflow is not started (simulation only).")
    parser.add_argument('-j', "--just_dump_tiso_org_ids", action="store_true", 
                        help="Just dump the org_ids of all TISOs as CSV and then exit.")
    parser.add_argument('-o', "--import_from_folder", 
                        help = "if set, will try to read csv files from given folder instead of git repo")

    args = parser.parse_args()

    csv_owner_file_pattern = read_custom_config(args.config, 'csvOwnerFilePattern', logger)
    csv_app_server_file_pattern = read_custom_config(args.config, 'csvAppServerFilePattern', logger)

    debug = int(args.debug)

    if args.stage == 'prod':
        stage = ''  # the production instance does not need any extra strings
    else:
        stage = args.stage
    first = int(args.first)
    if args.suppress_certificate_warnings:
        urllib3.disable_warnings()

    if debug>3:
        logger.debug(f"using config file {args.config}")

    ldap_path = read_custom_config(args.config, 'ldapPath', logger)
    iiq_hostname = read_custom_config(args.config, 'iiqHostname', logger)
    iiq_user = read_custom_config(args.config, 'iiqUsername', logger)
    iiq_password = read_custom_config(args.config, 'iiqPassword', logger)
    cmdb_exports = []
    
    # get/set template parameters
    iiq_app_name =  read_custom_config(args.config, 'iiqAppName', logger)
    user_prefix =  read_custom_config(args.config, 'userPrefix', logger)
    iiq_client = IIQClient(iiq_hostname, iiq_user, iiq_password, iiq_app_name, user_prefix, stage=stage, debug=debug, logger=logger)

    if args.import_from_folder:
        csv_file_base_dir = args.import_from_folder
    else:
        git_repo_url = read_custom_config(args.config, 'cmdbGitRepoUrl', logger)
        git_username = read_custom_config(args.config, 'cmdbGitUsername', logger)
        git_password = read_custom_config(args.config, 'cmdbGitPassword', logger)
        csv_file_base_dir=cmdb_repo_target_dir
        get_git_repo(git_repo_url, git_username, git_password, cmdb_repo_target_dir)

    if debug>0:
        logger.info("getting owners from file")
    owners, tisos = get_owners_from_csv_files(csv_owner_file_pattern, csv_app_server_file_pattern, csv_file_base_dir, ldap_path, logger, debug)
    
    tiso_orgids = get_tisos_orgids(tisos, iiq_client, exit_after_dump=args.just_dump_tiso_org_ids)

    # collect all app ids
    # owner_dict = [key.split("|")[0] for key in owners.keys()]

    stats = init_statistics()

    request_all_roles(owners, tisos, tiso_orgids, iiq_client, stats, first, args.run_workflow, debug=debug)

    if debug>0:
        print ("Stats: " + json.dumps(stats, indent=3))
    
    write_stats_to_file(stats, log_dir)

    sys.exit(0)

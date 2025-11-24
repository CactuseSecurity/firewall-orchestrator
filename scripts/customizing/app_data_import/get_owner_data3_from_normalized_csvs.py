#!/usr/bin/python3
# revision history:
__version__ = "2025-11-20-01"

# 2025-11-20-01, initial version

# reads the main app data from multiple csv files contained in a git repo
# users will reside in external ldap groups with standardized names
# only the main responsible person per app is taken from the csv files
# this does not use Tufin RLM any longer as a source
# here app servers will only have ip addresses (no names)


# dependencies: 
#   a) package python3-git must be installed
#   b) requires the following config items in /usr/local/orch/etc/secrets/customizingConfig.json (or given config file):

import traceback
import json
import sys
import argparse
import os
from pathlib import Path
import git  # apt install python3-git # or: pip install git
import re
import urllib3
from ..fwo_custom_lib.app_data_models import Owner, Appip
from ..fwo_custom_lib.read_app_data_csv import extract_app_data_from_csv, extract_ip_data_from_csv
from ..fwo_custom_lib.basic_helpers import read_custom_config, get_logger
from ..fwo_custom_lib.app_data_basics import transform_owner_dict_to_list, transform_app_list_to_dict


base_dir = "/usr/local/fworch/"
base_dir_etc = base_dir + "etc/"
repo_target_dir = base_dir_etc + "cmdb-repo"
default_config_file_name = base_dir_etc + "secrets/customizingConfig.json"
import_source_string = "tufinRlm"


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description='Read configuration from FW management via API calls')
    parser.add_argument('-c', '--config', default=default_config_file_name,
                        help='Filename of custom config file for modelling imports, default file='+default_config_file_name+',\
                        sample config file content: \
                        { \
                            "ldapPath": "dc=example,dc=de", \
                            "gitRepo": "github.example.de/cmdb/app-export", \
                            "gitUsername": "git-user-1", \
                            "gitPassword": "gituser-1-pwd", \
                            "csvOwnerFilePattern": "NeMo_???_meta.csv", \
                            "csvAppServerFilePattern": "NeMo_???_IP_.*?.csv" \
                        } \
                        ')
    parser.add_argument('-s', "--suppress_certificate_warnings", action='store_true', default = True,
                        help = "suppress certificate warnings")
    parser.add_argument('-f', "--import_from_folder", 
                        help = "if set, will try to read csv files from given folder instead of git repo")
    parser.add_argument('-l', '--limit', metavar='api_limit', default='150',
                        help='The maximal number of returned results per HTTPS Connection; default=50')
    parser.add_argument('-d', "--debug", default = 0, 
                        help = "debug level, default=0")

    args = parser.parse_args()

    if args.suppress_certificate_warnings:
        urllib3.disable_warnings()

    logger = get_logger(debug_level_in=2)

    # read config
    ldap_path = read_custom_config(args.config, 'ldapPath', logger)
    git_repo_url_without_protocol = read_custom_config(args.config, 'gitRepo', logger)
    git_username = read_custom_config(args.config, 'gitUser', logger)
    git_password = read_custom_config(args.config, 'gitPassword', logger)
    csv_owner_file_pattern = read_custom_config(args.config, 'csvOwnerFilePattern', logger)
    csv_app_server_file_pattern = read_custom_config(args.config, 'csvAppServerFilePattern', logger)

    if args.debug:
        debug_level = int(args.debug)
    else:
        debug_level = 0

    #############################################
    # 1. get CSV files from github repo

    if args.import_from_folder:
        base_dir = args.import_from_folder
    else:
        base_dir=repo_target_dir
        repo_url = "https://" + git_username + ":" + git_password + "@" + git_repo_url_without_protocol

        try:
            if os.path.exists(repo_target_dir):
                # If the repository already exists, open it and perform a pull
                repo = git.Repo(repo_target_dir)
                origin = repo.remotes.origin
                origin.pull()
            else:
                repo = git.Repo.clone_from(repo_url, repo_target_dir)
        except Exception as e:
            logger.warning("could not clone/pull git repo from " + repo_url + ", exception: " + str(traceback.format_exc()))
            logger.warning("trying to read csv files from folder given as parameter...")

    #############################################
    # 2. get app data from CSV files
    app_list = []
    re_owner_file_pattern = re.compile(csv_owner_file_pattern)
    for file_name in os.listdir(repo_target_dir):
        if re_owner_file_pattern.match(file_name):
            extract_app_data_from_csv(file_name, app_list, ldap_path, import_source_string, Owner, logger, debug_level, base_dir=base_dir)

    app_dict = transform_app_list_to_dict(app_list)

    re_app_server_file_pattern = re.compile(csv_app_server_file_pattern)
    for file_name in os.listdir(repo_target_dir):
        if re_app_server_file_pattern.match(file_name):
            if debug_level>0:
                logger.info(f"importing IP data from file {file_name} ...")
            extract_ip_data_from_csv(file_name, app_dict, Appip, logger, debug_level, base_dir=base_dir)

    #############################################    
    # 3. write owners to json file
    path = os.path.dirname(__file__)
    file_out = path + '/' + Path(os.path.basename(__file__)).stem + ".json"
    with open(file_out, "w") as out_fh:
        json.dump(transform_owner_dict_to_list(app_dict), out_fh, indent=3)
        
    #############################################    
    # 4. Some statistics
    if debug_level>0:
        logger.info(f"total #apps: {str(len(app_dict))}")
        apps_with_ip = 0
        for app_id in app_dict:
            apps_with_ip += 1 if len(app_dict[app_id].app_servers) > 0 else 0
        logger.info(f"#apps with ip addresses: {str(apps_with_ip)}")
        total_ips = 0
        for app_id in app_dict:
            total_ips += len(app_dict[app_id].app_servers)
        logger.info(f"#ip addresses in total: {str(total_ips)}")

    sys.exit(0)
    
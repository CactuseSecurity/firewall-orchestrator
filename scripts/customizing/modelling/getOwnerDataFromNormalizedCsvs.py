#!/usr/bin/python3
# reads the main app data from multiple csv files contained in a git repo
# users will reside in external ldap groups with standardized names
# only the main responsible person per app is taken from the csv files
# this does not use Tufin RLM any longer as a source
# here app servers will only have ip addresses (no names)

# dependencies: 
#   a) package python3-git must be installed
#   b) requires the following config items in /usr/local/orch/etc/secrets/customizingConfig.json (or given config file):

'''
sample config file /usr/local/orch/etc/secrets/customizingConfig.json

{
    "gitRepo": "github.domain.de/CMDB-export",
    "gitUser": "gituser1",
    "gitPassword": "xxx",
    "csvOwnerFilePattern": "NeMo_..._meta.csv",
    "csvAppServerFilePattern": "NeMo_..._IP.*?.csv",
    "ldapPath": "CN={USERID},OU=Benutzer,DC=DOMAIN,DC=DE"
}
'''

from asyncio.log import logger
import traceback
import requests.packages
import requests
import json
import sys
import argparse
import logging
import os
from pathlib import Path
import git  # apt install python3-git # or: pip install git
import csv
import re
import urllib3
from netaddr import IPAddress, IPNetwork


baseDir = "/usr/local/fworch/"
baseDirEtc = baseDir + "etc/"
repoTargetDir = baseDirEtc + "cmdb-repo"
defaultConfigFileName = baseDirEtc + "secrets/customizingConfig.json"
importSourceString = "tufinRlm"


class Owner:
    def __init__(self, name, app_id_external, main_user, recert_period_days, import_source):
        self.name = name
        self.app_id_external = app_id_external
        self.main_user = main_user
        self.modellers = []
        self.import_source = import_source
        self.recert_period_days = recert_period_days
        self.app_servers = []

    def to_json(self):
        return (
            {
                "name": self.name,
                "app_id_external": self.app_id_external,
                "main_user": self.main_user,
                # "criticality": self.criticality,
                "import_source": self.import_source,
                "recert_period_days": self.recert_period_days,
                "app_servers": [ip.to_json() for ip in self.app_servers]
            }
        )


class app_ip:
    def __init__(self, app_id_external: str, ip_start: IPAddress, ip_end: IPAddress, type: str, name: str):
        self.name = name
        self.app_id_external = app_id_external
        self.ip_start = ip_start
        self.ip_end = ip_end
        self.type = type

    def to_json(self):
        return (
            {
            "name": self.name,
            "app_id_external": self.app_id_external,
            "ip": str(IPAddress(self.ip_start)),
            "ip_end": str(IPAddress(self.ip_end)),
            "type": self.type
            }
        )
        

def read_custom_config(configFilename, keyToGet):
    try:
        with open(configFilename, "r") as customConfigFH:
            customConfig = json.loads(customConfigFH.read())
        return customConfig[keyToGet]

    except Exception:
        logger.error("could not read key '" + keyToGet + "' from config file " + configFilename + ", Exception: " + str(traceback.format_exc()))
        sys.exit(1)


def build_dn(userId, ldapPath):
    dn = ""
    if len(userId)>0:
        if '{USERID}' in ldapPath:
            dn = ldapPath.replace('{USERID}', userId)
        else:
            logger.error("could not find {USERID} parameter in ldapPath " + ldapPath)
    return dn


def get_logger(debug_level_in=0):
    debug_level=int(debug_level_in)
    if debug_level>=1:
        llevel = logging.DEBUG
    else:
        llevel = logging.INFO

    logger = logging.getLogger('import-fworch-app-data')
    logformat = "%(asctime)s [%(levelname)-5.5s] [%(filename)-10.10s:%(funcName)-10.10s:%(lineno)4d] %(message)s"
    logging.basicConfig(format=logformat, datefmt="%Y-%m-%dT%H:%M:%S%z", level=llevel)
    logger.setLevel(llevel)

    #set log level for noisy requests/connectionpool module to WARNING:
    connection_log = logging.getLogger("urllib3.connectionpool")
    connection_log.setLevel(logging.WARNING)
    connection_log.propagate = True

    if debug_level>8:
        logger.debug ("debug_level=" + str(debug_level) )
    return logger



def read_app_data_from_csv(csv_file_name: str):
    try:
        with open(csv_file_name, newline='') as csv_file_handle:
            reader = csv.reader(csv_file_handle)
            headers = next(reader)  # Get header row first
            
            # Define regex patterns for column headers
            name_pattern = re.compile(r'.*?:\s*Name')
            app_id_pattern = re.compile(r'.*?:\s*Alfabet-ID$')
            owner_tiso_pattern = re.compile(r'.*?:\s*TISO')
            owner_kwita_pattern = re.compile(r'.*?:\s*kwITA')
            
            # Find column indices using regex
            app_name_column = next(i for i, h in enumerate(headers) if name_pattern.match(h))
            app_id_column = next(i for i, h in enumerate(headers) if app_id_pattern.match(h))
            app_owner_tiso_column = next(i for i, h in enumerate(headers) if owner_tiso_pattern.match(h))
            app_owner_kwita_column = next(i for i, h in enumerate(headers) if owner_kwita_pattern.match(h))
            
            apps_from_csv = list(reader)  # Read remaining rows
    except Exception:
        logger.error("error while trying to read csv file '" + csv_file_name + "', exception: " + str(traceback.format_exc()))
        sys.exit(1)
    
    return apps_from_csv, app_name_column, app_id_column, app_owner_tiso_column, app_owner_kwita_column


# adds data from csv file to appData
# order of files in important: we only import apps which are included in files 3 and 4 (which only contain active apps)
# so first import files 3 and 4, then import files 1 and 2^
def extract_app_data_from_csv (csvFile: str, app_list: list, base_dir=repoTargetDir): 

    apps_from_csv = []
    csvFile = base_dir + '/' + csvFile # add directory to csv files

    apps_from_csv, app_name_column, app_id_column, app_owner_tiso_column, app_owner_kwita_column = read_app_data_from_csv(csvFile)

    countSkips = 0
    # append all owners from CSV
    for line in apps_from_csv:
        parse_app_line(line, app_name_column, app_id_column, app_owner_tiso_column, app_owner_kwita_column, app_list, countSkips)
    if debug_level>0:
        logger.info(f"{str(csvFile)}: #total lines {str(len(apps_from_csv))}, skipped: {str(countSkips)}")


def parse_app_line(line, app_name_column, app_id_column, app_owner_tiso_column, app_owner_kwita_column, app_list, countSkips):
    app_id = line[app_id_column]
    if app_id.lower().startswith('app-') or app_id.lower().startswith('com-'):
        app_name = line[app_name_column]
        app_main_user = line[app_owner_tiso_column]
        main_user_dn = build_dn(app_main_user, ldapPath)
        kwita = line[app_owner_kwita_column]
        if kwita is None or kwita == '' or kwita.lower() == 'nein':
            recert_period_days = 365
        else:
            recert_period_days = 182
        if main_user_dn=='' and debug_level>0:
            logger.warning('adding app without main user: ' + app_id)
        app_list.append(Owner(app_id_external=app_id, name=app_name, main_user=main_user_dn, recert_period_days = recert_period_days, import_source=importSourceString))
    else:
        if debug_level>1:
            logger.info(f'ignoring line from csv file: {app_id} - inconclusive appId')
        countSkips += 1


def read_ip_data_from_csv(csv_filename):
    try:
        with open(csv_filename, newline='', encoding='utf-8') as csvFile:
            reader = csv.reader(csvFile)
            headers = next(reader)  # Get header row first
            
            # Define regex patterns for column headers
            app_id_pattern = re.compile(r'.*?:\s*Alfabet-ID$')
            ip_pattern = re.compile(r'.*?:\s*IP')
            
            # Find column indices using regex
            app_id_column_no = next(i for i, h in enumerate(headers) if app_id_pattern.match(h))
            ip_column_no = next(i for i, h in enumerate(headers) if ip_pattern.match(h))
            
            ip_data = list(reader)  # Read remaining rows
    except Exception as e:
        logger.error("error while trying to read csv file '" + csv_filename + "', exception: " + str(traceback.format_exc()))
        sys.exit(1)
    
    return ip_data, app_id_column_no, ip_column_no


def parse_ip(line, app_id, ip_column_no, app_dict, count_skips):
    # add app server ip addresses (but do not add the whole app - it must already exist)
    app_server_ip_str = line[ip_column_no]
    if app_server_ip_str is not None and app_server_ip_str != "":
        try:
            ip_range = IPNetwork(app_server_ip_str)
        except Exception:
            if debug_level>1:
                logger.warning(f'error parsing IP/network {app_server_ip_str} for app {app_id}, skipping this entry')
            count_skips += 1
            return count_skips
        if ip_range.size > 1:
            ip_type = "network"
        else:
            ip_type = "host"

        ip_start = IPAddress(ip_range.first)
        ip_end = IPAddress(ip_range.last)
        ip_obj_name = f"{ip_type}_{app_server_ip_str}".replace('/', '_')
        app_server_ip = app_ip(app_id_external=app_id, ip_start=ip_start, ip_end=ip_end, type=ip_type, name=ip_obj_name)
        if app_server_ip not in app_dict[app_id].app_servers:
            app_dict[app_id].app_servers.append(app_server_ip)
    else:
        count_skips += 1                    

    return count_skips


# adds ip data from csv file to appData
def extract_ip_data_from_csv (csv_filename: str, app_dict: dict[str: Owner], base_dir=repoTargetDir): 

    valid_app_id_prefixes = ['app-', 'com-']

    ip_data = []
    csv_filename = base_dir + '/' + csv_filename # add directory to csv files

    ip_data, app_id_column_no, ip_column_no = read_ip_data_from_csv(csv_filename)

    count_skips = 0
    # append all owners from CSV
    for line in ip_data:
        count_skips += parse_single_ip_line(line, app_id_column_no, ip_column_no, app_dict, valid_app_id_prefixes)
    if debug_level>0:
        logger.info(f"{str(csv_filename)}: #total lines {str(len(ip_data))}, skipped: {str(count_skips)}")


def parse_single_ip_line(line, app_id_column_no, ip_column_no, app_dict, valid_app_id_prefixes):
    count_skips = 0
    if len(line)-1 < app_id_column_no:
        return 1

    app_id: str = line[app_id_column_no]
    app_id_prefix = app_id.split('-')[0].lower() + '-'

    if len(valid_app_id_prefixes)==0 or app_id_prefix in valid_app_id_prefixes:
        if app_id in app_dict.keys():
            count_skips = parse_ip(line, app_id, ip_column_no, app_dict, count_skips)
        else:
            if debug_level>1:
                logger.debug(f'ignoring line from csv file as the app_id is not part of the app_list: {app_id} inactive?')
            return 1
    else:
        if debug_level>1:
            logger.info(f'ignoring line from csv file: {app_id} - inconclusive appId')
        return 1
    return count_skips


def transform_owner_dict_to_list(app_data):
    owner_data = { "owners": [] }
    for app_id in app_data:
        owner_data['owners'].append( app_data[app_id].to_json())
    return owner_data


def transform_app_list_to_dict(app_list):
    app_data_dict = {}
    for app in app_list:
        app_data_dict[app.app_id_external] = app
    return app_data_dict


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description='Read configuration from FW management via API calls')
    parser.add_argument('-c', '--config', default=defaultConfigFileName,
                        help='Filename of custom config file for modelling imports')
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
    ldapPath = read_custom_config(args.config, 'ldapPath')
    gitRepoUrl = read_custom_config(args.config, 'gitRepo')
    gitUsername = read_custom_config(args.config, 'gitUser')
    gitPassword = read_custom_config(args.config, 'gitpassword')
    csvOwnerFilePattern = read_custom_config(args.config, 'csvOwnerFilePattern')
    csvAppServerFilePattern = read_custom_config(args.config, 'csvAppServerFilePattern')


    if args.import_from_folder:
        base_dir = args.import_from_folder
    else:
        base_dir=repoTargetDir

    if args.debug:
        debug_level = int(args.debug)
    else:
        debug_level = 0


    if args.import_from_folder:
        base_dir = args.import_from_folder
    else:
        base_dir=repoTargetDir

    if args.debug:
        debug_level = int(args.debug)
    else:
        debug_level = 0

        #############################################
        # 1. get CSV files from github repo

        try:
            repoUrl = "https://" + gitUsername + ":" + gitPassword + "@" + gitRepoUrl
            if os.path.exists(repoTargetDir):
                # If the repository already exists, open it and perform a pull
                repo = git.Repo(repoTargetDir)
                origin = repo.remotes.origin
                origin.pull()
            else:
                repo = git.Repo.clone_from(repoUrl, repoTargetDir)
        except Exception as e:
            logger.warning("could not clone/pull git repo from " + repoUrl + ", exception: " + str(traceback.format_exc()))
            logger.warning("trying to read csv files from folder given as parameter...")
            # sys.exit(1)

    #############################################
    # 2. get app data from CSV files
    app_list = []
    re_owner_file_pattern = re.compile(csvOwnerFilePattern)
    for file_name in os.listdir(repoTargetDir):
        if re_owner_file_pattern.match(file_name):
            extract_app_data_from_csv(file_name, app_list, base_dir=base_dir)

    app_dict = transform_app_list_to_dict(app_list)

    re_app_server_file_pattern = re.compile(csvAppServerFilePattern)
    for file_name in os.listdir(repoTargetDir):
        if re_app_server_file_pattern.match(file_name):
            if debug_level>0:
                logger.info(f"importing IP data from file {file_name} ...")
            extract_ip_data_from_csv(file_name, app_dict, base_dir=base_dir)

    #############################################    
    # 3. write owners to json file
    path = os.path.dirname(__file__)
    fileOut = path + '/' + Path(os.path.basename(__file__)).stem + ".json"
    with open(fileOut, "w") as outFH:
        json.dump(transform_owner_dict_to_list(app_dict), outFH, indent=3)
        
    #############################################    
    # 4. Some statistics
    if debug_level>0:
        logger.info(f"total #apps: {str(len(app_dict))}")
        appsWithIp = 0
        for app_id in app_dict:
            appsWithIp += 1 if len(app_dict[app_id].app_servers) > 0 else 0
        logger.info(f"#apps with ip addresses: {str(appsWithIp)}")
        totalIps = 0
        for app_id in app_dict:
            totalIps += len(app_dict[app_id].app_servers)
        logger.info(f"#ip addresses in total: {str(totalIps)}")

    sys.exit(0)
    
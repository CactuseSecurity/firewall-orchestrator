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
from netaddr import IPAddress, IPNetwork
import pydantic


baseDir = "/usr/local/fworch/"
baseDirEtc = baseDir + "etc/"
repoTargetDir = baseDirEtc + "cmdb-repo"
defaultConfigFileName = baseDirEtc + "secrets/customizingConfig.json"
importSourceString = "tufinRlm" # change this to "cmdb-csv-export"? or will this break anything?


class owner:
    def __init__(self, name, app_id_external, main_user, biso, import_source):
        self.name = name
        self.app_id_external = app_id_external
        self.main_user = main_user
        self.biso = biso
        self.modellers = []
        self.import_source = import_source
        self.app_servers = []


class app_ip:
    def __init__(self, app_id_external: str, ip_start: IPAddress, ip_end: IPAddress, type: str, name: str):
        self.name = name
        self.app_id_external = app_id_external
        self.ip_start = ip_start
        self.ip_end = ip_end
        self.type = type
        

def readConfig(configFilename, keyToGet):
    try:
        with open(configFilename, "r") as customConfigFH:
            customConfig = json.loads(customConfigFH.read())
        return customConfig[keyToGet]

    except Exception:
        logger.error("could not read key '" + keyToGet + "' from config file " + configFilename + ", Exception: " + str(traceback.format_exc()))
        sys.exit(1)


def buildDN(userId, ldapPath):
    dn = ""
    if len(userId)>0:
        if '{USERID}' in ldapPath:
            dn = ldapPath.replace('{USERID}', userId)
        else:
            logger.error("could not find {USERID} parameter in ldapPath " + ldapPath)
    return dn


def getLogger(debug_level_in=0):
    debug_level=int(debug_level_in)
    if debug_level>=1:
        llevel = logging.DEBUG
    else:
        llevel = logging.INFO

    logger = logging.getLogger('import-fworch-app-data')
    # logHandler = logging.StreamHandler(stream=stdout)
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


def match_and_extract_columns(colname, compiled_patterns):
    """Return (True, new_name) if the column matches a pattern, else (False, None)."""
    for p in compiled_patterns:
        m = p.search(colname)
        if m:
            # Capture the inner part — e.g., 'Alfabet-ID' or 'IP'
            return True, m.group(1)
    return False, None


# adds data from csv file to appData
# order of files in important: we only import apps which are included in files 3 and 4 (which only contain active apps)
# so first import files 3 and 4, then import files 1 and 2^
def extract_app_data_from_csv (csvFile: str, appData: dict, containsIp: bool): 

    appDataFromCsv = []
    csvFile = repoTargetDir + '/' + csvFile # add directory to csv files

    # read csv file:
    try:
        with open(csvFile, newline='') as csvFile:
            reader = csv.reader(csvFile)
            headers = next(reader)  # Get header row first
            
            # Define regex patterns for column headers
            name_pattern = re.compile(r'.*?:\s*Name')
            app_id_pattern = re.compile(r'.*?:\s*Alfabet-ID$')
            owner_tiso_pattern = re.compile(r'.*?:\s*TISO')
            owner_kwita_pattern = re.compile(r'.*?:\s*kwITA')
            
            # Find column indices using regex
            appNameColumn = next(i for i, h in enumerate(headers) if name_pattern.match(h))
            appIdColumn = next(i for i, h in enumerate(headers) if app_id_pattern.match(h))
            appOwnerTISOColumn = next(i for i, h in enumerate(headers) if owner_tiso_pattern.match(h))
            appOwnerBISOColumn = next(i for i, h in enumerate(headers) if owner_kwita_pattern.match(h))
            
            appDataFromCsv = list(reader)  # Read remaining rows
    except Exception:
        logger.error("error while trying to read csv file '" + csvFile + "', exception: " + str(traceback.format_exc()))
        sys.exit(1)

    countSkips = 0
    # append all owners from CSV
    for line in appDataFromCsv:
        appId = line[appIdColumn]
        if appId.lower().startswith('app-') or appId.lower().startswith('com-'):
            appName = line[appNameColumn]
            appMainUser = line[appOwnerTISOColumn]
            mainUserDn = buildDN(appMainUser, ldapPath)
            bisoDn = buildDN(line[appOwnerBISOColumn], ldapPath)
            if mainUserDn=='':
                logger.warning('adding app without main user: ' + appId)
            if appId not in appData.keys() and not containsIp:
                # only add app if it is in file 3 or 4
                appData.update({
                    appId: owner(app_id_external=app_id, appName=appName, main_user=mainUserDn,import_source=importSourceString)})
            else:
                logger.debug(f'ignoring line from csv file: {appId} - inactive?')
                countSkips += 1
        else:
            logger.info(f'ignoring line from csv file: {appId} - inconclusive appId')
            countSkips += 1
    logger.info(f"{str(csvFile)}: #total lines {str(len(appDataFromCsv))}, skipped: {str(countSkips)}")


# adds ip data from csv file to appData
def extract_ip_data_from_csv (csv_filename: str, app_data_dict: dict, containsIp: bool): 

    app_id_col_name = 'Alfabet-ID'
    ip_col_name = 'IP'
    column_names = [app_id_col_name, ip_col_name]
    column_prefix = r'[.*?]:'
    col_patterns = []
    valid_app_id_prefixes = ['app-', 'com-']
    # Compile regex patterns for matching column names
    column_patterns = [re.compile(fr'^{column_prefix}\s*({name})$') for name in column_names]

    ip_data = []
    csv_filename = repoTargetDir + '/' + csv_filename # add directory to csv files

    # read csv file:
    try:
        with open(csv_filename, newline='', encoding='utf-8') as file_obj:
            reader = csv.DictReader(file_obj)

            # Determine which columns to keep and how to rename them
            selected = {}
            for col in reader.fieldnames:
                matched, new_name = match_and_extract_columns(col, column_patterns)
                if matched:
                    selected[col] = new_name

            print(f"Selected columns and renaming: {selected}")

            # Read and transform
            ip_data = [
                {new: row[old] for old, new in selected.items()}
                for row in reader
            ]

    except Exception:
        logger.error("error while trying to read csv file '" + csv_filename + "', exception: " + str(traceback.format_exc()))
        sys.exit(1)

    countSkips = 0
    # append all owners from CSV
    for line in ip_data:
        app_id: str = line[app_id_col_name]
        if len(valid_app_id_prefixes)==0 or app_id.lower() in valid_app_id_prefixes:
            if containsIp and app_id in app_data_dict.keys():
                # add app server ip addresses (but do not add the whole app - it must already exist)
                app_server_ip = line[ip_col_name]
                if app_server_ip is not None and app_server_ip != "" and app_server_ip not in app_data_dict[app_id]['app_servers']:
                    if '/' in app_server_ip:
                        # calc start and end ip for subnet
                        pass
                    else:
                        app_data_dict[app_id]['app_servers'].append(
                            app_ip(ip=app_server_ip, ip_end=app_server_ip, type="host", name=f"host_{app_server_ip}")
                        )
                else:
                    # logger.debug(f'ignoring line from csv file: {appId} - empty IP')
                    countSkips += 1                    
            else:
                logger.debug(f'ignoring line from csv file: {app_id} - inactive?')
                countSkips += 1
        else:
            logger.info(f'ignoring line from csv file: {app_id} - inconclusive appId')
            countSkips += 1
    logger.info(f"{str(csv_filename)}: #total lines {str(len(ip_data))}, skipped: {str(countSkips)}")


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

    args = parser.parse_args()

    if args.suppress_certificate_warnings:
        requests.packages.urllib3.disable_warnings()

    logger = getLogger(debug_level_in=2)

    # read config
    ldapPath = readConfig(args.config, 'ldapPath')
    gitRepoUrl = readConfig(args.config, 'gitRepo')
    gitUsername = readConfig(args.config, 'gitUser')
    gitPassword = readConfig(args.config, 'gitpassword')
    csvAllOwnerFiles = readConfig(args.config, 'csvAllOwnerFiles')
    csvAppServerFiles = readConfig(args.config, 'csvAppServerFiles')

    #############################################
    # 1. get CSV files from github repo
    repoUrl = "https://" + gitUsername + ":" + gitPassword + "@" + gitRepoUrl
    if os.path.exists(repoTargetDir):
        # If the repository already exists, open it and perform a pull
        repo = git.Repo(repoTargetDir)
        origin = repo.remotes.origin
        origin.pull()
    else:
        repo = git.Repo.clone_from(repoUrl, repoTargetDir)

    #############################################
    # 2. get app data from CSV files
    appData = {}
    for csvFile in csvAllOwnerFiles:
        extract_app_data_from_csv(csvFile, appData, False)
    for csvFile in csvAppServerFiles:
        extract_ip_data_from_csv(csvFile, appData, True)

    owner_data = { "owners": [] } 

    for app_id in appData:
        if appData[app_id]['app_id_external'] != '':
            owner_data['owners'].append(
                {
                    "name": appData[app_id]['name'],
                    "app_id_external": appData[app_id]['app_id_external'],
                    "main_user": appData[app_id]['main_user'],
                    "modellers": appData[app_id]['modellers'],
                    "criticality": appData[app_id]['criticality'] if 'criticality' in appData[app_id] else None,
                    "import_source": appData[app_id]['import_source'],
                    "app_servers": appData[app_id]['app_servers'],
                    "recert_period_days": appData[app_id]['recert_period_days'] if 'recert_period_days' in appData[app_id] else 182
                }
            )
        else:
            logger.warning(f"App {app_id} has no external app id, skipping...")

    #############################################    
    # 3. write owners to json file
    path = os.path.dirname(__file__)
    fileOut = path + '/' + Path(os.path.basename(__file__)).stem + ".json"
    with open(fileOut, "w") as outFH:
        json.dump(owner_data, outFH, indent=3)
        
    #############################################    
    # 4. Some statistics
    logger.info(f"total #apps: {str(len(appData))}")
    appsWithIp = 0
    for app_id in appData:
        appsWithIp += 1 if len(appData[app_id]['app_servers']) > 0 else 0
    logger.info(f"#apps with ip addresses: {str(appsWithIp)}")
    totalIps = 0
    for app_id in appData:
        totalIps += len(appData[app_id]['app_servers'])
    logger.info(f"#ip addresses in total: {str(totalIps)}")

    sys.exit(0)

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


baseDir: str = "/usr/local/fworch/"
baseDirEtc: str = baseDir + "etc/"
repoTargetDir: str = baseDirEtc + "cmdb-repo"
defaultConfigFileName: str = baseDirEtc + "secrets/customizingConfig.json"
importSourceString: str = "tufinRlm" # change this to "cmdb-csv-export"? or will this break anything?


def build_dn(userId: str, ldapPath: str) -> str:
    dn: str = ""
    if len(userId)>0:
        if '{USERID}' in ldapPath:
            dn = ldapPath.replace('{USERID}', userId)
        else:
            logger.error("could not find {USERID} parameter in ldapPath " + ldapPath)
    return dn


# adds data from csv file to appData
# order of files in important: we only import apps which are included in files 3 and 4 (which only contain active apps)
# so first import files 3 and 4, then import files 1 and 2^
def extract_app_data_from_csv_file(csvFile: str, appData: dict[str, dict[str, Any]], containsIp: bool) -> None: 

    if containsIp:
        appNameColumn: int = 0
        appIdColumn: int = 2
        appOwnerBISOColumn: int = 4
        appOwnerTISOColumn: int = 5
        appServerIpColumn: int = 12
    else:
        appNameColumn = 0
        appIdColumn = 1
        appOwnerBISOColumn = 3
        appOwnerTISOColumn = 4
        appServerIpColumn: int | None = None

    appDataFromCsv: list[list[str]] = []
    csvFileName: str = repoTargetDir + '/' + csvFile # add directory to csv files

    # read csv file:
    try:
        with open(csvFileName, newline='', encoding="utf-8") as csvFileHandle:
            reader = csv.reader(csvFileHandle)
            appDataFromCsv += list(reader)[1:]# Skip headers in first line
    except Exception:
        logger.error("error while trying to read csv file '" + csvFileName + "', exception: " + str(traceback.format_exc()))
        sys.exit(1)

    countSkips: int = 0
    # append all owners from CSV
    for line in appDataFromCsv:
        appId: str = line[appIdColumn]
        if appId.lower().startswith('app-') or appId.lower().startswith('com-'):
            appName: str = line[appNameColumn]
            appMainUser: str = line[appOwnerTISOColumn]
            mainUserDn: str = build_dn(appMainUser, ldapPath)
            bisoDn: str = build_dn(line[appOwnerBISOColumn], ldapPath)
            if mainUserDn=='':
                logger.warning('adding app without main user: ' + appId)
            if appId not in appData.keys() and not containsIp:
                # only add app if it is in file 3 or 4
                appData.update({appId: {
                    "app_id_external": appId,
                    "name": appName,
                    "main_user": mainUserDn,
                    "BISO": bisoDn,
                    "modellers": [],
                    "import_source": importSourceString,
                    "app_servers": [],
                } })
            elif containsIp and appId in appData.keys():
                # add app server ip addresses (but do not add the whole app - it must already exist)
                if appServerIpColumn is None:
                    countSkips += 1
                    continue
                appServerIp: str = line[appServerIpColumn]
                if appServerIp is not None and appServerIp != "" and appServerIp not in appData[appId]['app_servers']:
                    appData[appId]['app_servers'].append({
                        "ip": appServerIp,
                        "ip_end": appServerIp,
                        "type": "host",
                        "name": f"host_{appServerIp}"
                    })
                else:
                    # logger.debug(f'ignoring line from csv file: {appId} - empty IP')
                    countSkips += 1                    
            else:
                logger.debug(f'ignoring line from csv file: {appId} - inactive?')
                countSkips += 1
        else:
            logger.info(f'ignoring line from csv file: {appId} - inconclusive appId')
            countSkips += 1
    logger.info(f"{str(csvFileName)}: #total lines {str(len(appDataFromCsv))}, skipped: {str(countSkips)}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description='Read configuration from FW management via API calls')
    parser.add_argument('-c', '--config', default=defaultConfigFileName,
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
    ldapPath: str = read_custom_config(args.config, 'ldapPath', logger=logger)
    gitRepoUrl: str = read_custom_config(args.config, 'ipamGitRepo', logger=logger)
    gitUsername: str = read_custom_config(args.config, 'ipamGitUser', logger=logger)
    gitPassword: str = read_custom_config(args.config, 'gitpassword', logger=logger)
    csvAllOwnerFiles: list[str] = read_custom_config(args.config, 'csvAllOwnerFiles', logger=logger)
    csvAppServerFiles: list[str] = read_custom_config(args.config, 'csvAppServerFiles', logger=logger)

    #############################################
    # 1. get CSV files from github repo
    repoUrl: str = "https://" + gitUsername + ":" + gitPassword + "@" + gitRepoUrl
    if os.path.exists(repoTargetDir):
        # If the repository already exists, open it and perform a pull
        repo: git.Repo = git.Repo(repoTargetDir)
        origin: git.Remote = repo.remotes.origin
        origin.pull()
    else:
        repo = git.Repo.clone_from(repoUrl, repoTargetDir)

    #############################################
    # 2. get app data from CSV files
    appData: dict[str, dict[str, Any]] = {}
    csvFile: str
    for csvFile in csvAllOwnerFiles:
        extract_app_data_from_csv_file(csvFile, appData, False)
    for csvFile in csvAppServerFiles:
        extract_app_data_from_csv_file(csvFile, appData, True)

    owner_data: dict[str, list[dict[str, Any]]] = { "owners": [] } 

    appId: str
    for appId in appData:
        if appData[appId]['app_id_external'] != '':
            owner_data['owners'].append({
                "name": appData[appId]['name'],
                "app_id_external": appData[appId]['app_id_external'],
                "main_user": appData[appId]['main_user'],
                "modellers": appData[appId]['modellers'],
                "criticality": appData[appId]['criticality'] if 'criticality' in appData[appId] else None,
                "import_source": appData[appId]['import_source'],
                "app_servers": appData[appId]['app_servers']
            })
        else:
            logger.warning(f"App {appId} has no external app id, skipping...")

    #############################################    
    # 3. write owners to json file
    path: str = os.path.dirname(__file__)
    fileOut: str = path + '/' + Path(os.path.basename(__file__)).stem + ".json"
    with open(fileOut, "w", encoding="utf-8") as outFH:
        json.dump(owner_data, outFH, indent=3)
        
    #############################################    
    # 4. Some statistics
    logger.info(f"total #apps: {str(len(appData))}")
    appsWithIp: int = 0
    for appId in appData:
        appsWithIp += 1 if len(appData[appId]['app_servers']) > 0 else 0
    logger.info(f"#apps with ip addresses: {str(appsWithIp)}")
    totalIps: int = 0
    for appId in appData:
        totalIps += len(appData[appId]['app_servers'])
    logger.info(f"#ip addresses in total: {str(totalIps)}")

    sys.exit(0)

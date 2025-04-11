#!/usr/bin/python3
# reads the main app data from multiple csv files contained in a git repo
# users will reside in ldap groups with standardized names
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


baseDir = "/usr/local/fworch/"
baseDirEtc = baseDir + "etc/"
repoTargetDir = baseDirEtc + "cmdb-repo"
defaultConfigFileName = baseDirEtc + "secrets/customizingConfig.json"
importSourceString = "tufinRlm" # change this to "cmdb-csv-export"? or will this break anything?

class ApiLoginFailed(Exception):
    """Raised when login to API failed"""

    def __init__(self, message="Login to API failed"):
            self.message = message
            super().__init__(self.message)

class ApiFailure(Exception):
    """Raised for any other Api call exceptions"""

    def __init__(self, message="There was an unclassified error while executing an API call"):
            self.message = message
            super().__init__(self.message)

class ApiTimeout(Exception):
    """Raised for 502 http error with proxy due to timeout"""

    def __init__(self, message="reverse proxy timeout error during API call - try increasing the reverse proxy timeout"):
            self.message = message
            super().__init__(self.message)

class ApiServiceUnavailable(Exception):
    """Raised for 503 http error Service unavailable"""

    def __init__(self, message="API unavailable"):
            self.message = message
            super().__init__(self.message)


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


# adds data from csv file to appData
# order of files in important: we only import apps which are included in files 3 and 4 (which only contain active apps)
# so first import files 3 and 4, then import files 1 and 2^
def extractAppDataFromCsvFile(csvFile: str, appData: dict, containsIp: bool): 

    if containsIp:
        appNameColumn = 0
        appIdColumn = 2
        appOwnerBISOColumn = 4
        appOwnerTISOColumn = 5
        appServerIpColumn = 12
    else:
        appNameColumn = 0
        appIdColumn = 1
        appOwnerBISOColumn = 3
        appOwnerTISOColumn = 4
        appServerIpColumn = None

    appDataFromCsv = []
    csvFile = repoTargetDir + '/' + csvFile # add directory to csv files

    # read csv file:
    try:
        with open(csvFile, newline='') as csvFile:
            reader = csv.reader(csvFile)
            appDataFromCsv += list(reader)[1:]# Skip headers in first line
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
                appServerIp = line[appServerIpColumn]
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
    logger.info(f"{str(csvFile.name)}: #total lines {str(len(appDataFromCsv))}, skipped: {str(countSkips)}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description='Read configuration from FW management via API calls')
    parser.add_argument('-c', '--config', default=defaultConfigFileName,
                        help='Filename of custom config file for modelling imports')
    parser.add_argument('-s', "--suppress_certificate_warnings", action='store_true', default = True,
                        help = "suppress certificate warnings")
    parser.add_argument('-l', '--limit', metavar='api_limit', default='150',
                        help='The maximal number of returned results per HTTPS Connection; default=50')

    args = parser.parse_args()

    if args.suppress_certificate_warnings:
        requests.packages.urllib3.disable_warnings()

    logger = getLogger(debug_level_in=2)

    # read config
    ldapPath = readConfig(args.config, 'ldapPath')
    gitRepoUrl = readConfig(args.config, 'ipamGitRepo')
    gitUsername = readConfig(args.config, 'ipamGitUser')
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
        extractAppDataFromCsvFile(csvFile, appData, False)
    for csvFile in csvAppServerFiles:
        extractAppDataFromCsvFile(csvFile, appData, True)

    #############################################    
    # 3. write owners to json file
    path = os.path.dirname(__file__)
    fileOut = path + '/' + Path(os.path.basename(__file__)).stem + ".json"
    with open(fileOut, "w") as outFH:
        json.dump(appData, outFH, indent=3)
        
    #############################################    
    # 4. Some statistics
    logger.info(f"total #apps: {str(len(appData))}")
    appsWithIp = 0
    for appId in appData:
        appsWithIp += 1 if len(appData[appId]['app_servers']) > 0 else 0
    logger.info(f"#apps with ip addresses: {str(appsWithIp)}")
    totalIps = 0
    for appId in appData:
        totalIps += len(appData[appId]['app_servers'])
    logger.info(f"#ip addresses in total: {str(totalIps)}")

    sys.exit(0)

#!/usr/bin/python3
# reads the main app data from a git repo
# and renriches the data with csv files containing users and server ip addresses

# dependencies: 
#   a) package python3-git must be installed
#   b) requires the following config items in /usr/local/fworch/etc/secrets/customizingConfig.json
#       Tufin RLM
#           username
#           password
#           apiBaseUri      # Tufin API, e.g. "https://tufin.domain.com/"
#           rlmVersion      # Tufin RLM Version (API breaking change in 2.6)
#       git
#           gitRepoUrl
#           gitusername
#           gitpassword
#       csvFiles # array of file basenames containing the app data
#       ldapPath # full ldap user path (used for building DN from user basename)

from asyncio.log import logger
import traceback
from textwrap import indent
import requests.packages
import requests
import json
import sys
import argparse
import logging
from sys import stdout
import ipaddress
import os
import socket
from pathlib import Path
import git  # apt install python3-git # or: pip install git
import csv


baseDir = "/usr/local/fworch/"
baseDirEtc = baseDir + "etc/"
repoTargetDir = baseDirEtc + "cmdb-repo"
defaultConfigFileName = baseDirEtc + "secrets/customizingConfig.json"
defaultRlmImportFileName = baseDirEtc + "getOwnersFromTufinRlm.json"
importSourceString = "tufinRlm"

# TUFIN settings:
api_url_path_rlm_login = 'apps/public/rlm/oauth/token'
api_url_path_rlm_apps = 'apps/public/rlm/api/owners'

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

    except:
        logger.error("could not read key '" + keyToGet + "' from config file " + configFilename + ", Exception: " + str(traceback.format_exc()))
        sys.exit(1)

# read owners from json file on disk which where imported from RLM
def getExistingOwnerIds(ownersIn):
    rlmOwners = []
    # convert owners into list of owner ids
    for o in ownersIn:
        if 'app_id_external' in o and not o['app_id_external'] in rlmOwners:
            rlmOwners.append(o['app_id_external'])
    return rlmOwners


def buildDN(userId, ldapPath):
    dn = ""
    if len(userId)>0:
        if '{USERID}' in ldapPath:
            dn = ldapPath.replace('{USERID}', userId)
        else:
            logger.error("could not find {USERID} parameter in ldapPath " + ldapPath)
    return dn


def getNetworkBorders(ip):
    if '/' in ip:
        network = ipaddress.IPv4Network(ip, strict=False)
        return str(network.network_address), str(network.broadcast_address), 'network'
    else:
        return str(ip), str(ip), 'host'


def reverse_dns_lookup(ip_address):
    """
    Perform a reverse DNS lookup to find the domain name associated with an IP address.

    Args:
    ip_address (str): The IP address to perform the reverse DNS lookup on.

    Returns:
    str: The domain name associated with the IP address or an error message if the lookup fails.
    """
    try:
        # Perform the reverse DNS lookup using the gethostbyaddr method of the socket module.
        # This method returns a tuple containing the primary domain name, an alias list, and an IP address list.
        hostname, _, _ = socket.gethostbyaddr(ip_address)

        # Return the primary domain name.
        return hostname
    except socket.herror as e:
        # Handle the exception if the host could not be found (herror).
        # Return an error message with the exception details.
        return f"ERROR: Reverse DNS lookup failed: {e}"
    except socket.gaierror as e:
        # Handle the exception if the address-related error occurs (gaierror).
        # Return an error message with the exception details.
        return f"ERROR: Address-related error during reverse DNS lookup: {e}"
    except Exception as e:
        # Handle any other exceptions that may occur.
        # Return a generic error message with the exception details.
        return f"ERROR: during reverse DNS lookup: {e}"


def extractSocketInfo(asset, services):
    # ignoring services for the moment
    sockets = []

    # dealing with plain ip addresses
    if 'assets' in asset and 'values' in asset['assets']:
        for ip in asset['assets']['values']:
            ip1, ip2, nwtype = getNetworkBorders(ip)
            
            assetName = ''  # default value = no name, leave empty, this needs to be handled in middleware app importer
            # find out name of asset
            if nwtype=='host':
                resolvedAssetName = reverse_dns_lookup(ip1)
                if not resolvedAssetName.startswith('ERROR:'):
                    # logger.debug("found resolved host " + assetName + ": " + ip1)
                    assetName = resolvedAssetName
                else:
                    logger.warning("IP address could not be resolved: " + ip1)
            elif nwtype=='network':
                logger.debug("found network: " + ip1)
                assetName = "NET-"+ip1    # might add netmask
            # elif nwtype=='range':
            #     logger.warning("found range: " + ip1)
            #     assetName = "NET-"+ip1+"-"+ip2
            else:
                logger.warning("IP address could not be resolved: " + ip1)

            sockets.append({ "ip": ip1, "ip_end": ip2, "type": nwtype, "name": assetName  })

    # now dealing with firewall objects
    if 'objects' in asset:
        for obj in asset['objects']:
            if 'values' in obj:
                for cidr in obj['values']:
                    ip1, ip2, nwtype = getNetworkBorders(cidr)
                    sockets.append({ "name": obj['name'], "ip": ip1, "ip_end": ip2, "type": nwtype })
    return sockets


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


def rlmLogin(user, password, api_url):
    payload = { "username": user, "password": password, "client_id": "securechange", "client_secret": "123", "grant_type": "password" }

    with requests.Session() as session:
        session.verify = False
        try:
            response = session.post(api_url, payload)
        except requests.exceptions.RequestException:
            raise ApiFailure ("api: error during login to url: " + str(api_url) + " with user " + user) from None

        if response.text is not None and response.status_code==200:
            return json.loads(response.text)['access_token']
        else:
            raise ApiLoginFailed("RLM api: ERROR: did not receive an OAUTH token during login" + \
                            ", api_url: " + str(api_url) + \
                            ", status code: " + str(response))


def rlmGetOwners(token, api_url, rlmVersion=2.5):

    headers = {}

    if rlmVersion < 2.6:
        headers = {'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'}
    else:
        api_url += "?access_token=" + token

    with requests.Session() as session:
        session.verify = False
        try:
            response = session.get(api_url, headers=headers)

        except requests.exceptions.RequestException:
            raise ApiServiceUnavailable ("api: error while getting owners from url: " + str(api_url) + " with token " + token) from None

        if response.text is not None and response.status_code==200:
            # logger.info(str(response.text))
            return json.loads(response.text)
        else:
            raise ApiFailure("api: ERROR: could not get owners" + \
                            ", api_url: " + str(api_url) + \
                            ", status code: " + str(response))


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

    ownersById = {}

    if args.suppress_certificate_warnings:
        requests.packages.urllib3.disable_warnings()

    logger = getLogger(debug_level_in=2)

    # read config
    rlmUsername = readConfig(args.config, 'username')
    rlmPassword = readConfig(args.config, 'password')
    rlmApiUrl = readConfig(args.config, 'apiBaseUri')
    ldapPath = readConfig(args.config, 'ldapPath')
    gitRepoUrl = readConfig(args.config, 'ipamGitRepo')
    gitUsername = readConfig(args.config, 'ipamGitUser')
    gitPassword = readConfig(args.config, 'gitpassword')
    rlmVersion = readConfig(args.config, 'rlmVersion')
    csvFiles = readConfig(args.config, 'csvFiles')

    ######################################################
    # 1. get all owners
    # get cmdb repo
    repoUrl = "https://" + gitUsername + ":" + gitPassword + "@" + gitRepoUrl
    if os.path.exists(repoTargetDir):
        # If the repository already exists, open it and perform a pull
        repo = git.Repo(repoTargetDir)
        origin = repo.remotes.origin
        origin.pull()
    else:
        repo = git.Repo.clone_from(repoUrl, repoTargetDir)

    dfAllApps = []
    for csvFile in csvFiles:
        csvFile = repoTargetDir + '/' + csvFile # add directory to csv files

        try:
            with open(csvFile, newline='') as csvFile:
                reader = csv.reader(csvFile)
                dfAllApps += list(reader)[1:]# Skip headers in first line
        except:
            logger.error("error while trying to read csv file '" + csvFile + "', exception: " + str(traceback.format_exc()))
            sys.exit(1)

    logger.info("#total apps: " + str(len(dfAllApps)))

    # append all owners from CSV
    for owner in dfAllApps:
        appId = owner[1]
        appName = owner[0]
        appMainUser = owner[3]
        if appId not in ownersById.keys():
            if appId.lower().startswith('app-') or appId.lower().startswith('com-'):
                mainUserDn = buildDN(appMainUser, ldapPath)
                if mainUserDn=='':
                    logger.warning('adding app without main user: ' + appId)

                ownersById.update(
                    {
                    owner[1]:
                        {
                            "app_id_external": appId,
                            "name": appName,
                            "main_user": mainUserDn,
                            "modellers": [],
                            "import_source": importSourceString,
                            "app_servers": [],
                        }
                    }
                )

    ######################################################
    # 2. now add data from RLM (add. users, server data)

    if not rlmApiUrl.startswith("http"):
        # assuming config file instead of direct API access
        try:
            with open(rlmApiUrl, "r") as ownerDumpFH:
                ownerData = json.loads(ownerDumpFH.read())
        except:
            logger.error("error while trying to read owners from config file '" + rlmApiUrl + "', exception: " + str(traceback.format_exc()))
            sys.exit(1)
    else:
        # get App List directly from RLM via API
        try:
            oauthToken = rlmLogin(rlmUsername, rlmPassword, rlmApiUrl + api_url_path_rlm_login)
            # logger.debug("token for RLM: " + oauthToken)
            rlmOwnerData = rlmGetOwners(oauthToken, rlmApiUrl + api_url_path_rlm_apps, float(rlmVersion))

        except:
            logger.error("error while getting owner data from RLM API: " + str(traceback.format_exc()))
            sys.exit(1)

    for rlmOwner in rlmOwnerData['owners']:
        # collect modeller users
        users = []
        appId = rlmOwner['owner']['name']
        for uid in rlmOwner['owner']['members']:
            dn = buildDN(uid, ldapPath)
            if appId in ownersById:
                if not dn == ownersById[appId]["main_user"]:  # leave out main owner
                    users.append(dn)

        # enrich modeller users and servers
        if appId in ownersById:
            ownersById[appId]['modellers'] += users
            ownersById[appId]['app_servers'] += extractSocketInfo(rlmOwner['asset'], rlmOwner['services'])
        else:
            logger.info('ignorning (inactive) app-id from RLM which is not in main app export: ' + appId)

    # 3. convert to normalized struct
    normOwners = { "owners": [] }
    for o in ownersById:
        normOwners['owners'].append(ownersById[o])

    ###################################################################################################
    # 4. write owners to json file

    path = os.path.dirname(__file__)
    fileOut = path + '/' + Path(os.path.basename(__file__)).stem + ".json"
    logger.info("dumping into file " + fileOut)

    with open(fileOut, "w") as outFH:
        json.dump(normOwners, outFH, indent=3)
    sys.exit(0)

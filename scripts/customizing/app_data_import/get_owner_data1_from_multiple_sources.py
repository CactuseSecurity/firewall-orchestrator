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
import argparse
import csv
import ipaddress
import json
import logging
import os
import socket
from pathlib import Path
import sys
from typing import Any
import traceback
import urllib3

import git  # apt install python3-git # or: pip install git
import requests
from scripts.customizing.fwo_custom_lib.basic_helpers import read_custom_config, get_logger


baseDir: str = "/usr/local/fworch/"
baseDirEtc: str = baseDir + "etc/"
repoTargetDir: str = baseDirEtc + "cmdb-repo"
defaultConfigFileName: str = baseDirEtc + "secrets/customizingConfig.json"
defaultRlmImportFileName: str = baseDirEtc + "getOwnersFromTufinRlm.json"
importSourceString: str = "tufinRlm"

# TUFIN settings:
api_url_path_rlm_login: str = 'apps/public/rlm/oauth/token'
api_url_path_rlm_apps: str = 'apps/public/rlm/api/owners'

class ApiLoginFailed(Exception):
    """Raised when login to API failed"""

    def __init__(self, message: str = "Login to API failed") -> None:
            self.message = message
            super().__init__(self.message)

class ApiFailure(Exception):
    """Raised for any other Api call exceptions"""

    def __init__(self, message: str = "There was an unclassified error while executing an API call") -> None:
            self.message = message
            super().__init__(self.message)

class ApiTimeout(Exception):
    """Raised for 502 http error with proxy due to timeout"""

    def __init__(self, message: str = "reverse proxy timeout error during API call - try increasing the reverse proxy timeout") -> None:
            self.message = message
            super().__init__(self.message)

class ApiServiceUnavailable(Exception):
    """Raised for 503 http error Service unavailable"""

    def __init__(self, message: str = "API unavailable") -> None:
            self.message = message
            super().__init__(self.message)


# read owners from json file on disk which where imported from RLM
def getExistingOwnerIds(ownersIn: list[dict[str, Any]]) -> list[str]:
    rlmOwners: list[str] = []
    # convert owners into list of owner ids
    o: dict[str, Any]
    for o in ownersIn:
        if 'app_id_external' in o and not o['app_id_external'] in rlmOwners:
            rlmOwners.append(o['app_id_external'])
    return rlmOwners


def buildDN(userId: str, ldapPath: str) -> str:
    dn: str = ""
    if len(userId)>0:
        if '{USERID}' in ldapPath:
            dn = ldapPath.replace('{USERID}', userId)
        else:
            logger.error("could not find {USERID} parameter in ldapPath " + ldapPath)
    return dn


def getNetworkBorders(ip: str) -> tuple[str, str, str]:
    if '/' in ip:
        network: ipaddress.IPv4Network = ipaddress.IPv4Network(ip, strict=False)
        return str(network.network_address), str(network.broadcast_address), 'network'
    else:
        return str(ip), str(ip), 'host'


def reverse_dns_lookup(ip_address: str) -> str:
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


def extractSocketInfo(asset: dict[str, Any], services: list[Any]) -> list[dict[str, str]]:
    # ignoring services for the moment
    sockets: list[dict[str, str]] = []

    # dealing with plain ip addresses
    if 'assets' in asset and 'values' in asset['assets']:
        for ip in asset['assets']['values']:
            ip1, ip2, nwtype = getNetworkBorders(ip)
            
            assetName: str = ''  # default value = no name, leave empty, this needs to be handled in middleware app importer
            # find out name of asset
            if nwtype=='host':
                resolvedAssetName: str = reverse_dns_lookup(ip1)
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


def rlmLogin(user: str, password: str, api_url: str) -> str:
    payload: dict[str, str] = { "username": user, "password": password, "client_id": "securechange", "client_secret": "123", "grant_type": "password" }

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


def rlmGetOwners(token: str, api_url: str, rlmVersion: float = 2.5) -> dict[str, Any]:

    headers: dict[str, str] = {}

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

    args: argparse.Namespace = parser.parse_args()

    ownersById: dict[str, dict[str, Any]] = {}

    if args.suppress_certificate_warnings:
        urllib3.disable_warnings()

    logger: logging.Logger = get_logger(debug_level_in=2)
    rlmOwnerData: dict[str, list[dict[str, Any]]] = { "owners": [] }
    # read config
    rlmUsername: str = read_custom_config(args.config, 'username', logger=logger)
    rlmPassword: str = read_custom_config(args.config, 'password', logger=logger)
    rlmApiUrl: str = read_custom_config(args.config, 'apiBaseUri', logger=logger)
    ldapPath: str = read_custom_config(args.config, 'ldapPath', logger=logger)
    gitRepoUrl: str = read_custom_config(args.config, 'ipamGitRepo', logger=logger)
    gitUsername: str = read_custom_config(args.config, 'ipamGitUser', logger=logger)
    gitPassword: str = read_custom_config(args.config, 'gitpassword', logger=logger)
    rlmVersion: str = read_custom_config(args.config, 'rlmVersion', logger=logger)
    csvFiles: list[str] = read_custom_config(args.config, 'csvFiles', logger=logger)

    ######################################################
    # 1. get all owners
    # get cmdb repo
    repoUrl: str = "https://" + gitUsername + ":" + gitPassword + "@" + gitRepoUrl
    if os.path.exists(repoTargetDir):
        # If the repository already exists, open it and perform a pull
        repo: git.Repo = git.Repo(repoTargetDir)
        origin: git.Remote = repo.remotes.origin
        origin.pull()
    else:
        repo = git.Repo.clone_from(repoUrl, repoTargetDir)

    dfAllApps: list[list[str]] = []
    csvFile: str
    for csvFile in csvFiles:
        csvFile = repoTargetDir + '/' + csvFile # add directory to csv files

        try:
            with open(csvFile, newline='', encoding="utf-8") as csvFileHandle:
                reader = csv.reader(csvFileHandle)
                dfAllApps += list(reader)[1:]# Skip headers in first line
        except Exception:
            logger.error("error while trying to read csv file '" + csvFile + "', exception: " + str(traceback.format_exc()))
            sys.exit(1)

    logger.info("#total apps: " + str(len(dfAllApps)))

    # append all owners from CSV
    owner: list[str]
    for owner in dfAllApps:
        appId: str = owner[1]
        appName: str = owner[0]
        appMainUser: str = owner[3]
        if appId not in ownersById.keys():
            if appId.lower().startswith('app-') or appId.lower().startswith('com-'):
                mainUserDn: str = buildDN(appMainUser, ldapPath)
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
            with open(rlmApiUrl, "r", encoding="utf-8") as ownerDumpFH:
                ownerData: dict[str, Any] = json.loads(ownerDumpFH.read())
        except Exception:
            logger.error("error while trying to read owners from config file '" + rlmApiUrl + "', exception: " + str(traceback.format_exc()))
            sys.exit(1)
    else:
        # get app list directly from RLM via API
        try:
            oauthToken: str = rlmLogin(rlmUsername, rlmPassword, rlmApiUrl + api_url_path_rlm_login)
            # logger.debug("token for RLM: " + oauthToken)
            rlmOwnerData = rlmGetOwners(oauthToken, rlmApiUrl + api_url_path_rlm_apps, float(rlmVersion))

        except Exception:
            logger.error("error while getting owner data from RLM API: " + str(traceback.format_exc()))
            sys.exit(1)

    rlmOwner: dict[str, Any]
    for rlmOwner in rlmOwnerData['owners']:
        # collect modeller users
        users: list[str] = []
        appId: str = rlmOwner['owner']['name']
        uid: str
        for uid in rlmOwner['owner']['members']:
            dn: str = buildDN(uid, ldapPath)
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
    normOwners: dict[str, list[dict[str, Any]]] = { "owners": [] }
    o: str
    for o in ownersById:
        normOwners['owners'].append(ownersById[o])

    ###################################################################################################
    # 4. write owners to json file

    path: str = os.path.dirname(__file__)
    fileOut: str = path + '/' + Path(os.path.basename(__file__)).stem + ".json"
    logger.info("dumping into file " + fileOut)

    with open(fileOut, "w", encoding="utf-8") as outFH:
        json.dump(normOwners, outFH, indent=3)
    sys.exit(0)

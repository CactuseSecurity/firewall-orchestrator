#!/usr/bin/python3

# library for Tufin STRACK API calls
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
from pathlib import Path


api_url_path_rlm_login = 'apps/public/rlm/oauth/token'
api_url_path_rlm_apps = 'apps/public/rlm/api/owners'
defaultConfigFileName = "/usr/local/fworch/etc/secrets/customizingConfig.json"

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


def readConfig(configFilename):
    try:

        with open(configFilename, "r") as customConfigFH:
            customConfig = json.loads(customConfigFH.read())
        return (customConfig['username'], customConfig['password'], customConfig['ldapPath'], customConfig['apiBaseUri'])

    except:
        logger.error("could not read config file " + configFilename + ", Exception: " + str(traceback.format_exc()))
        sys.exit(1)


def buildDN(userId, ldapPath):
    if '{USERID}' in ldapPath:
        return ldapPath.replace('{USERID}', userId)
    else:
        logger.error("could not find {USERID} parameter in ldapPath " + ldapPath)
        sys.exit(1)


def getNetworkBorders(ip):
    if '/' in ip:
        network = ipaddress.IPv4Network(ip, strict=False)
        return str(network.network_address), str(network.broadcast_address), 'network'
    else:
        return str(ip), str(ip), 'host'


def extractSocketInfo(asset, services):
    # ignoring services for the moment
    sockets =[]

    if 'assets' in asset and 'values' in asset['assets']:
        for ip in asset['assets']['values']:
            ip1, ip2, nwtype = getNetworkBorders(ip)
            sockets.append({ "ip": ip1, "ip_end": ip2, "type": nwtype })
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

    logger = logging.getLogger() # use root logger
    logHandler = logging.StreamHandler(stream=stdout)
    logformat = "%(asctime)s [%(levelname)-5.5s] [%(filename)-10.10s:%(funcName)-10.10s:%(lineno)4d] %(message)s"
    logHandler.setLevel(llevel)
    handlers = [logHandler]
    logging.basicConfig(format=logformat, datefmt="%Y-%m-%dT%H:%M:%S%z", handlers=handlers, level=llevel)
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
        # if verify_certs is None:    # only for first Recert API call (getting info on cert verification)
        #     session.verify = False
        # else: 
        #     session.verify = verify_certs
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
                            ", ssl_verification: " + str(verify_certs) + \
                            ", status code: " + str(response))


def rlmGetOwners(token, api_url):

    headers = {'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'}

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
    owners = {}

    if args.suppress_certificate_warnings:
        requests.packages.urllib3.disable_warnings()

    logger = getLogger(debug_level_in=2)

    # read config
    rlmUsername, rlmPassword, ldapPath, rlmApiUrl = readConfig(args.config)

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
            ownerData = rlmGetOwners(oauthToken, rlmApiUrl + api_url_path_rlm_apps)

        except:
            logger.error("error while getting owner data from RLM API: " + str(traceback.format_exc()))
            sys.exit(1)

    # normalizing owners config from Tufin RLM
    normOwners = { "owners": [] }
    for owner in ownerData['owners']:
        # logger.info("dealing with owner " + str(owner))

        users = []
        for uid in owner['owner']['members']:
            users.append(buildDN(uid, ldapPath))

        ownNorm = {
            "app_id_external": owner['owner']['name'],
            "name": owner['description'],
            "main_user": None,
            "modellers": users,
            "import_source": "tufinRlm",
            "app_servers": extractSocketInfo(owner['asset'], owner['services']),
        }
        normOwners['owners'].append(ownNorm)
    
    # logger.info("normOwners = " + json.dumps(normOwners, indent=3))
    path = os.path.dirname(__file__)
    fileOut = path + '/' + Path(os.path.basename(__file__)).stem + ".json"
    logger.info("dumping into file " + fileOut)
    with open(fileOut, "w") as outFH:
        json.dump(normOwners, outFH, indent=3)
    sys.exit(0)

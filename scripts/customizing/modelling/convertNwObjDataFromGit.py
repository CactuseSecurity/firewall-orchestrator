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
import git  # apt install python3-git # or: pip install git
import csv

defaultConfigFilename = "/usr/local/fworch/etc/secrets/customizingConfig.json"
ipamGitRepoTargetDir = "ipamRepo"


def getLogger(debug_level_in=0):
    debug_level=int(debug_level_in)
    if debug_level>=1:
        llevel = logging.DEBUG
    else:
        llevel = logging.INFO

    logger = logging.getLogger() # use root logger
    # logHandler = logging.StreamHandler(stream=stdout)
    logformat = "%(asctime)s [%(levelname)-5.5s] [%(filename)-10.10s:%(funcName)-10.10s:%(lineno)4d] %(message)s"
    # logHandler.setLevel(llevel)
    # handlers = [logHandler]
    # logging.basicConfig(format=logformat, datefmt="%Y-%m-%dT%H:%M:%S%z", handlers=handlers, level=llevel)
    logging.basicConfig(format=logformat, datefmt="%Y-%m-%dT%H:%M:%S%z", level=llevel)
    logger.setLevel(llevel)

    #set log level for noisy requests/connectionpool module to WARNING: 
    connection_log = logging.getLogger("urllib3.connectionpool")
    connection_log.setLevel(logging.WARNING)
    connection_log.propagate = True
    
    if debug_level>8:
        logger.debug ("debug_level=" + str(debug_level) )
    return logger


def readConfig(configFilename, keysToGet=['username', 'password', 'ldapPath', 'apiBaseUri']):
    try:

        with open(configFilename, "r") as customConfigFH:
            customConfig = json.loads(customConfigFH.read())

        configValues = []
        for key in keysToGet:
            configValues.append(customConfig[key])
        return configValues

    except:
        logger.error("could not read config file " + configFilename + ", Exception: " + str(traceback.format_exc()))
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
            sockets.append({ "ip": ip1, "ip-end": ip2, "type": nwtype })
    if 'objects' in asset:
        for obj in asset['objects']:
            if 'values' in obj:
                for cidr in obj['values']:
                    ip1, ip2, nwtype = getNetworkBorders(cidr)
                    sockets.append({ "ip": ip1, "ip-end": ip2, "type": nwtype })
    return sockets


def generatePublicIPv4NetworksAsInternetArea():
    internetSubnets = ['0.0.0.0/5', '8.0.0.0/7', '11.0.0.0/8', '12.0.0.0/6', '16.0.0.0/4', '32.0.0.0/3', '64.0.0.0/2', 
                       '128.0.0.0/3', '160.0.0.0/5', '168.0.0.0/6', '172.0.0.0/12', '172.32.0.0/11', '172.64.0.0/10', 
                       '172.128.0.0/9', '173.0.0.0/8', '174.0.0.0/7', '176.0.0.0/4', '192.0.0.0/9', '192.128.0.0/11', 
                       '192.160.0.0/13', '192.169.0.0/16', '192.170.0.0/15', '192.172.0.0/14', '192.176.0.0/12', 
                       '192.192.0.0/10', '193.0.0.0/8', '194.0.0.0/7', '196.0.0.0/6', '200.0.0.0/5', '208.0.0.0/4', 
                       '224.0.0.0/3']
    internetDicts = []
    for net in internetSubnets:
        internetDicts.append({'ip': net, 'name': 'inet'})
    return internetDicts


if __name__ == "__main__": 
    parser = argparse.ArgumentParser(
        description='Read configuration from FW management via API calls')
    parser.add_argument('-c', '--config', default=defaultConfigFilename,
                        help='Filename of custom config file for modelling imports')
    parser.add_argument('-l', '--limit', metavar='api_limit', default='150',
                        help='The maximal number of returned results per HTTPS Connection; default=50')

    args = parser.parse_args()
    subnets = []

    logger = getLogger(debug_level_in=2)

    # read config
    subnetDataFilename = ipamGitRepoTargetDir + '/' + readConfig(args.config, ['subnetData'])[0]
    ipamGitRepo = readConfig(args.config, ['ipamGitRepo'])[0]
    ipamGitUser = readConfig(args.config, ['ipamGitUser'])[0]
    ipamGitPassword = readConfig(args.config, ['ipamGitPassword'])[0]

    try:
        # get cmdb repo
        if os.path.exists(ipamGitRepoTargetDir):
            # If the repository already exists, open it and perform a pull
            repo = git.Repo(ipamGitRepoTargetDir)
            origin = repo.remotes.origin
            origin.pull()
        else:
            repoUrl = "https://" + ipamGitUser + ":" + ipamGitPassword + "@" + ipamGitRepo
            repo = git.Repo.clone_from(repoUrl, ipamGitRepoTargetDir)
    except:
        logger.error("error while trying to access git repo '" + ipamGitRepo + "', exception: " + str(traceback.format_exc()))
        sys.exit(1)

    # normalizing subnet data

    subnetAr = []

    try:
        with open(subnetDataFilename, 'r') as file:
            csv_reader = csv.DictReader(file)
            for row in csv_reader:
                subnetAr.append(row)
    except:
        logger.error("error while trying to read subnet csv file '" + subnetDataFilename + "', exception: " + str(traceback.format_exc()))
        sys.exit(1)

    normSubnetData = { "subnets": {}, "zones": {}, "areas": {} }
    snId = 0

    for subnet in subnetAr:
        # ignore all "reserved" subnets whose name starts with "RES"
        if not subnet['Subnetzname'].startswith('RES'):
            naId = subnet['Subnetzname'][2:4]
            subnetIp = subnet['Subnetzadresse']
            netmask = subnet['Subnetzmaske']
            cidr = str(ipaddress.ip_network(subnetIp + '/' + netmask))
            
            nameParts = subnet['Subnetzname'].split('.')
            if len(nameParts)>1:
                zoneName = nameParts[1]
                if len(nameParts)>=3:
                    subnetName = nameParts[2]
                else:
                    subnetName = ""
            else:
                logger.warning("ignoring malformed network entry for net " + subnet['Subnetzadresse'] + ", subnetname: " + subnet['Subnetzname'])
                continue

            zoneNamePartsDots = nameParts[0].split('.')

            zoneNamePartsUnderscore = zoneNamePartsDots[0].split('_')
            zoneId = zoneNamePartsUnderscore[0][2:7]
            areaName = '_'.join(zoneNamePartsUnderscore[1:])
            normSubnet = {
                "na-id": naId,
                "na-name": areaName,
                "zone-id": zoneId,
                "zone-name": zoneName,
                "ip": cidr,
                "name": subnetName
            }
            normSubnetData['subnets'].update({ snId: normSubnet})
            snId += 1;

            # filling areas
            if not naId in normSubnetData['areas']:
                normSubnetData['areas'].update({ naId: {"area-name": areaName, "area-id": naId, "subnets": [], "zones": [] }})
            normSubnetData['areas'][naId]['subnets'].append({"ip": cidr, "name": subnetName })
            normSubnetData['areas'][naId]['zones'].append({"zone-id": zoneId, "zone-name": zoneName })

            # filling zones
            if not zoneId in normSubnetData['zones']:
                normSubnetData['zones'].update({ zoneId: { "zone-name": zoneName, "subnets": [] }})
            normSubnetData['zones'][zoneId]['subnets'].append({"ip": cidr, "name": subnetName })

    # transform output
    transfSubnetData = { "areas": [] }
    for area in normSubnetData['areas'].values():
        areaIdString = "NA" + area['area-id']
        areaName = area['area-name']
        transfarea = { "name": areaName, "id_string": areaIdString, "subnets": area['subnets'] }
        transfSubnetData['areas'].append(transfarea)

    # add Internet as NA00_Internet
    transfSubnetData['areas'].append( {
        'name': 'Internet',
        'id_string': 'NA00',
        'subnets': generatePublicIPv4NetworksAsInternetArea() } )        
    # open: what about ipv6 addresses?
    # open: what about the companies own public ip addresses - should they be excluded here?

    path = os.path.dirname(__file__)
    fileOut = path + '/' + Path(os.path.basename(__file__)).stem + ".json"
    logger.info("dumping into file " + fileOut)
    with open(fileOut, "w") as outFH:
        json.dump(transfSubnetData, outFH, indent=3)
    sys.exit(0)

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
import csv

defaultConfigFilename = "/usr/local/fworch/etc/secrets/customizingConfig.json"


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
    subnetDataFilename = readConfig(args.config, ['subnetData'])[0]

    try:
        with open(subnetDataFilename, "r") as subnetFH:
            subnets = (subnetFH.readlines())
    except:
        logger.error("error while trying to read subnets from csv file '" + subnetDataFilename + "', exception: " + str(traceback.format_exc()))
        sys.exit(1)

    # normalizing subnet data

    subnetAr = []
    with open(subnetDataFilename, 'r') as file:
        csv_reader = csv.DictReader(file)
        for row in csv_reader:
            subnetAr.append(row)

    normSubnetData = { "subnets": {}, "zones": {}, "areas": {} }
    snId = 0
    for subnet in subnetAr:
        naId = subnet['Subnetzname'][2:4]
        subnetIp = subnet['Subnetzadresse']
        netmask = subnet['Subnetzmaske']
        cidr = str(ipaddress.ip_network(subnetIp + '/' + netmask))
        
        nameParts = subnet['Subnetzname'].split('.')
        zoneName = nameParts[1]
        if len(nameParts)>=3:
            subnetName = nameParts[2]
        else:
            subnetName = ""

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

    path = os.path.dirname(__file__)
    fileOut = path + '/' + Path(os.path.basename(__file__)).stem + ".json"
    logger.info("dumping into file " + fileOut)
    with open(fileOut, "w") as outFH:
        json.dump(transfSubnetData, outFH, indent=3)
    sys.exit(0)

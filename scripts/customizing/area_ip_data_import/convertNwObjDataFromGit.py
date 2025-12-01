#!/usr/bin/python3

# library for Tufin STRACK API calls
from asyncio.log import logger
import traceback
import json
import sys
import argparse
import ipaddress
import os
from pathlib import Path
import git  # apt install python3-git # or: pip install git
import csv
import urllib.parse
from scripts.customizing.fwo_custom_lib.basic_helpers import read_custom_config, get_logger

default_config_filename = "/usr/local/fworch/etc/secrets/customizingConfig.json"
ipam_git_repo_target_dir = "/usr/local/fworch/etc/ipamRepo"


def get_network_borders(ip_addr):
    if '/' in ip_addr:
        network = ipaddress.IPv4Network(ip_addr, strict=False)
        return str(network.network_address), str(network.broadcast_address), 'network'
    else:
        return str(ip_addr), str(ip_addr), 'host'


def extract_socket_info(asset, services):
    # ignoring services for the moment
    sockets = []

    if 'assets' in asset and 'values' in asset['assets']:
        for ip_addr in asset['assets']['values']:
            ip_start, ip_end, nw_type = get_network_borders(ip_addr)
            sockets.append({ "ip": ip_start, "ip-end": ip_end, "type": nw_type })
    if 'objects' in asset:
        for obj in asset['objects']:
            if 'values' in obj:
                for cidr in obj['values']:
                    ip_start, ip_end, nw_type = get_network_borders(cidr)
                    sockets.append({ "ip": ip_start, "ip-end": ip_end, "type": nw_type })
    return sockets


def generate_public_ipv4_networks_as_internet_area():
    internet_subnets = ['0.0.0.0/5', '8.0.0.0/7', '11.0.0.0/8', '12.0.0.0/6', '16.0.0.0/4', '32.0.0.0/3', '64.0.0.0/2',
                        '128.0.0.0/3', '160.0.0.0/5', '168.0.0.0/6', '172.0.0.0/12', '172.32.0.0/11', '172.64.0.0/10',
                        '172.128.0.0/9', '173.0.0.0/8', '174.0.0.0/7', '176.0.0.0/4', '192.0.0.0/9', '192.128.0.0/11',
                        '192.160.0.0/13', '192.169.0.0/16', '192.170.0.0/15', '192.172.0.0/14', '192.176.0.0/12',
                        '192.192.0.0/10', '193.0.0.0/8', '194.0.0.0/7', '196.0.0.0/6', '200.0.0.0/5', '208.0.0.0/4',
                        '224.0.0.0/3']
    internet_dicts = []
    for net in internet_subnets:
        internet_dicts.append({'ip': net, 'name': 'inet'})
    return internet_dicts


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description='Read configuration from FW management via API calls')
    parser.add_argument('-c', '--config', default=default_config_filename,
                        help='Filename of custom config file for modelling imports')
    parser.add_argument('-l', '--limit', metavar='api_limit', default='150',
                        help='The maximal number of returned results per HTTPS Connection; default=50')

    args = parser.parse_args()
    subnets = []

    logger = get_logger(debug_level_in=2)

    # read config
    subnet_data_filename = ipam_git_repo_target_dir + '/' + read_custom_config(args.config, ['subnetData'], logger=logger)[0]
    ipam_git_repo = read_custom_config(args.config, ['ipamGitRepo'], logger=logger)[0]
    ipam_git_user = read_custom_config(args.config, ['ipamGitUser'], logger=logger)[0]
    ipam_git_password = read_custom_config(args.config, ['ipamGitPassword'], logger=logger)[0]

    try:
        # get ipam repo
        if os.path.exists(ipam_git_repo_target_dir):
            # If the repository already exists, open it and perform a pull
            repo = git.Repo(ipam_git_repo_target_dir)
            origin = repo.remotes.origin
            origin.pull()
        else:
            repo_url = "https://" + ipam_git_user + ":" + urllib.parse.quote(ipam_git_password, safe='') + "@" + ipam_git_repo
            repo = git.Repo.clone_from(repo_url, ipam_git_repo_target_dir)
    except:
        logger.error("error while trying to access git repo '" + ipam_git_repo + "', exception: " + str(traceback.format_exc()))
        sys.exit(1)

    # normalizing subnet data

    subnet_ar = []

    try:
        with open(subnet_data_filename, newline='') as csv_file:
            reader = csv.reader(csv_file)
            subnet_ar += list(reader)
    except:
        logger.error("error while trying to read csv file '" + subnet_data_filename + "', exception: " + str(traceback.format_exc()))
        sys.exit(1)

    norm_subnet_data = { "subnets": {}, "zones": {}, "areas": {} }
    sn_id = 0

    name_column = 3
    ip_column = 0
    mask_column = 1
    max_column = name_column
    line = 0
    for subnet in subnet_ar:
        line += 1
        # ignore all "reserved" subnets whose name starts with "RES"
        if len(subnet) < max_column + 1:
            logger.warning("line " + str(line) + ": ignoring malformed subnet " + str(subnet))
        else:
            if not subnet[name_column].startswith('RES'):
                na_id = subnet[name_column][2:4]
                subnet_ip = subnet[ip_column]
                netmask = subnet[mask_column]
                try:
                    cidr = str(ipaddress.ip_network(subnet_ip + '/' + netmask))
                except ValueError:
                    logger.warning('found line with unparsable IP: ' + subnet_ip + '/' + netmask)
                    continue

                name_parts = subnet[name_column].split('.')
                if len(name_parts) > 1:
                    zone_name = name_parts[1]
                    if len(name_parts) >= 3:
                        subnet_name = name_parts[2]
                    else:
                        subnet_name = ""
                else:
                    logger.warning("line " + str(line) + ": ignoring malformed network entry for net " + subnet[ip_column] + ", subnetname: " + subnet[name_column])
                    continue

                zone_name_parts_dots = name_parts[0].split('.')

                zone_name_parts_underscore = zone_name_parts_dots[0].split('_')
                zone_id = zone_name_parts_underscore[0][2:7]
                area_name = '_'.join(zone_name_parts_underscore[1:])
                norm_subnet = {
                    "na-id": na_id,
                    "na-name": area_name,
                    "zone-id": zone_id,
                    "zone-name": zone_name,
                    "ip": cidr,
                    "name": subnet_name
                }
                norm_subnet_data['subnets'].update({ sn_id: norm_subnet})
                sn_id += 1

                # filling areas
                if not na_id in norm_subnet_data['areas']:
                    norm_subnet_data['areas'].update({ na_id: {"area-name": area_name, "area-id": na_id, "subnets": [], "zones": [] }})
                norm_subnet_data['areas'][na_id]['subnets'].append({"ip": cidr, "name": subnet_name })
                norm_subnet_data['areas'][na_id]['zones'].append({"zone-id": zone_id, "zone-name": zone_name })

                # filling zones
                if not zone_id in norm_subnet_data['zones']:
                    norm_subnet_data['zones'].update({ zone_id: { "zone-name": zone_name, "subnets": [] }})
                norm_subnet_data['zones'][zone_id]['subnets'].append({"ip": cidr, "name": subnet_name })

    # transform output
    transf_subnet_data = { "areas": [] }
    for area in norm_subnet_data['areas'].values():
        area_id_string = "NA" + area['area-id']
        area_name = area['area-name']
        transf_area = { "name": area_name, "id_string": area_id_string, "subnets": area['subnets'] }
        transf_subnet_data['areas'].append(transf_area)

    # add Internet as NA00_Internet
    transf_subnet_data['areas'].append( {
        'name': 'Internet',
        'id_string': 'NA00',
        'subnets': generate_public_ipv4_networks_as_internet_area() } )
    # open: what about ipv6 addresses?
    # open: what about the companies own public ip addresses - should they be excluded here?

    path = os.path.dirname(__file__)
    file_out = path + '/' + Path(os.path.basename(__file__)).stem + ".json"
    logger.info("dumping into file " + file_out)
    with open(file_out, "w") as out_fh:
        json.dump(transf_subnet_data, out_fh, indent=3)
    sys.exit(0)

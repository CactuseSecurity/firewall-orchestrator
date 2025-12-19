#!/usr/bin/python3

# library for Tufin STRACK API calls
import argparse
import csv
import ipaddress
import json
import logging
import sys
import urllib.parse
from asyncio.log import logger
from pathlib import Path
from typing import Any

import git  # apt install python3-git # or: pip install git

from scripts.customizing.fwo_custom_lib.basic_helpers import get_logger, read_custom_config

default_config_filename: str = "/usr/local/fworch/etc/secrets/customizingConfig.json"
ipam_git_repo_target_dir: str = "/usr/local/fworch/etc/ipamRepo"
SUBNET_NAME_PARTS_MIN_COUNT: int = 3
git_any: Any = git


def get_network_borders(ip_addr: str) -> tuple[str, str, str]:
    if "/" in ip_addr:
        network: ipaddress.IPv4Network = ipaddress.IPv4Network(ip_addr, strict=False)
        return str(network.network_address), str(network.broadcast_address), "network"
    return str(ip_addr), str(ip_addr), "host"


def extract_socket_info(asset: dict[str, Any], _services: list[Any]) -> list[dict[str, str]]:
    # ignoring services for the moment
    sockets: list[dict[str, str]] = []

    if "assets" in asset and "values" in asset["assets"]:
        for ip_addr in asset["assets"]["values"]:
            ip_start, ip_end, nw_type = get_network_borders(ip_addr)
            sockets.append({"ip": ip_start, "ip-end": ip_end, "type": nw_type})
    if "objects" in asset:
        for obj in asset["objects"]:
            if "values" in obj:
                for cidr in obj["values"]:
                    ip_start, ip_end, nw_type = get_network_borders(cidr)
                    sockets.append({"ip": ip_start, "ip-end": ip_end, "type": nw_type})
    return sockets


def generate_public_ipv4_networks_as_internet_area() -> list[dict[str, str]]:
    internet_subnets: list[str] = [
        "0.0.0.0/5",
        "8.0.0.0/7",
        "11.0.0.0/8",
        "12.0.0.0/6",
        "16.0.0.0/4",
        "32.0.0.0/3",
        "64.0.0.0/2",
        "128.0.0.0/3",
        "160.0.0.0/5",
        "168.0.0.0/6",
        "172.0.0.0/12",
        "172.32.0.0/11",
        "172.64.0.0/10",
        "172.128.0.0/9",
        "173.0.0.0/8",
        "174.0.0.0/7",
        "176.0.0.0/4",
        "192.0.0.0/9",
        "192.128.0.0/11",
        "192.160.0.0/13",
        "192.169.0.0/16",
        "192.170.0.0/15",
        "192.172.0.0/14",
        "192.176.0.0/12",
        "192.192.0.0/10",
        "193.0.0.0/8",
        "194.0.0.0/7",
        "196.0.0.0/6",
        "200.0.0.0/5",
        "208.0.0.0/4",
        "224.0.0.0/3",
    ]
    return [{"ip": net, "name": "inet"} for net in internet_subnets]


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Read configuration from FW management via API calls")
    parser.add_argument(
        "-c", "--config", default=default_config_filename, help="Filename of custom config file for modelling imports"
    )
    parser.add_argument(
        "-l",
        "--limit",
        metavar="api_limit",
        default="150",
        help="The maximal number of returned results per HTTPS Connection; default=50",
    )

    args: argparse.Namespace = parser.parse_args()
    subnets: list[Any] = []

    logger: logging.Logger = get_logger(debug_level_in=2)

    # read config
    subnet_data_filename: str = (
        ipam_git_repo_target_dir + "/" + read_custom_config(args.config, "subnetData", logger=logger)[0]
    )
    ipam_git_repo: str = read_custom_config(args.config, "ipamGitRepo", logger=logger)[0]
    ipam_git_user: str = read_custom_config(args.config, "ipamGitUser", logger=logger)[0]
    ipam_git_password: str = read_custom_config(args.config, "ipamGitPassword", logger=logger)[0]

    try:
        # get ipam repo
        if Path(ipam_git_repo_target_dir).exists():
            # If the repository already exists, open it and perform a pull
            repo: Any = git_any.Repo(ipam_git_repo_target_dir)
            origin: Any = repo.remotes.origin
            origin.pull()
        else:
            repo_url: str = (
                "https://" + ipam_git_user + ":" + urllib.parse.quote(ipam_git_password, safe="") + "@" + ipam_git_repo
            )
            repo = git_any.Repo.clone_from(repo_url, ipam_git_repo_target_dir)
    except Exception:
        logger.exception("error while trying to access git repo %s", ipam_git_repo)
        sys.exit(1)

    # normalizing subnet data

    subnet_ar: list[list[str]] = []

    try:
        with open(subnet_data_filename, newline="", encoding="utf-8") as csv_file:
            reader = csv.reader(csv_file)
            subnet_ar += list(reader)
    except Exception:
        logger.exception("error while trying to read csv file %s", subnet_data_filename)
        sys.exit(1)

    norm_subnet_data: dict[str, dict[str, Any]] = {"subnets": {}, "zones": {}, "areas": {}}
    sn_id: int = 0

    name_column: int = 3
    ip_column: int = 0
    mask_column: int = 1
    max_column: int = name_column
    line: int = 0
    subnet: list[str]
    for subnet in subnet_ar:
        line += 1
        # ignore all "reserved" subnets whose name starts with "RES"
        if len(subnet) < max_column + 1:
            logger.warning("line %s: ignoring malformed subnet %s", line, subnet)
        elif not subnet[name_column].startswith("RES"):
            na_id: str = subnet[name_column][2:4]
            subnet_ip: str = subnet[ip_column]
            netmask: str = subnet[mask_column]
            try:
                cidr: str = str(ipaddress.ip_network(subnet_ip + "/" + netmask))
            except ValueError:
                logger.warning("found line with unparsable IP: %s/%s", subnet_ip, netmask)
                continue

            name_parts: list[str] = subnet[name_column].split(".")
            if len(name_parts) > 1:
                zone_name: str = name_parts[1]
                if len(name_parts) >= SUBNET_NAME_PARTS_MIN_COUNT:
                    subnet_name: str = name_parts[2]
                else:
                    subnet_name = ""
            else:
                logger.warning(
                    "line %s: ignoring malformed network entry for net %s, subnetname: %s",
                    line,
                    subnet[ip_column],
                    subnet[name_column],
                )
                continue

            zone_name_parts_dots: list[str] = name_parts[0].split(".")

            zone_name_parts_underscore: list[str] = zone_name_parts_dots[0].split("_")
            zone_id: str = zone_name_parts_underscore[0][2:7]
            area_name: str = "_".join(zone_name_parts_underscore[1:])
            norm_subnet: dict[str, str] = {
                "na-id": na_id,
                "na-name": area_name,
                "zone-id": zone_id,
                "zone-name": zone_name,
                "ip": cidr,
                "name": subnet_name,
            }
            norm_subnet_data["subnets"].update({str(sn_id): norm_subnet})
            sn_id += 1

            # filling areas
            if na_id not in norm_subnet_data["areas"]:
                norm_subnet_data["areas"].update(
                    {na_id: {"area-name": area_name, "area-id": na_id, "subnets": [], "zones": []}}
                )
            norm_subnet_data["areas"][na_id]["subnets"].append({"ip": cidr, "name": subnet_name})
            norm_subnet_data["areas"][na_id]["zones"].append({"zone-id": zone_id, "zone-name": zone_name})

            # filling zones
            if zone_id not in norm_subnet_data["zones"]:
                norm_subnet_data["zones"].update({zone_id: {"zone-name": zone_name, "subnets": []}})
            norm_subnet_data["zones"][zone_id]["subnets"].append({"ip": cidr, "name": subnet_name})

    # transform output
    transf_subnet_data: dict[str, list[dict[str, Any]]] = {"areas": []}
    area: dict[str, Any]
    for area in norm_subnet_data["areas"].values():
        area_id_string: str = "NA" + area["area-id"]
        area_name: str = area["area-name"]
        transf_area: dict[str, Any] = {"name": area_name, "id_string": area_id_string, "subnets": area["subnets"]}
        transf_subnet_data["areas"].append(transf_area)

    # add Internet as NA00_Internet
    transf_subnet_data["areas"].append(
        {"name": "Internet", "id_string": "NA00", "subnets": generate_public_ipv4_networks_as_internet_area()}
    )
    # open: what about ipv6 addresses?
    # open: what about the companies own public ip addresses - should they be excluded here?

    file_out: Path = Path(__file__).with_suffix(".json")
    logger.info("dumping into file %s", file_out)
    with open(file_out, "w", encoding="utf-8") as out_fh:
        json.dump(transf_subnet_data, out_fh, indent=3)
    sys.exit(0)

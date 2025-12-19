#!/usr/bin/python3
# reads the main app data from a git repo
# and renriches the data with csv files containing users and server ip addresses

# dependencies:
#   a) package python3-git must be installed
#   b) requires the following config items in /usr/local/fworch/etc/secrets/customizingConfig.json
#       Tufin RLM
#           username
#           password
#           apiBaseUri      # Tufin API, e.g. "https://tufin.domain.com/"       #noqa: ERA001
#           rlmVersion      # Tufin RLM Version (API breaking change in 2.6)    #noqa: ERA001
#       git
#           gitRepoUrl
#           gitusername
#           gitpassword
#       csvFiles # array of file basenames containing the app data
#       ldapPath # full ldap user path (used for building DN from user basename) #noqa: ERA001

import argparse
import csv
import ipaddress
import json
import logging
import socket
import sys
from asyncio.log import logger
from pathlib import Path
from typing import Any

import git  # apt install python3-git # or: pip install git
import requests
import urllib3

from scripts.customizing.fwo_custom_lib.basic_helpers import get_logger, read_custom_config

base_dir: str = "/usr/local/fworch/"
base_dir_etc: str = base_dir + "etc/"
repo_target_dir: str = base_dir_etc + "cmdb-repo"
default_config_file_name: str = base_dir_etc + "secrets/customizingConfig.json"
default_rlm_import_file_name: str = base_dir_etc + "getOwnersFromTufinRlm.json"
import_source_string: str = "tufinRlm"
git_any: Any = git

# TUFIN settings:
api_url_path_rlm_login: str = "apps/public/rlm/oauth/token"
api_url_path_rlm_apps: str = "apps/public/rlm/api/owners"
HTTP_OK: int = 200
RLM_VERSION_BREAK: float = 2.6


class ApiLoginFailedError(Exception):
    """Raised when login to API failed"""

    def __init__(self, message: str = "Login to API failed") -> None:
        self.message = message
        super().__init__(self.message)


class ApiFailureError(Exception):
    """Raised for any other Api call exceptions"""

    def __init__(self, message: str = "There was an unclassified error while executing an API call") -> None:
        self.message = message
        super().__init__(self.message)


class ApiTimeoutError(Exception):
    """Raised for 502 http error with proxy due to timeout"""

    def __init__(
        self, message: str = "reverse proxy timeout error during API call - try increasing the reverse proxy timeout"
    ) -> None:
        self.message = message
        super().__init__(self.message)


class ApiServiceUnavailableError(Exception):
    """Raised for 503 http error Service unavailable"""

    def __init__(self, message: str = "API unavailable") -> None:
        self.message = message
        super().__init__(self.message)


# read owners from json file on disk which where imported from RLM
def get_existing_owner_ids(owners_in: list[dict[str, Any]]) -> list[str]:
    rlm_owners: list[str] = []
    # convert owners into list of owner ids
    o: dict[str, Any]
    for o in owners_in:
        if "app_id_external" in o and o["app_id_external"] not in rlm_owners:
            rlm_owners.append(o["app_id_external"])
    return rlm_owners


def build_dn(user_id: str, ldap_path: str) -> str:
    dn: str = ""
    if len(user_id) > 0:
        if "{USERID}" in ldap_path:
            dn = ldap_path.replace("{USERID}", user_id)
        else:
            logger.error("could not find {USERID} parameter in ldapPath %s", ldap_path)
    return dn


def get_network_borders(ip: str) -> tuple[str, str, str]:
    if "/" in ip:
        network: ipaddress.IPv4Network = ipaddress.IPv4Network(ip, strict=False)
        return str(network.network_address), str(network.broadcast_address), "network"
    return str(ip), str(ip), "host"


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


def _extract_plain_ip_sockets(asset: dict[str, Any]) -> list[dict[str, str]]:
    sockets: list[dict[str, str]] = []

    if "assets" in asset and "values" in asset["assets"]:
        for ip in asset["assets"]["values"]:
            ip1, ip2, nwtype = get_network_borders(ip)

            asset_name: str = (
                ""  # default value = no name, leave empty, this needs to be handled in middleware app importer
            )
            if nwtype == "host":
                resolved_asset_name: str = reverse_dns_lookup(ip1)
                if not resolved_asset_name.startswith("ERROR:"):
                    asset_name = resolved_asset_name
                else:
                    logger.warning("IP address could not be resolved: %s", ip1)
            elif nwtype == "network":
                logger.debug("found network: %s", ip1)
                asset_name = "NET-" + ip1  # might add netmask
            else:
                logger.warning("IP address could not be resolved: %s", ip1)

            sockets.append({"ip": ip1, "ip_end": ip2, "type": nwtype, "name": asset_name})

    return sockets


def _extract_object_sockets(asset: dict[str, Any]) -> list[dict[str, str]]:
    sockets: list[dict[str, str]] = []

    if "objects" in asset:
        for obj in asset["objects"]:
            if "values" in obj:
                for cidr in obj["values"]:
                    ip1, ip2, nwtype = get_network_borders(cidr)
                    sockets.append({"name": obj["name"], "ip": ip1, "ip_end": ip2, "type": nwtype})

    return sockets


def extract_socket_info(asset: dict[str, Any], _services: list[Any]) -> list[dict[str, str]]:
    # ignoring services for the moment
    sockets: list[dict[str, str]] = []
    sockets.extend(_extract_plain_ip_sockets(asset))
    sockets.extend(_extract_object_sockets(asset))
    return sockets


def rlm_login(user: str, password: str, api_url: str) -> str:
    payload: dict[str, str] = {
        "username": user,
        "password": password,
        "client_id": "securechange",
        "client_secret": "123",
        "grant_type": "password",
    }

    with requests.Session() as session:
        session.verify = False
        try:
            response = session.post(api_url, payload)
        except requests.exceptions.RequestException:
            raise ApiFailureError("api: error during login to url: " + str(api_url) + " with user " + user) from None

        if response.status_code == HTTP_OK:
            return json.loads(response.text)["access_token"]
        raise ApiLoginFailedError(
            "RLM api: ERROR: did not receive an OAUTH token during login"
            ", api_url: " + str(api_url) + ", status code: " + str(response)
        )


def rlm_get_owners(token: str, api_url: str, rlm_version: float = 2.5) -> dict[str, Any]:
    headers: dict[str, str] = {}

    if rlm_version < RLM_VERSION_BREAK:
        headers = {"Authorization": "Bearer " + token, "Content-Type": "application/json"}
    else:
        api_url += "?access_token=" + token

    with requests.Session() as session:
        session.verify = False
        try:
            response = session.get(api_url, headers=headers)

        except requests.exceptions.RequestException:
            raise ApiServiceUnavailableError(
                "api: error while getting owners from url: " + str(api_url) + " with token " + token
            ) from None

        if response.status_code == HTTP_OK:
            return json.loads(response.text)
        raise ApiFailureError(
            "api: ERROR: could not get owners, api_url: " + str(api_url) + ", status code: " + str(response)
        )


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Read configuration from FW management via API calls")
    parser.add_argument(
        "-c", "--config", default=default_config_file_name, help="Filename of custom config file for modelling imports"
    )
    parser.add_argument(
        "-s", "--suppress_certificate_warnings", action="store_true", default=True, help="suppress certificate warnings"
    )
    parser.add_argument(
        "-l",
        "--limit",
        metavar="api_limit",
        default="150",
        help="The maximal number of returned results per HTTPS Connection; default=50",
    )

    args: argparse.Namespace = parser.parse_args()

    owners_by_id: dict[str, dict[str, Any]] = {}

    if args.suppress_certificate_warnings:
        urllib3.disable_warnings()

    logger: logging.Logger = get_logger(debug_level_in=2)
    rlm_owner_data: dict[str, list[dict[str, Any]]] = {"owners": []}
    # read config
    rlm_username: str = read_custom_config(args.config, "username", logger=logger)
    rlm_password: str = read_custom_config(args.config, "password", logger=logger)
    rlm_api_url: str = read_custom_config(args.config, "apiBaseUri", logger=logger)
    ldap_path: str = read_custom_config(args.config, "ldapPath", logger=logger)
    git_repo_url: str = read_custom_config(args.config, "ipamGitRepo", logger=logger)
    git_username: str = read_custom_config(args.config, "ipamGitUser", logger=logger)
    git_password: str = read_custom_config(args.config, "gitpassword", logger=logger)
    rlm_version: str = read_custom_config(args.config, "rlmVersion", logger=logger)
    csv_files: list[str] = read_custom_config(args.config, "csvFiles", logger=logger)

    ######################################################
    # 1. get all owners
    # get cmdb repo
    repo_url: str = "https://" + git_username + ":" + git_password + "@" + git_repo_url
    if Path(repo_target_dir).exists():
        # If the repository already exists, open it and perform a pull
        repo: Any = git_any.Repo(repo_target_dir)
        origin: Any = repo.remotes.origin
        origin.pull()
    else:
        repo = git_any.Repo.clone_from(repo_url, repo_target_dir)

    df_all_apps: list[list[str]] = []
    csv_file: str
    for csv_file in csv_files:
        csv_file_path: str = repo_target_dir + "/" + csv_file  # add directory to csv files

        try:
            with open(csv_file_path, newline="", encoding="utf-8") as csv_file_handle:
                reader = csv.reader(csv_file_handle)
                df_all_apps += list(reader)[1:]  # Skip headers in first line
        except Exception:
            logger.exception("error while trying to read csv file %s", csv_file_path)
            sys.exit(1)

    logger.info("#total apps: %s", len(df_all_apps))

    # append all owners from CSV
    owner: list[str]
    for owner in df_all_apps:
        app_id: str = owner[1]
        app_name: str = owner[0]
        app_main_user: str = owner[3]
        if app_id not in owners_by_id and (app_id.lower().startswith("app-") or app_id.lower().startswith("com-")):
            main_user_dn: str = build_dn(app_main_user, ldap_path)
            if main_user_dn == "":
                logger.warning("adding app without main user: %s", app_id)

            owners_by_id.update(
                {
                    owner[1]: {
                        "app_id_external": app_id,
                        "name": app_name,
                        "main_user": main_user_dn,
                        "modellers": [],
                        "import_source": import_source_string,
                        "app_servers": [],
                    }
                }
            )

    ######################################################
    # 2. now add data from RLM (add. users, server data)

    if not rlm_api_url.startswith("http"):
        # assuming config file instead of direct API access
        try:
            with open(rlm_api_url, encoding="utf-8") as owner_dump_fh:
                owner_data: dict[str, Any] = json.loads(owner_dump_fh.read())
        except Exception:
            logger.exception("error while trying to read owners from config file %s", rlm_api_url)
            sys.exit(1)
    else:
        # get app list directly from RLM via API
        try:
            oauth_token: str = rlm_login(rlm_username, rlm_password, rlm_api_url + api_url_path_rlm_login)
            rlm_owner_data = rlm_get_owners(oauth_token, rlm_api_url + api_url_path_rlm_apps, float(rlm_version))

        except Exception:
            logger.exception("error while getting owner data from RLM API")
            sys.exit(1)

    rlm_owner: dict[str, Any]
    for rlm_owner in rlm_owner_data["owners"]:
        # collect modeller users
        users: list[str] = []
        app_id: str = rlm_owner["owner"]["name"]
        uid: str
        for uid in rlm_owner["owner"]["members"]:
            dn: str = build_dn(uid, ldap_path)
            if app_id in owners_by_id and dn != owners_by_id[app_id]["main_user"]:  # leave out main owner
                users.append(dn)

        # enrich modeller users and servers
        if app_id in owners_by_id:
            owners_by_id[app_id]["modellers"] += users
            owners_by_id[app_id]["app_servers"] += extract_socket_info(rlm_owner["asset"], rlm_owner["services"])
        else:
            logger.info("ignorning (inactive) app-id from RLM which is not in main app export: %s", app_id)

    # 3. convert to normalized struct
    owners_list: list[dict[str, Any]] = list(owners_by_id.values())
    norm_owners: dict[str, list[dict[str, Any]]] = {"owners": owners_list}

    ###################################################################################################
    # 4. write owners to json file

    file_out: Path = Path(__file__).with_suffix(".json")
    logger.info("dumping into file %s", file_out)

    with open(file_out, "w", encoding="utf-8") as out_fh:
        json.dump(norm_owners, out_fh, indent=3)
    sys.exit(0)

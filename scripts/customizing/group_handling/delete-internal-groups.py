#!/usr/bin/python3
# This script deletes internal groups in FWO
import argparse
import functools
import json
import logging
import sys
from argparse import ArgumentParser
from enum import Enum
from typing import Any

from requests import Session, exceptions

DEFAULT_API_URL: str = "https://localhost:8888/api/"
FWO_CONFIG_FILE: str = "/etc/fworch/fworch.json"
HTTP_OK: int = 200
# (connect, read) timeout tuple so a stalled endpoint cannot hang the group-handling run
HTTP_TIMEOUT: tuple[int, int] = (60, 14400)


@functools.lru_cache(maxsize=1)
def read_tls_identity(fwo_config_filename: str) -> tuple[tuple[str, str] | None, str | bool]:
    """
    Return the local FWO client certificate pair and the internal CA bundle.

    FWO endpoints present certificates from an internal CA that neither the system
    store nor certifi knows, so the CA file has to be named explicitly. A remote
    FWO installation has no local fworch.json, in which case verification falls
    back to the default trust store rather than being switched off. A file that
    exists but cannot be parsed is a real misconfiguration and is raised.
    """
    try:
        with open(fwo_config_filename, encoding="utf-8") as config_file:
            config: dict[str, Any] = json.load(config_file)
    except OSError:
        return (None, True)

    certificate = config.get("tls_client_certificate")
    private_key = config.get("tls_client_private_key")
    ca_certificate = config.get("tls_ca_certificate")
    client_identity = (
        (certificate, private_key) if isinstance(certificate, str) and isinstance(private_key, str) else None
    )
    return (client_identity, ca_certificate if isinstance(ca_certificate, str) else True)


def configure_tls(session: Session, fwo_config_filename: str | None = None) -> None:
    """Present the local FWO client identity and verify against the internal CA."""
    client_identity, ca_certificate = read_tls_identity(fwo_config_filename or FWO_CONFIG_FILE)
    session.verify = ca_certificate
    if client_identity is not None:
        session.cert = client_identity


class HttpCommand(Enum):
    GET = "get"
    POST = "post"
    PUT = "put"
    DELETE = "delete"


def fwo_rest_api_call(
    api_url: str,
    jwt: str,
    endpoint_name: str,
    command: str = "get",
    payload: dict[str, Any] | None = None,
) -> Any:
    if payload is None:
        payload = {}
    headers: dict[str, str] = {"Authorization": "Bearer " + jwt, "Content-Type": "application/json"}

    with Session() as session:
        configure_tls(session)
        http_method = getattr(session, command.lower())
        response = http_method(api_url + endpoint_name, json=payload, headers=headers, timeout=HTTP_TIMEOUT)

        if response.status_code == HTTP_OK:
            return response.json()
        logger.error("API call failed with status code %s: %s", response.status_code, response.text)
        sys.exit(1)


# get JWT token from FWO REST API
def get_jwt_token(user: str, password: str, api_url: str = DEFAULT_API_URL) -> str:
    payload: dict[str, Any] = {"Username": user, "Password": password}
    headers: dict[str, str] = {"content-type": "application/json"}

    endpoint: str = api_url + "AuthenticationToken/Get"

    with Session() as session:
        configure_tls(session)
        try:
            response = session.post(endpoint, json=payload, headers=headers, timeout=HTTP_TIMEOUT)
        except exceptions.RequestException:
            logger.exception("api: error during login to url: %s with user %s", endpoint, user)
            sys.exit(1)

        if response.status_code == HTTP_OK:
            return response.text
        logger.error(
            "FWO api: ERROR: did not receive JWT, endpoint: %s, status code: %s",
            endpoint,
            response,
        )
        sys.exit(1)


def get_matching_groups(jwt: str, group_pattern: str, api_url: str | None = None) -> list[dict[str, Any]]:
    # Get all groups
    if api_url is None:
        api_url = DEFAULT_API_URL
    groups: list[dict[str, Any]] = fwo_rest_api_call(api_url, jwt, "Group", HttpCommand.GET.value)

    # Filter groups
    return [group for group in groups if group_pattern in group["GroupDn"]]


def delete_groups_from_roles(groups_to_delete: list[str], roles: list[str] | None = None) -> None:
    if roles is None:
        roles = []
    # first we need to remove the groups from all roles to be able to delete them
    role: str
    for role in roles:
        group: str
        for group in groups_to_delete:
            delete_response = fwo_rest_api_call(
                args.api_url, jwt, "Role/User", HttpCommand.DELETE.value, payload={"Role": role, "UserDn": group}
            )
            if not delete_response:
                logger.warning("Failed to delete group %s from role %s", group, role)


def extract_common_names(group_dns_to_delete: list[dict[str, Any]]) -> list[str]:
    return [group["GroupDn"].split(",")[0].split("=")[1] for group in group_dns_to_delete]


if __name__ == "__main__":  # pragma: no cover
    parser = ArgumentParser(description="Delete internal groups from FWO")
    parser.add_argument("-u", "--user", required=True, help="Username for FWO API")
    parser.add_argument("-p", "--password", required=True, help="Password for FWO API")
    parser.add_argument(
        "-a", "--api_url", default="https://", help="Base URL for FWO API (default: https://localhost:8888/api/)"
    )
    parser.add_argument("-g", "--group_name", required=True, help="name of group to delete")

    args: argparse.Namespace = parser.parse_args()

    logger: logging.Logger = logging.getLogger(__name__)

    try:
        jwt: str = get_jwt_token(args.user, args.password, args.api_url)
        group_dns_to_delete: list[dict[str, Any]] = get_matching_groups(jwt, args.group_name, args.api_url)
        group_common_names_to_delete: list[str] = extract_common_names(group_dns_to_delete)

        group_delete_counter: int = 0
        group: str
        for group in group_common_names_to_delete:
            if fwo_rest_api_call(args.api_url, jwt, "Group", HttpCommand.DELETE.value, payload={"GroupName": group}):
                group_delete_counter += 1
            else:
                logger.warning("Failed to delete group %s", group)

    except Exception:
        # without this the operator gets a bare exit 1 and no idea what went wrong,
        # for example when fworch.json exists but cannot be parsed
        logger.exception("deleting internal groups failed")
        sys.exit(1)
    else:
        sys.exit(0)

#!/usr/bin/python3
# -*- coding: utf-8 -*-
# This script deletes internal groups in FWO
import argparse
import logging
import json
import sys
from enum import Enum
import urllib3
from typing import Any

from argparse import ArgumentParser
from requests import Session, exceptions

default_api_url: str = 'https://localhost:8888/api/'


class HttpCommand(Enum):
    GET = 'get'
    POST = 'post'
    PUT = 'put'
    DELETE = 'delete'


def fwo_rest_api_call(
    api_url: str,
    jwt: str,
    endpoint_name: str,
    command: str = 'get',
    payload: dict[str, Any] | None = None,
) -> Any:
    if payload is None:
        payload = {}
    headers: dict[str, str] = {
        'Authorization': 'Bearer ' + jwt,
        'Content-Type': 'application/json'
    }

    with Session() as session:
        session.verify = False
        http_method = getattr(session, command.lower())
        urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
        response = http_method(api_url + endpoint_name, json=payload, headers=headers)

        if response.status_code == 200:
            return response.json()
        else:
            logger.error(f"API call failed with status code {response.status_code}: {response.text}")
            sys.exit(1)


# get JWT token from FWO REST API
def get_jwt_token(user: str, password: str, api_url: str = default_api_url) -> str:
    payload: dict[str, str] = { "Username": user, "Password": password }
    headers: dict[str, str] = {'content-type': 'application/json'}

    endpoint: str = api_url + "AuthenticationToken/Get"

    with Session() as session:
        session.verify = False
        try:
            urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
            response = session.post(endpoint, json=payload, headers=headers)
        except exceptions.RequestException:
            logger.error(f"api: error during login to url: {endpoint} with user {user}")
            sys.exit(1)

        if response.text is not None and response.status_code==200:
            return response.text
        else:
            logger.error(f"FWO api: ERROR: did not receive JWT, endpoint: {endpoint}, status code: {str(response)}")
            sys.exit(1)


def get_matching_groups(jwt: str, group_pattern: str, api_url: str | None = None) -> list[dict[str, Any]]:
    # Get all groups
    groups: list[dict[str, Any]] = fwo_rest_api_call(api_url, jwt, "Group", HttpCommand.GET.value)

    # Filter groups
    return [group for group in groups if group_pattern in group['GroupDn']]


def delete_groups_from_roles(groups_to_delete: list[str], roles: list[str] | None = None) -> None:
    if roles is None:
        roles = []
    # first we need to remove the groups from all roles to be able to delete them
    from_role_delete_counter: int = 0
    error_counter: int = 0
    role: str
    for role in roles:
        group: str
        for group in groups_to_delete:
            delete_response = fwo_rest_api_call(
                args.api_url, jwt, "Role/User", HttpCommand.DELETE.value, payload={"Role": role, "UserDn": group})        
            if not delete_response:
                error_counter += 1
                logger.warning(f"Failed to delete group {group} from role {role}")
            else: 
                from_role_delete_counter += 1
    print(f"Deleted {from_role_delete_counter} groups from roles. Errors: {error_counter}")


def extract_common_names(group_dns_to_delete: list[dict[str, Any]]) -> list[str]:
    common_names: list[str] = []
    group: dict[str, Any]
    for group in group_dns_to_delete:
        common_names.append(group['GroupDn'].split(',')[0].split('=')[1])
    return common_names


if __name__ == "__main__":
    parser = ArgumentParser(description='Delete internal groups from FWO')
    parser.add_argument('-u', '--user', required=True, help='Username for FWO API')
    parser.add_argument('-p', '--password', required=True, help='Password for FWO API')
    parser.add_argument('-a', '--api_url', default='https://', help='Base URL for FWO API (default: https://localhost:8888/api/)')
    parser.add_argument('-g', '--group_name', required=True, help='name of group to delete')

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
                logger.warning(f"Failed to delete group {group}")

        print(f"Deleted {group_delete_counter} out of {len(group_common_names_to_delete)} groups.")

    except Exception as e:
        print(f"An unexpected error occurred: {str(e)}")
        sys.exit(1)
    else:
        sys.exit(0) 

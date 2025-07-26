#!/usr/bin/python3
# -*- coding: utf-8 -*-
# This script deletes internal groups in FWO
from requests import Session, exceptions
from argparse import ArgumentParser
import logging
import json
import sys
from enum import Enum
import urllib3

default_api_url = 'https://localhost:8888/api/'


class HttpCommand(Enum):
    GET = 'get'
    POST = 'post'
    PUT = 'put'
    DELETE = 'delete'


def fwo_rest_api_call(api_url, jwt, endpoint_name, command='get', payload={}):
    headers = {
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
def get_jwt_token(user, password, api_url=default_api_url):
    payload = { "Username": user, "Password": password }
    headers = {'content-type': 'application/json'}

    endpoint = api_url + "AuthenticationToken/Get"

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
            logger.error(f"FWO api: ERROR: did not receive JWT" + \
                            ", endpoint: " + endpoint + \
                            ", status code: " + str(response))
            sys.exit(1)


def get_matching_groups(jwt, group_pattern, api_url=None):
    # Get all groups
    groups = fwo_rest_api_call(api_url, jwt, "Group", HttpCommand.GET.value)

    # Filter groups
    return [group for group in groups if group_pattern in group['GroupDn']]


def delete_groups_from_roles(groups_to_delete, roles=[]):
    # first we need to remove the groups from all roles to be able to delete them
    from_role_delete_counter = 0
    error_counter = 0
    for role in roles:
        for group in groups_to_delete:
            delete_response = fwo_rest_api_call(
                args.api_url, jwt, "Role/User", HttpCommand.DELETE.value, payload={"Role": role, "UserDn": group})        
            if not delete_response:
                error_counter += 1
                logger.warning(f"Failed to delete group {group} from role {role}")
            else: 
                from_role_delete_counter += 1
    print(f"Deleted {from_role_delete_counter} groups from roles. Errors: {error_counter}")


def extract_common_names(group_dns_to_delete):
    common_names = []
    for group in group_dns_to_delete:
        common_names.append(group['GroupDn'].split(',')[0].split('=')[1])
    return common_names


if __name__ == "__main__":
    parser = ArgumentParser(description='Delete internal groups from FWO')
    parser.add_argument('-u', '--user', required=True, help='Username for FWO API')
    parser.add_argument('-p', '--password', required=True, help='Password for FWO API')
    parser.add_argument('-a', '--api_url', default='https://', help='Base URL for FWO API (default: https://localhost:8888/)')
    parser.add_argument('-g', '--group_name', required=True, help='name of group to delete')

    args = parser.parse_args()

    logger = logging.getLogger(__name__)


    try:
        jwt = get_jwt_token(args.user, args.password, args.api_url)
        group_dns_to_delete = get_matching_groups(jwt, args.group_name, args.api_url)
        group_common_names_to_delete = extract_common_names(group_dns_to_delete)

        group_delete_counter = 0
        for group in group_common_names_to_delete:
            if fwo_rest_api_call(args.api_url, jwt, "Group", HttpCommand.DELETE.value, payload={"GroupName": group}):
                group_delete_counter += 1
            else:
                logger.warning(f"Failed to delete group {group}")

        print(f"Deleted {group_delete_counter} out of {len(group_common_names_to_delete)} groups.")


    except ApiLoginFailed as e:
        print(f"Login failed: {e.message}")
    except ApiFailure as e:
        print(f"API failure: {e.message}")
    except ApiTimeout as e:
        print(f"API timeout: {e.message}")
    except ApiServiceUnavailable as e:
        print(f"API service unavailable: {e.message}")
    except Exception as e:
        print(f"An unexpected error occurred: {str(e)}")
        sys.exit(1)
    else:
        sys.exit(0) 

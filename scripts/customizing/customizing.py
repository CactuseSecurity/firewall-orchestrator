# library for FWORCH API calls
from asyncio.log import logger
import argparse
import getpass
import json
import sys
from typing import Any

import requests
import requests.packages


def call(
    url: str,
    jwt: str,
    query: str,
    query_variables: dict[str, Any] | str = "",
    role: str = "reporter",
    show_progress: bool = False,
    method: str = "",
) -> dict[str, Any] | None:
    request_headers: dict[str, str] = {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer ' + jwt,
        'x-hasura-role': role,
    }
    full_query: dict[str, Any] = {"query": query, "variables": query_variables}

    with requests.Session() as session:
        session.verify = False
        session.headers = request_headers

        r: requests.Response | None = None
        try:
            r = session.post(url, data=json.dumps(full_query), timeout=600)
            r.raise_for_status()
        except requests.exceptions.RequestException:
            if r != None:
                if r.status_code != 200:
                    raise Exception("fwo_api call ERROR: got error code: " + str(r.status_code))
            else:
                raise Exception("fwo_api call ERROR: got no result from FWO API call")
        if r != None:
            return r.json()
        else:
            return None


def login(user: str, password: str, user_management_api_base_url: str, method: str = 'api/AuthenticationToken/Get') -> str:
    payload: dict[str, str] = {"Username": user, "Password": password}

    with requests.Session() as session:
        session.verify = False
        session.headers = {'Content-Type': 'application/json'}

        try:
            response = session.post(user_management_api_base_url + method, data=json.dumps(payload))
        except requests.exceptions.RequestException:
            raise Exception("fwo_api login ERROR: no valid response from: " + str(user_management_api_base_url))

        if response.text is not None and response.status_code==200:
            return response.text    # the JWT
        else:
            raise Exception("fwo_api login ERROR: did not receive a JWT during login to api_url: " + str(user_management_api_base_url))


def get_config_value(fwo_api_base_url: str, jwt: str, key: str = 'limit') -> str | None:
    query_variables: dict[str, str] = {'key': key}
    config_query: str = "query getConf($key: String) {  config(where: {config_key: {_eq: $key}}) { config_value } }"
    result: dict[str, Any] | None = call(fwo_api_base_url, jwt, config_query, query_variables=query_variables, role='importer')
    if result is None:
        return None
    if 'data' in result and 'config' in result['data']:
        first_result: dict[str, Any] = result['data']['config'][0]
        if 'config_value' in first_result:
            return first_result['config_value']
        else:
            return None
    else:
        return None


def get_config_values(fwo_api_base_url: str, jwt: str, keyFilter: str = 'limit') -> dict[str, Any] | None:
    query_variables: dict[str, str] = {'keyFilter': keyFilter + "%"}
    config_query: str = "query getConf($keyFilter: String) { config(where: {config_key: {_ilike: $keyFilter}}) { config_key config_value } }"
    result: dict[str, Any] | None = call(fwo_api_base_url, jwt, config_query, query_variables=query_variables, role='importer')
    if result is None:
        return None
    if 'data' in result and 'config' in result['data']:
        resultArray: list[dict[str, Any]] = result['data']['config']
        dict1: dict[str, Any] = {v['config_key']: v['config_value'] for v in resultArray}
        return dict1
    else:
        return None


def readJsonFile(filename: str) -> dict[str, Any]:
    try: 
        with open(filename, "r", encoding="utf-8") as jsonFH:
            jsonDict: dict[str, Any] = json.loads(jsonFH.read())
    except Exception:
        raise Exception("readJsonFile ERROR: while reading file: " + filename)
    return jsonDict


def setCustomTxtValues(
    fwo_api_base_url: str,
    jwt: str,
    query_variables: dict[str, Any] | None = None,
    keyFilter: str = 'limit',
) -> int | str:
    if query_variables is None:
        query_variables = {}
    customTxt_mutation = """
        mutation upsertCustomText($id: String!, $language: String!, $txt: String!) {
            insert_customtxt(
                objects: {
                    id: $id
                    language: $language
                    txt: $txt
                },
                on_conflict: {
                    constraint: customtxt_pkey ,
                    update_columns: [txt]
                }
            ) {
                returning {
                    id: id
                }
            }
        }
    """
    result: dict[str, Any] | None = call(fwo_api_base_url, jwt, customTxt_mutation, query_variables=query_variables, role='admin')
    if result is None:
        return -1
    if result['data']['insert_customtxt']['returning'][0]['id']:
        return result['data']['insert_customtxt']['returning'][0]['id']
    else:
        return -1
        
        
def setModellingServiceValues(
    fwo_api_base_url: str,
    jwt: str,
    query_variables: dict[str, Any] | None = None,
    keyFilter: str = 'limit',
) -> int | str:
    if query_variables is None:
        query_variables = {}
    modellingService_mutation = """
        mutation upsertService(
            $name: String
            $app_id: Int
            $is_global: Boolean
            $port: Int
            $port_end: Int
            $proto_id: Int
            ) {
                insert_modelling_service(
                    objects: {
                        name: $name
                        app_id: $app_id
                        is_global: $is_global
                        port: $port
                        port_end: $port_end
                        proto_id: $proto_id
                    }
                    on_conflict: {
                        constraint: modelling_service_unique_name,
                        update_columns: [name, app_id, is_global, port, port_end, proto_id]
                    }
                ) {
                    returning {
                        id
                    }
                }
        }
    """

    # export your modelling services using the following query:
    # query getGlobalModServices {
    #   modelling_service(where: {is_global: {_eq: true}}) {
    #     port
    #     port_end
    #     proto_id
    #     name
    #     is_global
    #   }
    # }

    result: dict[str, Any] | None = call(fwo_api_base_url, jwt, modellingService_mutation, query_variables=query_variables, role='admin')
    if result is None:
        return -1
    if result['data']['insert_modelling_service']['returning'][0]['id']:
        return result['data']['insert_modelling_service']['returning'][0]['id']
    else:
        return -1


def setConfigValues(
    fwo_api_base_url: str,
    jwt: str,
    query_variables: dict[str, Any] | None = None,
    keyFilter: str = 'limit',
) -> int | str:
    if query_variables is None:
        query_variables = {}
    config_mutation = """
        mutation upsertConfigItem($config_key: String!, $config_value: String!, $config_user: Int!) {
            insert_config(
                objects: {
                config_key: $config_key,
                config_value: $config_value,
                config_user: $config_user
                },
                on_conflict: {
                constraint: config_pkey,
                update_columns: [config_value]
                }
            ) {
                returning {
                id: config_key
                }
            }
        }
    """
    result: dict[str, Any] | None = call(fwo_api_base_url, jwt, config_mutation, query_variables=query_variables, role='admin')
    if result is None:
        return -1
    if result['data']['insert_config']['returning'][0]['id']:
        return result['data']['insert_config']['returning'][0]['id']
    else:
        return -1
    

def getCredentials() -> tuple[str, str]:
    username: str = input("Enter your username: ")
    password: str = getpass.getpass("Enter your password: ")
    return username, password


if __name__ == '__main__':
    parser = argparse.ArgumentParser(
        description='Writing custom settings via API to firewall orchestrator')

    parser.add_argument('-c', '--customSettingsFile', required=True,
                        help='Filename of custom settings file for firewall orchstrator (mandatory parameter)')

    args: argparse.Namespace = parser.parse_args()

    if len(sys.argv) == 1:
        parser.print_help(sys.stderr)
        sys.exit(1)

    settingsFile: str = args.customSettingsFile
    fwo_config_filename: str = '/etc/fworch/fworch.json'
    requests.packages.urllib3.disable_warnings()

    fwo_config: dict[str, Any] = readJsonFile(fwo_config_filename)
    user_management_api_base_url: str = fwo_config['middleware_uri']
    fwo_api_base_url: str = fwo_config['api_uri']


    # read credentials interactively
    print("Enter credentials of a user with admin role:")
    username: str
    password: str
    username, password = getCredentials()

    # login with the credentials to get JWT
    jwt: str = login(username, password, user_management_api_base_url, method='api/AuthenticationToken/Get')

    # read settings to write to API from file
    settings: dict[str, Any] = readJsonFile(settingsFile)

    # write settings to FWO API using the JWT
    # overwrites existing values making this script idempotent

    t: str
    for t in settings:
        if t=='config':
            obj: dict[str, Any]
            for obj in settings[t]:
                setConfigValues(fwo_api_base_url, jwt, query_variables=obj)
                # issue in config: area ids will vary - do we re-write this using the area name?
                    # {
                    #     "config_key": "modCommonAreas",
                    #     "config_value": "[{\"area_id\":88,\"use_in_src\":true,\"use_in_dst\":false},{\"area_id\":43,\"use_in_src\":true,\"use_in_dst\":true}]",
                    #     "config_user": 0
                    # },
        elif t=='customtxt':
            for obj in settings[t]:
                setCustomTxtValues(fwo_api_base_url, jwt, query_variables=obj)

        elif t=='modelling_service':
            for obj in settings[t]:
                setModellingServiceValues(fwo_api_base_url, jwt, query_variables=obj)

        # if t=='local appserver':  # here again we have the (app) id issue - might be able to circumvent this by using objects as references

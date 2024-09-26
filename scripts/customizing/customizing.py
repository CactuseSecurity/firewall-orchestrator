# library for FWORCH API calls
from asyncio.log import logger
import requests.packages
import requests
import json
import argparse
import getpass
import argparse
import sys


def call(url, jwt, query, query_variables="", role="reporter", show_progress=False, method=''):
    request_headers = { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + jwt, 'x-hasura-role': role }
    full_query = {"query": query, "variables": query_variables}

    with requests.Session() as session:
        session.verify = False
        session.headers = request_headers

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


def login(user, password, user_management_api_base_url, method='api/AuthenticationToken/Get'):
    payload = {"Username": user, "Password": password}

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


def get_config_value(fwo_api_base_url, jwt, key='limit'):
    query_variables = {'key': key}
    config_query = "query getConf($key: String) {  config(where: {config_key: {_eq: $key}}) { config_value } }"
    result = call(fwo_api_base_url, jwt, config_query, query_variables=query_variables, role='importer')
    if 'data' in result and 'config' in result['data']:
        first_result = result['data']['config'][0]
        if 'config_value' in first_result:
            return first_result['config_value']
        else:
            return None
    else:
        return None


def get_config_values(fwo_api_base_url, jwt, keyFilter='limit'):
    query_variables = {'keyFilter': keyFilter+"%"}
    config_query = "query getConf($keyFilter: String) { config(where: {config_key: {_ilike: $keyFilter}}) { config_key config_value } }"
    result = call(fwo_api_base_url, jwt, config_query, query_variables=query_variables, role='importer')
    if 'data' in result and 'config' in result['data']:
        resultArray = result['data']['config']
        dict1 = {v['config_key']: v['config_value'] for k,v in enumerate(resultArray)}
        return dict1
    else:
        return None


def readJsonFile(filename):
    try: 
        with open(filename, "r") as jsonFH:
            jsonDict = json.loads(jsonFH.read())
    except:
        raise Exception("readJsonFile ERROR: while reading file: " + filename)
    return jsonDict


def setCustomTxtValues(fwo_api_base_url, jwt, query_variables={}, keyFilter='limit'):
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
    result = call(fwo_api_base_url, jwt, customTxt_mutation, query_variables=query_variables, role='admin')
    if result['data']['insert_customtxt']['returning'][0]['id']:
        return result['data']['insert_customtxt']['returning'][0]['id']
    else:
        return -1
        
        
def setModellingServiceValues(fwo_api_base_url, jwt, query_variables={}, keyFilter='limit'):
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

    result = call(fwo_api_base_url, jwt, modellingService_mutation, query_variables=query_variables, role='admin')
    if result['data']['insert_modelling_service']['returning'][0]['id']:
        return result['data']['insert_modelling_service']['returning'][0]['id']
    else:
        return -1


def setConfigValues(fwo_api_base_url, jwt, query_variables={}, keyFilter='limit'):
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
    result = call(fwo_api_base_url, jwt, config_mutation, query_variables=query_variables, role='admin')
    if result['data']['insert_config']['returning'][0]['id']:
        return result['data']['insert_config']['returning'][0]['id']
    else:
        return -1
    

def getCredentials():
    username = input("Enter your username: ")
    password = getpass.getpass("Enter your password: ")
    return username, password


if __name__ == '__main__':
    parser = argparse.ArgumentParser(
        description='Writing custom settings via API to firewall orchestrator')

    parser.add_argument('-c', '--customSettingsFile', required=True,
                        help='Filename of custom settings file for firewall orchstrator (mandatory parameter)')

    args = parser.parse_args()

    if len(sys.argv) == 1:
        parser.print_help(sys.stderr)
        sys.exit(1)

    settingsFile = args.customSettingsFile
    fwo_config_filename = '/etc/fworch/fworch.json'
    requests.packages.urllib3.disable_warnings()

    fwo_config = readJsonFile(fwo_config_filename)
    user_management_api_base_url = fwo_config['middleware_uri']
    fwo_api_base_url = fwo_config['api_uri']


    # read credentials interactively
    print("Enter credentials of a user with admin role:")
    username, password = getCredentials()

    # login with the credentials to get JWT
    jwt = login(username, password, user_management_api_base_url, method='api/AuthenticationToken/Get')

    # read settings to write to API from file
    settings = readJsonFile(settingsFile)

    # write settings to FWO API using the JWT
    # overwrites existing values making this script idempotent

    for t in settings:
        if t=='config':
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

#!/usr/bin/python3
# demonstration script

sys.exit(0)

from asyncio.log import logger
import re
import traceback
from textwrap import indent
import requests.packages
import requests
import json
import datetime

import sys, traceback
import argparse


# from fwo_log import getFwoLogger
# from fwo_const import fwo_api_http_import_timeout
# from fwo_exception import FwoApiTServiceUnavailable, FwoApiTimeout, FwoApiLoginFailed
# from fwo_base import writeAlertToLogFile

#from common import importer_base_dir
#import fwo_globals, fwo_config, fwo_api
#sys.path.append(importer_base_dir)


class FwoApiLoginFailed(Exception):
    """Raised when login to FWO API failed"""

    def __init__(self, message="Login to FWO API failed"):
            self.message = message
            super().__init__(self.message)


def showApiCallInfo(url, query, headers, type='debug'):
    max_query_size_to_display = 1000
    query_string = json.dumps(query, indent=2)
    header_string = json.dumps(headers, indent=2)
    query_size = len(query_string)

    if type=='error':
        result = "error while sending api_call to url "
    else:
        result = "successful FWO API call to url "        
    result += str(url) + " with payload \n"
    if query_size < max_query_size_to_display:
        result += query_string 
    else:
        result += str(query)[:round(max_query_size_to_display/2)] +   "\n ... [snip] ... \n" + \
            query_string[query_size-round(max_query_size_to_display/2):] + " (total query size=" + str(query_size) + " bytes)"
    result += "\n and  headers: \n" + header_string
    return result


def call(url, jwt, query, query_variables="", role="reporter", show_progress=False, method=''):
    request_headers = { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + jwt, 'x-hasura-role': role }
    full_query = {"query": query, "variables": query_variables}
    #logger = getFwoLogger()

    with requests.Session() as session:
        session.verify = False
        session.headers = request_headers

        try:
            #r = session.post(url, data=json.dumps(full_query), timeout=int(fwo_api_http_import_timeout))
            r = session.post(url, data=json.dumps(full_query), timeout=1000)
            r.raise_for_status()
        except requests.exceptions.RequestException:
            logger.error(showApiCallInfo(url, full_query, request_headers, type='error') + ":\n" + str(traceback.format_exc()))
            if r != None:
                if r.status_code == 503:
                    raise FwoApiTServiceUnavailable("FWO API HTTP error 503 (FWO API died?)" )
                if r.status_code == 502:
                    raise FwoApiTimeout("FWO API HTTP error 502 (might have reached timeout of " + str(1000/60) + " minutes)" )
            else:
                raise
        logger.debug (showApiCallInfo(url, full_query, request_headers, type='debug'))
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
            raise FwoApiLoginFailed ("fwo_api: error during login to url: " + str(user_management_api_base_url) + " with user " + user) from None

        if response.text is not None and response.status_code==200:
            return response.text
        else:
            error_txt = "fwo_api: ERROR: did not receive a JWT during login" + \
                            ", api_url: " + str(user_management_api_base_url)
            raise FwoApiLoginFailed(error_txt)


def get_mgm_ids(fwo_api_base_url, jwt, query_variables):
    mgm_query = """
        query getManagementIds {
            management(where:{do_not_import:{_eq:false}} order_by: {mgm_name: asc}) { id: mgm_id } } """
    return call(fwo_api_base_url, jwt, mgm_query, query_variables=query_variables, role='importer')['data']['management']


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


def setConfig(fwo_api_base_url, jwt, key, value, config_user=0):

    # insert might be replaced by upsert for idempotence

    addConfigMutation = """
        mutation addConfigItem(
            $key: String!
            $value: String!
            $user: Int!
            ) {
            insert_config(
                objects: {
                config_key: $key
                config_value: $value
                config_user: $user
                }
            ) {
                returning {
                newId: config_key
                }
            }
        }
        """

    # we still need to decide which role/user we may use for these settings (admin with admin pwd from etc/secrets/ui_admin_pwd?)
    # password should not be current when it was changed as suggested!
    # user with middleware role?

    result = call(fwo_api_base_url, jwt, addConfigMutation, query_variables={ "key": key, "value": value, "user": config_user}, role="admin", show_progress=False)

    logger.warning(str(result))


if __name__ == "__main__": 
    parser = argparse.ArgumentParser(
        description='Read configuration from FW management via API calls')
    parser.add_argument('-s', '--sample', metavar='sample_param',
                        required=False, help='template only here')

    args = parser.parse_args()
    if len(sys.argv) == 1:
        pass
        # parser.print_help(sys.stderr)
        # sys.exit(1)

    # TODO: move credentials to file
    user = "admin"
    password = 'xxx'

    user_management_api_base_url = "https://localhost:8888/"
    fwo_api_base_url = "https://localhost:9443/api/v1/graphql"

    key = "testKey2"
    value = "testValue2"

    try:
        # logging in to FWO API to get JWT 
        jwt = login(user, password, user_management_api_base_url, method='api/AuthenticationToken/Get')

        # setting config value via FWO API
        setConfig(fwo_api_base_url, jwt, key, value, config_user=0) # user=0 --> global config
    except:
        logger.error("error while setting config value " + str(key) + ": " + str(value) + ", " +  str(traceback.format_exc()))
        sys.exit(1)

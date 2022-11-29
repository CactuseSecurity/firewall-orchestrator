# library for API get functions
import base64
from typing import Dict
from fwo_log import getFwoLogger
import requests.packages
import requests
import json
import fwo_globals
from fwo_exception import FwLoginFailed


def api_call(url, params = {}, headers = {}, data = {}, azure_jwt = '', show_progress=False, method='get'):
    logger = getFwoLogger()
    request_headers = {}
    if not 'Content-Type' in headers:
        request_headers = {'Content-Type': 'application/json'}
    for header_key in headers:
        request_headers[header_key] = headers[header_key]
    if azure_jwt != '':
        request_headers["Authorization"] = 'Bearer {azure_jwt}'.format(azure_jwt=azure_jwt)
    if request_headers['Content-Type'] == 'application/json':
        data=json.dumps(data)
    else:   # login only
        data=data    
        
    if method == "post":
        response = requests.post(url, params=params, data=data, headers=request_headers, verify=fwo_globals.verify_certs)
    elif method == "get":
        response = requests.get(url, params=params, headers=request_headers, verify=fwo_globals.verify_certs)
    else:
        raise Exception("unknown HTTP method found in azure_getter")
    
    # error handling:
    exception_text = ''
    if response is None:
        if 'password' in json.dumps(data):
            exception_text = "error while sending api_call containing credential information to url '" + \
                str(url)
        else:
            exception_text = "error while sending api_call to url '" + str(url) + "' with payload '" + json.dumps(
                data, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2)
    if not response.ok:
        exception_text = 'error code: {error_code}, error={error}'.format(error_code=response.status_code, error=response.content)
        #logger.error(response.content)
    if (len(response.content) == 0):
        exception_text = 'empty response content'

    if exception_text != '':
        raise Exception(exception_text)

    # no errors found
    body_json = response.json()
        
    if fwo_globals.debug_level > 5:
        if 'pass' in json.dumps(data):
            logger.debug("api_call containing credential information to url '" +
                         str(url) + " - not logging query")
        else:
            logger.debug("api_call to url '" + str(url) + "' with payload '" + json.dumps(
                data, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2))

    return response.headers, body_json


def login(azure_user, azure_password, tenant_id, client_id, client_secret):
    base_url = 'https://login.microsoftonline.com/{tenant_id}/oauth2/token'.format(tenant_id=tenant_id)
    try:
        headers, body = api_call(base_url, method="post",
            headers={'Content-Type': 'application/x-www-form-urlencoded'},
            data={
                "grant_type" : "client_credentials",
                "client_id": client_id, 
                "client_secret": client_secret,
                "resource": "https://management.azure.com/",
                "username": str(base64.b64encode((azure_user).encode('utf-8')), 'utf-8'),
                "password": str(base64.b64encode((azure_password).encode('utf-8')), 'utf-8')
        })
    except Exception as e:
        raise FwLoginFailed("Azure login ERROR for client_id id=" + str(client_id) + " Message: " + str(e)) from None
    
    if body.get("access_token") == None:   # leaving out payload as it contains pwd
        raise FwLoginFailed("Azure login ERROR for client_id=" + str(client_id) + " Message: " + str(e)) from None
    
    if fwo_globals.debug_level > 2:
        logger = getFwoLogger()
        logger.debug("Login successful. Received JWT: " + body["access_token"])

    return body["access_token"]


def update_config_with_azure_api_call(azure_jwt, api_base_url, config, api_path, key, parameters={}, payload={}, show_progress=False, limit: int=1000, method="get"):
    offset = 0
    limit = 1000
    returned_new_data = True
    
    full_result = []
    #while returned_new_data:
        # parameters["offset"] = offset
        # parameters["limit"] = limit
    result = api_call(api_base_url + api_path, azure_jwt=azure_jwt, params=parameters, data=payload, show_progress=show_progress, method=method)[1]
    returned_new_data = len(result['value'])>0
    if returned_new_data:
        full_result.extend(result["value"])           
#            offset += limit
    config.update({key: full_result})

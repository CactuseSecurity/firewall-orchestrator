# library for API get functions
import base64
from typing import Dict
from fwo_log import getFwoLogger
import requests.packages
import requests
import xmltodict, json
import fwo_globals
from fwo_exception import FwLoginFailed


def api_call(url, params = {}, headers = {}, data = {}, key = '', show_progress=False, method='get'):
    logger = getFwoLogger()
    result_type='xml'
    request_headers = {'Content-Type': 'application/json'}
    for header_key in headers:
        request_headers[header_key] = headers[header_key]
    if key != '':
        request_headers["X-PAN-KEY"] = '{key}'.format(key=key)
        result_type='json'

    if method == "post":
        response = requests.post(url, params=params, data=data, headers=request_headers, verify=fwo_globals.verify_certs)
    elif method == "get":
        response = requests.get(url, params=params, headers=request_headers, verify=fwo_globals.verify_certs)
    else:
        raise Exception("unknown HTTP method found in palo_getter")
    
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
    if result_type=='xml':
        r = xmltodict.parse(response.content)
        body_json = json.loads(json.dumps(r))
    elif result_type=='json':
        body_json = json.loads(response.content)
        if 'result' in body_json:
            body_json = body_json['result']

    else:
        body_json = None

    # if fwo_globals.debug_level > 5:
    #     if 'pass' in json.dumps(data):
    #         logger.debug("api_call containing credential information to url '" +
    #                      str(url) + " - not logging query")
    #     else:
    #         logger.debug("api_call to url '" + str(url) + "' with payload '" + json.dumps(
    #             data, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2))

    return body_json


def login(apiuser, apipwd, apihost):
    base_url = "https://{apihost}/api/?type=keygen&user={apiuser}&password={apipwd}".format(apihost=apihost, apiuser=apiuser, apipwd=apipwd)
    try:
        body = api_call(base_url, method="get", headers={}, data={})
    except Exception as e:
        raise FwLoginFailed("Palo FW login to firewall=" + str(apihost) + " failed; Message: " + str(e)) from None
    
    if 'response' in body and 'result' in body['response'] and 'key' in body['response']['result'] and not body['response']['result']['key'] == None:
        key = body['response']['result']['key']
    else:
        raise FwLoginFailed("Palo FW login to firewall=" + str(apihost) + " failed") from None
    
    if fwo_globals.debug_level > 2:
        logger = getFwoLogger()
        logger.debug("Login successful. Received key: " + key)

    return key


def update_config_with_palofw_api_call(key, api_base_url, config, api_path, obj_type='generic', parameters={}, payload={}, show_progress=False, limit: int=1000, method="get"):
    returned_new_data = True
    
    full_result = []
    result = api_call(api_base_url + api_path,key=key, params=parameters, data=payload, show_progress=show_progress, method=method)
    if "entry" in result:
        returned_new_data = len(result['entry'])>0
    else:
        returned_new_data = False
    if returned_new_data:
        full_result.extend(result["entry"])           
        config.update({obj_type: full_result})

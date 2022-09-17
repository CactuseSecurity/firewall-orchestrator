# library for API get functions
import base64
import re
from typing import Dict
from fwo_log import getFwoLogger
import requests.packages
import requests
import json
import common
import fwo_globals

auth_token = ""

def api_call(url, params = {}, headers = {}, json_payload = {}, auth_token = '', show_progress=False, method='get'):
    logger = getFwoLogger()
    request_headers = {'Content-Type': 'application/json'}
    for header_key in headers:
        request_headers[header_key] = headers[header_key]
    if auth_token != '':
        request_headers["X-auth-access-token"] = auth_token
    # if command != '':
    #     for p in json_payload['params']:
    #         p.update({"url": command})
    #json_payload.update({"method": method})

    if method == "post":
        response = requests.post(url, params=params, data=json.dumps(json_payload), headers=request_headers,
                          verify=fwo_globals.verify_certs, proxies=fwo_globals.proxy)
    elif method == "get":
        response = requests.get(url, params=params, data=json.dumps(json_payload), headers=request_headers,
                         verify=fwo_globals.verify_certs, proxies=fwo_globals.proxy)
    if response is None:
        if 'pass' in json.dumps(json_payload):
            exception_text = "error while sending api_call containing credential information to url '" + \
                str(url)
        else:
            exception_text = "error while sending api_call to url '" + str(url) + "' with payload '" + json.dumps(
                json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2)
        raise Exception(exception_text)
    if (len(response.content) > 0):     
        body_json = response.json()
        # if 'items' not in body_json or len(body_json['items']) < 1:
        #     raise Exception("error while sending api_call '" + str(url))
        #     if 'pass' in json.dumps(json_payload):
        #         raise Exception(
        #             "error while sending api_call containing credential information to url '" + str(url))
        #     else:
        #         if 'status' in body_json['result'][0]:
        #             raise Exception("error while sending api_call to url '" + str(url) + "' with payload '" +
        #                             json.dumps(json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2) + ', result=' + json.dumps(response.json()['result'][0]['status'], indent=2))
        #         else:
        #             raise Exception("error while sending api_call to url '" + str(url) + "' with payload '" +
        #                             json.dumps(json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2) + ', result=' + json.dumps(response.json()['result'][0], indent=2))
        # if 'status' not in body_json['result'][0] or 'code' not in body_json['result'][0]['status'] or body_json['result'][0]['status']['code'] != 0:
        #     # trying to ignore empty results as valid
        #     pass  # logger.warning('received empty result')
    else:
        body_json = {}

    if fwo_globals.debug_level > 2:
        if 'pass' in json.dumps(json_payload):
            logger.debug("api_call containing credential information to url '" +
                         str(url) + " - not logging query")
        else:
            logger.debug("api_call to url '" + str(url) + "' with payload '" + json.dumps(
                json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2))

        if show_progress:
            print('.', end='', flush=True)
    return response.headers, body_json

def login(user, password, api_host, api_port):
    base_url = 'https://' + api_host + ':' + str(api_port) + '/api/'
    try:
        headers, _ = api_call(base_url + "fmc_platform/v1/auth/generatetoken", method="post", headers={"Authorization" : "Basic " + str(base64.b64encode((user + ":" + password).encode('utf-8')), 'utf-8')})
    except Exception as e:
        raise common.FwLoginFailed(
            "FortiManager login ERROR: host=" + str(api_host) + ":" + str(api_port) + " Message: " + str(e)) from None
    if headers.get("X-auth-access-token") == None:   # leaving out payload as it contains pwd
        raise common.FwLoginFailed(
            "FortiManager login ERROR: host=" + str(api_host) + ":" + str(api_port)) from None
    if fwo_globals.debug_level > 2:
        logger = getFwoLogger()
        logger.debug("Login successful. Received auth token: " + headers["X-auth-access-token"])
    return headers.get("X-auth-access-token"), headers.get("DOMAINS")

def logout(v_url, sid, method='exec'):
    return
    # logger = getFwoLogger()
    # payload = {"params": [{}]}

    # response = api_call(v_url, 'sys/logout', payload, sid, method=method)
    # if "result" in response and "status" in response["result"][0] and "code" in response["result"][0]["status"] and response["result"][0]["status"]["code"] == 0:
    #     logger.debug("successfully logged out")
    # else:
    #     raise Exception("cifp_getter ERROR: did not get status code 0 when logging out, " +
    #                     "api call: url: " + str(v_url) + ",  + payload: " + str(payload))

def update_config_with_cisco_api_call(session_id, api_base_url, api_path, parameters={}, payload={}, show_progress=False, limit: int=1000, method="get"):
    offset = 0
    limit = 1000
    returned_new_data = True
    full_result = []
    while returned_new_data:
        parameters["offset"] = offset
        parameters["limit"] = limit
        result = api_call(api_base_url + "/" + api_path, auth_token=session_id, params=parameters, json_payload=payload, show_progress=show_progress, method=method)[1]
        returned_new_data = result["paging"]["count"] > 0
        if returned_new_data:
            full_result.extend(result["items"])           
            offset += limit
    return full_result
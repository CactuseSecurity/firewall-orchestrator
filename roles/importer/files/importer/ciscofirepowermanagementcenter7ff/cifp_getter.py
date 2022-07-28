# library for API get functions
import base64
import re
from fwo_log import getFwoLogger
import requests.packages
import requests
import json
import common
import fwo_globals

def api_call(url, command, json_payload, auth_token = '', show_progress=False, method='', basic_auth_header=""):
    logger = getFwoLogger()
    request_headers = {'Content-Type': 'application/json'}
    if basic_auth_header != '':
        request_headers["Authorization"] = "Basic " + basic_auth_header
    if auth_token != '':
        request_headers["X-auth-access-token"] = auth_token
    # if command != '':
    #     for p in json_payload['params']:
    #         p.update({"url": command})
    if method == '':
        method = 'get'
    #json_payload.update({"method": method})

    if method == "post":
        response = requests.post(url, data=json.dumps(json_payload), headers=request_headers,
                          verify=fwo_globals.verify_certs, proxies=fwo_globals.proxy)
    elif method == "get":
        response = requests.get(url, data=json.dumps(json_payload), headers=request_headers,
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
        if 'result' not in body_json or len(body_json['result']) < 1:
            if 'pass' in json.dumps(json_payload):
                raise Exception(
                    "error while sending api_call containing credential information to url '" + str(url))
            else:
                if 'status' in body_json['result'][0]:
                    raise Exception("error while sending api_call to url '" + str(url) + "' with payload '" +
                                    json.dumps(json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2) + ', result=' + json.dumps(response.json()['result'][0]['status'], indent=2))
                else:
                    raise Exception("error while sending api_call to url '" + str(url) + "' with payload '" +
                                    json.dumps(json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2) + ', result=' + json.dumps(response.json()['result'][0], indent=2))
        if 'status' not in body_json['result'][0] or 'code' not in body_json['result'][0]['status'] or body_json['result'][0]['status']['code'] != 0:
            # trying to ignore empty results as valid
            pass  # logger.warning('received empty result')
    else:
        body_json = ""

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

def login(user, password, api_host, api_port, domain):
    payload = {}
    base_url = 'https://' + api_host + ':' + str(api_port) + '/api/'
    try:
        headers, _ = api_call(base_url + "fmc_platform/v1/auth/generatetoken",
                            payload, '', method="post", basic_auth_header=str(base64.b64encode((user + ":" + password).encode('utf-8')), 'utf-8'))
    except Exception as e:
        raise common.FwLoginFailed(
            "FortiManager login ERROR: host=" + str(api_host) + ":" + str(api_port) + " Message: " + str(e)) from None
    if headers.get("X-auth-access-token") == None:   # leaving out payload as it contains pwd
        raise common.FwLoginFailed(
            "FortiManager login ERROR: host=" + str(api_host) + ":" + str(api_port)) from None
    if fwo_globals.debug_level > 2:
        logger = getFwoLogger()
        logger.debug("Login successful. Received auth token: " + headers["X-auth-access-token"])
    return headers.get("X-auth-access-token")

def logout(v_url, sid, method='exec'):
    logger = getFwoLogger()
    payload = {"params": [{}]}

    response = api_call(v_url, 'sys/logout', payload, sid, method=method)
    if "result" in response and "status" in response["result"][0] and "code" in response["result"][0]["status"] and response["result"][0]["status"]["code"] == 0:
        logger.debug("successfully logged out")
    else:
        raise Exception("cifp_getter ERROR: did not get status code 0 when logging out, " +
                        "api call: url: " + str(v_url) + ",  + payload: " + str(payload))


def set_api_url(base_url, testmode, api_supported, hostname):
    url = ''
    if testmode == 'off':
        url = base_url
    else:
        if re.search(r'^\d+[\.\d+]+$', testmode) or re.search(r'^\d+$', testmode):
            if testmode in api_supported:
                url = base_url + 'v' + testmode + '/'
            else:
                raise Exception("api version " + testmode +
                                " is not supported by the manager " + hostname + " - Import is canceled")
        else:
            raise Exception("\"" + testmode + "\" - not a valid version")
    return url


def update_config_with_fortinet_api_call(config_json, sid, api_base_url, api_path, result_name, payload={}, options=[], show_progress=False, limit=150, method="get"):
    offset = 0
    limit = int(limit)
    returned_new_objects = True
    full_result = []
    while returned_new_objects:
        range = [offset, limit]
        if payload == {}:
            payload = {"params": [{'range': range}]}
        else:
            if 'params' in payload and len(payload['params']) > 0:
                payload['params'][0].update({'range': range})

        # adding options
        if len(options) > 0:
            payload['params'][0].update({'option': options})
            # payload['params'][0].update({'filter': options})

        result = fortinet_api_call(
            sid, api_base_url, api_path, payload=payload, show_progress=show_progress, method=method)
        full_result.extend(result)
        offset += limit
        if len(result) < limit:
            returned_new_objects = False

    if result_name in config_json:  # data already exists - extend
        config_json[result_name].extend(full_result)
    else:
        config_json.update({result_name: full_result})


def fortinet_api_call(sid, api_base_url, api_path, payload={}, show_progress=False, method="get"):
    if payload == {}:
        payload = {"params": [{}]}
    result = api_call(api_base_url, api_path, payload, sid, method=method)
    plain_result = result["result"][0]
    if "data" in plain_result:
        result = plain_result["data"]
    else:
        result = {}
    return result

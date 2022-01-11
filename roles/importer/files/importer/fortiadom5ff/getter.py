# library for API get functions
import re
import logging
import requests.packages
import requests
import json
import sys
sys.path.append(r"/usr/local/fworch/importer")


def api_call(url, command, json_payload, sid, ssl_verification='', proxy_string='', show_progress=False, method='', debug=0):
    request_headers = {'Content-Type': 'application/json'}
    if sid != '':
        json_payload.update({"session": sid})
    if command != '':
        for p in json_payload['params']:
            p.update({"url": command})
    if method == '':
        method = 'get'
    json_payload.update({"method": method})

    r = requests.post(url, data=json.dumps(
        json_payload), headers=request_headers, verify=ssl_verification, proxies=proxy_string)
    if r is None:
        if 'pass' in json.dumps(json_payload):
            logging.exception("\nerror while sending api_call containing credential information to url '" + str(url))
        else:
            logging.exception("\nerror while sending api_call to url '" + str(url) + "' with payload '" + json.dumps(json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2))
        sys.exit(1)
    result_json = r.json()
    if 'result' not in result_json or \
        len(result_json['result'])<1 or \
        'status' not in result_json['result'][0] \
        or 'code' not in result_json['result'][0]['status'] or \
        result_json['result'][0]['status']['code'] != 0:
        if 'pass' in json.dumps(json_payload):
            logging.exception("\nerror while sending api_call containing credential information to url '" + str(url))
        else:
            if 'status' in result_json['result'][0]:
                logging.exception("\nerror while sending api_call to url '" + str(url) + "' with payload '" +
                          json.dumps(json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2) + ', result=' + json.dumps(r.json()['result'][0]['status'], indent=2))
            else:
                logging.exception("\nerror while sending api_call to url '" + str(url) + "' with payload '" +
                          json.dumps(json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2) + ', result=' + json.dumps(r.json()['result'][0], indent=2))
 
#    if logging.DEBUG:
    if 'pass' in json.dumps(json_payload):
        logging.debug("api_call containing credential information to url '" + str(url) + " - not logging query")
    else:
        logging.debug("api_call to url '" + str(url) + "' with payload '" + json.dumps(
            json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2))

    if show_progress:
        print('.', end='', flush=True)
    return r.json()


def login(user, password, api_host, api_port, domain, ssl_verification, proxy_string, debug=0):
    payload = {
        "id": 1,
        "params": [
            {
                "data": [
                    {
                        "user": user,
                        "passwd": password,
                    }
                ]
            }
        ]
    }
    base_url = 'https://' + api_host + ':' + str(api_port) + '/jsonrpc'
    response = api_call(base_url, 'sys/login/user', payload, '', ssl_verification=ssl_verification,
                        proxy_string=proxy_string, method="exec", debug=debug)
    if "session" not in response:   # leaving out payload as it contains pwd
        logging.exception("\ngetter ERROR: did not receive a session id during login, " +
                          "api call: api_host: " + str(api_host) + ", api_port: " + str(api_port) + ", base_url: " + str(base_url) +
                          ", ssl_verification: " + str(ssl_verification) + ", proxy_string: " + str(proxy_string))
        sys.exit(1)
    return response["session"]


def logout(v_url, sid, ssl_verification='', proxy_string='', debug=0, method='exec'):
    payload = {"params": [{}]}

    response = api_call(v_url, 'sys/logout', payload, sid, ssl_verification=ssl_verification,
                        proxy_string=proxy_string, method="exec", debug=debug)
    if "result" in response and "status" in response["result"][0] and "code" in response["result"][0]["status"] and response["result"][0]["status"]["code"] == 0:
        logging.debug("\nsuccessfully logged out")
    else:
        logging.warning("\ngetter ERROR: did not get status code 0 when logging out, " +
                          "api call: url: " + str(v_url) + ",  + payload: " + str(payload) + ", ssl_verification: " + str(ssl_verification) + ", proxy_string: " + str(proxy_string))
        sys.exit(1)


def set_ssl_verification(ssl_verification_mode):
    logger = logging.getLogger(__name__)
    if ssl_verification_mode == '' or ssl_verification_mode == 'off':
        ssl_verification = False
        logger.debug("ssl_verification: False")
    else:
        ssl_verification = ssl_verification_mode
        logger.debug("ssl_verification: [ca]certfile=" + ssl_verification)
    return ssl_verification


def set_api_url(base_url, testmode, api_supported, hostname):
    logger = logging.getLogger(__name__)
    url = ''
    if testmode == 'off':
        url = base_url
    else:
        if re.search(r'^\d+[\.\d+]+$', testmode) or re.search(r'^\d+$', testmode):
            if testmode in api_supported:
                url = base_url + 'v' + testmode + '/'
            else:
                logger.debug("api version " + testmode +
                             " is not supported by the manager " + hostname + " - Import is canceled")
                sys.exit("api version " + testmode + " not supported")
        else:
            logger.debug("not a valid version")
            sys.exit("\"" + testmode + "\" - not a valid version")
    logger.debug("testmode: " + testmode + " - url: " + url)
    return url


    #   "option": [
    #     "get reserved"
    #   ]


def update_config_with_fortinet_api_call(config_json, sid, api_base_url, api_path, result_name, payload={}, options=[], ssl_verification='', proxy_string="", show_progress=False, debug=0, limit=150):
    offset = 0
    limit = int(limit)
    returned_new_objects = True
    full_result = []
    while returned_new_objects:
        range = [offset, limit]
        if payload == {}:
            payload = {"params": [{'range': range}]}
        else:
            if 'params' in payload and len(payload['params'])>0:
                payload['params'][0].update({'range': range})
        
        # adding options
        if len(options)>0:
            payload['params'][0].update({'option': options})
            # payload['params'][0].update({'filter': options})

        result = fortinet_api_call(sid, api_base_url, api_path, payload=payload, ssl_verification=ssl_verification,
                                   proxy_string=proxy_string, show_progress=show_progress, debug=debug)
        full_result.extend(result)
        offset += limit
        if len(result)<limit:
            returned_new_objects = False

    if result_name in config_json:  # data already exists - extend
        config_json[result_name].extend(full_result)
    else:
        config_json.update({result_name: full_result})


def fortinet_api_call(sid, api_base_url, api_path, payload={}, ssl_verification='', proxy_string="", show_progress=False, debug=0):
    if payload == {}:
        payload = {"params": [{}]}
    result = api_call(api_base_url, api_path, payload, sid,
                      ssl_verification, proxy_string, debug=debug)
    plain_result = result["result"][0]
    if "data" in plain_result:
        result = plain_result["data"]
    else:
        result = {}
    return result

# library for FWORCH API calls
import re
import logging
import requests.packages
import requests
import json
import sys
base_dir = "/usr/local/fworch"
sys.path.append(base_dir + '/importer')

requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

details_level = "full"    # 'standard'
use_object_dictionary = 'false'

# call(fwo_api_base_url, jwt, lock_mutation, query_variables=query_variables);


def call(url, jwt, query, query_variables="", role="reporter", ssl_verification='', proxy_string='', show_progress=False, method='', debug=0):
    request_headers = {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer ' + jwt,
        'x-hasura-role': role
    }
    full_query = {"variables": query_variables, "query": query}

    try:
        r = requests.post(url, data=json.dumps(
            full_query), headers=request_headers, verify=ssl_verification, proxies=proxy_string)
        r.raise_for_status()
    except requests.exceptions.RequestException as e:
        logging.exception("\nerror while sending api_call to url " + str(url) + " with payload \n" +
                          json.dumps(full_query, indent=2) + "\n and  headers: \n" + json.dumps(request_headers, indent=2))
        raise SystemExit(e) from None

    if debug > 0:
        logging.debug("\napi_call to url '" + str(url) + "' with payload '" + json.dumps(query, indent=2) + "' and headers: '" +
                      json.dumps(request_headers, indent=2))
    if show_progress:
        print('.', end='', flush=True)
    return r.json()


def login(user, password, user_management_api_base_url, method, ssl_verification=False, proxy_string='', debug=0):
    payload = {"Username": user, "Password": password}
    request_headers = {'Content-Type': 'application/json'}

    try:
        response = requests.post(user_management_api_base_url + method, data=json.dumps(
            payload), headers=request_headers, verify=ssl_verification, proxies=proxy_string)
        response.raise_for_status()
        #content = response.content
    except requests.exceptions.RequestException as e:
        logging.exception("fwo_api: error while sending api_call to url " + str(user_management_api_base_url) + " with payload \n" +
                          json.dumps(payload, indent=2) + "\n and  headers: \n" + json.dumps(request_headers, indent=2))
        raise SystemExit(e) from None

    if response.text is not None:
        return response.text
    else:
        logging.exception("fwo_api: getter ERROR: did not receive a JWT during login, " +
                        ", api_url: " + str(user_management_api_base_url) + ", payload: " + str(payload) +
                        ", ssl_verification: " + str(ssl_verification) + ", proxy_string: " + str(proxy_string))
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


def get_api_url(sid, api_host, api_port, user, base_url, limit, test_version, ssl_verification, proxy_string):
    return base_url + '/jsonrpc'


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

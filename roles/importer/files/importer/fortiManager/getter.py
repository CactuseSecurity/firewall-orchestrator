#!/usr/bin/python3
# library for API get functions
import json, argparse, pdb
import time, logging, re, sys, logging
import os
import requests, requests.packages.urllib3
import common

requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

details_level = "full"    # 'standard'
use_object_dictionary = 'false'

# all obj table names to look at:
# api_obj_types = [
#     'hosts', 'networks', 'groups', 'address-ranges', 'multicast-address-ranges', 'groups-with-exclusion', 'gateways-and-servers',
#     'security-zones', 'dynamic-objects', 'trusted-clients', 'dns-domains',
#     'services-tcp', 'services-udp', 'services-sctp', 'services-other', 'service-groups', 'services-dce-rpc', 'services-rpc', 'services-icmp', 'services-icmp6' ]

# svc_obj_table_names = ['services-tcp', 'services-udp', 'service-groups', 'services-dce-rpc', 'services-rpc', 'services-other', 'services-icmp', 'services-icmp6']
# # usr_obj_table_names : do not exist yet - not fetchable via API


def api_call(ip_addr, port, url, command, json_payload, sid, ssl_verification, proxy_string, show_progress=False):
    request_headers = {'Content-Type' : 'application/json'}
    if sid != '':
        json_payload.update({"session": sid})
    if command != '':
        for p in json_payload['params']:
            p.update({"url": command})
    r = requests.post(url, data=json.dumps(json_payload), headers=request_headers, verify=ssl_verification, proxies=proxy_string)
    if r is None:
        logging.exception("\nerror while sending api_call to url '" + str(url) + "' with payload '" + json.dumps(json_payload, indent=2) + "' and  headers: '" + json.dumps(request_headers, indent=2))
        sys.exit(1)
    if show_progress:
        print ('.', end='', flush=True)
    return r.json()


def login(user,password,api_host,api_port,domain, ssl_verification, proxy_string):
    payload = {
        "id": 1,
        "method": "exec",
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
    base_url = 'https://' + api_host + ':' + api_port + '/jsonrpc'
    response = api_call(api_host, api_port, base_url, 'sys/login/user', payload, '', ssl_verification, proxy_string)
    if "session" not in response:
        logging.exception("\ngetter ERROR: did not receive a session id during login, " +
            "api call: api_host: " + str(api_host) + ", api_port: " + str(api_port) + ", base_url: " + str(base_url) + ", payload: " + str(payload) +
            ", ssl_verification: " + str(ssl_verification) + ", proxy_string: " + str(proxy_string))
        sys.exit(1)
    return response["session"]


def set_ssl_verification(ssl_verification_mode):
    logger = logging.getLogger(__name__)
    if ssl_verification_mode == '' or ssl_verification_mode == 'off':
        ssl_verification = False
        logger.debug ("ssl_verification: False")
    else:
        ssl_verification = ssl_verification_mode
        logger.debug ("ssl_verification: [ca]certfile="+ ssl_verification )
    return ssl_verification


def get_api_url(sid, api_host, api_port, user, base_url, limit, test_version, ssl_verification, proxy_string):
    return base_url + '/jsonrpc'


def set_api_url(base_url,testmode,api_supported,hostname):
    logger = logging.getLogger(__name__)
    url = ''
    if testmode == 'off':
        url = base_url
    else:
        if re.search(r'^\d+[\.\d+]+$', testmode) or re.search(r'^\d+$', testmode):
            if testmode in api_supported :
                url = base_url + 'v' + testmode + '/'
            else:
                logger.debug ("api version " + testmode + " is not supported by the manager " + hostname + " - Import is canceled")
                sys.exit("api version " + testmode +" not supported")
        else:
            logger.debug ("not a valid version")
            sys.exit("\"" + testmode +"\" - not a valid version")
    logger.debug ("testmode: " + testmode + " - url: "+ url)
    return url

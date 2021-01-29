#!/usr/bin/python3
import fworch_logging_cp_r8x_api.py as log
import requests, json
import sys, re
import warnings
#import requests.packages.urllib3
#requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

warnings.filterwarnings('ignore', message='Unverified HTTPS request')

def api_call(ip_addr, port, url, command, json_payload, ssl, proxy, sid):
    url = url + command
    if sid == '':
        request_headers = {'Content-Type' : 'application/json'}
    else:
        request_headers = {'Content-Type' : 'application/json', 'X-chkp-sid' : sid}
    r = requests.post(url, data=json.dumps(json_payload), headers=request_headers, verify=ssl, proxies=proxy)
    return r.json()

def login(user,password,host,port,domain,url,ssl, proxy):
    if domain == '':
        payload = {'user':user, 'password' : password}
    else:
        payload = {'user':user, 'password' : password, 'domain' :  domain}
    response = api_call(host, port, url, 'login', payload, ssl, proxy, '')
    return response["sid"]

def set_ssl_verification(ssl_verification_mode):
    if ssl_verification_mode == '' or ssl_verification_mode == 'off':
        ssl_verification = False
        log.logging.debug ("fworch_session_cp_r8x_api - ssl_verification: False")
    else:
        ssl_verification = ssl_verification_mode
        log.logging.debug ("fworch_session_cp_r8x_api - ssl_verification: [ca]certfile="+ ssl_verification )
    return ssl_verification

def set_api_url(base_url,testmode,api_supported,hostname):
    url = ''
    if testmode == 'off':
        url = base_url
    else:
        if re.search(r'^\d+[\.\d+]+$', testmode) or re.search(r'^\d+$', testmode):
            if testmode in api_supported :
                url = base_url + 'v' + testmode + '/'
            else:
                log.logging.debug ("fworch_session_cp_r8x_api - api version " + testmode + " is not supported by the manager " + hostname + " - Import is canceled")
                sys.exit("api version " + testmode +" not supported")
        else:
            log.logging.debug ("fworch_session_cp_r8x_api - not a valid version")
            sys.exit("\"" + testmode +"\" - not a valid version")
    log.logging.debug ("fworch_session_cp_r8x_api - testmode: " + testmode + " - url: "+ url)
    return url

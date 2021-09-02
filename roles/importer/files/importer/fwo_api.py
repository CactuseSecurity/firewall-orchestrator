# library for FWORCH API calls
import sys
sys.path.append(r"/usr/local/fworch/importer")
import json, argparse, pdb
import time, logging, re, sys, logging
import os
import requests, requests.packages.urllib3
import common

requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

details_level = "full"    # 'standard'
use_object_dictionary = 'false'

# call(fwo_api_base_url, jwt, lock_mutation, query_variables=query_variables); 

def call(url, jwt, query, query_variables="", role="reporter", ssl_verification='', proxy_string='', show_progress=False, method='', debug=0):
    request_headers = {
            'Content-Type' : 'application/json',
            'Authorization': 'Bearer ' + jwt,
            'x-hasura-role': role
        }
    full_query={"variables": query_variables, "query": query}

    try:
        r = requests.post(url, data=json.dumps(full_query), headers=request_headers, verify=ssl_verification, proxies=proxy_string)
        r.raise_for_status()
        content = r.content
    except requests.exceptions.RequestException as e:
        logging.exception("\nerror while sending api_call to url " + str(url) + " with payload \n" + 
            json.dumps(full_query, indent=2) + "\n and  headers: \n" + json.dumps(request_headers, indent=2))
        raise SystemExit(e) from None

    if debug>0:
        logging.debug("\napi_call to url '" + str(url) + "' with payload '" + json.dumps(query, indent=2) + "' and headers: '" + 
            json.dumps(request_headers, indent=2))
    if show_progress:
        print ('.', end='', flush=True)
    return r.json()


def login(user, password, user_management_api_base_url, method, ssl_verification=False, proxy_string='', debug=0):
    payload = { "Username":user, "Password": password }
    request_headers = {'Content-Type' : 'application/json'}

    try:
        response = requests.post(user_management_api_base_url + method, data=json.dumps(payload), headers=request_headers, verify=ssl_verification, proxies=proxy_string)
        response.raise_for_status()
        #content = response.content
    except requests.exceptions.RequestException as e:
        logging.exception("\nerror while sending api_call to url " + str(user_management_api_base_url) + " with payload \n" + 
            json.dumps(payload, indent=2) + "\n and  headers: \n" + json.dumps(request_headers, indent=2))
        raise SystemExit(e) from None

    jsonResponse = response.json()
    if 'jwt' in jsonResponse:
        return jsonResponse["jwt"]        
    logging.exception("\ngetter ERROR: did not receive a JWT during login, " +
        ", api_url: " + str(user_management_api_base_url) + ", payload: " + str(payload) +
        ", ssl_verification: " + str(ssl_verification) + ", proxy_string: " + str(proxy_string))
    sys.exit(1)


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


def update_config_with_fortinet_api_call(config_json, sid, api_base_url, api_path, result_name, payload={}, ssl_verification='', proxy_string="", show_progress=False, debug=0):
    result = fortinet_api_call(sid, api_base_url, api_path, payload=payload, ssl_verification=ssl_verification, proxy_string=proxy_string, show_progress=show_progress, debug=debug)
    config_json.update({result_name: result})


def fortinet_api_call(sid, api_base_url, api_path, payload={}, ssl_verification='', proxy_string="", show_progress=False, debug=0):
    if payload=={}:
        payload = { "params": [ {} ] }
    result = call(api_base_url, api_path, payload, sid, ssl_verification, proxy_string, debug=debug)
    plain_result = result["result"][0]
    if "data" in plain_result:
        result = plain_result["data"]
    else:
        result = {}
    return result


def get_mgm_details(fwo_api_base_url, jwt, query_variables):

    mgm_query="""
        query getManagementDetails($mgmId: Int!) {
            management(where:{mgm_id:{_eq:$mgmId}} order_by: {mgm_name: asc}) {
                id: mgm_id
                name: mgm_name
                hostname: ssh_hostname
                port: ssh_port
                secret: ssh_private_key
                sshPublicKey: ssh_public_key
                user: ssh_user
                deviceType: stm_dev_typ {
                    id: dev_typ_id
                    name: dev_typ_name
                    version: dev_typ_version
                }
                configPath: config_path
                importDisabled: do_not_import
                forceInitialImport: force_initial_import
                hideInUi: hide_in_gui
                importerHostname: importer_hostname
                comment: mgm_comment
                debugLevel: debug_level
                creationDate: mgm_create
                updateDate: mgm_update
                lastConfigHash: last_import_md5_complete_config
                devices {
                    id: dev_id
                    name: dev_name
                    rulebase:dev_rulebase
                }
            }  
        }
    """
    return call(fwo_api_base_url, jwt, mgm_query, query_variables=query_variables, role='importer'); 


def lock_import (fwo_api_base_url, jwt, query_variables):
    lock_mutation = "mutation lockImport($mgmId: Int!) { insert_import_control(objects: {mgm_id: $mgmId}) { returning { control_id } } }"
    try:
        lock_result = call(fwo_api_base_url, jwt, lock_mutation, query_variables=query_variables, role='importer'); 
        current_import_id = lock_result['data']['insert_import_control']['returning'][0]['control_id']
    except: 
        logging.exception("failed to get import lock for management id " + str(query_variables))
        return -1
    return current_import_id


def unlock_import (fwo_api_base_url, jwt, mgm_id, stop_time, current_import_id, error_occured):
    query_variables={ "stopTime": stop_time, "importId": current_import_id, "success": not error_occured }

    unlock_mutation = """
        mutation unlockImport($importId: bigint!, $stopTime: timestamp!, $success: Boolean) {
            update_import_control(where: {control_id: {_eq: $importId}}, _set: {stop_time: $stopTime, successful_import: $success}) {
                affected_rows
            }
        }"""

    try:
        unlock_result = call(fwo_api_base_url, jwt, unlock_mutation, query_variables=query_variables, role='importer'); 
        changes_in_import_control = unlock_result['data']['update_import_control']['affected_rows']
    except: 
        logging.exception("failed to unlock import for management id " + str(mgm_id))
        changes_in_import_control = 0
    return changes_in_import_control


def import_json_config(fwo_api_base_url, jwt, mgm_id, query_variables):
    import_mutation = """
        mutation import($importId: bigint!, $config: jsonb) {
            insert_import_config(objects: {import_id: $importId, config: $config}) {
                affected_rows
            }
        }
    """

    try:
        import_result = call(fwo_api_base_url, jwt, import_mutation, query_variables=query_variables, role='importer'); 
        changes_in_import_control = import_result['data']['insert_import_config']['affected_rows']
    except: 
        logging.exception("failed to write config for mgm id " + str(mgm_id))
        changes_in_import_control = 0
    return changes_in_import_control


def store_full_json_config(fwo_api_base_url, jwt, mgm_id, query_variables):
    import_mutation = """
        mutation store_full_config($importId: bigint!, $config: jsonb) {
            insert_import_full_config(objects: {import_id: $importId, config: $config}) {
                affected_rows
            }
        }
    """

    try:
        import_result = call(fwo_api_base_url, jwt, import_mutation, query_variables=query_variables, role='importer'); 
        changes_in_import_full_config = import_result['data']['insert_import_full_config']['affected_rows']
    except: 
        logging.exception("failed to write full config for mgm id " + str(mgm_id))
        changes_in_import_full_config = 0
    return changes_in_import_full_config

# check_import_lock = """query runningImportForManagement($mgmId: Int!) {
#   import_control(where: {mgm_id: {_eq: $mgmId}, stop_time: {_is_null: true}}) {
#     control_id
#   }
# }"""
# response = fwo_api.call(fwo_api_base_url, jwt, check_import_lock, query_variables=query_variables, role='importer', ssl_verification=ssl_mode, proxy_string=proxy_setting)
# if 'data' in response and 'import_control' in response['data'] and len(response['data']['import_control'])==1:
#     logging.exception("\nERROR: import for management already running.")
#     sys.exit(1)    
# if 'data' in response and 'import_control' in response['data'] and len(response['data']['import_control'])==0:

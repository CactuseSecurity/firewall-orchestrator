# library for FWORCH API calls
import re
import logging
import requests.packages
import requests
import json
import sys
base_dir = "/usr/local/fworch/"
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
    except requests.exceptions.RequestException as e:
        logging.exception("\nfwo_api: error during login, url: " + str(user_management_api_base_url))
        raise SystemExit(e) from None

    if response.text is not None:
        return response.text
    else:
        logging.exception("\nfwo_api: getter ERROR: did not receive a JWT during login, " +
                        ", api_url: " + str(user_management_api_base_url) +
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


# def update_config_with_fortinet_api_call(config_json, sid, api_base_url, api_path, result_name, payload={}, ssl_verification='', proxy_string="", show_progress=False, debug=0):
#     result = fortinet_api_call(sid, api_base_url, api_path, payload=payload, ssl_verification=ssl_verification,
#                                proxy_string=proxy_string, show_progress=show_progress, debug=debug)
#     config_json.update({result_name: result})


# def fortinet_api_call(sid, api_base_url, api_path, payload={}, ssl_verification='', proxy_string="", show_progress=False, debug=0):
#     if payload == {}:
#         payload = {"params": [{}]}
#     result = call(api_base_url, api_path, payload, sid,
#                   ssl_verification, proxy_string, debug=debug)
#     plain_result = result["result"][0]
#     if "data" in plain_result:
#         result = plain_result["data"]
#     else:
#         result = {}
#     return result


def get_mgm_ids(fwo_api_base_url, jwt, query_variables):
    mgm_query = """
        query getManagementIds {
            management(where:{do_not_import:{_eq:false}} order_by: {mgm_name: asc}) {
                id: mgm_id } } """
    return call(fwo_api_base_url, jwt, mgm_query, query_variables=query_variables, role='importer')['data']['management']


def get_mgm_details(fwo_api_base_url, jwt, query_variables):
    mgm_query = """
        query getManagementDetails($mgmId: Int!) {
            management(where:{mgm_id:{_eq:$mgmId}, do_not_import:{_eq:false}} order_by: {mgm_name: asc}) {
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
                importerHostname: importer_hostname
                debugLevel: debug_level
                lastConfigHash: last_import_md5_complete_config
                devices(where:{do_not_import:{_eq:false}}) {
                    id: dev_id
                    name: dev_name
                    local_rulebase_name
                    global_rulebase_name
                    package_name
                }
                import_controls(where: { successful_import: {_eq: true} } order_by: {control_id: desc}, limit: 1) {
                    starttime: start_time
                }
            }  
        }
    """
    api_call_result = call(fwo_api_base_url, jwt, mgm_query, query_variables=query_variables, role='importer')
    if 'data' in api_call_result and 'management' in api_call_result['data'] and len(api_call_result['data']['management'])>=1:
        return api_call_result['data']['management'][0]
    else:
        logging.error('did not succeed in getting management details from API')
        sys.exit(1)


def lock_import(fwo_api_base_url, jwt, query_variables):
    lock_mutation = "mutation lockImport($mgmId: Int!) { insert_import_control(objects: {mgm_id: $mgmId}) { returning { control_id } } }"
    try:
        lock_result = call(fwo_api_base_url, jwt, lock_mutation,
                           query_variables=query_variables, role='importer')
        current_import_id = lock_result['data']['insert_import_control']['returning'][0]['control_id']
    except:
        logging.exception(
            "fwo_api: failed to get import lock for management id " + str(query_variables))
        return -1
    return current_import_id


def count_changes_per_import(fwo_api_base_url, jwt, import_id):
    change_count_query = """
        query count_changes($importId: bigint!) {
            changelog_object_aggregate(where: {control_id: {_eq: $importId}}) { aggregate { count } }
            changelog_service_aggregate(where: {control_id: {_eq: $importId}}) { aggregate { count } }
            changelog_user_aggregate(where: {control_id: {_eq: $importId}}) { aggregate { count } }
            changelog_rule_aggregate(where: {control_id: {_eq: $importId}}) { aggregate { count } }
        }"""
    try:
        count_result = call(fwo_api_base_url, jwt, change_count_query, query_variables={
                            'importId': import_id}, role='importer')
        changes_in_import = int(count_result['data']['changelog_object_aggregate']['aggregate']['count']) + \
            int(count_result['data']['changelog_service_aggregate']['aggregate']['count']) + \
            int(count_result['data']['changelog_user_aggregate']['aggregate']['count']) + \
            int(count_result['data']['changelog_rule_aggregate']
                ['aggregate']['count'])
    except:
        logging.exception(
            "fwo_api: failed to count changes for import id " + str(import_id))
        changes_in_import = 0
    return changes_in_import


def unlock_import(fwo_api_base_url, jwt, mgm_id, stop_time, current_import_id, error_count, change_count):
    query_variables = {"stopTime": stop_time, "importId": current_import_id,
                       "success": error_count == 0, "changesFound": change_count > 0}

    unlock_mutation = """
        mutation unlockImport($importId: bigint!, $stopTime: timestamp!, $success: Boolean, $changesFound: Boolean!) {
            update_import_control(where: {control_id: {_eq: $importId}}, _set: {stop_time: $stopTime, successful_import: $success, changes_found: $changesFound}) {
                affected_rows
            }
        }"""

    try:
        unlock_result = call(fwo_api_base_url, jwt, unlock_mutation,
                             query_variables=query_variables, role='importer')
        changes_in_import_control = unlock_result['data']['update_import_control']['affected_rows']
    except:
        logging.exception(
            "fwo_api: failed to unlock import for management id " + str(mgm_id))
        changes_in_import_control = 0
    return changes_in_import_control-1


# this effectively clears the management!
def delete_import(fwo_api_base_url, jwt, current_import_id):
    query_variables = {"importId": current_import_id}

    delete_import_mutation = """
        mutation deleteImport($importId: bigint!) {
            delete_import_control(where: {control_id: {_eq: $importId}}) { affected_rows }
        }"""

    try:
        result = call(fwo_api_base_url, jwt, delete_import_mutation,
                      query_variables=query_variables, role='importer')
        api_changes = result['data']['delete_import_control']['affected_rows']
    except:
        logging.exception(
            "fwo_api: failed to unlock import for import id " + str(current_import_id))
        return 1  # signalling an error
    if api_changes == 1:
        return 0        # return code 0 is ok
    else:
        return 1


def import_json_config(fwo_api_base_url, jwt, mgm_id, query_variables):
    import_mutation = """
        mutation import($importId: bigint!, $mgmId: Int!, $config: jsonb!) {
            insert_import_config(objects: {import_id: $importId, mgm_id: $mgmId, config: $config}) {
                affected_rows
            }
        }
    """

    try:
        import_result = call(fwo_api_base_url, jwt, import_mutation,
                             query_variables=query_variables, role='importer')
        if 'errors' in import_result:
            logging.exception("fwo_api:import_json_config - error while writing importable config for mgm id " +
                              str(mgm_id) + ": " + str(import_result['errors']))
        changes_in_import_control = import_result['data']['insert_import_config']['affected_rows']
    except:
        logging.exception(
            "fwo_api: failed to write importable config for mgm id " + str(mgm_id))
        return 2  # indicating 1 error
    return changes_in_import_control-1


def delete_json_config(fwo_api_base_url, jwt, query_variables):
    delete_mutation = """
        mutation delete_import_config($importId: bigint!) {
            delete_import_config(where: {import_id: {_eq: $importId}}) { affected_rows }
        }
    """

    try:
        delete_result = call(fwo_api_base_url, jwt, delete_mutation,
                             query_variables=query_variables, role='importer')
        changes_in_delete_config = delete_result['data']['delete_import_config']['affected_rows']
    except:
        logging.exception("fwo_api: failed to delete config without changes")
        return 2  # indicating 1 error
    return changes_in_delete_config-1


def store_full_json_config(fwo_api_base_url, jwt, mgm_id, query_variables):
    import_mutation = """
        mutation store_full_config($importId: bigint!, $mgmId: Int!, $config: jsonb!) {
            insert_import_full_config(objects: {import_id: $importId, mgm_id: $mgmId, config: $config}) {
                affected_rows
            }
        }
    """

    try:
        import_result = call(fwo_api_base_url, jwt, import_mutation,
                             query_variables=query_variables, role='importer')
        changes_in_import_full_config = import_result['data']['insert_import_full_config']['affected_rows']
    except:
        logging.exception(
            "fwo_api: failed to write full config for mgm id " + str(mgm_id))
        return 2  # indicating 1 error because we are expecting exactly one change
    return changes_in_import_full_config-1


def delete_full_json_config(fwo_api_base_url, jwt, query_variables):
    delete_mutation = """
        mutation delete_import_full_config($importId: bigint!) {
            delete_import_full_config(where: {import_id: {_eq: $importId}}) {
                affected_rows
            }
        }
    """

    try:
        delete_result = call(fwo_api_base_url, jwt, delete_mutation,
                             query_variables=query_variables, role='importer')
        changes_in_delete_full_config = delete_result['data']['delete_import_full_config']['affected_rows']
    except:
        logging.exception("fwo_api: failed to delete full config ")
        return 2  # indicating 1 error
    return changes_in_delete_full_config-1


def get_error_string_from_imp_control(fwo_api_base_url, jwt, query_variables):
    error_query = "query getErrors($importId:bigint) { import_control(where:{control_id:{_eq:$importId}}) { import_errors } }"
    return call(fwo_api_base_url, jwt, error_query, query_variables=query_variables, role='importer')['data']['import_control']

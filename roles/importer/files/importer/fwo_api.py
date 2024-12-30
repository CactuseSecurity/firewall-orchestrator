# library for FWORCH API calls
import re
import traceback
import requests.packages
import requests
import json
import datetime
import time

from fwo_const import fwo_config_filename
from fwo_log import getFwoLogger
import fwo_globals
import fwo_const
from fwo_const import fwo_api_http_import_timeout
from fwo_exception import FwoApiServiceUnavailable, FwoApiTimeout, FwoApiLoginFailed, \
    SecretDecryptionFailed, FwoApiFailedLockImport
from fwo_base import writeAlertToLogFile
from fwo_encrypt import decrypt

def showApiCallInfo(url, query, headers, typ='debug'):
    max_query_size_to_display = 1000
    query_string = json.dumps(query, indent=2)
    header_string = json.dumps(headers, indent=2)
    query_size = len(query_string)

    if typ=='error':
        result = "error while sending api_call to url "
    else:
        result = "successful FWO API call to url "        
    result += str(url) + " with payload \n"
    if query_size < max_query_size_to_display:
        result += query_string 
    else:
        result += str(query)[:round(max_query_size_to_display/2)] +   "\n ... [snip] ... \n" + \
            query_string[query_size-round(max_query_size_to_display/2):] + " (total query size=" + str(query_size) + " bytes)"
    result += "\n and  headers: \n" + header_string
    return result


# standard FWO API call
def call(url, jwt, query, query_variables="", role="reporter", show_progress=False, method=''):
    request_headers = { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + jwt, 'x-hasura-role': role }
    full_query = {"query": query, "variables": query_variables}
    logger = getFwoLogger()

    with requests.Session() as session:
        if fwo_globals.verify_certs is None:    # only for first FWO API call (getting info on cert verification)
            session.verify = False
        else: 
            session.verify = fwo_globals.verify_certs
        session.headers = request_headers

        try:
            r = session.post(url, data=json.dumps(full_query), timeout=int(fwo_api_http_import_timeout))
            r.raise_for_status()
        except requests.exceptions.HTTPError as http_err:
            logger.error(showApiCallInfo(url, full_query, request_headers, type='error') + ":\n" + str(traceback.format_exc()))
            print(f"HTTP error occurred: {http_err}")  
            if http_err.errno == 503:
                raise FwoApiServiceUnavailable("FWO API HTTP error 503 (FWO API died?)" )
            if http_err.errno == 502:
                raise FwoApiTimeout("FWO API HTTP error 502 (might have reached timeout of " + str(int(fwo_api_http_import_timeout)/60) + " minutes)" )
            else:
                raise
        except Exception as err:
            print(f"Other error occurred: {err}")

        if int(fwo_globals.debug_level) > 4:
            logger.debug (showApiCallInfo(url, full_query, request_headers, type='debug'))
        if show_progress:
            pass
            # print('.', end='', flush=True)
        if r is not None:
            return r.json()
        else:
            return None


def login(user, password, user_management_api_base_url, method='api/AuthenticationToken/Get'):
    payload = {"Username": user, "Password": password}

    with requests.Session() as session:
        if fwo_globals.verify_certs is None:    # only for first FWO API call (getting info on cert verification)
            session.verify = False
        else: 
            session.verify = fwo_globals.verify_certs
        session.headers = {'Content-Type': 'application/json'}

        try:
            response = session.post(user_management_api_base_url + method, data=json.dumps(payload))
        except requests.exceptions.RequestException:
            raise FwoApiLoginFailed ("fwo_api: error during login to url: " + str(user_management_api_base_url) + " with user " + user) from None

        if response.text is not None and response.status_code==200:
            return response.text
        else:
            error_txt = "fwo_api: ERROR: did not receive a JWT during login" + \
                            ", api_url: " + str(user_management_api_base_url) + \
                            ", ssl_verification: " + str(fwo_globals.verify_certs)
            raise FwoApiLoginFailed(error_txt)


def set_api_url(base_url, testmode, api_supported, hostname):
    logger = getFwoLogger()
    url = ''
    if testmode == 'off':
        url = base_url
    else:
        if re.search(r'^\d+[\.\d+]+$', testmode) or re.search(r'^\d+$', testmode):
            if testmode in api_supported:
                url = base_url + 'v' + testmode + '/'
            else:
                exception_text = "api version " + testmode + \
                             " is not supported by the manager " + hostname + " - Import is canceled"
                raise Exception(exception_text)
        else:
            raise Exception("\"" + testmode + "\" - not a valid version")
    logger.debug("testmode: " + testmode + " - url: " + url)
    return url


def get_mgm_ids(fwo_api_base_url, jwt, query_variables):
    # from 9.0 do not import sub-managers separately
    mgm_query = """
    query getManagementIds {
        management(where: {multi_device_manager_id: {_is_null: true}, do_not_import: {_eq: false}}, order_by: {is_super_manager: desc}) {
            id: mgm_id
            subManager: managementByMultiDeviceManagerId {
                mgm_id
            }
        }
    }
    """
    return call(fwo_api_base_url, jwt, mgm_query, query_variables=query_variables, role='importer')['data']['management']


def get_config_value(fwo_api_base_url, jwt, key='limit'):
    query_variables = {'key': key}
    config_query = "query getConf($key: String) {  config(where: {config_key: {_eq: $key}}) { config_value } }"
    result = call(fwo_api_base_url, jwt, config_query, query_variables=query_variables, role='importer')
    if 'data' in result and 'config' in result['data']:
        first_result = result['data']['config'][0]
        if 'config_value' in first_result:
            return first_result['config_value']
        else:
            return None
    else:
        return None


def get_config_values(fwo_api_base_url, jwt, keyFilter='limit'):
    query_variables = {'keyFilter': keyFilter+"%"}
    config_query = "query getConf($keyFilter: String) { config(where: {config_key: {_ilike: $keyFilter}}) { config_key config_value } }"
    result = call(fwo_api_base_url, jwt, config_query, query_variables=query_variables, role='importer')
    if 'data' in result and 'config' in result['data']:
        resultArray = result['data']['config']
        dict1 = {v['config_key']: v['config_value'] for k,v in enumerate(resultArray)}
        return dict1
    else:
        return None


def get_mgm_details(fwo_api_base_url, jwt, query_variables, debug_level=0):
    mgm_query = """
        query getManagementDetails($mgmId: Int!) {
            management(where:{mgm_id:{_eq:$mgmId}} order_by: {mgm_name: asc}) {
                id: mgm_id
                name: mgm_name
                hostname: ssh_hostname
                port: ssh_port
                import_credential {
                    id
                    credential_name
                    user: username
                    secret
                    sshPublicKey: public_key
                    cloudClientId: cloud_client_id
                    cloudClientSecret: cloud_client_secret
                }
                deviceType: stm_dev_typ {
                    id: dev_typ_id
                    name: dev_typ_name
                    version: dev_typ_version
                }
                isSuperManager: is_super_manager
                configPath: config_path
                domainUid: domain_uid
                cloudSubscriptionId: cloud_subscription_id
                cloudTenantId: cloud_tenant_id
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
                subManager: managementByMultiDeviceManagerId {
                mgm_id
                }
                import_controls(where: { successful_import: {_eq: true} } order_by: {control_id: desc}, limit: 1) {
                    starttime: start_time
                }
            }  
        }
    """
    api_call_result = call(fwo_api_base_url, jwt, mgm_query, query_variables=query_variables, role='importer')
    if 'data' in api_call_result and 'management' in api_call_result['data'] and len(api_call_result['data']['management'])>=1:
        if not '://' in api_call_result['data']['management'][0]['hostname']:
            # only decrypt if we have a real management and are not fetching the config from an URL
            # decrypt secret read from API
            try:
                secret = api_call_result['data']['management'][0]['import_credential']['secret']
                decryptedSecret = decrypt(secret, readMainKey())
            except ():
                raise SecretDecryptionFailed
            api_call_result['data']['management'][0]['import_credential']['secret'] = decryptedSecret
        return api_call_result['data']['management'][0]
    else:
        raise Exception('did not succeed in getting management details from FWO API')


def readMainKey(filePath=fwo_const.mainKeyFile):
    with open(filePath, "r") as keyfile:
        mainKey = keyfile.read().rstrip(' \n')
    return mainKey


# this mgm field is used by mw dailycheck scheduler
def log_import_attempt(fwo_api_base_url, jwt, mgm_id, successful=False):
    now = datetime.datetime.now().isoformat()
    query_variables = { "mgmId": mgm_id, "timeStamp": now, "success": successful }
    mgm_mutation = """
        mutation logImportAttempt($mgmId: Int!, $timeStamp: timestamp!, $success: Boolean) {
            update_management(where: {mgm_id: {_eq: $mgmId}}, _set: {last_import_attempt: $timeStamp, last_import_attempt_successful: $success } ) { affected_rows }
        }"""
    return call(fwo_api_base_url, jwt, mgm_mutation, query_variables=query_variables, role='importer')


def setImportLock(importState) -> None:
        logger = getFwoLogger()
        try: # set import lock
            url = importState.FwoConfig.FwoApiUri
            mgmId = int(importState.MgmDetails.Id)
            lock_mutation = """
                mutation lockImport($mgmId: Int!, $isFullImport: Boolean) { 
                    insert_import_control(objects: {mgm_id: $mgmId, is_full_import: $isFullImport}) 
                    { returning { control_id } } 
                }
                """
            lock_result = call(importState.FwoConfig.FwoApiUri, importState.Jwt, lock_mutation, 
                               query_variables={"mgmId": mgmId, "isFullImport": importState.IsFullImport },
                               role='importer')
            if lock_result['data']['insert_import_control']['returning'][0]['control_id']:
                importState.setImportId(lock_result['data']['insert_import_control']['returning'][0]['control_id'])
            else:
                importState.setImportId(-1)
        except:
            logger.error("import_management - failed to get import lock for management id " + str(mgmId))
            importState.setImportId(-1)
        if importState.ImportId == -1:
            create_data_issue(importState.FwoConfig.FwoApiUri, importState.Jwt, mgm_id=int(importState.MgmDetails.Id), severity=1, 
                description="failed to get import lock for management id " + str(mgmId))
            setAlert(url, importState.Jwt, import_id=importState.ImportId, title="import error", mgm_id=str(mgmId), severity=1, role='importer', \
                description="fwo_api: failed to get import lock", source='import', alertCode=15, mgm_details=importState.MgmDetails)
            raise FwoApiFailedLockImport("fwo_api: failed to get import lock for management id " + str(mgmId)) from None


def count_rule_changes_per_import(fwo_api_base_url, jwt, import_id):
    logger = getFwoLogger()
    change_count_query = """
        query count_rule_changes($importId: bigint!) {
            changelog_rule_aggregate(where: {control_id: {_eq: $importId}}) { aggregate { count } }
        }"""
    try:
        count_result = call(fwo_api_base_url, jwt, change_count_query, query_variables={'importId': import_id}, role='importer')
        rule_changes_in_import = int(count_result['data']['changelog_rule_aggregate']['aggregate']['count'])
    except:
        logger.exception("failed to count changes for import id " + str(import_id))
        rule_changes_in_import = 0
    return rule_changes_in_import


def count_changes_per_import(fwo_api_base_url, jwt, import_id):
    logger = getFwoLogger()
    change_count_query = """
        query count_changes($importId: bigint!) {
            changelog_object_aggregate(where: {control_id: {_eq: $importId}}) { aggregate { count } }
            changelog_service_aggregate(where: {control_id: {_eq: $importId}}) { aggregate { count } }
            changelog_user_aggregate(where: {control_id: {_eq: $importId}}) { aggregate { count } }
            changelog_rule_aggregate(where: {control_id: {_eq: $importId}}) { aggregate { count } }
        }"""
    try:
        count_result = call(fwo_api_base_url, jwt, change_count_query, query_variables={'importId': import_id}, role='importer')
        changes_in_import = int(count_result['data']['changelog_object_aggregate']['aggregate']['count']) + \
            int(count_result['data']['changelog_service_aggregate']['aggregate']['count']) + \
            int(count_result['data']['changelog_user_aggregate']['aggregate']['count']) + \
            int(count_result['data']['changelog_rule_aggregate']
                ['aggregate']['count'])
    except:
        logger.exception("failed to count changes for import id " + str(import_id))
        changes_in_import = 0
    return changes_in_import


def unlock_import(importState):
    logger = getFwoLogger()
    error_during_import_unlock = 0
    query_variables = {"stopTime": datetime.datetime.now().isoformat(), "importId": importState.ImportId,
                       "success": importState.ErrorCount == 0, "changesFound": importState.ChangeCount > 0, "changeNumber": importState.ChangeCount}

    unlock_mutation = """
        mutation unlockImport($importId: bigint!, $stopTime: timestamp!, $success: Boolean, $changesFound: Boolean!, $changeNumber: Int!) {
            update_import_control(where: {control_id: {_eq: $importId}}, _set: {stop_time: $stopTime, successful_import: $success, changes_found: $changesFound, security_relevant_changes_counter: $changeNumber}) {
                affected_rows
            }
        }"""

    try:
        unlock_result = call(importState.FwoConfig.FwoApiUri, importState.Jwt, unlock_mutation,
                             query_variables=query_variables, role='importer')
        changes_in_import_control = unlock_result['data']['update_import_control']['affected_rows']
    except:
        logger.exception("failed to unlock import for management id " + str(importState.MgmDetails.Id))
        error_during_import_unlock = 1
    return error_during_import_unlock


# this effectively clears the management!
def delete_import(importState):
    logger = getFwoLogger()
    query_variables = {"importId": importState.ImportId}

    delete_import_mutation = """
        mutation deleteImport($importId: bigint!) {
            delete_import_control(where: {control_id: {_eq: $importId}}) { affected_rows }
        }"""

    try:
        result = call(importState.FwoConfig.FwoApiUri, importState.Jwt, delete_import_mutation,
                      query_variables=query_variables, role='importer')
        api_changes = result['data']['delete_import_control']['affected_rows']
    except:
        logger.exception(
            "fwo_api: failed to unlock import for import id " + str(importState.ImportId))
        return 1  # signaling an error
    if api_changes == 1:
        return 0        # return code 0 is ok
    else:
        return 1

#   currently temporarliy only working with single chunk
def import_json_config(importState, config, startImport=True):
    logger = getFwoLogger()
    import_mutation = """
        mutation import($importId: bigint!, $mgmId: Int!, $config: jsonb!, $start_import_flag: Boolean!, $debug_mode: Boolean!) {
            insert_import_config(objects: {start_import_flag: $start_import_flag, import_id: $importId, mgm_id: $mgmId, config: $config, debug_mode: $debug_mode}) {
                affected_rows
            }
        }
    """
    try:
        debug_mode = (fwo_globals.debug_level>0)
        queryVariables = {
            'debug_mode': debug_mode,
            'mgmId': importState.MgmDetails.Id,
            'importId': importState.ImportId,
            'config': config,
            'start_import_flag': startImport,
        }
        import_result = call(importState.FwoConfig.FwoApiUri, importState.Jwt, import_mutation,
                             query_variables=queryVariables, role='importer')
        # note: this will not detect errors in triggered stored procedure run
        if 'errors' in import_result:
            logger.exception("fwo_api:import_json_config - error while writing importable config for mgm id " +
                              str(importState.MgmDetails.Id) + ": " + str(import_result['errors']))
            return 1 # error
        else:
            changes_in_import_control = import_result['data']['insert_import_config']['affected_rows']
    except:
        logger.exception(f"failed to write normalized config for mgm id {str(importState.MgmDetails.Id)}: {str(traceback.format_exc())}")
        return 1 # error
    
    if changes_in_import_control==1:
        return 0
    else:
        return 1


def update_hit_counter(importState, normalizedConfig):
    logger = getFwoLogger()
    # currently only data for check point firewalls is collected!

    queryVariablesLocal = {"mgmId": importState.MgmDetails.Id}
    # prerequesite: rule_uids are unique across a management
    # this is guaranteed for the newer devices
    # older devices like netscreen or FortiGate (via ssh) need to be checked
    # when hits information should be gathered here in the future

    # TODO (of minor importance) import rules per gateway to show which gateways have no hits
    found_hits = False
    last_hit_update_mutation = """
        mutation updateRuleLastHit($mgmId:Int!) {
            update_rule_metadata_many(updates: [
    """
    for policy in normalizedConfig.rules:
        for rule in policy.Rules:
            if 'last_hit' in rule and rule['last_hit'] is not None:
                found_hits = True
                update_expr = '{{ where: {{ device: {{ mgm_id:{{_eq:$mgmId}} }} rule_uid: {{ _eq: "{rule_uid}" }} }}, _set: {{ rule_last_hit: "{last_hit}" }} }}, '.format(rule_uid=rule["rule_uid"], last_hit=rule['last_hit'])
                last_hit_update_mutation += update_expr

    last_hit_update_mutation += " ]) { affected_rows } }"

    if found_hits:
        try:
            update_result = call(importState.FwoConfig.FwoApiUri, importState.Jwt, last_hit_update_mutation,
                                query_variables=queryVariablesLocal, role='importer')
            if 'errors' in update_result:
                logger.exception("fwo_api:update_hit_counter - error while updating hit counters for mgm id " +
                                str(importState.MgmDetails.Id) + ": " + str(update_result['errors']))
            update_counter = len(update_result['data']['update_rule_metadata_many'])
        except:
            logger.exception("failed to update hit counter for mgm id " + str(importState.MgmDetails.Id))
            return 1 # error
        
        return 0
    else:
        if len(normalizedConfig.rules)>0:
            logger.debug("found only rules without hit information for mgm_id " + str(importState.MgmDetails.Id))
            return 1
    # else:
    #     logger.debug("no rules found for mgm_id " + str(importState.MgmDetails.Id))
    #     return 1


def delete_import_object_tables(importState, query_variables):
    logger = getFwoLogger()
    delete_mutation = """
        mutation deleteImportData($importId: bigint!)  {
            delete_import_object(where: {control_id: {_eq: $importId}}) {
                affected_rows
            }
            delete_import_rule(where: {control_id: {_eq: $importId}}) {
                affected_rows
            }
            delete_import_service(where: {control_id: {_eq: $importId}}) {
                affected_rows
            }
            delete_import_user(where: {control_id: {_eq: $importId}}) {
                affected_rows
            }
        }
    """
    try:
        delete_result = call(importState.FwoConfig.FwoApiUri, importState.Jwt, delete_mutation,
                             query_variables=query_variables, role='importer')
        changes_in_delete_import_tables =  \
            int(delete_result['data']['delete_import_object']['affected_rows']) + \
            int(delete_result['data']['delete_import_rule']['affected_rows']) + \
            int(delete_result['data']['delete_import_service']['affected_rows']) + \
            int(delete_result['data']['delete_import_user']['affected_rows'])
    except:
        logger.exception("failed to delete from import_ tables")
        return -1  # indicating error
    return changes_in_delete_import_tables


def delete_json_config_in_import_table(importState, query_variables):
    logger = getFwoLogger()
    delete_mutation = """
        mutation delete_import_config($importId: bigint!) {
            delete_import_config(where: {import_id: {_eq: $importId}}) { affected_rows }
        }
    """
    try:
        delete_result = call(importState.FwoConfig.FwoApiUri, importState.Jwt, delete_mutation,
                             query_variables=query_variables, role='importer')
        changes_in_delete_config = delete_result['data']['delete_import_config']['affected_rows']
    except:
        logger.exception("failed to delete config without changes")
        return -1  # indicating error
    return changes_in_delete_config


# def store_full_json_config(importState, query_variables):
#     logger = getFwoLogger()
#     import_mutation = """
#         mutation store_full_config($importId: bigint!, $mgmId: Int!, $config: jsonb!) {
#             insert_import_full_config(objects: {import_id: $importId, mgm_id: $mgmId, config: $config}) {
#                 affected_rows
#             }
#         }
#     """

#     try:
#         import_result = call(importState.FwoConfig.FwoApiUri, importState.Jwt, import_mutation,
#                              query_variables=query_variables, role='importer')
#         changes_in_import_full_config = import_result['data']['insert_import_full_config']['affected_rows']
#     except:
#         logger.exception("failed to write full config for mgm id " + str(importState.MgmDetails.Id))
#         return 2  # indicating 1 error because we are expecting exactly one change
#     return changes_in_import_full_config-1


def get_error_string_from_imp_control(importState, query_variables):
    error_query = "query getErrors($importId:bigint) { import_control(where:{control_id:{_eq:$importId}}) { import_errors } }"
    return call(importState.FwoConfig.FwoApiUri, importState.Jwt, error_query, query_variables=query_variables, role='importer')['data']['import_control']


def create_data_issue(fwo_api_base_url, jwt, import_id=None, obj_name=None, mgm_id=None, dev_id=None, severity=1, role='importer',
        rule_uid=None, object_type=None, description=None, source='import'):
    logger = getFwoLogger()
    if obj_name=='all' or obj_name=='Original': 
        return True # ignore resolve errors for enriched objects that are not in the native config
    else:
        create_data_issue_mutation = """
            mutation createDataIssue($source: String!, $severity: Int!, $importId: bigint, $objectName: String, 
                    $objectType:String, $ruleUid: String, $description: String,
                    $mgmId: Int, $devId: Int) {
                insert_log_data_issue(objects: {source: $source, severity: $severity, import_id: $importId, 
                    object_name: $objectName, rule_uid: $ruleUid,
                    object_type:$objectType, description: $description, issue_dev_id: $devId, issue_mgm_id: $mgmId }) {
                    affected_rows
                }
            }
        """

        query_variables = {"source": source, "severity": severity }
 
        if dev_id is not None:
            query_variables.update({"devId": dev_id})
        if mgm_id is not None:
            query_variables.update({"mgmId": mgm_id})
        if obj_name is not None:
            query_variables.update({"objectName": obj_name})
        if object_type is not None:
            query_variables.update({"objectType": object_type})
        # setting import_id leads to error: 'Foreign key violation. insert or update on table "log_data_issue" 
        #       violates foreign key constraint "log_data_issue_import_control_control_id_fkey" 
        # if import_id is not None:
        #     query_variables.update({"importId": import_id})
        if rule_uid is not None:
            query_variables.update({"ruleUid": rule_uid})
        if description is not None:
            query_variables.update({"description": description})

        # write data issue to alert.log file as well
        # if severity>0:
        #     writeAlertToLogFile(query_variables)
        
        try:
            import_result = call(fwo_api_base_url, jwt, create_data_issue_mutation, query_variables=query_variables, role=role)
            changes = import_result['data']['insert_log_data_issue']['affected_rows']
        except:
            logger.error("failed to create log_data_issue: " + json.dumps(query_variables))
            return False
        return changes==1


def setAlert(fwo_api_base_url, jwt, import_id=None, title=None, mgm_id=None, dev_id=None, severity=1, role='importer',
        jsonData=None, description=None, source='import', user_id=None, refAlert=None, alertCode=None, mgm_details = None):

    logger = getFwoLogger()

    addAlert_mutation = """
        mutation addAlert(
            $source: String!
            $userId: Int
            $title: String
            $description: String
            $mgmId: Int
            $devId: Int
            $jsonData: json
            $refAlert: bigint
            $alertCode: Int
        ) 
        {
            insert_alert(
                objects: {
                    source: $source
                    user_id: $userId
                    title: $title
                    description: $description
                    alert_mgm_id: $mgmId
                    alert_dev_id: $devId
                    json_data: $jsonData
                    ref_alert_id: $refAlert
                    alert_code: $alertCode
                }
            ) 
            {
                returning { newId: alert_id }
            }
        }
    """
    getAlert_query = """
        query getAlerts($mgmId: Int!, $alertCode: Int!, $currentAlertId: bigint!) {
            alert(where: {
                alert_mgm_id: {_eq: $mgmId}, alert_code: {_eq: $alertCode}
                ack_timestamp: {_is_null: true}
                alert_id: {_neq: $currentAlertId}}) 
            {
                alert_id
            }
        }
    """
    ackAlert_mutation = """
        mutation ackAlert($userId: Int, $alertId: bigint, $ackTimeStamp: timestamp) {
            update_alert(where: {alert_id: {_eq: $alertId}}, _set: {ack_by: $userId, ack_timestamp: $ackTimeStamp}) {
                affected_rows
            }
        }
    """

    query_variables = {"source": source }

    if dev_id is not None:
        query_variables.update({"devId": dev_id})
    if user_id is not None:
        query_variables.update({"userId": user_id})
    if mgm_id is not None:
        query_variables.update({"mgmId": mgm_id})
    if refAlert is not None:
        query_variables.update({"refAlert": refAlert})
    if title is not None:
        query_variables.update({"title": title})
    if description is not None:
        query_variables.update({"description": description})
    if alertCode is not None:
        query_variables.update({"alertCode": alertCode})

    if jsonData is None:
        jsonData = {}
    if severity != None:
        jsonData.update({"severity": severity})
    if import_id != None:
        jsonData.update({"import_id": import_id})
    if mgm_details != None:
        jsonData.update({"mgm_name": mgm_details.Name})
    query_variables.update({"jsonData": json.dumps(jsonData)})

    # write data issue to alert.log file as well
    if severity>0:
        writeAlertToLogFile(query_variables)
    
    try:
        import_result = call(fwo_api_base_url, jwt, addAlert_mutation, query_variables=query_variables, role=role)
        newAlertId = import_result['data']['insert_alert']['returning'][0]['newId']
        if alertCode is not None and mgm_id is not None:
            # Acknowledge older alert for same problem on same management
            query_variables = { "mgmId": mgm_id, "alertCode": alertCode, "currentAlertId": newAlertId }
            existingUnacknowledgedAlerts = call(fwo_api_base_url, jwt, getAlert_query, query_variables=query_variables, role=role)
            if 'data' in existingUnacknowledgedAlerts and 'alert' in existingUnacknowledgedAlerts['data']:
                for alert in existingUnacknowledgedAlerts['data']['alert']:
                    if 'alert_id' in alert:
                        now = datetime.datetime.now().isoformat()
                        query_variables = { "userId": 0, "alertId": alert['alert_id'], "ackTimeStamp": now }
                        updateResult = call(fwo_api_base_url, jwt, ackAlert_mutation, query_variables=query_variables, role=role)
    except:
        logger.error("failed to create alert entry: " + json.dumps(query_variables))
        return False
    return True


def complete_import(importState):
    logger = getFwoLogger()
    
    success = (importState.ErrorCount==0)
    try:
        log_import_attempt(importState.FwoConfig.FwoApiUri, importState.Jwt, importState.MgmDetails.Id, successful=success)
    except:
        logger.error('error while trying to log import attempt')

    # try: # CLEANUP: delete configs of imports (without changes) (if no error occured)
    #     if delete_json_config_in_import_table(importState, {"importId": importState.ImportId})<0:
    #         importState.increaseErrorCounterByOne()
    # except:
    #     logger.error("import_management - unspecified error cleaning up import_config: " + str(traceback.format_exc()))

    try: # CLEANUP: delete data of this import from import_object/rule/service/user tables
        if delete_import_object_tables(importState, {"importId": importState.ImportId})<0:
            importState.increaseErrorCounterByOne()
    except:
        logger.error("import_management - unspecified error cleaning up import_ object tables: " + str(traceback.format_exc()))

    try: # finalize import by unlocking it
        importState.increaseErrorCounter(unlock_import(importState))
    except:
        logger.error("import_management - unspecified error while unlocking import: " + str(traceback.format_exc()))

    import_result = "import_management: import no. " + str(importState.ImportId) + \
            " for management " + importState.MgmDetails.Name + ' (id=' + str(importState.MgmDetails.Id) + ")" + \
            str(" threw errors," if importState.ErrorCount else " successful,") + \
            " change_count: " + str(importState.ChangeCount) + \
            ", duration: " + str(int(time.time()) - importState.StartTime) + "s" 
    import_result += ", ERRORS: " + importState.ErrorString if len(importState.ErrorString) > 0 else ""
    # def lock_import(fwo_api_base_url, jwt, query_variables):

    if importState.ErrorCount>0:
        create_data_issue(importState.FwoConfig.FwoApiUri, importState.Jwt, import_id=importState.ImportId, severity=1, description=importState.ErrorString)
        setAlert(importState.FwoConfig.FwoApiUri, importState.Jwt, import_id=importState.ImportId, title="import error", mgm_id=importState.MgmDetails.Id, severity=2, role='importer', \
            description=importState.ErrorString, source='import', alertCode=14, mgm_details=importState.MgmDetails)

    logger.info(import_result)

    return importState.ErrorCount

def getLastImportDetails(fwo_api_base_url, jwt, queryVariables, debug_level=0):
    mgm_query = """
        query getLastImportDetails($mgmId: Int!) {
            config(where: {config_key: {_eq: "dataRetentionTime"}, config_user: {_eq: 0}}) {
                retentionInDays: config_value
            }
            import_control(where: {mgm_id: {_eq: $mgmId}, _or: [{is_initial_import: {_eq: true}}, {is_full_import: {_eq: true}}]}, order_by: {control_id: desc}, limit: 1) {
                control_id
                start_time
            }
        }
    """

    try:
        pastDetails = call(fwo_api_base_url, jwt, mgm_query, query_variables=queryVariables, role='importer')
        retentionInDays = pastDetails['data']['config'][0]['retentionInDays']
        if len(pastDetails['data']['import_control'])>0:
            lastFullImportId = pastDetails['data']['import_control'][0]['control_id']
            lastFullImportDate = pastDetails['data']['import_control'][0]['start_time']
        else: # no matching imports found
            lastFullImportId = None
            lastFullImportDate = None
    except:
        logger = getFwoLogger()
        logger.error(f"error while getting past import details for mgm {str(queryVariables)}")

    return int(retentionInDays), lastFullImportId, lastFullImportDate

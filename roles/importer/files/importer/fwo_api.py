# library for FWORCH API calls
# from asyncio.log import logger
import re
import traceback
# from sqlite3 import Timestamp
# from textwrap import indent
import requests.packages
import requests
import json
import datetime
import base64
import gnupg
from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives import padding


from fwo_log import getFwoLogger
import fwo_globals
import fwo_const
from fwo_const import fwo_api_http_import_timeout
from fwo_exception import FwoApiTServiceUnavailable, FwoApiTimeout, FwoApiLoginFailed, SecretDecryptionFailed
from fwo_base import writeAlertToLogFile


def showApiCallInfo(url, query, headers, type='debug'):
    max_query_size_to_display = 1000
    query_string = json.dumps(query, indent=2)
    header_string = json.dumps(headers, indent=2)
    query_size = len(query_string)

    if type=='error':
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
        except requests.exceptions.RequestException:
            logger.error(showApiCallInfo(url, full_query, request_headers, type='error') + ":\n" + str(traceback.format_exc()))
            if r != None:
                if r.status_code == 503:
                    raise FwoApiTServiceUnavailable("FWO API HTTP error 503 (FWO API died?)" )
                if r.status_code == 502:
                    raise FwoApiTimeout("FWO API HTTP error 502 (might have reached timeout of " + str(int(fwo_api_http_import_timeout)/60) + " minutes)" )
            else:
                raise
        if int(fwo_globals.debug_level) > 4:
            logger.debug (showApiCallInfo(url, full_query, request_headers, type='debug'))
        if show_progress:
            pass
            # print('.', end='', flush=True)
        if r != None:
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
    mgm_query = """
        query getManagementIds {
            management(where:{do_not_import:{_eq:false}} order_by: {mgm_name: asc}) { id: mgm_id } } """
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


# can be used for decrypting text encrypted with postgresql.pgp_sym_encrypt
def decryptGpg(encryptedTextIn, key):
    logger = getFwoLogger()
    gpg = gnupg.GPG()

    binData = base64.b64decode(encryptedTextIn)
    decrypted_data = gpg.decrypt(binData, passphrase=key)

    if decrypted_data.ok:
        return decrypted_data.data.decode('utf-8')
    else:
        logger.info("error while decrypting: " + decrypted_data.status + ", assuming plaintext credentials")
        return encryptedTextIn


# can be used for decrypting text encrypted with C# (mw-server)
def decrypt_aes_ciphertext(base64_encrypted_text, passphrase):
    encrypted_data = base64.b64decode(base64_encrypted_text)
    ivLength = 16 # IV length for AES is 16 bytes

    # Extract IV from the encrypted data
    iv = encrypted_data[:ivLength]  

    # Initialize AES cipher with provided passphrase and IV
    backend = default_backend()
    cipher = Cipher(algorithms.AES(passphrase.encode()), modes.CBC(iv), backend=backend)
    decryptor = cipher.decryptor()

    # Decrypt the ciphertext
    decrypted_data = decryptor.update(encrypted_data[ivLength:]) + decryptor.finalize()

    # Remove padding
    unpadder = padding.PKCS7(algorithms.AES.block_size).unpadder()
    try:
        unpadded_data = unpadder.update(decrypted_data) + unpadder.finalize()
        return unpadded_data.decode('utf-8')  # Assuming plaintext is UTF-8 encoded
    except ValueError as e:
        raise Exception ('AES decryption failed:', e)


# wrapper for trying the different decryption methods
def decrypt(encrypted_data, passphrase):
    logger = getFwoLogger()
    try:
        decrypted = decrypt_aes_ciphertext(encrypted_data, passphrase)
        return decrypted
    except:
        logger.warning("Unspecified error while decrypting with MS: " + str(traceback.format_exc()))
        return encrypted_data


def log_import_attempt(fwo_api_base_url, jwt, mgm_id, successful=False):
    now = datetime.datetime.now().isoformat()
    query_variables = { "mgmId": mgm_id, "timeStamp": now, "success": successful }
    mgm_mutation = """
        mutation logImportAttempt($mgmId: Int!, $timeStamp: timestamp!, $success: Boolean) {
            update_management(where: {mgm_id: {_eq: $mgmId}}, _set: {last_import_attempt: $timeStamp, last_import_attempt_successful: $success } ) { affected_rows }
        }"""
    return call(fwo_api_base_url, jwt, mgm_mutation, query_variables=query_variables, role='importer')


def lock_import(fwo_api_base_url, jwt, query_variables):
    lock_mutation = "mutation lockImport($mgmId: Int!) { insert_import_control(objects: {mgm_id: $mgmId}) { returning { control_id } } }"
    lock_result = call(fwo_api_base_url, jwt, lock_mutation, query_variables=query_variables, role='importer')
    if lock_result['data']['insert_import_control']['returning'][0]['control_id']:
        return lock_result['data']['insert_import_control']['returning'][0]['control_id']
    else:
        return -1


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


def unlock_import(fwo_api_base_url, jwt, mgm_id, stop_time, current_import_id, error_count, change_count):
    logger = getFwoLogger()
    error_during_import_unlock = 0
    query_variables = {"stopTime": stop_time, "importId": current_import_id,
                       "success": error_count == 0, "changesFound": change_count > 0, "changeNumber": change_count}

    unlock_mutation = """
        mutation unlockImport($importId: bigint!, $stopTime: timestamp!, $success: Boolean, $changesFound: Boolean!, $changeNumber: Int!) {
            update_import_control(where: {control_id: {_eq: $importId}}, _set: {stop_time: $stopTime, successful_import: $success, changes_found: $changesFound, security_relevant_changes_counter: $changeNumber}) {
                affected_rows
            }
        }"""

    try:
        unlock_result = call(fwo_api_base_url, jwt, unlock_mutation,
                             query_variables=query_variables, role='importer')
        changes_in_import_control = unlock_result['data']['update_import_control']['affected_rows']
    except:
        logger.exception("failed to unlock import for management id " + str(mgm_id))
        error_during_import_unlock = 1
    return error_during_import_unlock


# this effectively clears the management!
def delete_import(fwo_api_base_url, jwt, current_import_id):
    logger = getFwoLogger()
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
        logger.exception(
            "fwo_api: failed to unlock import for import id " + str(current_import_id))
        return 1  # signaling an error
    if api_changes == 1:
        return 0        # return code 0 is ok
    else:
        return 1


def import_json_config(fwo_api_base_url, jwt, mgm_id, query_variables):
    logger = getFwoLogger()
    import_mutation = """
        mutation import($importId: bigint!, $mgmId: Int!, $config: jsonb!, $start_import_flag: Boolean!, $debug_mode: Boolean!, $chunk_number: Int!) {
            insert_import_config(objects: {start_import_flag: $start_import_flag, import_id: $importId, mgm_id: $mgmId, chunk_number: $chunk_number, config: $config, debug_mode: $debug_mode}) {
                affected_rows
            }
        }
    """
    try:
        debug_mode = (fwo_globals.debug_level>0)
        query_variables.update({'debug_mode': debug_mode})
        import_result = call(fwo_api_base_url, jwt, import_mutation,
                             query_variables=query_variables, role='importer')
        # note: this will not detect errors in triggered stored procedure run
        if 'errors' in import_result:
            logger.exception("fwo_api:import_json_config - error while writing importable config for mgm id " +
                              str(mgm_id) + ": " + str(import_result['errors']))
        changes_in_import_control = import_result['data']['insert_import_config']['affected_rows']
    except:
        logger.exception("failed to write importable config for mgm id " + str(mgm_id))
        return 1 # error
    
    if changes_in_import_control==1:
        return 0
    else:
        return 1


def update_hit_counter(fwo_api_base_url, jwt, mgm_id, query_variables):
    logger = getFwoLogger()
    # currently only data for check point firewalls is collected!

    if 'config' in query_variables and 'rules' in query_variables['config']:
        queryVariablesLocal = {"mgmId": mgm_id}
        # prerequesite: rule_uids are unique across a management
        # this is guaranteed for the newer devices
        # older devices like netscreen or FortiGate (via ssh) need to be checked
        # when hits information should be gathered here in the future

        found_hits = False
        last_hit_update_mutation = """
            mutation updateRuleLastHit($mgmId:Int!) {
                update_rule_metadata_many(updates: [
        """

        for rule in query_variables['config']['rules']:
            if 'last_hit' in rule and rule['last_hit'] is not None:
                found_hits = True
                update_expr = '{{ where: {{ device: {{ mgm_id:{{_eq:$mgmId}} }} rule_uid: {{ _eq: "{rule_uid}" }} }}, _set: {{ rule_last_hit: "{last_hit}" }} }}, '.format(rule_uid=rule["rule_uid"], last_hit=rule['last_hit'])
                last_hit_update_mutation += update_expr

        last_hit_update_mutation += " ]) { affected_rows } }"

        if found_hits:
            try:
                update_result = call(fwo_api_base_url, jwt, last_hit_update_mutation,
                                    query_variables=queryVariablesLocal, role='importer')
                if 'errors' in update_result:
                    logger.exception("fwo_api:update_hit_counter - error while updating hit counters for mgm id " +
                                    str(mgm_id) + ": " + str(update_result['errors']))
                update_counter = len(update_result['data']['update_rule_metadata_many'])
            except:
                logger.exception("failed to update hit counter for mgm id " + str(mgm_id))
                return 1 # error
            
            return 0
        else:
            if len(query_variables['config']['rules'])>0:
                logger.debug("found rules without hit information for mgm_id " + str(mgm_id))
                return 1
    else:
        logger.debug("no rules found for mgm_id " + str(mgm_id))
        return 1


def delete_import_object_tables(fwo_api_base_url, jwt, query_variables):
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
        delete_result = call(fwo_api_base_url, jwt, delete_mutation,
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


def delete_json_config_in_import_table(fwo_api_base_url, jwt, query_variables):
    logger = getFwoLogger()
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
        logger.exception("failed to delete config without changes")
        return -1  # indicating error
    return changes_in_delete_config


def store_full_json_config(fwo_api_base_url, jwt, mgm_id, query_variables):
    logger = getFwoLogger()
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
        logger.exception("failed to write full config for mgm id " + str(mgm_id))
        return 2  # indicating 1 error because we are expecting exactly one change
    return changes_in_import_full_config-1


def delete_full_json_config(fwo_api_base_url, jwt, query_variables):
    logger = getFwoLogger()
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
        logger.exception("failed to delete full config ")
        return 2  # indicating 1 error
    return changes_in_delete_full_config-1


def get_error_string_from_imp_control(fwo_api_base_url, jwt, query_variables):
    error_query = "query getErrors($importId:bigint) { import_control(where:{control_id:{_eq:$importId}}) { import_errors } }"
    return call(fwo_api_base_url, jwt, error_query, query_variables=query_variables, role='importer')['data']['import_control']


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
    if mgm_details != None and 'name' in mgm_details:
        jsonData.update({"mgm_name": mgm_details['name']})
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

from copy import deepcopy
import traceback
import sys, os, time, datetime
import json, requests, requests.packages
from socket import gethostname
import importlib.util
base_dir = '/usr/local/fworch'
importer_base_dir = base_dir + '/importer'
from pathlib import Path
sys.path.append(importer_base_dir) # adding absolute path here once
import fwo_api
from fwo_log import getFwoLogger

fw_module_name = 'fwcommon'  # the module start-point for product specific code
full_config_size_limit = 5000000 # native configs greater than 5 MB will not be stored in DB
config2import_size_limit = 2000000 # normalized configs greater than 2 MB will be deleted from import_config table after import
csv_delimiter = '%'
list_delimiter = '|'
line_delimiter = "\n"
apostrophe = "\""
section_header_uids=[]
nat_postfix = '_NatNwObj'
fwo_api_http_import_timeout = 14400 # 4 hours

fwo_config_filename = base_dir + '/etc/fworch.json'
with open(fwo_config_filename, "r") as fwo_config:
    fwo_config = json.loads(fwo_config.read())
fwo_api_base_url = fwo_config['api_uri']

# how many objects (network, services, rules, ...) should be sent to the FWO API in one go?
# should be between 500 and 2.000 in production (results in a max obj number of max. 5 x this value - nwobj/svc/rules/...)
# the database has a limit of 255 MB per jsonb
# https://stackoverflow.com/questions/12632871/size-limit-of-json-data-type-in-postgresql
# >25.000 rules exceed this limit
max_objs_per_chunk = 1000 


class FwLoginFailed(Exception):
    """Raised when login to FW management failed"""

    def __init__(self, message="Login to FW management failed"):
            self.message = message
            super().__init__(self.message)

class FwoApiLoginFailed(Exception):
    """Raised when login to FWO API failed"""

    def __init__(self, message="Login to FWO API failed"):
            self.message = message
            super().__init__(self.message)

class FwoApiFailedLockImport(Exception):
    """Raised when unable to lock import (import running?)"""

    def __init__(self, message="Locking import failed - already running?"):
            self.message = message
            super().__init__(self.message)

class FwoApiFailure(Exception):
    """Raised for any other FwoApi call exceptions"""

    def __init__(self, message="There was an unclassified error while executing an FWO API call"):
            self.message = message
            super().__init__(self.message)

class FwoApiTimeout(Exception):
    """Raised for 502 http error with proxy due to timeout"""

    def __init__(self, message="reverse proxy timeout error during FWO API call - try increasing the reverse proxy timeout"):
            self.message = message
            super().__init__(self.message)

class FwoApiTServiceUnavailable(Exception):
    """Raised for 503 http error Serice unavailable"""

    def __init__(self, message="FWO API Hasura container died"):
            self.message = message
            super().__init__(self.message)

class ConfigFileNotFound(Exception):
    """can only happen when specifying config file with -i switch"""

    def __init__(self, message="Could not read config file"):
            self.message = message
            super().__init__(self.message)

#  import_management: import a single management (if no import for it is running)
#     lock mgmt for import via FWORCH API call, generating new import_id y
#     check if we need to import (no md5, api call if anything has changed since last import)
#     get complete config (get, enrich, parse)
#     write into json dict write json dict to new table (single entry for complete config)
#     trigger import from json into csv and from there into destination tables
#     release mgmt for import via FWORCH API call (also removing import_id y data from import_tables?)
#     no changes: remove import_control?
def import_management(mgm_id=None, ssl='off', debug_level=0, proxy='', in_file=None, limit=150, force=False, clearManagementData=False):
    error_count = 0
    importer_user_name = 'importer'  # todo: move to config file?
    fwo_config_filename = base_dir + '/etc/fworch.json'
    importer_pwd_file = base_dir + '/etc/secrets/importer_pwd'
    import_tmp_path = base_dir + '/tmp/import'
    change_count = 0
    error_string = ''
    start_time = int(time.time())
    debug_level=int(debug_level)
    secret_filename = ''
    config2import = { "network_objects": [], "service_objects": [], "user_objects": [], "zone_objects": [], "rules": [] }
    config_changed_since_last_import = True

    # logger = set_log_level(log_level=debug_level, debug_level=debug_level)
    logger = getFwoLogger(debug_level=debug_level)
    
    if ssl == '' or ssl == 'off':
        requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

    # read fwo config (API URLs)
    with open(fwo_config_filename, "r") as fwo_config:
        fwo_config = json.loads(fwo_config.read())
    user_management_api_base_url = fwo_config['middleware_uri']
    fwo_api_base_url = fwo_config['api_uri']

    # authenticate to get JWT
    with open(importer_pwd_file, 'r') as file:
        importer_pwd = file.read().replace('\n', '')
    if proxy is not None:
        proxy = { "http_proxy": proxy, "https_proxy": proxy }
    else:
        proxy = None

    try:
        jwt = fwo_api.login(importer_user_name, importer_pwd, user_management_api_base_url,
                                ssl_verification=ssl, proxy=proxy)
    except FwoApiLoginFailed as e:
        logger.error(e.message)
        return e.message
    except:
        return "unspecified error during FWO API login"

    try: # get mgm_details (fw-type, port, ip, user credentials):
        mgm_details = fwo_api.get_mgm_details(fwo_api_base_url, jwt, {"mgmId": int(mgm_id)}, debug_level)
    except:
        logger.error("import_management - error while getting fw management details for mgm=" + str(mgm_id) )
        raise
    
    if mgm_details['importDisabled']:
        logger.info("import_management - import disabled for mgm " + str(mgm_id))
    else:
        Path(import_tmp_path).mkdir(parents=True, exist_ok=True)  # make sure tmp path exists
        package_list = []
        for dev in mgm_details['devices']:
            package_list.append(dev['package_name'])

        # only run if this is the correct import module
        if mgm_details['importerHostname'] != gethostname():
            logger.debug("import_management - this host (" + gethostname() + ") is not responsible for importing management " + str(mgm_id))
            return ""

        current_import_id = -1
        try: # set import lock
            current_import_id = fwo_api.lock_import(fwo_api_base_url, jwt, {"mgmId": int(mgm_id)})
        except:
            logger.error("import_management - failed to get import lock for management id " + str(mgm_id))
        if current_import_id == -1:
            fwo_api.create_data_issue(fwo_api_base_url, jwt, mgm_id=int(mgm_id), severity=1, 
                description="failed to get import lock for management id " + str(mgm_id))
            fwo_api.setAlert(fwo_api_base_url, jwt, import_id=current_import_id, title="import error", mgm_id=str(mgm_id), severity=1, role='importer', \
                description="fwo_api: failed to get import lock", source='import', alertCode=15, mgm_details=mgm_details)
            raise FwoApiFailedLockImport("fwo_api: failed to get import lock for management id " + str(mgm_id)) from None

        logger.info("starting import of management " + mgm_details['name'] + '(' + str(mgm_id) + "), import_id=" + str(current_import_id))
        full_config_json = {}
        # get_config_response = 0

        if clearManagementData:
            logger.info('this import run will reset the configuration of this management to "empty"')
        else:
            if in_file is not None:    # read native config from file
                try:
                    with open(in_file, 'r') as json_file:
                        full_config_json = json.load(json_file)
                except:
                    # logger.exception("import_management - error while reading json import from file", traceback.format_exc())
                    error_string = "Could not read config file " + in_file
                    error_count += 1
                    error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt, debug_level=debug_level)
                    raise ConfigFileNotFound(error_string) from None
            # note: we need to run get_config in any case (even when importing from a file) as this function 
            # also contains the conversion from native to config2import (parsing)
            
            ### geting config from firewall manager ######################
            config_changed_since_last_import, error_string, error_count, change_count = get_config_sub(mgm_details, full_config_json, config2import, jwt, current_import_id, start_time,
                in_file=in_file, import_tmp_path=import_tmp_path, error_string=error_string, error_count=error_count, change_count=change_count, 
                proxy=proxy, limit=limit, debug_level=debug_level, force=force, ssl=ssl)

        time_get_config = int(time.time()) - start_time
        logger.debug("import_management - getting config total duration " + str(time_get_config) + "s")

        if config_changed_since_last_import:
            try: # now we import the config via API chunk by chunk:
                for config_chunk in split_config(config2import, current_import_id, mgm_id, debug_level):
                    error_count += fwo_api.import_json_config(fwo_api_base_url, jwt, mgm_id, config_chunk, debug_level)
            except:
                logger.error("import_management - unspecified error while importing config via FWO API: " + str(traceback.format_exc()))
                raise

            time_write2api = int(time.time()) - time_get_config - start_time
            logger.debug("import_management - writing config to API and stored procedure import duration: " + str(time_write2api) + "s")

            error_from_imp_control = "assuming error"
            try: # checking for errors during stored_procedure db imort in import_control table
                error_from_imp_control = fwo_api.get_error_string_from_imp_control(fwo_api_base_url, jwt, {"importId": current_import_id})
            except:
                logger.error("import_management - unspecified error while getting error string: " + str(traceback.format_exc()))

            if error_from_imp_control != None and error_from_imp_control != [{'import_errors': None}]:
                error_count += 1
                error_string += str(error_from_imp_control)
            # todo: if no objects found at all: at least throw a warning

            try: # get change count from db
                change_count = fwo_api.count_changes_per_import(fwo_api_base_url, jwt, current_import_id)
            except:
                logger.error("import_management - unspecified error while getting change count: " + str(traceback.format_exc()))
                raise

            try: # calculate config sizes
                full_config_size = sys.getsizeof(json.dumps(full_config_json))
                config2import_size = sys.getsizeof(json.dumps(config2import))
                logger.debug("full_config size: " + str(full_config_size) + " bytes, config2import size: " + str(config2import_size) + " bytes")
            except:
                logger.error("import_management - unspecified error while calculating config sizes: " + str(traceback.format_exc()))
                raise

            if (change_count > 0 or error_count > 0) and full_config_size < full_config_size_limit:  # store full config in case of change or error
                try:  # store full config in DB
                    error_count += fwo_api.store_full_json_config(fwo_api_base_url, jwt, mgm_id, {
                        "importId": current_import_id, "mgmId": mgm_id, "config": full_config_json})
                except:
                    logger.error("import_management - unspecified error while storing full config: " + str(traceback.format_exc()))
                    raise
        else: # if no changes were found, we skip everything else without errors
            pass

        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt, debug_level=debug_level)
        
    return error_count


def get_config_sub(mgm_details, full_config_json, config2import, jwt, current_import_id, start_time,
        in_file=None, import_tmp_path='.', error_string='', error_count=0, change_count=0, 
        proxy='', limit=150, debug_level=0, force=False, ssl=''):

    logger = getFwoLogger(debug_level)

    try: # pick product-specific importer:
        pkg_name = mgm_details['deviceType']['name'].lower().replace(' ', '') + mgm_details['deviceType']['version']
        fw_module = importlib.import_module("." + fw_module_name, pkg_name)
    except:
        logger.exception("import_management - error while loading product specific fwcommon module", traceback.format_exc())        
        raise
    
    # Temporary failure in name resolution
    try: # get the config data from the firewall manager's API: 
        # check for changes from product-specific FW API
        config_changed_since_last_import = in_file != None or fw_module.has_config_changed(full_config_json,
            mgm_details, debug_level=debug_level, ssl_verification=ssl, proxy=proxy, force=force)
        if config_changed_since_last_import:
            logger.debug ( "has_config_changed: changes found or forced mode -> go ahead with getting config, Force = " + str(force))
        else:
            logger.debug ( "has_config_changed: no new changes found")

        if config_changed_since_last_import:
            fw_module.get_config( # get config from product-specific FW API
                config2import, full_config_json,  current_import_id, mgm_details, debug_level=debug_level, 
                ssl_verification=ssl, proxy=proxy, limit=limit, force=force, jwt=jwt)
    except FwLoginFailed as e:
        error_string += "  login failed: mgm_id=" + str(mgm_details['id']) + ", mgm_name=" + mgm_details['name'] + ", " + e.message
        error_count += 1
        logger.error(error_string)
        fwo_api.delete_import(fwo_api_base_url, jwt, current_import_id) # deleting trace of not even begun import
        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt, debug_level=debug_level)
        raise FwLoginFailed(e.message)
    except:
        error_string += "  import_management - unspecified error while getting config: " + str(traceback.format_exc())
        logger.error(error_string)
        error_count += 1
        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt, debug_level=debug_level)
        raise

    logger.debug("import_management: get_config completed (including normalization), duration: " + str(int(time.time()) - start_time) + "s") 

    if config_changed_since_last_import and debug_level>2:   # debugging: writing config to json file
        debug_start_time = int(time.time())
        # logger.debug("import_management: get_config completed, now writing debug config json files")
        try:
            normalized_config_filename = import_tmp_path + '/mgm_id_' + \
                str(mgm_details['id']) + '_config_normalized.json'
            with open(normalized_config_filename, "w") as json_data:
                json_data.write(json.dumps(config2import, indent=2))

            if debug_level>3:
                full_native_config_filename = import_tmp_path + '/mgm_id_' + \
                    str(mgm_details['id']) + '_config_native.json'
                with open(full_native_config_filename, "w") as json_data:  # create empty config file
                    json_data.write(json.dumps(full_config_json, indent=2))
        except:
            logger.error("import_management - unspecified error while dumping config to json file: " + str(traceback.format_exc()))
            raise

        time_write_debug_json = int(time.time()) - debug_start_time
        logger.debug("import_management - writing debug config json files duration " + str(time_write_debug_json) + "s")
    return config_changed_since_last_import, error_string, error_count, change_count


def complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt, debug_level=0):
    logger = getFwoLogger(debug_level=debug_level)

    try: # CLEANUP: delete configs of imports (without changes) (if no error occured)
        if fwo_api.delete_json_config_in_import_table(fwo_api_base_url, jwt, {"importId": current_import_id})<0:
            error_count = +1
        # if os.path.exists(secret_filename):
        #     os.remove(secret_filename)
    except:
        logger.error("import_management - unspecified error cleaning up: " + str(traceback.format_exc()))
        raise

    try: # finalize import by unlocking it
        error_count += fwo_api.unlock_import(fwo_api_base_url, jwt, int(
            mgm_details['id']), datetime.datetime.now().isoformat(), current_import_id, error_count, change_count)
    except:
        logger.error("import_management - unspecified error while unlocking import: " + str(traceback.format_exc()))
        raise

    import_result = "import_management: import no. " + str(current_import_id) + \
            " for management " + mgm_details['name'] + ' (id=' + str(mgm_details['id']) + ")" + \
            str(" threw errors," if error_count else " successful,") + \
            " change_count: " + str(change_count) + \
            ", duration: " + str(int(time.time()) - start_time) + "s" 
    import_result += ", ERRORS: " + error_string if len(error_string) > 0 else ""
    if error_count>0:
        fwo_api.create_data_issue(fwo_api_base_url, jwt, import_id=current_import_id, severity=1, description=error_string)
        fwo_api.setAlert(fwo_api_base_url, jwt, import_id=current_import_id, title="import error", mgm_id=mgm_details['id'], severity=2, role='importer', \
            description=error_string, source='import', alertCode=14, mgm_details=mgm_details)

    logger.info(import_result)

    return error_count


def split_list(list_in, max_list_length):
    if len(list_in)<max_list_length:
        return [list_in]
    else:
        list_of_lists = []
        i=0
        while i<len(list_in):
            last_element_in_chunk = min(len(list_in), i+max_list_length)
            list_of_lists.append(list_in[i:last_element_in_chunk])
            i += max_list_length
    return list_of_lists


def split_config(config2import, current_import_id, mgm_id, debug_level):
    conf_split_dict_of_lists = {}
    max_number_of_chunks = 0
    for obj_list_name in ["network_objects", "service_objects", "user_objects", "rules", "zone_objects"]:
        if obj_list_name in config2import:
            split_list_tmp = split_list(config2import[obj_list_name], max_objs_per_chunk)
            conf_split_dict_of_lists.update({obj_list_name: split_list_tmp})
            if len(split_list_tmp)>max_number_of_chunks:
                max_number_of_chunks = len(split_list_tmp)
        else:
            conf_split_dict_of_lists.update({obj_list_name: []})
    conf_split = []
    current_chunk = 0
    while current_chunk<max_number_of_chunks:
        network_object_chunk = []
        service_object_chunk = []
        user_object_chunk = []
        zone_object_chunk = []
        rules_chunk = []

        if current_chunk<len(conf_split_dict_of_lists['network_objects']):
            network_object_chunk = conf_split_dict_of_lists['network_objects'][current_chunk]
        if current_chunk<len(conf_split_dict_of_lists['service_objects']):
            service_object_chunk = conf_split_dict_of_lists['service_objects'][current_chunk]
        if current_chunk<len(conf_split_dict_of_lists['user_objects']):
            user_object_chunk = conf_split_dict_of_lists['user_objects'][current_chunk]
        if current_chunk<len(conf_split_dict_of_lists['zone_objects']):
            zone_object_chunk = conf_split_dict_of_lists['zone_objects'][current_chunk]
        if current_chunk<len(conf_split_dict_of_lists['rules']):
            rules_chunk = conf_split_dict_of_lists['rules'][current_chunk]

        conf_split.append({
            "network_objects": network_object_chunk,
            "service_objects": service_object_chunk,
            "user_objects": user_object_chunk,
            "zone_objects": zone_object_chunk,
            "rules": rules_chunk
        })
        current_chunk += 1

    # now adding meta data around
    config_split_with_metadata = []
    for conf_chunk in conf_split:
        config_split_with_metadata.append({
            "config": conf_chunk,
            "start_import_flag": False,
            "importId": int(current_import_id), 
            "mgmId": int(mgm_id), 
        })
    config_split_with_metadata[len(config_split_with_metadata)-1]["start_import_flag"] = True
    if debug_level>0:
        config_split_with_metadata[len(config_split_with_metadata)-1]["debug_mode"] = True
    return config_split_with_metadata


def csv_add_field(content, no_csv_delimiter=False):
    if (content == None or content == '') and not no_csv_delimiter:  # do not add apostrophes for empty fields
        field_result = csv_delimiter
    else:
        # add apostrophes at beginning and end and remove any ocurrence of them within the string
        if (isinstance(content, str)):
            escaped_field = content.replace(apostrophe,"")
            field_result = apostrophe + escaped_field + apostrophe
        else:   # leave non-string values as is
            field_result = str(content)
        if not no_csv_delimiter:
            field_result += csv_delimiter
    return field_result
 

def sanitize(content):
    if content == None:
        return None
    result = str(content)
    result = result.replace(apostrophe,"")  # remove possibly contained apostrophe
    result = result.replace(line_delimiter," ")  # replace possibly contained CR with space
    return result


def extend_string_list(list_string, src_dict, key, delimiter, jwt=None, import_id=None):
    if list_string is None:
        list_string = ''
    if list_string == '':
        if key in src_dict:
            result = delimiter.join(src_dict[key])
        else:
            result = ''
#            fwo_api.create_data_issue(fwo_api_base_url, jwt, import_id, key)
    else:
        if key in src_dict:
            old_list = list_string.split(delimiter)
            combined_list = old_list + src_dict[key]
            result = delimiter.join(combined_list)
        else:
            result = list_string
#            fwo_api.create_data_issue(fwo_api_base_url, jwt, import_id, key)
    return result


def resolve_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key, rule_type=None, jwt=None, import_id=None, mgm_id=None):
    # guessing ipv4 and adom (to also search global objects)
    return resolve_raw_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key, rule_type='v4_adom', obj_type='network', jwt=jwt, import_id=import_id, mgm_id=mgm_id)


def resolve_raw_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key, rule_type=None, obj_type='network', jwt=None, import_id=None, rule_uid=None, object_type=None, mgm_id=None):
    logger = getFwoLogger()

    ref_list = []
    objects_not_found = []
    for el in obj_name_string_list.split(delimiter):
        found = False
        if rule_type is not None:
            if obj_type == 'network':
                if 'v4' in rule_type and 'global' in rule_type:
                    object_tables = [obj_dict['nw_obj_global_firewall/address'], obj_dict['nw_obj_global_firewall/addrgrp']]
                elif 'v6' in rule_type and 'global' in rule_type:
                    object_tables = [obj_dict['nw_obj_global_firewall/address6'], obj_dict['nw_obj_global_firewall/addrgrp6']]
                elif 'v4' in rule_type and 'adom' in rule_type:
                    object_tables = [obj_dict['nw_obj_adom_firewall/address'], obj_dict['nw_obj_adom_firewall/addrgrp'], \
                        obj_dict['nw_obj_global_firewall/address'], obj_dict['nw_obj_global_firewall/addrgrp'], \
                        obj_dict['nw_obj_adom_firewall/vip'] ]
                elif 'v6' in rule_type and 'adom' in rule_type:
                    object_tables = [obj_dict['nw_obj_adom_firewall/address6'], obj_dict['nw_obj_adom_firewall/addrgrp6'], \
                        obj_dict['nw_obj_global_firewall/address6'], obj_dict['nw_obj_global_firewall/addrgrp6']]
                elif 'nat' in rule_type and 'adom' in rule_type:
                    object_tables = [obj_dict['nw_obj_adom_firewall/address'], obj_dict['nw_obj_adom_firewall/addrgrp'], \
                        obj_dict['nw_obj_global_firewall/address'], obj_dict['nw_obj_global_firewall/addrgrp']]
                elif 'nat' in rule_type and 'global' in rule_type:
                    object_tables = [obj_dict['nw_obj_global_firewall/address'], obj_dict['nw_obj_global_firewall/addrgrp']]
                break_flag = False # if we find a match we stop the two inner for-loops
                for tab in object_tables:
                    if break_flag:
                        found = True
                        break
                    else:
                        for obj in tab:
                            if obj[name_key] == el:
                                ref_list.append(obj[uid_key])
                                break_flag = True
                                found = True
                                break
            elif obj_type == 'service':
                print('later')  # todo
        else:
            print('decide what to do')
        if not found:
            objects_not_found.append(el)
    for obj in objects_not_found:

        if obj != 'all' and obj != 'Original':
            if not fwo_api.create_data_issue(fwo_api_base_url, jwt, import_id=import_id, obj_name=obj, severity=1, rule_uid=rule_uid, mgm_id=mgm_id, object_type=object_type):
                logger.warning("resolve_raw_objects: encountered error while trying to log an import data issue using create_data_issue")

            desc = "found a broken network object reference '" + obj + "' "
            if object_type is not None:
                desc +=  "(type=" + object_type + ") "
            desc += "in rule with UID '" + rule_uid + "'"
            fwo_api.setAlert(fwo_api_base_url, jwt, import_id=import_id, title="object reference error", mgm_id=mgm_id, severity=1, role='importer', \
                description=desc, source='import', alertCode=16)

    return delimiter.join(ref_list)


def jsonToLogFormat(jsonData):
    if type(jsonData) is dict:
        jsonString = json.dumps(jsonData)
    elif isinstance(jsonData, str):
        jsonString = jsonData
    else:
        jsonString = str(jsonData)
    
    if jsonString[0] == '{' and jsonString[-1] == '}':
        jsonString = jsonString[1:len(jsonString)-1]
    return jsonString


def writeAlertToLogFile(jsonData):
    logger = getFwoLogger()
    jsonDataCopy = deepcopy(jsonData)   # make sure the original alert is not changed
    if type(jsonDataCopy) is dict and 'jsonData' in jsonDataCopy:
        subDict = json.loads(jsonDataCopy.pop('jsonData'))
        jsonDataCopy.update(subDict)
    alertText = "FWORCHAlert - " + jsonToLogFormat(jsonDataCopy)
    if 'severity' in jsonDataCopy:
        if int(jsonDataCopy['severity'])>=2:
            if int(jsonDataCopy['severity'])<=3:
                logger.warning(alertText)
            else:
                logger.error(alertText)
            return
    logger.info(alertText)


def set_ssl_verification(ssl_verification_mode, debug_level=0):
    logger = getFwoLogger(debug_level=debug_level)
    if ssl_verification_mode == '' or ssl_verification_mode == 'off':
        ssl_verification = False
        if debug_level>5:
            logger.debug("ssl_verification: False")
    else:
        ssl_verification = ssl_verification_mode
        if debug_level>5:
            logger.debug("ssl_verification: [ca]certfile=" + ssl_verification)
    return ssl_verification

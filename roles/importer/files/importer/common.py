import logging
import traceback
import sys, os, time, datetime
import json, requests, requests.packages
import logging, socket
import importlib.util
base_dir = '/usr/local/fworch'
importer_base_dir = base_dir + '/importer'
from pathlib import Path
sys.path.append(importer_base_dir) # adding absolute path here once
import fwo_api

fw_module_name = 'fwcommon'  # the module start-point for product specific code
full_config_size_limit = 5000000 # native configs greater than 5 MB will not stored in DB
config2import_size_limit = 10000000 # native configs greater than 10 MB will be delted from import_config table after import
csv_delimiter = '%'
list_delimiter = '|'
line_delimiter = "\n"
apostrophe = "\""
section_header_uids=[]
nat_postfix = '_NatNwObj'
fwo_api_http_import_timeout = 14400 # 4 hours

# how many objects (network, services, rules, ...) should be sent to the FWO API in one go?
# should be between 500 and 2.000 in production (results in a max obj number of 5 x this value)
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

#  import_management: import a single management (if no import for it is running)
#     lock mgmt for import via FWORCH API call, generating new import_id y
#     check if we need to import (no md5, api call if anything has changed since last import)
#     get complete config (get, enrich, parse)
#     write into json dict write json dict to new table (single entry for complete config)
#     trigger import from json into csv and from there into destination tables
#     release mgmt for import via FWORCH API call (also removing import_id y data from import_tables?)
#     no changes: remove import_control?
def import_management(mgm_id=None, ssl='off', debug_level=0, proxy='', in_file=None, limit=150, force=False):
    error_count = 0
    importer_user_name = 'importer'  # todo: move to config file?
    fwo_config_filename = base_dir + '/etc/fworch.json'
    importer_pwd_file = base_dir + '/etc/secrets/importer_pwd'
    import_tmp_path = base_dir + '/tmp/import'
    change_count = 0
    error_string = ''
    start_time = int(time.time())
    debug_level=int(debug_level)

    if ssl == '' or ssl == 'off':
        requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only
    set_log_level(log_level=debug_level, debug_level=debug_level)
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
        logging.error(e.message)
        return e.message
    except:
        return "unspecified error during FWO API login"

    try: # get mgm_details (fw-type, port, ip, user credentials):
        mgm_details = fwo_api.get_mgm_details(fwo_api_base_url, jwt, {"mgmId": int(mgm_id)})
    except:
        logging.error("import_management - error while getting fw management details for mgm=" + str(mgm_id) )
        raise
    
    if mgm_details['importDisabled']:
        logging.info("import_management - import disabled for mgm " + str(mgm_id))
    else:
        Path(import_tmp_path).mkdir(parents=True, exist_ok=True)  # make sure tmp path exists
        package_list = []
        for dev in mgm_details['devices']:
            package_list.append(dev['package_name'])

        # only run if this is the correct import module
        if mgm_details['importerHostname'] != socket.gethostname():
            logging.debug("import_management - this host (" + socket.gethostname() + ") is not responsible for importing management " + str(mgm_id))
            return ""

        # set import lock
        current_import_id = -1
        try: 
            current_import_id = fwo_api.lock_import(
                fwo_api_base_url, jwt, {"mgmId": int(mgm_id)})
        except:
            logging.error("import_management - failed to get import lock for management id " + str(mgm_id))
        if current_import_id == -1:
            raise FwoApiFailedLockImport("fwo_api: failed to get import lock for management id " + str(mgm_id)) from None
        logging.debug("start import of management " + str(mgm_id) + ", import_id=" + str(current_import_id))

        full_config_json = {}
        config2import = {}
        rulebase_string = ''
        for device in mgm_details['devices']:
            rulebase_string += device['local_rulebase_name'] + ','
        rulebase_string = rulebase_string[:-1]  # remove final comma

        if in_file is not None:    # read native config from file
            try:
                with open(in_file, 'r') as json_file:
                    full_config_json = json.load(json_file)
            except:
                logging.exception("import_management - error while reading json import from file", traceback.format_exc())        
                raise

        secret_filename = base_dir + '/tmp/import/mgm_id_' + str(mgm_id) + '_secret.txt'
        try:
            with open(secret_filename, "w") as secret:  # write pwd to disk to avoid passing it as parameter
                secret.write(mgm_details['secret'])
        except:
            logging.exception("import_management - error while writing secrets file to disk", traceback.format_exc())        
            raise

        try: # pick product-specific importer:
            pkg_name = mgm_details['deviceType']['name'].lower().replace(' ', '') + mgm_details['deviceType']['version']
            fw_module = importlib.import_module("." + fw_module_name, pkg_name)
        except:
            logging.exception("import_management - error while loading product specific fwcommon module", traceback.format_exc())        
            raise
        
        try: # get config from product-specific FW API
            get_config_response = fw_module.get_config(
                config2import, full_config_json,  current_import_id, mgm_details, debug_level=debug_level, 
                    ssl_verification=ssl, proxy=proxy, limit=limit, force=force)
        except FwLoginFailed as e:
            logging.error("mgm_id=" + str(mgm_id) + ", mgm_name=" + mgm_details['name'] + ", " + e.message)
            fwo_api.delete_import(fwo_api_base_url, jwt, current_import_id) # deleting trace of not even begun import
            raise FwLoginFailed(e.message)
        except:
            logging.error("import_management - unspecified error while getting config: " + str(traceback.format_exc()))
            raise

        if debug_level>2:   # debugging: writing config to json file
            logging.debug("import_management: get_config completed, now writing debug config json files")
            try:
                normalized_config_filename = import_tmp_path + '/mgm_id_' + \
                    str(mgm_id) + '_config_normalized.json'
                with open(normalized_config_filename, "w") as json_data:
                    json_data.write(json.dumps(config2import, indent=2))

                if debug_level>3 and in_file is None:   # do not write native config if we just got it as input file
                    full_native_config_filename = import_tmp_path + '/mgm_id_' + \
                        str(mgm_id) + '_config_native.json'
                    with open(full_native_config_filename, "w") as json_data:  # create empty config file
                        json_data.write(json.dumps(full_config_json, indent=2))
            except:
                logging.error("import_management - unspecified error while dumping config to json file: " + str(traceback.format_exc()))
                raise
            
        if get_config_response == 1:
            error_count += get_config_response
        elif get_config_response == 0:

            try: # split the config into multiple chunks not bigger than x elements
                config2import_list = split_config(config2import)
            except:
                logging.error("import_management - unspecified error while splitting config: " + str(traceback.format_exc()))
                raise

            try: # now we import the config via API:
                idx = 0
                while idx<len(config2import_list):
                    if idx==len(config2import_list)-1:
                        start_import_flag = True
                    else:
                        start_import_flag = False
                    error_count += fwo_api.import_json_config(fwo_api_base_url, jwt, mgm_id, {
                        "start_import_flag": start_import_flag, "importId": current_import_id, "mgmId": mgm_id, "config": config2import_list[idx]},
                        debug_level=debug_level)
                    idx += 1
            except:
                logging.error("import_management - unspecified error while importing config via FWO API: " + str(traceback.format_exc()))
                raise

            try: # checking for errors during stored_procedure db imort in import_control table
                error_from_imp_control = fwo_api.get_error_string_from_imp_control(
                    fwo_api_base_url, jwt, {"importId": current_import_id})
            except:
                logging.error("import_management - unspecified error while getting error string: " + str(traceback.format_exc()))

            if error_from_imp_control != None and error_from_imp_control != [{'import_errors': None}]:
                error_count += 1
                error_string += str(error_from_imp_control)
            # todo: if no objects found at all: at least throw a warning

            try: # get change count from db
                change_count = fwo_api.count_changes_per_import(fwo_api_base_url, jwt, current_import_id)
            except:
                logging.error("import_management - unspecified error while getting change count: " + str(traceback.format_exc()))
                raise

            try: # calculate config sizes
                full_config_size = sys.getsizeof(json.dumps(full_config_json))
                config2import_size = sys.getsizeof(json.dumps(config2import))
                logging.debug("full_config size: " + str(full_config_size) + " bytes, config2import size: " + str(config2import_size) + " bytes")
            except:
                logging.error("import_management - unspecified error while calculating config sizes: " + str(traceback.format_exc()))
                raise

            if (change_count > 0 or error_count > 0) and full_config_size < full_config_size_limit:  # store full config in case of change or error
                try:  # store full config in DB
                    error_count += fwo_api.store_full_json_config(fwo_api_base_url, jwt, mgm_id, {
                        "importId": current_import_id, "mgmId": mgm_id, "config": full_config_json})
                except:
                    logging.error("import_management - unspecified error while storing full config: " + str(traceback.format_exc()))
                    raise
        else: # if no changes were found, we get get_config_response==512 and we skip everything else without errors
            pass

        try: # CLEANUP: delete configs of imports (without changes) (if no error occured)
            if fwo_api.delete_json_config(fwo_api_base_url, jwt, {"importId": current_import_id})<0:
                error_count = +1
            # if change_count == 0 and error_count == 0 and get_config_response < 2:
                # error_count += fwo_api.delete_import(fwo_api_base_url, jwt, current_import_id)  # this clears the import_control entry completely
            if change_count != 0 and config2import_size > config2import_size_limit:
                error_count += fwo_api.delete_json_config(fwo_api_base_url, jwt, {"importId": current_import_id})
            if os.path.exists(secret_filename):
                os.remove(secret_filename)
        except:
            logging.error("import_management - unspecified error cleaning up: " + str(traceback.format_exc()))
            raise

        try: # finalize import by unlocking it
            error_count += fwo_api.unlock_import(fwo_api_base_url, jwt, int(
                mgm_id), datetime.datetime.now().isoformat(), current_import_id, error_count, change_count)
        except:
            logging.error("import_management - unspecified error while unlocking import: " + str(traceback.format_exc()))
            raise

        import_result = "import_management: import no. " + str(current_import_id) + \
            " for management " + mgm_details['name'] + ' (id=' + str(mgm_id) + ")" + \
            str(" threw errors," if error_count else " successful,") + \
            " change_count: " + str(change_count) + \
            ", duration: " + str(int(time.time()) - start_time) + "s" 
        import_result += "\n   ERRORS: " + error_string if len(error_string) > 0 else ""
        logging.info(import_result)
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


def split_config(config2import):
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
    return conf_split


def set_log_level(log_level, debug_level):
    # todo: save the initial value, reset initial value at the end
    logger = logging.getLogger(__name__)
    # todo: use log_level to define non debug logging
    #       use debug_level to define different debug levels
    if debug_level >= 1:
        logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    else:
        logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
    #logger.debug ("debug_level: "+ str(debug_level) )


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
    #if result != '':  # do not add apostrophes for empty fields
    #    result = apostrophe + escaped_field + apostrophe
    return result


def extend_string_list(list_string, src_dict, key, delimiter):
    if list_string is None:
        list_string = ''
    if list_string == '':
        if key in src_dict:
            result = delimiter.join(src_dict[key])
        else:
            result = ''
    else:
        if key in src_dict:
            old_list = list_string.split(delimiter)
            combined_list = old_list + src_dict[key]
            result = delimiter.join(combined_list)
        else:
            result = list_string
    return result


# def resolve_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key, rule_type=None):
#     ref_list = []
#     for el in obj_name_string_list.split(delimiter):
#         for obj in obj_dict:
#             if obj[name_key] == el:
#                 ref_list.append(obj[uid_key])
#                 break
#     return delimiter.join(ref_list)

def resolve_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key):
    # guessing ipv4 and adom (to also search global objects)
    return resolve_raw_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key, rule_type='v4_adom', obj_type='network')


def resolve_raw_objects (obj_name_string_list, delimiter, obj_dict, name_key, uid_key, rule_type=None, obj_type='network'):
    ref_list = []
    for el in obj_name_string_list.split(delimiter):
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
                        break
                    else:
                        for obj in tab:
                            if obj[name_key] == el:
                                ref_list.append(obj[uid_key])
                                break_flag = True
                                break
            elif obj_type == 'service':
                print('later')  # todo
        else:
            print('decide what to do')
    return delimiter.join(ref_list)

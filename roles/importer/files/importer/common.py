from email import message
import logging
import sys, os, time, datetime
import json, requests, requests.packages, argparse
import importlib, logging, socket
from pathlib import Path
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
import common, fwo_api

full_config_size_limit = 5000000 # native configs greater than 5 MB will not stored in DB
config2import_size_limit = 10000000 # native configs greater than 10 MB will be delted from import_config table after import
csv_delimiter = '%'
list_delimiter = '|'
line_delimiter = "\n"
apostrophe = "\""
section_header_uids=[]
nat_postfix = '_NatNwObj'


# class Error(Exception):
#     """Base class for other exceptions"""
#     pass


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




def import_management(mgm_id=None, ssl='off', debug_level=0, proxy='', in_file=None, limit=150, force=False):
    error_count = 0
    change_count = 0
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
    common.set_log_level(log_level=debug_level, debug_level=debug_level)
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
    except common.FwoApiLoginFailed as e:
        logging.error(e.message)
        return e.message
    except Exception:
        return "unspecified error during FWO API login"
    # get mgm_details (fw-type, port, ip, user credentials):
    mgm_details = fwo_api.get_mgm_details(fwo_api_base_url, jwt, {"mgmId": int(mgm_id)})

    package_list = []
    for dev in mgm_details['devices']:
        package_list.append(dev['package_name'])

    # only run if this is the correct import module
    if mgm_details['importerHostname'] != socket.gethostname():
        logging.debug("import_management - this host (" + socket.gethostname() + ") is not responsible for importing management " + str(mgm_id))
        return ""

    # set import lock
    current_import_id = fwo_api.lock_import(
        fwo_api_base_url, jwt, {"mgmId": int(mgm_id)})
    if current_import_id == -1:
        exception = "error while setting import lock for management id " + str(mgm_id) + ", import already running?"
        raise Exception(exception)

    logging.debug("start import of management " + str(mgm_id) + ", import_id=" + str(current_import_id))

    Path(import_tmp_path).mkdir(parents=True,
                                exist_ok=True)  # make sure tmp path exists

    full_config_json = {}
    config2import = {}
    rulebase_string = ''
    for device in mgm_details['devices']:
        rulebase_string += device['local_rulebase_name'] + ','
    rulebase_string = rulebase_string[:-1]  # remove final comma

    if in_file is not None:    # read native config from file
        with open(in_file, 'r') as json_file:
            full_config_json = json.load(json_file)

    secret_filename = base_dir + '/tmp/import/mgm_id_' + \
        str(mgm_id) + '_secret.txt'
    with open(secret_filename, "w") as secret:  # write pwd to disk to avoid passing it as parameter
        secret.write(mgm_details['secret'])

    # pick product-specific importer:
    fw_module_name = mgm_details['deviceType']['name'].lower().replace(
        ' ', '') + mgm_details['deviceType']['version']+'.fwcommon'
    fw_module = importlib.import_module(fw_module_name)

    # get config from FW API and write config to json file "config_filename"
    try:
        get_config_response = fw_module.get_config(
            config2import, full_config_json,  current_import_id, mgm_details, debug_level=debug_level, 
                ssl_verification=ssl, proxy=proxy, limit=limit, force=force)
    except FwLoginFailed as e:
        logging.error("mgm_id=" + str(mgm_id) + ", mgm_name=" + mgm_details['name'] + ", " + e.message)
        fwo_api.delete_import(fwo_api_base_url, jwt, current_import_id)
        # fwo_api.unlock_import(fwo_api_base_url, jwt, int(mgm_id), datetime.datetime.now().isoformat(), current_import_id, error_count, change_count)
        raise FwLoginFailed(e.message)
    except Exception as e:
        error_text = "undefined error while getting config"
        if hasattr(e, message):
            error_text += ": " + message
        logging.error(error_text)
        raise Exception(error_text)
    logging.debug("import_management: get_config completed, now writing debug config json files")

    if debug_level>2:   # debugging: writing config to json file
        normalized_config_filename = import_tmp_path + '/mgm_id_' + \
            str(mgm_id) + '_config_normalized.json'
        with open(normalized_config_filename, "w") as json_data:
            json_data.write(json.dumps(config2import, indent=2))

        if debug_level>3:
            full_native_config_filename = import_tmp_path + '/mgm_id_' + \
                str(mgm_id) + '_config_native.json'
            with open(full_native_config_filename, "w") as json_data:  # create empty config file
                json_data.write(json.dumps(full_config_json, indent=2))

    # if no changes were found, we get get_config_response==512 and we skip everything else without errors
    # todo: re-structure this to make it more logical/readable

    if get_config_response == 1:
        error_count += get_config_response
    elif get_config_response == 0:
        # now we import the config via API:
        error_count += fwo_api.import_json_config(fwo_api_base_url, jwt, mgm_id, {
            "importId": current_import_id, "mgmId": mgm_id, "config": config2import})

        # checking for errors during imort
        error_string_from_imp_control = fwo_api.get_error_string_from_imp_control(
            fwo_api_base_url, jwt, {"importId": current_import_id})

        if error_string_from_imp_control != None and error_string_from_imp_control != [{'import_errors': None}]:
            error_count += 1
            error_string += str(error_string_from_imp_control)
        # todo: if no objects found at all: at least throw a warning

        change_count = fwo_api.count_changes_per_import(fwo_api_base_url, jwt, current_import_id)
        full_config_size = sys.getsizeof(json.dumps(full_config_json))
        config2import_size = sys.getsizeof(json.dumps(config2import))

        logging.debug("full_config size: " + str(full_config_size) + " bytes, config2import size: " + str(config2import_size) + " bytes")

        if (change_count > 0 or error_count > 0) and full_config_size < full_config_size_limit:  # store full config in case of change or error
            error_count += fwo_api.store_full_json_config(fwo_api_base_url, jwt, mgm_id, {
                "importId": current_import_id, "mgmId": mgm_id, "config": full_config_json})

    # CLEANUP: delete configs of imports without changes (if no error occured)
    if change_count == 0 and error_count == 0 and get_config_response < 2:
        error_count += fwo_api.delete_json_config(fwo_api_base_url, jwt, {"importId": current_import_id})
        error_count += fwo_api.delete_import(fwo_api_base_url, jwt, current_import_id)
    if change_count != 0 and config2import_size > config2import_size_limit:
        error_count += fwo_api.delete_json_config(fwo_api_base_url, jwt, {"importId": current_import_id})
    if os.path.exists(secret_filename):
        os.remove(secret_filename)

    # finalize import by unlocking it
    error_count += fwo_api.unlock_import(fwo_api_base_url, jwt, int(
        mgm_id), datetime.datetime.now().isoformat(), current_import_id, error_count, change_count)

    import_result = "import_management: import no. " + str(current_import_id) + \
        " for management " + mgm_details['name'] + ' (id=' + str(mgm_id) + ")" + \
        str(" threw errors," if error_count else " successful,") + \
        " change_count: " + str(change_count) + \
        ", duration: " + str(int(time.time()) - start_time) + "s" 
    import_result += "\n   ERRORS: " + error_string if len(error_string) > 0 else ""
    logging.info(import_result)
    return import_result


def set_log_level(log_level, debug_level):
    # todo: save the initial value, reset initial value at the end
    logger = logging.getLogger(__name__)
    # todo: use log_level to define non debug logging
    #       use debug_level to define different debug levels
    if debug_level >= 1:
        logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    else:
        logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
    logger.debug ("debug_level: "+ str(debug_level) )


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

import traceback
import sys, time, datetime
import json, requests, requests.packages
from socket import gethostname
import importlib.util
from fwo_const import importer_base_dir
from pathlib import Path
sys.path.append(importer_base_dir) # adding absolute path here once
import fwo_api
from fwo_log import getFwoLogger
from fwo_config import readConfig
from fwo_const import fw_module_name, full_config_size_limit
from fwo_const import fwo_config_filename, importer_pwd_file, importer_user_name, import_tmp_path
import fwo_globals
import jsonpickle
from fwo_exception import FwoApiLoginFailed, FwoApiFailedLockImport, ConfigFileNotFound, FwLoginFailed, ImportRecursionLimitReached
from fwo_base import split_config


#  import_management: import a single management (if no import for it is running)
#     lock mgmt for import via FWORCH API call, generating new import_id y
#     check if we need to import (no md5, api call if anything has changed since last import)
#     get complete config (get, enrich, parse)
#     write into json dict write json dict to new table (single entry for complete config)
#     trigger import from json into csv and from there into destination tables
#     release mgmt for import via FWORCH API call (also removing import_id y data from import_tables?)
#     no changes: remove import_control?
def import_management(mgm_id=None, ssl_verification=None, debug_level_in=0, 
        limit=150, force=False, clearManagementData=False, suppress_cert_warnings_in=None,
        in_file=None, normalized_in_file=None):

    check_input_parameters(mgm_id=mgm_id, ssl_verification=ssl_verification, debug_level_in=debug_level_in, 
        limit=limit, force=force, clearManagementData=clearManagementData, suppress_cert_warnings_in=suppress_cert_warnings_in,
        in_file=in_file, normalized_in_file=normalized_in_file)

    error_count = 0
    change_count = 0
    error_string = ''
    start_time = int(time.time())
    debug_level=int(debug_level_in)
    config2import = { "network_objects": [], "service_objects": [], "user_objects": [], "zone_objects": [], "rules": [] }
    config_changed_since_last_import = True

    logger = getFwoLogger()

    fwo_config = readConfig(fwo_config_filename)

    # authenticate to get JWT
    with open(importer_pwd_file, 'r') as file:
        importer_pwd = file.read().replace('\n', '')
    try:
        jwt = fwo_api.login(importer_user_name, importer_pwd, fwo_config['user_management_api_base_url'])
    except FwoApiLoginFailed as e:
        logger.error(e.message)
        return e.message
    except:
        return "unspecified error during FWO API login"

    # set global https connection values
    fwo_globals.setGlobalValues (suppress_cert_warnings_in=suppress_cert_warnings_in, verify_certs_in=ssl_verification, debug_level_in=debug_level_in)
    if fwo_globals.verify_certs is None:    # not defined via parameter
        fwo_globals.verify_certs = fwo_api.get_config_value(fwo_config['fwo_api_base_url'], jwt, key='importCheckCertificates')=='True'
    if fwo_globals.suppress_cert_warnings is None:    # not defined via parameter
        fwo_globals.suppress_cert_warnings = fwo_api.get_config_value(fwo_config['fwo_api_base_url'], jwt, key='importSuppressCertificateWarnings')=='True'
    if fwo_globals.suppress_cert_warnings: # not defined via parameter
        requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

    try: # get mgm_details (fw-type, port, ip, user credentials):
        mgm_details = fwo_api.get_mgm_details(fwo_config['fwo_api_base_url'], jwt, {"mgmId": int(mgm_id)}, debug_level)
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
        if mgm_details['importerHostname'] != gethostname() and not force:
            logger.info("import_management - this host (" + gethostname() + ") is not responsible for importing management " + str(mgm_id))
            return ""

        current_import_id = -1

        try: # set import lock
            current_import_id = fwo_api.lock_import(fwo_config['fwo_api_base_url'], jwt, {"mgmId": int(mgm_id)})
        except:
            logger.error("import_management - failed to get import lock for management id " + str(mgm_id))
        if current_import_id == -1:
            fwo_api.create_data_issue(fwo_config['fwo_api_base_url'], jwt, mgm_id=int(mgm_id), severity=1, 
                description="failed to get import lock for management id " + str(mgm_id))
            fwo_api.setAlert(fwo_config['fwo_api_base_url'], jwt, import_id=current_import_id, title="import error", mgm_id=str(mgm_id), severity=1, role='importer', \
                description="fwo_api: failed to get import lock", source='import', alertCode=15, mgm_details=mgm_details)
            raise FwoApiFailedLockImport("fwo_api: failed to get import lock for management id " + str(mgm_id)) from None

        logger.info("starting import of management " + mgm_details['name'] + '(' + str(mgm_id) + "), import_id=" + str(current_import_id))
        full_config_json = {}

        if clearManagementData:
            logger.info('this import run will reset the configuration of this management to "empty"')
        else:
            if in_file is not None:    # read native config from file
                full_config_json, error_count, change_count = \
                    read_fw_json_config_file(filename=in_file, error_string=error_string, error_count=error_count, \
                    current_import_id=current_import_id, start_time=start_time, mgm_details=mgm_details, change_count=change_count, jwt=jwt)
           
            if normalized_in_file is not None:    # read normalized config from file
                config2import, error_count, change_count = \
                    read_fw_json_config_file(filename=normalized_in_file, error_string=error_string, error_count=error_count, \
                    current_import_id=current_import_id, start_time=start_time, mgm_details=mgm_details, change_count=change_count, jwt=jwt)
                replace_import_id(config2import, current_import_id)
            else:   # standard case, read config from FW API
                # note: we need to run get_config_from_api in any case (even when importing from a file) as this function 
                # also contains the conversion from native to config2import (parsing)
                ### geting config from firewall manager ######################
                config_changed_since_last_import, error_string, error_count, change_count = get_config_from_api(mgm_details, full_config_json, config2import, jwt, current_import_id, start_time,
                in_file=in_file, import_tmp_path=import_tmp_path, error_string=error_string, error_count=error_count, change_count=change_count, 
                limit=limit, force=force)
                if (debug_level>7):  # dump full native config read from fw API
                    logger.info(json.dumps(full_config_json, indent=2))

        time_get_config = int(time.time()) - start_time
        logger.debug("import_management - getting config total duration " + str(time_get_config) + "s")

        if config_changed_since_last_import:
            try: # now we import the config via API chunk by chunk:
                for config_chunk in split_config(config2import, current_import_id, mgm_id):
                    error_count += fwo_api.import_json_config(fwo_config['fwo_api_base_url'], jwt, mgm_id, config_chunk)
                    fwo_api.update_hit_counter(fwo_config['fwo_api_base_url'], jwt, mgm_id, config_chunk)
            except:
                logger.error("import_management - unspecified error while importing config via FWO API: " + str(traceback.format_exc()))
                raise
            time_write2api = int(time.time()) - time_get_config - start_time
            logger.debug("import_management - writing config to API and stored procedure import duration: " + str(time_write2api) + "s")

            error_from_imp_control = "assuming error"
            try: # checking for errors during stored_procedure db imort in import_control table
                error_from_imp_control = fwo_api.get_error_string_from_imp_control(fwo_config['fwo_api_base_url'], jwt, {"importId": current_import_id})
            except:
                logger.error("import_management - unspecified error while getting error string: " + str(traceback.format_exc()))

            if error_from_imp_control != None and error_from_imp_control != [{'import_errors': None}]:
                error_count += 1
                error_string += str(error_from_imp_control)
            # todo: if no objects found at all: at least throw a warning

            try: # get change count from db
                change_count = fwo_api.count_changes_per_import(fwo_config['fwo_api_base_url'], jwt, current_import_id)
            except:
                logger.error("import_management - unspecified error while getting change count: " + str(traceback.format_exc()))
                raise

            try: # calculate config sizes
                full_config_size = sys.getsizeof(json.dumps(full_config_json))
                config2import_size = sys.getsizeof(jsonpickle.dumps(config2import))
                logger.debug("full_config size: " + str(full_config_size) + " bytes, config2import size: " + str(config2import_size) + " bytes")
            except:
                logger.error("import_management - unspecified error while calculating config sizes: " + str(traceback.format_exc()))
                raise

            if (debug_level>5 or change_count > 0 or error_count > 0) and full_config_size < full_config_size_limit:  # store full config in case of change or error
                try:  # store full config in DB
                    error_count += fwo_api.store_full_json_config(fwo_config['fwo_api_base_url'], jwt, mgm_id, {
                        "importId": current_import_id, "mgmId": mgm_id, "config": full_config_json})
                except:
                    logger.error("import_management - unspecified error while storing full config: " + str(traceback.format_exc()))
                    raise
        else: # if no changes were found, we skip everything else without errors
            pass

        if (debug_level>8): # dump normalized config for debugging purposes
            logger.info(json.dumps(config2import, indent=2))

        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt)
        
    return error_count


def get_config_from_api(mgm_details, full_config_json, config2import, jwt, current_import_id, start_time,
        in_file=None, import_tmp_path='.', error_string='', error_count=0, change_count=0, limit=150, force=False):
    logger = getFwoLogger()
    fwo_config = readConfig(fwo_config_filename)

    try: # pick product-specific importer:
        pkg_name = mgm_details['deviceType']['name'].lower().replace(' ', '') + mgm_details['deviceType']['version']
        fw_module = importlib.import_module("." + fw_module_name, pkg_name)
    except:
        logger.exception("import_management - error while loading product specific fwcommon module", traceback.format_exc())        
        raise
    
    try: # get the config data from the firewall manager's API: 
        # check for changes from product-specific FW API
        config_changed_since_last_import = in_file != None or fw_module.has_config_changed(full_config_json, mgm_details, force=force)
        if config_changed_since_last_import:
            logger.debug ( "has_config_changed: changes found or forced mode -> go ahead with getting config, Force = " + str(force))
        else:
            logger.debug ( "has_config_changed: no new changes found")

        if config_changed_since_last_import:
            fw_module.get_config( # get config from product-specific FW API
                config2import, full_config_json,  current_import_id, mgm_details, 
                limit=limit, force=force, jwt=jwt)
    except (FwLoginFailed) as e:
        error_string += "  login failed: mgm_id=" + str(mgm_details['id']) + ", mgm_name=" + mgm_details['name'] + ", " + e.message
        error_count += 1
        logger.error(error_string)
        fwo_api.delete_import(fwo_config['fwo_api_base_url'], jwt, current_import_id) # deleting trace of not even begun import
        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt)
        raise FwLoginFailed(e.message)
    except ImportRecursionLimitReached as e:
        error_string += "  recursion limit reached: mgm_id=" + str(mgm_details['id']) + ", mgm_name=" + mgm_details['name'] + ", " + e.message
        error_count += 1
        logger.error(error_string)
        fwo_api.delete_import(fwo_config['fwo_api_base_url'], jwt, current_import_id) # deleting trace of not even begun import
        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt)
        raise ImportRecursionLimitReached(e.message)
    except:
        error_string += "  import_management - unspecified error while getting config: " + str(traceback.format_exc())
        logger.error(error_string)
        error_count += 1
        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt)
        raise

    logger.debug("import_management: get_config completed (including normalization), duration: " + str(int(time.time()) - start_time) + "s") 

    if config_changed_since_last_import and fwo_globals.debug_level>2:   # debugging: writing config to json file
        debug_start_time = int(time.time())
        try:
            normalized_config_filename = import_tmp_path + '/mgm_id_' + \
                str(mgm_details['id']) + '_config_normalized.json'
            with open(normalized_config_filename, "w") as json_data:
                json_data.write(json.dumps(jsonpickle.dumps(config2import)))

            if fwo_globals.debug_level>3:
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


def check_input_parameters(mgm_id, ssl_verification=None, debug_level_in=0, 
        limit=150, force=False, clearManagementData=False, suppress_cert_warnings_in=None,
        in_file=None, normalized_in_file=None):

    if mgm_id is None:
        raise BaseException("parameter mgm_id is mandatory")
    if in_file is not None and normalized_in_file is not None:
        raise BaseException("you cannot specify both in_file and normalized_in_file")

    
def complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt):
    logger = getFwoLogger()
    fwo_config = readConfig(fwo_config_filename)

    fwo_api.log_import_attempt(fwo_config['fwo_api_base_url'], jwt, mgm_details['id'], successful=not error_count)

    try: # CLEANUP: delete configs of imports (without changes) (if no error occured)
        if fwo_api.delete_json_config_in_import_table(fwo_config['fwo_api_base_url'], jwt, {"importId": current_import_id})<0:
            error_count += 1
    except:
        logger.error("import_management - unspecified error cleaning up import_config: " + str(traceback.format_exc()))

    try: # CLEANUP: delete data of this import from import_object/rule/service/user tables
        if fwo_api.delete_import_object_tables(fwo_config['fwo_api_base_url'], jwt, {"importId": current_import_id})<0:
            error_count += 1
    except:
        logger.error("import_management - unspecified error cleaning up import_ object tables: " + str(traceback.format_exc()))

    try: # finalize import by unlocking it
        error_count += fwo_api.unlock_import(fwo_config['fwo_api_base_url'], jwt, int(
            mgm_details['id']), datetime.datetime.now().isoformat(), current_import_id, error_count, change_count)
    except:
        logger.error("import_management - unspecified error while unlocking import: " + str(traceback.format_exc()))

    import_result = "import_management: import no. " + str(current_import_id) + \
            " for management " + mgm_details['name'] + ' (id=' + str(mgm_details['id']) + ")" + \
            str(" threw errors," if error_count else " successful,") + \
            " change_count: " + str(change_count) + \
            ", duration: " + str(int(time.time()) - start_time) + "s" 
    import_result += ", ERRORS: " + error_string if len(error_string) > 0 else ""
    
    if error_count>0:
        fwo_api.create_data_issue(fwo_config['fwo_api_base_url'], jwt, import_id=current_import_id, severity=1, description=error_string)
        fwo_api.setAlert(fwo_config['fwo_api_base_url'], jwt, import_id=current_import_id, title="import error", mgm_id=mgm_details['id'], severity=2, role='importer', \
            description=error_string, source='import', alertCode=14, mgm_details=mgm_details)

    logger.info(import_result)

    return error_count


def read_fw_json_config_file(filename=None, config={}, error_string='', error_count=0, current_import_id=None, start_time=0, mgm_details=None, change_count=0, jwt=''):

    # when we read from a normalized config file, it contains non-matching dev_ids in gw_ tables
    def replace_device_id(config, mgm_details):
        logger = getFwoLogger()
        if 'routing' in config or 'interfaces' in config:
            if len(mgm_details['devices'])>1:
                logger.warning('importing from config file with more than one device - just picking the first device at random')
            if len(mgm_details['devices'])>=1:
                # just picking the first device
                dev_id = mgm_details['devices'][0]['id']
                if 'routing' in config:
                    i=0
                    while i<len(config['routing']):
                        config['routing'][i]['routing_device'] = dev_id
                        i += 1
                if 'interfaces' in config:
                    i=0
                    while i<len(config['interfaces']):
                        config['interfaces'][i]['routing_device'] = dev_id
                        i += 1    

    try:
        if filename is not None:
            if 'http://' in filename or 'https://' in filename:   # gettinf file via http(s)
                session = requests.Session()
                session.headers = { 'Content-Type': 'application/json' }
                session.verify=fwo_globals.verify_certs
                r = session.get(filename, )
                r.raise_for_status()
                config = json.loads(r.content)
            else:   # reading from local file
                with open(filename, 'r') as json_file:
                    config = json.load(json_file)
    except requests.exceptions.RequestException:
        error_string = 'got HTTP status code{code} while trying to read config file from URL {filename}'.format(code=str(r.status_code), filename=filename)
        error_count += 1
        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt)
        raise ConfigFileNotFound(error_string) from None
    except:
        # logger.exception("import_management - error while reading json import from file", traceback.format_exc())
        error_string = "Could not read config file {filename}".format(filename=filename)
        error_count += 1
        error_count = complete_import(current_import_id, error_string, start_time, mgm_details, change_count, error_count, jwt)
        raise ConfigFileNotFound(error_string) from None
    
    replace_device_id(config, mgm_details)

    return config, error_count, change_count


    # when we read from a normalized config file, it contains non-matching import ids, so updating them
    # for native configs this function should do nothing
def replace_import_id(config, current_import_id):
    logger = getFwoLogger()
    for tab in ['network_objects', 'service_objects', 'user_objects', 'zone_objects', 'rules']:
        if tab in config:
            for item in config[tab]:
                if 'control_id' in item:
                    item['control_id'] = current_import_id
        else: # assuming native config is read
            pass
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
from fwo_exception import FwoApiLoginFailed, FwoApiFailedLockImport, FwLoginFailed, ImportRecursionLimitReached
from fwo_base import split_config
import re
import fwo_file_import


class FwConfig():
    ConfigFormat: str
    Config: dict

    def __init__(self, configFormat=None, config=None):
        if configFormat is not None:
            self.ConfigFormat = configFormat
        else:
            self.ConfigFormat = None
        if config is not None:
            self.Config = config
        else:
            self.Config = {}

    @classmethod
    def from_json(cls, json_dict):
        ConfigFormat = json_dict['config-type']
        Config = json_dict['config']
        return cls(ConfigFormat, Config)

    def __str__(self):
        return f"{self.ConfigType}({str(self.Config)})"

class ManagementDetails():
    Id: int
    Name: str
    Hostname: str
    ImportDisabled: bool
    Devices: dict
    ImporterHostname: str
    DeviceTypeName: str
    DeviceTypeVersion: str

    def __init__(self, hostname, id, importDisabled, devices, importerHostname, name, deviceTypeName, deviceTypeVersion):
        self.Hostname = hostname
        self.Id = id
        self.ImportDisabled = importDisabled
        self.Devices = devices
        self.ImporterHostname = importerHostname
        self.Name = name
        self.DeviceTypeName = deviceTypeName
        self.DeviceTypeVersion = deviceTypeVersion

    @classmethod
    def from_json(cls, json_dict):
        Hostname = json_dict['hostname']
        Id = json_dict['id']
        ImportDisabled = json_dict['importDisabled']
        Devices = json_dict['devices']
        ImporterHostname = json_dict['importerHostname']
        Name = json_dict['name']
        DeviceTypeName = json_dict['deviceType']['name']
        DeviceTypeVersion = json_dict['deviceType']['version']
        return cls(Hostname, Id, ImportDisabled, Devices, ImporterHostname, Name, DeviceTypeName, DeviceTypeVersion)

    def __str__(self):
        return f"{self.Hostname}({self.Id})"


"""Used for storing state during import process per management"""
class ImportState():
    ErrorCount: int
    ChangeCount: int
    ErrorString: str
    StartTime: int
    DebugLevel: int
    Config2import: dict
    ConfigChangedSinceLastImport: bool
    FwoConfig: dict
    MgmDetails: dict
    FullMgmDetails: dict
    ImportId: int
    Jwt: str
    ImportFileName: str
    ForceImport: str


    def __init__(self, debugLevel, configChangedSinceLastImport, fwoConfig, mgmDetails, jwt, force):
        self.ErrorCount = 0
        self.ChangeCount = 0
        self.ErrorString = ''
        self.StartTime = int(time.time())
        self.DebugLevel = debugLevel
        self.Config2import = { "network_objects": [], "service_objects": [], "user_objects": [], "zone_objects": [], "rules": [] }
        self.ConfigChangedSinceLastImport = configChangedSinceLastImport
        self.FwoConfig = fwoConfig
        self.MgmDetails = ManagementDetails.from_json(mgmDetails)
        self.FullMgmDetails = mgmDetails
        self.ImportId = None
        self.Jwt = jwt
        self.ImportFileName = None
        self.ForceImport = force

    def __str__(self):
        return f"{str(self.ManagementDetails)}({self.age})"
    
    def setImportFileName(self, importFileName):
        self.ImportFileName = importFileName

    def setImportId(self, importId):
        self.ImportId = importId

    def setChangeCounter(self, changeNo):
        self.ChangeCount = changeNo

    def setErrorCounter(self, errorNo):
        self.ErrorCount = errorNo

    def setErrorString(self, errorStr):
        self.ErrorString = errorStr


#  import_management: import a single management (if no import for it is running)
#     lock mgmt for import via FWORCH API call, generating new import_id y
#     check if we need to import (no md5, api call if anything has changed since last import)
#     get complete config (get, enrich, parse)
#     write into json dict write json dict to new table (single entry for complete config)
#     trigger import from json into csv and from there into destination tables
#     release mgmt for import via FWORCH API call (also removing import_id y data from import_tables?)
#     no changes: remove import_control?
def import_management(mgmId=None, ssl_verification=None, debug_level_in=0, 
        limit=150, force=False, clearManagementData=False, suppress_cert_warnings_in=None,
        in_file=None):

    importState = initializeImport(mgmId, debugLevel=debug_level_in, force=force)
    logger = getFwoLogger()
    config_changed_since_last_import = True
  
    if importState.MgmDetails.ImportDisabled and not importState.ForceImport:
        logger.info("import_management - import disabled for mgm " + str(mgmId))
    else:
        Path(import_tmp_path).mkdir(parents=True, exist_ok=True)  # make sure tmp path exists
        package_list = []
        for dev in importState.MgmDetails.Devices:
            package_list.append(dev['package_name'])

        # only run if this is the correct import module
        if importState.MgmDetails.ImporterHostname != gethostname() and not importState.ForceImport:
            logger.info("import_management - this host (" + gethostname() + ") is not responsible for importing management " + str(mgmId))
            return ""

        setImportLock(importState)
        logger.info("starting import of management " + importState.MgmDetails.Name + '(' + str(mgmId) + "), import_id=" + str(importState.ImportId))
        full_config_json = {}
        config2import = {}

        if clearManagementData:
            logger.info('this import run will reset the configuration of this management to "empty"')
        else:
            configObj = FwConfig()            
            if in_file is None: # if the host name is an URI, do not connect to an API but simply read the config from this URI
                if stringIsUri(importState.MgmDetails.Hostname):
                    importState.setImportFileName(importState.MgmDetails.Hostname)
            else:
                importState.setImportFileName(in_file)
            if importState.ImportFileName is not None:
                configFromFile = fwo_file_import.readJsonConfigFromFile(importState, full_config_json)
                if 'config-format' in configFromFile:
                    if 'fw-config' in configFromFile:
                        configObj = FwConfig(configFromFile['config-format'], configFromFile['fw-config'])
                    else:
                        configObj = FwConfig(configFromFile['config-format'], configFromFile)
                else:   # assuming native config
                    # config2import = configFromFile
                    configObj = FwConfig('native', configFromFile)

            if configObj.ConfigFormat != 'normalized':
                # before importing from normalized config file, we need to replace the import id:
                if importState.ImportFileName is not None:
                    replace_import_id(configObj.Config, importState.ImportId)

                ### geting config from firewall manager ######################
                # note: we need to run get_config_from_api in any case (even when importing from a file) as this function 
                # also contains the conversion from native to config2import (parsing)
                config_changed_since_last_import = get_config_from_api(importState, configObj.Config, config2import)
                if (importState.DebugLevel>8):  # dump full native config read from fw API
                    logger.info(json.dumps(full_config_json, indent=2))

        time_get_config = int(time.time()) - importState.StartTime
        logger.debug("import_management - getting config total duration " + str(int(time.time()) - importState.StartTime) + "s")

        if config_changed_since_last_import or importState.ForceImport:
            try: # now we import the config via API chunk by chunk:
                for config_chunk in split_config(config2import, importState.ImportId, mgmId):
                    importState.ErrorCount += fwo_api.import_json_config(importState, config_chunk)
                    fwo_api.update_hit_counter(importState, config_chunk)
            except:
                logger.error("import_management - unspecified error while importing config via FWO API: " + str(traceback.format_exc()))
                raise
            time_write2api = int(time.time()) - time_get_config - importState.StartTime
            logger.debug("import_management - writing config to API and stored procedure import duration: " + str(time_write2api) + "s")

            error_from_imp_control = "assuming error"
            try: # checking for errors during stored_procedure db imort in import_control table
                error_from_imp_control = fwo_api.get_error_string_from_imp_control(importState, {"importId": importState.ImportId})
            except:
                logger.error("import_management - unspecified error while getting error string: " + str(traceback.format_exc()))

            if error_from_imp_control != None and error_from_imp_control != [{'import_errors': None}]:
                importState.setErrorCounter(importState.ErrorCount + 1)
                importState.setErrorString(importState.ErrorString + str(error_from_imp_control))
            # todo: if no objects found at all: at least throw a warning

            try: # get change count from db
                # temporarily only count rule changes until change report also includes other changes
                # change_count = fwo_api.count_changes_per_import(fwo_config['fwo_api_base_url'], jwt, current_import_id)
                change_count = fwo_api.count_rule_changes_per_import(importState.FwoConfig['fwo_api_base_url'], importState.Jwt, importState.ImportId)
                importState.setChangeCounter(change_count)
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

            if (importState.DebugLevel>5 or change_count > 0 or importState.ErrorCount > 0) and full_config_size < full_config_size_limit:  # store full config in case of change or error
                try:  # store full config in DB
                    importState.setErrorCounter(importState.ErrorCount + fwo_api.store_full_json_config(importState, {
                        "importId": importState.ImportId, "mgmId": mgmId, "config": full_config_json}))
                except:
                    logger.error("import_management - unspecified error while storing full config: " + str(traceback.format_exc()))
                    raise
        else: # if no changes were found, we skip everything else without errors
            pass

        if (importState.DebugLevel>7): # dump normalized config for debugging purposes
            logger.info(json.dumps(config2import, indent=2))

        importState.setErrorCounter(fwo_api.complete_import(importState))
        
    return importState.ErrorCount


# when we read from a normalized config file, it contains non-matching import ids, so updating them
# for native configs this function should do nothing
def replace_import_id(config, current_import_id):
    for tab in ['network_objects', 'service_objects', 'user_objects', 'zone_objects', 'rules']:
        if tab in config:
            for item in config[tab]:
                if 'control_id' in item:
                    item['control_id'] = current_import_id
        else: # assuming native config is read
            pass


def initializeImport(mgmId, debugLevel=0, suppressCertWarnings=False, sslVerification=False, force=False):

    def check_input_parameters(mgmId):
        if mgmId is None:
            raise BaseException("parameter mgm_id is mandatory")

    logger = getFwoLogger()
    check_input_parameters(mgmId)

    fwoConfig = readConfig(fwo_config_filename)

    # authenticate to get JWT
    with open(importer_pwd_file, 'r') as file:
        importer_pwd = file.read().replace('\n', '')
    try:
        jwt = fwo_api.login(importer_user_name, importer_pwd, fwoConfig['user_management_api_base_url'])
    except FwoApiLoginFailed as e:
        logger.error(e.message)
        return e.message
    except:
        return "unspecified error during FWO API login"

    # set global https connection values
    fwo_globals.setGlobalValues (suppress_cert_warnings_in=suppressCertWarnings, verify_certs_in=sslVerification, debug_level_in=debugLevel)
    if fwo_globals.verify_certs is None:    # not defined via parameter
        fwo_globals.verify_certs = fwo_api.get_config_value(fwoConfig['fwo_api_base_url'], jwt, key='importCheckCertificates')=='True'
    if fwo_globals.suppress_cert_warnings is None:    # not defined via parameter
        fwo_globals.suppress_cert_warnings = fwo_api.get_config_value(fwoConfig['fwo_api_base_url'], jwt, key='importSuppressCertificateWarnings')=='True'
    if fwo_globals.suppress_cert_warnings: # not defined via parameter
        requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only    

    try: # get mgm_details (fw-type, port, ip, user credentials):
        mgmDetails = fwo_api.get_mgm_details(fwoConfig['fwo_api_base_url'], jwt, {"mgmId": int(mgmId)}, debugLevel) 
    except:
        logger.error("import_management - error while getting fw management details for mgm=" + str(mgmId) )
        raise

    # return ImportState (int(debugLevel), True, fwoConfig, mgmDetails) 
    return ImportState (
        debugLevel = int(debugLevel),
        configChangedSinceLastImport = True,
        fwoConfig = fwoConfig,
        mgmDetails = mgmDetails,
        jwt = jwt,
        force = force
    ) 


def stringIsUri(s):
    return re.match('http://.+', s) or re.match('https://.+', s) or  re.match('file://.+', s)


def setImportLock(importState):
        logger = getFwoLogger()
        try: # set import lock
            # url = importState.FwoConfig['fwo_api_base_url']
            url = importState.FwoConfig['fwo_api_base_url']
            mgmId = int(importState.MgmDetails.Id)
            importState.setImportId(fwo_api.lock_import(url, importState.Jwt, {"mgmId": mgmId }))
        except:
            logger.error("import_management - failed to get import lock for management id " + str(mgmId))
            importState.setImportId(-1)
        if importState.ImportId == -1:
            fwo_api.create_data_issue(importState.FwoConfig['fwo_api_base_url'], importState.Jwt, mgm_id=int(importState.MgmDetails.Id), severity=1, 
                description="failed to get import lock for management id " + str(mgmId))
            fwo_api.setAlert(url, importState.Jwt, import_id=importState.ImportId, title="import error", mgm_id=str(mgmId), severity=1, role='importer', \
                description="fwo_api: failed to get import lock", source='import', alertCode=15, mgm_details=importState.MgmDetails)
            raise FwoApiFailedLockImport("fwo_api: failed to get import lock for management id " + str(mgmId)) from None


def get_config_from_api(importState, full_config_json, config2import, import_tmp_path='.', limit=150):
    logger = getFwoLogger()

    try: # pick product-specific importer:
        pkg_name = importState.MgmDetails.DeviceTypeName.lower().replace(' ', '') + importState.MgmDetails.DeviceTypeVersion
        fw_module = importlib.import_module("." + fw_module_name, pkg_name)
    except:
        logger.exception("import_management - error while loading product specific fwcommon module", traceback.format_exc())        
        raise
    
    try: # get the config data from the firewall manager's API: 
        # check for changes from product-specific FW API
        config_changed_since_last_import = importState.ImportFileName != None or fw_module.has_config_changed(full_config_json, importState.FullMgmDetails, force=importState.ForceImport)
        if config_changed_since_last_import:
            logger.debug ( "has_config_changed: changes found or forced mode -> go ahead with getting config, Force = " + str(importState.ForceImport))
        else:
            logger.debug ( "has_config_changed: no new changes found")

        if config_changed_since_last_import or importState.ForceImport:
            fw_module.get_config( # get config from product-specific FW API
                config2import, full_config_json,  importState.ImportId, importState.FullMgmDetails, 
                limit=limit, force=importState.ForceImport, jwt=importState.Jwt)
    except (FwLoginFailed) as e:
        importState.ErrorString += "  login failed: mgm_id=" + str(importState.MgmDetails.Id) + ", mgm_name=" + importState.MgmDetails.Name + ", " + e.message
        importState.ErrorCount += 1
        logger.error(importState.ErrorString)
        fwo_api.delete_import(importState) # deleting trace of not even begun import
        importState.ErrorCount = fwo_api.complete_import(importState)
        raise FwLoginFailed(e.message)
    except ImportRecursionLimitReached as e:
        importState.ErrorString += "  recursion limit reached: mgm_id=" + str(importState.MgmDetails.Id) + ", mgm_name=" + importState.MgmDetails.Name + ", " + e.message
        importState.ErrorCount += 1
        logger.error(importState.ErrorString)
        fwo_api.delete_import(importState.Jwt) # deleting trace of not even begun import
        importState.ErrorCount = fwo_api.complete_import(importState)
        raise ImportRecursionLimitReached(e.message)
    except:
        importState.ErrorString += "  import_management - unspecified error while getting config: " + str(traceback.format_exc())
        logger.error(importState.ErrorString)
        importState.ErrorCount += 1
        importState.ErrorCount = fwo_api.complete_import(importState)
        raise

    logger.debug("import_management: get_config completed (including normalization), duration: " + str(int(time.time()) - importState.StartTime) + "s") 

    if config_changed_since_last_import and fwo_globals.debug_level>2:   # debugging: writing config to json file
        debug_start_time = int(time.time())
        try:
            normalized_config_filename = import_tmp_path + '/mgm_id_' + \
                str(importState.MgmDetails.Id) + '_config_normalized.json'
            with open(normalized_config_filename, "w") as json_data:
                json_data.write(json.dumps(jsonpickle.dumps(config2import)))

            if fwo_globals.debug_level>3:
                full_native_config_filename = import_tmp_path + '/mgm_id_' + \
                    str(importState.MgmDetails.Id) + '_config_native.json'
                with open(full_native_config_filename, "w") as json_data:  # create empty config file
                    json_data.write(json.dumps(full_config_json, indent=2))
        except:
            logger.error("import_management - unspecified error while dumping config to json file: " + str(traceback.format_exc()))
            raise

        time_write_debug_json = int(time.time()) - debug_start_time
        logger.debug("import_management - writing debug config json files duration " + str(time_write_debug_json) + "s")
    return config_changed_since_last_import

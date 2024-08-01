import traceback
import sys, time
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
from fwo_exception import FwoApiLoginFailed, FwLoginFailed, ImportRecursionLimitReached
from fwo_base import stringIsUri, calcManagerUidHash
import fwo_file_import
from fwoBaseImport import FworchConfig, ImportState
from fwconfig import ConfFormat, ConfigAction, ConfigAction, FwConfigManagerList, FwConfigNormalized, FwConfigManager


"""  
    import_management: import a single management (if no import for it is running)
    lock mgmt for import via FWORCH API call, generating new import_id y
    check if we need to import (no md5, api call if anything has changed since last import)
    get complete config (get, enrich, parse)
    write into json dict write json dict to new table (single entry for complete config)
    trigger import from json into csv and from there into destination tables
    release mgmt for import via FWORCH API call (also removing import_id y data from import_tables?)
    no changes: remove import_control?
"""
def import_management(mgmId=None, ssl_verification=None, debug_level_in=0, 
        limit=150, force=False, clearManagementData=False, suppress_cert_warnings_in=None,
        in_file=None):

    logger = getFwoLogger()
    config_changed_since_last_import = True
    importState = initializeImport(mgmId, debugLevel=debug_level_in, force=force)
    if type(importState) is str:
        return 1
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

        fwo_api.setImportLock(importState)
        logger.info("starting import of management " + importState.MgmDetails.Name + '(' + str(mgmId) + "), import_id=" + str(importState.ImportId))
        configNormalized = {}

        if clearManagementData:
            logger.info('this import run will reset the configuration of this management to "empty"')
            configNormalized = FwConfigManagerList()
            configNormalized.addManager(manager=FwConfigManager(calcManagerUidHash(importState.FullMgmDetails)))
            configNormalized.ManagerSet[0].Configs.append(FwConfigNormalized(ConfigAction.INSERT, [], [], [], [], []))
        else:
            # configObj = FwConfig()            
            if in_file is None: # if the host name is an URI, do not connect to an API but simply read the config from this URI
                if stringIsUri(importState.MgmDetails.Hostname):
                    importState.setImportFileName(importState.MgmDetails.Hostname)
            else:
                importState.setImportFileName(in_file)
            if importState.ImportFileName is not None:
                configFromFile = fwo_file_import.readJsonConfigFromFile(importState)
                ### just parsing the config, note: we need to run get_config_from_api here to do this
                if isinstance(configFromFile, FwConfigManagerList):
                    config_changed_since_last_import, configNormalized = get_config_from_api(importState, configFromFile.Config)
                else: 
                    config_changed_since_last_import, configNormalized = get_config_from_api(importState, configFromFile)
            else:
                ### geting config from firewall manager ######################
                config_changed_since_last_import, configNormalized = get_config_from_api(importState, {})

        time_get_config = int(time.time()) - importState.StartTime
        logger.debug("import_management - getting config total duration " + str(int(time.time()) - importState.StartTime) + "s")

        if config_changed_since_last_import or importState.ForceImport:
            try: # now we import the config via API chunk by chunk:
                # for config_chunk in split_config(importState, configNormalized):
                for managerSet in configNormalized.ManagerSet:
                    for config in managerSet.Configs:
                        configChunk = config.toJsonLegacy(withAction=False)
                        importState.setChangeCounter (importState.ErrorCount + fwo_api.import_json_config(importState, configChunk)) # IMPORT TO FWO API HERE
                        fwo_api.update_hit_counter(importState, config)
                # currently assuming only one chunk
                # initiateImportStart(importState)
            except:
                logger.error("import_management - unspecified error while importing config via FWO API: " + str(traceback.format_exc()))
                raise
            logger.debug(f"writing config to API and stored procedure import duration: {str(int(time.time())-time_get_config-importState.StartTime)}s")

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
                change_count = fwo_api.count_rule_changes_per_import(importState.FwoConfig.FwoApiUri, importState.Jwt, importState.ImportId)
                importState.setChangeCounter(change_count)
            except:
                logger.error("import_management - unspecified error while getting change count: " + str(traceback.format_exc()))
                raise
        else: # if no changes were found, we skip everything else without errors
            pass

        if not configNormalized == {}:
            writeNormalizedConfigToFile(importState, configNormalized)
        importState.setErrorCounter(fwo_api.complete_import(importState))
        
    return importState.ErrorCount


def initiateImportStart(importState):
    # now start import by adding a dummy config with flag set
    emptyDummyConfig = FwConfigNormalized.fromJson( {
        'action': ConfigAction.INSERT,
        'network_objects': [],
        'service_objects': [],
        'users': [],
        'zone_objects': [], 
        'policies': [],
        'routing': [],
        'interfaces': []
    })

    importState.setChangeCounter (
        importState.ErrorCount + fwo_api.import_json_config(importState, 
                                    emptyDummyConfig.toJsonLegacy(withAction=False), 
                                    startImport=True))


def initializeImport(mgmId, debugLevel=0, suppressCertWarnings=False, sslVerification=False, force=False):

    def check_input_parameters(mgmId):
        if mgmId is None:
            raise BaseException("parameter mgm_id is mandatory")

    logger = getFwoLogger()
    check_input_parameters(mgmId)


    fwoConfig = FworchConfig.fromJson(readConfig(fwo_config_filename))
    # read importer password from file
    with open(importer_pwd_file, 'r') as file:
        importerPwd = file.read().replace('\n', '')
    fwoConfig.setImporterPwd(importerPwd)

    # authenticate to get JWT
    try:
        jwt = fwo_api.login(importer_user_name, fwoConfig.ImporterPassword, fwoConfig.FwoUserMgmtApiUri)
    except FwoApiLoginFailed as e:
        logger.error(e.message)
        return e.message
    except:
        return "unspecified error during FWO API login"

    # set global https connection values
    fwo_globals.setGlobalValues (suppress_cert_warnings_in=suppressCertWarnings, verify_certs_in=sslVerification, debug_level_in=debugLevel)
    if fwo_globals.verify_certs is None:    # not defined via parameter
        fwo_globals.verify_certs = fwo_api.get_config_value(fwoConfig.FwoApiUri, jwt, key='importCheckCertificates')=='True'
    if fwo_globals.suppress_cert_warnings is None:    # not defined via parameter
        fwo_globals.suppress_cert_warnings = fwo_api.get_config_value(fwoConfig.FwoApiUri, jwt, key='importSuppressCertificateWarnings')=='True'
    if fwo_globals.suppress_cert_warnings: # not defined via parameter
        requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only    

    try: # get mgm_details (fw-type, port, ip, user credentials):
        mgmDetails = fwo_api.get_mgm_details(fwoConfig.FwoApiUri, jwt, {"mgmId": int(mgmId)}, debugLevel) 
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


def get_config_from_api(importState, configNative, import_tmp_path=import_tmp_path, limit=150) -> FwConfigManagerList:
    logger = getFwoLogger()

    try: # pick product-specific importer:
        pkg_name = importState.MgmDetails.DeviceTypeName.lower().replace(' ', '') + importState.MgmDetails.DeviceTypeVersion
        fw_module = importlib.import_module("." + fw_module_name, pkg_name)
    except:
        logger.exception("import_management - error while loading product specific fwcommon module", traceback.format_exc())        
        raise
    
    try: # get the config data from the firewall manager's API: 
        # check for changes from product-specific FW API
        config_changed_since_last_import = importState.ImportFileName != None or \
            fw_module.has_config_changed(configNative, importState.FullMgmDetails, force=importState.ForceImport)
        if config_changed_since_last_import:
            logger.debug ( "has_config_changed: changes found or forced mode -> go ahead with getting config, Force = " + str(importState.ForceImport))
        else:
            logger.debug ( "has_config_changed: no new changes found")

        if config_changed_since_last_import or importState.ForceImport:
            # get config from product-specific FW API
            _, configNormalized = fw_module.get_config(configNative,  importState)
        else:
            configNormalized = {}
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
        fwo_api.delete_import(importState.FwoConfig.FwoApiUri, importState.Jwt, importState.ImportId) # deleting trace of not even begun import
        importState.ErrorCount = fwo_api.complete_import(importState)
        raise ImportRecursionLimitReached(e.message)
    except:
        importState.ErrorString += "  import_management - unspecified error while getting config: " + str(traceback.format_exc())
        logger.error(importState.ErrorString)
        importState.ErrorCount += 1
        importState.ErrorCount = fwo_api.complete_import(importState)
        raise

    writeNativeConfigToFile(importState, configNative)

    logger.debug("import_management: get_config completed (including normalization), duration: " + str(int(time.time()) - importState.StartTime) + "s") 

    return config_changed_since_last_import, configNormalized


def writeNormalizedConfigToFile(importState, configNormalized):
    logger = getFwoLogger()
    debug_start_time = int(time.time())
    try:
        if fwo_globals.debug_level>5:
            normalized_config_filename = f"{import_tmp_path}/mgm_id_{str(importState.MgmDetails.Id)}_config_normalized.json"
            with open(normalized_config_filename, "w") as json_data:
                json_data.write(configNormalized.toJsonStringLegacy())
    except:
        logger.error(f"import_management - unspecified error while dumping normalized config to json file: {str(traceback.format_exc())}")
        raise

    time_write_debug_json = int(time.time()) - debug_start_time
    logger.debug(f"import_management - writing native config json files duration {str(time_write_debug_json)}s")


def writeNativeConfigToFile(importState, configNative):
    logger = getFwoLogger()
    debug_start_time = int(time.time())
    try:
        if fwo_globals.debug_level>6:
            full_native_config_filename = f"{import_tmp_path}/mgm_id_{str(importState.MgmDetails.Id)}_config_native.json"
            with open(full_native_config_filename, "w") as json_data:
                json_data.write(json.dumps(configNative, indent=2))
    except:
        logger.error(f"import_management - unspecified error while dumping config to json file: {str(traceback.format_exc())}")
        raise

    time_write_debug_json = int(time.time()) - debug_start_time
    logger.debug(f"import_management - writing debug config json files duration {str(time_write_debug_json)}s")

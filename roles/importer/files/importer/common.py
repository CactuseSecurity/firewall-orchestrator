import importlib
import traceback
import sys, time
import json

import importlib
from socket import gethostname
from typing import List
import importlib.util
from fwo_config import readConfig
from fwo_const import fwo_config_filename, importer_user_name, importer_base_dir
from pathlib import Path
if importer_base_dir not in sys.path:
    sys.path.append(importer_base_dir) # adding absolute path here once
import fwo_api
from fwo_log import getFwoLogger
from fwo_const import fw_module_name, import_tmp_path
import fwo_globals
import fwo_exceptions
from fwo_base import stringIsUri, ConfigAction, ConfFormat
import fwo_file_import
from model_controllers.management_details_controller import ManagementDetailsController
from model_controllers.fworch_config_controller import FworchConfigController
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfig, FwConfigNormalized
from models.fwconfigmanagerlist import FwConfigManagerList, FwConfigManager
from models.gateway import Gateway
from fwconfig_base import calcManagerUidHash
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.gateway_controller import GatewayController
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.check_consistency import FwConfigImportCheckConsistency
from model_controllers.rollback import FwConfigImportRollback
import fwo_signalling

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
        in_file=None, version=8) -> int:

    fwo_signalling.registerSignallingHandlers()
    logger = getFwoLogger(debug_level=debug_level_in)
    config_changed_since_last_import = True
    time_get_config = 0
    verifyCerts = (ssl_verification is not None)
    result = 1  # Default result in case of an error

    try:
        importState = ImportStateController.initializeImport(mgmId, debugLevel=debug_level_in, 
                                                force=force, version=version, 
                                                isClearingImport=clearManagementData, isFullImport=False, sslVerification=verifyCerts)
        if not clearManagementData and importState.DataRetentionDays<importState.DaysSinceLastFullImport:
            # run clear import; this makes sure the following import is a full one
            import_management(mgmId=mgmId, ssl_verification=ssl_verification, debug_level_in=debug_level_in, 
                limit=limit, force=True, clearManagementData=True, suppress_cert_warnings_in=suppress_cert_warnings_in,
                in_file=in_file, version=version)
            importState.IsFullImport = True # the now following import is a full one

        if importState.MgmDetails.ImportDisabled and not importState.ForceImport:
            logger.info(f"import_management - import disabled for mgm  {str(mgmId)} - skipping")
            result = 0
        else:
            Path(import_tmp_path).mkdir(parents=True, exist_ok=True)  # make sure tmp path exists
            gateways = GatewayController.buildGatewayList(importState.MgmDetails)

            # only run if this is the correct import module
            if importState.MgmDetails.ImporterHostname != gethostname() and not importState.ForceImport:
                logger.info("import_management - this host (" + gethostname() + ") is not responsible for importing management " + str(mgmId))
                result = 0
            else:
                fwo_api.setImportLock(importState)
                logger.info("starting import of management " + importState.MgmDetails.Name + '(' + str(mgmId) + "), import_id=" + str(importState.ImportId))

                if clearManagementData:
                    configNormalized = clearManagement(importState)
                if in_file is not None or stringIsUri(importState.MgmDetails.Hostname):                          ### geting config from file ######################
                    config_changed_since_last_import, configNormalized = \
                        importFromFile(importState, in_file, gateways)
                else:
                    config_changed_since_last_import, configNormalized = get_config_from_api(importState, {})    ### getting config from firewall manager API ######
                
                time_get_config = int(time.time()) - importState.StartTime
                logger.debug("import_management - getting config total duration " + str(int(time.time()) - importState.StartTime) + "s")

                if config_changed_since_last_import or importState.ForceImport:
                    # for config_chunk in split_config(importState, configNormalized):
                    
                    try:
                        for managerSet in configNormalized.ManagerSet:
                            for config in managerSet.Configs:
                                try:
                                    configImporter = FwConfigImport(importState, config)
                                    configChecker = FwConfigImportCheckConsistency(configImporter)
                                    if len(configChecker.checkConfigConsistency())==0:
                                        configImporter.importConfig()
                                        if importState.Stats.ErrorCount>0:
                                            raise fwo_exceptions.FwoImporterError("Import failed due to errors.")
                                        else:
                                            configImporter.storeLatestConfig()
                                except Exception:
                                    importState.addError(str(traceback.format_exc()))
                                    raise
                                fwo_api.update_hit_counter(importState, config)

                    finally:
                         # Writes full config to file (for debugging). In case of exception writes the file after error handling here, but before roleback.
                        configNormalized.storeFullNormalizedConfigToFile(importState)

            if not clearManagementData and importState.DataRetentionDays<importState.DaysSinceLastFullImport:
                configImporter.deleteOldImports() # delete all imports of the current management before the last but one full import

        # Set the result based on the error count
        if hasattr(importState, 'Stats') and hasattr(importState.Stats, 'ErrorCount'):
            result = importState.Stats.ErrorCount

    except (fwo_exceptions.FwLoginFailed) as e:
        fwo_api.delete_import(importState) # delete whole import
        importState.addError("Login to FW manager failed")
    except (fwo_exceptions.ImportRecursionLimitReached) as e:
        fwo_api.delete_import(importState) # delete whole import
        importState.addError("ImportRecursionLimitReached - aborting import")
    except (KeyboardInterrupt, fwo_exceptions.ImportInterruption) as e:
        rollBackExceptionHandler(importState, configImporter=configImporter, exc=e, errorText="shutdown requested")
        raise
    except (fwo_exceptions.FwoApiWriteError, fwo_exceptions.FwoImporterError) as e:
        importState.addError("FwoApiWriteError or FwoImporterError - aborting import")
        rollBackExceptionHandler(importState, configImporter=configImporter, exc=e, errorText="")
        raise
    except Exception as e:
        if 'importState' in locals() and importState is not None:
            importState.addError("Unexpected exception in import process - aborting " + traceback.format_exc())
            if 'configImporter' in locals() and configImporter is not None:
                rollBackExceptionHandler(importState, configImporter=configImporter, exc=e)
        raise
    finally:
        try:
            fwo_api.complete_import(importState)
        except Exception as e:
            logger.error(f"Error during import completion: {str(e)}")

    return result

def clearManagement(importState: ImportStateController) -> FwConfigNormalized:
    logger = getFwoLogger(debug_level=importState.DebugLevel)
    logger.info('this import run will reset the configuration of this management to "empty"')
    configNormalized = FwConfigManagerListController()
    # Reset management
    configNormalized.addManager(
        manager=FwConfigManager(
            ManagerUid=calcManagerUidHash(importState.MgmDetails),
            ManagerName=importState.MgmDetails.Name,
            IsSuperManager=importState.MgmDetails.IsSuperManager,
            SubManagerIds=importState.MgmDetails.SubManagerIds,
            Configs=[]
        ))
    if len(importState.MgmDetails.SubManagerIds)>0:
        # Read config
        fwoConfig = FworchConfigController.fromJson(readConfig(fwo_config_filename))
        fwo_api_base_url = fwoConfig['fwo_api_base_url']
        # Authenticate to get JWT
        try:
            jwt = fwo_api.login(importer_user_name, fwoConfig.ImporterPassword, fwoConfig.FwoUserMgmtApiUri)
        except Exception as e:
            logger.error(str(e))
            raise             
        # Reset submanagement
        for subManagerId in importState.MgmDetails.SubManagerIds:
            # Fetch sub management details
            mgm_details_raw = fwo_api.get_mgm_details(fwo_api_base_url, jwt, {"mgmId": subManagerId})
            mgm_details = ManagementDetailsController.fromJson(mgm_details_raw)
            configNormalized.addManager(
                manager=FwConfigManager(
                    ManagerUid=calcManagerUidHash(mgm_details_raw),
                    ManagerName=mgm_details.Name,
                    IsSuperManager=mgm_details.IsSuperManager,
                    SubManagerIds=mgm_details.SubManagerIds,
                    Configs=[]
                )
            )
    # Reset objects
    for management in configNormalized.ManagerSet:
        management.Configs.append(
            FwConfigNormalized(
                action=ConfigAction.INSERT, 
                network_objects=[], 
                service_objects=[], 
                users=[], 
                zone_objects=[], 
                rulebases=[],
                gateways=[]
            )
        )
    importState.IsClearingImport = True # the now following import is a full one
    
    return configNormalized


def rollBackExceptionHandler(importState, configImporter=None, exc=None, errorText=""):
    try:
        logger = getFwoLogger()
        if fwo_globals.shutdown_requested:
            logger.warning("Shutdown requested.")
        elif errorText!="":
            logger.error(f"Exception: errorText")
        else:
            if exc is not None:
                logger.error(f"Exception: {type(exc).__name__}")
            else:
                logger.error(f"Exception: no exception provided")
        if 'configImporter' in locals() and configImporter is not None:
            FwConfigImportRollback(configImporter).rollbackCurrentImport()
        else:
            logger.info("No configImporter found, skipping rollback.")
        fwo_api.delete_import(importState) # delete whole import
    except Exception as rollbackError:
        logger.error(f"Error during rollback: {type(rollbackError).__name__} - {rollbackError}")

def importFromFile(importState: ImportStateController, fileName: str = "", gateways: List[Gateway] = []):

    logger = getFwoLogger(debug_level=importState.DebugLevel)
    logger.debug("import_management - not getting config from API but from file: " + fileName)

    config_changed_since_last_import = True
    
    # set file name in importState
    if fileName == '' or fileName is None: 
        # if the host name is an URI, do not connect to an API but simply read the config from this URI
        if stringIsUri(importState.MgmDetails.Hostname):
            importState.setImportFileName(importState.MgmDetails.Hostname)
    else:
        importState.setImportFileName(fileName)

    configFromFile = fwo_file_import.readJsonConfigFromFile(importState)

    if configFromFile.IsLegacy():
        if isinstance(configFromFile, FwConfig):
            if configFromFile.ConfigFormat == 'NORMALIZED_LEGACY':
                configNormalized = FwConfigManagerList(ConfigFormat=configFromFile.ConfigFormat)
                configNormalized.addManager(manager=FwConfigManager(calcManagerUidHash(importState.MgmDetails), importState.MgmDetails.Name))
                configNormalized.ManagerSet[0].Configs.append(FwConfigNormalized(ConfigAction.INSERT, 
                                                                                configFromFile.Config['network_objects'],
                                                                                configFromFile.Config['service_objects'],
                                                                                configFromFile.Config['user_objects'],
                                                                                configFromFile.Config['zone_objects'],
                                                                                configFromFile.Config['rules'],
                                                                                gateways
                                                                                ))
            elif configFromFile.ConfigFormat == 'NORMALIZED':
                # ideally just import from json
                configNormalized = FwConfigManagerList.fromJson(configFromFile)
        else: ### just parsing the native config, note: we need to run get_config_from_api here to do this
            if isinstance(configFromFile, FwConfigManagerList):
                for mgr in configFromFile.ManagerSet:
                    for conf in mgr.Configs:
                        # need to decide how to deal with the multiple results of this loop here!
                        config_changed_since_last_import, configNormalized = get_config_from_api(importState, conf)
            else: 
                config_changed_since_last_import, configNormalized = get_config_from_api(importState, configFromFile)
    else:
        configNormalized = configFromFile

    return config_changed_since_last_import, configNormalized


def get_config_from_api(importState: ImportStateController, configNative, import_tmp_path=import_tmp_path, limit=150) -> FwConfigManagerList:
    logger = getFwoLogger(debug_level=importState.DebugLevel)

    try: # pick product-specific importer:
        pkg_name = importState.MgmDetails.DeviceTypeName.lower().replace(' ', '') + \
            importState.MgmDetails.DeviceTypeVersion.replace(' ', '').replace('MDS', '')
        if not f"{importer_base_dir}/{pkg_name}" in sys.path:
            sys.path.append(f"{importer_base_dir}/{pkg_name}")
        fw_module = importlib.import_module("." + fw_module_name, pkg_name)
    except Exception:
        logger.exception("import_management - error while loading product specific fwcommon module", traceback.format_exc())        
        raise
    
    # check for changes from product-specific FW API
    config_changed_since_last_import = importState.ImportFileName != None or \
        fw_module.has_config_changed(configNative, importState, force=importState.ForceImport)
    if config_changed_since_last_import:
        logger.info ( "has_config_changed: changes found or forced mode -> go ahead with getting config, Force = " + str(importState.ForceImport))
    else:
        logger.info ( "has_config_changed: no new changes found")

    if config_changed_since_last_import or importState.ForceImport:
        # get config from product-specific FW API
        _, configNormalized = fw_module.get_config(configNative, importState)
    else:
        # returning empty config
        emptyConfigDict = {
                                'action': ConfigAction.INSERT,
                                'network_objects': {},
                                'service_objects': {},
                                'users': {},
                                'zone_objects': {},
                                'rules': [],
                                'gateways': [],
                                'ConfigFormat': ConfFormat.NORMALIZED
                            }
        configNormalized = FwConfigNormalized(**emptyConfigDict)

    writeNativeConfigToFile(importState, configNative)

    logger.debug("import_management: get_config completed (including normalization), duration: " + str(int(time.time()) - importState.StartTime) + "s") 

    return config_changed_since_last_import, configNormalized


def writeNativeConfigToFile(importState, configNative):
    if importState.DebugLevel>6:
        logger = getFwoLogger(debug_level=importState.DebugLevel)
        debug_start_time = int(time.time())
        try:
                full_native_config_filename = f"{import_tmp_path}/mgm_id_{str(importState.MgmDetails.Id)}_config_native.json"
                with open(full_native_config_filename, "w") as json_data:
                    json_data.write(json.dumps(configNative, indent=2))
        except Exception:
            logger.error(f"import_management - unspecified error while dumping config to json file: {str(traceback.format_exc())}")
            raise

        time_write_debug_json = int(time.time()) - debug_start_time
        logger.debug(f"import_management - writing debug config json files duration {str(time_write_debug_json)}s")

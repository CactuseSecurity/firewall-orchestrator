import traceback
import sys, time
import json

import importlib
from socket import gethostname
from typing import List

from fwo_const import importer_base_dir
from pathlib import Path
sys.path.append(importer_base_dir) # adding absolute path here once
import fwo_api
from fwo_log import getFwoLogger
from fwo_const import fw_module_name
from fwo_const import import_tmp_path
import fwo_globals
from fwo_exceptions import FwLoginFailed, ImportRecursionLimitReached, ImportInterruption, FwoImporterError
from fwo_base import stringIsUri, ConfigAction, ConfFormat
import fwo_file_import
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfig, FwConfigNormalized
from models.fwconfigmanagerlist import FwConfigManagerList, FwConfigManager
from models.gateway import Gateway
from fwconfig_base import calcManagerUidHash
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.gateway_controller import GatewayController
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.check_consistency import FwConfigImportCheckConsistency
from model_controllers.rollback import FwConfigImportRollback
from model_controllers.import_state_controller import ImportStateController
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
        in_file=None, version=8):

    fwo_signalling.registerSignallingHandlers()
    logger = getFwoLogger(debug_level=debug_level_in)
    config_changed_since_last_import = True
    time_get_config = 0
    verifyCerts = (ssl_verification is not None)

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
        else:
            Path(import_tmp_path).mkdir(parents=True, exist_ok=True)  # make sure tmp path exists
            gateways = GatewayController.buildGatewayList(importState.FullMgmDetails)

            # only run if this is the correct import module
            if importState.MgmDetails.ImporterHostname != gethostname() and not importState.ForceImport:
                logger.info("import_management - this host (" + gethostname() + ") is not responsible for importing management " + str(mgmId))
                return 0

            fwo_api.setImportLock(importState)
            logger.info("starting import of management " + importState.MgmDetails.Name + '(' + str(mgmId) + "), import_id=" + str(importState.ImportId))
            configNormalized = {}

            if clearManagementData:
                logger.info('this import run will reset the configuration of this management to "empty"')
                configNormalized = FwConfigManagerListController()
                configNormalized.addManager(
                    manager=FwConfigManager(
                        ManagerUid=calcManagerUidHash(importState.FullMgmDetails),
                        ManagerName=importState.MgmDetails.Name,
                        IsGlobal=importState.MgmDetails.IsSuperManager,
                        DependantManagerUids=[],
                        Configs=[]
                    ))
                configNormalized.ManagerSet[0].Configs.append(
                    FwConfigNormalized(
                        action=ConfigAction.INSERT, 
                        network_objects=[], 
                        service_objects=[], 
                        users=[], 
                        zone_objects=[], 
                        rulebases=[],
                        gateways=[]
                ))
                
                importState.IsClearingImport = True # the now following import is a full one
            else:
                if in_file is not None or stringIsUri(importState.MgmDetails.Hostname):
                    ### geting config from file ######################
                    config_changed_since_last_import, configNormalized = \
                        importFromFile(importState, in_file, gateways)
                else:
                    ### getting config from firewall manager API ######
                    config_changed_since_last_import, configNormalized = get_config_from_api(importState, {})

                    # also import sub managers if they exist
                    for subManagerId in importState.MgmDetails.SubManager:
                        subMgrImportState = ImportStateController.initializeImport(subManagerId, debugLevel=debug_level_in, 
                                                force=force, version=version, 
                                                isClearingImport=clearManagementData, isFullImport=False)
                        config_changed_since_last_import, configNormalizedSub = get_config_from_api(subMgrImportState, {})
                        configNormalized.mergeConfigs(configNormalizedSub)
                        # TODO: destroy configNormalizedSub?

                time_get_config = int(time.time()) - importState.StartTime
                logger.debug("import_management - getting config total duration " + str(int(time.time()) - importState.StartTime) + "s")

            if config_changed_since_last_import or importState.ForceImport:
                try: # now we import the config via API chunk by chunk:
                    # for config_chunk in split_config(importState, configNormalized):
                    configNormalized.storeFullNormalizedConfigToFile(importState) # write full config to file (for debugging)
                    for managerSet in configNormalized.ManagerSet:
                        for config in managerSet.Configs:
                            try:
                                configImporter = FwConfigImport(importState, config)
                                try:
                                    configChecker = FwConfigImportCheckConsistency(configImporter)
                                    if len(configChecker.checkConfigConsistency())==0:
                                        try:
                                            configImporter.importConfig()
                                        except Exception:
                                            importState.addError(str(traceback.format_exc()), log=True)
                                            raise

                                        if importState.Stats.ErrorCount>0:
                                            raise FwoImporterError("Import failed due to errors.")
                                        else:
                                            configImporter.storeLatestConfig()
                                except Exception:
                                    raise # ImportError("Import failed due to errors.")
                            except Exception:
                                importState.addError(str(traceback.format_exc()))
                                raise
                            fwo_api.update_hit_counter(importState, config)
                except Exception:
                    raise
                logger.debug(f"full import duration: {str(int(time.time())-time_get_config-importState.StartTime)}s")
                # TODO: if no objects found at all: at least show a warning

            fwo_api.complete_import(importState)    # default (successful) completion of import

        if not clearManagementData and importState.DataRetentionDays<importState.DaysSinceLastFullImport:
            # delete all imports of the current management before the last but one full import
            configImporter.deleteOldImports()

    except (KeyboardInterrupt, ImportInterruption) as e:
        if fwo_globals.shutdown_requested:
            logger.warning("Shutdown requested.")
        else:
            logger.error(e)
        if 'configImporter' in locals():
            FwConfigImportRollback(configImporter).rollbackCurrentImport()
        else:
            logger.info("No configImporter found, skipping rollback.")
        fwo_api.delete_import(importState) # delete whole import
        sys.exit(1)
    except (FwoImporterError) as e:
        logger.error(f"import error encountered: {importState.getErrorString()}")
        if 'configImporter' in locals():
            FwConfigImportRollback(configImporter).rollbackCurrentImport()
        else:
            logger.info("No configImporter found, skipping rollback.")
        fwo_api.delete_import(importState) # delete whole import
        sys.exit(1)
    except Exception as e:
        logger.error(f"Unexpected error in import_management: {e}")
        raise
    finally:
        fwo_api.complete_import(importState)
        # sys.exit(0)
        return importState.Stats.ErrorCount


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
                configNormalized.addManager(manager=FwConfigManager(calcManagerUidHash(importState.FullMgmDetails), importState.MgmDetails.Name))
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
        pkg_name = importState.MgmDetails.DeviceTypeName.lower().replace(' ', '') + importState.MgmDetails.DeviceTypeVersion
        fw_module = importlib.import_module("." + fw_module_name, pkg_name)
    except Exception:
        logger.exception("import_management - error while loading product specific fwcommon module", traceback.format_exc())        
        raise
    
    try: # get the config data from the firewall manager's API: 
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

    except ImportInterruption as e:
        logger.error(f"Import interrupted: {e}")
        # Perform rollback or cleanup here
        raise
    except (FwLoginFailed) as e:
        importState.appendErrorString(f"login failed: mgm_id={str(importState.MgmDetails.Id)}, mgm_name={importState.MgmDetails.Name}, {e.message}")
        importState.increaseErrorCounter()
        logger.error(importState.getErrorString())
        fwo_api.delete_import(importState) # deleting trace of not even begun import
        fwo_api.complete_import(importState)
        raise FwLoginFailed(e.message)
    except ImportRecursionLimitReached as e:
        importState.appendErrorString(f"recursion limit reached: mgm_id={str(importState.MgmDetails.Id)}, mgm_name={importState.MgmDetails.Name},{e.message}")
        importState.increaseErrorCounter()
        logger.error(importState.getErrorString())
        fwo_api.delete_import(importState.FwoConfig.FwoApiUri, importState.Jwt, importState.ImportId) # deleting trace of not even begun import
        fwo_api.complete_import(importState)
        raise ImportRecursionLimitReached(e.message)
    except Exception:
        importState.appendErrorString("import_management - unspecified error while getting config: " + str(traceback.format_exc()))
        logger.error(importState.getErrorString())
        importState.increaseErrorCounterByOne()
        fwo_api.complete_import(importState)
        raise

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

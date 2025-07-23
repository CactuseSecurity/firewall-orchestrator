import importlib
import traceback
import sys, time
import json
from socket import gethostname
from typing import List
from fwo_config import readConfig
from fwo_const import fwo_config_filename, importer_user_name, importer_base_dir
from pathlib import Path
from services.service_provider import ServiceProvider
from services.enums import Services
if importer_base_dir not in sys.path:
    sys.path.append(importer_base_dir) # adding absolute path here once
import fwo_api
from fwo_log import getFwoLogger
from fwo_const import fw_module_name, import_tmp_path
import fwo_globals
from fwo_exceptions import FwoImporterError, FwLoginFailed, ImportRecursionLimitReached, FwoApiWriteError, FwoImporterErrorInconsistencies, ImportInterruption
from fwo_base import stringIsUri, ConfigAction, ConfFormat
import fwo_file_import
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
from services.service_provider import ServiceProvider
from services.global_state import GlobalState
from services.enums import Services, Lifetime
from services.uid2id_mapper import Uid2IdMapper
from services.group_flats_mapper import GroupFlatsMapper
from services.enums import Services, Lifetime


"""  
    import_management: import a single management (if no import for it is running)
    if mgmId is that of a super management, it will import all submanagements as well
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
    verify_certs = (ssl_verification is not None)

    service_provider = register_services()
    global_state = service_provider.get_service(Services.GLOBAL_STATE)
    importState = ImportStateController.initializeImport(mgmId, debugLevel=debug_level_in, 
                                            force=force, version=version, 
                                            isClearingImport=clearManagementData, isFullImport=False, sslVerification=verify_certs)
    global_state.import_state = importState

    config_importer = FwConfigImport()

    try:
        _import_management(service_provider=service_provider, importState=importState, config_importer=config_importer,
                           mgmId=mgmId, ssl_verification=ssl_verification, debug_level_in=debug_level_in,
            limit=limit, clearManagementData=clearManagementData,
            suppress_cert_warnings_in=suppress_cert_warnings_in, in_file=in_file)

    except (FwLoginFailed) as e:
        fwo_api.delete_import(importState) # delete whole import
        importState.addError("Login to FW manager failed")
        rollBackExceptionHandler(importState, configImporter=config_importer, exc=e, errorText="")
    except (ImportRecursionLimitReached) as e:
        fwo_api.delete_import(importState) # delete whole import
        importState.addError("ImportRecursionLimitReached - aborting import")
    except (KeyboardInterrupt, ImportInterruption) as e:
        rollBackExceptionHandler(importState, configImporter=config_importer, exc=e, errorText="shutdown requested")
        raise
    except (FwoApiWriteError, FwoImporterError) as e:
        importState.addError("FwoApiWriteError or FwoImporterError - aborting import")
        rollBackExceptionHandler(importState, configImporter=config_importer, exc=e, errorText="")
    except FwoImporterErrorInconsistencies:
        fwo_api.delete_import(importState) # delete whole import
    except ValueError:
        importState.addError("ValueError - aborting import")
        raise
    except Exception as e:
        handle_unexpected_exception(importState=importState, config_importer=config_importer)
    finally:
        try:
            fwo_api.complete_import(importState)
            ServiceProvider().dispose_service(Services.UID2ID_MAPPER, importState.ImportId)
        except Exception as e:
            logger.error(f"Error during import completion: {str(e)}")

    if hasattr(importState, 'Stats') and hasattr(importState.Stats, 'ErrorCount'):
        return importState.Stats.ErrorCount
    else:
        return 1


def _import_management(service_provider=None, importState=None, config_importer=None, mgmId=None, ssl_verification=None, debug_level_in=0,
        limit=150, clearManagementData=False, suppress_cert_warnings_in=None, in_file=None) -> int:

    logger = getFwoLogger(debug_level=debug_level_in)
    config_changed_since_last_import = True

    if importState.DebugLevel > 8:
        logger.debug(f"import_management - ssl_verification: {ssl_verification}")
        logger.debug(f"import_management - suppress_cert_warnings_in: {suppress_cert_warnings_in}")
        logger.debug(f"import_management - limit: {limit}")

    if importState.MgmDetails.ImportDisabled and not importState.ForceImport:
        logger.info(f"import_management - import disabled for mgm  {str(mgmId)} - skipping")
        return 0
    
    if importState.MgmDetails.ImporterHostname != gethostname() and not importState.ForceImport:
        logger.info(f"import_management - this host ( {gethostname()}) is not responsible for importing management  {str(mgmId)}")
        return 0
    
    Path(import_tmp_path).mkdir(parents=True, exist_ok=True)  # make sure tmp path exists
    gateways = GatewayController.buildGatewayList(importState.MgmDetails)
    logger.info(f"starting import of management {importState.MgmDetails.Name} ({str(mgmId)}), import_id= {str(importState.ImportId)}")

    fwo_api.setImportLock(importState)
    if clearManagementData:
        config_normalized = config_importer.clear_management(importState)
    else:
        # get config
        config_changed_since_last_import, config_normalized = get_config_top_level(importState, in_file, gateways)

        # write normalized config to file
        config_normalized.storeFullNormalizedConfigToFile(importState)
        logger.debug("import_management - getting config total duration " + str(int(time.time()) - importState.StartTime) + "s")

    # check config consistency and import it
    if config_changed_since_last_import or importState.ForceImport:
        FwConfigImportCheckConsistency(importState, config_normalized).checkConfigConsistency(config_normalized)
        config_importer.import_management_set(importState, service_provider, config_normalized.ManagerSet)
        fwo_api.update_hit_counter(importState, config_normalized)

    # delete data that has passed the retention time
    # TODO: replace by deletion of old data with removed date > retention?
    if not clearManagementData and importState.DataRetentionDays<importState.DaysSinceLastFullImport:
        config_importer.deleteOldImports() # delete all imports of the current management before the last but one full import



def handle_unexpected_exception(importState=None, config_importer=None):
    if 'importState' in locals() and importState is not None:
        importState.addError("Unexpected exception in import process - aborting " + traceback.format_exc())
        if 'configImporter' in locals() and config_importer is not None:
            rollBackExceptionHandler(importState, configImporter=config_importer, exc=e)


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
            FwConfigImportRollback().rollbackCurrentImport()
        else:
            logger.info("No configImporter found, skipping rollback.")
        fwo_api.delete_import(importState) # delete whole import
    except Exception as rollbackError:
        logger.error(f"Error during rollback: {type(rollbackError).__name__} - {rollbackError}")


def register_services():
    service_provider = ServiceProvider()
    service_provider.register(Services.GLOBAL_STATE, lambda: GlobalState(), Lifetime.SINGLETON)
    service_provider.register(Services.GROUP_FLATS_MAPPER, lambda: GroupFlatsMapper(), Lifetime.IMPORT)
    service_provider.register(Services.PREV_GROUP_FLATS_MAPPER, lambda: GroupFlatsMapper(), Lifetime.IMPORT)
    service_provider.register(Services.UID2ID_MAPPER, lambda: Uid2IdMapper(), Lifetime.IMPORT)
    return service_provider


def get_config_top_level(importState: ImportStateController, in_file: str = None, gateways: List[Gateway] = []) -> tuple[bool, FwConfigManagerList]:
    if in_file is not None or stringIsUri(importState.MgmDetails.Hostname):
        ### geting config from file ######################
        if in_file is None:
            in_file = importState.MgmDetails.Hostname
        return import_from_file(importState, in_file, gateways)
    else:
        ### getting config from firewall manager API ######
        return get_config_from_api(importState, {})    


def import_from_file(importState: ImportStateController, fileName: str = "", gateways: List[Gateway] = []) -> tuple[bool, FwConfigManagerList]:

    logger = getFwoLogger(debug_level=importState.DebugLevel)
    logger.debug(f"import_management - not getting config from API but from file: {fileName}")

    config_changed_since_last_import = True
    
    set_filename(importState, file_name=fileName)

    configFromFile = fwo_file_import.readJsonConfigFromFile(importState)

    if not configFromFile.IsLegacy():
        return config_changed_since_last_import, configFromFile

    if isinstance(configFromFile, FwConfig):
        if configFromFile.ConfigFormat == 'NORMALIZED_LEGACY':
            normalized_config_list = FwConfigManagerList(ConfigFormat=configFromFile.ConfigFormat)
            normalized_config_list.addManager(manager=FwConfigManager(calcManagerUidHash(importState.MgmDetails), importState.MgmDetails.Name))
            normalized_config_list.ManagerSet[0].Configs.append(FwConfigNormalized(ConfigAction.INSERT, 
                                                                            configFromFile.Config['network_objects'],
                                                                            configFromFile.Config['service_objects'],
                                                                            configFromFile.Config['user_objects'],
                                                                            configFromFile.Config['zone_objects'],
                                                                            configFromFile.Config['rules'],
                                                                            gateways
                                                                            ))
        elif configFromFile.ConfigFormat == 'NORMALIZED':
            normalized_config_list = FwConfigManagerList.fromJson(configFromFile)  # ideally just import from json

    else: ### just parsing the native config, note: we need to run get_config_from_api here to do this
        if not isinstance(configFromFile, FwConfigManagerList):
            return get_config_from_api(importState, configFromFile)

        for mgr in configFromFile.ManagerSet:
            for conf in mgr.Configs:
                # need to decide how to deal with the multiple results of this loop here!
                config_changed_since_last_import, normalized_config_list = get_config_from_api(importState, conf)

    return config_changed_since_last_import, normalized_config_list  


def get_config_from_api(importState: ImportStateController, configNative) -> tuple[bool, FwConfigManagerList]:
    logger = getFwoLogger(debug_level=importState.DebugLevel)

    try: # pick product-specific importer:
        pkg_name = importState.MgmDetails.DeviceTypeName.lower().replace(' ', '') + \
            importState.MgmDetails.DeviceTypeVersion.replace(' ', '').replace('MDS', '')
        if f"{importer_base_dir}/{pkg_name}" not in sys.path:
            sys.path.append(f"{importer_base_dir}/{pkg_name}")
        fw_module = importlib.import_module("." + fw_module_name, pkg_name)
    except Exception:
        logger.exception("import_management - error while loading product specific fwcommon module", traceback.format_exc())        
        raise
    
    # check for changes from product-specific FW API, if we are importing from file we assume config changes
    config_changed_since_last_import = importState.ImportFileName != None or \
        fw_module.has_config_changed(configNative, importState, force=importState.ForceImport)
    if config_changed_since_last_import:
        logger.info ( "has_config_changed: changes found or forced mode -> go ahead with getting config, Force = " + str(importState.ForceImport))
    else:
        logger.info ( "has_config_changed: no new changes found")

    if config_changed_since_last_import or importState.ForceImport:
        # get config from product-specific FW API
        _, normalized_config_list = fw_module.get_config(configNative, importState)
    else:
        normalized_config_list = FwConfigManagerListController.generate_empty_config(importState.MgmDetails.IsSuperManager)

    write_native_config_to_file(importState, configNative)

    logger.debug("import_management: get_config completed (including normalization), duration: " + str(int(time.time()) - importState.StartTime) + "s") 

    return config_changed_since_last_import, normalized_config_list


def set_filename(import_state: ImportStateController, file_name: str = ''):
    # set file name in importState
    if file_name == '' or file_name is None: 
        # if the host name is an URI, do not connect to an API but simply read the config from this URI
        if stringIsUri(import_state.MgmDetails.Hostname):
            import_state.setImportFileName(import_state.MgmDetails.Hostname)
    else:
        import_state.setImportFileName(file_name)  


def write_native_config_to_file(importState, configNative):
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
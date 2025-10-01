import importlib
import traceback
import sys
import time
from socket import gethostname

from fwo_const import importer_base_dir, fwo_config_filename
from pathlib import Path
if importer_base_dir not in sys.path:
    sys.path.append(importer_base_dir) # adding absolute path here once
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_log import getFwoLogger
from fwo_const import fw_module_name, import_tmp_path, importer_user_name
import fwo_globals
from fwo_base import write_native_config_to_file
from fwo_exceptions import ShutdownRequested, FwoApiLoginFailed, FwoImporterError, FwLoginFailed, ImportRecursionLimitReached, FwoApiWriteError, FwoImporterErrorInconsistencies, ImportInterruption
from fwo_base import stringIsUri
import fwo_file_import
from model_controllers.import_state_controller import ImportStateController
from models.gateway import Gateway
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.management_controller import ManagementController
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.check_consistency import FwConfigImportCheckConsistency
from model_controllers.rollback import FwConfigImportRollback
import fwo_signalling
from services.service_provider import ServiceProvider
from services.enums import Services
from model_controllers.fworch_config_controller import FworchConfigController
from fwo_config import readConfig


"""  
    import_management: import a single management (if no import for it is running)
    if mgmId is that of a super management, it will import all submanagements as well
    lock mgmt for import via FWORCH API call, generating new import_id
    check if we need to import (no md5, api call if anything has changed since last import)
    get complete config (get, enrich, parse)
    write into json dict write json dict to new table (single entry for complete config)
    this top level function mainly deals with exception handling

    expects service_provider to be initialized
"""
def import_management(mgmId=None, ssl_verification=None, debug_level_in=0, 
        limit=150, force=False, clearManagementData=False, suppress_cert_warnings_in=None,
        in_file=None, version=8) -> int:

    fwo_signalling.registerSignallingHandlers()
    logger = getFwoLogger(debug_level=debug_level_in)
    # config_changed_since_last_import = True //TODO: implement change detection
    verify_certs = (ssl_verification is not None)

    service_provider = ServiceProvider()
    global_state = service_provider.get_service(Services.GLOBAL_STATE)

    fwoConfig = FworchConfigController.fromJson(readConfig(fwo_config_filename))

    # authenticate to get JWT
    try:
        jwt = FwoApi.login(importer_user_name, fwoConfig.ImporterPassword, fwoConfig.FwoUserMgmtApiUri)
        api_call = FwoApiCall(FwoApi(ApiUri=fwoConfig.FwoApiUri, Jwt=jwt))
    except FwoApiLoginFailed as e:
        logger.error(e.message)
        raise
    except Exception as e:
        logger.error(f"Unexpected error during login: {str(e)}")
        raise
    importState = ImportStateController.initializeImport(mgmId, fwo_api_uri=fwoConfig.FwoApiUri, jwt=jwt, 
                                            debugLevel=debug_level_in, force=force, version=version, 
                                            isClearingImport=clearManagementData, isFullImport=False, sslVerification=verify_certs)
    global_state.import_state = importState

    config_importer = FwConfigImport()

    try:
        _import_management(service_provider=service_provider, importState=importState, config_importer=config_importer,
                           mgmId=mgmId, ssl_verification=ssl_verification, debug_level_in=debug_level_in,
            limit=limit, clearManagementData=clearManagementData,
            suppress_cert_warnings_in=suppress_cert_warnings_in, in_file=in_file)

    except (FwLoginFailed) as e:
        importState.delete_import() # delete whole import
        importState.addError("Login to FW manager failed")
        rollBackExceptionHandler(importState, configImporter=config_importer, exc=e, errorText="")
    except (ImportRecursionLimitReached) as e:
        importState.delete_import() # delete whole import
        importState.addError("ImportRecursionLimitReached - aborting import")
    except (KeyboardInterrupt, ImportInterruption, ShutdownRequested) as e:
        rollBackExceptionHandler(importState, configImporter=config_importer, exc=e, errorText="shutdown requested")
        raise
    except (FwoApiWriteError, FwoImporterError) as e:
        importState.addError(f"FwoApiWriteError or FwoImporterError: {str(e.args)} - aborting import")
        rollBackExceptionHandler(importState, configImporter=config_importer, exc=e, errorText="")
    except FwoImporterErrorInconsistencies as e:
        importState.delete_import() # delete whole import
        importState.addError(str(e.args))
    except ValueError:
        importState.addError("ValueError - aborting import")
        raise
    except Exception as e:
        handle_unexpected_exception(importState=importState, config_importer=config_importer, e=e)
    finally:
        try:
            api_call.complete_import(importState)
            ServiceProvider().dispose_service(Services.UID2ID_MAPPER, importState.ImportId)
        except Exception as e:
            logger.error(f"Error during import completion: {str(e)}")

    if hasattr(importState, 'Stats') and hasattr(importState.Stats, 'ErrorCount'):
        return importState.Stats.ErrorCount
    else:
        return 1


def _import_management(service_provider, importState: ImportStateController, config_importer=None, mgmId=None, ssl_verification=None, debug_level_in=0,
        limit=150, clearManagementData=False, suppress_cert_warnings_in=None, in_file=None) -> None:

    config_normalized : FwConfigManagerListController

    if config_importer is None:
        config_importer = FwConfigImport()

    logger = getFwoLogger(debug_level=debug_level_in)
    config_changed_since_last_import = True

    if importState.DebugLevel > 8:
        logger.debug(f"import_management - ssl_verification: {ssl_verification}")
        logger.debug(f"import_management - suppress_cert_warnings_in: {suppress_cert_warnings_in}")
        logger.debug(f"import_management - limit: {limit}")

    if importState.MgmDetails.ImportDisabled and not importState.ForceImport:
        logger.info(f"import_management - import disabled for mgm  {str(mgmId)} - skipping")
        return
    
    if importState.MgmDetails.ImporterHostname != gethostname() and not importState.ForceImport:
        logger.info(f"import_management - this host ( {gethostname()}) is not responsible for importing management  {str(mgmId)}")
        importState.responsible_for_importing = False
        return
    
    Path(import_tmp_path).mkdir(parents=True, exist_ok=True)  # make sure tmp path exists
    gateways = ManagementController.buildGatewayList(importState.MgmDetails)

    importState.ImportId = importState.api_call.setImportLock(importState.MgmDetails, importState.IsFullImport, importState.IsInitialImport, fwo_globals.debug_level)
    logger.info(f"starting import of management {importState.MgmDetails.Name} ({str(mgmId)}), import_id={str(importState.ImportId)}")

    if clearManagementData:
        config_normalized = config_importer.clear_management()
    else:
        # get config
        config_changed_since_last_import, config_normalized = get_config_top_level(importState, in_file, gateways)

        # write normalized config to file
        config_normalized.storeFullNormalizedConfigToFile(importState)
        logger.debug("import_management - getting config total duration " + str(int(time.time()) - importState.StartTime) + "s")

    # check config consistency and import it
    if config_changed_since_last_import or importState.ForceImport:
        FwConfigImportCheckConsistency(importState, config_normalized).checkConfigConsistency(config_normalized)
        config_importer.import_management_set(importState, service_provider, config_normalized)
        importState.api_call.update_hit_counter(importState, config_normalized)

    # delete data that has passed the retention time
    # TODO: replace by deletion of old data with removed date > retention?
    if not clearManagementData and importState.DataRetentionDays<importState.DaysSinceLastFullImport:
        config_importer.deleteOldImports() # delete all imports of the current management before the last but one full import



def handle_unexpected_exception(importState=None, config_importer=None, e=None):
    if 'importState' in locals() and importState is not None:
        importState.addError("Unexpected exception in import process - aborting " + traceback.format_exc())
        if 'configImporter' in locals() and config_importer is not None:
            rollBackExceptionHandler(importState, configImporter=config_importer, exc=e)


def rollBackExceptionHandler(importState, configImporter=None, exc=None, errorText=""):
    logger = getFwoLogger()
    try:
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
        importState.delete_import() # delete whole import
    except Exception as rollbackError:
        logger.error(f"Error during rollback: {type(rollbackError).__name__} - {rollbackError}")


def get_config_top_level(importState: ImportStateController, in_file: str|None = None, gateways: list[Gateway]|None = None) \
    -> tuple[bool, FwConfigManagerListController]:
    config_from_file = FwConfigManagerListController.generate_empty_config()
    if gateways is None: gateways = []
    if in_file is not None or stringIsUri(importState.MgmDetails.Hostname):
        ### geting config from file ######################
        if in_file is None:
            in_file = importState.MgmDetails.Hostname
        _, config_from_file = import_from_file(importState, in_file, gateways)
        if not config_from_file.is_native_non_empty():
            config_has_changes=True
            return config_has_changes, config_from_file
        # else we feed the native config back into the importer process for normalization
    ### getting config from firewall manager API ######
    return get_config_from_api(importState, config_from_file)    


def import_from_file(importState: ImportStateController, fileName: str = "", gateways: list[Gateway] = []) -> tuple[bool, FwConfigManagerListController]:

    logger = getFwoLogger(debug_level=importState.DebugLevel)
    logger.debug(f"import_management - not getting config from API but from file: {fileName}")

    config_changed_since_last_import = True
    
    set_filename(importState, file_name=fileName)

    configFromFile = fwo_file_import.read_json_config_from_file(importState)

    return config_changed_since_last_import, configFromFile


def get_config_from_api(importState: ImportStateController, config_in) -> tuple[bool, FwConfigManagerListController]:
    logger = getFwoLogger(debug_level=importState.DebugLevel)

    try: # pick product-specific importer:
        pkg_name = get_module_package_name(importState)
        if f"{importer_base_dir}/{pkg_name}" not in sys.path:
            sys.path.append(f"{importer_base_dir}/{pkg_name}")
        fw_module = importlib.import_module("." + fw_module_name, pkg_name)
    except Exception:
        logger.exception("import_management - error while loading product specific fwcommon module", traceback.format_exc())        
        raise

    # check for changes from product-specific FW API, if we are importing from file we assume config changes
    config_changed_since_last_import = importState.ImportFileName != None or \
        fw_module.has_config_changed(config_in, importState, force=importState.ForceImport)
    if config_changed_since_last_import:
        logger.info ( "has_config_changed: changes found or forced mode -> go ahead with getting config, Force = " + str(importState.ForceImport))
    else:
        logger.info ( "has_config_changed: no new changes found")

    if config_changed_since_last_import or importState.ForceImport:
        # get config from product-specific FW API
        _, native_config = fw_module.get_config(config_in, importState)
    else:
        native_config = FwConfigManagerListController.generate_empty_config(importState.MgmDetails.IsSuperManager)

    write_native_config_to_file(importState, config_in.native_config)

    logger.debug("import_management: get_config completed (including normalization), duration: " 
                 + str(int(time.time()) - importState.StartTime) + "s") 

    return config_changed_since_last_import, native_config


# transform device name and type to correct package name
def get_module_package_name(import_state: ImportStateController):
    if import_state.MgmDetails.DeviceTypeName.lower().replace(' ', '') == 'checkpoint':
        pkg_name = import_state.MgmDetails.DeviceTypeName.lower().replace(' ', '') +\
            import_state.MgmDetails.DeviceTypeVersion.replace(' ', '').replace('MDS', '')
    elif import_state.MgmDetails.DeviceTypeName.lower() == 'fortimanager':
        pkg_name = import_state.MgmDetails.DeviceTypeName.lower().replace(' ', '').replace('fortimanager', 'FortiAdom').lower() +\
            import_state.MgmDetails.DeviceTypeVersion.replace(' ', '').lower()
    else:
        pkg_name = f"{import_state.MgmDetails.DeviceTypeName.lower().replace(' ', '')}{import_state.MgmDetails.DeviceTypeVersion}"

    return pkg_name


def set_filename(import_state: ImportStateController, file_name: str = ''):
    # set file name in importState
    if file_name == '' or file_name is None: 
        # if the host name is an URI, do not connect to an API but simply read the config from this URI
        if stringIsUri(import_state.MgmDetails.Hostname):
            import_state.setImportFileName(import_state.MgmDetails.Hostname)
    else:
        import_state.setImportFileName(file_name)  

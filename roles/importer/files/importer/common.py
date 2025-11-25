import importlib
import traceback
import sys
import time
from socket import gethostname

from fwo_const import IMPORTER_BASE_DIR
from pathlib import Path

from fwo_const import IMPORTER_BASE_DIR
from fwo_log import FWOLogger
if IMPORTER_BASE_DIR not in sys.path:
    sys.path.append(IMPORTER_BASE_DIR) # adding absolute path here once
from fwo_api_call import FwoApiCall
from fwo_const import FW_MODULE_NAME, IMPORT_TMP_PATH
import fwo_globals
from fwo_base import write_native_config_to_file
from fwo_exceptions import ShutdownRequested, FwoImporterError, FwLoginFailed, ImportRecursionLimitReached, FwoApiWriteError, FwoImporterErrorInconsistencies, ImportInterruption
from fwo_base import string_is_uri
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
def import_management(mgm_id: int, api_call: FwoApiCall, ssl_verification: bool, 
        limit: int, clear_management_data: bool, suppress_cert_warnings: bool,
        file: str | None = None) -> None:

    fwo_signalling.register_signalling_handlers()
    service_provider = ServiceProvider()
    importState = service_provider.get_global_state().import_state
    config_importer = FwConfigImport()



    try:
        _import_management(mgm_id, ssl_verification,file , limit, clear_management_data, suppress_cert_warnings)

    except (FwLoginFailed) as e:
        importState.delete_import() # delete whole import
        importState.addError("Login to FW manager failed")
        roll_back_exception_handler(importState, config_importer=config_importer, exc=e, error_text="")
    except (ImportRecursionLimitReached) as e:
        importState.delete_import() # delete whole import
        importState.addError("ImportRecursionLimitReached - aborting import")
    except (KeyboardInterrupt, ImportInterruption, ShutdownRequested) as e:
        roll_back_exception_handler(importState, config_importer=config_importer, exc=e, error_text="shutdown requested")
        raise
    except (FwoApiWriteError, FwoImporterError) as e:
        importState.addError(f"FwoApiWriteError or FwoImporterError: {str(e.args)} - aborting import")
        roll_back_exception_handler(importState, config_importer=config_importer, exc=e, error_text="")
    except FwoImporterErrorInconsistencies as e:
        importState.delete_import() # delete whole import
        importState.addError(str(e.args))
    except ValueError:
        importState.addError("ValueError - aborting import")
        raise
    except Exception as e:
        handle_unexpected_exception(import_state=importState, config_importer=config_importer, e=e)
    finally:
        try:
            api_call.complete_import(importState)
            ServiceProvider().dispose_service(Services.UID2ID_MAPPER, importState.ImportId)
        except Exception as e:
            FWOLogger.error(f"Error during import completion: {str(e)}")


def _import_management(mgm_id: int, ssl_verification: bool, file: str | None,
        limit: int, clear_management_data: bool, suppress_cert_warnings: bool) -> None:

    config_normalized : FwConfigManagerListController

    config_changed_since_last_import = True
    service_provider = ServiceProvider()
    import_state = service_provider.get_global_state().import_state
    config_importer = FwConfigImport()
    FWOLogger.debug(f"import_management - ssl_verification: {ssl_verification}", 9)
    FWOLogger.debug(f"import_management - suppress_cert_warnings_in: {suppress_cert_warnings}", 9)
    FWOLogger.debug(f"import_management - limit: {limit}", 9)

    if import_state.MgmDetails.ImportDisabled and not import_state.ForceImport:
        FWOLogger.info(f"import_management - import disabled for mgm  {str(mgm_id)} - skipping")
        return
    
    if import_state.MgmDetails.ImporterHostname != gethostname() and not import_state.ForceImport:
        FWOLogger.info(f"import_management - this host ( {gethostname()}) is not responsible for importing management  {str(mgm_id)}")
        import_state.responsible_for_importing = False
        return
    
    Path(IMPORT_TMP_PATH).mkdir(parents=True, exist_ok=True)  # make sure tmp path exists
    gateways = ManagementController.build_gateway_list(import_state.MgmDetails)

    import_state.ImportId = import_state.api_call.set_import_lock(import_state.MgmDetails, import_state.IsFullImport, import_state.IsInitialImport)
    FWOLogger.info(f"starting import of management {import_state.MgmDetails.Name} ({str(mgm_id)}), import_id={str(import_state.ImportId)}")

    if clear_management_data:
        config_normalized = config_importer.clear_management()
    else:
        # get config
        config_changed_since_last_import, config_normalized = get_config_top_level(import_state, file, gateways)

        # write normalized config to file
        config_normalized.storeFullNormalizedConfigToFile(import_state)
        FWOLogger.debug("import_management - getting config total duration " + str(int(time.time()) - import_state.StartTime) + "s")

    # check config consistency and import it
    if config_changed_since_last_import or import_state.ForceImport:
        FwConfigImportCheckConsistency(import_state, config_normalized).checkConfigConsistency(config_normalized)
        config_importer.import_management_set(import_state, service_provider, config_normalized)

    # delete data that has passed the retention time
    # TODO: replace by deletion of old data with removed date > retention?
    if not clear_management_data and import_state.DataRetentionDays<import_state.DaysSinceLastFullImport:
        config_importer.deleteOldImports() # delete all imports of the current management before the last but one full import



def handle_unexpected_exception(import_state: ImportStateController | None = None, config_importer: FwConfigImport | None = None, e: Exception | None = None):
    if 'importState' in locals() and import_state is not None:
        import_state.addError("Unexpected exception in import process - aborting " + traceback.format_exc())
        if 'configImporter' in locals() and config_importer is not None:
            roll_back_exception_handler(import_state, config_importer=config_importer, exc=e)


def roll_back_exception_handler(import_state: ImportStateController, config_importer: FwConfigImport | None = None, exc: BaseException | None = None, error_text: str = ""):
    try:
        if fwo_globals.shutdown_requested:
            FWOLogger.warning("Shutdown requested.")
        elif error_text!="":
            FWOLogger.error(f"Exception: {error_text}")
        else:
            if exc is not None:
                FWOLogger.error(f"Exception: {type(exc).__name__}")
            else:
                FWOLogger.error(f"Exception: no exception provided")
        if 'configImporter' in locals() and config_importer is not None:
            FwConfigImportRollback().rollbackCurrentImport()
        else:
            FWOLogger.info("No configImporter found, skipping rollback.")
        import_state.delete_import() # delete whole import
    except Exception as rollbackError:
        FWOLogger.error(f"Error during rollback: {type(rollbackError).__name__} - {rollbackError}")


def get_config_top_level(import_state: ImportStateController, in_file: str|None = None, gateways: list[Gateway]|None = None) \
    -> tuple[bool, FwConfigManagerListController]:
    config_from_file = FwConfigManagerListController.generate_empty_config()
    if gateways is None: gateways = []
    if in_file is not None or string_is_uri(import_state.MgmDetails.Hostname):
        ### getting config from file ######################
        if in_file is None:
            in_file = import_state.MgmDetails.Hostname
        _, config_from_file = import_from_file(import_state, in_file)
        if not config_from_file.is_native_non_empty():
            config_has_changes=True
            return config_has_changes, config_from_file
        # else we feed the native config back into the importer process for normalization
    ### getting config from firewall manager API ######
    return get_config_from_api(import_state, config_from_file)    


def import_from_file(import_state: ImportStateController, file_name: str = "") -> tuple[bool, FwConfigManagerListController]:

    FWOLogger.debug(f"import_management - not getting config from API but from file: {file_name}")

    config_changed_since_last_import = True
    
    set_filename(import_state, file_name=file_name)

    configFromFile = fwo_file_import.read_json_config_from_file(import_state)

    return config_changed_since_last_import, configFromFile


def get_config_from_api(import_state: ImportStateController, config_in: FwConfigManagerListController) -> tuple[bool, FwConfigManagerListController]:
    try: # pick product-specific importer:
        pkg_name = get_module_package_name(import_state)
        if f"{IMPORTER_BASE_DIR}/{pkg_name}" not in sys.path:
            sys.path.append(f"{IMPORTER_BASE_DIR}/{pkg_name}")
        fw_module = importlib.import_module("." + FW_MODULE_NAME, pkg_name)
    except Exception:
        FWOLogger.exception("import_management - error while loading product specific fwcommon module", traceback.format_exc())        
        raise

    # check for changes from product-specific FW API, if we are importing from file we assume config changes
    #TODO: implement real change detection
    config_changed_since_last_import = fw_module.has_config_changed(config_in, import_state, import_state.ForceImport)
    if config_changed_since_last_import:
        FWOLogger.info ( "has_config_changed: changes found or forced mode -> go ahead with getting config, Force = " + str(import_state.ForceImport))
    else:
        FWOLogger.info ( "has_config_changed: no new changes found")

    if config_changed_since_last_import or import_state.ForceImport:
        # get config from product-specific FW API
        _, native_config = fw_module.get_config(config_in, import_state)
    else:
        native_config = FwConfigManagerListController.generate_empty_config(import_state.MgmDetails.IsSuperManager)

    if config_in.native_config is None:
        raise FwoImporterError("import_management: get_config returned no config")
    
    write_native_config_to_file(import_state, config_in.native_config)

    FWOLogger.debug("import_management: get_config completed (including normalization), duration: " 
                 + str(int(time.time()) - import_state.StartTime) + "s") 

    return config_changed_since_last_import, native_config


# transform device name and type to correct package name
def get_module_package_name(import_state: ImportStateController):
    if import_state.MgmDetails.DeviceTypeName.lower().replace(' ', '') == 'checkpoint':
        pkg_name = import_state.MgmDetails.DeviceTypeName.lower().replace(' ', '') +\
            import_state.MgmDetails.DeviceTypeVersion.replace(' ', '').replace('MDS', '')
    elif import_state.MgmDetails.DeviceTypeName.lower() == 'fortimanager':
        pkg_name = import_state.MgmDetails.DeviceTypeName.lower().replace(' ', '').replace('fortimanager', 'FortiAdom').lower() +\
            import_state.MgmDetails.DeviceTypeVersion.replace(' ', '').lower()
    elif import_state.MgmDetails.DeviceTypeName == 'Cisco Asa on FirePower':
        pkg_name = 'ciscoasa' + import_state.MgmDetails.DeviceTypeVersion
    else:
        pkg_name = f"{import_state.MgmDetails.DeviceTypeName.lower().replace(' ', '')}{import_state.MgmDetails.DeviceTypeVersion}"

    return pkg_name


def set_filename(import_state: ImportStateController, file_name: str = ''):
    # set file name in importState
    if file_name == '': 
        # if the host name is an URI, do not connect to an API but simply read the config from this URI
        if string_is_uri(import_state.MgmDetails.Hostname):
            import_state.setImportFileName(import_state.MgmDetails.Hostname)
    else:
        import_state.setImportFileName(file_name)  

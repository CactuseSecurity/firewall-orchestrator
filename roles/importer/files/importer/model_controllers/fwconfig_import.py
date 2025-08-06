import traceback

import fwo_const
from fwo_const import fwo_config_filename, importer_user_name, importer_base_dir
from fwo_api_call import FwoApiCall
from fwo_api import FwoApi
import fwo_globals
from fwo_exceptions import FwoImporterError, FwoApiFailedDeleteOldImports
from fwo_config import readConfig
from fwo_exceptions import ImportInterruption
from fwo_log import getFwoLogger
from model_controllers.import_state_controller import ImportStateController
from fwo_base import ConfigAction, ConfFormat
from models.fwconfig_normalized import FwConfigNormalized
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.rule_enforced_on_gateway_controller import RuleEnforcedOnGatewayController
from services.service_provider import ServiceProvider
from services.global_state import GlobalState
from services.enums import Services
from models.fwconfigmanagerlist import FwConfigManagerList, FwConfigManager
from model_controllers.management_controller import ManagementController
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from services.global_state import GlobalState
from services.service_provider import ServiceProvider
from services.enums import Services
from model_controllers.management_controller import ManagementController
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fworch_config_controller import FworchConfigController


# this class is used for importing a config into the FWO API
class FwConfigImport():

    import_state: ImportStateController
    NormalizedConfig: FwConfigNormalized

    _fw_config_import_rule: FwConfigImportRule
    _fw_config_import_object: FwConfigImportObject
    _fw_config_import_gateway: FwConfigImportGateway
    _global_state: GlobalState

    @property
    def fwconfig_import_object(self):
        return self._fw_config_import_object

    def __init__(self):
        service_provider = ServiceProvider()
        self._global_state = service_provider.get_service(Services.GLOBAL_STATE)

        self.import_state = self._global_state.import_state
        self.NormalizedConfig = self._global_state.normalized_config

        self._fw_config_import_object = FwConfigImportObject()
        self._fw_config_import_rule = FwConfigImportRule()
        self._fw_config_import_gateway = FwConfigImportGateway()

        
    def import_single_config(self, single_manager: FwConfigManager):
        # current implementation restriction: assuming we always get the full config (only inserts) from API
        previousConfig = self.getLatestConfig(mgm_id=self.import_state.lookupManagementId(single_manager.ManagerUid))
        self._global_state.previous_config = previousConfig
        # calculate differences and write them to the database via API
        self.updateDiffs(previousConfig, single_manager)


    def import_management_set(self, import_state: ImportStateController, service_provider: ServiceProvider, mgr_set: FwConfigManagerList):
        global_state = service_provider.get_service(Services.GLOBAL_STATE)

        for manager in sorted(mgr_set, key=lambda m: not getattr(m, 'IsSuperManager', False)):
            """
            the following loop is a preparation for future functionality
            we might add support for multiple configs per manager
            e.g. one config only adds data, one only deletes data, etc.
            currently we always only have one config per manager
            """
            for config in manager.Configs:
                global_state.normalized_config = config
                if manager.IsSuperManager:
                    # store lobal config as it is needed when importing sub managers which might reference it
                    global_state.global_normalized_config = config
                config_importer = FwConfigImport()
                config_importer.import_single_config(manager) 
                if import_state.Stats.ErrorCount>0:
                    raise FwoImporterError("Import failed due to errors.")
                else:
                    config_importer.storeLatestConfig()




    def clear_management(self) -> FwConfigNormalized:
        logger = getFwoLogger(debug_level=self.import_state.DebugLevel)
        logger.info('this import run will reset the configuration of this management to "empty"')
        configNormalized = FwConfigManagerListController()
        # Reset management
        configNormalized.addManager(
            manager=FwConfigManager(
                ManagerUid=ManagementController.calcManagerUidHash(import_state.MgmDetails),
                ManagerName=self.import_state.MgmDetails.Name,
                IsSuperManager=self.import_state.MgmDetails.IsSuperManager,
                SubManagerIds=self.import_state.MgmDetails.SubManagerIds,
                DomainName=self.import_state.MgmDetails.DomainName,
                DomainUid=self.import_state.MgmDetails.DomainUid,
                Configs=[]
            ))
        if len(self.import_state.MgmDetails.SubManagerIds)>0:
            # Read config
            fwo_api = FwoApi(self.import_state.FwoConfig.FwoApiUri, self.import_state.Jwt)
            fwo_api_call = FwoApiCall(fwo_api)
            # # Authenticate to get JWT
            # try:
            #     jwt = fwo_api.login(importer_user_name, fwoConfig.ImporterPassword, fwoConfig.FwoUserMgmtApiUri)
            # except Exception as e:
            #     logger.error(str(e))
            #     raise             
            # Reset submanagement
            for subManagerId in self.import_state.MgmDetails.SubManagerIds:
                # Fetch sub management details
                mgm_details_raw = fwo_api_call.get_mgm_details({"mgmId": subManagerId})
                mgm_details = ManagementController.fromJson(mgm_details_raw)
                configNormalized.addManager(
                    manager=FwConfigManager(
                        ManagerUid=ManagementController.calcManagerUidHash(mgm_details_raw),
                        ManagerName=mgm_details.Name,
                        IsSuperManager=mgm_details.IsSuperManager,
                        SubManagerIds=mgm_details.SubManagerIds,
                        DomainName= mgm_details.DomainName,
                        DomainUid=mgm_details.DomainUid,
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
        self.import_state.IsClearingImport = True # the now following import is a full one
        
        return configNormalized
    

    def updateDiffs(self, previousConfig: FwConfigNormalized, single_manager: FwConfigManager):
        
        self._fw_config_import_object.updateObjectDiffs(previousConfig, single_manager)

        if fwo_globals.shutdown_requested:
            # self.ImportDetails.addError("shutdown requested, aborting import")
            raise ImportInterruption("Shutdown requested during updateObjectDiffs.")

        newRuleIds = self._fw_config_import_rule.updateRulebaseDiffs(previousConfig)

        if fwo_globals.shutdown_requested:
            # self.ImportDetails.addError("shutdown requested, aborting import")
            raise ImportInterruption("Shutdown requested during updateRulebaseDiffs.")

        self.import_state.SetRuleMap(self.import_state.api_call) # update all rule entries (from currently running import for rulebase_links)
        self._fw_config_import_gateway.update_gateway_diffs()

        # get new rules details from API (for obj refs as well as enforcing gateways)
        errors, changes, newRules = self._fw_config_import_rule.getRulesByIdWithRefUids(newRuleIds)

        enforcingController = RuleEnforcedOnGatewayController(self.import_state)
        ids = enforcingController.add_new_rule_enforced_on_gateway_refs(newRules, self.import_state)
        

    # cleanup configs which do not need to be retained according to data retention time
    def deleteOldImports(self) -> None:
        logger = getFwoLogger()
        mgmId = int(self.import_state.MgmDetails.Id)
        delete_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/deleteOldImports.graphql"])

        try:
            deleteResult = self.import_state.api_call.call(delete_mutation, query_variables={"mgmId": mgmId, "is_full_import": self.import_state.IsFullImport })
            if deleteResult['data']['delete_import_control']['returning']['control_id']:
                importsDeleted = len(deleteResult['data']['delete_import_control']['returning']['control_id'])
                if importsDeleted>0:
                    logger.info(f"deleted {str(importsDeleted)} imports which passed the retention time of {ImportStateController.DataRetentionDays} days")
        except Exception:
            fwo_api = FwoApi(self.import_state.FwoConfig.FwoApiUri, self.import_state.Jwt)
            fwo_api_call = FwoApiCall(fwo_api)
            logger.error(f"error while trying to delete old imports for mgm {str(self.import_state.MgmDetails.Id)}")
            fwo_api_call.create_data_issue(self.import_state.FwoConfig.FwoApiUri, self.import_state.Jwt, mgm_id=int(self.import_state.MgmDetails.Id), severity=1, 
                 description="failed to get import lock for management id " + str(mgmId))
            fwo_api_call.setAlert(self.import_state.FwoConfig.FwoApiUri, self.import_state.Jwt, import_id=self.import_state.ImportId, title="import error", mgm_id=str(mgmId), severity=1, role='importer', \
                 description="fwo_api: failed to get import lock", source='import', alertCode=15, mgm_details=self.import_state.MgmDetails)
            raise FwoApiFailedDeleteOldImports(f"management id: {mgmId}") from None


    def storeLatestConfig(self):
        logger = getFwoLogger(debug_level=self.import_state.DebugLevel)
        changes = 0
        errorsFound = 0

        if self.import_state.ImportVersion>8:
            # convert FwConfigImport to FwConfigNormalized
            self.NormalizedConfig = FwConfigNormalized(action=self.NormalizedConfig.action, 
                                    network_objects=self.NormalizedConfig.network_objects, 
                                    service_objects=self.NormalizedConfig.service_objects, 
                                    users=self.NormalizedConfig.users,
                                    zone_objects=self.NormalizedConfig.zone_objects,
                                    rulebases=self.NormalizedConfig.rulebases,
                                    gateways=self.NormalizedConfig.gateways,
                                    ConfigFormat=self.NormalizedConfig.ConfigFormat)
        
            errorsFound = self.deleteLatestConfig()
            if errorsFound:
                getFwoLogger().warning(f"error while trying to delete latest config for mgm_id: {self.import_state.ImportId}")
            insertMutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/storeLatestConfig.graphql"])
            try:
                query_variables = {
                    'mgmId': self.import_state.MgmDetails.Id,
                    'importId': self.import_state.ImportId,
                    'config': self.NormalizedConfig.json()
                }
                import_result = self.import_state.api_call.call(insertMutation, query_variables=query_variables)
                if 'errors' in import_result:
                    logger.exception("fwo_api:storeLatestConfig - error while writing importable config for mgm id " +
                                    str(self.import_state.MgmDetails.Id) + ": " + str(import_result['errors']))
                    errorsFound = 1 # error
                else:
                    changes = import_result['data']['insert_latest_config']['affected_rows']
            except Exception:
                logger.exception(f"failed to write latest normalized config for mgm id {str(self.import_state.MgmDetails.Id)}: {str(traceback.format_exc())}")
                errorsFound = 1 # error
                self.import_state.addError("error while trying to write latest config for management id " + str(self.import_state.MgmDetails.Id))
                raise
            if changes==1:
                errorsFound = 0
            else:
                errorsFound = 1

            if errorsFound:
                getFwoLogger().warning(f"error while writing latest config for mgm_id: {self.import_state.ImportId}")        

        
    def deleteLatestConfig(self) -> int:
        logger = getFwoLogger()
        deleteMutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/deleteLatestConfig.graphql"])
        try:
            query_variables = { 'mgmId': self.import_state.MgmDetails.Id }
            import_result = self.import_state.api_call.call(deleteMutation, query_variables=query_variables)
            if 'errors' in import_result:
                logger.exception("fwo_api:import_latest_config - error while deleting last config for mgm id " +
                                str(self.import_state.MgmDetails.Id) + ": " + str(import_result['errors']))
                return 1 # error
            else:
                changes = import_result['data']['delete_latest_config']['affected_rows']
        except Exception:
            self.import_state.addError(f"failed to delete latest normalized config for mgm id {str(self.import_state.MgmDetails.Id)}: {str(traceback.format_exc())}")
            return 1 # error
        
        if changes<=1:  # if nothing was changed, we are also happy (assuming this to be the first config of the current management)
            return 0
        else:
            return 1


    # return previous config or empty config if there is none; only returns the config of a single management
    def getLatestConfig(self, mgm_id: int = None) -> FwConfigNormalized:
        prev_config = FwConfigNormalized(**{
                                'action': ConfigAction.INSERT,
                                'network_objects': {},
                                'service_objects': {},
                                'users': {},
                                'zone_objects': {},
                                'rules': [],
                                'gateways': [],
                                'ConfigFormat': ConfFormat.NORMALIZED_LEGACY
                            })
        logger = getFwoLogger(debug_level=self.import_state.DebugLevel)

        if not mgm_id:
            logger.error("fwo_api:import_latest_config - no mgm id found for current manager uid")
            return prev_config
        
        query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/getLatestConfig.graphql"])
        query_variables = { 'mgmId': mgm_id }
        try:
            query_result = self.import_state.api_connection.call(query, query_variables=query_variables)
            if 'errors' in query_result:
                logger.exception("fwo_api:import_latest_config - error while deleting last config for mgm id " +
                                str(self.import_state.MgmDetails.Id) + ": " + str(query_result['errors']))
                return 1 # error
            else:
                if len(query_result['data']['latest_config'])>0: # do we have a prev config?
                    prev_config = FwConfigNormalized.parse_raw(query_result['data']['latest_config'][0]['config'])
                    
            return prev_config
        except Exception:
            logger.exception(f"failed to get latest normalized config for mgm id {str(self.import_state.MgmDetails.Id)}: {str(traceback.format_exc())}")
            raise FwoImporterError("error while trying to get the previous config")

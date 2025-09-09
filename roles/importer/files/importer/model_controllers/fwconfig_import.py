import traceback

import fwo_const
from fwo_api_call import FwoApiCall
from fwo_api import FwoApi
import fwo_globals
from fwo_exceptions import FwoImporterError, FwoApiFailedDeleteOldImports
from fwo_exceptions import ImportInterruption
from fwo_log import getFwoLogger
from model_controllers.import_state_controller import ImportStateController
from fwo_base import ConfigAction, ConfFormat, find_all_diffs
from models.fwconfig_normalized import FwConfigNormalized
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.rule_enforced_on_gateway_controller import RuleEnforcedOnGatewayController
from services.service_provider import ServiceProvider
from services.global_state import GlobalState
from services.enums import Services
from models.fwconfigmanagerlist import FwConfigManager
from model_controllers.management_controller import ManagementController, DeviceInfo, ConnectionInfo, CredentialInfo, ManagerInfo, DomainInfo
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController


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
        mgm_id = self.import_state.lookupManagementId(single_manager.ManagerUid)
        if mgm_id is None:
            raise FwoImporterError(f"could not find manager id in DB for UID {single_manager.ManagerUid}")
        # previousConfig = self.getLatestConfig(mgm_id=mgm_id)
        previousConfig = self.get_latest_config_from_db()
        self._global_state.previous_config = previousConfig
        # calculate differences and write them to the database via API
        self.updateDiffs(previousConfig, single_manager)


    def import_management_set(self, import_state: ImportStateController, service_provider: ServiceProvider, mgr_set: FwConfigManagerListController):
        global_state = service_provider.get_service(Services.GLOBAL_STATE)

        for manager in sorted(mgr_set.ManagerSet, key=lambda m: not getattr(m, 'IsSuperManager', False)):
            """
            the following loop is a preparation for future functionality
            we might add support for multiple configs per manager
            e.g. one config only adds data, one only deletes data, etc.
            currently we always only have one config per manager
            """
            for config in manager.Configs:
                self.import_config(service_provider, import_state, manager, config)

    def import_config(self, service_provider: ServiceProvider, import_state: ImportStateController, manager: FwConfigManager, config: FwConfigNormalized):
        global_state = service_provider.get_service(Services.GLOBAL_STATE)
        global_state.normalized_config = config
        if manager.IsSuperManager:
            # store global config as it is needed when importing sub managers which might reference it
            global_state.global_normalized_config = config
        mgm_id = self.import_state.lookupManagementId(manager.ManagerUid)
        if mgm_id is None:
            raise FwoImporterError(f"could not find manager id in DB for UID {manager.ManagerUid}")
        #TODO: clean separation between values relevant for all managers and those only relevant for specific managers
        self.import_state.MgmDetails.Id = mgm_id
        self.import_state.MgmDetails.Uid = manager.ManagerUid
        self.import_state.MgmDetails.Name = manager.ManagerName
        self.import_state.MgmDetails.IsSuperManager = manager.IsSuperManager
        if not manager.IsSuperManager:
            self.import_state.MgmDetails.SubManagerIds = []
            self.import_state.MgmDetails.SubManagers = []
        config_importer = FwConfigImport() #TODO: strange to create another import object here - see #3154
        config_importer.import_single_config(manager)
        if import_state.Stats.ErrorCount>0:
            raise FwoImporterError("Import failed due to errors.")
        else:
            config_importer.consistency_check_db()
            config_importer.write_latest_config()


    def clear_management(self) -> FwConfigManagerListController:
        logger = getFwoLogger(debug_level=self.import_state.DebugLevel)
        logger.info('this import run will reset the configuration of this management to "empty"')
        configNormalized = FwConfigManagerListController()
        # Reset management
        configNormalized.addManager(
            manager=FwConfigManager(
                ManagerUid=self.import_state.MgmDetails.calcManagerUidHash(),
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
                mgm_controller = ManagementController(
                    mgm_id=int(subManagerId), uid='', devices={},
                    device_info=DeviceInfo(),
                    connection_info=ConnectionInfo(),
                    importer_hostname='',
                    credential_info=CredentialInfo(),
                    manager_info=ManagerInfo(),
                    domain_info=DomainInfo()
                )
                mgm_details_raw = mgm_controller.get_mgm_details(fwo_api, subManagerId)
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
                    network_objects={}, 
                    service_objects={}, 
                    users={}, 
                    zone_objects={}, 
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
            fwo_api_call.set_alert(import_id=self.import_state.ImportId, title="import error", mgm_id=str(mgmId), severity=1, \
                 description="fwo_api: failed to get import lock", source='import', alertCode=15, mgm_details=self.import_state.MgmDetails)
            raise FwoApiFailedDeleteOldImports(f"management id: {mgmId}") from None


    def write_latest_config(self):
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
        
            errorsFound = self.deleteLatestConfigOfManagement()
            if errorsFound:
                getFwoLogger().warning(f"error while trying to delete latest config for mgm_id: {self.import_state.ImportId}")
            insertMutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/storeLatestConfig.graphql"])
            try:
                query_variables = {
                    'mgmId': self.import_state.MgmDetails.Id,
                    'importId': self.import_state.ImportId,
                    'config': self.NormalizedConfig.model_dump_json()
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
                getFwoLogger().warning(f"error while writing latest config for import_id {self.import_state.ImportId}, mgm_id: {self.import_state.MgmDetails.Id}, mgm_uid: {self.import_state.MgmDetails.Uid}")

        
    def deleteLatestConfigOfManagement(self) -> int:
        logger = getFwoLogger()
        deleteMutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/deleteLatestConfigOfManagement.graphql"])
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

    def get_latest_import_id(self) -> int|None:
        logger = getFwoLogger(debug_level=self.import_state.DebugLevel)
        query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/getLastSuccessImport.graphql"])
        query_variables = { 'mgmId': self.import_state.MgmDetails.Id }
        try:
            query_result = self.import_state.api_connection.call(query, query_variables=query_variables)
            if 'errors' in query_result:
                raise FwoImporterError(f"failed to get latest import id for mgm id {str(self.import_state.MgmDetails.Id)}: {str(query_result['errors'])}")
            if len(query_result['data']['import_control']) == 0:
                return None
            return query_result['data']['import_control'][0]['control_id']
        except Exception:
            logger.exception(f"failed to get latest import id for mgm id {str(self.import_state.MgmDetails.Id)}: {str(traceback.format_exc())}")
            raise FwoImporterError("error while trying to get the latest import id")

    # return previous config or empty config if there is none; only returns the config of a single management
    def getLatestConfig(self, mgm_id: int) -> FwConfigNormalized:
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

        latest_import_id = self.get_latest_import_id()
        if latest_import_id is None:
            logger.info(f"first import - no existing import was found for mgm id {mgm_id}") #TODO: change msg
            return prev_config

        query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "import/getLatestConfig.graphql"])
        query_variables = { 'mgmId': mgm_id }
        try:
            query_result = self.import_state.api_connection.call(query, query_variables=query_variables)
            if 'errors' in query_result:
                raise FwoImporterError(f"failed to get latest config for mgm id {str(self.import_state.MgmDetails.Id)}: {str(query_result['errors'])}")
            else:
                if len(query_result['data']['latest_config'])>0: # do we have a prev config?
                    if query_result['data']['latest_config'][0]['import_id'] == latest_import_id:
                        prev_config = FwConfigNormalized.model_validate_json(query_result['data']['latest_config'][0]['config'])
                        return prev_config
                    else:
                        logger.warning(f"fwo_api:import_latest_config - latest config for mgm id {mgm_id} did not match last import id {latest_import_id}")
                logger.info("fetching latest config from DB as fallback")
                return self.get_latest_config_from_db()
        except Exception:
            logger.exception(f"failed to get latest normalized config for mgm id {str(self.import_state.MgmDetails.Id)}: {str(traceback.format_exc())}")
            raise FwoImporterError("error while trying to get the previous config")

    def get_latest_config_from_db(self) -> FwConfigNormalized:
        logger = getFwoLogger(debug_level=self.import_state.DebugLevel)
        params = {
            "mgm-ids": [self.import_state.MgmDetails.Id]
        }
        result = self.import_state.api_connection.call_endpoint("POST", "api/NormalizedConfig/Get", params=params)
        try:
            latest_config = FwConfigNormalized.model_validate(result)
            return latest_config
        except Exception:
            logger.exception(f"failed to get latest normalized config from db for mgm id {str(self.import_state.MgmDetails.Id)}: {str(traceback.format_exc())}")
            raise FwoImporterError("error while trying to get the latest config")

    def consistency_check_db(self):
        logger = getFwoLogger(debug_level=self.import_state.DebugLevel)
        normalized_config = self.NormalizedConfig
        normalized_config_from_db = self.get_latest_config_from_db()
        if normalized_config != normalized_config_from_db:
            all_diffs = find_all_diffs(normalized_config.model_dump(), normalized_config_from_db.model_dump())
            logger.warning(f"normalized config for mgm id {self.import_state.MgmDetails.Id} is inconsistent to database state: {all_diffs[0]}")
            logger.debug(f"all differences: {all_diffs}")
            # TODO: long-term this should raise an error:
            # raise FwoImporterError("the database state created by this import is not consistent to the normalized config")

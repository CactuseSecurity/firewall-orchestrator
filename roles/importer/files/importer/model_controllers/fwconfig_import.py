import traceback
from typing import Any

import fwo_const
from fwo_api_call import FwoApiCall
from fwo_api import FwoApi
import fwo_globals
from fwo_exceptions import FwoImporterError, FwoApiFailedDeleteOldImports
from fwo_exceptions import ImportInterruption
from fwo_log import FWOLogger
from model_controllers.import_state_controller import ImportStateController
from fwo_base import ConfigAction, find_all_diffs
from models.fwconfig_normalized import FwConfigNormalized
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.rule_enforced_on_gateway_controller import RuleEnforcedOnGatewayController
from services.service_provider import ServiceProvider
from services.global_state import GlobalState
from models.fwconfigmanagerlist import FwConfigManager
from model_controllers.management_controller import ManagementController, DeviceInfo, ConnectionInfo, CredentialInfo, ManagerInfo, DomainInfo
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController


# this class is used for importing a config into the FWO API
class FwConfigImport():

    import_state: ImportStateController
    NormalizedConfig: FwConfigNormalized | None

    _fw_config_import_rule: FwConfigImportRule
    _fw_config_import_object: FwConfigImportObject
    _fw_config_import_gateway: FwConfigImportGateway
    _global_state: GlobalState

    @property
    def fwconfig_import_object(self):
        return self._fw_config_import_object

    def __init__(self):
        service_provider = ServiceProvider()
        self._global_state = service_provider.get_global_state()
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
        previousConfig = self.get_latest_config_from_db()
        self._global_state.previous_config = previousConfig
        if single_manager.IsSuperManager:
            self._global_state.previous_global_config = previousConfig

        # calculate differences and write them to the database via API
        self.updateDiffs(previousConfig, self._global_state.previous_global_config, single_manager)


    def import_management_set(self, import_state: ImportStateController, service_provider: ServiceProvider, mgr_set: FwConfigManagerListController):
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
        global_state = service_provider.get_global_state()
        global_state.normalized_config = config
        if manager.IsSuperManager:
            # store global config as it is needed when importing sub managers which might reference it
            global_state.global_normalized_config = config
        mgm_id = self.import_state.lookupManagementId(manager.ManagerUid)
        if mgm_id is None:
            raise FwoImporterError(f"could not find manager id in DB for UID {manager.ManagerUid}")
        #TODO: clean separation between values relevant for all managers and those only relevant for specific managers - see #3646
        self.import_state.MgmDetails.CurrentMgmId = mgm_id
        self.import_state.MgmDetails.CurrentMgmIsSuperManager = manager.IsSuperManager
        config_importer = FwConfigImport() #TODO: strange to create another import object here - see #3154
        config_importer.import_single_config(manager)
        if import_state.Stats.ErrorCount>0:
            raise FwoImporterError("Import failed due to errors.")
        else:
            config_importer.consistency_check_db()
            config_importer.write_latest_config()


    def clear_management(self) -> FwConfigManagerListController:
        FWOLogger.info('this import run will reset the configuration of this management to "empty"')
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
            _ = FwoApiCall(fwo_api) #TODO why not used ??
            # # Authenticate to get JWT
            # try:
            #     jwt = fwo_api.login(importer_user_name, fwoConfig.ImporterPassword, fwoConfig.FwoUserMgmtApiUri)
            # except Exception as e:
            #     FWOLogger.error(str(e))
            #     raise             
            # Reset submanagement
            for subManagerId in self.import_state.MgmDetails.SubManagerIds:
                # Fetch sub management details
                mgm_controller = ManagementController(
                    mgm_id=int(subManagerId), uid='', devices=[],
                    device_info=DeviceInfo(),
                    connection_info=ConnectionInfo(),
                    importer_hostname='',
                    credential_info=CredentialInfo(),
                    manager_info=ManagerInfo(),
                    domain_info=DomainInfo()
                )
                mgm_details_raw = mgm_controller.get_mgm_details(fwo_api, subManagerId)
                mgm_details = ManagementController.from_json(mgm_details_raw)
                configNormalized.addManager(
                    manager=FwConfigManager(
                        ManagerUid=ManagementController.calcManagerUidHash(mgm_details_raw), #type: ignore # TODO: check: should be mgm_details
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
    

    def updateDiffs(self, prev_config: FwConfigNormalized, prev_global_config: FwConfigNormalized|None, single_manager: FwConfigManager):

        self._fw_config_import_object.updateObjectDiffs(prev_config, prev_global_config, single_manager)

        if fwo_globals.shutdown_requested:
            # self.ImportDetails.addError("shutdown requested, aborting import")
            raise ImportInterruption("Shutdown requested during updateObjectDiffs.")

        newRuleIds = self._fw_config_import_rule.updateRulebaseDiffs(prev_config)

        if fwo_globals.shutdown_requested:
            # self.ImportDetails.addError("shutdown requested, aborting import")
            raise ImportInterruption("Shutdown requested during updateRulebaseDiffs.")

        self.import_state.SetRuleMap(self.import_state.api_call) # update all rule entries (from currently running import for rulebase_links)
        self._fw_config_import_gateway.update_gateway_diffs()

        # get new rules details from API (for obj refs as well as enforcing gateways)
        newRules = self._fw_config_import_rule.getRulesByIdWithRefUids(newRuleIds)

        enforcingController = RuleEnforcedOnGatewayController(self.import_state)
        enforcingController.add_new_rule_enforced_on_gateway_refs(newRules, self.import_state)
        

    # cleanup configs which do not need to be retained according to data retention time
    def deleteOldImports(self) -> None:
        mgmId = int(self.import_state.MgmDetails.Id)
        delete_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/deleteOldImports.graphql"])

        try:
            deleteResult = self.import_state.api_call.call(delete_mutation, query_variables={"mgmId": mgmId, "is_full_import": self.import_state.IsFullImport })
            if deleteResult['data']['delete_import_control']['returning']['control_id']:
                importsDeleted = len(deleteResult['data']['delete_import_control']['returning']['control_id'])
                if importsDeleted>0:
                    FWOLogger.info(f"deleted {str(importsDeleted)} imports which passed the retention time of {ImportStateController.DataRetentionDays} days")
        except Exception:
            fwo_api = FwoApi(self.import_state.FwoConfig.FwoApiUri, self.import_state.Jwt)
            fwo_api_call = FwoApiCall(fwo_api)
            FWOLogger.error(f"error while trying to delete old imports for mgm {str(self.import_state.MgmDetails.Id)}")
            fwo_api_call.create_data_issue(mgm_id=self.import_state.MgmDetails.Id, severity=1, 
                 description="failed to get import lock for management id " + str(mgmId))
            fwo_api_call.set_alert(import_id=self.import_state.ImportId, title="import error", mgm_id=mgmId, severity=1, \
                 description="fwo_api: failed to get import lock", source='import', alert_code=15, mgm_details=self.import_state.MgmDetails)
            raise FwoApiFailedDeleteOldImports(f"management id: {mgmId}") from None


    def write_latest_config(self):
        changes = 0
        errorsFound = 0

        if self.import_state.ImportVersion>8:
            if self.NormalizedConfig is None:
                raise FwoImporterError("cannot write latest config: NormalizedConfig is None")
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
                FWOLogger.warning(f"error while trying to delete latest config for mgm_id: {self.import_state.ImportId}")
            insertMutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/storeLatestConfig.graphql"])
            try:
                query_variables: dict[str, Any] = {
                    'mgmId': self.import_state.MgmDetails.CurrentMgmId,
                    'importId': self.import_state.ImportId,
                    'config': self.NormalizedConfig.model_dump_json()
                }
                import_result = self.import_state.api_call.call(insertMutation, query_variables=query_variables)
                if 'errors' in import_result:
                    FWOLogger.exception("fwo_api:storeLatestConfig - error while writing importable config for mgm id " +
                                    str(self.import_state.MgmDetails.CurrentMgmId) + ": " + str(import_result['errors']))
                    errorsFound = 1 # error
                else:
                    changes = import_result['data']['insert_latest_config']['affected_rows']
            except Exception:
                FWOLogger.exception(f"failed to write latest normalized config for mgm id {str(self.import_state.MgmDetails.CurrentMgmId)}: {str(traceback.format_exc())}")
                errorsFound = 1 # error
                self.import_state.addError("error while trying to write latest config for management id " + str(self.import_state.MgmDetails.Id))
                raise
            if changes==1:
                errorsFound = 0
            else:
                errorsFound = 1

            if errorsFound:
                FWOLogger.warning(f"error while writing latest config for import_id {self.import_state.ImportId}, mgm_id: {self.import_state.MgmDetails.Id}, mgm_uid: {self.import_state.MgmDetails.Uid}")

        
    def deleteLatestConfigOfManagement(self) -> int:
        deleteMutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/deleteLatestConfigOfManagement.graphql"])
        try:
            query_variables = { 'mgmId': self.import_state.MgmDetails.CurrentMgmId }
            import_result = self.import_state.api_call.call(deleteMutation, query_variables=query_variables)
            if 'errors' in import_result:
                FWOLogger.exception("fwo_api:import_latest_config - error while deleting last config for mgm id " +
                                str(self.import_state.MgmDetails.CurrentMgmId) + ": " + str(import_result['errors']))
                return 1 # error
            else:
                changes = import_result['data']['delete_latest_config']['affected_rows']
        except Exception:
            self.import_state.addError(f"failed to delete latest normalized config for mgm id {str(self.import_state.MgmDetails.CurrentMgmId)}: {str(traceback.format_exc())}")
            return 1 # error
        
        if changes<=1:  # if nothing was changed, we are also happy (assuming this to be the first config of the current management)
            return 0
        else:
            return 1

    def get_latest_import_id(self) -> int|None:
        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/getLastSuccessImport.graphql"])
        query_variables = { 'mgmId': self.import_state.MgmDetails.Id }
        try:
            query_result = self.import_state.api_connection.call(query, query_variables=query_variables)
            if 'errors' in query_result:
                raise FwoImporterError(f"failed to get latest import id for mgm id {str(self.import_state.MgmDetails.Id)}: {str(query_result['errors'])}")
            if len(query_result['data']['import_control']) == 0:
                return None
            return query_result['data']['import_control'][0]['control_id']
        except Exception:
            FWOLogger.exception(f"failed to get latest import id for mgm id {str(self.import_state.MgmDetails.Id)}: {str(traceback.format_exc())}")
            raise FwoImporterError("error while trying to get the latest import id")

    # return previous config or empty config if there is none; only returns the config of a single management
    def get_latest_config(self) -> FwConfigNormalized:
        mgm_id = self.import_state.MgmDetails.CurrentMgmId
        prev_config = FwConfigNormalized()

        latest_import_id = self.get_latest_import_id()
        if latest_import_id is None:
            FWOLogger.info(f"first import - no existing import was found for mgm id {mgm_id}") #TODO: change msg
            return prev_config

        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/getLatestConfig.graphql"])
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
                        FWOLogger.warning(f"fwo_api:import_latest_config - latest config for mgm id {mgm_id} did not match last import id {latest_import_id}")
                FWOLogger.info("fetching latest config from DB as fallback")
                return self.get_latest_config_from_db()
        except Exception:
            FWOLogger.exception(f"failed to get latest normalized config for mgm id {str(self.import_state.MgmDetails.Id)}: {str(traceback.format_exc())}")
            raise FwoImporterError("error while trying to get the previous config")

    def get_latest_config_from_db(self) -> FwConfigNormalized:
        params = {
            "mgm-ids": [self.import_state.MgmDetails.CurrentMgmId]
        }
        result = self.import_state.api_connection.call_endpoint("POST", "api/NormalizedConfig/Get", params=params)
        try:
            latest_config = FwConfigNormalized.model_validate(result)
            return latest_config
        except Exception:
            FWOLogger.exception(f"failed to get latest normalized config from db for mgm id {str(self.import_state.MgmDetails.Id)}: {str(traceback.format_exc())}")
            raise FwoImporterError("error while trying to get the latest config")

    def _sort_lists(self, config: FwConfigNormalized):
        # sort lists in config to have consistent ordering for diff checks
        config.rulebases.sort(key=lambda rb: rb.uid)
        if any(gw.Uid is None for gw in config.gateways):
            raise FwoImporterError("found gateway without UID while sorting gateways for consistency check - this should not happen")
        config.gateways.sort(key=lambda gw: gw.Uid) # type: ignore
        for gw in config.gateways:
            gw.RulebaseLinks.sort(key=lambda rbl: f"{rbl.from_rulebase_uid}-{rbl.from_rule_uid}-{rbl.to_rulebase_uid}")
            if gw.EnforcedPolicyUids is not None:
                gw.EnforcedPolicyUids.sort()
            if gw.EnforcedNatPolicyUids is not None:
                gw.EnforcedNatPolicyUids.sort()
            #TODO: interfaces and routing as soon as they are implemented

    def consistency_check_db(self):
        normalized_config = self.NormalizedConfig
        if normalized_config is None:
            raise FwoImporterError("cannot perform consistency check: NormalizedConfig is None")
        normalized_config_from_db = self.get_latest_config_from_db()
        self._sort_lists(normalized_config)
        self._sort_lists(normalized_config_from_db)
        all_diffs = find_all_diffs(normalized_config.model_dump(), normalized_config_from_db.model_dump(), strict=True)
        if len(all_diffs) > 0:
            FWOLogger.warning(f"normalized config for mgm id {self.import_state.MgmDetails.CurrentMgmId} is inconsistent to database state: {all_diffs[0]}")
            FWOLogger.debug(f"all {len(all_diffs)} differences:\n\t" + "\n\t".join(all_diffs))
            # TODO: long-term this should raise an error:
            # raise FwoImporterError("the database state created by this import is not consistent to the normalized config")

import traceback

import fwo_const
import fwo_api
from fwo_log import getFwoLogger
from model_controllers.import_state_controller import ImportStateController
from fwo_api_oo import FwoApi
from fwo_base import ConfigAction, ConfFormat
from models.fwconfig_normalized import FwConfigNormalized
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.rule_enforced_on_gateway_controller import RuleEnforcedOnGatewayController


"""
Class hierarchy:
    FwConfigImport(FwConfigImportObject, FwconfigImportRule)
    FwconfigImportObject(FwConfigImportBase)
    FwConfigImportRule(FwConfigImportBase)
    FwConfigImportGateway(FwConfigImportBase)
    FwConfigImportBase(FwConfigNormalized)
"""

# this class is used for importing a config into the FWO API
class FwConfigImport(FwConfigImportObject, FwConfigImportRule, FwConfigImportGateway, FwoApi):
    ImportDetails: ImportStateController
    NormalizedConfig: FwConfigNormalized
    
    def __init__(self, importState: ImportStateController, config: FwConfigNormalized):
        self.ImportDetails = importState
        self.NormalizedConfig = config
        FwConfigImportObject.__init__(self, importState, config)
        
    def importConfig(self):
        # current implementation restriction: assuming we always get the full config (only inserts) from API
        previousConfig = self.getPreviousConfig()
        # calculate differences and write them to the database via API
        self.updateDiffs(previousConfig)
        return

    def updateDiffs(self, previousConfig: FwConfigNormalized):
        self.updateObjectDiffs(previousConfig)
        newRuleIds = self.updateRulebaseDiffs(previousConfig)
        self.ImportDetails.SetRuleMap() # update all rule entries (from currently running import for rulebase_links)
        self.updateGatewayDiffs(previousConfig)

        # raise NotImplementedError("just testing")

        # get new rules details from API (for obj refs as well as enforcing gateways)
        errors, changes, newRules = self.getRulesByIdWithRefUids(newRuleIds)

        self.addNewRule2ObjRefs(newRules)
        # TODO: self.addNewRuleSvcRefs(newRulebases, newRuleIds)

        enforcingController = RuleEnforcedOnGatewayController(self.ImportDetails)
        ids = enforcingController.addNewRuleEnforcedOnGatewayRefs(newRules, self.ImportDetails)

        return 


    # cleanup configs which do not need to be retained according to data retention time
    def deleteOldImports(self) -> None:
        logger = getFwoLogger()
        mgmId = int(self.ImportDetails.MgmDetails.Id)
        deleteMutation = fwo_api.getGraphqlCode([fwo_const.graphqlQueryPath + "import/deleteOldImports.graphql"])

        try:
            deleteResult = self.ImportDetails.call(deleteMutation, query_variables={"mgmId": mgmId, "is_full_import": self.ImportDetails.IsFullImport })
            if deleteResult['data']['delete_import_control']['returning']['control_id']:
                importsDeleted = len(deleteResult['data']['delete_import_control']['returning']['control_id'])
                if importsDeleted>0:
                    logger.info(f"deleted {str(importsDeleted)} imoprts which passed the retention time of {ImportStateController.DataRetentionDays} days")
        except Exception:
            logger.error(f"error while trying to delete old imports for mgm {str(self.ImportDetails.MgmDetails.Id)}")
            # create_data_issue(importState.FwoConfig.FwoApiUri, importState.Jwt, mgm_id=int(importState.MgmDetails.Id), severity=1, 
            #     description="failed to get import lock for management id " + str(mgmId))
            # setAlert(url, importState.Jwt, import_id=importState.ImportId, title="import error", mgm_id=str(mgmId), severity=1, role='importer', \
            #     description="fwo_api: failed to get import lock", source='import', alertCode=15, mgm_details=importState.MgmDetails)
            # raise FwoApiFailedDeleteOldImports("fwo_api: failed to get import lock for management id " + str(mgmId)) from None

    # def fillGateways(self, importState: ImportState):      
    #     for dev in importState.MgmDetails.Devices:
    #         gw = Gateway(Uid = dev['uid'],
    #                      # Uid = f"{dev['name']}_{dev['local_rulebase_name']}",
    #                      Name = dev['name'],
    #                      Routing=[],    # TODO: routing
    #                      Interfaces=[],    # TODO: interfaces
    #                      RulebaseLinks=[],    # TODO: rulebase links
    #                      EnforcedPolicyUids=[dev['local_rulebase_name']] if dev['local_rulebase_name'] is not None else [],
    #                      EnforcedNatPolicyUids=[dev['package_name']] if dev['package_name'] is not None else [],
    #                      GlobalPolicyUid=None  # TODO: global policy UID
    #                      )
    #         self.NormalizedConfig.gateways.append(gw)


    def storeLatestConfig(self):

        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
        changes = 0
        errorsFound = 0

        if self.ImportDetails.ImportVersion>8:
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
                getFwoLogger().warning(f"error while trying to delete latest config for mgm_id: {self.ImportDetails.ImportId}")
            insertMutation = fwo_api.getGraphqlCode([fwo_const.graphqlQueryPath + "import/storeLatestConfig.graphql"])
            try:
                queryVariables = {
                    'mgmId': self.ImportDetails.MgmDetails.Id,
                    'importId': self.ImportDetails.ImportId,
                    'config': self.NormalizedConfig.json()
                }
                import_result = self.ImportDetails.call(insertMutation, queryVariables=queryVariables)
                if 'errors' in import_result:
                    logger.exception("fwo_api:storeLatestConfig - error while writing importable config for mgm id " +
                                    str(self.ImportDetails.MgmDetails.Id) + ": " + str(import_result['errors']))
                    errorsFound = 1 # error
                else:
                    changes = import_result['data']['insert_latest_config']['affected_rows']
            except Exception:
                logger.exception(f"failed to write latest normalized config for mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
                errorsFound = 1 # error
            
            if changes==1:
                errorsFound = 0
            else:
                errorsFound = 1

            if errorsFound:
                getFwoLogger().warning(f"error while writing latest config for mgm_id: {self.ImportDetails.ImportId}")        

        
    def deleteLatestConfig(self) -> int:
        logger = getFwoLogger()
        deleteMutation = fwo_api.getGraphqlCode([fwo_const.graphqlQueryPath + "import/deleteLatestConfig.graphql"])
        try:
            queryVariables = { 'mgmId': self.ImportDetails.MgmDetails.Id }
            import_result = self.ImportDetails.call(deleteMutation, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception("fwo_api:import_latest_config - error while deleting last config for mgm id " +
                                str(self.ImportDetails.MgmDetails.Id) + ": " + str(import_result['errors']))
                return 1 # error
            else:
                changes = import_result['data']['delete_latest_config']['affected_rows']
        except Exception:
            logger.exception(f"failed to delete latest normalized config for mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
            return 1 # error
        
        if changes<=1:  # if nothing was changed, we are also happy (assuming this to be the first config of the current management)
            return 0
        else:
            return 1

    # return previous config or empty config if there is none
    def getPreviousConfig(self) -> FwConfigNormalized:
        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
        query = fwo_api.getGraphqlCode([fwo_const.graphqlQueryPath + "import/getLatestConfig.graphql"])
        queryVariables = { 'mgmId': self.ImportDetails.MgmDetails.Id }
        try:
            queryResult = self.ImportDetails.call(query, queryVariables=queryVariables)
            if 'errors' in queryResult:
                logger.exception("fwo_api:import_latest_config - error while deleting last config for mgm id " +
                                str(self.ImportDetails.MgmDetails.Id) + ": " + str(queryResult['errors']))
                return 1 # error
            else:
                if len(queryResult['data']['latest_config'])>0: # do we have a prev config?
                    prevConfig = FwConfigNormalized.parse_raw(queryResult['data']['latest_config'][0]['config'])
                else:   # we return an empty config (just to satisfy object requirements)
                    prevConfigDict = {
                        'action': ConfigAction.INSERT,
                        'network_objects': {},
                        'service_objects': {},
                        'users': {},
                        'zone_objects': {},
                        'rules': [],
                        'gateways': [],
                        'ConfigFormat': ConfFormat.NORMALIZED_LEGACY
                    }
                    prevConfig = FwConfigNormalized(**prevConfigDict)
                return prevConfig
        except Exception:
            logger.exception(f"failed to get latest normalized config for mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
            raise Exception(f"error while trying to get the previous config")

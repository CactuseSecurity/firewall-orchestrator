from typing import List
import json
import traceback

import fwo_const
from fwo_log import getFwoLogger
from fwoBaseImport import ImportState
from fwo_api_oo import FwoApi
from models.fwconfig_normalized import FwConfigNormalized
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfig_import_rule import FwConfigImportRule


# from models.gateway import Gateway
# from models.rulebase_link import RulebaseLinkUidBased
# from model_controllers.rulebase_link_uid_based_controller import RulebaseLinkUidBasedController
# from model_controllers.rulebase_link_controller import RulebaseLinkController
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway


"""
Class hierachy:
    FwConfigImport(FwConfigImportObject, FwconfigImportRule)
    FwconfigImportObject(FwConfigImportBase)
    FwConfigImportRule(FwConfigImportBase)
    FwConfigImportGateway(FwConfigImportBase)
    FwConfigImportBase(FwConfigNormalized)
"""

# this class is used for importing a config into the FWO API
class FwConfigImport(FwConfigImportObject, FwConfigImportRule, FwConfigImportGateway, FwoApi):
    ImportDetails: ImportState
    NormalizedConfig: FwConfigNormalized
    
    def __init__(self, importState: ImportState, config: FwConfigNormalized):
        self.ImportDetails = importState
        self.NormalizedConfig = config
        
        FwConfigNormalizedController.__init__(self, importState, config)
        FwConfigImportObject.__init__(self, importState, config)
        FwConfigImportRule.__init__(self, importState, config)
        FwConfigImportGateway.__init__(self, importState, config)
        
    def importConfig(self):
        # assuming we always get the full config (only inserts) from API
        previousConfig = self.getPreviousConfig()
        self.updateDiffs(previousConfig)
        # calculate differences and write them to the database via API
        # self.getRulebaseLinks()
        # TODO: deal with networking later
        return

    def updateDiffs(self, previousConfig: FwConfigNormalized):
        objectErrorCount, objectChangeCount = self.updateObjectDiffs(previousConfig)
        ruleErrorCount, ruleChangeCount = self.updateRulebaseDiffs(previousConfig)
        gwErrorCount, gwChangeCount = self.updateGatewayDiffs(previousConfig)

        # update error and change counters
        self.ImportDetails.increaseErrorCounter(objectErrorCount + ruleErrorCount)
        self.ImportDetails.setChangeCounter(objectChangeCount + ruleChangeCount)
        return 

    def storeLatestConfig(self, config):
        logger = getFwoLogger()
        import_mutation = """
            mutation storeLatestConfig($importId: bigint!, $mgmId: Int!, $config: jsonb!) {
                insert_latest_config(objects: {import_id: $importId, mgm_id: $mgmId, config: $config}) {
                    affected_rows
                }
            }
        """
        try:
            queryVariables = {
                'mgmId': self.ImportDetails.MgmDetails.Id,
                'importId': self.ImportDetails.ImportId,
                'config': config
            }
            import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception("fwo_api:storeLatestConfig - error while writing importable config for mgm id " +
                                str(self.ImportDetails.MgmDetails.Id) + ": " + str(import_result['errors']))
                return 1 # error
            else:
                changes = import_result['data']['insert_latest_config']['affected_rows']
        except:
            logger.exception(f"failed to write latest normalized config for mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
            return 1 # error
        
        if changes==1:
            return 0
        else:
            return 1
        
    def deleteLatestConfig(self) -> int:
        logger = getFwoLogger()
        import_mutation = """
            mutation deleteLatestConfig($mgmId: Int!) {
                delete_latest_config(where: { mgm_id: {_eq: $mgmId} }) {
                    affected_rows
                }
            }
        """
        try:
            queryVariables = { 'mgmId': self.ImportDetails.MgmDetails.Id }
            import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception("fwo_api:import_latest_config - error while deleting last config for mgm id " +
                                str(self.ImportDetails.MgmDetails.Id) + ": " + str(import_result['errors']))
                return 1 # error
            else:
                changes = import_result['data']['delete_latest_config']['affected_rows']
        except:
            logger.exception(f"failed to delete latest normalized config for mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
            return 1 # error
        
        if changes<=1:  # if nothing was changed, we are also happy (assuming this to be the first config of the current management)
            return 0
        else:
            return 1

    def storeConfigToApi(self):

         # convert FwConfigImport to FwConfigNormalized
        self.NormalizedConfig = FwConfigNormalized(action=self.NormalizedConfig.action, 
                                  network_objects=self.NormalizedConfig.network_objects, 
                                  service_objects=self.NormalizedConfig.service_objects, 
                                  users=self.NormalizedConfig.users,
                                  zone_objects=self.NormalizedConfig.zone_objects,
                                  rulebases=self.NormalizedConfig.rulebases,
                                  gateways=self.NormalizedConfig.gateways,
                                  ConfigFormat=self.NormalizedConfig.ConfigFormat)
        
        if self.ImportDetails.ImportVersion>8:
            errorsFound = self.deleteLatestConfig()
            if errorsFound:
                getFwoLogger().warning(f"error while trying to delete latest config for mgm_id: {self.ImportDetails.ImportId}")
            errorsFound = self.storeLatestConfig(self.NormalizedConfig.json())
            if errorsFound:
                getFwoLogger().warning(f"error while writing latest config for mgm_id: {self.ImportDetails.ImportId}")


    # cleanup configs which do not need to be retained according to data retention time
    def deleteOldImports(self) -> None:
        logger = getFwoLogger()
        mgmId = int(self.ImportDetails.MgmDetails.Id)
        deleteMutation = """
            mutation deleteOldImports($mgmId: Int!, $lastImportToKeep: bigint!) {
                delete_import_control(where: {mgm_id: {_eq: $mgmId}, control_id: {_lt: $lastImportToKeep}}) {
                    returning {
                        control_id
                    }
                }
            }
        """

        try:
            deleteResult = self.ImportDetails.call(deleteMutation, query_variables={"mgmId": mgmId, "is_full_import": self.ImportDetails.IsFullImport })
            if deleteResult['data']['delete_import_control']['returning']['control_id']:
                importsDeleted = len(deleteResult['data']['delete_import_control']['returning']['control_id'])
                if importsDeleted>0:
                    logger.info(f"deleted {str(importsDeleted)} imoprts which passed the retention time of {ImportState.DataRetentionDays} days")
        except:
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

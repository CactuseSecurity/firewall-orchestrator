from typing import List
import json
import traceback

from fwo_log import getFwoLogger
from fwoBaseImport import ImportState
from fwconfig_normalized import FwConfigNormalized
from fwconfig_import_object import FwConfigImportObject
from fwconfig_import_rule import FwConfigImportRule

"""
Class hierachy:
    FwConfigImport(FwConfigImportObject, FwconfigImportRule)
    FwconfigImportObject(FwConfigImportBase)
    FwConfigImportRule(FwConfigImportBase)
    FwConfigImportBase(FwConfigNormalized)
"""

# this class is used for importing a config into the FWO API
class FwConfigImport(FwConfigImportObject, FwConfigImportRule):
    ImportDetails: ImportState
    
    def __init__(self, importState: ImportState, config: FwConfigNormalized):
        self.FwoApiUrl = importState.FwoConfig.FwoApiUri
        self.FwoJwt = importState.Jwt
        self.ImportDetails = importState
        FwConfigNormalized.__init__(self, action=config.action,
                         network_objects=config.network_objects,
                         service_objects=config.service_objects,
                         users=config.users,
                         zone_objects=config.zone_objects,
                         rules=config.rules,
                         gateways=config.gateways,
                         ConfigFormat=config.ConfigFormat)
        FwConfigImportObject.__init__(self, importState, config)
        FwConfigImportRule.__init__(self, importState, config)
        
    def importConfig(self):

        self.fillGateways(self.ImportDetails)

        # assuming we always get the full config (only inserts) from API
        # 
        if self.isInitialImport():
            self.addObjects()
            self.addRules()
        else:
            previousConfig = self.getPreviousConfig()
            self.updateDiffs(previousConfig)
            
            # build references from gateways to rulebases 
            # deal with networking later

        return 

    def updateDiffs(self, previousConfig: FwConfigNormalized):

        prevConfig = json.loads(previousConfig)

        # calculate network object diffs
        previousNwObjects = prevConfig['network_objects']
        deletedNwobjUids = previousNwObjects.keys() - self.network_objects.keys()
        newNwobjUids = self.network_objects.keys() - previousNwObjects.keys()
        nwobjUidsInBoth = self.network_objects.keys() & previousNwObjects.keys()
        changedNwobjUids = []
        for nwObjUid in nwobjUidsInBoth:
            if previousNwObjects[nwObjUid] != self.network_objects[nwObjUid]:
                changedNwobjUids.append(nwObjUid)

        # calculate service object diffs
        previousSvcObjects = prevConfig['service_objects']
        deletedSvcObjUids = previousSvcObjects.keys() - self.service_objects.keys()
        newSvcObjUids = self.service_objects.keys() - previousSvcObjects.keys()
        svcObjUidsInBoth = self.service_objects.keys() & previousSvcObjects.keys()
        changedSvcObjUids = []
        for uid in svcObjUidsInBoth:
            if previousSvcObjects[uid] != self.service_objects[uid]:
                changedSvcObjUids.append(uid)

        errorCount, numberOfAddedObjects, newNwObjIds, newNwSvcids = self.addNewObjects(newNwobjUids, newSvcObjUids)
        self.addNwObjGroupMemberships(newNwObjIds)
        # TODO:         self.addSvcObjGroupMemberships(newSvcObjIds)

        errorCount, numberOfDeletedObjects, removedNwObjIds, removedNwSvcids = self.markObjectsRemoved(deletedNwobjUids, deletedSvcObjUids)
        # these objects have really been deleted so there should be no refs to them anywhere! verify this

        # TODO: deal with object changes (e.g. group with added member)
        # for nwobjUid in nwobjUidsInBoth:
        #     if self.network_objects[nwobjUid] != prevConfig[nwobjUid]:
        #         self.updateNetworkObject(nwobjUid)
        # TODO: update all references to objects marked as removed

        # TODO: calculate user diffs
        # TODO: calculate zone diffs
        # TODO: write changes to changelog_xxx tables

        # calculate rule diffs

        self.ImportDetails.setErrorCounter(errorCount)
        self.ImportDetails.setChangeCounter(numberOfAddedObjects + numberOfDeletedObjects)
        return 

    def importLatestConfig(self, config):
        logger = getFwoLogger()
        import_mutation = """
            mutation importLatestConfig($importId: bigint!, $mgmId: Int!, $config: jsonb!) {
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
            import_result = self.call(import_mutation, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception("fwo_api:import_latest_config - error while writing importable config for mgm id " +
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
        
    def deleteLatestConfig(self):
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
            import_result = self.call(import_mutation, queryVariables=queryVariables)
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
        conf = FwConfigNormalized(action=self.action, 
                                  network_objects=self.network_objects, 
                                  service_objects=self.service_objects, 
                                  users=self.users,
                                  zone_objects=self.zone_objects,
                                  rules=self.rules,
                                  gateways=self.gateways,
                                  ConfigFormat=self.ConfigFormat)
        
        if self.ImportDetails.ImportVersion>8:
            errorsFound = self.deleteLatestConfig()
            if errorsFound:
                getFwoLogger().warning(f"error while trying to delete latest config for mgm_id: {self.ImportDetails.ImportId}")
            errorsFound = self.importLatestConfig(conf.toJsonString(prettyPrint=False))
            if errorsFound:
                getFwoLogger().warning(f"error while writing latest config for mgm_id: {self.ImportDetails.ImportId}")


    def deleteOldImports(self) -> None:
        logger = getFwoLogger()
        mgmId = int(self.ImportState.MgmDetails.Id)
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
            deleteResult = self.call(deleteMutation, query_variables={"mgmId": mgmId, "is_full_import": self.ImportState.IsFullImport })
            if deleteResult['data']['delete_import_control']['returning']['control_id']:
                importsDeleted = len(deleteResult['data']['delete_import_control']['returning']['control_id'])
                if importsDeleted>0:
                    logger.info(f"deleted {str(importsDeleted)} imoprts which passed the retention time of {ImportState.DataRetentionDays} days")
        except:
            logger.error(f"error while trying to delete old imports for mgm {str(self.ImportState.MgmDetails.Id)}")
            # create_data_issue(importState.FwoConfig.FwoApiUri, importState.Jwt, mgm_id=int(importState.MgmDetails.Id), severity=1, 
            #     description="failed to get import lock for management id " + str(mgmId))
            # setAlert(url, importState.Jwt, import_id=importState.ImportId, title="import error", mgm_id=str(mgmId), severity=1, role='importer', \
            #     description="fwo_api: failed to get import lock", source='import', alertCode=15, mgm_details=importState.MgmDetails)
            # raise FwoApiFailedDeleteOldImports("fwo_api: failed to get import lock for management id " + str(mgmId)) from None

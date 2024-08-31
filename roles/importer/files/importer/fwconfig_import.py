from typing import List
import json
import traceback

from fwo_log import getFwoLogger
from fwoBaseImport import ImportState
from fwconfig_base import ConfigAction, Policy
from fwconfig_normalized import FwConfigNormalized
from fwconfig_import_object import FwConfigImportObject
from fwconfig_import_rule import FwConfigImportRule
import fwo_const


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
        previousConfig = self.getPreviousConfig()
        # calculate differences and write them to the database via API
        self.updateDiffs(previousConfig)
        # TODO: deal with networking later
        return 

    def updateDiffs(self, previousConfig: dict):
        prevConfigDict = json.loads(previousConfig)

        objectErrorCount, objectChangeCount = self.updateObjectDiffs(prevConfigDict)
        ruleErrorCount, ruleChangeCount = self.updateRuleDiffs(prevConfigDict)

        # update error and change counters
        self.ImportDetails.setErrorCounter(self.ImportDetails.ErrorCount + objectErrorCount + ruleErrorCount)
        self.ImportDetails.setChangeCounter(self.ImportDetails.ChangeCount + objectChangeCount + ruleChangeCount)
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

    # pre-flight checks
    def checkConfigConsistency(self):
        issues = {}
        issues.update(self.checkNetworkObjectConsistency())
        issues.update(self.checkServiceObjectConsistency())
        issues.update(self.checkUserObjectConsistency())
        issues.update(self.checkZoneObjectConsistency())
        issues.update(self.checkRuleConsistency())
        return issues

    def checkNetworkObjectConsistency(self):
        issues = {}
        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all new obj refs from all rules
        for policyId in self.rules:
            ## TODO: clean this up! policy should be detected in json.loads
            if isinstance(self.rules[policyId], Policy):
                for ruleId in self.rules[policyId].Rules:
                    allUsedObjRefs += self.rules[policyId].Rules[ruleId]['rule_src_refs'].split(fwo_const.list_delimiter)
                    allUsedObjRefs += self.rules[policyId].Rules[ruleId]['rule_dst_refs'].split(fwo_const.list_delimiter)
            else:
                for ruleId in self.rules[policyId]['Rules']:
                    allUsedObjRefs += self.rules[policyId]['Rules'][ruleId]['rule_src_refs'].split(fwo_const.list_delimiter)
                    allUsedObjRefs += self.rules[policyId]['Rules'][ruleId]['rule_dst_refs'].split(fwo_const.list_delimiter)

        # add all nw obj refs from groups
        for objId in self.network_objects:
            if self.network_objects[objId]['obj_typ']=='group':
                if 'obj_member_refs' in self.network_objects[objId] and self.network_objects[objId]['obj_member_refs'] is not None:
                    allUsedObjRefs += self.network_objects[objId]['obj_member_refs'].split(fwo_const.list_delimiter)

            # TODO: also check color

        # now make list unique and get all refs not contained in network_objects
        allUsedObjRefsUnique = list(set(allUsedObjRefs))
        unresolvableNwObRefs = allUsedObjRefsUnique - self.network_objects.keys()
        if len(unresolvableNwObRefs)>0:
            issues.update({'unresolvableNwObRefs': list(unresolvableNwObRefs)})

        # check that all obj_typ exist 
        allUsedObjTypes = set()
        for objId in self.network_objects:
            allUsedObjTypes.add(self.network_objects[objId]['obj_typ'])
        allUsedObjTypes = list(set(allUsedObjTypes))
        missingNwObjTypes = allUsedObjTypes - self.NetworkObjectTypeMap.keys()
        if len(missingNwObjTypes)>0:
            issues.update({'unresolvableNwObjTypes': list(missingNwObjTypes)})

        return issues
    
    def checkServiceObjectConsistency(self):
        issues = {}
        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all new obj refs from all rules
        for policyId in self.rules:
            ## TODO: clean this up! policy should be detected in json.loads
            if isinstance(self.rules[policyId], Policy):
                for ruleId in self.rules[policyId].Rules:
                    allUsedObjRefs += self.rules[policyId].Rules[ruleId]['rule_svc_refs'].split(fwo_const.list_delimiter)
            else:
                for ruleId in self.rules[policyId]['Rules']:
                    allUsedObjRefs += self.rules[policyId]['Rules'][ruleId]['rule_svc_refs'].split(fwo_const.list_delimiter)

        # add all svc obj refs from groups
        for objId in self.service_objects:
            if self.service_objects[objId]['svc_typ']=='group':
                if 'svc_member_refs' in self.service_objects[objId] and self.service_objects[objId]['svc_member_refs'] is not None:
                    allUsedObjRefs += self.service_objects[objId]['svc_member_refs'].split(fwo_const.list_delimiter)

        # now make list unique and get all refs not contained in service_objects
        allUsedObjRefsUnique = list(set(allUsedObjRefs))
        unresolvableObRefs = allUsedObjRefsUnique - self.service_objects.keys()
        if len(unresolvableObRefs)>0:
            issues.update({'unresolvableSvcObRefs': list(unresolvableObRefs)})

        # check that all obj_typ exist 
        allUsedObjTypes = set()
        for objId in self.service_objects:
            allUsedObjTypes.add(self.service_objects[objId]['svc_typ'])
        allUsedObjTypes = list(set(allUsedObjTypes))
        missingObjTypes = allUsedObjTypes - self.ServiceObjectTypeMap.keys()
        if len(missingObjTypes)>0:
            issues.update({'unresolvableSvcObjTypes': list(missingObjTypes)})

        return issues
    
    def checkUserObjectConsistency(self):

        def collectUsersFromRule(listOfElements):
            userRefs = []
            for el in listOfElements:
                splitResult = el.split(fwo_const.user_delimiter)
                if len(splitResult)==2:
                    allUsedObjRefs.append(splitResult[0])
            return userRefs

        issues = {}
        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all user refs from all rules
        for policyId in self.rules:
            ## TODO: clean this up! policy should be detected in json.loads
            if isinstance(self.rules[policyId], Policy):
                for ruleId in self.rules[policyId].Rules:
                    if fwo_const.user_delimiter in self.rules[policyId].Rules[ruleId]['rule_src_refs']:
                        allUsedObjRefs += collectUsersFromRule(self.rules[policyId].Rules[ruleId]['rule_src_refs'].split(fwo_const.list_delimiter))
                        allUsedObjRefs += collectUsersFromRule(self.rules[policyId].Rules[ruleId]['rule_dst_refs'].split(fwo_const.list_delimiter))
            else:
                for ruleId in self.rules[policyId]['Rules']:
                    if fwo_const.user_delimiter in self.rules[policyId]['Rules'][ruleId]['rule_src_refs']:
                        allUsedObjRefs += collectUsersFromRule(self.rules[policyId]['Rules'][ruleId]['rule_src_refs'].split(fwo_const.list_delimiter))
                        allUsedObjRefs += collectUsersFromRule(self.rules[policyId]['Rules'][ruleId]['rule_dst_refs'].split(fwo_const.list_delimiter))

        # add all user obj refs from groups
        for objId in self.users:
            if self.users[objId]['user_typ']=='group':
                if 'user_member_refs' in self.users[objId] and self.users[objId]['user_member_refs'] is not None:
                    allUsedObjRefs += self.users[objId]['user_member_refs'].split(fwo_const.list_delimiter)

        # now make list unique and get all refs not contained in users
        allUsedObjRefsUnique = list(set(allUsedObjRefs))
        unresolvableObRefs = allUsedObjRefsUnique - self.users.keys()
        if len(unresolvableObRefs)>0:
            issues.update({'unresolvableUserObRefs': list(unresolvableObRefs)})

        # check that all obj_typ exist 
        allUsedObjTypes = set()
        for objId in self.users:
            allUsedObjTypes.add(self.users[objId]['user_typ'])
        allUsedObjTypes = list(set(allUsedObjTypes))    # make list unique
        missingObjTypes = allUsedObjTypes - self.UserObjectTypeMap.keys()
        if len(missingObjTypes)>0:
            issues.update({'unresolvableUserObjTypes': list(missingObjTypes)})

        return issues
    
    def checkZoneObjectConsistency(self):
        issues = {}
        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all zone refs from all rules
        for policyId in self.rules:
            ## TODO: clean this up! policy should be detected in json.loads
            if isinstance(self.rules[policyId], Policy):
                for ruleId in self.rules[policyId].Rules:
                    if 'rule_src_zone' in self.rules[policyId].Rules[ruleId] and \
                        self.rules[policyId].Rules[ruleId]['rule_src_zone'] is not None:
                        allUsedObjRefs += self.rules[policyId].Rules[ruleId]['rule_src_zone']
                    if 'rule_dst_zone' in self.rules[policyId].Rules[ruleId] and \
                        self.rules[policyId].Rules[ruleId]['rule_dst_zone'] is not None:
                        allUsedObjRefs += self.rules[policyId].Rules[ruleId]['rule_dst_zone']
            else:
                for ruleId in self.rules[policyId]['Rules']:
                    if 'rule_src_zone' in self.rules[policyId]['Rules'][ruleId] and \
                        self.rules[policyId]['Rules'][ruleId]['rule_src_zone'] is not None:
                        allUsedObjRefs += self.rules[policyId]['Rules'][ruleId]['rule_src_zone']
                    if 'rule_dst_zone' in self.rules[policyId]['Rules'][ruleId] and \
                        self.rules[policyId]['Rules'][ruleId]['rule_dst_zone'] is not None:
                        allUsedObjRefs += self.rules[policyId]['Rules'][ruleId]['rule_dst_zone']

        # we currently do not have zone groups - skipping group ref handling

        # now make list unique and get all refs not contained in zone_objects
        allUsedObjRefsUnique = list(set(allUsedObjRefs))
        unresolvableObRefs = allUsedObjRefsUnique - self.zone_objects.keys()
        if len(unresolvableObRefs)>0:
            issues.update({'unresolvableZoneObRefs': list(unresolvableObRefs)})

        # we currently do not have zone types - skipping type handling

        return issues
    
    # e.g. check rule to rule refs
    def checkRuleConsistency(self):
        issues = {}
        return issues

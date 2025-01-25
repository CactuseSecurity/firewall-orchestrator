from typing import List
import json
import traceback

from fwo_log import getFwoLogger
from fwoBaseImport import ImportState
from fwo_api_oo import FwoApi
from roles.importer.files.importer.models.rulebase import Rulebase
from models.gateway import Gateway
from models.fwconfig_normalized import FwConfigNormalized
from models.rulebase_link import RulebaseLinkUidBased
from model_controllers.rulebase_link_controller import RulebaseLinkUidBasedController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfig_import_rule import FwConfigImportRule
import fwo_const


"""
Class hierachy:
    FwConfigImport(FwConfigImportObject, FwconfigImportRule)
    FwconfigImportObject(FwConfigImportBase)
    FwConfigImportRule(FwConfigImportBase)
    FwConfigImportBase(FwConfigNormalized)
"""

# this class is used for importing a config into the FWO API
class FwConfigImport(FwConfigImportObject, FwConfigImportRule, FwoApi):
    ImportDetails: ImportState
    # FwoApiUrl: str
    # FwoJwt: str
    NormalizedConfig: FwConfigNormalized
    
    def __init__(self, importState: ImportState, config: FwConfigNormalized):
        # self.FwoApiUrl = importState.FwoConfig.FwoApiUri
        # self.FwoJwt = importState.Jwt
        self.ImportDetails = importState
        self.NormalizedConfig = config
        
        FwConfigNormalizedController.__init__(self, importState, config)
        FwConfigImportObject.__init__(self, importState, config)
        FwConfigImportRule.__init__(self, importState, config)
        
    def importConfig(self):
        self.fillGateways(self.ImportDetails)
        # assuming we always get the full config (only inserts) from API
        previousConfig = self.getPreviousConfig()
        # calculate differences and write them to the database via API
        # self.getRulebaseLinks()
        self.updateDiffs(previousConfig)
        # TODO: deal with networking later
        return

    def updateDiffs(self, previousConfig: FwConfigNormalized):
        # prevConfigDict = json.loads(previousConfig)
        # prevConfigAsObjects = FwConfigNormalized(previousConfig)

        objectErrorCount, objectChangeCount = self.updateObjectDiffs(previousConfig)
        ruleErrorCount, ruleChangeCount = self.updateRuleDiffs(previousConfig)

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

    # pre-flight checks
    def checkConfigConsistency(self):
        issues = {}
        issues.update(self.checkNetworkObjectConsistency())
        issues.update(self.checkServiceObjectConsistency())
        issues.update(self.checkUserObjectConsistency())
        issues.update(self.checkZoneObjectConsistency())
        issues.update(self.checkRuleConsistency())
        if len(issues)>0:
            logger = getFwoLogger()
            logger.warning(f'config not imported due to the following inconsistencies: {json.dumps(issues, indent=3)}')
            self.ImportDetails.increaseErrorCounterByOne()

        return issues

    def checkNetworkObjectConsistency(self):
        issues = {}
        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all new obj refs from all rules
        for rb in self.NormalizedConfig.rulebases:
            for ruleId in rb.Rules:
                allUsedObjRefs += rb.Rules[ruleId].rule_src_refs.split(fwo_const.list_delimiter)
                allUsedObjRefs += rb.Rules[ruleId].rule_dst_refs.split(fwo_const.list_delimiter)

        # add all nw obj refs from groups
        for objId in self.NormalizedConfig.network_objects:
            if self.NormalizedConfig.network_objects[objId].obj_typ=='group':
                if self.NormalizedConfig.network_objects[objId].obj_member_refs is not None:
                    allUsedObjRefs += self.NormalizedConfig.network_objects[objId].obj_member_refs.split(fwo_const.list_delimiter)

            # TODO: also check color

        # now make list unique and get all refs not contained in network_objects
        allUsedObjRefsUnique = list(set(allUsedObjRefs))
        unresolvableNwObRefs = allUsedObjRefsUnique - self.NormalizedConfig.network_objects.keys()
        if len(unresolvableNwObRefs)>0:
            issues.update({'unresolvableNwObRefs': list(unresolvableNwObRefs)})

        # check that all obj_typ exist 
        allUsedObjTypes = set()
        for objId in self.NormalizedConfig.network_objects:
            allUsedObjTypes.add(self.NormalizedConfig.network_objects[objId].obj_typ)
        allUsedObjTypes = list(set(allUsedObjTypes))
        missingNwObjTypes = allUsedObjTypes - self.NetworkObjectTypeMap.keys()
        if len(missingNwObjTypes)>0:
            issues.update({'unresolvableNwObjTypes': list(missingNwObjTypes)})


        # check if there are any objects with obj_typ<>group and empty ip addresses (breaking constraint)
        nonGroupNwObjWithMissingIps = []
        for objId in self.NormalizedConfig.network_objects:
            if self.NormalizedConfig.network_objects[objId].obj_typ!='group':
                ip1 = self.NormalizedConfig.network_objects[objId].obj_ip
                ip2 = self.NormalizedConfig.network_objects[objId].obj_ip_end
                if ip1==None or ip2==None:
                    nonGroupNwObjWithMissingIps.append(self.NormalizedConfig.network_objects[objId])
        if len(nonGroupNwObjWithMissingIps)>0:
            issues.update({'non-group network object with undefined IP addresse(s)': list(nonGroupNwObjWithMissingIps)})

        return issues
    
    def checkServiceObjectConsistency(self):
        issues = {}
        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all new obj refs from all rules
        for rb in self.NormalizedConfig.rulebases:
            for ruleId in rb.Rules:
                allUsedObjRefs += rb.Rules[ruleId].rule_svc_refs.split(fwo_const.list_delimiter)

        # add all svc obj refs from groups
        for objId in self.NormalizedConfig.service_objects:
            if self.NormalizedConfig.service_objects[objId].svc_typ=='group':
                if self.NormalizedConfig.service_objects[objId].svc_member_refs is not None:
                    allUsedObjRefs += self.NormalizedConfig.service_objects[objId].svc_member_refs.split(fwo_const.list_delimiter)

        # now make list unique and get all refs not contained in service_objects
        allUsedObjRefsUnique = list(set(allUsedObjRefs))
        unresolvableObRefs = allUsedObjRefsUnique - self.NormalizedConfig.service_objects.keys()
        if len(unresolvableObRefs)>0:
            issues.update({'unresolvableSvcObRefs': list(unresolvableObRefs)})

        # check that all obj_typ exist 
        allUsedObjTypes = set()
        for objId in self.NormalizedConfig.service_objects:
            allUsedObjTypes.add(self.NormalizedConfig.service_objects[objId].svc_typ)
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
        for rb in self.NormalizedConfig.rulebases:
            for ruleId in rb.Rules:
                if fwo_const.user_delimiter in rb.Rules[ruleId].rule_src_refs:
                    allUsedObjRefs += collectUsersFromRule(rb.Rules[ruleId].rule_src_refs.split(fwo_const.list_delimiter))
                    allUsedObjRefs += collectUsersFromRule(rb.Rules[ruleId].rule_dst_refs.split(fwo_const.list_delimiter))

        # add all user obj refs from groups
        for objId in self.NormalizedConfig.users:
            if self.users[objId].user_typ=='group':
                if self.users[objId].user_member_refs is not None:
                    allUsedObjRefs += self.users[objId].user_member_refs.split(fwo_const.list_delimiter)

        # now make list unique and get all refs not contained in users
        allUsedObjRefsUnique = list(set(allUsedObjRefs))
        unresolvableObRefs = allUsedObjRefsUnique - self.NormalizedConfig.users.keys()
        if len(unresolvableObRefs)>0:
            issues.update({'unresolvableUserObRefs': list(unresolvableObRefs)})

        # check that all obj_typ exist 
        allUsedObjTypes = set()
        for objId in self.NormalizedConfig.users:
            allUsedObjTypes.add(self.NormalizedConfig.users[objId].user_typ)
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
        for rb in self.NormalizedConfig.rulebases:
            for ruleId in rb.Rules:
                if rb.Rules[ruleId].rule_src_zone is not None:
                    allUsedObjRefs += rb.Rules[ruleId].rule_src_zone
                if rb.Rules[ruleId].rule_dst_zone is not None:
                    allUsedObjRefs += rb.Rules[ruleId].rule_dst_zone

        # we currently do not have zone groups - skipping group ref handling

        # now make list unique and get all refs not contained in zone_objects
        allUsedObjRefsUnique = list(set(allUsedObjRefs))
        unresolvableObRefs = allUsedObjRefsUnique - self.NormalizedConfig.zone_objects.keys()
        if len(unresolvableObRefs)>0:
            issues.update({'unresolvableZoneObRefs': list(unresolvableObRefs)})

        # we currently do not have zone types - skipping type handling

        return issues
    
    # e.g. check rule to rule refs
    def checkRuleConsistency(self):
        issues = {}
        return issues

    def rollbackCurrentImport(self) -> None:
        logger = getFwoLogger()
        rollbackMutation = """
            mutation rollbackCurrentImport($mgmId: Int!, $currentImportId: bigint!) {
                delete_rule(where: {mgm_id: {_eq: $mgmId}, rule_create: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_object(where: {mgm_id: {_eq: $mgmId}, obj_create: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_service(where: {mgm_id: {_eq: $mgmId}, svc_create: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_usr(where: {mgm_id: {_eq: $mgmId}, user_create: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_zone(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_objgrp(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_svcgrp(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_usergrp(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_objgrp_flat(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_svcgrp_flat(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_usergrp_flat(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_rule_to(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_rule_from(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_rule_service(where: {removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_rule_nwobj_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_rule_svc_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                delete_rule_user_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}) {
                    affected_rows
                }
                update_rule(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_object(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_service(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_usr(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_zone(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_objgrp(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_svcgrp(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_usergrp(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_objgrp_flat(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_svcgrp_flat(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_usergrp_flat(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_rule_to(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_rule_from(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_rule_service(where: {removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_rule_nwobj_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_rule_svc_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
                update_rule_user_resolved(where: {mgm_id: {_eq: $mgmId}, removed: {_eq: $currentImportId}}, _set: {removed: null}) {
                    affected_rows
                }
            }
        """
        try:
            queryVariables = {
                'mgmId': self.ImportDetails.MgmDetails.Id, 
                'currentImportId': self.ImportDetails.ImportId
            }
            rollbackResult = self.ImportDetails.call(rollbackMutation, queryVariables=queryVariables)
            if 'errors' in rollbackResult:
                logger.exception("error while trying to roll back current import for mgm id " +
                                str(self.ImportDetails.MgmDetails.Id) + ": " + str(rollbackResult['errors']))
                return 1 # error
        except:
            logger.exception(f"failed to rollback current importfor mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
            return 1 # error
        
        return 0

    def fillGateways(self, importState: ImportState):      
        for dev in importState.MgmDetails.Devices:
            gw = Gateway(Uid = dev['uid'],
                         # Uid = f"{dev['name']}_{dev['local_rulebase_name']}",
                         Name = dev['name'],
                         Routing=[],    # TODO: routing
                         Interfaces=[],    # TODO: interfaces
                         RulebaseLinks=[],    # TODO: rulebase links
                         EnforcedPolicyUids=[dev['local_rulebase_name']] if dev['local_rulebase_name'] is not None else [],
                         EnforcedNatPolicyUids=[dev['package_name']] if dev['package_name'] is not None else [],
                         GlobalPolicyUid=None  # TODO: global policy UID
                         )
            self.NormalizedConfig.gateways.append(gw)

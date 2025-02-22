from typing import List
import traceback
import time, datetime
import json

from fwo_log import getFwoLogger
from fwoBaseImport import ImportState
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from model_controllers.fwconfig_import_base import FwConfigImportBase
from models.networkobject import NetworkObjectForImport
from models.serviceobject import ServiceObjectForImport
import fwo_const


# this class is used for importing a config into the FWO API
class FwConfigImportObject(FwConfigImportBase):

    NwObjUidToIdMap = {}
    NwObjMemberUidToIdMap = {}  
    SvcObjUidToIdMap = {}

    # @root_validator(pre=True)
    # def custom_initialization(cls, values):
    #     values['NetworkObjectTypeMap'] = cls.GetNetworkObjTypeMap()
    #     values['ServiceObjectTypeMap'] = cls.GetServiceObjTypeMap()
    #     values['UserObjectTypeMap'] = cls.GetUserObjTypeMap()
    #     values['ProtocolMap'] = cls.GetProtocolMap()
    #     values['ColorMap'] = cls.GetColorMap()
    #     return values
    
    def __init__(self, importState: ImportState, config: FwConfigNormalized):
        super().__init__(importState, config)

        self.NetworkObjectTypeMap = self.GetNetworkObjTypeMap()
        self.ServiceObjectTypeMap = self.GetServiceObjTypeMap()
        self.UserObjectTypeMap = self.GetUserObjTypeMap()
        self.ProtocolMap = self.GetProtocolMap()
        self.ColorMap = self.GetColorMap()

    def updateObjectDiffs(self, prevConfig: FwConfigNormalized):

        # calculate network object diffs
        # here we are handling the previous config as a dict for a while
        # previousNwObjects = prevConfig.network_objects
        deletedNwobjUids = prevConfig.network_objects.keys() - self.NormalizedConfig.network_objects.keys()
        newNwobjUids = self.NormalizedConfig.network_objects.keys() - prevConfig.network_objects.keys()
        nwobjUidsInBoth = self.NormalizedConfig.network_objects.keys() & prevConfig.network_objects.keys()

        # decide if it is prudent to mix changed, deleted and added rules here:
        for nwObjUid in nwobjUidsInBoth:
            if prevConfig.network_objects[nwObjUid] != self.NormalizedConfig.network_objects[nwObjUid]:
                newNwobjUids.add(nwObjUid)
                deletedNwobjUids.add(nwObjUid)

        # calculate service object diffs
        deletedSvcObjUids = list(prevConfig.service_objects.keys() - self.NormalizedConfig.service_objects.keys())
        newSvcObjUids = list(self.NormalizedConfig.service_objects.keys() - prevConfig.service_objects.keys())
        svcObjUidsInBoth = list(self.NormalizedConfig.service_objects.keys() & prevConfig.service_objects.keys())

        for uid in svcObjUidsInBoth:
            if prevConfig.service_objects[uid] != self.NormalizedConfig.service_objects[uid]:
                newSvcObjUids.append(uid)
                deletedSvcObjUids.append(uid)

        # TODO: deal with object changes (e.g. group with added member)

        # add newly created objects
        errorCountUpdate, numberOfModifiedObjects, newNwObjIds, newNwSvcIds, removedNwObjIds, removedNwSvcIds = \
            self.updateObjectsViaApi(newNwobjUids, newSvcObjUids, deletedNwobjUids, deletedSvcObjUids)

        # update group memberships
        self.addNwObjGroupMemberships(newNwObjIds)
        # TODO: self.addSvcObjGroupMemberships(newSvcObjIds)
        # TODO: self.addUserObjGroupMemberships(newUserObjIds)

        # these objects have really been deleted so there should be no refs to them anywhere! verify this

        # update all references to objects marked as removed
        self.markObjectRefsRemoved(removedNwObjIds, removedNwSvcIds)

        # TODO: calculate user diffs
        # TODO: calculate zone diffs

        # TODO: write changes to changelog_xxx tables
        self.addChangelogObjects(newNwObjIds, newNwSvcIds, removedNwObjIds, removedNwSvcIds)

        return errorCountUpdate, numberOfModifiedObjects

    def updateObjectsViaApi(self, newNwObjectUids, newSvcObjectUids, removedNwObjectUids, removedSvcObjectUids):
        # here we also mark old objects removed before adding the new versions
        logger = getFwoLogger()
        errors = 0
        changes = 0
        newNwObjIds = []
        newNwSvcIds = []
        removedNwObjIds = []
        removedNwSvcIds = []
        import_mutation = """
            mutation updateObjects($mgmId: Int!, $importId: bigint!, $removedNwObjectUids: [String!]!, $removedSvcObjectUids: [String!]!, $newNwObjects: [object_insert_input!]!, $newSvcObjects: [service_insert_input!]!) {
                update_object(where: {mgm_id: {_eq: $mgmId}, obj_uid: {_in: $removedNwObjectUids}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
                    affected_rows
                    returning {
                        obj_id
                    }
                }
                update_service(where: {mgm_id: {_eq: $mgmId}, svc_uid: {_in: $removedSvcObjectUids}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
                    affected_rows
                    returning {
                        svc_id
                    }
                }
                insert_object(objects: $newNwObjects) {
                    affected_rows
                    returning {
                        obj_id
                        obj_member_refs
                    }
                }
                insert_service(objects: $newSvcObjects) {
                    affected_rows
                    returning {
                        svc_id
                        svc_member_refs
                    }
                }
            }
        """
        queryVariables = {
            'mgmId': self.ImportDetails.MgmDetails.Id,
            'importId': self.ImportDetails.ImportId,
            'newNwObjects': self.prepareNewNwObjects(newNwObjectUids),
            'newSvcObjects': self.prepareNewSvcObjects(newSvcObjectUids),
            'removedNwObjectUids': list(removedNwObjectUids),
            'removedSvcObjectUids': list(removedSvcObjectUids)
        }
        
        try:
            import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception(f"fwo_api:importNwObject - error in updateObjectsViaApi: {str(import_result['errors'])}")
                errors = 1
            else:
                changes = int(import_result['data']['insert_object']['affected_rows']) + \
                    int(import_result['data']['insert_service']['affected_rows']) + \
                    int(import_result['data']['update_object']['affected_rows']) + \
                    int(import_result['data']['update_service']['affected_rows'])
                newNwObjIds = import_result['data']['insert_object']['returning']
                newNwSvcIds = import_result['data']['insert_service']['returning']
                removedNwObjIds = import_result['data']['update_object']['returning']
                removedNwSvcIds = import_result['data']['update_service']['returning']
        except:
            logger.exception(f"failed to update objects: {str(traceback.format_exc())}")
            errors = 1
        return errors, changes, newNwObjIds, newNwSvcIds, removedNwObjIds, removedNwSvcIds
    
    def GetNetworkObjTypeMap(self):
        query = "query getNetworkObjTypeMap { stm_obj_typ { obj_typ_name obj_typ_id } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_obj_typ')
            return {}
        
        map = {}
        for nwType in result['data']['stm_obj_typ']:
            map.update({nwType['obj_typ_name']: nwType['obj_typ_id']})
        return map

    def GetServiceObjTypeMap(self):
        query = "query getServiceObjTypeMap { stm_svc_typ { svc_typ_name svc_typ_id } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_svc_typ')
            return {}
        
        map = {}
        for svcType in result['data']['stm_svc_typ']:
            map.update({svcType['svc_typ_name']: svcType['svc_typ_id']})
        return map

    def GetUserObjTypeMap(self):
        query = "query getUserObjTypeMap { stm_usr_typ { usr_typ_name usr_typ_id } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_usr_typ')
            return {}
        
        map = {}
        for usrType in result['data']['stm_usr_typ']:
            map.update({usrType['usr_typ_name']: usrType['usr_typ_id']})
        return map

    def GetProtocolMap(self):
        query = "query getIpProtocols { stm_ip_proto { ip_proto_id ip_proto_name } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_ip_proto')
            return {}
        
        map = {}
        for proto in result['data']['stm_ip_proto']:
            map.update({proto['ip_proto_name'].lower(): proto['ip_proto_id']})
        return map

    def GetColorMap(self):
        query = "query getColorMap { stm_color { color_name color_id } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_color')
            return {}
        
        map = {}
        for color in result['data']['stm_color']:
            map.update({color['color_name']: color['color_id']})
        return map

    def prepareNewNwObjects(self, newNwobjUids):
        newNwObjs = []
        for nwobjUid in newNwobjUids:
            newNwObj = NetworkObjectForImport(nwObject=self.NormalizedConfig.network_objects[nwobjUid],
                                                    mgmId=self.ImportDetails.MgmDetails.Id, 
                                                    importId=self.ImportDetails.ImportId, 
                                                    colorId=self.lookupColor(self.NormalizedConfig.network_objects[nwobjUid].obj_color), 
                                                    typId=self.lookupObjType(self.NormalizedConfig.network_objects[nwobjUid].obj_typ))
            newNwObjDict = newNwObj.toDict()
            newNwObjs.append(newNwObjDict)
            # newNwObjs.append(NetworkObjectForImport(nwObject=self.NormalizedConfig.network_objects[nwobjUid],
            #                                         mgmId=self.ImportDetails.MgmDetails.Id, 
            #                                         importId=self.ImportDetails.ImportId, 
            #                                         colorId=self.lookupColor(self.NormalizedConfig.network_objects[nwobjUid].obj_color), 
            #                                         typId=self.lookupObjType(self.NormalizedConfig.network_objects[nwobjUid].obj_typ)).toJson())
        return newNwObjs


    def buildObjUidToIdMapFromApi(self, rules):
        nwObjUids = self.extractNwObjUidsFromRules(rules)
        # svcObjUids = self.extractSvcObjUidsFromRules(rules)
        self.NwObjUidToIdMap = self.buildNwObjUidToIdMapFromApi(nwObjUids)
        self.UserUidToIdMap = self.buildUserObjUidToIdMapFromApi(nwObjUids)
        # self.SvcObjMemberUidToIdMap = self.buildSvcObjUidToIdMapFromApi(svcObjUids)

    def extractNwObjUidsFromRules(self, rules):
        nwObjUids = set()
        for rule in rules:
            if rule['rule_src_refs'] is not None:
                nwObjUids.update(rule['rule_src_refs'].split(fwo_const.list_delimiter))
            if rule['rule_dst_refs'] is not None:
                nwObjUids.update(rule['rule_dst_refs'].split(fwo_const.list_delimiter))
        return list(nwObjUids)

    def buildNwObjUidToIdMapFromApi(self, nwObjUids: List[str]):
        logger = getFwoLogger()
        nwObjUidToIdMap = {}

        if len(nwObjUids)>0:
            # TODO: remove active filter later
            buildQuery = """
                query getMapOfUid2Id($uids: [String!]!) {
                    object(where: {obj_uid: {_in: $uids}, removed: {_is_null: true}, active: {_eq: true}}) {
                        obj_id
                        obj_uid
                    }
                }
            """

            try:
                uidMapResult = self.ImportDetails.call(buildQuery, queryVariables={ 'uids': nwObjUids })
                if 'errors' in uidMapResult:
                    logger.exception(f"fwo_api:importNwObject - error in buildNwObjMemberUidToIdMap: {str(uidMapResult['errors'])}")
                    # TODO: add error to global import error counter
                else:
                    uidMap = uidMapResult['data']['object']
                    # now turn the list into a dict with key = uid
                    for obj in uidMap:
                        nwObjUidToIdMap.update({ obj['obj_uid']: obj['obj_id'] })
            except:
                logger.exception(f"failed to write new objects: {str(traceback.format_exc())}")
                # TODO: add error to global import error counter

        return nwObjUidToIdMap

    def buildUserObjUidToIdMapFromApi(self, userObjUids: List[str]):
        logger = getFwoLogger()
        userObjUidToIdMap = {}

        # TODO: impelment

        return userObjUidToIdMap


    def buildNwObjMemberUidToIdMap(self, newIds):
        uidList = []
        for addedObj in newIds:
            if addedObj['obj_member_refs'] is not None:
                for memberUid in addedObj['obj_member_refs'].split(fwo_const.list_delimiter):
                    uidList.append(memberUid)

        self.NwObjMemberUidToIdMap = self.buildNwObjUidToIdMapFromApi(uidList)


    def addNwObjGroupMemberships(self, newIds):
        newGroupMembers = []
        logger = getFwoLogger()
        errors = 0
        changes = 0
        newObjGrpIds = []

        self.buildNwObjMemberUidToIdMap(newIds)
        for addedObj in newIds:
            if addedObj['obj_member_refs'] is not None:
                for memberUId in addedObj['obj_member_refs'].split(fwo_const.list_delimiter):
                    memberId =self.NwObjMemberUidToIdMap[memberUId]
                    newGroupMembers.append({
                        "objgrp_id": addedObj['obj_id'],
                        "objgrp_member_id": memberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId })
                    

        if len(newGroupMembers)>0:

            import_mutation = """
                mutation insertNwGroup($nwGroups: [objgrp_insert_input!]!) {
                    insert_objgrp(objects: $nwGroups) {
                        affected_rows
                        returning { objgrp_id objgrp_member_id }
                    }
                }
            """

            # TODO: also add objgrp_flat here

            queryVariables = { 
                'nwGroups': newGroupMembers
            }
            try:
                import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables)
                if 'errors' in import_result:
                    logger.exception(f"fwo_api:importNwObject - error in addNwObjGroupMemberships: {str(import_result['errors'])}")
                    errors = 1
                else:
                    changes = int(import_result['data']['insert_objgrp']['affected_rows'])
                    newObjGrpIds = import_result['data']['insert_objgrp']['returning']
            except:
                logger.exception(f"failed to write new objects: {str(traceback.format_exc())}")
                errors = 1
            
        return errors, changes, newObjGrpIds

    def prepareNewSvcObjects(self, newSvcobjUids):
        newObjs = []
        for uid in newSvcobjUids:
            newObjs.append(ServiceObjectForImport(svcObject=self.NormalizedConfig.service_objects[uid],
                                        mgmId=self.ImportDetails.MgmDetails.Id, 
                                        importId=self.ImportDetails.ImportId, 
                                        colorId=self.lookupColor(self.NormalizedConfig.service_objects[uid].svc_color), 
                                        typId=self.lookupSvcType(self.NormalizedConfig.service_objects[uid].svc_typ),
                                        ).toDict())

            # newEnrichedSvcObj = self.NormalizedConfig.service_objects[uid].copy() # leave the original dict as is

            # color = newEnrichedSvcObj.pop('svc_color', None)     # get and remove
            # if color != None:
            #     color = self.lookupColor(color)
            # objtype = newEnrichedSvcObj.pop('svc_typ', None)     # get and remove
            # if objtype != None:
            #     objtype = self.lookupSvcType(objtype)
            # protoId = newEnrichedSvcObj.pop('ip_proto', None)     # get and remove
            # if protoId != None:
            #     protoId = self.lookupProtoNameToId(protoId)

            # rpcNr = newEnrichedSvcObj.pop('rpc_nr', None)     # get and remove

            # newEnrichedSvcObj.update({
            #         'mgm_id': self.ImportDetails.MgmDetails.Id,
            #         'svc_create': self.ImportDetails.ImportId,
            #         'svc_last_seen': self.ImportDetails.ImportId,   # could be left out
            #         'svc_color_id': color,
            #         'svc_typ_id': objtype,
            #         'ip_proto_id': protoId,
            #         'svc_rpcnr': rpcNr
            #     })
            # newObjs.append(newEnrichedSvcObj)
        return newObjs

    # # objects are not deleted but marked as removed
    # def markObjectsRemoved(self, removedNwObjectUids, removedSvcObjectUids):
    #     logger = getFwoLogger()
    #     errors = 0
    #     changes = 0
    #     removedNwObjIds = []
    #     removedNwSvcIds = []
        
    #     removeMutation = """
    #         mutation updateObjects($mgmId: Int!, $importId: bigint!, $removedNwObjectUids: [String!]!, $removedSvcObjectUids: [String!]!) {
    #             update_object(where: {mgm_id: {_eq: $mgmId}, obj_uid: {_in: $removedNwObjectUids}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
    #                 affected_rows
    #                 returning { obj_id }
    #             }
    #             update_service(where: {mgm_id: {_eq: $mgmId}, svc_uid: {_in: $removedSvcObjectUids}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
    #                 affected_rows
    #                 returning { svc_id }
    #             }
    #         }
    #     """
    #     queryVariables = {
    #         'mgmId': self.ImportDetails.MgmDetails.Id,
    #         'importId': self.ImportDetails.ImportId,
    #         'removedNwObjectUids': list(removedNwObjectUids), #convert set to list
    #         'removedSvcObjectUids': list(removedSvcObjectUids) #convert set to list
    #     }
        
    #     try:
    #         removeResult = self.ImportDetails.call(removeMutation, queryVariables=queryVariables)
    #         if 'errors' in removeResult:
    #             logger.exception(f"error while marking objects as removed: {str(removeResult['errors'])}")
    #             errors = 1
    #         else:
    #             changes = int(removeResult['data']['update_object']['affected_rows']) + \
    #                 int(removeResult['data']['update_service']['affected_rows'])
    #             removedNwObjIds = removeResult['data']['update_object']['returning']
    #             removedNwSvcIds = removeResult['data']['update_service']['returning']
    #     except:
    #         logger.exception(f"fatal error while marking objects as removed: {str(traceback.format_exc())}")
    #         errors = 1
        
    #     return errors, changes, removedNwObjIds, removedNwSvcIds

    # object refs are not deleted but marked as removed
    def markObjectRefsRemoved(self, removedNwObjectIds, removedSvcObjectIds):
        logger = getFwoLogger()
        errors = 0
        changes = 0
        removedNwObjIds = []
        removedNwSvcIds = []
        removeMutation = """
            mutation updateObjects($importId: bigint!, $removedNwObjectIds: [bigint!]!, $removedSvcObjectIds: [bigint!]!) {
                update_objgrp(where: {objgrp_member_id: {_in: $removedNwObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
                    affected_rows
                }
                update_svcgrp(where: {svcgrp_member_id: {_in: $removedSvcObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
                    affected_rows
                }
                update_objgrp_flat(where: {objgrp_flat_member_id: {_in: $removedNwObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
                    affected_rows
                }
                update_svcgrp_flat(where: {svcgrp_flat_member_id: {_in: $removedSvcObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
                    affected_rows
                }
                update_rule_from(where: {obj_id: {_in: $removedNwObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
                    affected_rows
                }
                update_rule_to(where: {obj_id: {_in: $removedNwObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
                    affected_rows
                }
                update_rule_service(where: {svc_id: {_in: $removedSvcObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
                    affected_rows
                }
                update_rule_nwobj_resolved(where: {obj_id: {_in: $removedNwObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId}) {
                    affected_rows
                }
                update_rule_svc_resolved(where: {svc_id: {_in: $removedSvcObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId}) {
                    affected_rows
                }
            }
        """

        plainRemovedNwObjIds = []
        for obj in removedNwObjectIds:
            plainRemovedNwObjIds.append(obj['obj_id'])
        plainRemovedSvcObjIds = []
        for obj in removedSvcObjectIds:
            plainRemovedSvcObjIds.append(obj['svc_id'])

        queryVariables = {
            'importId': self.ImportDetails.ImportId,
            'removedNwObjectIds': plainRemovedNwObjIds,
            'removedSvcObjectIds': plainRemovedSvcObjIds
        }
        
        try:
            removeResult = self.ImportDetails.call(removeMutation, queryVariables=queryVariables)
            if 'errors' in removeResult:
                logger.exception(f"error while marking objects as removed: {str(removeResult['errors'])}")
                errors = 1
            else:
                changes = int(removeResult['data']['update_objgrp']['affected_rows']) + \
                    int(removeResult['data']['update_svcgrp']['affected_rows']) + \
                    int(removeResult['data']['update_objgrp_flat']['affected_rows']) + \
                    int(removeResult['data']['update_svcgrp_flat']['affected_rows']) + \
                    int(removeResult['data']['update_rule_from']['affected_rows']) + \
                    int(removeResult['data']['update_rule_to']['affected_rows']) + \
                    int(removeResult['data']['update_rule_service']['affected_rows']) + \
                    int(removeResult['data']['update_rule_nwobj_resolved']['affected_rows']) + \
                    int(removeResult['data']['update_rule_svc_resolved']['affected_rows'])
                    
        except:
            logger.exception(f"fatal error while marking objects as removed: {str(traceback.format_exc())}")
            errors = 1
        
        return errors, changes, removedNwObjIds, removedNwSvcIds

    def lookupObjType(self, objTypeString):
        # TODO: might check for miss here as this is a mandatory field!
        return self.NetworkObjectTypeMap.get(objTypeString, None)

    def lookupSvcType(self, svcTypeString):
        # TODO: might check for miss here as this is a mandatory field!
        return self.ServiceObjectTypeMap.get(svcTypeString, None)

    def lookupObjIdToUidAndPolicyName(self, objId: int):
        return str(objId) # mock
        # CAST((COALESCE (rule.rule_ruleid, rule.rule_uid) || ', Rulebase: ' || device.local_rulebase_name) AS VARCHAR) AS unique_name,
        # return self.NetworkObjectIdMap.get(objId, None)

    def lookupColor(self, colorString):
        return self.ColorMap.get(colorString, None)

    def lookupProtoNameToId(self, protoString):
        if isinstance(protoString, int):
            # logger = getFwoLogger()
            # logger.warning(f"found protocol with an id as name: {str(protoString)}")
            return protoString  # already an int, do nothing
        else:
            if protoString == None:
                return None
            else:
                return self.ProtocolMap.get(protoString.lower(), None)

    def prepareChangelogObjects(self, nwObjIdsAdded, svcObjIdsAdded, nwObjIdsRemoved, svcObjIdsRemoved):
        """
            insert into stm_change_type (change_type_id,change_type_name) VALUES (1,'factory settings');
            insert into stm_change_type (change_type_id,change_type_name) VALUES (2,'initial import');
            insert into stm_change_type (change_type_id,change_type_name) VALUES (3,'in operation');
        """
        # TODO: deal with object changes where we need old and new obj id

        nwObjs = []
        svcObjs = []
        importTime = datetime.datetime.now().isoformat()
        changeTyp = 3  # standard
        if self.ImportDetails.IsFullImport or self.ImportDetails.IsClearingImport:
            changeTyp = 2   # to be ignored in change reports
        for obj in nwObjIdsAdded:
            uniqueName = self.lookupObjIdToUidAndPolicyName(obj['obj_id'])
            nwObjs.append({
                "new_obj_id": obj['obj_id'],
                "control_id": self.ImportDetails.ImportId,
                "change_action": "I",
                "mgm_id": self.ImportDetails.MgmDetails.Id,
                "change_type_id": changeTyp,
                # "security_relevant": secRelevant, # assuming everything is security relevant for now
                "change_time": importTime,
                "unique_name": uniqueName
            })
        return nwObjs, svcObjs

    def addChangelogObjects(self, nwObjIdsAdded, svcObjIdsAdded, nwObjIdsRemoved, svcObjIdsRemoved):
        logger = getFwoLogger()
        errors = 0

        nwObjsChanged, svcObjsChanged = self.prepareChangelogObjects(nwObjIdsAdded, svcObjIdsAdded, nwObjIdsRemoved, svcObjIdsRemoved)

        changelogMutation = """
            mutation updateObjChangelogs($nwObjChanges: [changelog_object_insert_input!]!, $svcObjChanges: [changelog_service_insert_input!]!) {
                insert_changelog_object(objects: $nwObjChanges) {
                    affected_rows
                }
                insert_changelog_service(objects: $svcObjChanges) {
                    affected_rows
                }
            }
        """
        queryVariables = {
            'nwObjChanges': nwObjsChanged, 
            'svcObjChanges': svcObjsChanged
        }

        if len(nwObjsChanged) + len(svcObjsChanged)>0:
            try:
                changelogResult = self.ImportDetails.call(changelogMutation, queryVariables=queryVariables)
                if 'errors' in changelogResult:
                    logger.exception(f"error while adding changelog entries for objects: {str(changelogResult['errors'])}")
                    errors = 1
            except:
                logger.exception(f"fatal error while adding changelog entries for objects: {str(traceback.format_exc())}")
                errors = 1
        
        return errors

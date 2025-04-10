from typing import Dict, List
import traceback
import time, datetime
import json

from fwo_log import getFwoLogger
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from model_controllers.fwconfig_import_base import FwConfigImportBase
from models.networkobject import NetworkObjectForImport
from models.serviceobject import ServiceObjectForImport
import fwo_const


# this class is used for importing a config into the FWO API
class FwConfigImportObject(FwConfigImportBase):

    # @root_validator(pre=True)
    # def custom_initialization(cls, values):
    #     values['NetworkObjectTypeMap'] = cls.GetNetworkObjTypeMap()
    #     values['ServiceObjectTypeMap'] = cls.GetServiceObjTypeMap()
    #     values['UserObjectTypeMap'] = cls.GetUserObjTypeMap()
    #     values['ProtocolMap'] = cls.GetProtocolMap()
    #     values['ColorMap'] = cls.GetColorMap()
    #     return values
    
    def __init__(self, importState: ImportStateController, config: FwConfigNormalized):
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

        for nwSvcUid in svcObjUidsInBoth:
            if prevConfig.service_objects[nwSvcUid] != self.NormalizedConfig.service_objects[nwSvcUid]:
                newSvcObjUids.append(nwSvcUid)
                deletedSvcObjUids.append(nwSvcUid)
        
        #TODO: calculate user diffs
        # deletedUserUids = list(prevConfig.users.keys() - self.NormalizedConfig.users.keys())


        # TODO: deal with object changes (e.g. group with added member)

        # add newly created objects
        errorCountUpdate, numberOfModifiedObjects, newNwObjIds, newNwSvcIds, removedNwObjIds, removedNwSvcIds = \
            self.updateObjectsViaApi(newNwobjUids, newSvcObjUids, deletedNwobjUids, deletedSvcObjUids)

        # initial mapping of object uids to ids. already updated for new objects
        #TODO: only fetch objects needed later. Esp for !isFullImport. but: newNwObjIds not enough!
        # -> newObjs + extract all objects from new/changed rules and groups, flatten them. Complete?
        self.uid2id_mapper.update_network_object_mapping()
        self.uid2id_mapper.update_service_object_mapping()
        self.uid2id_mapper.update_user_mapping()
        # important!: always update mapping whenever we update objects via API
        # if not, there will be inconsistencies in the database!


        # update group memberships
        self.addNwObjGroupMemberships(newNwObjIds)
        self.addNwSvcGroupMemberships(newNwSvcIds)
        # self.addUserObjGroupMemberships(newUserIds)

        # these objects have really been deleted so there should be no refs to them anywhere! verify this

        # update all references to objects marked as removed
        self.markObjectRefsRemoved(removedNwObjIds, removedNwSvcIds)

        # TODO: calculate user diffs
        # TODO: calculate zone diffs

        # TODO: write changes to changelog_xxx tables
        self.addChangelogObjects(newNwObjIds, newNwSvcIds, removedNwObjIds, removedNwSvcIds)

        # note changes:
        self.ImportDetails.Stats.NetworkObjectAddCount = len(newNwObjIds)
        self.ImportDetails.Stats.NetworkObjectDeleteCount = len(removedNwObjIds)
        self.ImportDetails.Stats.ServiceObjectAddCount = len(newNwSvcIds)
        self.ImportDetails.Stats.ServiceObjectDeleteCount = len(removedNwSvcIds)

        return

    def updateObjectsViaApi(self, newNwObjectUids, newSvcObjectUids, removedNwObjectUids, removedSvcObjectUids):
        # here we also mark old objects removed before adding the new versions
        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
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
                        obj_uid
                        obj_member_refs
                    }
                }
                insert_service(objects: $newSvcObjects) {
                    affected_rows
                    returning {
                        svc_id
                        svc_uid
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
        except Exception:
            logger.exception(f"failed to update objects: {str(traceback.format_exc())}")
            errors = 1
        return errors, changes, newNwObjIds, newNwSvcIds, removedNwObjIds, removedNwSvcIds
    
    def GetNetworkObjTypeMap(self):
        query = "query getNetworkObjTypeMap { stm_obj_typ { obj_typ_name obj_typ_id } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting stm_obj_typ")
            return {}
        
        map = {}
        for nwType in result['data']['stm_obj_typ']:
            map.update({nwType['obj_typ_name']: nwType['obj_typ_id']})
        return map

    def GetServiceObjTypeMap(self):
        query = "query getServiceObjTypeMap { stm_svc_typ { svc_typ_name svc_typ_id } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting stm_svc_typ")
            return {}
        
        map = {}
        for svcType in result['data']['stm_svc_typ']:
            map.update({svcType['svc_typ_name']: svcType['svc_typ_id']})
        return map

    def GetUserObjTypeMap(self):
        query = "query getUserObjTypeMap { stm_usr_typ { usr_typ_name usr_typ_id } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting stm_usr_typ")
            return {}
        
        map = {}
        for usrType in result['data']['stm_usr_typ']:
            map.update({usrType['usr_typ_name']: usrType['usr_typ_id']})
        return map

    def GetProtocolMap(self):
        query = "query getIpProtocols { stm_ip_proto { ip_proto_id ip_proto_name } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting stm_ip_proto")
            return {}
        
        map = {}
        for proto in result['data']['stm_ip_proto']:
            map.update({proto['ip_proto_name'].lower(): proto['ip_proto_id']})
        return map

    def GetColorMap(self):
        query = "query getColorMap { stm_color { color_name color_id } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting stm_color")
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


    def addNwObjGroupMemberships(self, newIds):
        newGroupMembers = []
        newGroupMemberFlats = []
        logger = getFwoLogger()
        errors = 0
        changes = 0
        newObjGrpIds = []

        for addedObj in newIds:
            if addedObj['obj_member_refs'] is not None:
                for memberUId in addedObj['obj_member_refs'].split(fwo_const.list_delimiter):
                    memberId = self.uid2id_mapper.get_network_object_id(memberUId)
                    newGroupMembers.append({
                        "objgrp_id": addedObj['obj_id'],
                        "objgrp_member_id": memberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId
                    })
                for flatMemberUid in self.group_flats_mapper.get_network_object_flats([addedObj['obj_uid']]):
                    flatMemberId = self.uid2id_mapper.get_network_object_id(flatMemberUid)
                    newGroupMemberFlats.append({
                        "objgrp_flat_id": addedObj['obj_id'],
                        "objgrp_flat_member_id": flatMemberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId
                    })

        if len(newGroupMembers)>0:

            import_mutation = """
                mutation insertNwGroup($nwGroups: [objgrp_insert_input!]!, $nwGroupFlats: [objgrp_flat_insert_input!]!) {
                    insert_objgrp(objects: $nwGroups) {
                        affected_rows
                        returning { objgrp_id objgrp_member_id
                        }
                    }
                    insert_objgrp_flat(objects: $nwGroupFlats) {
                        affected_rows
                        returning { objgrp_flat_id objgrp_flat_member_id }
                    }
                }
            """

            queryVariables = { 
                'nwGroups': newGroupMembers,
                'nwGroupFlats': newGroupMemberFlats
            }
            try:
                import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables)
                if 'errors' in import_result:
                    logger.exception(f"fwo_api:importNwObject - error in addNwObjGroupMemberships: {str(import_result['errors'])}")
                    errors = 1
                else:
                    changes = int(import_result['data']['insert_objgrp']['affected_rows'])
                    newObjGrpIds = import_result['data']['insert_objgrp']['returning']
            except Exception:
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

    def addNwSvcGroupMemberships(self, newIds):
        newGroupMembers = []
        newGroupMemberFlats = []
        logger = getFwoLogger()
        errors = 0
        changes = 0
        newSvcGrpIds = []

        for addedObj in newIds:
            if addedObj['svc_member_refs'] is not None:
                for memberUId in addedObj['svc_member_refs'].split(fwo_const.list_delimiter):
                    memberId = self.uid2id_mapper.get_service_object_id(memberUId)
                    newGroupMembers.append({
                        "svcgrp_id": addedObj['svc_id'],
                        "svcgrp_member_id": memberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId
                    })
                for flatMemberUid in self.group_flats_mapper.get_service_object_flats([addedObj['svc_uid']]):
                    flatMemberId = self.uid2id_mapper.get_service_object_id(flatMemberUid)
                    newGroupMemberFlats.append({
                        "svcgrp_flat_id": addedObj['svc_id'],
                        "svcgrp_flat_member_id": flatMemberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId
                    })

        if len(newGroupMembers)>0:

            import_mutation = """
                mutation insertSvcGroup($svcGroups: [svcgrp_insert_input!]!, $svcGroupFlats: [svcgrp_flat_insert_input!]!) {
                    insert_svcgrp(objects: $svcGroups) {
                        affected_rows
                        returning { svcgrp_id svcgrp_member_id
                        }
                    }
                    insert_svcgrp_flat(objects: $svcGroupFlats) {
                        affected_rows
                        returning { svcgrp_flat_id svcgrp_flat_member_id }
                    }
                }
            """

            queryVariables = { 
                'svcGroups': newGroupMembers,
                'svcGroupFlats': newGroupMemberFlats
            }
            try:
                import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables)
                if 'errors' in import_result:
                    logger.exception(f"fwo_api:importNwObject - error in addSvcObjGroupMemberships: {str(import_result['errors'])}")
                    errors = 1
                else:
                    changes = int(import_result['data']['insert_svcgrp']['affected_rows'])
                    newSvcGrpIds = import_result['data']['insert_svcgrp']['returning']
            except Exception:
                logger.exception(f"failed to write new objects: {str(traceback.format_exc())}")
                errors = 1
            
        return errors, changes, newSvcGrpIds


    def addUserGroupMemberships(self, newIds):
        newGroupMembers = []
        newGroupMemberFlats = []
        logger = getFwoLogger()
        errors = 0
        changes = 0
        newUserGrpIds = []

        for addedUser in newIds:
            if addedUser['user_member_refs'] is not None:
                for memberUId in addedUser['user_member_refs'].split(fwo_const.list_delimiter):
                    memberId = self.uid2id_mapper.get_user_id(memberUId)
                    newGroupMembers.append({
                        "usergrp_id": addedUser['user_id'],
                        "usergrp_member_id": memberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId
                    })
                for flatMemberUid in self.group_flats_mapper.get_user_flats([addedUser['user_uid']]):
                    flatMemberId = self.uid2id_mapper.get_user_id(flatMemberUid)
                    newGroupMemberFlats.append({
                        "usergrp_flat_id": addedUser['user_id'],
                        "usergrp_flat_member_id": flatMemberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId
                    })

        if len(newGroupMembers)>0:

            import_mutation = """
                mutation insertUserGroup($userGroups: [usergrp_insert_input!]!, $userGroupFlats: [usergrp_flat_insert_input!]!) {
                    insert_usergrp(objects: $userGroups) {
                        affected_rows
                        returning { usergrp_id usergrp_member_id
                        }
                    }
                    insert_usergrp_flat(objects: $userGroupFlats) {
                        affected_rows
                        returning { usergrp_flat_id usergrp_flat_member_id }
                    }
                }
            """

            queryVariables = { 
                'userGroups': newGroupMembers,
                'userGroupFlats': newGroupMemberFlats
            }
            try:
                import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables)
                if 'errors' in import_result:
                    logger.exception(f"fwo_api:importNwObject - error in addUserObjGroupMemberships: {str(import_result['errors'])}")
                    errors = 1
                else:
                    changes = int(import_result['data']['insert_usergrp']['affected_rows'])
                    newUserGrpIds = import_result['data']['insert_usergrp']['returning']
            except Exception:
                logger.exception(f"failed to write new objects: {str(traceback.format_exc())}")
                errors = 1

        return errors, changes, newUserGrpIds


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
    #     except Exception:
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
                    
        except Exception:
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
            except Exception:
                logger.exception(f"fatal error while adding changelog entries for objects: {str(traceback.format_exc())}")
                errors = 1
        
        return errors

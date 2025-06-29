from enum import Enum
from typing import Dict, List
import traceback
import time, datetime
import json

from fwo_log import ChangeLogger, getFwoLogger
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from models.networkobject import NetworkObjectForImport
from models.serviceobject import ServiceObjectForImport
import fwo_const
from services.service_provider import ServiceProvider
from services.enums import Services

class Type(Enum):
    NETWORK_OBJECT = "network_object"
    SERVICE_OBJECT = "service_object"
    USER = "user"

# this class is used for importing a config into the FWO API
class FwConfigImportObject():

    ImportDetails: ImportStateController
    NormalizedConfig: FwConfigNormalized
    
    def __init__(self):

        # Get state, config and services.

        service_provider = ServiceProvider()
        global_state = service_provider.get_service(Services.GLOBAL_STATE)
        self.ImportDetails = global_state.import_state
        self.NormalizedConfig = global_state.normalized_config
        self.group_flats_mapper = service_provider.get_service(Services.GROUP_FLATS_MAPPER)
        self.prev_group_flats_mapper = service_provider.get_service(Services.GROUP_FLATS_MAPPER)
        self.uid2id_mapper = service_provider.get_service(Services.UID2ID_MAPPER, self.ImportDetails.ImportId)




        # Create maps.
        
        self.NetworkObjectTypeMap = self.GetNetworkObjTypeMap()
        self.ServiceObjectTypeMap = self.GetServiceObjTypeMap()
        self.UserObjectTypeMap = self.GetUserObjTypeMap()
        self.ProtocolMap = self.GetProtocolMap()


    def updateObjectDiffs(self, prevConfig: FwConfigNormalized):

        # calculate network object diffs
        # here we are handling the previous config as a dict for a while
        # previousNwObjects = prevConfig.network_objects
        deletedNwobjUids = list(prevConfig.network_objects.keys() - self.NormalizedConfig.network_objects.keys())
        newNwobjUids = list(self.NormalizedConfig.network_objects.keys() - prevConfig.network_objects.keys())
        nwobjUidsInBoth = list(self.NormalizedConfig.network_objects.keys() & prevConfig.network_objects.keys())
        change_logger = ChangeLogger()

        # For correct changelog and stats.
        changed_nw_objs = []
        changed_svcs = []

        # decide if it is prudent to mix changed, deleted and added rules here:
        for nwObjUid in nwobjUidsInBoth:
            if self.NormalizedConfig.network_objects[nwObjUid] != prevConfig.network_objects[nwObjUid]:
                newNwobjUids.append(nwObjUid)
                deletedNwobjUids.append(nwObjUid)
                changed_nw_objs.append(nwObjUid)

        # calculate service object diffs
        deletedSvcObjUids = list(prevConfig.service_objects.keys() - self.NormalizedConfig.service_objects.keys())
        newSvcObjUids = list(self.NormalizedConfig.service_objects.keys() - prevConfig.service_objects.keys())
        svcObjUidsInBoth = list(self.NormalizedConfig.service_objects.keys() & prevConfig.service_objects.keys())

        for nwSvcUid in svcObjUidsInBoth:
            if self.NormalizedConfig.service_objects[nwSvcUid] != prevConfig.service_objects[nwSvcUid]:
                newSvcObjUids.append(nwSvcUid)
                deletedSvcObjUids.append(nwSvcUid)
                changed_svcs.append(nwSvcUid)
        
        # calculate user diffs
        deletedUserUids = list(prevConfig.users.keys() - self.NormalizedConfig.users.keys())
        newUserUids = list(self.NormalizedConfig.users.keys() - prevConfig.users.keys())
        userUidsInBoth = list(self.NormalizedConfig.users.keys() & prevConfig.users.keys())
        for userUid in userUidsInBoth:
            if self.NormalizedConfig.users[userUid] != prevConfig.users[userUid]:
                newUserUids.append(userUid)
                deletedUserUids.append(userUid)

        # initial mapping of object uids to ids. needs to be updated, if more objects are created in the db after this point
        #TODO: only fetch objects needed later. Esp for !isFullImport. but: newNwObjIds not enough!
        # -> newObjs + extract all objects from new/changed rules and groups, flatten them. Complete?
        self.uid2id_mapper.update_network_object_mapping()
        self.uid2id_mapper.update_service_object_mapping()
        self.uid2id_mapper.update_user_mapping()

        self.group_flats_mapper.init_config(self.NormalizedConfig)
        self.prev_group_flats_mapper.init_config(prevConfig)

        # need to do this first, since we need the old object IDs for the group memberships
        #TODO: computationally expensive? Even without changes, all group objects and their members are compared to the previous config.
        errors, changes, changedNwObjMembers, changedNwObjFlats = self.removeOutdatedMemberships(prevConfig, Type.NETWORK_OBJECT)
        errors, changes, changedSvcObjMembers, changedSvcObjFlats = self.removeOutdatedMemberships(prevConfig, Type.SERVICE_OBJECT)
        errors, changes, changedUserObjMembers, changedUserObjFlats = self.removeOutdatedMemberships(prevConfig, Type.USER)

        # add newly created objects
        errors, changes, newNwObjIds, newNwSvcIds, newUserIds, removedNwObjIds, removedNwSvcIds, removedUserIds =  \
            self.updateObjectsViaApi(newNwobjUids, newSvcObjUids, newUserUids, deletedNwobjUids, deletedSvcObjUids, deletedUserUids)
        
        self.uid2id_mapper.add_network_object_mappings(newNwObjIds)
        self.uid2id_mapper.add_service_object_mappings(newNwSvcIds)
        self.uid2id_mapper.add_user_mappings(newUserIds)

        # insert new and updated group memberships
        errors, changes = self.addGroupMemberships(prevConfig, Type.NETWORK_OBJECT, changedNwObjMembers, changedNwObjFlats)
        errors, changes = self.addGroupMemberships(prevConfig, Type.SERVICE_OBJECT, changedSvcObjMembers, changedSvcObjFlats)
        errors, changes = self.addGroupMemberships(prevConfig, Type.USER, changedUserObjMembers, changedUserObjFlats)

        # these objects have really been deleted so there should be no refs to them anywhere! verify this

        # update all references to objects marked as removed
        self.markObjectRefsRemoved(removedNwObjIds, removedNwSvcIds)

        # TODO: calculate user diffs
        # TODO: calculate zone diffs


        # Get Changed Ids.

        change_logger.create_change_id_maps(self.uid2id_mapper, changed_nw_objs, changed_svcs, removedNwObjIds, removedNwSvcIds)

        # Seperate changes from adds and removes for changelog and stats.

        newNwObjIds = [newNwObjId
            for newNwObjId in newNwObjIds
            if newNwObjId['obj_id'] not in list(change_logger.changed_object_id_map.values())       
        ]
        removedNwObjIds = [removedNwObjId
            for removedNwObjId in removedNwObjIds
            if removedNwObjId['obj_id'] not in list(change_logger.changed_object_id_map.keys())       
        ]
        newNwSvcIds = [newNwSvcId
            for newNwSvcId in newNwSvcIds
            if newNwSvcId['svc_id'] not in list(change_logger.changed_service_id_map.values())       
        ]
        removedNwSvcIds = [removedNwSvcId
            for removedNwSvcId in removedNwSvcIds
            if removedNwSvcId['svc_id'] not in list(change_logger.changed_service_id_map.keys())       
        ]

        # Write change logs to tables.
        
        self.addChangelogObjects(newNwObjIds, newNwSvcIds, removedNwObjIds, removedNwSvcIds)

        # note changes:
        self.ImportDetails.Stats.NetworkObjectAddCount = len(newNwObjIds)
        self.ImportDetails.Stats.NetworkObjectDeleteCount = len(removedNwObjIds)
        self.ImportDetails.Stats.NetworkObjectChangeCount = len(change_logger.changed_object_id_map.items())
        self.ImportDetails.Stats.ServiceObjectAddCount = len(newNwSvcIds)
        self.ImportDetails.Stats.ServiceObjectDeleteCount = len(removedNwSvcIds)
        self.ImportDetails.Stats.ServiceObjectChangeCount = len(change_logger.changed_service_id_map.items())

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

    def updateObjectsViaApi(self, newNwObjectUids, newSvcObjectUids, newUserUids, removedNwObjectUids, removedSvcObjectUids, removedUserUids):
        # here we also mark old objects removed before adding the new versions
        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
        errors = 0
        changes = 0
        newNwObjIds = []
        newNwSvcIds = []
        newUserIds = []
        removedNwObjIds = []
        removedNwSvcIds = []
        removedUserIds = []
        import_mutation = """mutation updateObjects($mgmId: Int!, $importId: bigint!, $removedNwObjectUids: [String!]!, $removedSvcObjectUids: [String!]!, $newNwObjects: [object_insert_input!]!, $newSvcObjects: [service_insert_input!]!) {
                update_object(where: {mgm_id: {_eq: $mgmId}, obj_uid: {_in: $removedNwObjectUids}, removed: {_is_null: true}},
                    _set: {
                        removed: $importId,
                        active: false
                    }
                ) {
                    affected_rows
                    returning {
                        obj_id
                        obj_uid
                        obj_typ_id
                    }
                }
                update_service(where: {mgm_id: {_eq: $mgmId}, svc_uid: {_in: $removedSvcObjectUids}, removed: {_is_null: true}},
                    _set: {
                        removed: $importId,
                        active: false
                    }
                ) {
                    affected_rows
                    returning {
                        svc_id
                        svc_uid
                    }
                }
                update_usr(where: {mgm_id: {_eq: $mgmId}, user_uid: {_in: $removedUserUids}, removed: {_is_null: true}},
                    _set: {
                        removed: $importId,
                        active: false
                    }
                ) {
                    affected_rows
                    returning {
                        user_id
                        user_uid
                    }
                }
                insert_object(objects: $newNwObjects) {
                    affected_rows
                    returning {
                        obj_id
                        obj_uid
                        obj_typ_id
                    }
                }
                insert_service(objects: $newSvcObjects) {
                    affected_rows
                    returning {
                        svc_id
                        svc_uid
                    }
                }
                insert_usr(objects: $newUsers) {
                    affected_rows
                    returning {
                        user_id
                        user_uid
                    }
                }
            }
        """
        queryVariables = {
            'mgmId': self.ImportDetails.MgmDetails.Id,
            'importId': self.ImportDetails.ImportId,
            'newNwObjects': self.prepareNewNwObjects(newNwObjectUids),
            'newSvcObjects': self.prepareNewSvcObjects(newSvcObjectUids),
            'newUsers': self.prepareNewUserObjects(newUserUids),
            'removedNwObjectUids': removedNwObjectUids,
            'removedSvcObjectUids': removedSvcObjectUids,
            'removedUserUids': removedUserUids
        }
        
        try:
            import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables, debug_level=self.ImportDetails.DebugLevel, analyze_payload=True)
            if 'errors' in import_result:
                logger.exception(f"fwo_api:importNwObject - error in updateObjectsViaApi: {str(import_result['errors'])}")
                errors = 1
            else:
                changes = int(import_result['data']['insert_object']['affected_rows']) + \
                    int(import_result['data']['insert_service']['affected_rows']) + \
                    int(import_result['data']['insert_usr']['affected_rows']) + \
                    int(import_result['data']['update_object']['affected_rows']) + \
                    int(import_result['data']['update_service']['affected_rows']) + \
                    int(import_result['data']['update_usr']['affected_rows'])
                newNwObjIds = import_result['data']['insert_object']['returning']
                newNwSvcIds = import_result['data']['insert_service']['returning']
                newUserIds = import_result['data']['insert_usr']['returning']
                removedNwObjIds = import_result['data']['update_object']['returning']
                removedNwSvcIds = import_result['data']['update_service']['returning']
                removedUserIds = import_result['data']['update_usr']['returning']
        except Exception:
            logger.exception(f"failed to update objects: {str(traceback.format_exc())}")
            errors = 1
        return errors, changes, newNwObjIds, newNwSvcIds, newUserIds, removedNwObjIds, removedNwSvcIds, removedUserIds
    

    def prepareNewNwObjects(self, newNwobjUids):
        newNwObjs = []
        for nwobjUid in newNwobjUids:
            newNwObj = NetworkObjectForImport(nwObject=self.NormalizedConfig.network_objects[nwobjUid],
                                                    mgmId=self.ImportDetails.MgmDetails.Id, 
                                                    importId=self.ImportDetails.ImportId, 
                                                    colorId=self.ImportDetails.lookupColorId(self.NormalizedConfig.network_objects[nwobjUid].obj_color), 
                                                    typId=self.lookupObjType(self.NormalizedConfig.network_objects[nwobjUid].obj_typ))
            newNwObjDict = newNwObj.toDict()
            newNwObjs.append(newNwObjDict)
        return newNwObjs


    def prepareNewSvcObjects(self, newSvcobjUids):
        newObjs = []
        for uid in newSvcobjUids:
            newObjs.append(ServiceObjectForImport(svcObject=self.NormalizedConfig.service_objects[uid],
                                        mgmId=self.ImportDetails.MgmDetails.Id, 
                                        importId=self.ImportDetails.ImportId, 
                                        colorId=self.ImportDetails.lookupColorId(self.NormalizedConfig.service_objects[uid].svc_color), 
                                        typId=self.lookupSvcType(self.NormalizedConfig.service_objects[uid].svc_typ),
                                        ).toDict())
        return newObjs
    
    def prepareNewUserObjects(self, newUserUids):
        newObjs = []
        for uid in newUserUids:
            newObjs.append({
                'user_uid': uid,
                'mgm_id': self.ImportDetails.MgmDetails.Id,
                'user_create': self.ImportDetails.ImportId,
                'user_last_seen': self.ImportDetails.ImportId,
                'user_typ_id': self.lookupUserType(self.NormalizedConfig.users[uid]['usr_typ']),
                'user_name': self.NormalizedConfig.users[uid]['user_name'],
            })
        return newObjs
    
    def get_config_objects(self, type: Type, prevConfig: FwConfigNormalized):
        if type == Type.NETWORK_OBJECT:
            return prevConfig.network_objects, self.NormalizedConfig.network_objects
        if type == Type.SERVICE_OBJECT:
            return prevConfig.service_objects, self.NormalizedConfig.service_objects
        if type == Type.USER:
            return prevConfig.users, self.NormalizedConfig.users

    def get_id(self, type, uid):
        if type == Type.NETWORK_OBJECT:
            return self.uid2id_mapper.get_network_object_id(uid)
        if type == Type.SERVICE_OBJECT:
            return self.uid2id_mapper.get_service_object_id(uid)
        return self.uid2id_mapper.get_user_id(uid)

    def get_refs(self, type, obj):
        if type == Type.NETWORK_OBJECT:
            return obj.obj_member_refs
        if type == Type.SERVICE_OBJECT:
            return obj.svc_member_refs
        return obj.get('user_member_refs', None)

    def get_flats(self, type, uid):
        if type == Type.NETWORK_OBJECT:
            return self.group_flats_mapper.get_network_object_flats([uid])
        if type == Type.SERVICE_OBJECT:
            return self.group_flats_mapper.get_service_object_flats([uid])
        return self.group_flats_mapper.get_user_object_flats([uid])
    
    def get_prefix(self, type: Type):
        if type == Type.NETWORK_OBJECT:
            return "objgrp"
        if type == Type.SERVICE_OBJECT:
            return "svcgrp"
        return "usrgrp"


    def removeOutdatedMemberships(self, prevConfig: FwConfigNormalized, type: Type):
        errors = 0
        changes = 0
        removedMembers = []
        changedMembers = []
        removedFlats = []
        changedFlats = []

        prev_config_objects, current_config_objects = self.get_config_objects(type, prevConfig)
        prefix = self.get_prefix(type)

        for uid in prev_config_objects.keys():
            refs = self.get_refs(type, prev_config_objects[uid])
            if refs is not None:
                id = self.get_id(type, uid)
                prevMemberUIds = refs.split(fwo_const.list_delimiter)
                prevFlatMemberUIds = self.get_flats(type, uid)
                memberUIds = []  # all members need to be removed if group deleted or changed
                flatMemberUIds = []
                if uid in current_config_objects:  # group not removed
                    if not current_config_objects[uid] != prev_config_objects[uid]:
                        # group not changed -> check for changes in members
                        memberUIds = self.get_refs(type, current_config_objects[uid]).split(fwo_const.list_delimiter)
                        flatMemberUIds = self.get_flats(type, uid)
                for prevMemberUId in prevMemberUIds:
                    removed = prevMemberUId not in memberUIds
                    if not removed:
                        if current_config_objects[prevMemberUId] != prev_config_objects[prevMemberUId]:
                            removed = True
                            changedMembers.append([uid, prevMemberUId])
                    if removed:
                        prevMemberId = self.get_id(type, prevMemberUId)
                        removedMembers.append({
                            "_and": [
                                {f"{prefix}_id": {"_eq": id}},
                                {f"{prefix}_member_id": {"_eq": prevMemberId}},
                            ]
                        })
                for prevFlatMemberUId in prevFlatMemberUIds:
                    removed = prevFlatMemberUId not in flatMemberUIds
                    if not removed:
                        if current_config_objects[prevFlatMemberUId] != prev_config_objects[prevFlatMemberUId]:
                            removed = True
                            changedFlats.append([uid, prevFlatMemberUId])
                    if removed:
                        prevFlatMemberId = self.get_id(type, prevFlatMemberUId)
                        removedFlats.append({
                            "_and": [
                                {f"{prefix}_flat_id": {"_eq": id}},
                                {f"{prefix}_flat_member_id": {"_eq": prevFlatMemberId}},
                            ]
                        })
        # remove outdated group memberships
        if len(removedMembers) > 0:
            import_mutation = f"""
                mutation removeOutdated{prefix.capitalize()}Memberships($importId: bigint!, $removedMembers: [{prefix}_bool_exp!]!, $removedFlats: [{prefix}_flat_bool_exp!]!) {{
                    update_{prefix}(where: {{_and: [{{_or: $removedMembers}}, {{removed: {{_is_null: true}}}}]}},
                        _set: {{
                            removed: $importId,
                            active: false
                        }}
                    ) {{
                        affected_rows
                    }}
                    update_{prefix}_flat(where: {{_and: [{{_or: $removedFlats}}, {{removed: {{_is_null: true}}}}]}},
                        _set: {{
                            removed: $importId,
                            active: false
                        }}
                    ) {{
                        affected_rows
                    }}
                }}
            """
            queryVariables = {
                'importId': self.ImportDetails.ImportId,
                'removedMembers': removedMembers,
                'removedFlats': removedFlats
            }
            try:
                import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables, analyze_payload=True)
                if 'errors' in import_result:
                    logger = getFwoLogger()
                    logger.exception(f"fwo_api:importNwObject - error in removeOutdated{prefix.capitalize()}Memberships: {str(import_result['errors'])}")
                else:
                    changes = int(import_result['data'][f'update_{prefix}']['affected_rows']) + \
                        int(import_result['data'][f'update_{prefix}_flat']['affected_rows'])
            except Exception:
                logger = getFwoLogger()
                logger.exception(f"failed to remove outdated group memberships for {type}: {str(traceback.format_exc())}")
                errors = 1

        return errors, changes, changedMembers, changedFlats


    def addGroupMemberships(self, prevConfig, type: Type, outdatedMembers, outdatedFlats):
        """
        This function is used to update group memberships for nwobjs, services or users in the database.
        It adds group memberships and flats for new and updated members.
        Args:
            prevConfig (FwConfigNormalized): The previous configuration.
            outdatedMembers (List[Tuple[string, string]]): List of tuples containing the group UIDs and member UIDs of outdated members.
            outdatedFlats (List[Tuple[string, string]]): List of tuples containing the group UIDs and flat member UIDs of outdated flats.
        """
        logger = getFwoLogger()
        errors = 0
        changes = 0
        newGroupMembers = []
        newGroupMemberFlats = []
        prev_config_objects, current_config_objects = self.get_config_objects(type, prevConfig)
        prefix = self.get_prefix(type)
        for uid in current_config_objects.keys():
            if self.get_refs(type, current_config_objects[uid]) is not None:
                memberUids = self.get_refs(type, current_config_objects[uid]).split(fwo_const.list_delimiter)
                if uid in prev_config_objects:
                    # group not added
                    if not current_config_objects[uid] != prev_config_objects[uid]:
                        # group not changed -> if exist, changed members are handled below
                        continue
                groupId = self.get_id(type, uid)
                for memberUId in memberUids:
                    memberId = self.get_id(type, memberUId)
                    newGroupMembers.append({
                        f"{prefix}_id": groupId,
                        f"{prefix}_member_id": memberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId # to be removed in the future
                    })
                flatMemberUids = self.get_flats(type, uid)
                for flatMemberUid in flatMemberUids:
                    flatMemberId = self.get_id(type, flatMemberUid)
                    newGroupMemberFlats.append({
                        f"{prefix}_flat_id": groupId,
                        f"{prefix}_flat_member_id": flatMemberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId # to be removed in the future
                    })
        for changedObj in outdatedMembers: # readd changed members
            groupId = self.get_id(type, changedObj[0])
            memberId = self.get_id(type, changedObj[1])
            newGroupMembers.append({
                f"{prefix}_id": groupId,
                f"{prefix}_member_id": memberId,
                "import_created": self.ImportDetails.ImportId,
                "import_last_seen": self.ImportDetails.ImportId # to be removed in the future
            })
        for changedObj in outdatedFlats: # readd changed flats
            groupId = self.get_id(type, changedObj[0])
            memberId = self.get_id(type, changedObj[1])
            newGroupMemberFlats.append({
                f"{prefix}_flat_id": groupId,
                f"{prefix}_flat_member_id": memberId,
                "import_created": self.ImportDetails.ImportId,
                "import_last_seen": self.ImportDetails.ImportId # to be removed in the future
            })
        if len(newGroupMembers) > 0:
            import_mutation = f"""
                mutation update{prefix.capitalize()}Groups($groups: [{prefix}_insert_input!]!, $groupFlats: [{prefix}_flat_insert_input!]!) {{
                    insert_{prefix}(objects: $groups) {{
                        affected_rows
                    }}
                    insert_{prefix}_flat(objects: $groupFlats) {{
                        affected_rows
                    }}
                }}
            """
            queryVariables = {
                'groups': newGroupMembers,
                'groupFlats': newGroupMemberFlats
            }
            try:
                import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables, analyze_payload=True)
                if 'errors' in import_result:
                    logger.exception(f"fwo_api:importNwObject - error in addGroupMemberships: {str(import_result['errors'])}")
                    errors = 1
                else:
                    changes = int(import_result['data'][f'insert_{prefix}']['affected_rows']) + \
                        int(import_result['data'][f'insert_{prefix}_flat']['affected_rows'])
            except Exception:
                logger.exception(f"failed to write new objects: {str(traceback.format_exc())}")
                errors = 1
            
        return errors, changes


    def markObjectRefsRemoved(self, removedNwObjectIds, removedSvcObjectIds):
        # object refs are not deleted but marked as removed
        logger = getFwoLogger()
        errors = 0
        changes = 0
        removedNwObjIds = []
        removedNwSvcIds = []
        removeMutation = """
            mutation updateObjects($importId: bigint!, $removedNwObjectIds: [bigint!]!, $removedSvcObjectIds: [bigint!]!) {
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
            removeResult = self.ImportDetails.call(removeMutation, queryVariables=queryVariables, analyze_payload=True)
            if 'errors' in removeResult:
                logger.exception(f"error while marking objects as removed: {str(removeResult['errors'])}")
                errors = 1
            else:
                changes = int(removeResult['data']['update_rule_from']['affected_rows']) + \
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
    
    def lookupUserType(self, userTypeString):
        return self.UserObjectTypeMap.get(userTypeString, None)

    def lookupObjIdToUidAndPolicyName(self, objId: int):
        return str(objId) # mock
        # CAST((COALESCE (rule.rule_ruleid, rule.rule_uid) || ', Rulebase: ' || device.local_rulebase_name) AS VARCHAR) AS unique_name,
        # return self.NetworkObjectIdMap.get(objId, None)

    def lookupSvcIdToUidAndPolicyName(self, svcId: int):
        return str(svcId) # mock

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
        change_logger = ChangeLogger()

        if self.ImportDetails.IsFullImport or self.ImportDetails.IsClearingImport:
            changeTyp = 2   # to be ignored in change reports
        
        # Write changelog for network objects.

        for nw_obj_id in [nw_obj_ids_added_item["obj_id"] for nw_obj_ids_added_item in nwObjIdsAdded]:
            nwObjs.append(change_logger.create_changelog_import_object("obj", self.ImportDetails, 'I', changeTyp, importTime, nw_obj_id))

        for nw_obj_id in [nw_obj_ids_removed_item["obj_id"] for nw_obj_ids_removed_item in nwObjIdsRemoved]:
            nwObjs.append(change_logger.create_changelog_import_object("obj", self.ImportDetails, 'D', changeTyp, importTime, nw_obj_id))

        for old_nw_obj_id, new_nw_obj_id in change_logger.changed_object_id_map.items():
            nwObjs.append(change_logger.create_changelog_import_object("obj", self.ImportDetails, 'C', changeTyp, importTime, new_nw_obj_id, old_nw_obj_id))

        # Write changelog for Services.

        for svc_id in [svc_ids_added_item["svc_id"] for svc_ids_added_item in svcObjIdsAdded]:
            svcObjs.append(change_logger.create_changelog_import_object("svc", self.ImportDetails, 'I', changeTyp, importTime, svc_id))

        for svc_id in [svc_ids_removed_item["svc_id"] for svc_ids_removed_item in svcObjIdsRemoved]:
            svcObjs.append(change_logger.create_changelog_import_object("svc", self.ImportDetails, 'D', changeTyp, importTime, svc_id))

        for old_svc_id, new_svc_id in change_logger.changed_service_id_map.items():
            svcObjs.append(change_logger.create_changelog_import_object("svc", self.ImportDetails, 'C', changeTyp, importTime, new_svc_id, old_svc_id))

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
                changelogResult = self.ImportDetails.call(changelogMutation, queryVariables=queryVariables, analyze_payload=True)
                if 'errors' in changelogResult:
                    logger.exception(f"error while adding changelog entries for objects: {str(changelogResult['errors'])}")
                    errors = 1
            except Exception:
                logger.exception(f"fatal error while adding changelog entries for objects: {str(traceback.format_exc())}")
                errors = 1
        
        return errors

from enum import Enum
import traceback
import datetime
import json
from typing import Any

from fwo_log import ChangeLogger, getFwoLogger
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from models.networkobject import NetworkObjectForImport
from models.fwconfigmanager import FwConfigManager
from models.serviceobject import ServiceObjectForImport
import fwo_const
from fwo_api_call import FwoApi
from fwo_exceptions import FwoDuplicateKeyViolation, FwoImporterError
from services.group_flats_mapper import GroupFlatsMapper
from services.uid2id_mapper import Uid2IdMapper
from services.service_provider import ServiceProvider
from services.enums import Services

class Type(Enum):
    NETWORK_OBJECT = "network_object"
    SERVICE_OBJECT = "service_object"
    USER = "user"

# this class is used for importing a config into the FWO API
class FwConfigImportObject():

    import_state: ImportStateController
    normalized_config: FwConfigNormalized
    global_normalized_config: FwConfigNormalized | None = None
    group_flats_mapper: GroupFlatsMapper
    prev_group_flats_mapper: GroupFlatsMapper
    uid2id_mapper: Uid2IdMapper
    
    def __init__(self):

        # Get state, config and services.

        service_provider = ServiceProvider()
        global_state = service_provider.get_service(Services.GLOBAL_STATE)
        self.import_state = global_state.import_state
        self.normalized_config = global_state.normalized_config
        self.global_normalized_config = global_state.global_normalized_config
        self.group_flats_mapper = service_provider.get_service(Services.GROUP_FLATS_MAPPER, self.import_state.ImportId)
        self.prev_group_flats_mapper = service_provider.get_service(Services.PREV_GROUP_FLATS_MAPPER, self.import_state.ImportId)
        self.uid2id_mapper = service_provider.get_service(Services.UID2ID_MAPPER, self.import_state.ImportId)

        # Create maps.
        
        self.NetworkObjectTypeMap = self.GetNetworkObjTypeMap()
        self.ServiceObjectTypeMap = self.GetServiceObjTypeMap()
        self.UserObjectTypeMap = self.GetUserObjTypeMap()
        self.ProtocolMap = self.GetProtocolMap()


    def updateObjectDiffs(self, prev_config: FwConfigNormalized, prev_global_config: FwConfigNormalized|None, single_manager: FwConfigManager):

        change_logger = ChangeLogger()
        # calculate network object diffs
        # here we are handling the previous config as a dict for a while
        # previousNwObjects = prevConfig.network_objects
        deletedNwobjUids: list[str] = list(prev_config.network_objects.keys() - self.normalized_config.network_objects.keys())
        newNwobjUids: list[str] = list(self.normalized_config.network_objects.keys() - prev_config.network_objects.keys())
        nwobjUidsInBoth: list[str] = list(self.normalized_config.network_objects.keys() & prev_config.network_objects.keys())

        # For correct changelog and stats.
        changed_nw_objs: list[str] = []
        changed_svcs: list[str] = []

        # decide if it is prudent to mix changed, deleted and added rules here:
        for nwObjUid in nwobjUidsInBoth:
            if self.normalized_config.network_objects[nwObjUid] != prev_config.network_objects[nwObjUid]:
                newNwobjUids.append(nwObjUid)
                deletedNwobjUids.append(nwObjUid)
                changed_nw_objs.append(nwObjUid)

        # calculate service object diffs
        deletedSvcObjUids: list[str] = list(prev_config.service_objects.keys() - self.normalized_config.service_objects.keys())
        newSvcObjUids: list[str] = list(self.normalized_config.service_objects.keys() - prev_config.service_objects.keys())
        svcObjUidsInBoth: list[str] = list(self.normalized_config.service_objects.keys() & prev_config.service_objects.keys())

        for nwSvcUid in svcObjUidsInBoth:
            if self.normalized_config.service_objects[nwSvcUid] != prev_config.service_objects[nwSvcUid]:
                newSvcObjUids.append(nwSvcUid)
                deletedSvcObjUids.append(nwSvcUid)
                changed_svcs.append(nwSvcUid)
        
        # calculate user diffs
        deletedUserUids: list[str] = list(prev_config.users.keys() - self.normalized_config.users.keys())
        newUserUids: list[str] = list(self.normalized_config.users.keys() - prev_config.users.keys())
        userUidsInBoth: list[str] = list(self.normalized_config.users.keys() & prev_config.users.keys())
        for userUid in userUidsInBoth:
            if self.normalized_config.users[userUid] != prev_config.users[userUid]:
                newUserUids.append(userUid)
                deletedUserUids.append(userUid)

        # initial mapping of object uids to ids. needs to be updated, if more objects are created in the db after this point
        #TODO: only fetch objects needed later. Esp for !isFullImport. but: newNwObjIds not enough!
        # -> newObjs + extract all objects from new/changed rules and groups, flatten them. Complete?
        self.uid2id_mapper.update_network_object_mapping(is_global=single_manager.IsSuperManager)
        self.uid2id_mapper.update_service_object_mapping(is_global=single_manager.IsSuperManager)
        self.uid2id_mapper.update_user_mapping(is_global=single_manager.IsSuperManager)
        self.uid2id_mapper.update_zone_mapping(is_global=single_manager.IsSuperManager)

        self.group_flats_mapper.init_config(self.normalized_config, self.global_normalized_config)
        self.prev_group_flats_mapper.init_config(prev_config, prev_global_config)

        # need to do this first, since we need the old object IDs for the group memberships
        #TODO: computationally expensive? Even without changes, all group objects and their members are compared to the previous config.
        self.remove_outdated_memberships(prev_config, Type.NETWORK_OBJECT)
        self.remove_outdated_memberships(prev_config, Type.SERVICE_OBJECT)
        self.remove_outdated_memberships(prev_config, Type.USER)

        # calculate zone object diffs
        deleted_zone_names: list[str] = list(prev_config.zone_objects.keys() - self.normalized_config.zone_objects.keys())
        new_zone_names: list[str] = list(self.normalized_config.zone_objects.keys() - prev_config.zone_objects.keys())
        zone_names_in_both: list[str] = list(self.normalized_config.zone_objects.keys() & prev_config.zone_objects.keys())
        changed_zones: list[str] = []

        for zone_name in zone_names_in_both:
            if self.normalized_config.zone_objects[zone_name] != prev_config.zone_objects[zone_name]:
                new_zone_names.append(zone_name)
                deleted_zone_names.append(zone_name)
                changed_zones.append(zone_name)

        # add newly created objects
        newNwObjIds, newNwSvcIds, newUserIds, new_zone_ids, removedNwObjIds, removedNwSvcIds, _, _ =  \
            self.updateObjectsViaApi(single_manager, newNwobjUids, newSvcObjUids, newUserUids, new_zone_names, deletedNwobjUids, deletedSvcObjUids, deletedUserUids, deleted_zone_names)
        
        self.uid2id_mapper.add_network_object_mappings(newNwObjIds, is_global=single_manager.IsSuperManager)
        self.uid2id_mapper.add_service_object_mappings(newNwSvcIds, is_global=single_manager.IsSuperManager)
        self.uid2id_mapper.add_user_mappings(newUserIds, is_global=single_manager.IsSuperManager)
        self.uid2id_mapper.add_zone_mappings(new_zone_ids, is_global=single_manager.IsSuperManager)

        # insert new and updated group memberships
        self.addGroupMemberships(prev_config, Type.NETWORK_OBJECT)
        self.addGroupMemberships(prev_config, Type.SERVICE_OBJECT)
        self.addGroupMemberships(prev_config, Type.USER) 

        # these objects have really been deleted so there should be no refs to them anywhere! verify this

        # TODO: calculate user diffs
        # TODO: write changelog for zones
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
        self.import_state.Stats.NetworkObjectAddCount = len(newNwObjIds)
        self.import_state.Stats.NetworkObjectDeleteCount = len(removedNwObjIds)
        self.import_state.Stats.NetworkObjectChangeCount = len(change_logger.changed_object_id_map.items())
        self.import_state.Stats.ServiceObjectAddCount = len(newNwSvcIds)
        self.import_state.Stats.ServiceObjectDeleteCount = len(removedNwSvcIds)
        self.import_state.Stats.ServiceObjectChangeCount = len(change_logger.changed_service_id_map.items())


    def GetNetworkObjTypeMap(self) -> dict[str, int]:
        query = "query getNetworkObjTypeMap { stm_obj_typ { obj_typ_name obj_typ_id } }"
        try:
            result = self.import_state.api_call.call(query=query, query_variables={})
        except Exception as e:
            logger = getFwoLogger()
            logger.error(f"Error while getting stm_obj_typ: str{e}")
            return {}
        
        map: dict[str, Any] = {}
        for nwType in result['data']['stm_obj_typ']:
            map.update({nwType['obj_typ_name']: nwType['obj_typ_id']})
        return map

    def GetServiceObjTypeMap(self) -> dict[str, int]:
        query = "query getServiceObjTypeMap { stm_svc_typ { svc_typ_name svc_typ_id } }"
        try:
            result = self.import_state.api_call.call(query=query, query_variables={})
        except Exception as e:
            logger = getFwoLogger()
            logger.error(f"Error while getting stm_svc_typ: {str(e)}")
            return {}
        
        map: dict[str, Any] = {}
        for svcType in result['data']['stm_svc_typ']:
            map.update({svcType['svc_typ_name']: svcType['svc_typ_id']})
        return map

    def GetUserObjTypeMap(self) -> dict[str, int]:
        query = "query getUserObjTypeMap { stm_usr_typ { usr_typ_name usr_typ_id } }"
        try:
            result = self.import_state.api_call.call(query=query, query_variables={})
        except Exception as e:
            logger = getFwoLogger()
            logger.error(f"Error while getting stm_usr_typ: {str(e)}")
            return {}
        
        map: dict[str, Any] = {}
        for usrType in result['data']['stm_usr_typ']:
            map.update({usrType['usr_typ_name']: usrType['usr_typ_id']})
        return map

    def GetProtocolMap(self) -> dict[str, int]:
        query = "query getIpProtocols { stm_ip_proto { ip_proto_id ip_proto_name } }"
        try:
            result = self.import_state.api_call.call(query=query, query_variables={})
        except Exception as e:
            logger = getFwoLogger()
            logger.error(f"Error while getting stm_ip_proto: {str(e)}")
            return {}
        
        map: dict[str, Any] = {}
        for proto in result['data']['stm_ip_proto']:
            map.update({proto['ip_proto_name'].lower(): proto['ip_proto_id']})
        return map

    def updateObjectsViaApi(self, single_manager: FwConfigManager, newNwObjectUids: list[str], newSvcObjectUids: list[str], newUserUids: list[str], new_zone_names: list[str], removedNwObjectUids: list[str], removedSvcObjectUids: list[str], removedUserUids: list[str], removed_zone_names: list[str]):
        # here we also mark old objects removed before adding the new versions
        logger = getFwoLogger(debug_level=self.import_state.DebugLevel)
        newNwObjIds = []
        newNwSvcIds = []
        newUserIds = []
        new_zone_ids = []
        removedNwObjIds = []
        removedNwSvcIds = []
        removedUserIds = []
        removed_zone_ids = []
        this_managements_id = self.import_state.lookupManagementId(single_manager.ManagerUid)
        if this_managements_id is None:
            raise FwoImporterError(f"failed to update objects in updateObjectsViaApi: no management id found for manager uid '{single_manager.ManagerUid}'")
        import_mutation = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "allObjects/upsertObjects.graphql"])
        query_variables: dict[str, Any] = {
            'mgmId': this_managements_id,
            'importId': self.import_state.ImportId,
            'newNwObjects': self.prepareNewNwObjects(newNwObjectUids, this_managements_id),
            'newSvcObjects': self.prepareNewSvcObjects(newSvcObjectUids, this_managements_id),
            'newUsers': self.prepareNewUserObjects(newUserUids, this_managements_id),
            'newZones': self.prepare_new_zones(new_zone_names, this_managements_id),
            'removedNwObjectUids': removedNwObjectUids,
            'removedSvcObjectUids': removedSvcObjectUids,
            'removedUserUids': removedUserUids,
            'removedZoneUids': removed_zone_names
        }

        if self.import_state.DebugLevel>8:
            logger.debug(f"fwo_api:importNwObject - import_mutation: {import_mutation}")
            # Save the query variables to a file for debugging purposes.
            json.dump(query_variables, open(f"/usr/local/fworch/tmp/import/mgm_id_{self.import_state.MgmDetails.Id}_query_variables.json", "w"), indent=4)

        try:
            import_result = self.import_state.api_call.call(import_mutation, query_variables=query_variables, debug_level=self.import_state.DebugLevel, analyze_payload=True)
            if 'errors' in import_result:
                raise FwoImporterError(f"failed to update objects in updateObjectsViaApi: {str(import_result['errors'])}")
            else:
                _ = int(import_result['data']['insert_object']['affected_rows']) + \
                    int(import_result['data']['insert_service']['affected_rows']) + \
                    int(import_result['data']['insert_usr']['affected_rows']) + \
                    int(import_result['data']['update_object']['affected_rows']) + \
                    int(import_result['data']['update_service']['affected_rows']) + \
                    int(import_result['data']['update_usr']['affected_rows']) +\
                    int(import_result['data']['update_zone']['affected_rows'])
                newNwObjIds = import_result['data']['insert_object']['returning']
                newNwSvcIds = import_result['data']['insert_service']['returning']
                newUserIds = import_result['data']['insert_usr']['returning']
                new_zone_ids = import_result['data']['insert_zone']['returning']
                removedNwObjIds = import_result['data']['update_object']['returning']
                removedNwSvcIds = import_result['data']['update_service']['returning']
                removedUserIds = import_result['data']['update_usr']['returning']
                removed_zone_ids = import_result['data']['update_zone']['returning']
        except Exception:
            # logger.exception(f"failed to update objects: {str(traceback.format_exc())}")
            raise FwoImporterError(f"failed to update objects: {str(traceback.format_exc())}")
        return newNwObjIds, newNwSvcIds, newUserIds, new_zone_ids, removedNwObjIds, removedNwSvcIds, removedUserIds, removed_zone_ids
    

    def prepareNewNwObjects(self, newNwobjUids: list[str], mgm_id: int) -> list[dict[str, Any]]:
        newNwObjs: list[dict[str, Any]] = []
        for nwobjUid in newNwobjUids:
            newNwObj = NetworkObjectForImport(nwObject=self.normalized_config.network_objects[nwobjUid],
                                                    mgmId=mgm_id, 
                                                    importId=self.import_state.ImportId, 
                                                    colorId=self.import_state.lookupColorId(self.normalized_config.network_objects[nwobjUid].obj_color), 
                                                    typId=self.lookupObjType(self.normalized_config.network_objects[nwobjUid].obj_typ))
            newNwObjDict = newNwObj.toDict()
            newNwObjs.append(newNwObjDict)
        return newNwObjs


    def prepareNewSvcObjects(self, newSvcobjUids: list[str], mgm_id: int) -> list[dict[str, Any]]:
        newObjs: list[dict[str, Any]] = []
        for uid in newSvcobjUids:
            newObjs.append(ServiceObjectForImport(svcObject=self.normalized_config.service_objects[uid],
                                        mgmId=mgm_id, 
                                        importId=self.import_state.ImportId, 
                                        colorId=self.import_state.lookupColorId(self.normalized_config.service_objects[uid].svc_color), 
                                        typId=self.lookupSvcType(self.normalized_config.service_objects[uid].svc_typ),
                                        ).toDict())
        return newObjs

    def prepareNewUserObjects(self, newUserUids: list[str], mgm_id: int) -> list[dict[str, Any]]:
        newObjs: list[dict[str, Any]] = []
        for uid in newUserUids:
            newObjs.append({
                'user_uid': uid,
                'mgm_id': mgm_id,
                'user_create': self.import_state.ImportId,
                'user_last_seen': self.import_state.ImportId,
                'usr_typ_id': self.lookupUserType(self.normalized_config.users[uid]['user_typ']),
                'user_name': self.normalized_config.users[uid]['user_name'],
            })
        return newObjs


    def prepare_new_zones(self, new_zone_names: list[str], mgm_id: int) -> list[dict[str, Any]]:
        new_objects: list[dict[str, Any]] = []
        for uid in new_zone_names:
            new_objects.append({
                'mgm_id': mgm_id,
                'zone_create': self.import_state.ImportId,
                'zone_last_seen': self.import_state.ImportId,
                'zone_name': self.normalized_config.zone_objects[uid]['zone_name'],
            })
        return new_objects
    
 
    def get_config_objects(self, type: Type, prevConfig: FwConfigNormalized):
        if type == Type.NETWORK_OBJECT:
            return prevConfig.network_objects, self.normalized_config.network_objects
        if type == Type.SERVICE_OBJECT:
            return prevConfig.service_objects, self.normalized_config.service_objects
        if type == Type.USER:
            return prevConfig.users, self.normalized_config.users

    def get_id(self, type: Type, uid: str, before_update: bool = False) -> int | None:
        if type == Type.NETWORK_OBJECT:
            return self.uid2id_mapper.get_network_object_id(uid, before_update)
        if type == Type.SERVICE_OBJECT:
            return self.uid2id_mapper.get_service_object_id(uid, before_update)
        return self.uid2id_mapper.get_user_id(uid, before_update)

    def get_local_id(self, type: Type, uid: str, before_update: bool = False) -> int | None:
        if type == Type.NETWORK_OBJECT:
            return self.uid2id_mapper.get_network_object_id(uid, before_update, local_only=True)
        if type == Type.SERVICE_OBJECT:
            return self.uid2id_mapper.get_service_object_id(uid, before_update, local_only=True)
        return self.uid2id_mapper.get_user_id(uid, before_update, local_only=True)

    def is_group(self, type: Type, obj: Any) -> bool:
        if type == Type.NETWORK_OBJECT:
            return obj.obj_typ == "group"
        if type == Type.SERVICE_OBJECT:
            return obj.svc_typ == "group"
        if type == Type.USER:
            return obj.get('user_typ', None) == "group"


    def get_refs(self, type: Type, obj: Any) -> str | None:
        if type == Type.NETWORK_OBJECT:
            return obj.obj_member_refs
        if type == Type.SERVICE_OBJECT:
            return obj.svc_member_refs
        return obj.get('user_member_refs', None)

    def get_members(self, type: Type, refs: str | None) -> list[str]:
        if type == Type.NETWORK_OBJECT:
            return [member.split(fwo_const.user_delimiter)[0] for member in refs.split(fwo_const.list_delimiter) if member] if refs else []
        return refs.split(fwo_const.list_delimiter) if refs else []

    def get_flats(self, type: Type, uid: str) -> list[str]:
        if type == Type.NETWORK_OBJECT:
            return self.group_flats_mapper.get_network_object_flats([uid])
        if type == Type.SERVICE_OBJECT:
            return self.group_flats_mapper.get_service_object_flats([uid])
        return self.group_flats_mapper.get_user_flats([uid])

    def get_prev_flats(self, type: Type, uid: str) -> list[str]:
        if type == Type.NETWORK_OBJECT:
            return self.prev_group_flats_mapper.get_network_object_flats([uid])
        if type == Type.SERVICE_OBJECT:
            return self.prev_group_flats_mapper.get_service_object_flats([uid])
        return self.prev_group_flats_mapper.get_user_flats([uid])
    
    def get_prefix(self, type: Type):
        if type == Type.NETWORK_OBJECT:
            return "objgrp"
        if type == Type.SERVICE_OBJECT:
            return "svcgrp"
        return "usrgrp"


    def remove_outdated_memberships(self, prev_config: FwConfigNormalized, type: Type):
        errors = 0
        changes = 0
        removed_members: list[dict[str, Any]] = []
        removed_flats: list[dict[str, Any]] = []

        prev_config_objects, current_config_objects = self.get_config_objects(type, prev_config)
        prefix = self.get_prefix(type)

        for uid in prev_config_objects.keys():
            self.find_removed_objects(current_config_objects, prev_config_objects, removed_members, removed_flats, prefix, uid, type)
        # remove outdated group memberships
        if len(removed_members) == 0:
            return errors, changes

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
        query_variables: dict[str, Any] = {
            'importId': self.import_state.ImportId,
            'removedMembers': removed_members,
            'removedFlats': removed_flats
        }
        try:
            import_result = self.import_state.api_call.call(import_mutation, query_variables, analyze_payload=True)
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

        return errors, changes
    

    def find_removed_objects(self, current_config_objects: dict[str, Any], prev_config_objects: dict[str, Any], removed_members: list[dict[str, Any]], removed_flats: list[dict[str, Any]],
                             prefix: str, uid: str, type: Type) -> None:
        if not self.is_group(type, prev_config_objects[uid]):
            return
        db_id = self.get_id(type, uid, before_update=True)
        prev_member_uids = self.get_members(type, self.get_refs(type, prev_config_objects[uid]))
        prev_flat_member_uids = self.get_prev_flats(type, uid)
        member_uids = []  # all members need to be removed if group deleted or changed
        flat_member_uids = []
        # group not removed and group not changed -> check for changes in members
        if uid in current_config_objects and current_config_objects[uid] == prev_config_objects[uid]:
            member_uids = self.get_members(type, self.get_refs(type, current_config_objects[uid]))
            flat_member_uids = self.get_flats(type, uid)
        for prev_member_uid in prev_member_uids:
            if prev_member_uid in member_uids and current_config_objects[prev_member_uid] == prev_config_objects[prev_member_uid]:
                continue # member was not removed or changed
            prev_member_id = self.get_id(type, prev_member_uid, before_update=True)
            removed_members.append({
                "_and": [
                    {f"{prefix}_id": {"_eq": db_id}},
                    {f"{prefix}_member_id": {"_eq": prev_member_id}},
                ]
            })
        for prev_flat_member_uid in prev_flat_member_uids:
            if prev_flat_member_uid in flat_member_uids and current_config_objects[prev_flat_member_uid] == prev_config_objects[prev_flat_member_uid]:
                continue # flat member was not removed or changed
            prev_flat_member_id = self.get_id(type, prev_flat_member_uid, before_update=True)
            removed_flats.append({
                "_and": [
                    {f"{prefix}_flat_id": {"_eq": db_id}},
                    {f"{prefix}_flat_member_id": {"_eq": prev_flat_member_id}},
                ]
            })


    def addGroupMemberships(self, prev_config: FwConfigNormalized, obj_type: Type) -> tuple[int, int]:
        """
        This function is used to update group memberships for nwobjs, services or users in the database.
        It adds group memberships and flats for new and updated members.
        Args:
            prev_config (FwConfigNormalized): The previous normalized config.
        """
        errors = 0
        new_group_members: list[dict[str, Any]] = []
        new_group_member_flats: list[dict[str, Any]] = []
        prev_config_objects, current_config_objects = self.get_config_objects(obj_type, prev_config)
        prefix = self.get_prefix(obj_type)
        for uid in current_config_objects.keys():
            if not self.is_group(obj_type, current_config_objects[uid]):
                continue
            member_uids = self.get_members(obj_type, self.get_refs(obj_type, current_config_objects[uid]))
            prev_member_uids = []  # all members need to be added if group added or changed
            prev_flat_member_uids = []
            if uid in prev_config_objects:
                # group not added
                if current_config_objects[uid] == prev_config_objects[uid]:
                    # group not changed -> check for changes in members
                    prev_member_uids = self.get_members(obj_type, self.get_refs(obj_type, prev_config_objects[uid]))
                    prev_flat_member_uids = self.get_prev_flats(obj_type, uid)

            group_id = self.get_id(obj_type, uid)
            if group_id is None:
                logger = getFwoLogger()
                logger.error(f"failed to add group memberships: no id found for group uid '{uid}'")
                continue
            self.collect_group_members(group_id, current_config_objects, new_group_members, member_uids, obj_type, prefix, prev_member_uids, prev_config_objects)
            flat_member_uids = self.get_flats(obj_type, uid)
            self.collect_flat_group_members(group_id, current_config_objects, new_group_member_flats, flat_member_uids, obj_type, prefix, prev_flat_member_uids, prev_config_objects)

        if len(new_group_members)==0:
            return errors, 0
        
        return self.write_member_updates(new_group_members, new_group_member_flats, prefix, errors)


    def collect_flat_group_members(self, group_id: int, current_config_objects: dict[str, Any], new_group_member_flats: list[dict[str, Any]], flat_member_uids: list[str], obj_type: Type, prefix: str, prev_flat_member_uids: list[str], prev_config_objects: dict[str, Any]):
        for flat_member_uid in flat_member_uids:
            if flat_member_uid in prev_flat_member_uids and prev_config_objects[flat_member_uid] == current_config_objects[flat_member_uid]:
                continue # flat member was not added or changed
            flat_member_id = self.get_id(obj_type, flat_member_uid)
            new_group_member_flats.append({
                f"{prefix}_flat_id": group_id,
                f"{prefix}_flat_member_id": flat_member_id,
                "import_created": self.import_state.ImportId,
                "import_last_seen": self.import_state.ImportId # to be removed in the future
            })


    def collect_group_members(self, group_id: int, current_config_objects: dict[str, Any], new_group_members: list[dict[str, Any]], member_uids: list[str], obj_type: Type, prefix: str, prev_member_uids: list[str], prev_config_objects: dict[str, Any]):
        for member_uid in member_uids:
            if member_uid in prev_member_uids and prev_config_objects[member_uid] == current_config_objects[member_uid]:
                continue # member was not added or changed
            member_id = self.get_id(obj_type, member_uid)
            new_group_members.append({
                f"{prefix}_id": group_id,
                f"{prefix}_member_id": member_id,
                "import_created": self.import_state.ImportId,
                "import_last_seen": self.import_state.ImportId # to be removed in the future
            })


    def write_member_updates(self, new_group_members: list[dict[str, Any]], new_group_member_flats: list[dict[str, Any]], prefix: str, errors: int) -> tuple[int, int]:
        logger = getFwoLogger()
        changes = 0
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
        query_variables = {
            'groups': new_group_members,
            'groupFlats': new_group_member_flats
        }
        try:
            import_result = self.import_state.api_call.call(import_mutation, query_variables=query_variables, analyze_payload=True)
            if 'errors' in import_result:
                logger.exception(f"fwo_api:addGroupMemberships: {str(import_result['errors'])}")
                errors = 1
                if 'duplicate' in import_result['errors']:
                    raise FwoDuplicateKeyViolation(str(import_result['errors']))
                else:
                    raise FwoImporterError(str(import_result['errors']))
            else:
                changes = int(import_result['data'][f'insert_{prefix}']['affected_rows']) + \
                    int(import_result['data'][f'insert_{prefix}_flat']['affected_rows'])
        except Exception:
            logger.exception(f"failed to write new objects: {str(traceback.format_exc())}")
            raise
        
        return errors, changes


    def lookupObjType(self, objTypeString: str) -> int:
        # TODO: might check for miss here as this is a mandatory field!
        return self.NetworkObjectTypeMap.get(objTypeString, -1)

    def lookupSvcType(self, svcTypeString: str) -> int:
        # TODO: might check for miss here as this is a mandatory field!
        return self.ServiceObjectTypeMap.get(svcTypeString, -1)

    def lookupUserType(self, userTypeString: str) -> int:
        return self.UserObjectTypeMap.get(userTypeString, -1)

    def lookupObjIdToUidAndPolicyName(self, objId: int) -> str:
        return str(objId) # mock
        # CAST((COALESCE (rule.rule_ruleid, rule.rule_uid) || ', Rulebase: ' || device.local_rulebase_name) AS VARCHAR) AS unique_name,
        # return self.NetworkObjectIdMap.get(objId, None)

    def lookupSvcIdToUidAndPolicyName(self, svcId: int):
        return str(svcId) # mock

    def lookupProtoNameToId(self, protoString: str | int) -> int | None:
        if isinstance(protoString, int):
            # logger = getFwoLogger()
            # logger.warning(f"found protocol with an id as name: {str(protoString)}")
            return protoString  # already an int, do nothing
        else:
            return self.ProtocolMap.get(protoString.lower(), None)

    def prepareChangelogObjects(self, nwObjIdsAdded: list[dict[str, int]], svcObjIdsAdded: list[dict[str, int]], nwObjIdsRemoved: list[dict[str, int]], svcObjIdsRemoved: list[dict[str, int]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
        """
            insert into stm_change_type (change_type_id,change_type_name) VALUES (1,'factory settings');
            insert into stm_change_type (change_type_id,change_type_name) VALUES (2,'initial import');
            insert into stm_change_type (change_type_id,change_type_name) VALUES (3,'in operation');
        """
        # TODO: deal with object changes where we need old and new obj id

        nwObjs: list[dict[str, Any]] = []
        svcObjs: list[dict[str, Any]] = []
        importTime = datetime.datetime.now().isoformat()
        changeTyp = 3  # standard
        change_logger = ChangeLogger()

        if self.import_state.IsFullImport or self.import_state.IsClearingImport:
            changeTyp = 2   # to be ignored in change reports
        
        # Write changelog for network objects.

        for nw_obj_id in [nw_obj_ids_added_item["obj_id"] for nw_obj_ids_added_item in nwObjIdsAdded]:
            nwObjs.append(change_logger.create_changelog_import_object("obj", self.import_state, 'I', changeTyp, importTime, nw_obj_id))

        for nw_obj_id in [nw_obj_ids_removed_item["obj_id"] for nw_obj_ids_removed_item in nwObjIdsRemoved]:
            nwObjs.append(change_logger.create_changelog_import_object("obj", self.import_state, 'D', changeTyp, importTime, nw_obj_id))

        for old_nw_obj_id, new_nw_obj_id in change_logger.changed_object_id_map.items():
            nwObjs.append(change_logger.create_changelog_import_object("obj", self.import_state, 'C', changeTyp, importTime, new_nw_obj_id, old_nw_obj_id))

        # Write changelog for Services.

        for svc_id in [svc_ids_added_item["svc_id"] for svc_ids_added_item in svcObjIdsAdded]:
            svcObjs.append(change_logger.create_changelog_import_object("svc", self.import_state, 'I', changeTyp, importTime, svc_id))

        for svc_id in [svc_ids_removed_item["svc_id"] for svc_ids_removed_item in svcObjIdsRemoved]:
            svcObjs.append(change_logger.create_changelog_import_object("svc", self.import_state, 'D', changeTyp, importTime, svc_id))

        for old_svc_id, new_svc_id in change_logger.changed_service_id_map.items():
            svcObjs.append(change_logger.create_changelog_import_object("svc", self.import_state, 'C', changeTyp, importTime, new_svc_id, old_svc_id))

        return nwObjs, svcObjs


    def addChangelogObjects(self, nwObjIdsAdded: list[dict[str, int]], svcObjIdsAdded: list[dict[str, int]], nwObjIdsRemoved: list[dict[str, int]], svcObjIdsRemoved: list[dict[str, int]]):
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

        query_variables = {
            'nwObjChanges': nwObjsChanged, 
            'svcObjChanges': svcObjsChanged
        }

        if len(nwObjsChanged) + len(svcObjsChanged)>0:
            try:
                changelogResult = self.import_state.api_call.call(changelogMutation, query_variables=query_variables, analyze_payload=True)
                if 'errors' in changelogResult:
                    logger.exception(f"error while adding changelog entries for objects: {str(changelogResult['errors'])}")
                    errors = 1
            except Exception:
                logger.exception(f"fatal error while adding changelog entries for objects: {str(traceback.format_exc())}")
                errors = 1
        
        return errors

from enum import Enum
import traceback
import datetime
import json
from typing import Any

from fwo_log import ChangeLogger, FWOLogger
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

class Type(Enum):
    NETWORK_OBJECT = "network_object"
    SERVICE_OBJECT = "service_object"
    USER = "user"

# this class is used for importing a config into the FWO API
class FwConfigImportObject():

    import_state: ImportStateController
    normalized_config: FwConfigNormalized | None = None
    global_normalized_config: FwConfigNormalized | None = None
    group_flats_mapper: GroupFlatsMapper
    prev_group_flats_mapper: GroupFlatsMapper
    uid2id_mapper: Uid2IdMapper
    
    def __init__(self):

        # Get state, config and services.

        service_provider = ServiceProvider()
        global_state = service_provider.get_global_state()
        self.import_state = global_state.import_state
        self.normalized_config = global_state.normalized_config
        self.global_normalized_config = global_state.global_normalized_config
        self.group_flats_mapper = service_provider.get_group_flats_mapper(self.import_state.ImportId)
        self.prev_group_flats_mapper = service_provider.get_prev_group_flats_mapper(self.import_state.ImportId)
        self.uid2id_mapper = service_provider.get_uid2id_mapper(self.import_state.ImportId)
        # Create maps.
        
        self.NetworkObjectTypeMap = self.get_network_obj_type_map()
        self.ServiceObjectTypeMap = self.get_service_obj_type_map()
        self.UserObjectTypeMap = self.get_user_obj_type_map()
        self.ProtocolMap = self.get_protocol_map()


    def updateObjectDiffs(self, prev_config: FwConfigNormalized, prev_global_config: FwConfigNormalized|None, single_manager: FwConfigManager):

        change_logger = ChangeLogger()
        if self.normalized_config is None:
            raise FwoImporterError("no normalized config available in FwConfigImportObject.updateObjectDiffs")
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
            self.update_objects_via_api(single_manager, newNwobjUids, newSvcObjUids, newUserUids, new_zone_names, deletedNwobjUids, deletedSvcObjUids, deletedUserUids, deleted_zone_names)
        
        self.uid2id_mapper.add_network_object_mappings(newNwObjIds, is_global=single_manager.IsSuperManager)
        self.uid2id_mapper.add_service_object_mappings(newNwSvcIds, is_global=single_manager.IsSuperManager)
        self.uid2id_mapper.add_user_mappings(newUserIds, is_global=single_manager.IsSuperManager)
        self.uid2id_mapper.add_zone_mappings(new_zone_ids, is_global=single_manager.IsSuperManager)

        # insert new and updated group memberships
        self.add_group_memberships(prev_config, Type.NETWORK_OBJECT)
        self.add_group_memberships(prev_config, Type.SERVICE_OBJECT)
        self.add_group_memberships(prev_config, Type.USER) 

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
        
        self.add_changelog_objs(newNwObjIds, newNwSvcIds, removedNwObjIds, removedNwSvcIds)

        # note changes:
        self.import_state.Stats.NetworkObjectAddCount = len(newNwObjIds)
        self.import_state.Stats.NetworkObjectDeleteCount = len(removedNwObjIds)
        self.import_state.Stats.NetworkObjectChangeCount = len(change_logger.changed_object_id_map.items())
        self.import_state.Stats.ServiceObjectAddCount = len(newNwSvcIds)
        self.import_state.Stats.ServiceObjectDeleteCount = len(removedNwSvcIds)
        self.import_state.Stats.ServiceObjectChangeCount = len(change_logger.changed_service_id_map.items())


    def get_network_obj_type_map(self) -> dict[str, int]:
        query = "query getNetworkObjTypeMap { stm_obj_typ { obj_typ_name obj_typ_id } }"
        try:
            result = self.import_state.api_call.call(query=query, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_obj_typ: str{e}")
            return {}
        
        nwobj_type_map: dict[str, Any] = {}
        for nw_type in result['data']['stm_obj_typ']:
            nwobj_type_map.update({nw_type['obj_typ_name']: nw_type['obj_typ_id']})
        return nwobj_type_map

    def get_service_obj_type_map(self) -> dict[str, int]:
        query = "query getServiceObjTypeMap { stm_svc_typ { svc_typ_name svc_typ_id } }"
        try:
            result = self.import_state.api_call.call(query=query, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_svc_typ: {str(e)}")
            return {}
        
        svc_type_map: dict[str, Any] = {}
        for svc_type in result['data']['stm_svc_typ']:
            svc_type_map.update({svc_type['svc_typ_name']: svc_type['svc_typ_id']})
        return svc_type_map

    def get_user_obj_type_map(self) -> dict[str, int]:
        query = "query getUserObjTypeMap { stm_usr_typ { usr_typ_name usr_typ_id } }"
        try:
            result = self.import_state.api_call.call(query=query, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_usr_typ: {str(e)}")
            return {}
        
        user_type_map: dict[str, Any] = {}
        for usr_type in result['data']['stm_usr_typ']:
            user_type_map.update({usr_type['usr_typ_name']: usr_type['usr_typ_id']})
        return user_type_map

    def get_protocol_map(self) -> dict[str, int]:
        query = "query getIpProtocols { stm_ip_proto { ip_proto_id ip_proto_name } }"
        try:
            result = self.import_state.api_call.call(query=query, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_ip_proto: {str(e)}")
            return {}
        
        protocol_map: dict[str, Any] = {}
        for proto in result['data']['stm_ip_proto']:
            protocol_map.update({proto['ip_proto_name'].lower(): proto['ip_proto_id']})
        return protocol_map

    def update_objects_via_api(self, single_manager: FwConfigManager, newNwObjectUids: list[str], newSvcObjectUids: list[str], newUserUids: list[str], new_zone_names: list[str], removedNwObjectUids: list[str], removedSvcObjectUids: list[str], removedUserUids: list[str], removed_zone_names: list[str]):
        # here we also mark old objects removed before adding the new versions
        new_nwobj_ids = []
        new_nwsvc_ids = []
        new_user_ids = []
        new_zone_ids = []
        removed_nwobj_ids = []
        removed_nwsvc_ids = []
        removed_user_ids = []
        removed_zone_ids = []
        this_managements_id = self.import_state.lookupManagementId(single_manager.ManagerUid)
        if this_managements_id is None:
            raise FwoImporterError(f"failed to update objects in updateObjectsViaApi: no management id found for manager uid '{single_manager.ManagerUid}'")
        import_mutation = FwoApi.get_graphql_code(file_list=[fwo_const.GRAPHQL_QUERY_PATH + "allObjects/upsertObjects.graphql"])
        query_variables: dict[str, Any] = {
            'mgmId': this_managements_id,
            'importId': self.import_state.ImportId,
            'newNwObjects': self.prepare_new_nwobjs(newNwObjectUids, this_managements_id),
            'newSvcObjects': self.prepare_new_svcobjs(newSvcObjectUids, this_managements_id),
            'newUsers': self.prepare_new_userobjs(newUserUids, this_managements_id),
            'newZones': self.prepare_new_zones(new_zone_names, this_managements_id),
            'removedNwObjectUids': removedNwObjectUids,
            'removedSvcObjectUids': removedSvcObjectUids,
            'removedUserUids': removedUserUids,
            'removedZoneUids': removed_zone_names
        }

        FWOLogger.debug(f"fwo_api:importNwObject - import_mutation: {import_mutation}", 9)
        if FWOLogger.is_debug_level(9):
            json.dump(query_variables, open(f"/usr/local/fworch/tmp/import/mgm_id_{self.import_state.MgmDetails.Id}_query_variables.json", "w"), indent=4)

        try:
            import_result = self.import_state.api_call.call(import_mutation, query_variables=query_variables, analyze_payload=True)
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
                new_nwobj_ids = import_result['data']['insert_object']['returning']
                new_nwsvc_ids = import_result['data']['insert_service']['returning']
                new_user_ids = import_result['data']['insert_usr']['returning']
                new_zone_ids = import_result['data']['insert_zone']['returning']
                removed_nwobj_ids = import_result['data']['update_object']['returning']
                removed_nwsvc_ids = import_result['data']['update_service']['returning']
                removed_user_ids = import_result['data']['update_usr']['returning']
                removed_zone_ids = import_result['data']['update_zone']['returning']
        except Exception:
            # FWOLogger.exception(f"failed to update objects: {str(traceback.format_exc())}")
            raise FwoImporterError(f"failed to update objects: {str(traceback.format_exc())}")
        return new_nwobj_ids, new_nwsvc_ids, new_user_ids, new_zone_ids, removed_nwobj_ids, removed_nwsvc_ids, removed_user_ids, removed_zone_ids
    

    def prepare_new_nwobjs(self, new_nwobj_uids: list[str], mgm_id: int) -> list[dict[str, Any]]:
        if self.normalized_config is None:
            raise FwoImporterError("no normalized config available in FwConfigImportObject.prepare_new_nwobjs")
        new_nwobjs: list[dict[str, Any]] = []
        for nwobj_uid in new_nwobj_uids:
            new_nwobj = NetworkObjectForImport(nwObject=self.normalized_config.network_objects[nwobj_uid],
                                                    mgmId=mgm_id, 
                                                    importId=self.import_state.ImportId, 
                                                    colorId=self.import_state.lookupColorId(self.normalized_config.network_objects[nwobj_uid].obj_color), 
                                                    typId=self.lookup_obj_type(self.normalized_config.network_objects[nwobj_uid].obj_typ))
            new_nwobj_dict = new_nwobj.toDict()
            new_nwobjs.append(new_nwobj_dict)
        return new_nwobjs


    def prepare_new_svcobjs(self, new_svcobj_uids: list[str], mgm_id: int) -> list[dict[str, Any]]:
        if self.normalized_config is None:
            raise FwoImporterError("no normalized config available in FwConfigImportObject.prepare_new_svcobjs")
        new_svcs: list[dict[str, Any]] = []
        for uid in new_svcobj_uids:
            new_svcs.append(ServiceObjectForImport(svcObject=self.normalized_config.service_objects[uid],
                                        mgmId=mgm_id, 
                                        importId=self.import_state.ImportId, 
                                        colorId=self.import_state.lookupColorId(self.normalized_config.service_objects[uid].svc_color), 
                                        typId=self.lookup_svc_type(self.normalized_config.service_objects[uid].svc_typ),
                                        ).toDict())
        return new_svcs

    def prepare_new_userobjs(self, new_user_uids: list[str], mgm_id: int) -> list[dict[str, Any]]:
        if self.normalized_config is None:
            raise FwoImporterError("no normalized config available in FwConfigImportObject.prepare_new_userobjs")
        new_users: list[dict[str, Any]] = []
        for uid in new_user_uids:
            new_users.append({
                'user_uid': uid,
                'mgm_id': mgm_id,
                'user_create': self.import_state.ImportId,
                'user_last_seen': self.import_state.ImportId,
                'usr_typ_id': self.lookup_user_type(self.normalized_config.users[uid]['user_typ']),
                'user_name': self.normalized_config.users[uid]['user_name'],
            })
        return new_users


    def prepare_new_zones(self, new_zone_names: list[str], mgm_id: int) -> list[dict[str, Any]]:
        if self.normalized_config is None:
            raise FwoImporterError("no normalized config available in FwConfigImportObject.prepare_new_zones")
        new_objects: list[dict[str, Any]] = []
        for uid in new_zone_names:
            new_objects.append({
                'mgm_id': mgm_id,
                'zone_create': self.import_state.ImportId,
                'zone_last_seen': self.import_state.ImportId,
                'zone_name': self.normalized_config.zone_objects[uid]['zone_name'],
            })
        return new_objects
    
 
    def get_config_objects(self, type: Type, prev_config: FwConfigNormalized):
        if self.normalized_config is None:
            raise FwoImporterError("no normalized config available in FwConfigImportObject.get_config_objects")
        if type == Type.NETWORK_OBJECT:
            return prev_config.network_objects, self.normalized_config.network_objects
        if type == Type.SERVICE_OBJECT:
            return prev_config.service_objects, self.normalized_config.service_objects
        if type == Type.USER:
            return prev_config.users, self.normalized_config.users

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
            return [member.split(fwo_const.USER_DELIMITER)[0] for member in refs.split(fwo_const.LIST_DELIMITER) if member] if refs else []
        return refs.split(fwo_const.LIST_DELIMITER) if refs else []

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
        removed_members: list[dict[str, Any]] = []
        removed_flats: list[dict[str, Any]] = []

        prev_config_objects, current_config_objects = self.get_config_objects(type, prev_config)
        prefix = self.get_prefix(type)

        for uid in prev_config_objects.keys():
            self.find_removed_objects(current_config_objects, prev_config_objects, removed_members, removed_flats, prefix, uid, type)
        # remove outdated group memberships
        if len(removed_members) == 0:
            return

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
                FWOLogger.exception(f"fwo_api:importNwObject - error in removeOutdated{prefix.capitalize()}Memberships: {str(import_result['errors'])}")
            else:
                _ = int(import_result['data'][f'update_{prefix}']['affected_rows']) + \
                    int(import_result['data'][f'update_{prefix}_flat']['affected_rows'])
        except Exception:
            FWOLogger.exception(f"failed to remove outdated group memberships for {type}: {str(traceback.format_exc())}")
            

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


    def add_group_memberships(self, prev_config: FwConfigNormalized, obj_type: Type):
        """
        This function is used to update group memberships for nwobjs, services or users in the database.
        It adds group memberships and flats for new and updated members.
        Args:
            prev_config (FwConfigNormalized): The previous normalized config.
        """
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
                FWOLogger.error(f"failed to add group memberships: no id found for group uid '{uid}'")
                continue
            self.collect_group_members(group_id, current_config_objects, new_group_members, member_uids, obj_type, prefix, prev_member_uids, prev_config_objects)
            flat_member_uids = self.get_flats(obj_type, uid)
            self.collect_flat_group_members(group_id, current_config_objects, new_group_member_flats, flat_member_uids, obj_type, prefix, prev_flat_member_uids, prev_config_objects)

        if len(new_group_members)==0:
            return
             
        self.write_member_updates(new_group_members, new_group_member_flats, prefix)


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


    def write_member_updates(self, new_group_members: list[dict[str, Any]], new_group_member_flats: list[dict[str, Any]], prefix: str):
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
                FWOLogger.exception(f"fwo_api:addGroupMemberships: {str(import_result['errors'])}")
                if 'duplicate' in import_result['errors']:
                    raise FwoDuplicateKeyViolation(str(import_result['errors']))
                else:
                    raise FwoImporterError(str(import_result['errors']))
            else:
                _ = int(import_result['data'][f'insert_{prefix}']['affected_rows']) + \
                    int(import_result['data'][f'insert_{prefix}_flat']['affected_rows'])
        except Exception:
            FWOLogger.exception(f"failed to write new objects: {str(traceback.format_exc())}")
            raise


    def lookup_obj_type(self, obj_type_str: str) -> int:
        # TODO: might check for miss here as this is a mandatory field!
        return self.NetworkObjectTypeMap.get(obj_type_str, -1)

    def lookup_svc_type(self, svc_type_str: str) -> int:
        # TODO: might check for miss here as this is a mandatory field!
        return self.ServiceObjectTypeMap.get(svc_type_str, -1)

    def lookup_user_type(self, user_type_str: str) -> int:
        return self.UserObjectTypeMap.get(user_type_str, -1)

    def lookup_obj_id_to_uid_and_policy_name(self, obj_id: int) -> str:
        return str(obj_id) # mock
        # CAST((COALESCE (rule.rule_ruleid, rule.rule_uid) || ', Rulebase: ' || device.local_rulebase_name) AS VARCHAR) AS unique_name,
        # return self.NetworkObjectIdMap.get(objId, None)

    def lookup_svc_id_to_uid_and_policy_name(self, svc_id: int):
        return str(svc_id) # mock

    def lookup_proto_name_to_id(self, proto_str: str | int) -> int | None:
        if isinstance(proto_str, int):
            # logger = getFwoLogger()
            # FWOLogger.warning(f"found protocol with an id as name: {str(proto_str)}")
            return proto_str  # already an int, do nothing
        else:
            return self.ProtocolMap.get(proto_str.lower(), None)

    def prepare_changelog_objects(self, nw_obj_ids_added: list[dict[str, int]], svc_obj_ids_added: list[dict[str, int]], nw_obj_ids_removed: list[dict[str, int]], svc_obj_ids_removed: list[dict[str, int]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
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

        for nw_obj_id in [nw_obj_ids_added_item["obj_id"] for nw_obj_ids_added_item in nw_obj_ids_added]:
            nwObjs.append(change_logger.create_changelog_import_object("obj", self.import_state, 'I', changeTyp, importTime, nw_obj_id))

        for nw_obj_id in [nw_obj_ids_removed_item["obj_id"] for nw_obj_ids_removed_item in nw_obj_ids_removed]:
            nwObjs.append(change_logger.create_changelog_import_object("obj", self.import_state, 'D', changeTyp, importTime, nw_obj_id))

        for old_nw_obj_id, new_nw_obj_id in change_logger.changed_object_id_map.items():
            nwObjs.append(change_logger.create_changelog_import_object("obj", self.import_state, 'C', changeTyp, importTime, new_nw_obj_id, old_nw_obj_id))

        # Write changelog for Services.

        for svc_id in [svc_ids_added_item["svc_id"] for svc_ids_added_item in svc_obj_ids_added]:
            svcObjs.append(change_logger.create_changelog_import_object("svc", self.import_state, 'I', changeTyp, importTime, svc_id))

        for svc_id in [svc_ids_removed_item["svc_id"] for svc_ids_removed_item in svc_obj_ids_removed]:
            svcObjs.append(change_logger.create_changelog_import_object("svc", self.import_state, 'D', changeTyp, importTime, svc_id))

        for old_svc_id, new_svc_id in change_logger.changed_service_id_map.items():
            svcObjs.append(change_logger.create_changelog_import_object("svc", self.import_state, 'C', changeTyp, importTime, new_svc_id, old_svc_id))

        return nwObjs, svcObjs


    def add_changelog_objs(self, nwobj_ids_added: list[dict[str, int]], svc_obj_ids_added: list[dict[str, int]], nw_obj_ids_removed: list[dict[str, int]], svc_obj_ids_removed: list[dict[str, int]]):
        nwobjs_changed, svcobjs_changed = self.prepare_changelog_objects(nwobj_ids_added, svc_obj_ids_added, nw_obj_ids_removed, svc_obj_ids_removed)
        changelog_mutation = """
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
            'nwObjChanges': nwobjs_changed, 
            'svcObjChanges': svcobjs_changed
        }

        if len(nwobjs_changed) + len(svcobjs_changed)>0:
            try:
                changelog_result = self.import_state.api_call.call(changelog_mutation, query_variables=query_variables, analyze_payload=True)
                if 'errors' in changelog_result:
                    FWOLogger.exception(f"error while adding changelog entries for objects: {str(changelog_result['errors'])}")
            except Exception:
                FWOLogger.exception(f"fatal error while adding changelog entries for objects: {str(traceback.format_exc())}")

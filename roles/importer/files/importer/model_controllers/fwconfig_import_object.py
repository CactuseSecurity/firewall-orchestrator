import datetime
import traceback
from enum import Enum
from typing import Any

import fwo_const
from fwo_api_call import FwoApi
from fwo_exceptions import FwoDuplicateKeyViolationError, FwoImporterError
from fwo_log import ChangeLogger, FWOLogger
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager
from models.networkobject import NetworkObjectForImport
from models.serviceobject import ServiceObjectForImport
from models.time_object import TimeObject, TimeObjectForImport
from states.global_state import GlobalState
from states.import_state import ImportState
from states.management_state import ManagementState


class Type(Enum):
    NETWORK_OBJECT = "network_object"
    SERVICE_OBJECT = "service_object"
    USER = "user"


# this class is used for importing a config into the FWO API
class FwConfigImportObject:
    def update_object_diffs(
        self,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
        single_manager: FwConfigManager,
    ):
        change_logger = ChangeLogger()
        prev_config = management_state.previous_config
        prev_global_config = import_state.previous_super_config
        if management_state.normalized_config is None:
            raise FwoImporterError("no normalized config available in FwConfigImportObject.update_object_diffs")
        if prev_config is None or prev_global_config is None:
            raise FwoImporterError(
                "previous configs needed for diff calculation in FwConfigImportObject.update_object_diffs"
            )
        # calculate network object diffs
        # here we are handling the previous config as a dict for a while
        deleted_nw_obj_uids: list[str] = list(
            prev_config.network_objects.keys() - management_state.normalized_config.network_objects.keys()
        )
        new_nw_obj_uids: list[str] = list(
            management_state.normalized_config.network_objects.keys() - prev_config.network_objects.keys()
        )
        nw_obj_uids_in_both: list[str] = list(
            management_state.normalized_config.network_objects.keys() & prev_config.network_objects.keys()
        )

        # For correct changelog and stats.
        changed_nw_objs: list[str] = []
        changed_svcs: list[str] = []

        # decide if it is prudent to mix changed, deleted and added rules here:
        for nw_obj_uid in nw_obj_uids_in_both:
            if (
                management_state.normalized_config.network_objects[nw_obj_uid]
                != prev_config.network_objects[nw_obj_uid]
            ):
                new_nw_obj_uids.append(nw_obj_uid)
                deleted_nw_obj_uids.append(nw_obj_uid)
                changed_nw_objs.append(nw_obj_uid)

        # calculate service object diffs
        deleted_svc_obj_uids: list[str] = list(
            prev_config.service_objects.keys() - management_state.normalized_config.service_objects.keys()
        )
        new_svc_obj_uids: list[str] = list(
            management_state.normalized_config.service_objects.keys() - prev_config.service_objects.keys()
        )
        svc_obj_uids_in_both: list[str] = list(
            management_state.normalized_config.service_objects.keys() & prev_config.service_objects.keys()
        )

        for svc_obj_uid in svc_obj_uids_in_both:
            if (
                management_state.normalized_config.service_objects[svc_obj_uid]
                != prev_config.service_objects[svc_obj_uid]
            ):
                new_svc_obj_uids.append(svc_obj_uid)
                deleted_svc_obj_uids.append(svc_obj_uid)
                changed_svcs.append(svc_obj_uid)

        # calculate user diffs
        deleted_user_uids: list[str] = list(prev_config.users.keys() - management_state.normalized_config.users.keys())
        new_user_uids: list[str] = list(management_state.normalized_config.users.keys() - prev_config.users.keys())
        user_uids_in_both: list[str] = list(management_state.normalized_config.users.keys() & prev_config.users.keys())
        for user_uid in user_uids_in_both:
            if management_state.normalized_config.users[user_uid] != prev_config.users[user_uid]:
                new_user_uids.append(user_uid)
                deleted_user_uids.append(user_uid)

        # initial mapping of object uids to ids. needs to be updated, if more objects are created in the db after this point
        # TODO: only fetch objects needed later. Esp for !isFullImport. but: newNwObjIds not enough!
        # -> newObjs + extract all objects from new/changed rules and groups, flatten them. Complete?
        management_state.uid2id_mapper.update_network_object_mapping(is_global=single_manager.is_super_manager)
        management_state.uid2id_mapper.update_service_object_mapping(is_global=single_manager.is_super_manager)
        management_state.uid2id_mapper.update_user_mapping(is_global=single_manager.is_super_manager)
        management_state.uid2id_mapper.update_zone_mapping(is_global=single_manager.is_super_manager)

        management_state.group_flats_mapper.init_config(management_state.normalized_config, import_state.super_config)
        management_state.prev_group_flats_mapper.init_config(prev_config, prev_global_config)

        # need to do this first, since we need the old object IDs for the group memberships
        # TODO: computationally expensive? Even without changes, all group objects and their members are compared to the previous config.
        self.remove_outdated_memberships(import_state, management_state, prev_config, Type.NETWORK_OBJECT)
        self.remove_outdated_memberships(import_state, management_state, prev_config, Type.SERVICE_OBJECT)
        self.remove_outdated_memberships(import_state, management_state, prev_config, Type.USER)

        # calculate zone object diffs
        deleted_zone_names: list[str] = list(
            prev_config.zone_objects.keys() - management_state.normalized_config.zone_objects.keys()
        )
        new_zone_names: list[str] = list(
            management_state.normalized_config.zone_objects.keys() - prev_config.zone_objects.keys()
        )
        zone_names_in_both: list[str] = list(
            management_state.normalized_config.zone_objects.keys() & prev_config.zone_objects.keys()
        )
        changed_zones: list[str] = []

        for zone_name in zone_names_in_both:
            if management_state.normalized_config.zone_objects[zone_name] != prev_config.zone_objects[zone_name]:
                new_zone_names.append(zone_name)
                deleted_zone_names.append(zone_name)
                changed_zones.append(zone_name)

        # add newly created objects
        (
            new_nw_obj_ids,
            new_svc_obj_ids,
            new_user_ids,
            new_zone_ids,
            removed_nw_obj_ids,
            removed_svc_obj_ids,
            _,
            _,
        ) = self.update_objects_via_api(
            import_state,
            management_state,
            single_manager,
            new_nw_obj_uids,
            new_svc_obj_uids,
            new_user_uids,
            new_zone_names,
            deleted_nw_obj_uids,
            deleted_svc_obj_uids,
            deleted_user_uids,
            deleted_zone_names,
        )
        self.update_time_objs_via_api(
            import_state,
            management_state,
            prev_config.time_objects,
            management_state.normalized_config.time_objects,
            is_global=single_manager.is_super_manager,
        )

        management_state.uid2id_mapper.add_network_object_mappings(
            new_nw_obj_ids, is_global=single_manager.is_super_manager
        )
        management_state.uid2id_mapper.add_service_object_mappings(
            new_svc_obj_ids, is_global=single_manager.is_super_manager
        )
        management_state.uid2id_mapper.add_user_mappings(new_user_ids, is_global=single_manager.is_super_manager)
        management_state.uid2id_mapper.add_zone_mappings(new_zone_ids, is_global=single_manager.is_super_manager)

        # insert new and updated group memberships
        self.add_group_memberships(import_state, management_state, prev_config, Type.NETWORK_OBJECT)
        self.add_group_memberships(import_state, management_state, prev_config, Type.SERVICE_OBJECT)
        self.add_group_memberships(import_state, management_state, prev_config, Type.USER)

        # these objects have really been deleted so there should be no refs to them anywhere! verify this

        # TODO: calculate user diffs
        # TODO: write changelog for zones
        # Get Changed Ids.

        change_logger.create_change_id_maps(
            management_state.uid2id_mapper,
            changed_nw_objs,
            changed_svcs,
            removed_nw_obj_ids,
            removed_svc_obj_ids,
        )

        # Seperate changes from adds and removes for changelog and stats.

        new_nw_obj_ids = [
            new_nw_obj_id
            for new_nw_obj_id in new_nw_obj_ids
            if new_nw_obj_id["obj_id"] not in list(change_logger.changed_object_id_map.values())
        ]
        removed_nw_obj_ids = [
            removed_nw_obj_id
            for removed_nw_obj_id in removed_nw_obj_ids
            if removed_nw_obj_id["obj_id"] not in list(change_logger.changed_object_id_map.keys())
        ]
        new_svc_obj_ids = [
            new_svc_obj_id
            for new_svc_obj_id in new_svc_obj_ids
            if new_svc_obj_id["svc_id"] not in list(change_logger.changed_service_id_map.values())
        ]
        removed_svc_obj_ids = [
            removed_svc_obj_id
            for removed_svc_obj_id in removed_svc_obj_ids
            if removed_svc_obj_id["svc_id"] not in list(change_logger.changed_service_id_map.keys())
        ]

        # Write change logs to tables.

        self.add_changelog_objs(
            global_state, import_state, new_nw_obj_ids, new_svc_obj_ids, removed_nw_obj_ids, removed_svc_obj_ids
        )

        # note changes:
        import_state.statistics_controller.increment_network_object_add_count(len(new_nw_obj_ids))
        import_state.statistics_controller.increment_network_object_delete_count(len(removed_nw_obj_ids))
        import_state.statistics_controller.increment_network_object_change_count(
            len(change_logger.changed_object_id_map.items())
        )
        import_state.statistics_controller.increment_service_object_add_count(len(new_svc_obj_ids))
        import_state.statistics_controller.increment_service_object_delete_count(len(removed_svc_obj_ids))
        import_state.statistics_controller.increment_service_object_change_count(
            len(change_logger.changed_service_id_map.items())
        )

    # TODO: split into multiple functions again, as large queries are not handled efficiently in some scenarios
    def update_objects_via_api(
        self,
        import_state: ImportState,
        management_state: ManagementState,
        single_manager: FwConfigManager,
        new_nw_object_uids: list[str],
        new_svc_obj_uids: list[str],
        new_user_uids: list[str],
        new_zone_names: list[str],
        removed_nw_object_uids: list[str],
        removed_svc_object_uids: list[str],
        removed_user_uids: list[str],
        removed_zone_names: list[str],
    ) -> tuple[
        list[dict[str, Any]],
        list[dict[str, Any]],
        list[dict[str, Any]],
        list[dict[str, Any]],
        list[dict[str, Any]],
        list[dict[str, Any]],
        list[dict[str, Any]],
        list[dict[str, Any]],
    ]:
        """
        Update objects via FWO API.

        Args:
            single_manager (FwConfigManager): The manager for which the objects are being updated.
            new_nw_object_uids (list[str]): List of UIDs for new network objects to be added.
            new_svc_obj_uids (list[str]): List of UIDs for new service objects to be added.
            new_user_uids (list[str]): List of UIDs for new users to be added.
            new_zone_names (list[str]): List of names for new zones to be added.
            removed_nw_object_uids (list[str]): List of UIDs for network objects to be removed.
            removed_svc_object_uids (list[str]): List of UIDs for service objects to be removed.
            removed_user_uids (list[str]): List of UIDs for users to be removed.
            removed_zone_names (list[str]): List of names for zones to be removed.

        Returns:
            tuple: A tuple containing lists of dictionaries for new and removed objects' IDs.

        """
        # here we also mark old objects removed before adding the new versions
        new_nwobj_ids = []
        new_nwsvc_ids = []
        new_user_ids = []
        new_zone_ids = []
        removed_nwobj_ids = []
        removed_nwsvc_ids = []
        removed_user_ids = []
        removed_zone_ids = []
        this_managements_id = import_state.lookup_management_id(single_manager.manager_uid)
        if this_managements_id is None:
            raise FwoImporterError(
                f"failed to update objects in updateObjectsViaApi: no management id found for manager uid '{single_manager.manager_uid}'"
            )
        import_mutation = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "allObjects/upsertObjects.graphql"]
        )
        query_variables: dict[str, Any] = {
            "mgmId": this_managements_id,
            "importId": import_state.import_id,
            "newNwObjects": self.prepare_new_nwobjs(import_state, management_state, new_nw_object_uids),
            "newSvcObjects": self.prepare_new_svcobjs(import_state, management_state, new_svc_obj_uids),
            "newUsers": self.prepare_new_userobjs(import_state, management_state, new_user_uids),
            "newZones": self.prepare_new_zones(import_state, management_state, new_zone_names),
            "removedNwObjectUids": removed_nw_object_uids,
            "removedSvcObjectUids": removed_svc_object_uids,
            "removedUserUids": removed_user_uids,
            "removedZoneUids": removed_zone_names,
        }

        FWOLogger.debug(f"fwo_api:importNwObject - import_mutation: {import_mutation}", 9)

        try:
            import_result = import_state.fwo_api_call.call(
                import_mutation, query_variables=query_variables, analyze_payload=True
            )
            if "errors" in import_result:
                raise FwoImporterError(f"failed to update objects in updateObjectsViaApi: {import_result['errors']!s}")
            _ = (
                int(import_result["data"]["insert_object"]["affected_rows"])
                + int(import_result["data"]["insert_service"]["affected_rows"])
                + int(import_result["data"]["insert_usr"]["affected_rows"])
                + int(import_result["data"]["update_object"]["affected_rows"])
                + int(import_result["data"]["update_service"]["affected_rows"])
                + int(import_result["data"]["update_usr"]["affected_rows"])
                + int(import_result["data"]["update_zone"]["affected_rows"])
            )
            new_nwobj_ids = import_result["data"]["insert_object"]["returning"]
            new_nwsvc_ids = import_result["data"]["insert_service"]["returning"]
            new_user_ids = import_result["data"]["insert_usr"]["returning"]
            new_zone_ids = import_result["data"]["insert_zone"]["returning"]
            removed_nwobj_ids = import_result["data"]["update_object"]["returning"]
            removed_nwsvc_ids = import_result["data"]["update_service"]["returning"]
            removed_user_ids = import_result["data"]["update_usr"]["returning"]
            removed_zone_ids = import_result["data"]["update_zone"]["returning"]
        except Exception:
            raise FwoImporterError(f"failed to update objects: {traceback.format_exc()!s}")
        return (
            new_nwobj_ids,
            new_nwsvc_ids,
            new_user_ids,
            new_zone_ids,
            removed_nwobj_ids,
            removed_nwsvc_ids,
            removed_user_ids,
            removed_zone_ids,
        )

    def update_time_objs_via_api(
        self,
        import_state: ImportState,
        management_state: ManagementState,
        previous_time_objs: dict[str, TimeObject],
        current_time_objs: dict[str, TimeObject],
        is_global: bool,
    ) -> None:
        """
        Insert new time objects and update removed time objects via FWO API.
        Also updates uid2id mapping for time objects and import statistics.
        """
        management_state.uid2id_mapper.update_time_object_mapping(is_global=is_global)
        import_mutation = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "time/upsertTimeObjects.graphql"]
        )
        new_uids = list(current_time_objs.keys() - previous_time_objs.keys())
        removed_uids = list(previous_time_objs.keys() - current_time_objs.keys())
        # changed time objects will be set to removed and re-added with new data
        changed_uids = [
            uid
            for uid in current_time_objs.keys() & previous_time_objs.keys()
            if current_time_objs[uid] != previous_time_objs[uid]
        ]
        query_variables: dict[str, Any] = {
            "mgmId": import_state.mgm_details.current_mgm_id,
            "importId": import_state.import_id,
            "newTimeObjects": [
                TimeObjectForImport.from_normalized(
                    current_time_objs[uid],
                    import_state.mgm_details.current_mgm_id,
                    import_state.import_id,
                ).model_dump()
                for uid in new_uids + changed_uids
            ],
            "removedTimeObjectIds": [
                management_state.uid2id_mapper.get_time_object_id(uid) for uid in removed_uids + changed_uids
            ],
        }
        try:
            import_result = import_state.fwo_api_call.call(
                import_mutation, query_variables=query_variables, analyze_payload=True
            )
            if "errors" in import_result:
                raise FwoImporterError(f"failed to update time objects: {import_result['errors']!s}")
            insert_count = int(import_result["data"]["insert_time_object"]["affected_rows"])
            update_count = int(import_result["data"]["update_time_object"]["affected_rows"])
            management_state.uid2id_mapper.add_time_object_mappings(
                import_result["data"]["insert_time_object"]["returning"], is_global=is_global
            )
            import_state.statistics_controller.statistics.time_object_add_count += len(new_uids)
            import_state.statistics_controller.statistics.time_object_delete_count += len(removed_uids)
            import_state.statistics_controller.statistics.time_object_change_count += len(changed_uids)
            FWOLogger.debug(
                f"fwo_api:importTimeObject - updated time objects via API. Inserted: {insert_count}, Updated: {update_count}"
            )
        except Exception:
            raise FwoImporterError(f"failed to update time objects: {traceback.format_exc()!s}")

    def prepare_new_nwobjs(
        self, import_state: ImportState, management_state: ManagementState, new_nwobj_uids: list[str]
    ) -> list[dict[str, Any]]:
        if management_state.normalized_config is None:
            raise FwoImporterError("no normalized config available in FwConfigImportObject.prepare_new_nwobjs")
        new_nwobjs: list[dict[str, Any]] = []
        for nwobj_uid in new_nwobj_uids:
            new_nwobj = NetworkObjectForImport(
                nw_object=management_state.normalized_config.network_objects[nwobj_uid],
                mgm_id=management_state.mgm_id,
                import_id=import_state.import_id,
                color_id=import_state.lookup_color_id(
                    management_state.normalized_config.network_objects[nwobj_uid].obj_color
                ),
                typ_id=import_state.lookup_network_obj_type_id(
                    management_state.normalized_config.network_objects[nwobj_uid].obj_typ
                ),
            )
            new_nwobj_dict = new_nwobj.to_dict()
            new_nwobjs.append(new_nwobj_dict)
        return new_nwobjs

    def prepare_new_svcobjs(
        self, import_state: ImportState, management_state: ManagementState, new_svcobj_uids: list[str]
    ) -> list[dict[str, Any]]:
        if management_state.normalized_config is None:
            raise FwoImporterError("no normalized config available in FwConfigImportObject.prepare_new_svcobjs")
        return [
            ServiceObjectForImport(
                svc_object=management_state.normalized_config.service_objects[uid],
                mgm_id=management_state.mgm_id,
                import_id=import_state.import_id,
                color_id=import_state.lookup_color_id(
                    management_state.normalized_config.service_objects[uid].svc_color
                ),
                typ_id=import_state.lookup_service_obj_type_id(
                    management_state.normalized_config.service_objects[uid].svc_typ
                ),
            ).to_dict()
            for uid in new_svcobj_uids
        ]

    def prepare_new_userobjs(
        self, import_state: ImportState, management_state: ManagementState, new_user_uids: list[str]
    ) -> list[dict[str, Any]]:
        if management_state.normalized_config is None:
            raise FwoImporterError("no normalized config available in FwConfigImportObject.prepare_new_userobjs")
        return [
            {
                "user_uid": uid,
                "mgm_id": management_state.mgm_id,
                "user_create": import_state.import_id,
                "user_last_seen": import_state.import_id,
                "usr_typ_id": import_state.lookup_user_obj_type_id(
                    management_state.normalized_config.users[uid]["user_typ"]
                ),
                "user_name": management_state.normalized_config.users[uid]["user_name"],
            }
            for uid in new_user_uids
        ]

    def prepare_new_zones(
        self, import_state: ImportState, management_state: ManagementState, new_zone_names: list[str]
    ) -> list[dict[str, Any]]:
        if management_state.normalized_config is None:
            raise FwoImporterError("no normalized config available in FwConfigImportObject.prepare_new_zones")

        return [
            {
                "mgm_id": management_state.mgm_id,
                "zone_create": import_state.import_id,
                "zone_last_seen": import_state.import_id,
                "zone_name": management_state.normalized_config.zone_objects[uid]["zone_name"],
            }
            for uid in new_zone_names
        ]

    def get_config_objects(
        self, management_state: ManagementState, typ: Type, prev_config: FwConfigNormalized
    ) -> tuple[dict[str, Any], dict[str, Any]]:
        if management_state.normalized_config is None:
            raise FwoImporterError("no normalized config available in FwConfigImportObject.get_config_objects")
        if typ == Type.NETWORK_OBJECT:
            return prev_config.network_objects, management_state.normalized_config.network_objects
        if typ == Type.SERVICE_OBJECT:
            return prev_config.service_objects, management_state.normalized_config.service_objects
        return prev_config.users, management_state.normalized_config.users

    def get_id(self, management_state: ManagementState, typ: Type, uid: str, before_update: bool = False) -> int | None:
        if typ == Type.NETWORK_OBJECT:
            return management_state.uid2id_mapper.get_network_object_id(uid, before_update)
        if typ == Type.SERVICE_OBJECT:
            return management_state.uid2id_mapper.get_service_object_id(uid, before_update)
        return management_state.uid2id_mapper.get_user_id(uid, before_update)

    def get_local_id(
        self, management_state: ManagementState, typ: Type, uid: str, before_update: bool = False
    ) -> int | None:
        if typ == Type.NETWORK_OBJECT:
            return management_state.uid2id_mapper.get_network_object_id(uid, before_update, local_only=True)
        if typ == Type.SERVICE_OBJECT:
            return management_state.uid2id_mapper.get_service_object_id(uid, before_update, local_only=True)
        return management_state.uid2id_mapper.get_user_id(uid, before_update, local_only=True)

    def is_group(self, typ: Type, obj: Any) -> bool | None:
        if typ == Type.NETWORK_OBJECT:
            return obj.obj_typ == "group"
        if typ == Type.SERVICE_OBJECT:
            return obj.svc_typ == "group"
        return obj.get("user_typ", None) == "group"

    def get_refs(self, typ: Type, obj: Any) -> str | None:
        if typ == Type.NETWORK_OBJECT:
            return obj.obj_member_refs
        if typ == Type.SERVICE_OBJECT:
            return obj.svc_member_refs
        return obj.get("user_member_refs", None)

    def get_members(self, typ: Type, refs: str | None) -> list[str]:
        if typ == Type.NETWORK_OBJECT:
            return (
                [member.split(fwo_const.USER_DELIMITER)[0] for member in refs.split(fwo_const.LIST_DELIMITER) if member]
                if refs
                else []
            )
        return refs.split(fwo_const.LIST_DELIMITER) if refs else []

    def get_flats(self, management_state: ManagementState, typ: Type, uid: str) -> list[str]:
        if typ == Type.NETWORK_OBJECT:
            return management_state.group_flats_mapper.get_network_object_flats([uid])
        if typ == Type.SERVICE_OBJECT:
            return management_state.group_flats_mapper.get_service_object_flats([uid])
        return management_state.group_flats_mapper.get_user_flats([uid])

    def get_prev_flats(self, management_state: ManagementState, typ: Type, uid: str) -> list[str]:
        if typ == Type.NETWORK_OBJECT:
            return management_state.prev_group_flats_mapper.get_network_object_flats([uid])
        if typ == Type.SERVICE_OBJECT:
            return management_state.prev_group_flats_mapper.get_service_object_flats([uid])
        return management_state.prev_group_flats_mapper.get_user_flats([uid])

    def get_prefix(self, typ: Type):
        if typ == Type.NETWORK_OBJECT:
            return "objgrp"
        if typ == Type.SERVICE_OBJECT:
            return "svcgrp"
        return "usergrp"

    def remove_outdated_memberships(
        self, import_state: ImportState, management_state: ManagementState, prev_config: FwConfigNormalized, typ: Type
    ):
        removed_members: list[dict[str, Any]] = []
        removed_flats: list[dict[str, Any]] = []

        prev_config_objects, current_config_objects = self.get_config_objects(management_state, typ, prev_config)
        prefix = self.get_prefix(typ)

        for uid in prev_config_objects:
            self.find_removed_objects(
                management_state,
                current_config_objects,
                prev_config_objects,
                removed_members,
                removed_flats,
                prefix,
                uid,
                typ,
            )
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
            "importId": import_state.import_id,
            "removedMembers": removed_members,
            "removedFlats": removed_flats,
        }
        try:
            import_result = import_state.fwo_api_call.call(
                import_mutation, query_variables=query_variables, analyze_payload=True
            )
            if "errors" in import_result:
                FWOLogger.exception(
                    f"fwo_api:importNwObject - error in removeOutdated{prefix.capitalize()}Memberships: {import_result['errors']!s}"
                )
            else:
                _ = int(import_result["data"][f"update_{prefix}"]["affected_rows"]) + int(
                    import_result["data"][f"update_{prefix}_flat"]["affected_rows"]
                )
        except Exception:
            FWOLogger.exception(f"failed to remove outdated group memberships for {typ}: {traceback.format_exc()!s}")

    def find_removed_objects(
        self,
        management_state: ManagementState,
        current_config_objects: dict[str, Any],
        prev_config_objects: dict[str, Any],
        removed_members: list[dict[str, Any]],
        removed_flats: list[dict[str, Any]],
        prefix: str,
        uid: str,
        typ: Type,
    ) -> None:
        if not self.is_group(typ, prev_config_objects[uid]):
            return
        db_id = self.get_id(management_state, typ, uid, before_update=True)
        prev_member_uids = self.get_members(typ, self.get_refs(typ, prev_config_objects[uid]))
        prev_flat_member_uids = self.get_prev_flats(management_state, typ, uid)
        member_uids = []  # all members need to be removed if group deleted or changed
        flat_member_uids = []
        # group not removed and group not changed -> check for changes in members
        if uid in current_config_objects and current_config_objects[uid] == prev_config_objects[uid]:
            member_uids = self.get_members(typ, self.get_refs(typ, current_config_objects[uid]))
            flat_member_uids = self.get_flats(management_state, typ, uid)
        for prev_member_uid in prev_member_uids:
            if (
                prev_member_uid in member_uids
                and current_config_objects[prev_member_uid] == prev_config_objects[prev_member_uid]
            ):
                continue  # member was not removed or changed
            prev_member_id = self.get_id(management_state, typ, prev_member_uid, before_update=True)
            removed_members.append(
                {
                    "_and": [
                        {f"{prefix}_id": {"_eq": db_id}},
                        {f"{prefix}_member_id": {"_eq": prev_member_id}},
                    ]
                }
            )
        for prev_flat_member_uid in prev_flat_member_uids:
            if (
                prev_flat_member_uid in flat_member_uids
                and current_config_objects[prev_flat_member_uid] == prev_config_objects[prev_flat_member_uid]
            ):
                continue  # flat member was not removed or changed
            prev_flat_member_id = self.get_id(management_state, typ, prev_flat_member_uid, before_update=True)
            removed_flats.append(
                {
                    "_and": [
                        {f"{prefix}_flat_id": {"_eq": db_id}},
                        {f"{prefix}_flat_member_id": {"_eq": prev_flat_member_id}},
                    ]
                }
            )

    def add_group_memberships(
        self,
        import_state: ImportState,
        management_state: ManagementState,
        prev_config: FwConfigNormalized,
        obj_type: Type,
    ):
        """
        Function is used to update group memberships for nwobjs, services or users in the database.
        It adds group memberships and flats for new and updated members.

        Args:
            prev_config (FwConfigNormalized): The previous normalized config.

        """
        new_group_members: list[dict[str, Any]] = []
        new_group_member_flats: list[dict[str, Any]] = []
        prev_config_objects, current_config_objects = self.get_config_objects(management_state, obj_type, prev_config)
        prefix = self.get_prefix(obj_type)
        for uid in current_config_objects:
            if not self.is_group(obj_type, current_config_objects[uid]):
                continue
            member_uids = self.get_members(obj_type, self.get_refs(obj_type, current_config_objects[uid]))
            prev_member_uids = []  # all members need to be added if group added or changed
            prev_flat_member_uids = []
            if uid in prev_config_objects and current_config_objects[uid] == prev_config_objects[uid]:
                # group not changed -> check for changes in members
                prev_member_uids = self.get_members(obj_type, self.get_refs(obj_type, prev_config_objects[uid]))
                prev_flat_member_uids = self.get_prev_flats(management_state, obj_type, uid)

            group_id = self.get_id(management_state, obj_type, uid)
            if group_id is None:
                FWOLogger.error(f"failed to add group memberships: no id found for group uid '{uid}'")
                continue

            self.collect_group_members(
                import_state,
                management_state,
                group_id,
                current_config_objects,
                new_group_members,
                member_uids,
                obj_type,
                prefix,
                prev_member_uids,
                prev_config_objects,
            )
            flat_member_uids = self.get_flats(management_state, obj_type, uid)
            self.collect_flat_group_members(
                import_state,
                management_state,
                group_id,
                current_config_objects,
                new_group_member_flats,
                flat_member_uids,
                obj_type,
                prefix,
                prev_flat_member_uids,
                prev_config_objects,
            )

        if len(new_group_members) == 0:
            return

        self.write_member_updates(import_state, new_group_members, new_group_member_flats, prefix)

    def collect_flat_group_members(
        self,
        import_state: ImportState,
        management_state: ManagementState,
        group_id: int,
        current_config_objects: dict[str, Any],
        new_group_member_flats: list[dict[str, Any]],
        flat_member_uids: list[str],
        obj_type: Type,
        prefix: str,
        prev_flat_member_uids: list[str],
        prev_config_objects: dict[str, Any],
    ):
        for flat_member_uid in flat_member_uids:
            if (
                flat_member_uid in prev_flat_member_uids
                and prev_config_objects[flat_member_uid] == current_config_objects[flat_member_uid]
            ):
                continue  # flat member was not added or changed
            flat_member_id = self.get_id(management_state, obj_type, flat_member_uid)
            new_group_member_flats.append(
                {
                    f"{prefix}_flat_id": group_id,
                    f"{prefix}_flat_member_id": flat_member_id,
                    "import_created": import_state.import_id,
                    "import_last_seen": import_state.import_id,  # to be removed in the future
                }
            )

    def collect_group_members(
        self,
        import_state: ImportState,
        management_state: ManagementState,
        group_id: int,
        current_config_objects: dict[str, Any],
        new_group_members: list[dict[str, Any]],
        member_uids: list[str],
        obj_type: Type,
        prefix: str,
        prev_member_uids: list[str],
        prev_config_objects: dict[str, Any],
    ):
        for member_uid in member_uids:
            if member_uid in prev_member_uids and prev_config_objects[member_uid] == current_config_objects[member_uid]:
                continue  # member was not added or changed
            member_id = self.get_id(management_state, obj_type, member_uid)
            new_group_members.append(
                {
                    f"{prefix}_id": group_id,
                    f"{prefix}_member_id": member_id,
                    "import_created": import_state.import_id,
                    "import_last_seen": import_state.import_id,  # to be removed in the future
                }
            )

    def write_member_updates(
        self,
        import_state: ImportState,
        new_group_members: list[dict[str, Any]],
        new_group_member_flats: list[dict[str, Any]],
        prefix: str,
    ):
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
            "groups": new_group_members,
            "groupFlats": new_group_member_flats,
        }
        try:
            import_result = import_state.fwo_api_call.call(
                import_mutation, query_variables=query_variables, analyze_payload=True
            )
            if "errors" in import_result:
                FWOLogger.exception(f"fwo_api:addGroupMemberships: {import_result['errors']!s}")
                if "duplicate" in import_result["errors"]:
                    raise FwoDuplicateKeyViolationError(str(import_result["errors"]))
                raise FwoImporterError(str(import_result["errors"]))
            _ = int(import_result["data"][f"insert_{prefix}"]["affected_rows"]) + int(
                import_result["data"][f"insert_{prefix}_flat"]["affected_rows"]
            )
        except Exception:
            FWOLogger.exception(f"failed to write new objects: {traceback.format_exc()!s}")
            raise

    def prepare_changelog_objects(
        self,
        global_state: GlobalState,
        import_state: ImportState,
        nw_obj_ids_added: list[dict[str, int]],
        svc_obj_ids_added: list[dict[str, int]],
        nw_obj_ids_removed: list[dict[str, int]],
        svc_obj_ids_removed: list[dict[str, int]],
    ) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
        """
        Insert into stm_change_type (change_type_id,change_type_name) VALUES (1,'factory settings');
        insert into stm_change_type (change_type_id,change_type_name) VALUES (2,'initial import');
        insert into stm_change_type (change_type_id,change_type_name) VALUES (3,'in operation');
        """
        # TODO: deal with object changes where we need old and new obj id

        nw_objs: list[dict[str, Any]] = []
        svc_objs: list[dict[str, Any]] = []
        import_time = datetime.datetime.now().isoformat()
        change_typ = 3  # standard
        change_logger = ChangeLogger()

        if import_state.is_initial_import or global_state.fwo_config_controller.fwo_config.clear:
            change_typ = 2  # initial - to be ignored in change reports

        # Write changelog for network objects.

        nw_objs = [
            change_logger.create_changelog_import_object(
                "obj", import_state.import_id, import_state.mgm_details.mgm_id, "I", change_typ, import_time, nw_obj_id
            )
            for nw_obj_id in [nw_obj_ids_added_item["obj_id"] for nw_obj_ids_added_item in nw_obj_ids_added]
        ]

        nw_objs.extend(
            [
                change_logger.create_changelog_import_object(
                    "obj",
                    import_state.import_id,
                    import_state.mgm_details.mgm_id,
                    "D",
                    change_typ,
                    import_time,
                    nw_obj_id,
                )
                for nw_obj_id in [nw_obj_ids_removed_item["obj_id"] for nw_obj_ids_removed_item in nw_obj_ids_removed]
            ]
        )

        for old_nw_obj_id, new_nw_obj_id in change_logger.changed_object_id_map.items():
            nw_objs.append(
                change_logger.create_changelog_import_object(
                    "obj",
                    import_state.import_id,
                    import_state.mgm_details.mgm_id,
                    "C",
                    change_typ,
                    import_time,
                    new_nw_obj_id,
                    old_nw_obj_id,
                )
            )

        # Write changelog for Services.

        svc_objs.extend(
            [
                change_logger.create_changelog_import_object(
                    "svc", import_state.import_id, import_state.mgm_details.mgm_id, "I", change_typ, import_time, svc_id
                )
                for svc_id in [svc_ids_added_item["svc_id"] for svc_ids_added_item in svc_obj_ids_added]
            ]
        )

        svc_objs.extend(
            [
                change_logger.create_changelog_import_object(
                    "svc", import_state.import_id, import_state.mgm_details.mgm_id, "D", change_typ, import_time, svc_id
                )
                for svc_id in [svc_ids_removed_item["svc_id"] for svc_ids_removed_item in svc_obj_ids_removed]
            ]
        )

        for old_svc_id, new_svc_id in change_logger.changed_service_id_map.items():
            svc_objs.append(
                change_logger.create_changelog_import_object(
                    "svc",
                    import_state.import_id,
                    import_state.mgm_details.mgm_id,
                    "C",
                    change_typ,
                    import_time,
                    new_svc_id,
                    old_svc_id,
                )
            )

        return nw_objs, svc_objs

    def add_changelog_objs(
        self,
        global_state: GlobalState,
        import_state: ImportState,
        nwobj_ids_added: list[dict[str, int]],
        svc_obj_ids_added: list[dict[str, int]],
        nw_obj_ids_removed: list[dict[str, int]],
        svc_obj_ids_removed: list[dict[str, int]],
    ):
        nwobjs_changed, svcobjs_changed = self.prepare_changelog_objects(
            global_state, import_state, nwobj_ids_added, svc_obj_ids_added, nw_obj_ids_removed, svc_obj_ids_removed
        )
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
            "nwObjChanges": nwobjs_changed,
            "svcObjChanges": svcobjs_changed,
        }

        if len(nwobjs_changed) + len(svcobjs_changed) > 0:
            try:
                changelog_result = import_state.fwo_api_call.call(
                    changelog_mutation,
                    query_variables=query_variables,
                    analyze_payload=True,
                )
                if "errors" in changelog_result:
                    FWOLogger.exception(
                        f"error while adding changelog entries for objects: {changelog_result['errors']!s}"
                    )
            except Exception:
                FWOLogger.exception(
                    f"fatal error while adding changelog entries for objects: {traceback.format_exc()!s}"
                )

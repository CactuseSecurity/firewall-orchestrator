import traceback
from typing import Any

import fwo_const
import fwo_globals
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_base import ConfigAction, find_all_diffs
from fwo_exceptions import FwoApiFailedDeleteOldImportsError, FwoImporterError, ImportInterruptionError
from fwo_log import FWOLogger
from model_controllers.check_consistency import FwConfigImportCheckConsistency
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.management_controller import (
    ManagementController,
)
from model_controllers.rulebase_link_controller import RulebaseLinkController
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanagerlist import FwConfigManager
from states.global_state import GlobalState
from states.import_state import ImportState
from states.management_state import ManagementState


# this class is used for importing a config into the FWO API
class FwConfigImport:
    _fw_config_import_rule: FwConfigImportRule | None
    _fw_config_import_object: FwConfigImportObject
    _fw_config_import_gateway: FwConfigImportGateway
    _rb_link_controller: RulebaseLinkController

    @property
    def fwconfig_import_object(self):
        return self._fw_config_import_object

    def __init__(self):
        self._fw_config_import_object = FwConfigImportObject()
        self._fw_config_import_rule = None
        self._fw_config_import_gateway = FwConfigImportGateway()
        self._rb_link_controller = RulebaseLinkController()

    def import_single_config(
        self,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
        single_manager: FwConfigManager,
    ):
        previous_config = self.get_latest_config_from_db(
            management_state=management_state, fwo_api=global_state.fwo_api
        )
        management_state.previous_config = previous_config
        if single_manager.is_super_manager:
            import_state.previous_super_config = previous_config

        self.check_and_fix_db_consistency(import_state, management_state, global_state.fwo_api_call)

        # calculate differences and write them to the database via API
        self.update_diffs(global_state, import_state, management_state, single_manager)

    def import_management_set(
        self, global_state: GlobalState, import_state: ImportState, mgr_set: FwConfigManagerListController
    ):
        for manager in sorted(mgr_set.ManagerSet, key=lambda m: not getattr(m, "IsSuperManager", False)):
            """
            the following loop is a preparation for future functionality
            we might add support for multiple configs per manager
            e.g. one config only adds data, one only deletes data, etc.
            currently we always only have one config per manager
            """
            for config in manager.configs:
                self.import_config(global_state, import_state, manager, config)
        self.update_removed_managers(mgr_set.ManagerSet, import_state, global_state)

    def import_config(
        self,
        global_state: GlobalState,
        import_state: ImportState,
        manager: FwConfigManager,
        config: FwConfigNormalized,
    ):
        mgm_id = import_state.lookup_management_id(manager.manager_uid)

        if mgm_id is None:
            raise FwoImporterError(f"could not find manager id in DB for UID {manager.manager_uid}")

        management_state = ManagementState(import_state, global_state.fwo_api, mgm_id, config, manager.is_super_manager)

        if manager.is_super_manager:
            # store global config as it is needed when importing sub managers which might reference it
            import_state.super_config = config
            import_state.super_uid2id_mapper = management_state.uid2id_mapper

        self._fw_config_import_rule = FwConfigImportRule(global_state, import_state, management_state)

        self.import_single_config(global_state, import_state, management_state, manager)
        self.consistency_check_config_against_db(management_state=management_state, fwo_api=global_state.fwo_api)
        self.write_latest_config(
            global_state=global_state, import_state=import_state, management_state=management_state
        )

    def update_removed_managers(
        self, mgr_set: list[FwConfigManager], import_state: ImportState, global_state: GlobalState
    ):
        """
        Sets removed flag on all db entries associated with sub-managers which are not part of the current import set.
        """
        if not import_state.mgm_details.is_super_manager:
            return  # nothing to do for single management imports
        get_sub_mgrs_query = FwoApi.get_graphql_code(
            [fwo_const.GRAPHQL_QUERY_PATH + "device/getSubManagerUids.graphql"]
        )
        query_variables = {"mgmId": import_state.mgm_details.mgm_id}
        try:
            query_result = global_state.fwo_api.call(get_sub_mgrs_query, query_variables=query_variables)
            if "errors" in query_result:
                raise FwoImporterError(
                    f"failed to get sub manager UIDs for super manager mgm id {import_state.mgm_details.mgm_id!s}: {query_result['errors']!s}"
                )
            mgrs_in_db = query_result["data"]["management"]
        except Exception:
            FWOLogger.exception(
                f"failed to get sub manager UIDs for super manager mgm id {import_state.mgm_details.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to get the sub manager UIDs") from None
        mgr_uids_in_db = {mgr["mgm_uid"] for mgr in mgrs_in_db}
        mgr_uids_in_import = {mgr.manager_uid for mgr in mgr_set}
        mgr_uids_to_remove = list(mgr_uids_in_db - mgr_uids_in_import)
        if not mgr_uids_to_remove:
            return  # nothing to do
        FWOLogger.info(f"marking all entries associated with sub-managers {mgr_uids_to_remove!s} as removed")
        mutation = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "device/markManagersRemoved.graphql"]
        )
        query_variables: dict[str, Any] = {
            "mgmIds": [mgr["mgm_id"] for mgr in mgrs_in_db if mgr["mgm_uid"] in mgr_uids_to_remove],
            "importId": import_state.import_id,
        }
        try:
            result = global_state.fwo_api.call(mutation, query_variables=query_variables)

            affected_tables = {key: value["affected_rows"] for key, value in result["data"].items()}
            FWOLogger.debug(f"marked sub-managers {mgr_uids_to_remove!s} as removed in tables: {affected_tables!s}")
            FWOLogger.info(
                f"marked {sum(affected_tables.values())!s} entries as removed for sub-managers {mgr_uids_to_remove!s}"
            )
            import_state.statistics_controller.statistics.network_object_delete_count += affected_tables.get(
                "update_object", 0
            )
            import_state.statistics_controller.statistics.service_object_delete_count += affected_tables.get(
                "update_service", 0
            )
            import_state.statistics_controller.statistics.user_object_delete_count += affected_tables.get(
                "update_usr", 0
            )
            import_state.statistics_controller.statistics.zone_object_delete_count += affected_tables.get(
                "update_zone", 0
            )
            import_state.statistics_controller.statistics.rule_delete_count += affected_tables.get("update_rule", 0)
            import_state.statistics_controller.statistics.rulebase_delete_count += affected_tables.get(
                "update_rulebase", 0
            )
        except Exception:
            FWOLogger.exception(
                f"failed to mark sub-managers as removed for super manager mgm id {import_state.mgm_details.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to mark sub-managers as removed") from None

    def clear_management(self, import_state: ImportState, global_state: GlobalState) -> FwConfigManagerListController:
        FWOLogger.info('this import run will reset the configuration of this management to "empty"')
        manager_list = FwConfigManagerListController()
        mgm_details = import_state.mgm_details
        # Reset management
        manager_list.add_manager(
            manager=FwConfigManager(
                manager_uid=mgm_details.uid,
                manager_name=mgm_details.name,
                is_super_manager=mgm_details.is_super_manager,
                sub_manager_ids=mgm_details.sub_manager_ids,
                domain_name=mgm_details.domain_name,
                domain_uid=mgm_details.domain_uid,
                configs=[],
            )
        )
        if len(import_state.mgm_details.sub_manager_ids) > 0:
            # Reset submanagement
            for sub_manager_id in import_state.mgm_details.sub_manager_ids:
                # Fetch sub management details

                mgm_details_raw = ManagementController.get_mgm_details(global_state.fwo_api, sub_manager_id)
                mgm_details = ManagementController.from_json(mgm_details_raw)
                manager_list.add_manager(
                    manager=FwConfigManager(
                        manager_uid=mgm_details.uid,
                        manager_name=mgm_details.name,
                        is_super_manager=mgm_details.is_super_manager,
                        sub_manager_ids=mgm_details.sub_manager_ids,
                        domain_name=mgm_details.domain_name,
                        domain_uid=mgm_details.domain_uid,
                        configs=[],
                    )
                )
        # Reset objects
        for management in manager_list.ManagerSet:
            management.configs.append(
                FwConfigNormalized(
                    action=ConfigAction.INSERT,
                    network_objects={},
                    service_objects={},
                    users={},
                    zone_objects={},
                    rulebases=[],
                    gateways=[],
                )
            )

        return manager_list

    def update_diffs(
        self,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
        single_manager: FwConfigManager,
    ):
        self._fw_config_import_object.update_object_diffs(
            global_state,
            import_state,
            management_state,
            single_manager,
        )

        if fwo_globals.shutdown_requested:
            raise ImportInterruptionError("Shutdown requested during updateObjectDiffs.")

        if self._fw_config_import_rule is None:
            raise FwoImporterError("FwConfigImportRule is not initialized")
        self._fw_config_import_rule.update_rulebase_diffs(management_state.previous_config)

        if fwo_globals.shutdown_requested:
            raise ImportInterruptionError("Shutdown requested during updateRulebaseDiffs.")

        self._fw_config_import_gateway.update_gateway_diffs(
            global_state, import_state, management_state, self._rb_link_controller
        )

    # cleanup configs which do not need to be retained according to data retention time
    def delete_old_imports(self, import_state: ImportState, fwo_api_call: FwoApiCall) -> None:
        mgm_id = int(import_state.mgm_details.mgm_id)
        delete_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/deleteOldImports.graphql"])

        try:
            delete_result = fwo_api_call.call(
                delete_mutation,
                query_variables={"mgmId": mgm_id},
            )
            if delete_result["data"]["delete_import_control"]["returning"]["control_id"]:
                imports_deleted = len(delete_result["data"]["delete_import_control"]["returning"]["control_id"])
                if imports_deleted > 0:
                    FWOLogger.info(
                        f"deleted {imports_deleted!s} imports which passed the retention time of {import_state.data_retention_days} days"
                    )
        except Exception:
            FWOLogger.error(f"error while trying to delete old imports for mgm {import_state.mgm_details.mgm_id!s}")
            fwo_api_call.create_data_issue(
                mgm_id=import_state.mgm_details.mgm_id,
                severity=1,
                description="failed to delete old imports for management id " + str(mgm_id),
            )
            fwo_api_call.set_alert(
                import_id=import_state.import_id,
                title="import error",
                mgm_id=mgm_id,
                severity=1,
                description="fwo_api: failed to delete old imports for management id " + str(mgm_id),
                source="import",
                alert_code=15,
                mgm_details=import_state.mgm_details,
            )
            raise FwoApiFailedDeleteOldImportsError(f"management id: {mgm_id}") from None

    def write_latest_config(
        self, global_state: GlobalState, import_state: ImportState, management_state: ManagementState
    ) -> None:
        if global_state.importer_version > 8:  # noqa: PLR2004
            if management_state.normalized_config is None:
                raise FwoImporterError("cannot write latest config: NormalizedConfig is None")
            # convert FwConfigImport to FwConfigNormalized

            self.delete_latest_config_of_management(global_state, management_state)
            insert_mutation = FwoApi.get_graphql_code(
                [fwo_const.GRAPHQL_QUERY_PATH + "import/storeLatestConfig.graphql"]
            )
            try:
                query_variables: dict[str, Any] = {
                    "mgmId": management_state.mgm_id,
                    "importId": import_state.import_id,
                    "config": management_state.normalized_config.model_dump_json(),
                }
                import_result = global_state.fwo_api.call(insert_mutation, query_variables=query_variables)
                if "errors" in import_result:
                    FWOLogger.exception(
                        "fwo_api:storeLatestConfig - error while writing importable config for mgm id "
                        + str(management_state.mgm_id)
                        + ": "
                        + str(import_result["errors"])
                    )
                    FWOLogger.warning(
                        f"error while writing latest config for import_id {import_state.import_id}, mgm_id: {management_state.mgm_id}, mgm_uid: {import_state.mgm_details.uid}"
                    )
                else:
                    _ = import_result["data"]["insert_latest_config"]["affected_rows"]
            except Exception:
                FWOLogger.exception(
                    f"failed to write latest normalized config for mgm id {management_state.mgm_id!s}: {traceback.format_exc()!s}"
                )
                raise

    def delete_latest_config_of_management(self, global_state: GlobalState, management_state: ManagementState) -> None:
        delete_mutation = FwoApi.get_graphql_code(
            [fwo_const.GRAPHQL_QUERY_PATH + "import/deleteLatestConfigOfManagement.graphql"]
        )
        try:
            query_variables = {"mgmId": management_state.mgm_id}
            import_result = global_state.fwo_api.call(delete_mutation, query_variables=query_variables)
            if "errors" in import_result:
                FWOLogger.exception(
                    "fwo_api:import_latest_config - error while deleting last config for mgm id "
                    + str(management_state.mgm_id)
                    + ": "
                    + str(import_result["errors"])
                )
            else:
                _ = import_result["data"]["delete_latest_config"]["affected_rows"]
        except Exception:
            FWOLogger.exception(
                f"failed to delete latest normalized config for mgm id {management_state.mgm_id!s}: {traceback.format_exc()!s}"
            )

    def get_latest_import_id(self, import_state: ImportState, fwo_api: FwoApi) -> int | None:
        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/getLastSuccessImport.graphql"])
        query_variables = {"mgmId": import_state.mgm_details.mgm_id}
        try:
            query_result = fwo_api.call(query, query_variables=query_variables)
            if "errors" in query_result:
                raise FwoImporterError(
                    f"failed to get latest import id for mgm id {import_state.mgm_details.mgm_id!s}: {query_result['errors']!s}"
                )
            if len(query_result["data"]["import_control"]) == 0:
                return None
            return query_result["data"]["import_control"][0]["control_id"]
        except Exception:
            FWOLogger.exception(
                f"failed to get latest import id for mgm id {import_state.mgm_details.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to get the latest import id")

    # return previous config or empty config if there is none; only returns the config of a single management
    def get_latest_config(
        self, import_state: ImportState, management_state: ManagementState, fwo_api: FwoApi
    ) -> FwConfigNormalized:
        prev_config = FwConfigNormalized()

        latest_import_id = self.get_latest_import_id(import_state, fwo_api)
        if latest_import_id is None:
            FWOLogger.info(
                f"first import - no existing import was found for mgm id {management_state.mgm_id}"
            )  # TODO: change msg
            return prev_config

        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/getLatestConfig.graphql"])
        query_variables = {"mgmId": management_state.mgm_id}
        try:
            query_result = fwo_api.call(query, query_variables=query_variables)
            if "errors" in query_result:
                raise FwoImporterError(
                    f"failed to get latest config for mgm id {import_state.mgm_details.mgm_id!s}: {query_result['errors']!s}"
                )
            if len(query_result["data"]["latest_config"]) > 0:  # do we have a prev config?
                if query_result["data"]["latest_config"][0]["import_id"] == latest_import_id:
                    return FwConfigNormalized.model_validate_json(query_result["data"]["latest_config"][0]["config"])
                FWOLogger.warning(
                    f"fwo_api:import_latest_config - latest config for mgm id {management_state.mgm_id} did not match last import id {latest_import_id}"
                )
            FWOLogger.info("fetching latest config from DB as fallback")
            return self.get_latest_config_from_db(management_state, fwo_api)
        except Exception:
            FWOLogger.exception(
                f"failed to get latest normalized config for mgm id {management_state.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to get the previous config")

    def get_latest_config_from_db(self, management_state: ManagementState, fwo_api: FwoApi) -> FwConfigNormalized:
        params = {"mgm-ids": [management_state.mgm_id]}
        result = fwo_api.call_endpoint("POST", "api/NormalizedConfig/Get", params=params)
        try:
            return FwConfigNormalized.model_validate(result)
        except Exception:
            FWOLogger.exception(
                f"failed to get latest normalized config from db for mgm id {management_state.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to get the latest config")

    def _sort_lists(self, config: FwConfigNormalized):
        # sort lists in config to have consistent ordering for diff checks
        config.rulebases.sort(key=lambda rb: rb.uid)
        if any(gw.Uid is None for gw in config.gateways):
            raise FwoImporterError(
                "found gateway without UID while sorting gateways for consistency check - this should not happen"
            )
        config.gateways.sort(key=lambda gw: gw.Uid or "")
        for gw in config.gateways:
            gw.RulebaseLinks.sort(key=lambda rbl: f"{rbl.from_rulebase_uid}-{rbl.from_rule_uid}-{rbl.to_rulebase_uid}")
            if gw.EnforcedPolicyUids is not None:
                gw.EnforcedPolicyUids.sort()
            if gw.EnforcedNatPolicyUids is not None:
                gw.EnforcedNatPolicyUids.sort()
            # TODO: interfaces and routing as soon as they are implemented

    def consistency_check_config_against_db(self, management_state: ManagementState, fwo_api: FwoApi):
        normalized_config = management_state.normalized_config
        if normalized_config is None:
            raise FwoImporterError("cannot perform consistency check: NormalizedConfig is None")
        normalized_config_from_db = self.get_latest_config_from_db(management_state, fwo_api)
        self._sort_lists(normalized_config)
        self._sort_lists(normalized_config_from_db)
        # filter gateways from DB which are not part of the current import (e.g. in case of import_disabled)
        normalized_config_from_db.gateways = [
            gw
            for gw in normalized_config_from_db.gateways
            if any(gw.Uid == imported_gw.Uid for imported_gw in normalized_config.gateways)
        ]
        all_diffs = find_all_diffs(normalized_config.model_dump(), normalized_config_from_db.model_dump(), strict=True)
        if len(all_diffs) > 0:
            FWOLogger.warning(
                f"normalized config for mgm id {management_state.mgm_id} is inconsistent to database state: {all_diffs[0]}"
            )
            FWOLogger.debug(f"all {len(all_diffs)} differences:\n\t" + "\n\t".join(all_diffs))
            # TODO: long-term this should raise an error:

    def check_and_fix_db_consistency(
        self,
        import_state: ImportState,
        management_state: ManagementState,
        fwo_api_call: FwoApiCall,
    ):
        """
        Check consistency of the latest config (=previous config) built from database state before import.
        If inconsistencies are found, they will be fixed in the database by marking objects/rules/links as removed.
        """
        consistency_checker = FwConfigImportCheckConsistency(import_state)
        consistency_checker.check_config_consistency(
            management_state.normalized_config, import_state.previous_super_config, fix_config=True
        )
        self.fix_objects_in_db(
            import_state,
            management_state,
            fwo_api_call,
            consistency_checker.network_objects_to_remove,
            consistency_checker.service_objects_to_remove,
            consistency_checker.user_objects_to_remove,
        )
        self.fix_rules_in_db(import_state, management_state, fwo_api_call, consistency_checker.rules_to_remove)
        if consistency_checker.invalid_rulebase_links_exist:
            self.fix_rulebase_links_in_db(import_state, management_state, fwo_api_call)
        self.fix_rule_to_gw_refs_in_db(import_state, management_state, fwo_api_call)
        self.fix_ref_tables_in_db(import_state, management_state, fwo_api_call)
        self.fix_changelog_rule(management_state, fwo_api_call)

    def fix_objects_in_db(
        self,
        import_state: ImportState,
        management_state: ManagementState,
        fwo_api_call: FwoApiCall,
        nwobj_uids: list[str],
        svcobj_uids: list[str],
        user_uids: list[str],
    ):
        """
        Sets removed flag on network objects, service objects and user objects with the given UIDs in the database
        to fix consistency issues.
        """
        if not nwobj_uids and not svcobj_uids and not user_uids:
            return  # nothing to do

        mutation = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "allObjects/upsertObjects.graphql"]
        )

        query_variables: dict[str, Any] = {
            "mgmId": management_state.mgm_id,
            "importId": import_state.import_id,
            "newNwObjects": [],
            "newSvcObjects": [],
            "newUsers": [],
            "newZones": [],
            "removedNwObjectUids": nwobj_uids,
            "removedSvcObjectUids": svcobj_uids,
            "removedUserUids": user_uids,
            "removedZoneUids": [],
        }

        try:
            result = fwo_api_call.call(mutation, query_variables=query_variables, analyze_payload=True)

            removed_nwobj_ids = result["data"]["update_object"]["returning"]
            removed_nwsvc_ids = result["data"]["update_service"]["returning"]
            removed_user_ids = result["data"]["update_usr"]["returning"]
            FWOLogger.info(
                f"removed {len(removed_nwobj_ids)!s} network objects, {len(removed_nwsvc_ids)!s} service objects and {len(removed_user_ids)!s} user objects from DB to fix consistency issues"
            )
            import_state.statistics_controller.statistics.inconsistent_nwobj_delete_count += len(removed_nwobj_ids)
            import_state.statistics_controller.statistics.inconsistent_svcobj_delete_count += len(removed_nwsvc_ids)
            import_state.statistics_controller.statistics.inconsistent_userobj_delete_count += len(removed_user_ids)
        except Exception:
            FWOLogger.exception(
                f"failed to fix object consistency issues for mgm id {management_state.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to fix object consistency issues") from None

    def fix_rules_in_db(
        self,
        import_state: ImportState,
        management_state: ManagementState,
        fwo_api_call: FwoApiCall,
        rule_uids: list[str],
    ):
        """
        Sets removed flag on rules with the given UIDs in the database to fix consistency issues.
        """
        if not rule_uids:
            return  # nothing to do

        mutation = """
            mutation markRulesRemoved($importId: bigint!, $mgmId: Int!, $uids: [String!]!) {
                update_rule(where: {removed: { _is_null: true }, rule_uid: {_in: $uids}, mgm_id: {_eq: $mgmId}}, _set: {removed: $importId}) {
                    affected_rows
                    returning { rule_id }
                }
            }
        """
        query_variables: dict[str, Any] = {
            "mgmId": management_state.mgm_id,
            "importId": import_state.import_id,
            "uids": rule_uids,
        }
        try:
            result = fwo_api_call.call(mutation, query_variables=query_variables, analyze_payload=True)

            removed_rule_ids = result["data"]["update_rule"]["returning"]
            FWOLogger.info(f"marked {len(removed_rule_ids)!s} rules as removed in DB to fix consistency issues")
            import_state.statistics_controller.statistics.inconsistent_rule_delete_count += len(removed_rule_ids)
        except Exception:
            FWOLogger.exception(
                f"failed to fix rule consistency issues for mgm id {management_state.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to fix rule consistency issues") from None

    def fix_rulebase_links_in_db(
        self, import_state: ImportState, management_state: ManagementState, fwo_api_call: FwoApiCall
    ):
        """
        Removes inconsistent rulebase links from the database to fix consistency issues.
        """
        mutation = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "rule/removeInconsistentRulebaseLinks.graphql"]
        )
        query_variables: dict[str, Any] = {
            "mgmId": management_state.mgm_id,
            "importId": import_state.import_id,
        }
        try:
            result = fwo_api_call.call(mutation, query_variables=query_variables)

            removed_links = result["data"]["update_rulebase_link"]["affected_rows"]
            FWOLogger.info(f"removed {removed_links!s} inconsistent rulebase links from DB to fix consistency issues")
            import_state.statistics_controller.statistics.inconsistent_rulebase_link_delete_count += removed_links
        except Exception:
            FWOLogger.exception(
                f"failed to remove inconsistent rulebase links for mgm id {management_state.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to remove inconsistent rulebase links") from None

    def _insert_missing_rule_to_gw_refs_in_db(
        self,
        refs_to_add: set[tuple[str, str]],
        import_state: ImportState,
        management_state: ManagementState,
        fwo_api_call: FwoApiCall,
    ):
        """Inserts missing rule enforced on gateway references to the database to fix consistency issues."""
        if not refs_to_add:
            return  # nothing to do
        mgm_id = management_state.mgm_id
        fetch_rule_ids_query = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "rule/getRulesByUidsWithCreate.graphql"]
        )
        fetch_rule_ids_variables: dict[str, Any] = {
            "mgmId": mgm_id,
            "uids": [rule_uid for rule_uid, _gw_uid in refs_to_add],
        }
        try:
            fetch_rule_ids_result = fwo_api_call.call(
                fetch_rule_ids_query, query_variables=fetch_rule_ids_variables, analyze_payload=True
            )
            if "errors" in fetch_rule_ids_result:
                raise FwoImporterError(
                    f"failed to fetch rule ids for rule UIDs {fetch_rule_ids_variables['uids']!s} for mgm id {mgm_id!s}: {fetch_rule_ids_result['errors']!s}"
                )
            rule_uid_to_id_create = {
                rule["rule_uid"]: (rule["rule_id"], rule["rule_create"])
                for rule in fetch_rule_ids_result["data"]["rule"]
            }
        except Exception:
            FWOLogger.exception(
                f"failed to fetch rule ids for rule UIDs {fetch_rule_ids_variables['uids']!s} for mgm id {mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to fetch rule ids for rule UIDs") from None
        mutation = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "rule/insertRuleEnforcedOnGateway.graphql"]
        )
        query_variables: dict[str, Any] = {
            "rulesEnforcedOnGateway": [
                {
                    "rule_id": rule_uid_to_id_create[rule_uid][0],
                    "dev_id": import_state.lookup_gateway_id(gw_uid, mgm_id),
                    "created": rule_uid_to_id_create[rule_uid][1],
                }
                for rule_uid, gw_uid in refs_to_add
            ],
        }
        try:
            result = fwo_api_call.call(mutation, query_variables=query_variables, analyze_payload=True)

            added_refs = result["data"]["insert_rule_enforced_on_gateway"]["affected_rows"]
            FWOLogger.info(
                f"added {added_refs!s} missing rule enforced on gateway references to DB to fix consistency issues"
            )
        except Exception:
            FWOLogger.exception(
                f"failed to add missing rule enforced on gateway references for mgm id {management_state.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to add missing rule enforced on gateway references") from None

    def fix_rule_to_gw_refs_in_db(
        self, import_state: ImportState, management_state: ManagementState, fwo_api_call: FwoApiCall
    ):
        """
        Set inconsistent rule_enforced_on_gateway entries removed and insert missing ones.
        """
        if management_state.previous_config is None:
            return  # nothing to compare against, so nothing to fix
        mgm_id = management_state.mgm_id
        if mgm_id not in import_state.gateway_map:
            # no gateways assigned to management (e.g. super-mgr)
            return
        gw_ids = import_state.lookup_all_gateway_ids(mgm_id)
        query = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "rule/getRulesEnforcedOnGateways.graphql"]
        )
        query_variables: dict[str, Any] = {
            "gwIds": gw_ids,
        }
        try:
            result = fwo_api_call.call(query, query_variables=query_variables)
            if "errors" in result:
                raise FwoImporterError(
                    f"failed to get rules enforced on gateways for mgm id {management_state.mgm_id!s}: {result['errors']!s}"
                )
            rules_enforced_on_gw = result["data"]["rule_enforced_on_gateway"]
        except Exception:
            FWOLogger.exception(
                f"failed to get rules enforced on gateways for mgm id {management_state.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to get rules enforced on gateways") from None
        # need to set removed flag on active refs referencing removed rule
        ref_with_removed_rule_exists = any(ref for ref in rules_enforced_on_gw if ref["rule"]["removed"] is not None)
        # comparing expected refs from config with existing refs to *active* rules to determine missing refs to add
        expected_refs = FwConfigImportRule.get_rule_to_gw_refs(
            management_state.previous_config.rulebases,
            import_state.previous_super_config.rulebases if import_state.previous_super_config else None,
            management_state.previous_config.gateways,
        )
        refs_in_db_active_rule = {
            (ref["rule"]["rule_uid"], ref["device"]["dev_uid"])
            for ref in rules_enforced_on_gw
            if ref["rule"]["removed"] is None
        }
        refs_to_add = expected_refs - refs_in_db_active_rule
        # Note: incorrect entries referencing *active* rules will not be fixed here.
        unexpected_refs_in_db = sum(
            1
            for ref in rules_enforced_on_gw
            if ref["rule"]["removed"] is None
            and (ref["rule"]["rule_uid"], ref["device"]["dev_uid"]) not in expected_refs
        )
        if unexpected_refs_in_db > 0:
            FWOLogger.warning(
                f"{unexpected_refs_in_db} inconsistent rule enforced on gateway refs cannot be removed as they reference active rules"
            )
        if ref_with_removed_rule_exists:
            mutation = FwoApi.get_graphql_code(
                file_list=[fwo_const.GRAPHQL_QUERY_PATH + "rule/removeInconsistentEnforcedOnGateways.graphql"]
            )
            query_variables: dict[str, Any] = {
                "gwIds": gw_ids,
                "importId": import_state.import_id,
            }
            try:
                result = fwo_api_call.call(mutation, query_variables=query_variables)
                if "errors" in result:
                    raise FwoImporterError(
                        f"failed to remove inconsistent rule enforced on gateway references for mgm id {management_state.mgm_id!s}: {result['errors']!s}"
                    )
                removed_refs = result["data"]["update_rule_enforced_on_gateway"]["affected_rows"]
                FWOLogger.info(
                    f"removed {removed_refs!s} inconsistent rule enforced on gateway references from DB to fix consistency issues"
                )
                import_state.statistics_controller.statistics.inconsistent_ref_delete_count += removed_refs
            except Exception:
                FWOLogger.exception(
                    f"failed to remove inconsistent rule enforced on gateway references for mgm id {management_state.mgm_id!s}: {traceback.format_exc()!s}"
                )
                raise FwoImporterError(
                    "error while trying to remove inconsistent rule enforced on gateway references"
                ) from None

        if refs_to_add:
            self._insert_missing_rule_to_gw_refs_in_db(refs_to_add, import_state, management_state, fwo_api_call)

    def fix_ref_tables_in_db(
        self, import_state: ImportState, management_state: ManagementState, fwo_api_call: FwoApiCall
    ):
        """
        Check ref tables for active references to objects/rules which were marked as removed and remove these
        references to fix consistency issues.
        """
        mutation = FwoApi.get_graphql_code(file_list=[fwo_const.GRAPHQL_QUERY_PATH + "allObjects/fixRefTables.graphql"])
        query_variables: dict[str, Any] = {
            "mgmId": management_state.mgm_id,
            "importId": import_state.import_id,
        }
        try:
            result = fwo_api_call.call(mutation, query_variables=query_variables)

            affected_rows = {key: value["affected_rows"] for key, value in result["data"].items()}
            if sum(affected_rows.values()) > 0:
                FWOLogger.info(
                    f"fixed references to removed objects/rules in ref tables to fix consistency issues: {affected_rows!s}"
                )
            import_state.statistics_controller.statistics.inconsistent_ref_delete_count += sum(affected_rows.values())
        except Exception:
            FWOLogger.exception(
                f"failed to fix references to removed objects/rules in ref tables for mgm id {management_state.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError(
                "error while trying to fix references to removed objects/rules in ref tables"
            ) from None

    def fix_changelog_rule(self, management_state: ManagementState, fwo_api_call: FwoApiCall):
        """
        Fix changelog entries with old_rule_id == new_rule_id (both containing new_rule_id due to a bug in the past)
        """
        get_changelog_entries_query = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "rule/getChangelogRulesCForMgm.graphql"]
        )
        query_variables: dict[str, Any] = {
            "mgmId": management_state.mgm_id,
        }
        try:
            result = fwo_api_call.call(get_changelog_entries_query, query_variables=query_variables)
            if "errors" in result:
                raise FwoImporterError(
                    f"failed to get changelog entries for mgm id {management_state.mgm_id!s}: {result['errors']!s}"
                )
            changelog_entries = result["data"]["changelog_rule"]
            entries_to_fix = [
                entry
                for entry in changelog_entries
                if entry["new_rule_id"] is not None
                and entry["old_rule_id"] is not None
                and entry["new_rule_id"] == entry["old_rule_id"]
            ]
            if not entries_to_fix:
                return  # nothing to fix
            FWOLogger.info(
                f"found {len(entries_to_fix)!s} changelog entries with identical new and old rule id for mgm id {management_state.mgm_id!s}, fixing these entries now"
            )
            # get correct old rule ids
            get_rule_ids_mutation = FwoApi.get_graphql_code(
                file_list=[fwo_const.GRAPHQL_QUERY_PATH + "rule/getRulesByUidsForMgm.graphql"]
            )
            get_rule_ids_variables: dict[str, Any] = {
                "mgmId": management_state.mgm_id,
                "ruleUids": list({entry["rule"]["rule_uid"] for entry in entries_to_fix}),
            }
            get_rule_ids_result = fwo_api_call.call(
                get_rule_ids_mutation, query_variables=get_rule_ids_variables, analyze_payload=True
            )
            if "errors" in get_rule_ids_result:
                raise FwoImporterError(
                    f"failed to get rule ids for UIDs of changelog entries to fix for mgm id {management_state.mgm_id!s}: {get_rule_ids_result['errors']!s}"
                )
            correct_old_rule_ids: dict[int, int] = {}
            for entry in entries_to_fix:
                rule_uid = entry["rule"]["rule_uid"]
                new_rule_id = entry["new_rule_id"]
                correct_old_rule_id = max(
                    rule["rule_id"]
                    for rule in get_rule_ids_result["data"]["rule"]
                    if rule["rule_uid"] == rule_uid and rule["rule_id"] < new_rule_id
                )
                correct_old_rule_ids[entry["log_rule_id"]] = correct_old_rule_id
            changelog_rule_updates = {
                "updates": [
                    {"where": {"log_rule_id": {"_eq": log_rule_id}}, "_set": {"old_rule_id": old_rule_id}}
                    for log_rule_id, old_rule_id in correct_old_rule_ids.items()
                ]
            }
            update_changelog_entries_mutation = FwoApi.get_graphql_code(
                file_list=[fwo_const.GRAPHQL_QUERY_PATH + "rule/updateChangelogRuleEntries.graphql"]
            )
            update_result = fwo_api_call.call(
                update_changelog_entries_mutation, query_variables=changelog_rule_updates, analyze_payload=True
            )
            if "errors" in update_result:
                raise FwoImporterError(
                    f"failed to update changelog entries with correct old rule ids for mgm id {management_state.mgm_id!s}: {update_result['errors']!s}"
                )
            updated_entries = sum(
                update["affected_rows"] for update in update_result["data"]["update_changelog_rule_many"]
            )
            FWOLogger.info(
                f"updated {updated_entries!s} changelog entries with correct old rule ids for mgm id {management_state.mgm_id!s}"
            )
        except Exception:
            FWOLogger.exception(
                f"failed to fix changelog entries with identical new and old rule id for mgm id {management_state.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError(
                "error while trying to fix changelog entries with identical new and old rule id"
            ) from None

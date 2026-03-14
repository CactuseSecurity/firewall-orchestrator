import traceback
from typing import Any

import fwo_const
import fwo_globals
from fwo_api import FwoApi
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
from model_controllers.rule_enforced_on_gateway_controller import RuleEnforcedOnGatewayController
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanagerlist import FwConfigManager
from states.global_state import GlobalState
from states.import_state import ImportState
from states.management_state import ManagementState


# this class is used for importing a config into the FWO API
class FwConfigImport:
    _fw_config_import_rule: FwConfigImportRule
    _fw_config_import_object: FwConfigImportObject
    _fw_config_import_gateway: FwConfigImportGateway

    @property
    def fwconfig_import_object(self):
        return self._fw_config_import_object

    def __init__(self):
        self._fw_config_import_object = FwConfigImportObject()
        self._fw_config_import_rule = FwConfigImportRule()
        self._fw_config_import_gateway = FwConfigImportGateway()

    def import_single_config(
        self,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
        single_manager: FwConfigManager,
    ):
        previous_config = self.get_latest_config_from_db(import_state=import_state)
        management_state.previous_config = previous_config
        if single_manager.is_super_manager:
            import_state.previous_super_config = previous_config

        self.check_and_fix_db_consistency(import_state, management_state)

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

    def import_config(
        self,
        global_state: GlobalState,
        import_state: ImportState,
        manager: FwConfigManager,
        config: FwConfigNormalized,
    ):
        if manager.is_super_manager:
            # store global config as it is needed when importing sub managers which might reference it
            import_state.super_config = config
        mgm_id = import_state.lookup_management_id(manager.manager_uid)

        if mgm_id is None:
            raise FwoImporterError(f"could not find manager id in DB for UID {manager.manager_uid}")

        management_state = ManagementState(mgm_id)
        # TODO: clean separation between values relevant for all managers and those only relevant for specific managers - see #3646
        import_state.mgm_details.current_mgm_id = mgm_id
        import_state.mgm_details.current_mgm_is_super_manager = manager.is_super_manager
        self.import_single_config(global_state, import_state, management_state, manager)
        self.consistency_check_config_against_db(import_state=import_state, management_state=management_state)
        self.write_latest_config(import_state=import_state, management_state=management_state)

    def clear_management(self, import_state: ImportState) -> FwConfigManagerListController:
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

                mgm_details_raw = ManagementController.get_mgm_details(import_state.fwo_api, sub_manager_id)
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

        new_rule_ids = self._fw_config_import_rule.update_rulebase_diffs(management_state.previous_config)

        if fwo_globals.shutdown_requested:
            raise ImportInterruptionError("Shutdown requested during updateRulebaseDiffs.")

        self._fw_config_import_gateway.update_gateway_diffs(import_state)

        # get new rules details from API (for obj refs as well as enforcing gateways)
        new_rules = self._fw_config_import_rule.get_rules_by_id_with_ref_uids(new_rule_ids)

        RuleEnforcedOnGatewayController().add_new_rule_enforced_on_gateway_refs(new_rules, import_state)

    # cleanup configs which do not need to be retained according to data retention time
    def delete_old_imports(self, global_state: GlobalState, import_state: ImportState) -> None:
        mgm_id = int(import_state.mgm_details.mgm_id)
        delete_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/deleteOldImports.graphql"])

        try:
            delete_result = import_state.fwo_api_call.call(
                delete_mutation,
                query_variables={
                    "mgmId": mgm_id,
                    "is_full_import": global_state.fwo_config_controller.fwo_config.is_full_import,
                },
            )
            if delete_result["data"]["delete_import_control"]["returning"]["control_id"]:
                imports_deleted = len(delete_result["data"]["delete_import_control"]["returning"]["control_id"])
                if imports_deleted > 0:
                    FWOLogger.info(
                        f"deleted {imports_deleted!s} imports which passed the retention time of {import_state.data_retention_days} days"
                    )
        except Exception:
            fwo_api_call = import_state.fwo_api_call
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

    def write_latest_config(self, import_state: ImportState, management_state: ManagementState) -> None:
        if import_state.import_version > 8:  # noqa: PLR2004
            if management_state.normalized_config is None:
                raise FwoImporterError("cannot write latest config: NormalizedConfig is None")
            # convert FwConfigImport to FwConfigNormalized

            self.delete_latest_config_of_management(import_state)
            insert_mutation = FwoApi.get_graphql_code(
                [fwo_const.GRAPHQL_QUERY_PATH + "import/storeLatestConfig.graphql"]
            )
            try:
                query_variables: dict[str, Any] = {
                    "mgmId": import_state.mgm_details.current_mgm_id,
                    "importId": import_state.import_id,
                    "config": management_state.normalized_config.model_dump_json(),
                }
                import_result = import_state.fwo_api.call(insert_mutation, query_variables=query_variables)
                if "errors" in import_result:
                    FWOLogger.exception(
                        "fwo_api:storeLatestConfig - error while writing importable config for mgm id "
                        + str(import_state.mgm_details.current_mgm_id)
                        + ": "
                        + str(import_result["errors"])
                    )
                    FWOLogger.warning(
                        f"error while writing latest config for import_id {import_state.import_id}, mgm_id: {import_state.mgm_details.mgm_id}, mgm_uid: {import_state.mgm_details.uid}"
                    )
                else:
                    _ = import_result["data"]["insert_latest_config"]["affected_rows"]
            except Exception:
                FWOLogger.exception(
                    f"failed to write latest normalized config for mgm id {import_state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
                )
                raise

    def delete_latest_config_of_management(self, import_state: ImportState) -> None:
        delete_mutation = FwoApi.get_graphql_code(
            [fwo_const.GRAPHQL_QUERY_PATH + "import/deleteLatestConfigOfManagement.graphql"]
        )
        try:
            query_variables = {"mgmId": import_state.mgm_details.current_mgm_id}
            import_result = import_state.fwo_api.call(delete_mutation, query_variables=query_variables)
            if "errors" in import_result:
                FWOLogger.exception(
                    "fwo_api:import_latest_config - error while deleting last config for mgm id "
                    + str(import_state.mgm_details.current_mgm_id)
                    + ": "
                    + str(import_result["errors"])
                )
            else:
                _ = import_result["data"]["delete_latest_config"]["affected_rows"]
        except Exception:
            FWOLogger.exception(
                f"failed to delete latest normalized config for mgm id {import_state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )

    def get_latest_import_id(self, import_state: ImportState) -> int | None:
        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/getLastSuccessImport.graphql"])
        query_variables = {"mgmId": import_state.mgm_details.mgm_id}
        try:
            query_result = import_state.fwo_api.call(query, query_variables=query_variables)
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
    def get_latest_config(self, import_state: ImportState) -> FwConfigNormalized:
        mgm_id = import_state.mgm_details.current_mgm_id
        prev_config = FwConfigNormalized()

        latest_import_id = self.get_latest_import_id(import_state)
        if latest_import_id is None:
            FWOLogger.info(f"first import - no existing import was found for mgm id {mgm_id}")  # TODO: change msg
            return prev_config

        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/getLatestConfig.graphql"])
        query_variables = {"mgmId": mgm_id}
        try:
            query_result = import_state.fwo_api.call(query, query_variables=query_variables)
            if "errors" in query_result:
                raise FwoImporterError(
                    f"failed to get latest config for mgm id {import_state.mgm_details.mgm_id!s}: {query_result['errors']!s}"
                )
            if len(query_result["data"]["latest_config"]) > 0:  # do we have a prev config?
                if query_result["data"]["latest_config"][0]["import_id"] == latest_import_id:
                    return FwConfigNormalized.model_validate_json(query_result["data"]["latest_config"][0]["config"])
                FWOLogger.warning(
                    f"fwo_api:import_latest_config - latest config for mgm id {mgm_id} did not match last import id {latest_import_id}"
                )
            FWOLogger.info("fetching latest config from DB as fallback")
            return self.get_latest_config_from_db(import_state)
        except Exception:
            FWOLogger.exception(
                f"failed to get latest normalized config for mgm id {import_state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to get the previous config")

    def get_latest_config_from_db(self, import_state: ImportState) -> FwConfigNormalized:
        params = {"mgm-ids": [import_state.mgm_details.current_mgm_id]}
        result = import_state.fwo_api.call_endpoint("POST", "api/NormalizedConfig/Get", params=params)
        try:
            return FwConfigNormalized.model_validate(result)
        except Exception:
            FWOLogger.exception(
                f"failed to get latest normalized config from db for mgm id {import_state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
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

    def consistency_check_config_against_db(self, import_state: ImportState, management_state: ManagementState):
        normalized_config = management_state.normalized_config
        if normalized_config is None:
            raise FwoImporterError("cannot perform consistency check: NormalizedConfig is None")
        normalized_config_from_db = self.get_latest_config_from_db(import_state)
        self._sort_lists(normalized_config)
        self._sort_lists(normalized_config_from_db)
        all_diffs = find_all_diffs(normalized_config.model_dump(), normalized_config_from_db.model_dump(), strict=True)
        if len(all_diffs) > 0:
            FWOLogger.warning(
                f"normalized config for mgm id {import_state.mgm_details.current_mgm_id} is inconsistent to database state: {all_diffs[0]}"
            )
            FWOLogger.debug(f"all {len(all_diffs)} differences:\n\t" + "\n\t".join(all_diffs))
            # TODO: long-term this should raise an error:

    def check_and_fix_db_consistency(
        self,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        """
        Check consistency of the imported config against the previous config from the database.
        If inconsistencies are found, they will be fixed in the database by marking objects/rules/links as removed.
        """
        consistency_checker = FwConfigImportCheckConsistency(import_state)
        consistency_checker.check_config_consistency(
            management_state.normalized_config, import_state.previous_super_config, fix_config=True
        )
        self.fix_objects_in_db(
            import_state,
            consistency_checker.network_objects_to_remove,
            consistency_checker.service_objects_to_remove,
            consistency_checker.user_objects_to_remove,
        )
        self.fix_rules_in_db(import_state, consistency_checker.rules_to_remove)
        if consistency_checker.invalid_rulebase_links_exist:
            self.fix_rulebase_links_in_db(import_state)

    def fix_objects_in_db(
        self, import_state: ImportState, nwobj_uids: list[str], svcobj_uids: list[str], user_uids: list[str]
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
            "mgmId": import_state.mgm_details.current_mgm_id,
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
            result = import_state.fwo_api_call.call(mutation, query_variables=query_variables)

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
                f"failed to fix object consistency issues for mgm id {import_state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to fix object consistency issues") from None

    def fix_rules_in_db(self, import_state: ImportState, rule_uids: list[str]):
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
            "mgmId": import_state.mgm_details.current_mgm_id,
            "importId": import_state.import_id,
            "uids": rule_uids,
        }
        try:
            result = import_state.fwo_api_call.call(mutation, query_variables=query_variables)

            removed_rule_ids = result["data"]["update_rule"]["returning"]
            FWOLogger.info(f"marked {len(removed_rule_ids)!s} rules as removed in DB to fix consistency issues")
            import_state.statistics_controller.statistics.inconsistent_rule_delete_count += len(removed_rule_ids)
        except Exception:
            FWOLogger.exception(
                f"failed to fix rule consistency issues for mgm id {import_state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to fix rule consistency issues") from None

    def fix_rulebase_links_in_db(self, import_state: ImportState):
        """
        Removes inconsistent rulebase links from the database to fix consistency issues.
        """
        mutation = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "rule/removeInconsistentRulebaseLinks.graphql"]
        )
        query_variables: dict[str, Any] = {
            "mgmId": import_state.mgm_details.current_mgm_id,
            "importId": import_state.import_id,
        }
        try:
            result = import_state.fwo_api_call.call(mutation, query_variables=query_variables)

            removed_links = result["data"]["update_rulebase_link"]["affected_rows"]
            FWOLogger.info(f"removed {removed_links!s} inconsistent rulebase links from DB to fix consistency issues")
            import_state.statistics_controller.statistics.inconsistent_rulebase_link_delete_count += removed_links
        except Exception:
            FWOLogger.exception(
                f"failed to remove inconsistent rulebase links for mgm id {import_state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to remove inconsistent rulebase links") from None

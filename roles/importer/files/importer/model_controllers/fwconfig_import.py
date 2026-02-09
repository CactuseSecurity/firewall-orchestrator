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
from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import (
    ConnectionInfo,
    CredentialInfo,
    DeviceInfo,
    DomainInfo,
    ManagementController,
    ManagerInfo,
)
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanagerlist import FwConfigManager
from services.global_state import GlobalState
from services.service_provider import ServiceProvider


# this class is used for importing a config into the FWO API
class FwConfigImport:
    import_state: ImportStateController
    normalized_config: FwConfigNormalized | None

    _fw_config_import_rule: FwConfigImportRule
    _fw_config_import_object: FwConfigImportObject
    _fw_config_import_gateway: FwConfigImportGateway
    _global_state: GlobalState

    @property
    def fwconfig_import_object(self):
        return self._fw_config_import_object

    def __init__(self):
        service_provider = ServiceProvider()
        self._global_state = service_provider.get_global_state()
        self.import_state = self._global_state.import_state

        self.normalized_config = self._global_state.normalized_config

        self._fw_config_import_object = FwConfigImportObject()
        self._fw_config_import_rule = FwConfigImportRule()
        self._fw_config_import_gateway = FwConfigImportGateway()

    def import_single_config(self, single_manager: FwConfigManager):
        # current implementation restriction: assuming we always get the full config (only inserts) from API
        mgm_id = self.import_state.state.lookup_management_id(single_manager.manager_uid)
        if mgm_id is None:
            raise FwoImporterError(f"could not find manager id in DB for UID {single_manager.manager_uid}")
        previous_config = self.get_latest_config_from_db()
        previous_global_config: FwConfigNormalized | None = None
        self._global_state.previous_config = previous_config
        if single_manager.is_super_manager:
            self._global_state.previous_global_config = previous_config
        else:
            # only set global config for sub managers
            previous_global_config = self._global_state.previous_global_config

        self.check_and_fix_db_consistency(previous_config, previous_global_config)

        # calculate differences and write them to the database via API
        self.update_diffs(previous_config, previous_global_config, single_manager)

    def import_management_set(self, service_provider: ServiceProvider, mgr_set: FwConfigManagerListController):
        for manager in sorted(mgr_set.ManagerSet, key=lambda m: not getattr(m, "IsSuperManager", False)):
            """
            the following loop is a preparation for future functionality
            we might add support for multiple configs per manager
            e.g. one config only adds data, one only deletes data, etc.
            currently we always only have one config per manager
            """
            for config in manager.configs:
                self.import_config(service_provider, manager, config)
        self.update_removed_managers(mgr_set.ManagerSet)

    def import_config(self, service_provider: ServiceProvider, manager: FwConfigManager, config: FwConfigNormalized):
        global_state = service_provider.get_global_state()
        global_state.normalized_config = config
        if manager.is_super_manager:
            # store global config as it is needed when importing sub managers which might reference it
            global_state.global_normalized_config = config
        mgm_id = self.import_state.state.lookup_management_id(manager.manager_uid)
        if mgm_id is None:
            raise FwoImporterError(f"could not find manager id in DB for UID {manager.manager_uid}")
        # TODO: clean separation between values relevant for all managers and those only relevant for specific managers - see #3646
        self.import_state.state.mgm_details.current_mgm_id = mgm_id
        self.import_state.state.mgm_details.current_mgm_is_super_manager = manager.is_super_manager
        config_importer = FwConfigImport()  # TODO: strange to create another import object here - see #3154
        config_importer.import_single_config(manager)
        config_importer.consistency_check_config_against_db()
        config_importer.write_latest_config()

    def update_removed_managers(self, mgr_set: list[FwConfigManager]):
        """
        Sets removed flag on all db entries associated with sub-managers which are not part of the current import set.
        """
        if not self.import_state.state.mgm_details.is_super_manager:
            return  # nothing to do for single management imports
        get_sub_mgrs_query = FwoApi.get_graphql_code(
            [fwo_const.GRAPHQL_QUERY_PATH + "device/getSubManagerUids.graphql"]
        )
        query_variables = {"mgmId": self.import_state.state.mgm_details.mgm_id}
        try:
            query_result = self.import_state.api_connection.call(get_sub_mgrs_query, query_variables=query_variables)
            if "errors" in query_result:
                raise FwoImporterError(
                    f"failed to get sub manager UIDs for super manager mgm id {self.import_state.state.mgm_details.mgm_id!s}: {query_result['errors']!s}"
                )
            mgrs_in_db = query_result["data"]["management"]
        except Exception:
            FWOLogger.exception(
                f"failed to get sub manager UIDs for super manager mgm id {self.import_state.state.mgm_details.mgm_id!s}: {traceback.format_exc()!s}"
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
            "importId": self.import_state.state.import_id,
        }
        try:
            result = self.import_state.api_connection.call(mutation, query_variables=query_variables)

            affected_tables = {key: value["affected_rows"] for key, value in result["data"].items()}
            FWOLogger.debug(f"marked sub-managers {mgr_uids_to_remove!s} as removed in tables: {affected_tables!s}")
            FWOLogger.info(
                f"marked {sum(affected_tables.values())!s} entries as removed for sub-managers {mgr_uids_to_remove!s}"
            )
            self.import_state.state.stats.statistics.network_object_delete_count += affected_tables.get(
                "update_object", 0
            )
            self.import_state.state.stats.statistics.service_object_delete_count += affected_tables.get(
                "update_service", 0
            )
            self.import_state.state.stats.statistics.user_object_delete_count += affected_tables.get("update_usr", 0)
            self.import_state.state.stats.statistics.zone_object_delete_count += affected_tables.get("update_zone", 0)
            self.import_state.state.stats.statistics.rule_delete_count += affected_tables.get("update_rule", 0)
            self.import_state.state.stats.statistics.rulebase_delete_count += affected_tables.get("update_rulebase", 0)
        except Exception:
            FWOLogger.exception(
                f"failed to mark sub-managers as removed for super manager mgm id {self.import_state.state.mgm_details.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to mark sub-managers as removed") from None

    def clear_management(self) -> FwConfigManagerListController:
        FWOLogger.info('this import run will reset the configuration of this management to "empty"')
        config_normalized = FwConfigManagerListController()
        mgm_details = self.import_state.state.mgm_details
        # Reset management
        config_normalized.add_manager(
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
        if len(self.import_state.state.mgm_details.sub_manager_ids) > 0:
            # Read config
            fwo_api = self.import_state.api_connection

            # Reset submanagement
            for sub_manager_id in self.import_state.state.mgm_details.sub_manager_ids:
                # Fetch sub management details
                mgm_controller = ManagementController(
                    mgm_id=int(sub_manager_id),
                    uid="",
                    devices=[],
                    device_info=DeviceInfo(),
                    connection_info=ConnectionInfo(),
                    importer_hostname="",
                    credential_info=CredentialInfo(),
                    manager_info=ManagerInfo(),
                    domain_info=DomainInfo(),
                )
                mgm_details_raw = mgm_controller.get_mgm_details(fwo_api, sub_manager_id)
                mgm_details = ManagementController.from_json(mgm_details_raw)
                config_normalized.add_manager(
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
        for management in config_normalized.ManagerSet:
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
        self.import_state.state.is_clearing_import = True  # the now following import is a full one

        return config_normalized

    def update_diffs(
        self,
        prev_config: FwConfigNormalized,
        prev_global_config: FwConfigNormalized | None,
        single_manager: FwConfigManager,
    ):
        self._fw_config_import_object.update_object_diffs(prev_config, prev_global_config, single_manager)

        if fwo_globals.shutdown_requested:
            raise ImportInterruptionError("Shutdown requested during updateObjectDiffs.")

        self._fw_config_import_rule.update_rulebase_diffs(prev_config)

        if fwo_globals.shutdown_requested:
            raise ImportInterruptionError("Shutdown requested during updateRulebaseDiffs.")

        self._fw_config_import_gateway.update_gateway_diffs()

    # cleanup configs which do not need to be retained according to data retention time
    def delete_old_imports(self) -> None:
        mgm_id = int(self.import_state.state.mgm_details.mgm_id)
        delete_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/deleteOldImports.graphql"])

        try:
            delete_result = self.import_state.api_call.call(
                delete_mutation,
                query_variables={"mgmId": mgm_id, "is_full_import": self.import_state.state.is_full_import},
            )
            if delete_result["data"]["delete_import_control"]["returning"]["control_id"]:
                imports_deleted = len(delete_result["data"]["delete_import_control"]["returning"]["control_id"])
                if imports_deleted > 0:
                    FWOLogger.info(
                        f"deleted {imports_deleted!s} imports which passed the retention time of {self.import_state.state.data_retention_days} days"
                    )
        except Exception:
            fwo_api_call = self.import_state.api_call
            FWOLogger.error(
                f"error while trying to delete old imports for mgm {self.import_state.state.mgm_details.mgm_id!s}"
            )
            fwo_api_call.create_data_issue(
                mgm_id=self.import_state.state.mgm_details.mgm_id,
                severity=1,
                description="failed to get import lock for management id " + str(mgm_id),
            )
            fwo_api_call.set_alert(
                import_id=self.import_state.state.import_id,
                title="import error",
                mgm_id=mgm_id,
                severity=1,
                description="fwo_api: failed to get import lock",
                source="import",
                alert_code=15,
                mgm_details=self.import_state.state.mgm_details,
            )
            raise FwoApiFailedDeleteOldImportsError(f"management id: {mgm_id}") from None

    def write_latest_config(self):
        if self.import_state.state.import_version > 8:  # noqa: PLR2004
            if self.normalized_config is None:
                raise FwoImporterError("cannot write latest config: NormalizedConfig is None")
            # convert FwConfigImport to FwConfigNormalized
            self.normalized_config = FwConfigNormalized(
                action=self.normalized_config.action,
                network_objects=self.normalized_config.network_objects,
                service_objects=self.normalized_config.service_objects,
                users=self.normalized_config.users,
                zone_objects=self.normalized_config.zone_objects,
                rulebases=self.normalized_config.rulebases,
                gateways=self.normalized_config.gateways,
                ConfigFormat=self.normalized_config.ConfigFormat,
            )

            self.delete_latest_config_of_management()
            insert_mutation = FwoApi.get_graphql_code(
                [fwo_const.GRAPHQL_QUERY_PATH + "import/storeLatestConfig.graphql"]
            )
            try:
                query_variables: dict[str, Any] = {
                    "mgmId": self.import_state.state.mgm_details.current_mgm_id,
                    "importId": self.import_state.state.import_id,
                    "config": self.normalized_config.model_dump_json(),
                }
                import_result = self.import_state.api_call.call(insert_mutation, query_variables=query_variables)
                if "errors" in import_result:
                    FWOLogger.exception(
                        "fwo_api:storeLatestConfig - error while writing importable config for mgm id "
                        + str(self.import_state.state.mgm_details.current_mgm_id)
                        + ": "
                        + str(import_result["errors"])
                    )
                    FWOLogger.warning(
                        f"error while writing latest config for import_id {self.import_state.state.import_id}, mgm_id: {self.import_state.state.mgm_details.mgm_id}, mgm_uid: {self.import_state.state.mgm_details.uid}"
                    )
                else:
                    _ = import_result["data"]["insert_latest_config"]["affected_rows"]
            except Exception:
                FWOLogger.exception(
                    f"failed to write latest normalized config for mgm id {self.import_state.state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
                )
                raise

    def delete_latest_config_of_management(self):
        delete_mutation = FwoApi.get_graphql_code(
            [fwo_const.GRAPHQL_QUERY_PATH + "import/deleteLatestConfigOfManagement.graphql"]
        )
        try:
            query_variables = {"mgmId": self.import_state.state.mgm_details.current_mgm_id}
            import_result = self.import_state.api_call.call(delete_mutation, query_variables=query_variables)
            if "errors" in import_result:
                FWOLogger.exception(
                    "fwo_api:import_latest_config - error while deleting last config for mgm id "
                    + str(self.import_state.state.mgm_details.current_mgm_id)
                    + ": "
                    + str(import_result["errors"])
                )
            else:
                _ = import_result["data"]["delete_latest_config"]["affected_rows"]
        except Exception:
            FWOLogger.exception(
                f"failed to delete latest normalized config for mgm id {self.import_state.state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )

    def get_latest_import_id(self) -> int | None:
        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/getLastSuccessImport.graphql"])
        query_variables = {"mgmId": self.import_state.state.mgm_details.mgm_id}
        try:
            query_result = self.import_state.api_connection.call(query, query_variables=query_variables)
            if "errors" in query_result:
                raise FwoImporterError(
                    f"failed to get latest import id for mgm id {self.import_state.state.mgm_details.mgm_id!s}: {query_result['errors']!s}"
                )
            if len(query_result["data"]["import_control"]) == 0:
                return None
            return query_result["data"]["import_control"][0]["control_id"]
        except Exception:
            FWOLogger.exception(
                f"failed to get latest import id for mgm id {self.import_state.state.mgm_details.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to get the latest import id")

    # return previous config or empty config if there is none; only returns the config of a single management
    def get_latest_config(self) -> FwConfigNormalized:
        mgm_id = self.import_state.state.mgm_details.current_mgm_id
        prev_config = FwConfigNormalized()

        latest_import_id = self.get_latest_import_id()
        if latest_import_id is None:
            FWOLogger.info(f"first import - no existing import was found for mgm id {mgm_id}")  # TODO: change msg
            return prev_config

        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/getLatestConfig.graphql"])
        query_variables = {"mgmId": mgm_id}
        try:
            query_result = self.import_state.api_connection.call(query, query_variables=query_variables)
            if "errors" in query_result:
                raise FwoImporterError(
                    f"failed to get latest config for mgm id {self.import_state.state.mgm_details.mgm_id!s}: {query_result['errors']!s}"
                )
            if len(query_result["data"]["latest_config"]) > 0:  # do we have a prev config?
                if query_result["data"]["latest_config"][0]["import_id"] == latest_import_id:
                    return FwConfigNormalized.model_validate_json(query_result["data"]["latest_config"][0]["config"])
                FWOLogger.warning(
                    f"fwo_api:import_latest_config - latest config for mgm id {mgm_id} did not match last import id {latest_import_id}"
                )
            FWOLogger.info("fetching latest config from DB as fallback")
            return self.get_latest_config_from_db()
        except Exception:
            FWOLogger.exception(
                f"failed to get latest normalized config for mgm id {self.import_state.state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to get the previous config")

    def get_latest_config_from_db(self) -> FwConfigNormalized:
        params = {"mgm-ids": [self.import_state.state.mgm_details.current_mgm_id]}
        result = self.import_state.api_connection.call_endpoint("POST", "api/NormalizedConfig/Get", params=params)
        try:
            return FwConfigNormalized.model_validate(result)
        except Exception:
            FWOLogger.exception(
                f"failed to get latest normalized config from db for mgm id {self.import_state.state.mgm_details.mgm_id!s}: {traceback.format_exc()!s}"
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

    def consistency_check_config_against_db(self):
        normalized_config = self.normalized_config
        if normalized_config is None:
            raise FwoImporterError("cannot perform consistency check: NormalizedConfig is None")
        normalized_config_from_db = self.get_latest_config_from_db()
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
                f"normalized config for mgm id {self.import_state.state.mgm_details.current_mgm_id} is inconsistent to database state: {all_diffs[0]}"
            )
            FWOLogger.debug(f"all {len(all_diffs)} differences:\n\t" + "\n\t".join(all_diffs))
            # TODO: long-term this should raise an error:

    def check_and_fix_db_consistency(
        self,
        previous_config: FwConfigNormalized,
        previous_global_config: FwConfigNormalized | None,
    ):
        """
        Check consistency of the latest config (=previous config) built from database state before import.
        If inconsistencies are found, they will be fixed in the database by marking objects/rules/links as removed.
        """
        consistency_checker = FwConfigImportCheckConsistency(self.import_state.state)
        consistency_checker.check_config_consistency(previous_config, previous_global_config, fix_config=True)
        self.fix_objects_in_db(
            consistency_checker.network_objects_to_remove,
            consistency_checker.service_objects_to_remove,
            consistency_checker.user_objects_to_remove,
        )
        self.fix_rules_in_db(consistency_checker.rules_to_remove)
        if consistency_checker.invalid_rulebase_links_exist:
            self.fix_rulebase_links_in_db()
        self.fix_rule_to_gw_refs_in_db(previous_config, previous_global_config)
        self.fix_ref_tables_in_db()
        self.fix_rule_to_gw_refs_in_db(previous_config, previous_global_config)

    def fix_objects_in_db(self, nwobj_uids: list[str], svcobj_uids: list[str], user_uids: list[str]):
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
            "mgmId": self.import_state.state.mgm_details.current_mgm_id,
            "importId": self.import_state.state.import_id,
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
            result = self.import_state.api_call.call(mutation, query_variables=query_variables)

            removed_nwobj_ids = result["data"]["update_object"]["returning"]
            removed_nwsvc_ids = result["data"]["update_service"]["returning"]
            removed_user_ids = result["data"]["update_usr"]["returning"]
            FWOLogger.info(
                f"removed {len(removed_nwobj_ids)!s} network objects, {len(removed_nwsvc_ids)!s} service objects and {len(removed_user_ids)!s} user objects from DB to fix consistency issues"
            )
            self.import_state.state.stats.statistics.inconsistent_nwobj_delete_count += len(removed_nwobj_ids)
            self.import_state.state.stats.statistics.inconsistent_svcobj_delete_count += len(removed_nwsvc_ids)
            self.import_state.state.stats.statistics.inconsistent_userobj_delete_count += len(removed_user_ids)
        except Exception:
            FWOLogger.exception(
                f"failed to fix object consistency issues for mgm id {self.import_state.state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to fix object consistency issues") from None

    def fix_rules_in_db(self, rule_uids: list[str]):
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
            "mgmId": self.import_state.state.mgm_details.current_mgm_id,
            "importId": self.import_state.state.import_id,
            "uids": rule_uids,
        }
        try:
            result = self.import_state.api_call.call(mutation, query_variables=query_variables)

            removed_rule_ids = result["data"]["update_rule"]["returning"]
            FWOLogger.info(f"marked {len(removed_rule_ids)!s} rules as removed in DB to fix consistency issues")
            self.import_state.state.stats.statistics.inconsistent_rule_delete_count += len(removed_rule_ids)
        except Exception:
            FWOLogger.exception(
                f"failed to fix rule consistency issues for mgm id {self.import_state.state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to fix rule consistency issues") from None

    def fix_rulebase_links_in_db(self):
        """
        Removes inconsistent rulebase links from the database to fix consistency issues.
        """
        mutation = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "rule/removeInconsistentRulebaseLinks.graphql"]
        )
        query_variables: dict[str, Any] = {
            "mgmId": self.import_state.state.mgm_details.current_mgm_id,
            "importId": self.import_state.state.import_id,
        }
        try:
            result = self.import_state.api_call.call(mutation, query_variables=query_variables)

            removed_links = result["data"]["update_rulebase_link"]["affected_rows"]
            FWOLogger.info(f"removed {removed_links!s} inconsistent rulebase links from DB to fix consistency issues")
            self.import_state.state.stats.statistics.inconsistent_rulebase_link_delete_count += removed_links
        except Exception:
            FWOLogger.exception(
                f"failed to remove inconsistent rulebase links for mgm id {self.import_state.state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to remove inconsistent rulebase links") from None

    def _insert_missing_rule_to_gw_refs_in_db(self, refs_to_add: set[tuple[str, str]]):
        """Inserts missing rule enforced on gateway references to the database to fix consistency issues."""
        if not refs_to_add:
            return  # nothing to do
        mgm_id = self.import_state.state.mgm_details.current_mgm_id
        fetch_rule_ids_query = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "rule/getRulesByUidsWithCreate.graphql"]
        )
        fetch_rule_ids_variables: dict[str, Any] = {
            "mgmId": mgm_id,
            "uids": [rule_uid for rule_uid, _gw_uid in refs_to_add],
        }
        try:
            fetch_rule_ids_result = self.import_state.api_call.call(
                fetch_rule_ids_query, query_variables=fetch_rule_ids_variables
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
                    "dev_id": self.import_state.state.gateway_map[mgm_id][gw_uid],
                    "created": rule_uid_to_id_create[rule_uid][1],
                }
                for rule_uid, gw_uid in refs_to_add
            ],
        }
        try:
            result = self.import_state.api_call.call(mutation, query_variables=query_variables)

            added_refs = result["data"]["insert_rule_enforced_on_gateway"]["affected_rows"]
            FWOLogger.info(
                f"added {added_refs!s} missing rule enforced on gateway references to DB to fix consistency issues"
            )
        except Exception:
            FWOLogger.exception(
                f"failed to add missing rule enforced on gateway references for mgm id {self.import_state.state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to add missing rule enforced on gateway references") from None

    def fix_rule_to_gw_refs_in_db(
        self, previous_config: FwConfigNormalized, previous_global_config: FwConfigNormalized | None
    ):
        """
        Set inconsistent rule_enforced_on_gateway entries removed and insert missing ones.
        """
        mgm_id = self.import_state.state.mgm_details.current_mgm_id
        if mgm_id not in self.import_state.state.gateway_map:
            # no gateways assigned to management (e.g. super-mgr)
            return
        gw_ids = list(self.import_state.state.gateway_map[mgm_id].values())
        query = FwoApi.get_graphql_code(
            file_list=[fwo_const.GRAPHQL_QUERY_PATH + "rule/getRulesEnforcedOnGateways.graphql"]
        )
        query_variables: dict[str, Any] = {
            "gwIds": gw_ids,
        }
        try:
            result = self.import_state.api_call.call(query, query_variables=query_variables)
            if "errors" in result:
                raise FwoImporterError(
                    f"failed to get rules enforced on gateways for mgm id {self.import_state.state.mgm_details.current_mgm_id!s}: {result['errors']!s}"
                )
            rules_enforced_on_gw = result["data"]["rule_enforced_on_gateway"]
        except Exception:
            FWOLogger.exception(
                f"failed to get rules enforced on gateways for mgm id {self.import_state.state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError("error while trying to get rules enforced on gateways") from None
        # need to set removed flag on active refs referencing removed rule
        ref_with_removed_rule_exists = any(ref for ref in rules_enforced_on_gw if ref["rule"]["removed"] is not None)
        # comparing expected refs from config with existing refs to *active* rules to determine missing refs to add
        expected_refs = FwConfigImportRule.get_rule_to_gw_refs(
            previous_config.rulebases,
            previous_global_config.rulebases if previous_global_config else None,
            previous_config.gateways,
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
                "importId": self.import_state.state.import_id,
            }
            try:
                result = self.import_state.api_call.call(mutation, query_variables=query_variables)
                if "errors" in result:
                    raise FwoImporterError(
                        f"failed to remove inconsistent rule enforced on gateway references for mgm id {self.import_state.state.mgm_details.current_mgm_id!s}: {result['errors']!s}"
                    )
                removed_refs = result["data"]["update_rule_enforced_on_gateway"]["affected_rows"]
                FWOLogger.info(
                    f"removed {removed_refs!s} inconsistent rule enforced on gateway references from DB to fix consistency issues"
                )
                self.import_state.state.stats.statistics.inconsistent_ref_delete_count += removed_refs
            except Exception:
                FWOLogger.exception(
                    f"failed to remove inconsistent rule enforced on gateway references for mgm id {self.import_state.state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
                )
                raise FwoImporterError(
                    "error while trying to remove inconsistent rule enforced on gateway references"
                ) from None

        if refs_to_add:
            self._insert_missing_rule_to_gw_refs_in_db(refs_to_add)

    def fix_ref_tables_in_db(self):
        """
        Check ref tables for active references to objects/rules which were marked as removed and remove these
        references to fix consistency issues.
        """
        mutation = FwoApi.get_graphql_code(file_list=[fwo_const.GRAPHQL_QUERY_PATH + "allObjects/fixRefTables.graphql"])
        query_variables: dict[str, Any] = {
            "mgmId": self.import_state.state.mgm_details.current_mgm_id,
            "importId": self.import_state.state.import_id,
        }
        try:
            result = self.import_state.api_call.call(mutation, query_variables=query_variables)

            affected_rows = {key: value["affected_rows"] for key, value in result["data"].items()}
            FWOLogger.info(
                f"fixed references to removed objects/rules in ref tables to fix consistency issues: {affected_rows!s}"
            )
            self.import_state.state.stats.statistics.inconsistent_ref_delete_count += sum(affected_rows.values())
        except Exception:
            FWOLogger.exception(
                f"failed to fix references to removed objects/rules in ref tables for mgm id {self.import_state.state.mgm_details.current_mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError(
                "error while trying to fix references to removed objects/rules in ref tables"
            ) from None

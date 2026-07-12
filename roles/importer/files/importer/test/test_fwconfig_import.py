# pyright: reportPrivateUsage=false
from types import SimpleNamespace
from typing import cast
from unittest.mock import MagicMock, patch

import fwo_globals
import pytest
from fwo_exceptions import FwoApiFailedDeleteOldImportsError, FwoImporterError, ImportInterruptionError
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.management_controller import ManagementController
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager
from models.gateway import Gateway
from models.rulebase import Rulebase
from models.rulebase_link import RulebaseLinkUidBased
from services.service_provider import ServiceProvider


@pytest.fixture
def importer() -> FwConfigImport:
    return FwConfigImport()


def api_call_mock(importer: FwConfigImport) -> MagicMock:
    return cast("MagicMock", importer.import_state.api_call)


def api_connection_mock(importer: FwConfigImport) -> MagicMock:
    return cast("MagicMock", importer.import_state.api_connection)


def make_manager(uid: str = "mock-manager-uid", is_super_manager: bool = False) -> FwConfigManager:
    return FwConfigManager(
        manager_uid=uid,
        manager_name=uid,
        is_super_manager=is_super_manager,
        domain_uid="dom-uid",
        domain_name="dom",
        sub_manager_ids=[],
        configs=[],
    )


def make_rulebase(uid: str) -> Rulebase:
    return Rulebase(uid=uid, name=uid, mgm_uid="mock-uid")


def make_link(to_rulebase_uid: str, from_rule_uid: str | None = None) -> RulebaseLinkUidBased:
    return RulebaseLinkUidBased(
        from_rulebase_uid=None,
        from_rule_uid=from_rule_uid,
        to_rulebase_uid=to_rulebase_uid,
        is_initial=True,
        is_global=False,
        is_section=False,
    )


class TestImportSingleConfig:
    def test_updates_diffs_with_previous_configs(self, importer: FwConfigImport):
        manager = make_manager()
        importer.import_state.state.management_map = {manager.manager_uid: 3}
        previous_config = FwConfigNormalized()
        with (
            patch.object(FwConfigImport, "get_latest_config_from_db", return_value=previous_config),
            patch.object(FwConfigImport, "check_and_fix_db_consistency") as consistency_fixer,
            patch.object(FwConfigImport, "update_diffs") as diff_updater,
        ):
            importer.import_single_config(manager)

        expected_global = importer._global_state.previous_global_config
        consistency_fixer.assert_called_once_with(previous_config, expected_global)
        diff_updater.assert_called_once_with(previous_config, expected_global, manager)
        assert importer._global_state.previous_config is previous_config

    def test_super_manager_sets_previous_global_config(self, importer: FwConfigImport):
        manager = make_manager(is_super_manager=True)
        importer.import_state.state.management_map = {manager.manager_uid: 3}
        previous_config = FwConfigNormalized()
        with (
            patch.object(FwConfigImport, "get_latest_config_from_db", return_value=previous_config),
            patch.object(FwConfigImport, "check_and_fix_db_consistency"),
            patch.object(FwConfigImport, "update_diffs") as diff_updater,
        ):
            importer.import_single_config(manager)

        diff_updater.assert_called_once_with(previous_config, None, manager)
        assert importer._global_state.previous_global_config is previous_config

    def test_unknown_manager_uid_raises(self, importer: FwConfigImport):
        importer.import_state.state.management_map = {}
        with pytest.raises(FwoImporterError, match="could not find manager id"):
            importer.import_single_config(make_manager())


class TestImportManagementSet:
    def test_imports_each_config_and_updates_removed_managers(
        self, importer: FwConfigImport, service_provider: ServiceProvider
    ):
        mgr_set = FwConfigManagerListController()
        manager = make_manager()
        manager.configs.append(FwConfigNormalized())
        manager.configs.append(FwConfigNormalized())
        mgr_set.add_manager(manager)
        with (
            patch.object(FwConfigImport, "import_config") as config_importer,
            patch.object(FwConfigImport, "update_removed_managers") as removed_updater,
        ):
            importer.import_management_set(service_provider, mgr_set)

        assert importer.import_state.state.rollback_required is True
        assert config_importer.call_count == 2
        removed_updater.assert_called_once_with(mgr_set.ManagerSet)

    def test_import_config_sets_current_manager_context(
        self, importer: FwConfigImport, service_provider: ServiceProvider
    ):
        manager = make_manager(is_super_manager=True)
        importer.import_state.state.management_map = {manager.manager_uid: 42}
        config = FwConfigNormalized()
        with (
            patch.object(FwConfigImport, "import_single_config") as single_importer,
            patch.object(FwConfigImport, "consistency_check_config_against_db") as consistency_checker,
            patch.object(FwConfigImport, "write_latest_config") as config_writer,
        ):
            importer.import_config(service_provider, manager, config)

        global_state = service_provider.get_global_state()
        assert global_state.normalized_config is config
        assert global_state.global_normalized_config is config
        assert importer.import_state.state.mgm_details.current_mgm_id == 42
        assert importer.import_state.state.mgm_details.current_mgm_is_super_manager is True
        single_importer.assert_called_once_with(manager)
        consistency_checker.assert_called_once_with()
        config_writer.assert_called_once_with()

    def test_import_config_with_unknown_manager_raises(
        self, importer: FwConfigImport, service_provider: ServiceProvider
    ):
        importer.import_state.state.management_map = {}
        with pytest.raises(FwoImporterError, match="could not find manager id"):
            importer.import_config(service_provider, make_manager(), FwConfigNormalized())


class TestUpdateRemovedManagers:
    def test_non_super_manager_does_nothing(self, importer: FwConfigImport):
        importer.import_state.state.mgm_details.is_super_manager = False
        importer.update_removed_managers([])
        api_connection_mock(importer).call.assert_not_called()

    def test_marks_missing_sub_managers_as_removed(self, importer: FwConfigImport):
        importer.import_state.state.mgm_details.is_super_manager = True
        api_connection_mock(importer).call.side_effect = [
            {"data": {"management": [{"mgm_uid": "keep", "mgm_id": 1}, {"mgm_uid": "gone", "mgm_id": 2}]}},
            {"data": {"update_firewall_nw_object": {"affected_rows": 2}, "update_firewall_rule": {"affected_rows": 3}}},
        ]

        importer.update_removed_managers([make_manager(uid="keep")])

        mutation_variables = api_connection_mock(importer).call.call_args_list[1].kwargs["query_variables"]
        assert mutation_variables["mgmIds"] == [2]
        statistics = importer.import_state.state.stats.statistics
        assert statistics.network_object_delete_count == 2
        assert statistics.rule_delete_count == 3

    def test_nothing_to_remove_skips_mutation(self, importer: FwConfigImport):
        importer.import_state.state.mgm_details.is_super_manager = True
        api_connection_mock(importer).call.side_effect = [
            {"data": {"management": [{"mgm_uid": "keep", "mgm_id": 1}]}},
        ]

        importer.update_removed_managers([make_manager(uid="keep")])

        assert api_connection_mock(importer).call.call_count == 1

    def test_query_errors_raise(self, importer: FwConfigImport):
        importer.import_state.state.mgm_details.is_super_manager = True
        api_connection_mock(importer).call.return_value = {"errors": ["nope"]}
        with pytest.raises(FwoImporterError, match="sub manager UIDs"):
            importer.update_removed_managers([])

    def test_mutation_failure_raises(self, importer: FwConfigImport):
        importer.import_state.state.mgm_details.is_super_manager = True
        api_connection_mock(importer).call.side_effect = [
            {"data": {"management": [{"mgm_uid": "gone", "mgm_id": 2}]}},
            Exception("api down"),
        ]
        with pytest.raises(FwoImporterError, match="mark sub-managers as removed"):
            importer.update_removed_managers([])


class TestClearManagement:
    def test_resets_sub_managements(self, importer: FwConfigImport):
        importer.import_state.state.mgm_details.sub_manager_ids = [7]
        sub_details = SimpleNamespace(
            uid="sub-uid",
            name="Sub",
            is_super_manager=False,
            sub_manager_ids=[],
            domain_name="dom",
            domain_uid="dom-uid",
        )
        with (
            patch.object(ManagementController, "get_mgm_details", return_value={"raw": True}),
            patch.object(ManagementController, "from_json", return_value=sub_details),
        ):
            config = importer.clear_management()

        assert [manager.manager_uid for manager in config.ManagerSet] == ["mock-uid", "sub-uid"]
        assert all(len(manager.configs) == 1 for manager in config.ManagerSet)
        assert all(manager.configs[0].network_objects == {} for manager in config.ManagerSet)
        assert importer.import_state.state.is_clearing_import is True


class TestUpdateDiffs:
    def test_calls_all_diff_updaters(self, importer: FwConfigImport, monkeypatch: pytest.MonkeyPatch):
        monkeypatch.setattr(fwo_globals, "shutdown_requested", False)
        previous_config = FwConfigNormalized()
        manager = make_manager()
        with (
            patch.object(importer._fw_config_import_object, "update_object_diffs") as object_updater,
            patch.object(importer._fw_config_import_rule, "update_rulebase_diffs") as rule_updater,
            patch.object(importer._fw_config_import_gateway, "update_gateway_diffs") as gateway_updater,
        ):
            importer.update_diffs(previous_config, None, manager)

        object_updater.assert_called_once_with(previous_config, None, manager)
        rule_updater.assert_called_once_with(previous_config)
        gateway_updater.assert_called_once_with()

    def test_shutdown_after_object_diffs_interrupts(self, importer: FwConfigImport, monkeypatch: pytest.MonkeyPatch):
        monkeypatch.setattr(fwo_globals, "shutdown_requested", True)
        with (
            patch.object(importer._fw_config_import_object, "update_object_diffs"),
            patch.object(importer._fw_config_import_rule, "update_rulebase_diffs") as rule_updater,
            pytest.raises(ImportInterruptionError, match="updateObjectDiffs"),
        ):
            importer.update_diffs(FwConfigNormalized(), None, make_manager())
        rule_updater.assert_not_called()

    def test_shutdown_after_rulebase_diffs_interrupts(self, importer: FwConfigImport, monkeypatch: pytest.MonkeyPatch):
        monkeypatch.setattr(fwo_globals, "shutdown_requested", False)

        def request_shutdown(_previous_config: FwConfigNormalized) -> None:
            monkeypatch.setattr(fwo_globals, "shutdown_requested", True)

        with (
            patch.object(importer._fw_config_import_object, "update_object_diffs"),
            patch.object(importer._fw_config_import_rule, "update_rulebase_diffs", side_effect=request_shutdown),
            patch.object(importer._fw_config_import_gateway, "update_gateway_diffs") as gateway_updater,
            pytest.raises(ImportInterruptionError, match="updateRulebaseDiffs"),
        ):
            importer.update_diffs(FwConfigNormalized(), None, make_manager())
        gateway_updater.assert_not_called()


class TestDeleteOldImports:
    def test_logs_deleted_imports(self, importer: FwConfigImport):
        api_call_mock(importer).call.return_value = {
            "data": {"delete_import_control": {"returning": {"control_id": [1, 2]}}}
        }
        importer.delete_old_imports()
        api_call_mock(importer).call.assert_called_once()

    def test_no_deleted_imports(self, importer: FwConfigImport):
        api_call_mock(importer).call.return_value = {
            "data": {"delete_import_control": {"returning": {"control_id": []}}}
        }
        importer.delete_old_imports()

    def test_failure_creates_alert_and_raises(self, importer: FwConfigImport):
        api_call_mock(importer).call.side_effect = Exception("api down")
        with pytest.raises(FwoApiFailedDeleteOldImportsError):
            importer.delete_old_imports()
        api_call_mock(importer).create_data_issue.assert_called_once()
        api_call_mock(importer).set_alert.assert_called_once()


class TestWriteLatestConfig:
    def test_old_import_version_skips_write(self, importer: FwConfigImport):
        importer.import_state.state.import_version = 8
        importer.write_latest_config()
        api_call_mock(importer).call.assert_not_called()

    def test_missing_config_raises(self, importer: FwConfigImport):
        importer.import_state.state.import_version = 9
        importer.normalized_config = None
        with pytest.raises(FwoImporterError, match="NormalizedConfig is None"):
            importer.write_latest_config()

    def test_writes_config(self, importer: FwConfigImport):
        importer.import_state.state.import_version = 9
        importer.normalized_config = FwConfigNormalized()
        api_call_mock(importer).call.return_value = {"data": {"insert_latest_config": {"affected_rows": 1}}}
        with patch.object(FwConfigImport, "delete_latest_config_of_management") as deleter:
            importer.write_latest_config()

        deleter.assert_called_once_with()
        query_variables = api_call_mock(importer).call.call_args.kwargs["query_variables"]
        assert query_variables["mgmId"] == importer.import_state.state.mgm_details.current_mgm_id

    def test_write_errors_are_logged_without_raising(self, importer: FwConfigImport):
        importer.import_state.state.import_version = 9
        importer.normalized_config = FwConfigNormalized()
        api_call_mock(importer).call.return_value = {"errors": ["nope"]}
        with patch.object(FwConfigImport, "delete_latest_config_of_management"):
            importer.write_latest_config()

    def test_write_exception_is_reraised(self, importer: FwConfigImport):
        importer.import_state.state.import_version = 9
        importer.normalized_config = FwConfigNormalized()
        api_call_mock(importer).call.side_effect = Exception("api down")
        with (
            patch.object(FwConfigImport, "delete_latest_config_of_management"),
            pytest.raises(Exception, match="api down"),
        ):
            importer.write_latest_config()


class TestDeleteLatestConfig:
    def test_deletes_latest_config(self, importer: FwConfigImport):
        api_call_mock(importer).call.return_value = {"data": {"delete_latest_config": {"affected_rows": 1}}}
        importer.delete_latest_config_of_management()

    def test_delete_errors_are_swallowed(self, importer: FwConfigImport):
        api_call_mock(importer).call.return_value = {"errors": ["nope"]}
        importer.delete_latest_config_of_management()
        api_call_mock(importer).call.side_effect = Exception("api down")
        importer.delete_latest_config_of_management()


class TestGetLatestConfig:
    def test_get_latest_import_id(self, importer: FwConfigImport):
        api_connection_mock(importer).call.return_value = {"data": {"import_control": [{"control_id": 77}]}}
        assert importer.get_latest_import_id() == 77

    def test_get_latest_import_id_without_imports(self, importer: FwConfigImport):
        api_connection_mock(importer).call.return_value = {"data": {"import_control": []}}
        assert importer.get_latest_import_id() is None

    def test_get_latest_import_id_errors_raise(self, importer: FwConfigImport):
        api_connection_mock(importer).call.return_value = {"errors": ["nope"]}
        with pytest.raises(FwoImporterError, match="latest import id"):
            importer.get_latest_import_id()

    def test_first_import_returns_empty_config(self, importer: FwConfigImport):
        with patch.object(FwConfigImport, "get_latest_import_id", return_value=None):
            assert importer.get_latest_config() == FwConfigNormalized()

    def test_returns_stored_config_matching_import_id(self, importer: FwConfigImport):
        stored_config = FwConfigNormalized()
        api_connection_mock(importer).call.return_value = {
            "data": {"latest_config": [{"import_id": 5, "config": stored_config.model_dump_json()}]}
        }
        with patch.object(FwConfigImport, "get_latest_import_id", return_value=5):
            assert importer.get_latest_config() == stored_config

    def test_falls_back_to_db_on_import_id_mismatch(self, importer: FwConfigImport):
        fallback_config = FwConfigNormalized()
        api_connection_mock(importer).call.return_value = {
            "data": {"latest_config": [{"import_id": 4, "config": FwConfigNormalized().model_dump_json()}]}
        }
        with (
            patch.object(FwConfigImport, "get_latest_import_id", return_value=5),
            patch.object(FwConfigImport, "get_latest_config_from_db", return_value=fallback_config) as db_getter,
        ):
            assert importer.get_latest_config() is fallback_config
        db_getter.assert_called_once_with()

    def test_query_errors_raise(self, importer: FwConfigImport):
        api_connection_mock(importer).call.return_value = {"errors": ["nope"]}
        with (
            patch.object(FwConfigImport, "get_latest_import_id", return_value=5),
            pytest.raises(FwoImporterError, match="previous config"),
        ):
            importer.get_latest_config()

    def test_get_latest_config_from_db(self, importer: FwConfigImport):
        api_connection_mock(importer).call_endpoint.return_value = FwConfigNormalized().model_dump()
        assert importer.get_latest_config_from_db() == FwConfigNormalized()

    def test_get_latest_config_from_db_with_invalid_payload_raises(self, importer: FwConfigImport):
        api_connection_mock(importer).call_endpoint.return_value = "not-a-config"
        with pytest.raises(FwoImporterError, match="latest config"):
            importer.get_latest_config_from_db()


class TestSortListsAndConsistency:
    def test_sort_lists_orders_rulebases_gateways_and_links(self, importer: FwConfigImport):
        config = FwConfigNormalized(
            rulebases=[make_rulebase("rb-b"), make_rulebase("rb-a")],
            gateways=[
                Gateway(
                    Uid="gw-b",
                    RulebaseLinks=[make_link("rb-b", from_rule_uid="r2"), make_link("rb-a", from_rule_uid="r1")],
                    EnforcedPolicyUids=["z", "a"],
                    EnforcedNatPolicyUids=["y", "b"],
                ),
                Gateway(Uid="gw-a", EnforcedPolicyUids=None, EnforcedNatPolicyUids=None),
            ],
        )

        importer._sort_lists(config)

        assert [rulebase.uid for rulebase in config.rulebases] == ["rb-a", "rb-b"]
        assert [gateway.Uid for gateway in config.gateways] == ["gw-a", "gw-b"]
        assert [link.to_rulebase_uid for link in config.gateways[1].RulebaseLinks] == ["rb-a", "rb-b"]
        assert config.gateways[1].EnforcedPolicyUids == ["a", "z"]
        assert config.gateways[1].EnforcedNatPolicyUids == ["b", "y"]

    def test_sort_lists_rejects_gateway_without_uid(self, importer: FwConfigImport):
        config = FwConfigNormalized(gateways=[Gateway(Uid=None)])
        with pytest.raises(FwoImporterError, match="gateway without UID"):
            importer._sort_lists(config)

    def test_consistency_check_requires_config(self, importer: FwConfigImport):
        importer.normalized_config = None
        with pytest.raises(FwoImporterError, match="NormalizedConfig is None"):
            importer.consistency_check_config_against_db()

    def test_consistency_check_filters_foreign_gateways(self, importer: FwConfigImport):
        importer.normalized_config = FwConfigNormalized()
        db_config = FwConfigNormalized(gateways=[Gateway(Uid="gw-not-imported")])
        with patch.object(FwConfigImport, "get_latest_config_from_db", return_value=db_config):
            importer.consistency_check_config_against_db()
        assert db_config.gateways == []

    def test_consistency_check_logs_differences(self, importer: FwConfigImport):
        importer.normalized_config = FwConfigNormalized(rulebases=[make_rulebase("rb-a")])
        with patch.object(FwConfigImport, "get_latest_config_from_db", return_value=FwConfigNormalized()):
            importer.consistency_check_config_against_db()


class TestCheckAndFixDbConsistency:
    def test_runs_checker_and_all_fixers(self, importer: FwConfigImport):
        previous_config = FwConfigNormalized()
        checker = MagicMock()
        checker.network_objects_to_remove = ["nw-1"]
        checker.service_objects_to_remove = ["svc-1"]
        checker.user_objects_to_remove = ["usr-1"]
        checker.rules_to_remove = ["rule-1"]
        with (
            patch(
                "model_controllers.fwconfig_import.FwConfigImportCheckConsistency", return_value=checker
            ) as checker_class,
            patch.object(FwConfigImport, "fix_objects_in_db") as object_fixer,
            patch.object(FwConfigImport, "fix_rules_in_db") as rule_fixer,
            patch.object(FwConfigImport, "fix_rulebase_links_in_db") as link_fixer,
            patch.object(FwConfigImport, "fix_rule_to_gw_refs_in_db") as ref_fixer,
            patch.object(FwConfigImport, "fix_ref_tables_in_db") as table_fixer,
            patch.object(FwConfigImport, "fix_changelog_rule") as changelog_fixer,
        ):
            importer.check_and_fix_db_consistency(previous_config, None)

        checker_class.assert_called_once_with(importer.import_state.state)
        checker.check_config_consistency.assert_called_once_with(previous_config, None, fix_config=True)
        object_fixer.assert_called_once_with(["nw-1"], ["svc-1"], ["usr-1"])
        rule_fixer.assert_called_once_with(["rule-1"])
        link_fixer.assert_called_once_with(previous_config)
        ref_fixer.assert_called_once_with(previous_config, None)
        table_fixer.assert_called_once_with()
        changelog_fixer.assert_called_once_with()


class TestFixObjectsAndRules:
    def test_fix_objects_with_nothing_to_do(self, importer: FwConfigImport):
        importer.fix_objects_in_db([], [], [])
        api_call_mock(importer).call.assert_not_called()

    def test_fix_objects_updates_statistics(self, importer: FwConfigImport):
        api_call_mock(importer).call.return_value = {
            "data": {
                "update_firewall_nw_object": {"returning": [1]},
                "update_firewall_nw_service": {"returning": [1, 2]},
                "update_firewall_nw_user": {"returning": []},
            }
        }
        importer.fix_objects_in_db(["nw-1"], ["svc-1"], [])
        statistics = importer.import_state.state.stats.statistics
        assert statistics.inconsistent_nwobj_delete_count == 1
        assert statistics.inconsistent_svcobj_delete_count == 2
        assert statistics.inconsistent_userobj_delete_count == 0

    def test_fix_objects_failure_raises(self, importer: FwConfigImport):
        api_call_mock(importer).call.side_effect = Exception("api down")
        with pytest.raises(FwoImporterError, match="object consistency"):
            importer.fix_objects_in_db(["nw-1"], [], [])

    def test_fix_rules_with_nothing_to_do(self, importer: FwConfigImport):
        importer.fix_rules_in_db([])
        api_call_mock(importer).call.assert_not_called()

    def test_fix_rules_updates_statistics(self, importer: FwConfigImport):
        api_call_mock(importer).call.return_value = {
            "data": {"update_firewall_rule": {"returning": [{"rule_id": 1}, {"rule_id": 2}]}}
        }
        importer.fix_rules_in_db(["rule-1", "rule-2"])
        assert importer.import_state.state.stats.statistics.inconsistent_rule_delete_count == 2

    def test_fix_rules_failure_raises(self, importer: FwConfigImport):
        api_call_mock(importer).call.side_effect = Exception("api down")
        with pytest.raises(FwoImporterError, match="rule consistency"):
            importer.fix_rules_in_db(["rule-1"])


class TestFixRulebaseLinks:
    def test_without_removed_links_keeps_previous_config(self, importer: FwConfigImport):
        api_call_mock(importer).call.return_value = {"data": {"update_firewall_rulebase_link": {"affected_rows": 0}}}
        importer.fix_rulebase_links_in_db(FwConfigNormalized())
        assert api_call_mock(importer).call.call_count == 1

    def test_removed_links_reload_gateway_links(self, importer: FwConfigImport):
        previous_config = FwConfigNormalized(gateways=[Gateway(Uid="gw-1", RulebaseLinks=[make_link("stale")])])
        api_call_mock(importer).call.side_effect = [
            {"data": {"update_firewall_rulebase_link": {"affected_rows": 1}}},
            {
                "data": {
                    "device": [
                        {
                            "dev_uid": "gw-1",
                            "rulebase_links": [
                                {
                                    "rulebaseByFromRulebaseId": {"uid": "rb-a"},
                                    "rule": {"rule_uid": "rule-1"},
                                    "rulebase": {"uid": "rb-b"},
                                    "stm_link_type": {"name": "ordered"},
                                    "is_initial": False,
                                    "is_global": False,
                                    "is_section": True,
                                },
                                {
                                    "rulebaseByFromRulebaseId": None,
                                    "rule": None,
                                    "rulebase": {"uid": "rb-c"},
                                    "stm_link_type": {"name": "concatenated"},
                                    "is_initial": True,
                                    "is_global": False,
                                    "is_section": False,
                                },
                            ],
                        }
                    ]
                }
            },
        ]

        importer.fix_rulebase_links_in_db(previous_config)

        links = previous_config.gateways[0].RulebaseLinks
        assert [link.to_rulebase_uid for link in links] == ["rb-b", "rb-c"]
        assert links[0].from_rulebase_uid == "rb-a"
        assert links[1].from_rule_uid is None
        statistics = importer.import_state.state.stats.statistics
        assert statistics.inconsistent_rulebase_link_delete_count == 1

    def test_gateway_missing_in_db_links_raises(self, importer: FwConfigImport):
        previous_config = FwConfigNormalized(gateways=[Gateway(Uid="gw-unknown")])
        api_call_mock(importer).call.side_effect = [
            {"data": {"update_firewall_rulebase_link": {"affected_rows": 1}}},
            {"data": {"device": []}},
        ]
        with pytest.raises(FwoImporterError, match="fetch rulebase links"):
            importer.fix_rulebase_links_in_db(previous_config)

    def test_removal_failure_raises(self, importer: FwConfigImport):
        api_call_mock(importer).call.side_effect = Exception("api down")
        with pytest.raises(FwoImporterError, match="remove inconsistent rulebase links"):
            importer.fix_rulebase_links_in_db(FwConfigNormalized())


class TestFixRuleToGwRefs:
    def prepare_state(self, importer: FwConfigImport) -> None:
        importer.import_state.state.mgm_details.current_mgm_id = 3
        importer.import_state.state.gateway_map = {3: {"gw-1": 99}}

    def test_without_gateways_does_nothing(self, importer: FwConfigImport):
        importer.import_state.state.gateway_map = {}
        importer.fix_rule_to_gw_refs_in_db(FwConfigNormalized(), None)
        api_call_mock(importer).call.assert_not_called()

    def test_removes_stale_refs_and_inserts_missing_ones(self, importer: FwConfigImport):
        self.prepare_state(importer)
        api_call_mock(importer).call.side_effect = [
            {
                "data": {
                    "firewall_rule_enforced_on_gateway": [
                        {"rule": {"removed": 123, "rule_uid": "old"}, "device": {"dev_uid": "gw-1"}},
                        {"rule": {"removed": None, "rule_uid": "unexpected"}, "device": {"dev_uid": "gw-1"}},
                    ]
                }
            },
            {"data": {"update_firewall_rule_enforced_on_gateway": {"affected_rows": 1}}},
        ]
        with (
            patch(
                "model_controllers.fwconfig_import.FwConfigImportRule.get_rule_to_gw_refs",
                return_value={("new", "gw-1")},
            ),
            patch.object(FwConfigImport, "_insert_missing_rule_to_gw_refs_in_db") as ref_inserter,
        ):
            importer.fix_rule_to_gw_refs_in_db(FwConfigNormalized(), FwConfigNormalized())

        ref_inserter.assert_called_once_with({("new", "gw-1")})
        assert importer.import_state.state.stats.statistics.inconsistent_ref_delete_count == 1

    def test_query_errors_raise(self, importer: FwConfigImport):
        self.prepare_state(importer)
        api_call_mock(importer).call.return_value = {"errors": ["nope"]}
        with pytest.raises(FwoImporterError, match="rules enforced on gateways"):
            importer.fix_rule_to_gw_refs_in_db(FwConfigNormalized(), None)

    def test_insert_missing_refs_maps_rule_ids(self, importer: FwConfigImport):
        self.prepare_state(importer)
        api_call_mock(importer).call.side_effect = [
            {"data": {"firewall_rule": [{"rule_uid": "rule-1", "rule_id": 11, "rule_create": 100}]}},
            {"data": {"insert_firewall_rule_enforced_on_gateway": {"affected_rows": 1}}},
        ]

        importer._insert_missing_rule_to_gw_refs_in_db({("rule-1", "gw-1")})

        insert_variables = api_call_mock(importer).call.call_args_list[1].kwargs["query_variables"]
        assert insert_variables["rulesEnforcedOnGateway"] == [{"rule_id": 11, "dev_id": 99, "created": 100}]

    def test_insert_missing_refs_with_empty_set_does_nothing(self, importer: FwConfigImport):
        importer._insert_missing_rule_to_gw_refs_in_db(set())
        api_call_mock(importer).call.assert_not_called()

    def test_insert_missing_refs_fetch_errors_raise(self, importer: FwConfigImport):
        self.prepare_state(importer)
        api_call_mock(importer).call.return_value = {"errors": ["nope"]}
        with pytest.raises(FwoImporterError, match="fetch rule ids"):
            importer._insert_missing_rule_to_gw_refs_in_db({("rule-1", "gw-1")})


class TestFixRefTablesAndChangelog:
    def test_fix_ref_tables_updates_statistics(self, importer: FwConfigImport):
        api_call_mock(importer).call.return_value = {
            "data": {"update_rule_from": {"affected_rows": 2}, "update_rule_to": {"affected_rows": 1}}
        }
        importer.fix_ref_tables_in_db()
        assert importer.import_state.state.stats.statistics.inconsistent_ref_delete_count == 3

    def test_fix_ref_tables_failure_raises(self, importer: FwConfigImport):
        api_call_mock(importer).call.side_effect = Exception("api down")
        with pytest.raises(FwoImporterError, match="ref tables"):
            importer.fix_ref_tables_in_db()

    def test_fix_changelog_without_entries_to_fix(self, importer: FwConfigImport):
        api_call_mock(importer).call.return_value = {
            "data": {
                "changelog_rule": [{"new_rule_id": 10, "old_rule_id": 8, "log_rule_id": 5, "rule": {"rule_uid": "r"}}]
            }
        }
        importer.fix_changelog_rule()
        assert api_call_mock(importer).call.call_count == 1

    def test_fix_changelog_updates_broken_entries(self, importer: FwConfigImport):
        api_call_mock(importer).call.side_effect = [
            {
                "data": {
                    "changelog_rule": [
                        {"new_rule_id": 10, "old_rule_id": 10, "log_rule_id": 5, "rule": {"rule_uid": "rule-1"}}
                    ]
                }
            },
            {
                "data": {
                    "firewall_rule": [
                        {"rule_uid": "rule-1", "rule_id": 8},
                        {"rule_uid": "rule-1", "rule_id": 10},
                    ]
                }
            },
            {"data": {"update_changelog_rule_many": [{"affected_rows": 1}]}},
        ]

        importer.fix_changelog_rule()

        update_variables = api_call_mock(importer).call.call_args_list[2].kwargs["query_variables"]
        assert update_variables["updates"] == [{"where": {"log_rule_id": {"_eq": 5}}, "_set": {"old_rule_id": 8}}]

    def test_fix_changelog_query_errors_raise(self, importer: FwConfigImport):
        api_call_mock(importer).call.return_value = {"errors": ["nope"]}
        with pytest.raises(FwoImporterError, match="changelog entries"):
            importer.fix_changelog_rule()


def test_fwconfig_import_object_property(importer: FwConfigImport):
    assert importer.fwconfig_import_object is importer._fw_config_import_object

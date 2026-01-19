import copy
import unittest.mock
from typing import Any

from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.import_state_controller import ImportStateController
from models.rulebase import Rulebase
from pytest_mock import MockerFixture
from services.global_state import GlobalState
from services.uid2id_mapper import Uid2IdMapper
from unit_tests.utils.config_builder import FwConfigBuilder
from unit_tests.utils.rule_helper_functions import insert_rule_in_config, move_rule_in_config, remove_rule_from_rulebase
from unit_tests.utils.test_utils import mock_get_graphql_code


class TestFwconfigImportRuleUpdateRulebaseDiffOldMigration:
    def test_update_rulebase_diffs_on_insert_delete_and_move(
        self,
        import_state_controller: ImportStateController,
        fwconfig_import_rule: FwConfigImportRule,
        fwconfig_builder: FwConfigBuilder,
        api_connection: FwoApi,
        api_call: FwoApiCall,
        global_state: GlobalState,
        mocker: MockerFixture,
        uid2id_mapper: Uid2IdMapper,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )
        mock_get_graphql_code(mocker, "query { dummy }")

        global_state.normalized_config = config
        global_state.previous_config = copy.deepcopy(config)
        fwconfig_import_rule.normalized_config = global_state.normalized_config

        rulebase = global_state.normalized_config.rulebases[0]
        rule_uids = list(rulebase.rules.keys())
        rule_uid = rule_uids[0]

        uid2id_mapper.get_rulebase_id = unittest.mock.Mock(return_value=69)
        fwconfig_import_rule.uid2id_mapper.get_network_object_id = unittest.mock.Mock(return_value=42)
        fwconfig_import_rule.uid2id_mapper.rule_uid2id.get = unittest.mock.Mock(return_value=43)
        fwconfig_import_rule.uid2id_mapper.get_service_object_id = unittest.mock.Mock(return_value=84)

        api_connection.call = unittest.mock.Mock(
            return_value={"data": {"insert_rulebase": {"affected_rows": 1}, "rule": []}}
        )

        def side_effect_mark_rules_removed(removedRuleUids: dict[str, list[int]]) -> tuple[int, list[int]]:
            changes = 0
            collectedRemovedRuleIds: list[int] = []
            for rulebase in removedRuleUids:
                changes += len(removedRuleUids[rulebase])
                collectedRemovedRuleIds.extend(removedRuleUids[rulebase])

            return changes, collectedRemovedRuleIds

        fwconfig_import_rule.mark_rules_removed = unittest.mock.Mock(side_effect=side_effect_mark_rules_removed)

        def side_effect_create_new_rule_version(
            rule_uids: dict[str, list[str]],
        ) -> tuple[int, list[int], list[dict[str, Any]]]:
            changes = 0
            collected_rule_ids: list[int] = []
            insert_rules_return: list[dict[str, Any]] = []

            for rulebase_rule_uids in rule_uids.values():
                changes += len(rulebase_rule_uids)
                collected_rule_ids = list(range(1, len(rulebase_rule_uids) + 1))
                for counter in range(len(rulebase_rule_uids)):
                    insert_rule_return: dict[str, Any] = {}
                    insert_rule_return["rule_uid"] = rulebase_rule_uids[counter]
                    insert_rule_return["rule_id"] = changes + counter + 1
                    insert_rules_return.append(insert_rule_return)

            return changes, collected_rule_ids, insert_rules_return

        fwconfig_import_rule.create_new_rule_version = unittest.mock.Mock(
            side_effect=side_effect_create_new_rule_version
        )

        def side_effect_add_new_rules(rulebases: list[Rulebase]) -> tuple[int, list[dict[str, Any]]]:
            changes = 0
            newRuleIds: list[dict[str, Any]] = []

            for rulebase in rulebases:
                for rule in list(rulebase.rules.values()):
                    changes += 1
                    newRuleIds.append({"rule_uid": rule.rule_uid, "rule_id": changes})

            return changes, newRuleIds

        fwconfig_import_rule.add_new_rules = unittest.mock.Mock(side_effect=side_effect_add_new_rules)

        def api_call_side_effect(
            query: str, query_variables: dict[str, dict[str, Any]], analyze_payload: bool = False
        ) -> dict[str, Any]:
            outcome: dict[str, Any] = {"data": {}}

            if "ruleMetadata" in query_variables.keys():
                outcome["data"].update(
                    {"insert_rule_metadata": {"affected_rows": len(query_variables.get("ruleMetadata", []))}}
                )

            if "rulebases" in query_variables.keys():
                outcome["data"].update(
                    {
                        "insert_rulebase": {
                            "affected_rows": len(query_variables.get("rulebases", [])),
                            "returning": [{"id": 999} for _ in range(len(query_variables.get("rulebases", [])))],
                        }
                    }
                )

            if "uids" in query_variables.keys() and "objects" in query_variables.keys():
                outcome["data"].update(
                    {
                        "update_rule": {
                            "affected_rows": len(query_variables.get("uids", [])),
                            "returning": [
                                {"rule_id": 888 + i, "rule_uid": uid}
                                for i, uid in enumerate(query_variables.get("uids", []))
                            ],
                        },
                        "insert_rule": {
                            "affected_rows": len(query_variables.get("uids", [])),
                            "returning": [
                                {"rule_id": 777 + i, "rule_uid": uid}
                                for i, uid in enumerate(query_variables.get("uids", []))
                            ],
                        },
                    }
                )

            if "ruleFroms" in query_variables.keys():
                outcome["data"].update(
                    {
                        "insert_rule_from": {
                            "affected_rows": len(query_variables.get("ruleFroms", [])),
                        },
                        "insert_rule_to": {
                            "affected_rows": len(query_variables.get("ruleTos", [])),
                        },
                        "insert_rule_service": {
                            "affected_rows": len(query_variables.get("ruleServices", [])),
                        },
                        "insert_rule_nwobj_resolved": {
                            "affected_rows": len(query_variables.get("ruleNwObjResolveds", [])),
                        },
                        "insert_rule_svc_resolved": {
                            "affected_rows": len(query_variables.get("ruleSvcResolveds", [])),
                        },
                        "insert_rule_user_resolved": {
                            "affected_rows": len(query_variables.get("ruleUserResolveds", [])),
                        },
                        "insert_rule_from_zone": {
                            "affected_rows": len(query_variables.get("ruleFromZones", [])),
                        },
                        "insert_rule_to_zone": {
                            "affected_rows": len(query_variables.get("ruleToZones", [])),
                        },
                        "update_rule_from": {
                            "affected_rows": len(query_variables.get("ruleFroms", [])),
                        },
                        "update_rule_to": {
                            "affected_rows": len(query_variables.get("ruleTos", [])),
                        },
                        "update_rule_service": {
                            "affected_rows": len(query_variables.get("ruleServices", [])),
                        },
                        "update_rule_nwobj_resolved": {
                            "affected_rows": len(query_variables.get("ruleNwObjResolveds", [])),
                        },
                        "update_rule_svc_resolved": {
                            "affected_rows": len(query_variables.get("ruleSvcResolveds", [])),
                        },
                        "update_rule_user_resolved": {
                            "affected_rows": len(query_variables.get("ruleUserResolveds", [])),
                        },
                        "update_rule_from_zone": {
                            "affected_rows": len(query_variables.get("ruleFromZones", [])),
                        },
                        "update_rule_to_zone": {
                            "affected_rows": len(query_variables.get("ruleToZones", [])),
                        },
                    }
                )
            return outcome

        api_call.call = unittest.mock.Mock(side_effect=api_call_side_effect)
        remove_rule_from_rulebase(global_state.normalized_config, rulebase.uid, rule_uid, rule_uids)
        insert_rule_in_config(global_state.normalized_config, rulebase.uid, 0, rule_uids, fwconfig_builder)
        move_rule_in_config(global_state.normalized_config, rulebase.uid, 9, 0, rule_uids)

        # Act

        fwconfig_import_rule.update_rulebase_diffs(global_state.previous_config)

        # The order of the entries in normalized_config
        assert rule_uids == list(rulebase.rules.keys())

        sorted_rulebase_rules = sorted(rulebase.rules.values(), key=lambda r: r.rule_num_numeric)
        sorted_rulebase_rules_uids = [r.rule_uid for r in sorted_rulebase_rules]

        # The sequence of the rule_num_numeric values
        assert rule_uids == sorted_rulebase_rules_uids

        # Insert, delete and move recognized in ImportDetails
        assert import_state_controller.state.stats.statistics.rule_add_count == 1
        assert import_state_controller.state.stats.statistics.rule_delete_count == 1
        assert import_state_controller.state.stats.statistics.rule_change_count == 1
        assert import_state_controller.state.stats.statistics.rule_move_count == 1


class TestFwconfigImportRuleComputeNumForChangedRule:
    pass

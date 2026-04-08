import copy
import unittest.mock
from typing import Any

import pytest
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from models.rule import RuleNormalized
from pytest_mock import MockerFixture
from services.uid2id_mapper import Uid2IdMapper
from states.import_state import ImportState
from states.management_state import ManagementState
from test.utils.config_builder import FwConfigBuilder
from test.utils.rule_helper_functions import insert_rule_in_config, move_rule_in_config, remove_rule_from_rulebase
from test.utils.test_utils import mock_get_graphql_code


@pytest.fixture
def mock_graphql(mocker: MockerFixture):
    mock_get_graphql_code(mocker, "query { dummy }")


@pytest.fixture
def mock_uid2id_mapper_response(uid2id_mapper: Uid2IdMapper, fwconfig_import_rule: FwConfigImportRule):
    uid2id_mapper.get_rulebase_id = unittest.mock.Mock(return_value=69)
    fwconfig_import_rule.uid2id_mapper.get_network_object_id = unittest.mock.Mock(return_value=42)
    fwconfig_import_rule.uid2id_mapper.rule_uid2id.get = unittest.mock.Mock(return_value=43)
    fwconfig_import_rule.uid2id_mapper.get_service_object_id = unittest.mock.Mock(return_value=84)


@pytest.fixture
def mock_api_connection_response(api_connection: FwoApi):
    api_connection.call = unittest.mock.Mock(
        return_value={"data": {"insert_rulebase": {"affected_rows": 1}, "rule": []}}
    )


@pytest.fixture
def mock_fwconfig_import_rule_side_effects(fwconfig_import_rule: FwConfigImportRule):
    def side_effect_mark_rules_removed(removed_rule_uids: list[str]) -> tuple[int, list[int]]:
        changes = 0
        changes = len(removed_rule_uids)
        collected_removed_rule_ids = [42 + i for i in range(len(removed_rule_uids))]

        return changes, collected_removed_rule_ids

    fwconfig_import_rule.mark_rules_removed = unittest.mock.Mock(side_effect=side_effect_mark_rules_removed)

    def side_effect_add_new_rules(rulebases: dict[str, tuple[RuleNormalized, str]]) -> tuple[int, list[dict[str, Any]]]:
        changes = 0
        new_rule_ids: list[dict[str, Any]] = []

        for rule, _rulebase_uid in rulebases.values():
            changes += 1
            new_rule_ids.append({"rule_uid": rule.rule_uid, "rule_id": changes})

        return changes, new_rule_ids

    fwconfig_import_rule.add_new_rules = unittest.mock.Mock(side_effect=side_effect_add_new_rules)


@pytest.fixture
def mock_api_call_response(api_call: FwoApiCall):
    def api_call_side_effect(
        query: str,  # noqa: ARG001
        query_variables: dict[str, dict[str, Any]],
        analyze_payload: bool = False,  # noqa: ARG001
    ) -> dict[str, Any]:
        outcome: dict[str, Any] = {"data": {}}

        if "ruleMetadata" in query_variables:
            outcome["data"].update(
                {"insert_rule_metadata": {"affected_rows": len(query_variables.get("ruleMetadata", []))}}
            )

        if "rulebases" in query_variables:
            outcome["data"].update(
                {
                    "insert_rulebase": {
                        "affected_rows": len(query_variables.get("rulebases", [])),
                        "returning": [{"id": 999} for _ in range(len(query_variables.get("rulebases", [])))],
                    }
                }
            )

        if "uids" in query_variables and "objects" in query_variables:
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

        if "ruleFroms" in query_variables:
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
                    "insert_rule_time": {
                        "affected_rows": len(query_variables.get("ruleTimes", [])),
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
                    "update_rule_time": {
                        "affected_rows": len(query_variables.get("ruleTimes", [])),
                    },
                }
            )

        if "rulesEnforcedOnGateway" in query_variables:
            outcome["data"].update(
                {
                    "update_rule_enforced_on_gateway": {
                        "affected_rows": len(query_variables.get("rulesEnforcedOnGateway", [])),
                    },
                    "insert_rule_enforced_on_gateway": {
                        "affected_rows": len(query_variables.get("rulesEnforcedOnGateway", [])),
                    },
                }
            )

        return outcome

    api_call.call = unittest.mock.Mock(side_effect=api_call_side_effect)


class TestFwconfigImportRuleUpdateRulebaseDiffOldMigration:
    def test_update_rulebase_diffs_on_insert_delete_and_move(
        self,
        import_state: ImportState,
        management_state: ManagementState,
        fwconfig_import_rule: FwConfigImportRule,
        fwconfig_builder: FwConfigBuilder,
        mock_graphql: None,  # noqa: ARG002
        mock_uid2id_mapper_response: None,  # noqa: ARG002
        mock_api_connection_response: None,  # noqa: ARG002
        mock_fwconfig_import_rule_side_effects: None,  # noqa: ARG002
        mock_api_call_response: None,  # noqa: ARG002
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(
            uid2id_mapper=management_state.uid2id_mapper,
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        import_state.lookup_gateway_id = unittest.mock.Mock(return_value=1)
        management_state.previous_config = config
        management_state.normalized_config = copy.deepcopy(config)
        fwconfig_builder.initialize_rule_num_numerics(management_state.previous_config)

        rulebase = management_state.normalized_config.rulebases[0]
        rule_uids = list(rulebase.rules.keys())
        rule_uid = rule_uids[0]
        remove_rule_from_rulebase(management_state.normalized_config, rulebase.uid, rule_uid, rule_uids)
        insert_rule_in_config(management_state.normalized_config, rulebase.uid, 0, rule_uids, fwconfig_builder)
        move_rule_in_config(management_state.normalized_config, rulebase.uid, 9, 0, rule_uids)

        # Act

        fwconfig_import_rule.update_rulebase_diffs(management_state.previous_config)

        # The order of the entries in normalized_config
        assert rule_uids == list(rulebase.rules.keys())

        sorted_rulebase_rules = sorted(rulebase.rules.values(), key=lambda r: r.rule_num_numeric)
        sorted_rulebase_rules_uids = [r.rule_uid for r in sorted_rulebase_rules]

        # The sequence of the rule_num_numeric values
        assert rule_uids == sorted_rulebase_rules_uids

        # Insert, delete and move recognized in ImportDetails
        assert import_state.statistics_controller.statistics.rule_add_count == 1
        assert import_state.statistics_controller.statistics.rule_delete_count == 1
        assert import_state.statistics_controller.statistics.rule_change_count == 1
        assert import_state.statistics_controller.statistics.rule_move_count == 1

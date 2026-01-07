import copy
import unittest.mock

from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized
from services.global_state import GlobalState
from unit_tests.utils.config_builder import FwConfigBuilder
from unit_tests.utils.rule_helper_functions import insert_rule_in_config


def test_update_rulebase_diffs_on_insert_delete_and_move(
    import_state_controller: ImportStateController,
    fwconfig_import_rule: FwConfigImportRule,
    fwconfig_builder: FwConfigBuilder,
    config_tuple: tuple[FwConfigNormalized, str],
    api_connection: FwoApi,
    api_call: FwoApiCall,
    global_state: GlobalState,
):
    # Arrange
    config, _ = config_tuple
    fwconfig_import_rule.normalized_config = copy.deepcopy(config)
    global_state.previous_config = config
    global_state.normalized_config = copy.deepcopy(config)
    previous_config = config

    rulebase = fwconfig_import_rule.normalized_config.rulebases[0]
    rule_uids = list(rulebase.rules.keys())
    rule_uid = rule_uids[0]

    import_state_controller.state.lookup_rulebase_id = unittest.mock.Mock(return_value=69)
    fwconfig_import_rule.uid2id_mapper.get_network_object_id = unittest.mock.Mock(return_value=42)
    fwconfig_import_rule.uid2id_mapper.rule_uid2id.get = unittest.mock.Mock(return_value=43)
    fwconfig_import_rule.uid2id_mapper.get_service_object_id = unittest.mock.Mock(return_value=84)

    api_connection.call = unittest.mock.Mock(
        return_value={"data": {"insert_rulebase": {"affected_rows": 1}, "rule": []}}
    )

    api_call.call = unittest.mock.Mock(
        return_value={
            "data": {
                "insert_rule_from": {"affected_rows": 1},
                "insert_rule_to": {"affected_rows": 1},
                "insert_rule_service": {"affected_rows": 1},
                "insert_rulebase": {"affected_rows": 1, "returning": [{"id": 999}]},
                "rule": 1,
                "insert_rule_metadata": {"affected_rows": 1},
                "update_rule": {
                    "affected_rows": 1,
                    "returning": [{"rule_id": 888, "rule_uid": rule_uid}],
                },
                "insert_rule": {
                    "affected_rows": 1,
                    "returning": [{"rule_id": 777, "rule_uid": rule_uid}],
                },
            }
        }
    )
    # remove_rule_from_rulebase(fwconfig_import_rule.normalized_config, rulebase.uid, rule_uid, rule_uids)
    insert_rule_in_config(fwconfig_import_rule.normalized_config, rulebase.uid, 0, rule_uids, fwconfig_builder)
    # move_rule_in_config(fwconfig_import_rule.normalized_config, rulebase.uid, 9, 0, rule_uids)

    # Act

    fwconfig_import_rule.update_rulebase_diffs(previous_config)
    print(len(fwconfig_import_rule.normalized_config.rulebases[0].rules))
    print(len(previous_config.rulebases[0].rules))
    # Assert

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

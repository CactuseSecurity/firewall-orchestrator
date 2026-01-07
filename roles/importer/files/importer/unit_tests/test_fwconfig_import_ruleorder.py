import copy

from fwo_const import RULE_NUM_NUMERIC_STEPS
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.fwconfig_import_ruleorder import RuleOrderService
from models.fwconfig_normalized import FwConfigNormalized
from services.global_state import GlobalState
from unit_tests.utils.config_builder import FwConfigBuilder
from unit_tests.utils.rule_helper_functions import (
    get_rule,
    insert_rule_in_config,
    move_rule_in_config,
    remove_rule_from_rulebase,
)


def test_initialize_on_initial_import(
    global_state: GlobalState,
    rule_order_service: RuleOrderService,
):
    # Act
    rule_order_service.update_rule_order_diffs()

    # Assert
    assert global_state.normalized_config is not None
    for rulebase in global_state.normalized_config.rulebases:
        for index, rule_uid in enumerate(rulebase.rules):
            expected_rule_num_numeric = (index + 1) * RULE_NUM_NUMERIC_STEPS
            actual_rule_num_numeric = rulebase.rules[rule_uid].rule_num_numeric
            assert actual_rule_num_numeric == expected_rule_num_numeric, (
                f"Rule UID: {rule_uid}, actual rule_num_numeric: {actual_rule_num_numeric}, expected: {expected_rule_num_numeric}"
            )


def test_initialize_on_insert_delete_and_move(
    global_state: GlobalState,
    rule_order_service: RuleOrderService,
    fwconfig_builder: FwConfigBuilder,
    config_tuple: tuple[FwConfigNormalized, str],
):
    # Arrange
    config, _ = config_tuple
    global_state.normalized_config = config
    global_state.previous_config = copy.deepcopy(config)

    rulebase = config.rulebases[0]
    rule_uids = list(rulebase.rules.keys())
    removed_rule_uid = rule_uids[0]

    remove_rule_from_rulebase(config, rulebase.uid, removed_rule_uid, rule_uids)
    inserted_rule_uid = insert_rule_in_config(config, rulebase.uid, 0, rule_uids, fwconfig_builder)
    moved_rule_uid = move_rule_in_config(config, rulebase.uid, 9, 0, rule_uids)
    # Act
    rule_order_service.update_rule_order_diffs()

    # Assert
    assert inserted_rule_uid is not None
    insert_rule = get_rule(config, 0, inserted_rule_uid)
    assert insert_rule is not None

    moved_rule = get_rule(config, 0, moved_rule_uid)
    assert moved_rule is not None

    assert insert_rule.rule_num_numeric == RULE_NUM_NUMERIC_STEPS, (
        f"Inserted rule_num_numeric is {config.rulebases[0].rules[inserted_rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS}"
    )

    assert moved_rule.rule_num_numeric == RULE_NUM_NUMERIC_STEPS / 2, (
        f"Moved rule_num_numeric is {config.rulebases[0].rules[moved_rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
    )


def test_initialize_on_consecutive_insertions(
    global_state: GlobalState,
    rule_order_service: RuleOrderService,
    fwconfig_builder: FwConfigBuilder,
    config_tuple: tuple[FwConfigNormalized, str],
):
    # Arrange
    config, _ = config_tuple
    global_state.normalized_config = config
    global_state.previous_config = copy.deepcopy(config)

    assert global_state.normalized_config is not None
    rulebase = global_state.normalized_config.rulebases[0]
    rule_uids = list(rulebase.rules.keys())

    # Inserting three new rules at the beginning of the rulebase
    rule_1_1_uid = insert_rule_in_config(global_state.normalized_config, rulebase.uid, 0, rule_uids, fwconfig_builder)
    rule_1_2_uid = insert_rule_in_config(global_state.normalized_config, rulebase.uid, 1, rule_uids, fwconfig_builder)
    rule_1_3_uid = insert_rule_in_config(global_state.normalized_config, rulebase.uid, 2, rule_uids, fwconfig_builder)

    # Inserting three new rules in the middle of the rulebase
    rule_1_6_uid = insert_rule_in_config(global_state.normalized_config, rulebase.uid, 5, rule_uids, fwconfig_builder)
    rule_1_7_uid = insert_rule_in_config(global_state.normalized_config, rulebase.uid, 6, rule_uids, fwconfig_builder)
    rule_1_8_uid = insert_rule_in_config(global_state.normalized_config, rulebase.uid, 7, rule_uids, fwconfig_builder)

    # Inserting three new rules at the end of the rulebase
    rule_1_17_uid = insert_rule_in_config(global_state.normalized_config, rulebase.uid, 16, rule_uids, fwconfig_builder)
    rule_1_18_uid = insert_rule_in_config(global_state.normalized_config, rulebase.uid, 17, rule_uids, fwconfig_builder)
    rule_1_19_uid = insert_rule_in_config(global_state.normalized_config, rulebase.uid, 18, rule_uids, fwconfig_builder)

    # Act

    rule_order_service.update_rule_order_diffs()

    # Assert
    assert rule_1_1_uid is not None
    rule_1_1 = get_rule(global_state.normalized_config, 0, rule_1_1_uid)
    assert rule_1_1 is not None
    assert rule_1_1.rule_num_numeric == RULE_NUM_NUMERIC_STEPS / 2, (
        f"Rule 1.1 rule_num_numeric: {rule_1_1.rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
    )
    assert rule_1_2_uid is not None
    rule_1_2 = get_rule(global_state.normalized_config, 0, rule_1_2_uid)
    assert rule_1_2 is not None
    assert rule_1_2.rule_num_numeric == 3 * RULE_NUM_NUMERIC_STEPS / 4, (
        f"Rule 1.2 rule_num_numeric: {rule_1_2.rule_num_numeric}, expected {3 * RULE_NUM_NUMERIC_STEPS / 4}"
    )

    assert rule_1_3_uid is not None
    rule_1_3 = get_rule(global_state.normalized_config, 0, rule_1_3_uid)
    assert rule_1_3 is not None
    assert rule_1_3.rule_num_numeric == 7 * RULE_NUM_NUMERIC_STEPS / 8, (
        f"Rule 1.3 rule_num_numeric: {rule_1_3.rule_num_numeric}, expected {7 * RULE_NUM_NUMERIC_STEPS / 8}"
    )

    assert rule_1_6_uid is not None
    rule_1_6 = get_rule(global_state.normalized_config, 0, rule_1_6_uid)
    assert rule_1_6 is not None
    assert rule_1_6.rule_num_numeric == 5 * RULE_NUM_NUMERIC_STEPS / 2, (
        f"Rule 1.6 rule_num_numeric: {rule_1_6.rule_num_numeric}, expected {5 * RULE_NUM_NUMERIC_STEPS / 2}"
    )
    assert rule_1_7_uid is not None
    rule_1_7 = get_rule(global_state.normalized_config, 0, rule_1_7_uid)
    assert rule_1_7 is not None
    assert rule_1_7.rule_num_numeric == 11 * RULE_NUM_NUMERIC_STEPS / 4, (
        f"Rule 1.7 rule_num_numeric: {rule_1_7.rule_num_numeric}, expected {11 * RULE_NUM_NUMERIC_STEPS / 4}"
    )
    assert rule_1_8_uid is not None
    rule_1_8 = get_rule(global_state.normalized_config, 0, rule_1_8_uid)
    assert rule_1_8 is not None
    assert rule_1_8.rule_num_numeric == 23 * RULE_NUM_NUMERIC_STEPS / 8, (
        f"Rule 1.8 rule_num_numeric: {rule_1_8.rule_num_numeric}, expected {23 * RULE_NUM_NUMERIC_STEPS / 8}"
    )

    assert rule_1_17_uid is not None
    rule_1_17 = get_rule(global_state.normalized_config, 0, rule_1_17_uid)
    assert rule_1_17 is not None
    assert rule_1_17.rule_num_numeric == 11 * RULE_NUM_NUMERIC_STEPS, (
        f"Rule 1.17 rule_num_numeric: {rule_1_17.rule_num_numeric}, expected {11 * RULE_NUM_NUMERIC_STEPS}"
    )
    assert rule_1_18_uid is not None
    rule_1_18 = get_rule(global_state.normalized_config, 0, rule_1_18_uid)
    assert rule_1_18 is not None
    assert rule_1_18.rule_num_numeric == 12 * RULE_NUM_NUMERIC_STEPS, (
        f"Rule 1.18 rule_num_numeric: {rule_1_18.rule_num_numeric}, expected {12 * RULE_NUM_NUMERIC_STEPS}"
    )
    assert rule_1_19_uid is not None
    rule_1_19 = get_rule(global_state.normalized_config, 0, rule_1_19_uid)
    assert rule_1_19 is not None
    assert rule_1_19.rule_num_numeric == 13 * RULE_NUM_NUMERIC_STEPS, (
        f"Rule 1.19 rule_num_numeric: {rule_1_19.rule_num_numeric}, expected {13 * RULE_NUM_NUMERIC_STEPS}"
    )


def test_initialize_on_move_across_rulebases(
    rule_order_service: RuleOrderService,
    fwconfig_builder: FwConfigBuilder,
    fwconfig_import_rule_mock: FwConfigImportRule,
    config_tuple: tuple[FwConfigNormalized, str],
):
    # Arrange
    config, _ = config_tuple
    fwconfig_import_rule_mock.normalized_config = config

    assert fwconfig_import_rule_mock.normalized_config is not None
    source_rulebase = fwconfig_import_rule_mock.normalized_config.rulebases[0]
    source_rulebase_uids = list(source_rulebase.rules.keys())
    target_rulebase = fwconfig_import_rule_mock.normalized_config.rulebases[1]
    target_rulebase_uids = list(target_rulebase.rules.keys())

    deleted_rule = remove_rule_from_rulebase(
        fwconfig_import_rule_mock.normalized_config, source_rulebase.uid, source_rulebase_uids[0], source_rulebase_uids
    )
    insert_rule_in_config(
        fwconfig_import_rule_mock.normalized_config,
        target_rulebase.uid,
        0,
        target_rulebase_uids,
        fwconfig_builder,
        deleted_rule,
    )

    # Act

    rule_order_service.update_rule_order_diffs()

    # Assert
    assert deleted_rule.rule_uid is not None
    rule = get_rule(fwconfig_import_rule_mock.normalized_config, 1, deleted_rule.rule_uid)
    assert rule is not None
    assert rule.rule_num_numeric == RULE_NUM_NUMERIC_STEPS / 2, (
        f"Moved rule_num_numeric is {fwconfig_import_rule_mock.normalized_config.rulebases[1].rules[deleted_rule.rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
    )


def test_update_rulebase_diffs_on_moves_to_beginning_middle_and_end_of_rulebase(
    rule_order_service: RuleOrderService,
    fwconfig_import_rule_mock: FwConfigImportRule,
    config_tuple: tuple[FwConfigNormalized, str],
):
    # Arrange
    config, _ = config_tuple
    fwconfig_import_rule_mock.normalized_config = config
    rulebase = fwconfig_import_rule_mock.normalized_config.rulebases[0]
    rule_uids = list(rulebase.rules.keys())

    beginning_rule_uid = move_rule_in_config(
        fwconfig_import_rule_mock.normalized_config, rulebase.uid, 5, 0, rule_uids
    )  # Move to beginning
    middle_rule_uid = move_rule_in_config(
        fwconfig_import_rule_mock.normalized_config, rulebase.uid, 1, 4, rule_uids
    )  # Move to middle
    end_rule_uid = move_rule_in_config(
        fwconfig_import_rule_mock.normalized_config, rulebase.uid, 2, 9, rule_uids
    )  # Move to end

    # Act

    rule_order_service.update_rule_order_diffs()

    # Assert
    assert beginning_rule_uid is not None
    beginning_rule = get_rule(fwconfig_import_rule_mock.normalized_config, 0, beginning_rule_uid)
    assert beginning_rule is not None
    assert beginning_rule.rule_num_numeric == RULE_NUM_NUMERIC_STEPS / 2, (
        f"Beginning moved rule_num_numeric is {fwconfig_import_rule_mock.normalized_config.rulebases[0].rules[beginning_rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
    )

    assert middle_rule_uid is not None
    middle_rule = get_rule(fwconfig_import_rule_mock.normalized_config, 0, middle_rule_uid)
    assert middle_rule is not None
    assert middle_rule.rule_num_numeric == 4608, (
        f"Middle moved rule_num_numeric is {fwconfig_import_rule_mock.normalized_config.rulebases[0].rules[middle_rule_uid].rule_num_numeric}, expected 4608"
    )
    assert end_rule_uid is not None
    end_rule = get_rule(fwconfig_import_rule_mock.normalized_config, 0, end_rule_uid)
    assert end_rule is not None
    assert end_rule.rule_num_numeric == 11 * RULE_NUM_NUMERIC_STEPS, (
        f"End moved rule_num_numeric is {fwconfig_import_rule_mock.normalized_config.rulebases[0].rules[end_rule_uid].rule_num_numeric}, expected {11 * RULE_NUM_NUMERIC_STEPS}"
    )

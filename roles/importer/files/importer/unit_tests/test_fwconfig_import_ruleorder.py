import copy

from fwo_const import RULE_NUM_NUMERIC_STEPS
from model_controllers.fwconfig_import_ruleorder import RuleOrderService
from models.fwconfig_normalized import FwConfigNormalized
from models.rule import RuleNormalized
from services.global_state import GlobalState
from unit_tests.utils.config_builder import FwConfigBuilder


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


def _remove_rule_from_rulebase(
    config: FwConfigNormalized, rulebase_uid: str, rule_uid: str, uid_sequence: list[str] | None = None
) -> RuleNormalized:
    """
    Imitates the deletion of a rule in the config dict.
    """
    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)

    if rulebase:
        rule = rulebase.rules.pop(rule_uid)

        if uid_sequence:
            uid_sequence[:] = [uid for uid in uid_sequence if uid != rule_uid]

        return rule
    raise ValueError(f"Rulebase with UID {rulebase_uid} not found.")


def _reorder_rulebase_rules_dict(config: FwConfigNormalized, rulebase_uid: str, rule_uids: list[str]):
    """
    Imitates the changes in order in the config dict.
    """
    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)

    if rulebase:
        rules = copy.deepcopy(rulebase.rules)
        rulebase.rules = {}
        for rule_uid in rule_uids:
            rulebase.rules[rule_uid] = rules[rule_uid]


def _insert_rule_in_config(
    config: FwConfigNormalized,
    rulebase_uid: str,
    rule_position: int,
    rule_uids: list[str],
    config_builder: FwConfigBuilder,
    rule: RuleNormalized | None = None,
):
    """
    Imitates the insertion of a rule in the config dict.
    """
    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)
    inserted_rule_uid = None

    if rulebase:
        if rule is None:
            inserted_rule = config_builder.add_rule(config, rulebase_uid)
        else:
            inserted_rule = rule
            assert inserted_rule.rule_uid is not None
            rulebase.rules[inserted_rule.rule_uid] = inserted_rule

        assert inserted_rule.rule_uid is not None
        rule_uids.insert(rule_position, inserted_rule.rule_uid)

        _reorder_rulebase_rules_dict(config, rulebase_uid, rule_uids)

        inserted_rule_uid = inserted_rule.rule_uid

    return inserted_rule_uid


def _move_rule_in_config(
    config: FwConfigNormalized, rulebase_uid: str, source_position: int, target_position: int, rule_uids: list[str]
):
    """
    Imitates the moving of a rule in the config dict.
    """
    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)
    moved_rule_uid = ""

    if rulebase:
        rule_uid = list(rulebase.rules.keys())[source_position]
        rule = rulebase.rules.pop(rule_uid)
        rulebase.rules[rule_uid] = rule
        rule_uids.pop(source_position)
        rule_uids.insert(target_position, rule_uid)

        _reorder_rulebase_rules_dict(config, rulebase.uid, rule_uids)

        moved_rule_uid = rule_uid

    return moved_rule_uid


def _get_rule(normalized_config: FwConfigNormalized, rulebase_index: int, rule_uid: str) -> RuleNormalized | None:
    """
    Helper method to get a rule from the normalized config.
    """
    rulebase = normalized_config.rulebases[rulebase_index]
    return rulebase.rules.get(rule_uid, None)


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

    _remove_rule_from_rulebase(config, rulebase.uid, removed_rule_uid, rule_uids)
    inserted_rule_uid = _insert_rule_in_config(config, rulebase.uid, 0, rule_uids, fwconfig_builder)
    moved_rule_uid = _move_rule_in_config(config, rulebase.uid, 9, 0, rule_uids)
    # Act
    rule_order_service.update_rule_order_diffs()

    # Assert
    assert inserted_rule_uid is not None
    insert_rule = _get_rule(config, 0, inserted_rule_uid)
    assert insert_rule is not None

    moved_rule = _get_rule(config, 0, moved_rule_uid)
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
):
    # Arrange

    assert global_state.normalized_config is not None
    rulebase = global_state.normalized_config.rulebases[0]
    rule_uids = list(rulebase.rules.keys())

    # Inserting three new rules at the beginning of the rulebase
    rule_1_1_uid = _insert_rule_in_config(global_state.normalized_config, rulebase.uid, 0, rule_uids, fwconfig_builder)
    rule_1_2_uid = _insert_rule_in_config(global_state.normalized_config, rulebase.uid, 1, rule_uids, fwconfig_builder)
    rule_1_3_uid = _insert_rule_in_config(global_state.normalized_config, rulebase.uid, 2, rule_uids, fwconfig_builder)

    # Inserting three new rules in the middle of the rulebase
    rule_1_6_uid = _insert_rule_in_config(global_state.normalized_config, rulebase.uid, 5, rule_uids, fwconfig_builder)
    rule_1_7_uid = _insert_rule_in_config(global_state.normalized_config, rulebase.uid, 6, rule_uids, fwconfig_builder)
    rule_1_8_uid = _insert_rule_in_config(global_state.normalized_config, rulebase.uid, 7, rule_uids, fwconfig_builder)

    # Inserting three new rules at the end of the rulebase
    rule_1_17_uid = _insert_rule_in_config(
        global_state.normalized_config, rulebase.uid, 16, rule_uids, fwconfig_builder
    )
    rule_1_18_uid = _insert_rule_in_config(
        global_state.normalized_config, rulebase.uid, 17, rule_uids, fwconfig_builder
    )
    rule_1_19_uid = _insert_rule_in_config(
        global_state.normalized_config, rulebase.uid, 18, rule_uids, fwconfig_builder
    )

    # Act

    rule_order_service.update_rule_order_diffs()

    # Assert
    assert rule_1_1_uid is not None
    rule_1_1 = _get_rule(global_state.normalized_config, 0, rule_1_1_uid)
    assert rule_1_1 is not None
    assert rule_1_1.rule_num_numeric == RULE_NUM_NUMERIC_STEPS / 2, (
        f"Rule 1.1 rule_num_numeric: {rule_1_1.rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
    )
    assert rule_1_2_uid is not None
    rule_1_2 = _get_rule(global_state.normalized_config, 0, rule_1_2_uid)
    assert rule_1_2 is not None
    assert rule_1_2.rule_num_numeric == 3 * RULE_NUM_NUMERIC_STEPS / 4, (
        f"Rule 1.2 rule_num_numeric: {rule_1_2.rule_num_numeric}, expected {3 * RULE_NUM_NUMERIC_STEPS / 4}"
    )

    assert rule_1_3_uid is not None
    rule_1_3 = _get_rule(global_state.normalized_config, 0, rule_1_3_uid)
    assert rule_1_3 is not None
    assert rule_1_3.rule_num_numeric == 7 * RULE_NUM_NUMERIC_STEPS / 8, (
        f"Rule 1.3 rule_num_numeric: {rule_1_3.rule_num_numeric}, expected {7 * RULE_NUM_NUMERIC_STEPS / 8}"
    )

    assert rule_1_6_uid is not None
    rule_1_6 = _get_rule(global_state.normalized_config, 0, rule_1_6_uid)
    assert rule_1_6 is not None
    assert rule_1_6.rule_num_numeric == 5 * RULE_NUM_NUMERIC_STEPS / 2, (
        f"Rule 1.6 rule_num_numeric: {rule_1_6.rule_num_numeric}, expected {5 * RULE_NUM_NUMERIC_STEPS / 2}"
    )
    assert rule_1_7_uid is not None
    rule_1_7 = _get_rule(global_state.normalized_config, 0, rule_1_7_uid)
    assert rule_1_7 is not None
    assert rule_1_7.rule_num_numeric == 11 * RULE_NUM_NUMERIC_STEPS / 4, (
        f"Rule 1.7 rule_num_numeric: {rule_1_7.rule_num_numeric}, expected {11 * RULE_NUM_NUMERIC_STEPS / 4}"
    )
    assert rule_1_8_uid is not None
    rule_1_8 = _get_rule(global_state.normalized_config, 0, rule_1_8_uid)
    assert rule_1_8 is not None
    assert rule_1_8.rule_num_numeric == 23 * RULE_NUM_NUMERIC_STEPS / 8, (
        f"Rule 1.8 rule_num_numeric: {rule_1_8.rule_num_numeric}, expected {23 * RULE_NUM_NUMERIC_STEPS / 8}"
    )

    assert rule_1_17_uid is not None
    rule_1_17 = _get_rule(global_state.normalized_config, 0, rule_1_17_uid)
    assert rule_1_17 is not None
    assert rule_1_17.rule_num_numeric == 11 * RULE_NUM_NUMERIC_STEPS, (
        f"Rule 1.17 rule_num_numeric: {rule_1_17.rule_num_numeric}, expected {11 * RULE_NUM_NUMERIC_STEPS}"
    )
    assert rule_1_18_uid is not None
    rule_1_18 = _get_rule(global_state.normalized_config, 0, rule_1_18_uid)
    assert rule_1_18 is not None
    assert rule_1_18.rule_num_numeric == 12 * RULE_NUM_NUMERIC_STEPS, (
        f"Rule 1.18 rule_num_numeric: {rule_1_18.rule_num_numeric}, expected {12 * RULE_NUM_NUMERIC_STEPS}"
    )
    assert rule_1_19_uid is not None
    rule_1_19 = _get_rule(global_state.normalized_config, 0, rule_1_19_uid)
    assert rule_1_19 is not None
    assert rule_1_19.rule_num_numeric == 13 * RULE_NUM_NUMERIC_STEPS, (
        f"Rule 1.19 rule_num_numeric: {rule_1_19.rule_num_numeric}, expected {13 * RULE_NUM_NUMERIC_STEPS}"
    )


# def _initialize_on_move_across_rulebases(
#     global_state: GlobalState,
#     rule_order_service: RuleOrderService,
#     fwconfig_builder: FwConfigBuilder,
#     fwconfig_import_rule: FwConfigImportRule,
# ):
#     # Arrange
#     assert fwconfig_import_rule.normalized_config is not None
#     source_rulebase = fwconfig_import_rule.normalized_config.rulebases[0]
# source_rulebase_uids = list(source_rulebase.rules.keys())
# target_rulebase = fwconfig_import_rule.normalized_config.rulebases[1]
# target_rulebase_uids = list(target_rulebase.rules.keys())

# deleted_rule = remove_rule_from_rulebase(
#     self._normalized_config, source_rulebase.uid, source_rulebase_uids[0], source_rulebase_uids
# )
# insert_rule_in_config(
#     self._normalized_config, target_rulebase.uid, 0, target_rulebase_uids, self._config_builder, deleted_rule
# )

# # Act

# self._rule_order_service.update_rule_order_diffs()

# # Assert

# self.assertTrue(
#     self._get_rule(1, deleted_rule.rule_uid).rule_num_numeric == RULE_NUM_NUMERIC_STEPS / 2,
#     f"Moved rule_num_numeric is {self._normalized_config.rulebases[1].rules[deleted_rule.rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}",
# )

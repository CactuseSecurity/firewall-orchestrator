# pyright: reportPrivateUsage=false

import copy

from fwo_const import RULE_NUM_NUMERIC_STEPS
from model_controllers.fwconfig_import_ruleorder import update_rule_order_diffs
from states.management_state import ManagementState
from test.utils.config_builder import FwConfigBuilder
from test.utils.rule_helper_functions import (
    get_rule,
    insert_rule_in_config,
    move_rule_in_config,
    remove_rule_from_rulebase,
)


class TestFwConfigImportRuleOrderOldMigration:
    def test_initialize_on_insert_delete_and_move(
        self,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        previous_config = copy.deepcopy(normalized_config)
        fwconfig_builder.initialize_rule_num_numerics(previous_config)

        rulebase = normalized_config.rulebases[0]
        rule_uids = list(rulebase.rules.keys())
        removed_rule_uid = rule_uids[0]

        remove_rule_from_rulebase(normalized_config, rulebase.uid, removed_rule_uid, rule_uids)
        inserted_rule_uid = insert_rule_in_config(normalized_config, rulebase.uid, 0, rule_uids, fwconfig_builder)

        moved_rule_uid = move_rule_in_config(normalized_config, rulebase.uid, 9, 0, rule_uids)
        # Act
        update_rule_order_diffs(normalized_config=normalized_config, previous_config=previous_config)

        last_rule_uid = list(normalized_config.rulebases[0].rules.keys())[-1]

        # Assert
        assert inserted_rule_uid is not None
        assert moved_rule_uid is not None
        insert_rule = get_rule(normalized_config, 0, inserted_rule_uid)
        assert insert_rule is not None

        moved_rule = get_rule(normalized_config, 0, moved_rule_uid)
        assert moved_rule is not None

        assert moved_rule.rule_num_numeric == RULE_NUM_NUMERIC_STEPS, (
            f"Moved rule_num_numeric is {normalized_config.rulebases[0].rules[inserted_rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS}"
        )

        assert insert_rule.rule_num_numeric == 3 * RULE_NUM_NUMERIC_STEPS / 2, (
            f"Inserted rule_num_numeric is {normalized_config.rulebases[0].rules[moved_rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
        )

        assert last_rule_uid is not None
        last_rule = get_rule(normalized_config, 0, last_rule_uid)
        assert last_rule is not None
        expected_last_rule_num_numeric = 9 * RULE_NUM_NUMERIC_STEPS
        assert last_rule.rule_num_numeric == expected_last_rule_num_numeric, (
            f"Last rule_num_numeric is {normalized_config.rulebases[0].rules[last_rule_uid].rule_num_numeric}, expected {expected_last_rule_num_numeric}"
        )

    def test_initialize_on_consecutive_insertions(
        self,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )
        previous_config = copy.deepcopy(normalized_config)
        fwconfig_builder.initialize_rule_num_numerics(previous_config)

        assert normalized_config is not None
        rulebase = normalized_config.rulebases[0]
        rule_uids = list(rulebase.rules.keys())

        # Inserting three new rules at the beginning of the rulebase
        rule_1_1_uid = insert_rule_in_config(normalized_config, rulebase.uid, 0, rule_uids, fwconfig_builder)
        rule_1_2_uid = insert_rule_in_config(normalized_config, rulebase.uid, 1, rule_uids, fwconfig_builder)
        rule_1_3_uid = insert_rule_in_config(normalized_config, rulebase.uid, 2, rule_uids, fwconfig_builder)

        # Inserting three new rules in the middle of the rulebase
        rule_1_6_uid = insert_rule_in_config(normalized_config, rulebase.uid, 5, rule_uids, fwconfig_builder)
        rule_1_7_uid = insert_rule_in_config(normalized_config, rulebase.uid, 6, rule_uids, fwconfig_builder)
        rule_1_8_uid = insert_rule_in_config(normalized_config, rulebase.uid, 7, rule_uids, fwconfig_builder)

        # Inserting three new rules at the end of the rulebase
        rule_1_17_uid = insert_rule_in_config(normalized_config, rulebase.uid, 16, rule_uids, fwconfig_builder)
        rule_1_18_uid = insert_rule_in_config(normalized_config, rulebase.uid, 17, rule_uids, fwconfig_builder)
        rule_1_19_uid = insert_rule_in_config(normalized_config, rulebase.uid, 18, rule_uids, fwconfig_builder)

        # Act

        update_rule_order_diffs(normalized_config=normalized_config, previous_config=previous_config)

        # Assert
        assert rule_1_1_uid is not None
        rule_1_1 = get_rule(normalized_config, 0, rule_1_1_uid)
        assert rule_1_1 is not None
        assert rule_1_1.rule_num_numeric == RULE_NUM_NUMERIC_STEPS / 2, (
            f"Rule 1.1 rule_num_numeric: {rule_1_1.rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
        )
        assert rule_1_2_uid is not None
        rule_1_2 = get_rule(normalized_config, 0, rule_1_2_uid)
        assert rule_1_2 is not None
        assert rule_1_2.rule_num_numeric == 3 * RULE_NUM_NUMERIC_STEPS / 4, (
            f"Rule 1.2 rule_num_numeric: {rule_1_2.rule_num_numeric}, expected {3 * RULE_NUM_NUMERIC_STEPS / 4}"
        )

        assert rule_1_3_uid is not None
        rule_1_3 = get_rule(normalized_config, 0, rule_1_3_uid)
        assert rule_1_3 is not None
        assert rule_1_3.rule_num_numeric == 7 * RULE_NUM_NUMERIC_STEPS / 8, (
            f"Rule 1.3 rule_num_numeric: {rule_1_3.rule_num_numeric}, expected {7 * RULE_NUM_NUMERIC_STEPS / 8}"
        )

        assert rule_1_6_uid is not None
        rule_1_6 = get_rule(normalized_config, 0, rule_1_6_uid)
        assert rule_1_6 is not None
        assert rule_1_6.rule_num_numeric == 5 * RULE_NUM_NUMERIC_STEPS / 2, (
            f"Rule 1.6 rule_num_numeric: {rule_1_6.rule_num_numeric}, expected {5 * RULE_NUM_NUMERIC_STEPS / 2}"
        )
        assert rule_1_7_uid is not None
        rule_1_7 = get_rule(normalized_config, 0, rule_1_7_uid)
        assert rule_1_7 is not None
        assert rule_1_7.rule_num_numeric == 11 * RULE_NUM_NUMERIC_STEPS / 4, (
            f"Rule 1.7 rule_num_numeric: {rule_1_7.rule_num_numeric}, expected {11 * RULE_NUM_NUMERIC_STEPS / 4}"
        )
        assert rule_1_8_uid is not None
        rule_1_8 = get_rule(normalized_config, 0, rule_1_8_uid)
        assert rule_1_8 is not None
        assert rule_1_8.rule_num_numeric == 23 * RULE_NUM_NUMERIC_STEPS / 8, (
            f"Rule 1.8 rule_num_numeric: {rule_1_8.rule_num_numeric}, expected {23 * RULE_NUM_NUMERIC_STEPS / 8}"
        )

        assert rule_1_17_uid is not None
        rule_1_17 = get_rule(normalized_config, 0, rule_1_17_uid)
        assert rule_1_17 is not None
        assert rule_1_17.rule_num_numeric == 11 * RULE_NUM_NUMERIC_STEPS, (
            f"Rule 1.17 rule_num_numeric: {rule_1_17.rule_num_numeric}, expected {11 * RULE_NUM_NUMERIC_STEPS}"
        )
        assert rule_1_18_uid is not None
        rule_1_18 = get_rule(normalized_config, 0, rule_1_18_uid)
        assert rule_1_18 is not None
        assert rule_1_18.rule_num_numeric == 12 * RULE_NUM_NUMERIC_STEPS, (
            f"Rule 1.18 rule_num_numeric: {rule_1_18.rule_num_numeric}, expected {12 * RULE_NUM_NUMERIC_STEPS}"
        )
        assert rule_1_19_uid is not None
        rule_1_19 = get_rule(normalized_config, 0, rule_1_19_uid)
        assert rule_1_19 is not None
        assert rule_1_19.rule_num_numeric == 13 * RULE_NUM_NUMERIC_STEPS, (
            f"Rule 1.19 rule_num_numeric: {rule_1_19.rule_num_numeric}, expected {13 * RULE_NUM_NUMERIC_STEPS}"
        )

    def test_initialize_on_move_across_rulebases(
        self,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        previous_config = copy.deepcopy(normalized_config)
        fwconfig_builder.initialize_rule_num_numerics(previous_config)

        assert normalized_config is not None
        source_rulebase = normalized_config.rulebases[0]
        source_rulebase_uids = list(source_rulebase.rules.keys())
        target_rulebase = normalized_config.rulebases[1]
        target_rulebase_uids = list(target_rulebase.rules.keys())
        deleted_rule = remove_rule_from_rulebase(
            normalized_config, source_rulebase.uid, source_rulebase_uids[0], source_rulebase_uids
        )
        insert_rule_in_config(
            normalized_config,
            target_rulebase.uid,
            0,
            target_rulebase_uids,
            fwconfig_builder,
            deleted_rule,
        )
        # Act

        update_rule_order_diffs(normalized_config=normalized_config, previous_config=previous_config)  # Assert
        assert deleted_rule.rule_uid is not None
        rule = get_rule(normalized_config, 1, deleted_rule.rule_uid)
        assert rule is not None
        assert rule.rule_num_numeric == RULE_NUM_NUMERIC_STEPS / 2, (
            f"Moved rule_num_numeric is {normalized_config.rulebases[1].rules[deleted_rule.rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
        )

    def test_update_rulebase_diffs_on_moves_to_beginning_middle_and_end_of_rulebase(
        self,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        previous_config = copy.deepcopy(normalized_config)
        fwconfig_builder.initialize_rule_num_numerics(previous_config)

        rulebase = normalized_config.rulebases[0]
        rule_uids = list(rulebase.rules.keys())

        beginning_rule_uid = move_rule_in_config(normalized_config, rulebase.uid, 5, 0, rule_uids)  # Move to beginning
        middle_rule_uid = move_rule_in_config(normalized_config, rulebase.uid, 1, 4, rule_uids)  # Move to middle
        end_rule_uid = move_rule_in_config(normalized_config, rulebase.uid, 2, 9, rule_uids)  # Move to end

        # Act

        update_rule_order_diffs(normalized_config=normalized_config, previous_config=previous_config)

        # Assert
        assert beginning_rule_uid is not None
        beginning_rule = get_rule(normalized_config, 0, beginning_rule_uid)
        assert beginning_rule is not None
        assert beginning_rule.rule_num_numeric == RULE_NUM_NUMERIC_STEPS, (
            f"Beginning moved rule_num_numeric is {normalized_config.rulebases[0].rules[beginning_rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
        )

        assert middle_rule_uid is not None
        middle_rule = get_rule(normalized_config, 0, middle_rule_uid)
        assert middle_rule is not None
        assert middle_rule.rule_num_numeric == 4608, (
            f"Middle moved rule_num_numeric is {normalized_config.rulebases[0].rules[middle_rule_uid].rule_num_numeric}, expected 4608"
        )
        assert end_rule_uid is not None
        end_rule = get_rule(normalized_config, 0, end_rule_uid)
        assert end_rule is not None
        assert end_rule.rule_num_numeric == 11 * RULE_NUM_NUMERIC_STEPS, (
            f"End moved rule_num_numeric is {normalized_config.rulebases[0].rules[end_rule_uid].rule_num_numeric}, expected {11 * RULE_NUM_NUMERIC_STEPS}"
        )

    def test_update_rulebase_diffs_multiple_on_same_spot(
        self,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        previous_config = copy.deepcopy(normalized_config)
        fwconfig_builder.initialize_rule_num_numerics(previous_config)

        rulebase = normalized_config.rulebases[0]
        rule_uids = list(rulebase.rules.keys())
        # Move rule 5 to position 0, then move rule 6 (which is now at position 6) to position 0 as well and insert a new rule at position 0
        rule_1_uid = insert_rule_in_config(normalized_config, rulebase.uid, 5, rule_uids, fwconfig_builder)
        rule_2_uid = insert_rule_in_config(normalized_config, rulebase.uid, 5, rule_uids, fwconfig_builder)
        rule_3_uid = insert_rule_in_config(normalized_config, rulebase.uid, 5, rule_uids, fwconfig_builder)
        rule_4_uid = insert_rule_in_config(normalized_config, rulebase.uid, 5, rule_uids, fwconfig_builder)

        # Act

        update_rule_order_diffs(normalized_config=normalized_config, previous_config=previous_config)

        # Assert
        for i in range(5):
            rule = get_rule(normalized_config, 0, rule_uids[i])
            assert rule is not None
            expected_rule_num_numeric = (i + 1) * RULE_NUM_NUMERIC_STEPS
            assert rule.rule_num_numeric == expected_rule_num_numeric, (
                f"Rule at position {i} has rule_num_numeric {rule.rule_num_numeric}, expected {expected_rule_num_numeric}"
            )

        assert rule_uids[5] == rule_4_uid
        assert rule_4_uid is not None
        rule_4 = get_rule(normalized_config, 0, rule_4_uid)
        assert rule_4 is not None
        assert rule_4.rule_num_numeric == 11 * RULE_NUM_NUMERIC_STEPS / 2, (
            f"Rule 4 rule_num_numeric is {normalized_config.rulebases[0].rules[rule_4_uid].rule_num_numeric}, expected {11 * RULE_NUM_NUMERIC_STEPS / 2}"
        )

        assert rule_uids[6] == rule_3_uid
        assert rule_3_uid is not None
        rule_3 = get_rule(normalized_config, 0, rule_3_uid)
        assert rule_3 is not None
        assert rule_3.rule_num_numeric == 23 * RULE_NUM_NUMERIC_STEPS / 4, (
            f"Rule 3 rule_num_numeric is {normalized_config.rulebases[0].rules[rule_3_uid].rule_num_numeric}, expected {23 * RULE_NUM_NUMERIC_STEPS / 4}"
        )

        assert rule_uids[7] == rule_2_uid
        assert rule_2_uid is not None
        rule_2 = get_rule(normalized_config, 0, rule_2_uid)
        assert rule_2 is not None
        assert rule_2.rule_num_numeric == 47 * RULE_NUM_NUMERIC_STEPS / 8, (
            f"Rule 2 rule_num_numeric is {normalized_config.rulebases[0].rules[rule_2_uid].rule_num_numeric}, expected {47 * RULE_NUM_NUMERIC_STEPS / 8}"
        )

        assert rule_uids[8] == rule_1_uid
        assert rule_1_uid is not None
        rule_1 = get_rule(normalized_config, 0, rule_1_uid)
        assert rule_1 is not None
        assert rule_1.rule_num_numeric == 95 * RULE_NUM_NUMERIC_STEPS / 16, (
            f"Rule 1 rule_num_numeric is {normalized_config.rulebases[0].rules[rule_1_uid].rule_num_numeric}, expected {95 * RULE_NUM_NUMERIC_STEPS / 16}"
        )

    def test_update_rulebase_diffs_on_no_changes(
        self,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        previous_config = copy.deepcopy(normalized_config)
        fwconfig_builder.initialize_rule_num_numerics(previous_config)

        # Act

        update_rule_order_diffs(normalized_config=normalized_config, previous_config=previous_config)

        # Assert - rule_num_numeric values should remain unchanged
        for rulebase in normalized_config.rulebases:
            for rule_uid, rule in rulebase.rules.items():
                previous_rule = next(
                    (r for rb in previous_config.rulebases for r_uid, r in rb.rules.items() if r_uid == rule_uid), None
                )
                assert previous_rule is not None
                assert rule.rule_num_numeric == previous_rule.rule_num_numeric, (
                    f"Rule {rule_uid} has rule_num_numeric {rule.rule_num_numeric}, expected {previous_rule.rule_num_numeric}"
                )

    def test_update_rulebase_diffs_on_all_rules_moved(
        self,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        previous_config = copy.deepcopy(normalized_config)
        fwconfig_builder.initialize_rule_num_numerics(previous_config)

        rulebase = normalized_config.rulebases[0]
        rule_uids = list(rulebase.rules.keys())
        # Move all rules down by one position, last rule goes to position 0
        for i in range(len(rule_uids)):
            move_rule_in_config(normalized_config, rulebase.uid, i, (i + 1) % len(rule_uids), rule_uids)

        # Act

        update_rule_order_diffs(normalized_config=normalized_config, previous_config=previous_config)

        # Assert - all rules should have the same rule_num_numeric as before since the order is the same just rotated
        for rulebase in normalized_config.rulebases:
            for rule_uid, rule in rulebase.rules.items():
                previous_rule = next(
                    (r for rb in previous_config.rulebases for r_uid, r in rb.rules.items() if r_uid == rule_uid), None
                )
                assert previous_rule is not None
                assert rule.rule_num_numeric == previous_rule.rule_num_numeric, (
                    f"Rule {rule_uid} has rule_num_numeric {rule.rule_num_numeric}, expected {previous_rule.rule_num_numeric}"
                )

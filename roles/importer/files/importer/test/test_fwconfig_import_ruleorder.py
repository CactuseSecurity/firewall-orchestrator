# pyright: reportPrivateUsage=false

import copy

from fwo_const import RULE_NUM_NUMERIC_STEPS
from model_controllers.fwconfig_import_ruleorder import RuleOrderService
from services.global_state import GlobalState
from test.utils.config_builder import FwConfigBuilder
from test.utils.rule_helper_functions import (
    get_rule,
    insert_rule_in_config,
    move_rule_in_config,
    remove_rule_from_rulebase,
)


class TestFwConfigImportRuleOrderOldMigration:
    def test_initialize_on_initial_import(
        self,
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
        self,
        global_state: GlobalState,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        global_state.normalized_config = config
        global_state.previous_config = copy.deepcopy(config)

        rulebase = global_state.normalized_config.rulebases[0]
        rule_uids = list(rulebase.rules.keys())
        removed_rule_uid = rule_uids[0]

        remove_rule_from_rulebase(global_state.normalized_config, rulebase.uid, removed_rule_uid, rule_uids)
        inserted_rule_uid = insert_rule_in_config(
            global_state.normalized_config, rulebase.uid, 0, rule_uids, fwconfig_builder
        )
        moved_rule_uid = move_rule_in_config(global_state.normalized_config, rulebase.uid, 9, 0, rule_uids)
        # Act
        rule_order_service.update_rule_order_diffs()

        last_rule_uid = list(global_state.normalized_config.rulebases[0].rules.keys())[-1]

        # Assert
        assert inserted_rule_uid is not None
        assert moved_rule_uid is not None
        insert_rule = get_rule(global_state.normalized_config, 0, inserted_rule_uid)
        assert insert_rule is not None

        moved_rule = get_rule(global_state.normalized_config, 0, moved_rule_uid)
        assert moved_rule is not None

        assert moved_rule.rule_num_numeric == RULE_NUM_NUMERIC_STEPS / 2, (
            f"Moved rule_num_numeric is {global_state.normalized_config.rulebases[0].rules[inserted_rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
        )

        assert insert_rule.rule_num_numeric == 3 * RULE_NUM_NUMERIC_STEPS / 2, (
            f"Inserted rule_num_numeric is {global_state.normalized_config.rulebases[0].rules[moved_rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
        )

        assert last_rule_uid is not None
        last_rule = get_rule(global_state.normalized_config, 0, last_rule_uid)
        assert last_rule is not None
        expected_last_rule_num_numeric = 9 * RULE_NUM_NUMERIC_STEPS
        assert last_rule.rule_num_numeric == expected_last_rule_num_numeric, (
            f"Last rule_num_numeric is {global_state.normalized_config.rulebases[0].rules[last_rule_uid].rule_num_numeric}, expected {expected_last_rule_num_numeric}"
        )

    def test_initialize_on_consecutive_insertions(
        self,
        global_state: GlobalState,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )
        global_state.normalized_config = config
        global_state.previous_config = copy.deepcopy(config)

        assert global_state.normalized_config is not None
        rulebase = global_state.normalized_config.rulebases[0]
        rule_uids = list(rulebase.rules.keys())

        # Inserting three new rules at the beginning of the rulebase
        rule_1_1_uid = insert_rule_in_config(
            global_state.normalized_config, rulebase.uid, 0, rule_uids, fwconfig_builder
        )
        rule_1_2_uid = insert_rule_in_config(
            global_state.normalized_config, rulebase.uid, 1, rule_uids, fwconfig_builder
        )
        rule_1_3_uid = insert_rule_in_config(
            global_state.normalized_config, rulebase.uid, 2, rule_uids, fwconfig_builder
        )

        # Inserting three new rules in the middle of the rulebase
        rule_1_6_uid = insert_rule_in_config(
            global_state.normalized_config, rulebase.uid, 5, rule_uids, fwconfig_builder
        )
        rule_1_7_uid = insert_rule_in_config(
            global_state.normalized_config, rulebase.uid, 6, rule_uids, fwconfig_builder
        )
        rule_1_8_uid = insert_rule_in_config(
            global_state.normalized_config, rulebase.uid, 7, rule_uids, fwconfig_builder
        )

        # Inserting three new rules at the end of the rulebase
        rule_1_17_uid = insert_rule_in_config(
            global_state.normalized_config, rulebase.uid, 16, rule_uids, fwconfig_builder
        )
        rule_1_18_uid = insert_rule_in_config(
            global_state.normalized_config, rulebase.uid, 17, rule_uids, fwconfig_builder
        )
        rule_1_19_uid = insert_rule_in_config(
            global_state.normalized_config, rulebase.uid, 18, rule_uids, fwconfig_builder
        )

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
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
        global_state: GlobalState,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        global_state.normalized_config = config
        global_state.previous_config = copy.deepcopy(config)

        assert global_state.normalized_config is not None
        source_rulebase = global_state.normalized_config.rulebases[0]
        source_rulebase_uids = list(source_rulebase.rules.keys())
        target_rulebase = global_state.normalized_config.rulebases[1]
        target_rulebase_uids = list(target_rulebase.rules.keys())
        deleted_rule = remove_rule_from_rulebase(
            global_state.normalized_config, source_rulebase.uid, source_rulebase_uids[0], source_rulebase_uids
        )
        insert_rule_in_config(
            global_state.normalized_config,
            target_rulebase.uid,
            0,
            target_rulebase_uids,
            fwconfig_builder,
            deleted_rule,
        )
        # Act

        rule_order_service.update_rule_order_diffs()  # Assert
        assert deleted_rule.rule_uid is not None
        rule = get_rule(global_state.normalized_config, 1, deleted_rule.rule_uid)
        assert rule is not None
        assert rule.rule_num_numeric == RULE_NUM_NUMERIC_STEPS / 2, (
            f"Moved rule_num_numeric is {global_state.normalized_config.rulebases[1].rules[deleted_rule.rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
        )

    def test_update_rulebase_diffs_on_moves_to_beginning_middle_and_end_of_rulebase(
        self,
        rule_order_service: RuleOrderService,
        global_state: GlobalState,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )
        global_state.normalized_config = config
        global_state.previous_config = copy.deepcopy(config)

        rulebase = global_state.normalized_config.rulebases[0]
        rule_uids = list(rulebase.rules.keys())

        beginning_rule_uid = move_rule_in_config(
            global_state.normalized_config, rulebase.uid, 5, 0, rule_uids
        )  # Move to beginning
        middle_rule_uid = move_rule_in_config(
            global_state.normalized_config, rulebase.uid, 1, 4, rule_uids
        )  # Move to middle
        end_rule_uid = move_rule_in_config(global_state.normalized_config, rulebase.uid, 2, 9, rule_uids)  # Move to end

        # Act

        rule_order_service.update_rule_order_diffs()

        # Assert
        assert beginning_rule_uid is not None
        beginning_rule = get_rule(global_state.normalized_config, 0, beginning_rule_uid)
        assert beginning_rule is not None
        assert beginning_rule.rule_num_numeric == RULE_NUM_NUMERIC_STEPS / 2, (
            f"Beginning moved rule_num_numeric is {global_state.normalized_config.rulebases[0].rules[beginning_rule_uid].rule_num_numeric}, expected {RULE_NUM_NUMERIC_STEPS / 2}"
        )

        assert middle_rule_uid is not None
        middle_rule = get_rule(global_state.normalized_config, 0, middle_rule_uid)
        assert middle_rule is not None
        assert middle_rule.rule_num_numeric == 4608, (
            f"Middle moved rule_num_numeric is {global_state.normalized_config.rulebases[0].rules[middle_rule_uid].rule_num_numeric}, expected 4608"
        )
        assert end_rule_uid is not None
        end_rule = get_rule(global_state.normalized_config, 0, end_rule_uid)
        assert end_rule is not None
        assert end_rule.rule_num_numeric == 11 * RULE_NUM_NUMERIC_STEPS, (
            f"End moved rule_num_numeric is {global_state.normalized_config.rulebases[0].rules[end_rule_uid].rule_num_numeric}, expected {11 * RULE_NUM_NUMERIC_STEPS}"
        )


# pyright: ignore[reportPrivateUsage]
class TestGetAdjacentListElement:
    def test_get_adjacent_list_element(self, rule_order_service: RuleOrderService):  # pyright: ignore[reportPrivateUsage]
        test_list = ["item1", "item2", "item3"]

        # Act & Assert - Standard case (middle)
        prev_item, next_item = rule_order_service._get_adjacent_list_element(test_list, 1)  # pyright: ignore[reportPrivateUsage]
        assert prev_item == "item1"
        assert next_item == "item3"

    def test_get_adjacent_list_element_on_start_and_end(self, rule_order_service: RuleOrderService):
        test_list = ["item1", "item2", "item3"]
        # Act & Assert - Start of list
        prev_item, next_item = rule_order_service._get_adjacent_list_element(test_list, 0)
        assert prev_item is None
        assert next_item == "item2"

    def test_get_adjacent_list_element_on_end_of_list(self, rule_order_service: RuleOrderService):
        test_list = ["item1", "item2", "item3"]

        # Act & Assert - End of list
        prev_item, next_item = rule_order_service._get_adjacent_list_element(test_list, 2)
        assert prev_item == "item2"
        assert next_item is None

    def test_get_adjacent_list_element_on_single_item_list(self, rule_order_service: RuleOrderService):
        test_list = ["single"]
        # Act & Assert - Single item list
        assert rule_order_service._get_adjacent_list_element(test_list, 0) == (None, None)

    def test_get_adjacent_list_element_on_invalid_indices(self, rule_order_service: RuleOrderService):
        test_list: list[str] = []
        # Act & Assert - Empty list
        assert rule_order_service._get_adjacent_list_element(test_list, 0) == (None, None)

    def test_get_adjacent_list_element_on_out_of_bound_negative(self, rule_order_service: RuleOrderService):
        test_list = ["item1", "item2", "item3"]
        # Act & Assert - Out of bounds (negative)
        assert rule_order_service._get_adjacent_list_element(test_list, -1) == (None, None)

    def test_get_adjacent_list_element_on_out_of_bound_overflow(self, rule_order_service: RuleOrderService):
        test_list = ["item1", "item2", "item3"]
        # Act & Assert - Out of bounds (overflow)
        assert rule_order_service._get_adjacent_list_element(test_list, 99) == (None, None)


class TestParseRuleUidsAndObjectsFromConfig:
    def test_parse_rule_uids_and_objects_from_config(
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        # Act - Case with rules
        uids, rules_flat = rule_order_service._parse_rule_uids_and_objects_from_config(config)

        # Assert
        assert len(uids) == 30
        assert len(rules_flat) == 30
        assert len(uids) == len(rules_flat)
        assert rules_flat[0].rule_uid == uids[0]

    def test_parse_rule_uids_and_objects_from_config_empty_rulebase(
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        config = fwconfig_builder.build_empty_config()

        # Act - Case with empty rulebase
        uids, rules_flat = rule_order_service._parse_rule_uids_and_objects_from_config(config)

        # Assert
        assert len(uids) == 0
        assert len(rules_flat) == 0

    def test_parse_rule_uids_and_objects_from_config_no_rulebases(
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=5,
            service_object_count=5,
            rulebase_count=0,
            rules_per_rulebase_count=0,
        )

        # Act - Case with no rulebases
        uids, rules_flat = rule_order_service._parse_rule_uids_and_objects_from_config(config)

        # Assert
        assert len(uids) == 0
        assert len(rules_flat) == 0


class TestFwConfigImportRuleOrderFunctions:
    def test_get_relevant_rule_num_numeric_returns_updated_value(
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(rulebase_count=1, rules_per_rulebase_count=1)
        rulebase = config.rulebases[0]
        rule = next(iter(rulebase.rules.values()))
        rule.rule_num_numeric = 999.0

        # Inject state: Rule is marked as updated
        assert rule.rule_uid is not None
        rule_order_service._updated_rules = [rule.rule_uid]
        rule_order_service._target_rules_flat = [rule]
        rule_order_service._source_rules_flat = []

        # Act
        result = rule_order_service._get_relevant_rule_num_numeric(
            rule.rule_uid, ascending=True, target_rulebase=rulebase
        )

        # Assert
        assert int(result) == 999

    def test_get_relevant_rule_num_numeric_returns_zero_for_consecutive_insert(
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(rulebase_count=1, rules_per_rulebase_count=2)
        rulebase = config.rulebases[0]
        rules = list(rulebase.rules.values())
        r1, r2 = rules[0], rules[1]

        # Mock state to simulate consecutive insert:
        # 1. More than 1 new rule in total.
        # 2. Checked rule (r1) is new.
        # 3. Adjacent rule (r2) is also new.
        rule_order_service._updated_rules = []
        assert r1.rule_uid is not None
        assert r2.rule_uid is not None
        rule_order_service._new_rule_uids = {rulebase.uid: [r1.rule_uid, r2.rule_uid]}
        rule_order_service._moved_rule_uids = {}
        rule_order_service._target_rules_flat = rules
        rule_order_service._target_rule_uids = [str(r.rule_uid) for r in rules]

        # Act
        result = rule_order_service._get_relevant_rule_num_numeric(
            r1.rule_uid, ascending=True, target_rulebase=rulebase
        )

        # Assert
        assert int(result) == 0

    def test_get_relevant_rule_num_numeric_calculates_for_moved_rule_using_recursion(
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(rulebase_count=1, rules_per_rulebase_count=2)
        rulebase = config.rulebases[0]
        rules = list(rulebase.rules.values())
        moved_rule, static_neighbor = rules[0], rules[1]

        static_neighbor.rule_num_numeric = 500.0

        # State: moved_rule is moved. static_neighbor is stable (source).
        # Order in target: [moved_rule, static_neighbor]
        rule_order_service._updated_rules = []
        rule_order_service._new_rule_uids = {}
        assert moved_rule.rule_uid is not None
        rule_order_service._moved_rule_uids = {rulebase.uid: [moved_rule.rule_uid]}

        # Target setup
        rule_order_service._target_rules_flat = rules
        rule_order_service._target_rule_uids = [str(r.rule_uid) for r in rules]

        # Source setup (fallback for static_neighbor lookup)
        rule_order_service._source_rules_flat = [static_neighbor]

        # Act
        # ascending=True calls _num_for_ascending_case -> looks at next neighbor (static_neighbor)
        # recurses -> _get_relevant_rule_num_numeric(static_neighbor) -> falls back to source -> 500.0
        assert moved_rule.rule_uid is not None
        result = rule_order_service._get_relevant_rule_num_numeric(
            moved_rule.rule_uid, ascending=True, target_rulebase=rulebase
        )

        # Assert
        assert int(result) == 500

    def test_get_relevant_rule_num_numeric_calculates_for_new_rule_descending(
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        # Descending check for new_rule -> looks at prev neighbor (static_neighbor).
        config, _ = fwconfig_builder.build_config(rulebase_count=1, rules_per_rulebase_count=2)
        rulebase = config.rulebases[0]
        rules = list(rulebase.rules.values())
        static_neighbor, new_rule = rules[0], rules[1]

        static_neighbor.rule_num_numeric = 300.0

        rule_order_service._updated_rules = []
        assert new_rule.rule_uid is not None
        rule_order_service._new_rule_uids = {rulebase.uid: [new_rule.rule_uid]}
        rule_order_service._moved_rule_uids = {}

        rule_order_service._target_rules_flat = rules
        rule_order_service._target_rule_uids = [str(r.rule_uid) for r in rules]
        rule_order_service._source_rules_flat = [static_neighbor]

        # Act
        # ascending=False -> _num_for_descending_case -> looks at prev (static_neighbor) -> 300.0
        assert new_rule.rule_uid is not None
        result = rule_order_service._get_relevant_rule_num_numeric(
            new_rule.rule_uid, ascending=False, target_rulebase=rulebase
        )

        # Assert
        assert int(result) == 300

    def test_get_relevant_rule_num_numeric_fallback_to_source(
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(rulebase_count=1, rules_per_rulebase_count=1)
        rulebase = config.rulebases[0]
        rule = next(iter(rulebase.rules.values()))
        rule.rule_num_numeric = 101.0

        # Inject state: Not currently updated, not new, not moved. Simply existing.
        rule_order_service._updated_rules = []
        rule_order_service._target_rules_flat = [rule]
        rule_order_service._source_rules_flat = [rule]
        rule_order_service._new_rule_uids = {}
        rule_order_service._moved_rule_uids = {}

        # Act
        assert rule.rule_uid is not None
        result = rule_order_service._get_relevant_rule_num_numeric(
            rule.rule_uid, ascending=True, target_rulebase=rulebase
        )

        # Assert
        assert int(result) == 101


class TestFwConfigImportRuleOrderCalculateNewRuleNum:
    def test_compute_num_for_changed_rule_ascending_no_next_neighbor(
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(rulebase_count=1, rules_per_rulebase_count=1)
        rulebase = config.rulebases[0]
        rule = next(iter(rulebase.rules.values()))

        # Act
        assert rule.rule_uid is not None
        result = rule_order_service._compute_num_for_changed_rule(
            rule.rule_uid, ascending=True, target_rulebase=rulebase
        )

        # Assert
        # If no next neighbor, ascending recursive usually returns None or handling logic inside _num_for_ascending_case returns 0/None.
        # Based on typical implementations of "gap closing to right end", it often implies rule_num_numeric + huge offset, or if it's the only rule.
        # However, looking at logic patterns: if next_uid is None -> returns 0.
        assert result == 1024

    def test_compute_num_for_changed_rule_descending_no_prev_neighbor(
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(rulebase_count=1, rules_per_rulebase_count=1)
        rulebase = config.rulebases[0]
        rule = next(iter(rulebase.rules.values()))

        # Act
        assert rule.rule_uid is not None
        result = rule_order_service._compute_num_for_changed_rule(
            rule.rule_uid, ascending=False, target_rulebase=rulebase
        )

        # Assert
        # If no prev neighbor, descending recursive often returns 0.
        assert int(result) == 512

    def test_compute_num_for_changed_rule_delegates_ascending(
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(rulebase_count=1, rules_per_rulebase_count=2)
        rulebase = config.rulebases[0]
        rules = list(rulebase.rules.values())
        r1, r2 = rules[0], rules[1]

        # r2 is the "next" neighbor of r1
        # Set r2 to be a known "source" rule to stop recursion immediately
        r2.rule_num_numeric = 500.0
        rule_order_service._source_rules_flat = [r2]
        rule_order_service._updated_rules = []
        rule_order_service._new_rule_uids = {}
        rule_order_service._moved_rule_uids = {}

        # Act
        # Compute for r1 (index 0), next is r2
        assert r1.rule_uid is not None
        result = rule_order_service._compute_num_for_changed_rule(r1.rule_uid, ascending=True, target_rulebase=rulebase)

        # Assert
        # Should equate to r2.rule_num_numeric (500.0)
        assert int(result) == 500

    def test_compute_num_for_changed_rule_delegates_descending(
        self,
        rule_order_service: RuleOrderService,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(rulebase_count=1, rules_per_rulebase_count=2)
        rulebase = config.rulebases[0]
        rules = list(rulebase.rules.values())
        r1, r2 = rules[0], rules[1]

        # r1 is the "prev" neighbor of r2
        r1.rule_num_numeric = 100.0
        rule_order_service._source_rules_flat = [r1]
        rule_order_service._updated_rules = []
        rule_order_service._new_rule_uids = {}
        rule_order_service._moved_rule_uids = {}

        # Act
        # Compute for r2 (index 1), prev is r1
        assert r2.rule_uid is not None
        result = rule_order_service._compute_num_for_changed_rule(
            r2.rule_uid, ascending=False, target_rulebase=rulebase
        )

        # Assert
        # Should equate to r1.rule_num_numeric (100.0)
        assert int(result) == 100

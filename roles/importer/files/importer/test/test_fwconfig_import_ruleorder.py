import copy
import unittest

from models.rule import RuleNormalized
from model_controllers.fwconfig_import_ruleorder import RuleOrderService
from test.mocking.mock_fwconfig_import_rule import MockFwConfigImportRule
from test.mocking.mock_config import MockFwConfigNormalizedBuilder
from test.tools.set_up_test import remove_rule_from_rulebase, insert_rule_in_config, move_rule_in_config, update_rule_map_and_rulebase_map, update_rule_num_numerics
from fwo_const import rule_num_numeric_steps
import fwo_local_settings
from fwo_base import init_service_provider

from services.service_provider import ServiceProvider
from services.enums import Services


class TestFwConfigImportRuleOrder(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        """
            Gets invoked one time before running any tests.
        """

        init_service_provider()
        cls._service_provider = ServiceProvider()
        cls._global_state = cls._service_provider.get_service(Services.GLOBAL_STATE)
        cls._debug_level: int = 1


    def setUp(self):
        """
            Gets invoked one time per test method before running it.
        """

        self._config_builder = MockFwConfigNormalizedBuilder()
        self._config_builder.set_up()
        self._previous_config, self._mgm_uid = self._config_builder.build_config(
            {
                "rule_config": [10,10,10],
                "network_object_config": 10,
                "service_config": 10,
                "user_config": 10
            }
        )
        self._global_state.normalized_config = copy.deepcopy(self._previous_config)
        self._global_state.previous_config = self._previous_config
        self._fwconfig_import_rule = MockFwConfigImportRule()
        self._fwconfig_import_rule.normalized_config = self._global_state.normalized_config
        self._import_state = self._fwconfig_import_rule.import_details
        self._normalized_config = self._fwconfig_import_rule.normalized_config
        
        self._global_state.import_details = self._import_state
        self._rule_order_service = self._service_provider.get_service(Services.RULE_ORDER_SERVICE, 1)

        update_rule_num_numerics(self._previous_config)
        update_rule_map_and_rulebase_map(self._previous_config, self._import_state)


    def tearDown(self):
        """
            Gets invoked one time per test method after running it.
        """

        if fwo_local_settings.python_unit_tests_verbose:

            outcome = self._outcome

            if len(outcome.result.failures) > 0:
                self._print_rule_num_numerics()


    def _print_rule_num_numerics(self):
        print("\nPrevious Config Rule Num Numerics:")
        for rb in self._previous_config.rulebases:
            for rule in rb.Rules.values():
                print(f"Rule UID: {rule.rule_uid}, rule_num_numeric: {rule.rule_num_numeric}")
        print("\nNormalized Config Rule Num Numerics:")
        for rb in self._normalized_config.rulebases:
            for rule in rb.Rules.values():
                print(f"Rule UID: {rule.rule_uid}, rule_num_numeric: {rule.rule_num_numeric}")


    def test_initialize_on_initial_import(self):
            
            # Arrange

            self._previous_config = self._config_builder.empty_config()
            self._global_state.previous_config = self._previous_config

            # Act

            self._rule_order_service.update_rule_order_diffs(self._debug_level)

            # Assert
            for rulebase in self._normalized_config.rulebases:
                for index, rule_uid in enumerate(rulebase.Rules):
                    expected_rule_num_numeric = (index + 1) * rule_num_numeric_steps
                    actual_rule_num_numeric = rulebase.Rules[rule_uid].rule_num_numeric
                    self.assertTrue(actual_rule_num_numeric == expected_rule_num_numeric, f"Rule UID: {rule_uid}, actual rule_num_numeric: {actual_rule_num_numeric}, expected: {expected_rule_num_numeric}")


    def test_initialize_on_insert_delete_and_move(self):
        
        # Arrange

        rulebase = self._normalized_config.rulebases[0]
        rule_uids = list(rulebase.Rules.keys())
        removed_rule_uid = rule_uids[0]

        remove_rule_from_rulebase(self._normalized_config, rulebase.uid, removed_rule_uid, rule_uids)
        inserted_rule_uid = insert_rule_in_config(self._normalized_config, rulebase.uid, 0, rule_uids, self._config_builder)
        moved_rule_uid = move_rule_in_config(self._normalized_config, rulebase.uid, 9, 0, rule_uids)

        # Act

        self._rule_order_service.update_rule_order_diffs(self._debug_level)

        # # Assert

        self.assertTrue(self._get_rule(0, inserted_rule_uid).rule_num_numeric == rule_num_numeric_steps, f"Inserted rule_num_numeric is {self._normalized_config.rulebases[0].Rules[inserted_rule_uid].rule_num_numeric}, expected {rule_num_numeric_steps}")
        self.assertTrue(self._get_rule(0, moved_rule_uid).rule_num_numeric == rule_num_numeric_steps / 2, f"Moved rule_num_numeric is {self._normalized_config.rulebases[0].Rules[moved_rule_uid].rule_num_numeric}, expected {rule_num_numeric_steps / 2}")


    def test_initialize_on_consecutive_insertions(self):
        
        # Arrange

        rulebase = self._normalized_config.rulebases[0]
        rule_uids = list(rulebase.Rules.keys())

        # Inserting three new rules at the beginning of the rulebase
        rule_1_1_uid = insert_rule_in_config(self._normalized_config, rulebase.uid, 0, rule_uids, self._config_builder)
        rule_1_2_uid = insert_rule_in_config(self._normalized_config, rulebase.uid, 1, rule_uids, self._config_builder)
        rule_1_3_uid = insert_rule_in_config(self._normalized_config, rulebase.uid, 2, rule_uids, self._config_builder)

        # Inserting three new rules in the middle of the rulebase
        rule_1_6_uid = insert_rule_in_config(self._normalized_config, rulebase.uid, 5, rule_uids, self._config_builder)
        rule_1_7_uid = insert_rule_in_config(self._normalized_config, rulebase.uid, 6, rule_uids, self._config_builder)
        rule_1_8_uid = insert_rule_in_config(self._normalized_config, rulebase.uid, 7, rule_uids, self._config_builder)

        # Inserting three new rules at the end of the rulebase
        rule_1_17_uid = insert_rule_in_config(self._normalized_config, rulebase.uid, 16, rule_uids, self._config_builder)
        rule_1_18_uid = insert_rule_in_config(self._normalized_config, rulebase.uid, 17, rule_uids, self._config_builder)
        rule_1_19_uid = insert_rule_in_config(self._normalized_config, rulebase.uid, 18, rule_uids, self._config_builder)

        # Act

        self._rule_order_service.update_rule_order_diffs(self._debug_level)

        # Assert

        self.assertTrue(self._get_rule(0, rule_1_1_uid).rule_num_numeric == rule_num_numeric_steps / 2, f"Rule 1.1 rule_num_numeric: {self._get_rule(0, rule_1_1_uid).rule_num_numeric}, expected {rule_num_numeric_steps / 2}")
        self.assertTrue(self._get_rule(0, rule_1_2_uid).rule_num_numeric == 3 * rule_num_numeric_steps / 4, f"Rule 1.2 rule_num_numeric: {self._get_rule(0, rule_1_2_uid).rule_num_numeric}, expected {3 * rule_num_numeric_steps / 4}")
        self.assertTrue(self._get_rule(0, rule_1_3_uid).rule_num_numeric == 7 * rule_num_numeric_steps / 8, f"Rule 1.3 rule_num_numeric: {self._get_rule(0, rule_1_3_uid).rule_num_numeric}, expected {7 * rule_num_numeric_steps / 8}")

        self.assertTrue(self._get_rule(0, rule_1_6_uid).rule_num_numeric == 5 * rule_num_numeric_steps / 2, f"Rule 1.6 rule_num_numeric: {self._get_rule(0, rule_1_6_uid).rule_num_numeric}, expected {5 * rule_num_numeric_steps / 2}")
        self.assertTrue(self._get_rule(0, rule_1_7_uid).rule_num_numeric == 11 * rule_num_numeric_steps / 4, f"Rule 1.7 rule_num_numeric: {self._get_rule(0, rule_1_7_uid).rule_num_numeric}, expected {11 * rule_num_numeric_steps / 4}")
        self.assertTrue(self._get_rule(0, rule_1_8_uid).rule_num_numeric == 23 * rule_num_numeric_steps / 8, f"Rule 1.8 rule_num_numeric: {self._get_rule(0, rule_1_8_uid).rule_num_numeric}, expected {23 * rule_num_numeric_steps / 8}")

        self.assertTrue(self._get_rule(0, rule_1_17_uid).rule_num_numeric == 11 * rule_num_numeric_steps, f"Rule 1.17 rule_num_numeric: {self._get_rule(0, rule_1_17_uid).rule_num_numeric}, expected {11 * rule_num_numeric_steps}")
        self.assertTrue(self._get_rule(0, rule_1_18_uid).rule_num_numeric == 12 * rule_num_numeric_steps, f"Rule 1.18 rule_num_numeric: {self._get_rule(0, rule_1_18_uid).rule_num_numeric}, expected {12 * rule_num_numeric_steps}")
        self.assertTrue(self._get_rule(0, rule_1_19_uid).rule_num_numeric == 13 * rule_num_numeric_steps, f"Rule 1.19 rule_num_numeric: {self._get_rule(0, rule_1_19_uid).rule_num_numeric}, expected {13 * rule_num_numeric_steps}")

   
    def test_initialize_on_move_across_rulebases(self):

        # Arrange

        source_rulebase = self._fwconfig_import_rule.normalized_config.rulebases[0]
        source_rulebase_uids = list(source_rulebase.Rules.keys())
        target_rulebase = self._fwconfig_import_rule.normalized_config.rulebases[1]
        target_rulebase_uids = list(target_rulebase.Rules.keys())

        deleted_rule = remove_rule_from_rulebase(self._normalized_config, source_rulebase.uid, source_rulebase_uids[0], source_rulebase_uids)
        insert_rule_in_config(self._normalized_config, target_rulebase.uid, 0, target_rulebase_uids, self._config_builder, deleted_rule)

        # Act

        self._rule_order_service.update_rule_order_diffs(self._debug_level)

        # Assert

        self.assertTrue(self._get_rule(1, deleted_rule.rule_uid).rule_num_numeric == rule_num_numeric_steps / 2, f"Moved rule_num_numeric is {self._normalized_config.rulebases[1].Rules[deleted_rule.rule_uid].rule_num_numeric}, expected {rule_num_numeric_steps / 2}")


    def test_update_rulebase_diffs_on_moves_to_beginning_middle_and_end_of_rulebase(self):

        # Arrange

        rulebase = self._normalized_config.rulebases[0]
        rule_uids = list(rulebase.Rules.keys())

        beginning_rule_uid = move_rule_in_config(self._normalized_config, rulebase.uid, 5, 0, rule_uids)  # Move to beginning
        middle_rule_uid = move_rule_in_config(self._normalized_config, rulebase.uid, 1, 4, rule_uids)  # Move to middle
        end_rule_uid = move_rule_in_config(self._normalized_config, rulebase.uid, 2, 9, rule_uids)  # Move to end

        # Act

        self._rule_order_service.update_rule_order_diffs(self._debug_level)

        # Assert

        self.assertTrue(self._get_rule(0, beginning_rule_uid).rule_num_numeric == rule_num_numeric_steps / 2, f"Beginning moved rule_num_numeric is {self._normalized_config.rulebases[0].Rules[beginning_rule_uid].rule_num_numeric}, expected {rule_num_numeric_steps / 2}")
        self.assertTrue(self._get_rule(0, middle_rule_uid).rule_num_numeric == 4608, f"Middle moved rule_num_numeric is {self._normalized_config.rulebases[0].Rules[middle_rule_uid].rule_num_numeric}, expected 4608")
        self.assertTrue(self._get_rule(0, end_rule_uid).rule_num_numeric == 11 * rule_num_numeric_steps, f"End moved rule_num_numeric is {self._normalized_config.rulebases[0].Rules[end_rule_uid].rule_num_numeric}, expected {11 * rule_num_numeric_steps}")


    def _get_rule(self, rulebase_index: int, rule_uid: str) -> RuleNormalized:
        """
            Helper method to get a rule from the normalized config.
        """

        rulebase = self._normalized_config.rulebases[rulebase_index]
        rule = rulebase.Rules.get(rule_uid, None)

        return rule
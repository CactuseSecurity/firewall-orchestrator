import copy
import unittest

from models.fwconfig_normalized import FwConfigNormalized

from test.mocking.mock_import_state import MockImportStateController
from test.mocking.mock_config import MockFwConfigNormalizedBuilder
from test.mocking.mock_fwconfig_import_rule import MockFwConfigImportRule
from test.tools.set_up_test import remove_rule_from_rulebase, insert_rule_in_config, move_rule_in_config, update_rule_map_and_rulebase_map, update_rule_num_numerics
from fwo_base import init_service_provider
from services.service_provider import ServiceProvider
from services.enums import Services

class TestUpdateRulebaseDiffs(unittest.TestCase):

    _config_builder: MockFwConfigNormalizedBuilder
    _fwconfig_import_rule: MockFwConfigImportRule
    _mgm_uid: str
    _previous_config: FwConfigNormalized
    _normalized_config: FwConfigNormalized
    _import_state: MockImportStateController
    _import_id: int
    _debug_level: int


    @classmethod
    def setUpClass(cls):
        """
            Gets invoked once before running any test of this class.
        """
        
        cls._config_builder = MockFwConfigNormalizedBuilder()
        init_service_provider()
        cls._service_provider = ServiceProvider()
        cls._global_state = cls._service_provider.get_service(Services.GLOBAL_STATE)
        cls._import_id = 0
        cls._debug_level = 1


    def setUp(self):
        """
            Gets invoked one time per test method before running it.
        """

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
        self._import_id += 1
        self._fwconfig_import_rule.import_details.ImportId = self._import_id
        self._fwconfig_import_rule.normalized_config = self._global_state.normalized_config
        self._import_state = self._fwconfig_import_rule.import_details
        self._normalized_config = self._fwconfig_import_rule.normalized_config

        self._global_state.import_details = self._import_state
        
        update_rule_num_numerics(self._previous_config)
        update_rule_map_and_rulebase_map(self._previous_config, self._import_state)
        
      
    def test_update_rulebase_diffs_on_insert_delete_and_move(self):
        
        # Arrange

        rulebase = self._normalized_config.rulebases[0]
        rule_uids = list(rulebase.Rules.keys())
        rule_uid = rule_uids[0]

        remove_rule_from_rulebase(self._normalized_config, rulebase.uid, rule_uid, rule_uids)
        insert_rule_in_config(self._normalized_config, rulebase.uid, 0, rule_uids, self._config_builder)
        move_rule_in_config(self._normalized_config, rulebase.uid, 9, 0, rule_uids)
        
        # Act

        self._fwconfig_import_rule.updateRulebaseDiffs(self._previous_config)

        # Assert

        # The order of the entries in normalized_config
        self.assertEqual(rule_uids, list(rulebase.Rules.keys())) 

        sorted_rulebase_rules = sorted(list(rulebase.Rules.values()), key=lambda r: r.rule_num_numeric)
        sorted_rulebase_rules_uids = [r.rule_uid for r in sorted_rulebase_rules]
        
        # The sequence of the rule_num_numeric values
        self.assertEqual(rule_uids, sorted_rulebase_rules_uids) 

        # Insert, delete and move recognized in ImportDetails
        self.assertEqual(self._import_state.Stats.RuleAddCount, 1)
        self.assertEqual(self._import_state.Stats.RuleDeleteCount, 1)
        self.assertEqual(self._import_state.Stats.RuleChangeCount, 1)
        self.assertEqual(self._import_state.Stats.RuleMoveCount, 1)


    def test_update_rulebase_diffs_on_consecutive_insertions(self):
        
        # Arrange

        rulebase = self._normalized_config.rulebases[0]
        rule_uids = list(rulebase.Rules.keys())

        # Inserting three new rules at the beginning of the rulebase
        insert_rule_in_config(self._normalized_config, rulebase.uid, 0, rule_uids, self._config_builder)
        insert_rule_in_config(self._normalized_config, rulebase.uid, 0, rule_uids, self._config_builder)
        insert_rule_in_config(self._normalized_config, rulebase.uid, 0, rule_uids, self._config_builder)

        # Inserting three new rules in the middle of the rulebase
        insert_rule_in_config(self._normalized_config, rulebase.uid, (len(rule_uids) - 3)//2, rule_uids, self._config_builder)
        insert_rule_in_config(self._normalized_config, rulebase.uid, (len(rule_uids) - 3)//2, rule_uids, self._config_builder)
        insert_rule_in_config(self._normalized_config, rulebase.uid, (len(rule_uids) - 3)//2, rule_uids, self._config_builder)

        # Inserting three new rules at the end of the rulebase
        insert_rule_in_config(self._normalized_config, rulebase.uid, len(rule_uids), rule_uids, self._config_builder)
        insert_rule_in_config(self._normalized_config, rulebase.uid, len(rule_uids), rule_uids, self._config_builder)
        insert_rule_in_config(self._normalized_config, rulebase.uid, len(rule_uids), rule_uids, self._config_builder)

        # Act

        self._fwconfig_import_rule.updateRulebaseDiffs(self._previous_config)

        # Assert

        # The order of the entries in normalized_config
        self.assertEqual(rule_uids, list(rulebase.Rules.keys())) 

        sorted_rulebase_rules = sorted(list(rulebase.Rules.values()), key=lambda r: r.rule_num_numeric)
        sorted_rulebase_rules_uids = [r.rule_uid for r in sorted_rulebase_rules]
        
        # The sequence of the rule_num_numeric values
        self.assertEqual(rule_uids, sorted_rulebase_rules_uids) 

        # Insertions recognized in ImportDetails
        self.assertEqual(self._import_state.Stats.RuleAddCount, 9)
        self.assertEqual(self._import_state.Stats.RuleDeleteCount, 0)
        self.assertEqual(self._import_state.Stats.RuleChangeCount, 0)
        self.assertEqual(self._import_state.Stats.RuleMoveCount, 0)


    def test_update_rulebase_diffs_on_move_across_rulebases(self):

        # Arrange

        source_rulebase = self._fwconfig_import_rule.normalized_config.rulebases[0]
        source_rulebase_uids = list(source_rulebase.Rules.keys())
        target_rulebase = self._fwconfig_import_rule.normalized_config.rulebases[1]
        target_rulebase_uids = list(target_rulebase.Rules.keys())

        deleted_rule = remove_rule_from_rulebase(self._normalized_config, source_rulebase.uid, source_rulebase_uids[0], source_rulebase_uids)
        insert_rule_in_config(self._normalized_config, target_rulebase.uid, 0, target_rulebase_uids, self._config_builder, deleted_rule)

        # Act

        self._fwconfig_import_rule.updateRulebaseDiffs(self._previous_config)

        # Assert

        # The order of the entries in normalized_config
        self.assertEqual(source_rulebase_uids, list(source_rulebase.Rules.keys())) 
        self.assertEqual(target_rulebase_uids, list(target_rulebase.Rules.keys())) 

        sorted_source_rulebase_rules = sorted(list(source_rulebase.Rules.values()), key=lambda r: r.rule_num_numeric)
        sorted_source_rulebase_rules_uids = [r.rule_uid for r in sorted_source_rulebase_rules]
        
        sorted_target_rulebase_rules = sorted(list(target_rulebase.Rules.values()), key=lambda r: r.rule_num_numeric)
        sorted_target_rulebase_rules_uids = [r.rule_uid for r in sorted_target_rulebase_rules]

        # The sequence of the rule_num_numeric values
        self.assertEqual(source_rulebase_uids, sorted_source_rulebase_rules_uids) 
        self.assertEqual(target_rulebase_uids, sorted_target_rulebase_rules_uids) 

        # Move across rulebases recognized in ImportDetails
        self.assertEqual(self._import_state.Stats.RuleAddCount, 0)
        self.assertEqual(self._import_state.Stats.RuleDeleteCount, 0)
        self.assertEqual(self._import_state.Stats.RuleChangeCount, 1)
        self.assertEqual(self._import_state.Stats.RuleMoveCount, 1)


    def test_update_rulebase_diffs_on_moves_to_beginning_middle_and_end_of_rulebase(self):

        # Arrange

        rulebase = self._normalized_config.rulebases[0]
        rule_uids = list(rulebase.Rules.keys())

        move_rule_in_config(self._normalized_config, rulebase.uid, (len(rule_uids) - 1)//2, 0, rule_uids)  # Move to beginning
        move_rule_in_config(self._normalized_config, rulebase.uid, 1, (len(rule_uids) - 1)//2, rule_uids)  # Move to middle
        move_rule_in_config(self._normalized_config, rulebase.uid, 2, len(rule_uids) - 1, rule_uids)  # Move to end

        # Act

        self._fwconfig_import_rule.updateRulebaseDiffs(self._previous_config)

        # Assert

        # The order of the entries in normalized_config
        self.assertEqual(rule_uids, list(rulebase.Rules.keys())) 

        sorted_rulebase_rules = sorted(list(rulebase.Rules.values()), key=lambda r: r.rule_num_numeric)
        sorted_rulebase_rules_uids = [r.rule_uid for r in sorted_rulebase_rules]
        
        # The sequence of the rule_num_numeric values
        self.assertEqual(rule_uids, sorted_rulebase_rules_uids) 

        # Move to beginning, middle and end recognized in ImportDetails
        self.assertEqual(self._import_state.Stats.RuleAddCount, 0)
        self.assertEqual(self._import_state.Stats.RuleDeleteCount, 0)
        self.assertEqual(self._import_state.Stats.RuleChangeCount, 3)
        self.assertEqual(self._import_state.Stats.RuleMoveCount, 3)


    def test_update_rulebase_diffs_on_delete_section_header(self):
            
            # Arrange

            # Move last five rules of last rulebase to new rulebase (previous config).

            last_rulebase = self._previous_config.rulebases[-1]
            last_five_rules_uids = list(last_rulebase.Rules.keys())[-5:]

            new_rulebase = self._config_builder.add_rulebase(self._previous_config, self._mgm_uid)

            for rule_uid in last_five_rules_uids:
                rule = last_rulebase.Rules.pop(rule_uid)
                self._config_builder.add_rule(self._previous_config, new_rulebase.uid, rule.model_dump())
            
            # Create rulebase link for cp_section header (previous config)

            last_rulebase_last_rule_uid = list(last_rulebase.Rules.keys())[-1]
            gateway = self._previous_config.gateways[0]
            self._config_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase.uid, last_rulebase_last_rule_uid)

            update_rule_map_and_rulebase_map(self._previous_config, self._import_state)
            update_rule_num_numerics(self._previous_config)

            rule_uids = [r for rb in self._normalized_config.rulebases for r in rb.Rules.keys()]

            # Act

            self._fwconfig_import_rule.updateRulebaseDiffs(self._previous_config)

            # Assert

            # The order of the entries in normalized_config (across rulebases)
            self.assertEqual(rule_uids, [r for rb in self._normalized_config.rulebases for r in rb.Rules.keys()]) 

            sorted_rules = [] 
            for rulebase in self._normalized_config.rulebases:
                sorted_rules.extend(sorted(rulebase.Rules.values(), key=lambda r: r.rule_num_numeric)) 
            sorted_rules_uids = [r.rule_uid for r in sorted_rules]
            
            # The sequence of the rule_num_numeric values
            self.assertEqual(rule_uids, sorted_rules_uids) 

            # Move to beginning, middle and end recognized in ImportDetails
            self.assertEqual(self._import_state.Stats.RuleAddCount, 0)
            self.assertEqual(self._import_state.Stats.RuleDeleteCount, 0)
            self.assertEqual(self._import_state.Stats.RuleChangeCount, 5)
            self.assertEqual(self._import_state.Stats.RuleMoveCount, 5)

        
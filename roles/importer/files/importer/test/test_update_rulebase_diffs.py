import unittest

from test.tools.set_up_test import set_up_test_for_ruleorder_test_with_delete_insert_move
from test.tools.set_up_test import set_up_test_for_ruleorder_test_with_consecutive_insertions
from test.tools.set_up_test import set_up_test_for_ruleorder_test_with_move_across_rulebases
from test.tools.set_up_test import set_up_test_for_ruleorder_test_with_move_to_beginning_middle_and_end_of_rulebase
from test.tools.set_up_test import set_up_test_for_ruleorder_test_with_delete_of_section_header


class TestUpdateRulebaseDiffs(unittest.TestCase):
      
    def test_update_rulebase_diffs_on_insert_delete_and_move(self):
        
        # Arrange

        previous_config, fwconfig_import_rule, rule_uids = set_up_test_for_ruleorder_test_with_delete_insert_move()
        
        # Act

        fwconfig_import_rule.updateRulebaseDiffs(previous_config)

        # Assert

        # The order of the entries in normalized_config
        self.assertEqual(rule_uids, list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.keys())) 

        sorted_rulebase_rules = sorted(list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.values()), key=lambda r: r.rule_num_numeric)
        sorted_rulebase_rules_uids = [r.rule_uid for r in sorted_rulebase_rules]
        
        # The sequence of the rule_num_numeric values
        self.assertEqual(rule_uids, sorted_rulebase_rules_uids) 

        # Insert, delete and move recognized in ImportDetails
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleAddCount, 1)
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleDeleteCount, 1)
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleChangeCount, 1)
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleMoveCount, 1)

    def test_update_rulebase_diffs_on_consecutive_insertions(self):
        
        # Arrange

        previous_config, fwconfig_import_rule, rule_uids = set_up_test_for_ruleorder_test_with_consecutive_insertions()

        # Act

        fwconfig_import_rule.updateRulebaseDiffs(previous_config)

        # Assert

        # The order of the entries in normalized_config
        self.assertEqual(rule_uids, list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.keys())) 

        sorted_rulebase_rules = sorted(list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.values()), key=lambda r: r.rule_num_numeric)
        sorted_rulebase_rules_uids = [r.rule_uid for r in sorted_rulebase_rules]
        
        # The sequence of the rule_num_numeric values
        self.assertEqual(rule_uids, sorted_rulebase_rules_uids) 

        # Insertions recognized in ImportDetails
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleAddCount, 9)
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleDeleteCount, 0)
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleChangeCount, 0)
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleMoveCount, 0)


    def test_update_rulebase_diffs_on_move_across_rulebases(self):

        # Arrange

        previous_config, fwconfig_import_rule, source_rulebase_uids, target_rulebase_uids = set_up_test_for_ruleorder_test_with_move_across_rulebases()

        # Act

        fwconfig_import_rule.updateRulebaseDiffs(previous_config)

        # Assert

        # The order of the entries in normalized_config
        self.assertEqual(source_rulebase_uids, list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.keys())) 
        self.assertEqual(target_rulebase_uids, list(fwconfig_import_rule.normalized_config.rulebases[1].Rules.keys())) 

        sorted_source_rulebase_rules = sorted(list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.values()), key=lambda r: r.rule_num_numeric)
        sorted_source_rulebase_rules_uids = [r.rule_uid for r in sorted_source_rulebase_rules]
        
        sorted_target_rulebase_rules = sorted(list(fwconfig_import_rule.normalized_config.rulebases[1].Rules.values()), key=lambda r: r.rule_num_numeric)
        sorted_target_rulebase_rules_uids = [r.rule_uid for r in sorted_target_rulebase_rules]

        # The sequence of the rule_num_numeric values
        self.assertEqual(source_rulebase_uids, sorted_source_rulebase_rules_uids) 
        self.assertEqual(target_rulebase_uids, sorted_target_rulebase_rules_uids) 

        # Move across rulebases recognized in ImportDetails
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleAddCount, 0)
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleDeleteCount, 0)
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleChangeCount, 1)
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleMoveCount, 1)


    def test_update_rulebase_diffs_on_moves_to_beginning_middle_and_end_of_rulebase(self):

        # Arrange

        previous_config, fwconfig_import_rule, rule_uids = set_up_test_for_ruleorder_test_with_move_to_beginning_middle_and_end_of_rulebase()

        # Act

        fwconfig_import_rule.updateRulebaseDiffs(previous_config)

        # Assert

        # The order of the entries in normalized_config
        self.assertEqual(rule_uids, list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.keys())) 

        sorted_rulebase_rules = sorted(list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.values()), key=lambda r: r.rule_num_numeric)
        sorted_rulebase_rules_uids = [r.rule_uid for r in sorted_rulebase_rules]
        
        # The sequence of the rule_num_numeric values
        self.assertEqual(rule_uids, sorted_rulebase_rules_uids) 

        # Move to beginning, middle and end recognized in ImportDetails
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleAddCount, 0)
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleDeleteCount, 0)
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleChangeCount, 3)
        self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleMoveCount, 3)


    def test_update_rulebase_diffs_on_delete_section_header(self):
            
            # Arrange

            previous_config, fwconfig_import_rule, rule_uids = set_up_test_for_ruleorder_test_with_delete_of_section_header()

            # Act

            fwconfig_import_rule.updateRulebaseDiffs(previous_config)

            # Assert

            # The order of the entries in normalized_config (across rulebases)
            self.assertEqual(rule_uids, [r for rb in fwconfig_import_rule.normalized_config.rulebases for r in rb.Rules.keys()]) 

            sorted_rules = [] 
            for rulebase in fwconfig_import_rule.normalized_config.rulebases:
                sorted_rules.extend(sorted(rulebase.Rules.values(), key=lambda r: r.rule_num_numeric)) 
            sorted_rules_uids = [r.rule_uid for r in sorted_rules]
            
            # The sequence of the rule_num_numeric values
            self.assertEqual(rule_uids, sorted_rules_uids) 

            # Move to beginning, middle and end recognized in ImportDetails
            self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleAddCount, 0)
            self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleDeleteCount, 0)
            self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleChangeCount, 5)
            self.assertEqual(fwconfig_import_rule.import_details.Stats.RuleMoveCount, 5)


        
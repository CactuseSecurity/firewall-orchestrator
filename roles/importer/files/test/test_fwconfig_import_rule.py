import unittest
import sys
import os

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

from test.tools.set_up_test import set_up_test_for_ruleorder_test_with_relevant_changes


class TestFwoConfigImportRule(unittest.TestCase):

    @unittest.skip("Temporary deactivated, because necessary feature in mock class (mocking api calls) is not implemented yet.")        
    def test_update_rulebase_diffs_on_insert_delete_and_move(self):
        
        # Arrange

        previous_config, fwconfig_import_rule, rule_uids = set_up_test_for_ruleorder_test_with_relevant_changes()
        
        # Act

        fwconfig_import_rule.updateRulebaseDiffs(previous_config)

        # Assert

        self.assertEqual(rule_uids, list(fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.keys())) # The order of the entries in the dictionary

        sorted_rulebase_rules = sorted(list(fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.values()), key=lambda r: r.rule_num_numeric)
        sorted_rulebase_rules_uids = [r.rule_uid for r in sorted_rulebase_rules]

        self.assertEqual(rule_uids, sorted_rulebase_rules_uids) # The sequence of the rule_num_numeric values

        # Insert, delete and move recognized in ImportDetails
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleAddCount, 1)
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleDeleteCount, 1)
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleChangeCount, 0)
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleMoveCount, 1)


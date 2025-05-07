import unittest
import sys
import os
import copy

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

from importer.models.rule import RuleNormalized
from test.mocking.mock_config import MockFwConfigNormalized
from test.mocking.mock_fwconfig_import_rule import MockFwConfigImportRule

class TestFwoConfigImportRule(unittest.TestCase):

        
    def test_update_rulebase_diffs_on_insert_delete_and_move(self):
        
        # Arrange

        previous_config = MockFwConfigNormalized()
        previous_config.initialize_config(
            {
                "rule_config": [10,10,10]
            }
        )
        new_num_numeric = 0.0
        rule_num_numeric_steps = 10.0
        for rulebase in previous_config.rulebases:
            for rule in rulebase.Rules.values():
                new_num_numeric += rule_num_numeric_steps
                rule.rule_num_numeric = new_num_numeric

        fwconfig_import_rule = MockFwConfigImportRule()
        fwconfig_import_rule.NormalizedConfig = copy.deepcopy(previous_config)
        
        rule_uids = list(fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.keys())

        deleted_rule_position = 0
        deleted_rule_uid = list(fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.keys())[deleted_rule_position]
        deleted_rule = fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.pop(deleted_rule_uid)
        rule_uids.pop(deleted_rule_position)

        inserted_rule_position = 0
        inserted_rule = fwconfig_import_rule.NormalizedConfig.add_rule_to_rulebase(fwconfig_import_rule.NormalizedConfig.rulebases[0].uid)
        inserted_rule_uid = inserted_rule.rule_uid
        fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules[inserted_rule_uid] = inserted_rule
        rule_uids.insert(inserted_rule_position, inserted_rule_uid)

        self.reorder_rulebase_rules_dict(fwconfig_import_rule, 0, rule_uids)

        moved_rule_source_position = 9
        moved_rule_target_position = 0
        moved_rule_uid = list(fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.keys())[moved_rule_source_position]
        moved_rule = fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.pop(moved_rule_uid)
        fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules[moved_rule_uid] = moved_rule
        rule_uids.pop(9)
        rule_uids.insert(moved_rule_target_position, moved_rule_uid)

        self.reorder_rulebase_rules_dict(fwconfig_import_rule, 0, rule_uids)
        
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


    def reorder_rulebase_rules_dict(self, fwconfig_import_rule, rulebase_index, rule_uids):
        """
            Imitates the changes in order in the config dict.
        """
        rules = copy.deepcopy(fwconfig_import_rule.NormalizedConfig.rulebases[rulebase_index].Rules)
        fwconfig_import_rule.NormalizedConfig.rulebases[rulebase_index].Rules = {}
        for rule_uid in rule_uids:
            fwconfig_import_rule.NormalizedConfig.rulebases[rulebase_index].Rules[rule_uid] = rules[rule_uid]


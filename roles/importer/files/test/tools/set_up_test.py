import unittest
import sys
import os
import copy

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

from test.mocking.mock_config import MockFwConfigNormalized
from test.mocking.mock_fwconfig_import_rule import MockFwConfigImportRule


def set_up_test_for_ruleorder_test_with_defaults():

    previous_config = MockFwConfigNormalized()
    previous_config.initialize_config(
        {
            "rule_config": [10,10,10],
            "network_object_config": 10,
            "service_config": 10,
            "user_config": 10
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

    return previous_config, fwconfig_import_rule


def set_up_test_for_ruleorder_test_with_relevant_changes():
        
        previous_config, fwconfig_import_rule = set_up_test_for_ruleorder_test_with_defaults()

        rule_uids = list(fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.keys())

        deleted_rule_position = 0
        deleted_rule_uid = list(fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.keys())[deleted_rule_position]
        fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.pop(deleted_rule_uid)
        rule_uids.pop(deleted_rule_position)

        inserted_rule_position = 0
        inserted_rule = fwconfig_import_rule.NormalizedConfig.add_rule_to_rulebase(fwconfig_import_rule.NormalizedConfig.rulebases[0].uid)
        inserted_rule_uid = inserted_rule.rule_uid
        fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules[inserted_rule_uid] = inserted_rule
        rule_uids.insert(inserted_rule_position, inserted_rule_uid)

        reorder_rulebase_rules_dict(fwconfig_import_rule, 0, rule_uids)

        moved_rule_source_position = 9
        moved_rule_target_position = 0
        moved_rule_uid = list(fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.keys())[moved_rule_source_position]
        moved_rule = fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.pop(moved_rule_uid)
        fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules[moved_rule_uid] = moved_rule
        rule_uids.pop(9)
        rule_uids.insert(moved_rule_target_position, moved_rule_uid)

        reorder_rulebase_rules_dict(fwconfig_import_rule, 0, rule_uids)

        return previous_config, fwconfig_import_rule, rule_uids
    

def reorder_rulebase_rules_dict(fwconfig_import_rule, rulebase_index, rule_uids):
    """
        Imitates the changes in order in the config dict.
    """
    rules = copy.deepcopy(fwconfig_import_rule.NormalizedConfig.rulebases[rulebase_index].Rules)
    fwconfig_import_rule.NormalizedConfig.rulebases[rulebase_index].Rules = {}
    for rule_uid in rule_uids:
        fwconfig_import_rule.NormalizedConfig.rulebases[rulebase_index].Rules[rule_uid] = rules[rule_uid]


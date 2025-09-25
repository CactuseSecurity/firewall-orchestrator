import sys
import os
import copy

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

from test.mocking.mock_config import MockFwConfigNormalizedBuilder
from test.mocking.mock_fwconfig_import_rule import MockFwConfigImportRule

from fwo_const import rule_num_numeric_steps

def set_up_config_for_import_consistency_test():

    config_builder = MockFwConfigNormalizedBuilder()
    config = config_builder.build_config(
        {
            "rule_config": [10, 10, 10],
            "network_object_config": 10,
            "service_config": 10,
            "user_config": 10
        }
    )
    config_builder.add_rule_with_nested_groups(config)

    return config


def set_up_test_for_ruleorder_test_with_defaults():

    config_builder = MockFwConfigNormalizedBuilder()
    previous_config = config_builder.build_config(
        {
            "rule_config": [10,10,10],
            "network_object_config": 10,
            "service_config": 10,
            "user_config": 10
        }
    )
    new_num_numeric = 0.0


    fwconfig_import_rule = MockFwConfigImportRule()
    fwconfig_import_rule.normalized_config = copy.deepcopy(previous_config)

    for rulebase in previous_config.rulebases:
        for rule in rulebase.Rules.values():
            new_num_numeric += rule_num_numeric_steps
            rule.rule_num_numeric = new_num_numeric

    return previous_config, fwconfig_import_rule


def set_up_test_for_ruleorder_test_with_delete_insert_move():
        
        config_builder = MockFwConfigNormalizedBuilder()
        previous_config, fwconfig_import_rule = set_up_test_for_ruleorder_test_with_defaults()

        rule_uids = list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.keys())

        delete_rule_from_config(fwconfig_import_rule, 0, 0, rule_uids)
        insert_rule_in_config(fwconfig_import_rule, 0, 0, rule_uids, config_builder)
        move_rule_in_config(fwconfig_import_rule, 0, 9, 0, rule_uids)

        return previous_config, fwconfig_import_rule, rule_uids


def set_up_test_for_ruleorder_test_with_consecutive_insertions():
        
        config_builder = MockFwConfigNormalizedBuilder()
        previous_config, fwconfig_import_rule = set_up_test_for_ruleorder_test_with_defaults()

        rule_uids = list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.keys())

        # Inserting three new rules at the beginning of the rulebase
        insert_rule_in_config(fwconfig_import_rule, 0, 0, rule_uids, config_builder)
        insert_rule_in_config(fwconfig_import_rule, 0, 0, rule_uids, config_builder)
        insert_rule_in_config(fwconfig_import_rule, 0, 0, rule_uids, config_builder)

        # Inserting three new rules in the middle of the rulebase
        insert_rule_in_config(fwconfig_import_rule, 0, (len(rule_uids) - 3)//2, rule_uids, config_builder)
        insert_rule_in_config(fwconfig_import_rule, 0, (len(rule_uids) - 3)//2, rule_uids, config_builder)
        insert_rule_in_config(fwconfig_import_rule, 0, (len(rule_uids) - 3)//2, rule_uids, config_builder)

        # Inserting three new rules at the end of the rulebase
        insert_rule_in_config(fwconfig_import_rule, 0, len(rule_uids), rule_uids, config_builder)
        insert_rule_in_config(fwconfig_import_rule, 0, len(rule_uids), rule_uids, config_builder)
        insert_rule_in_config(fwconfig_import_rule, 0, len(rule_uids), rule_uids, config_builder)

        return previous_config, fwconfig_import_rule, rule_uids


def set_up_test_for_ruleorder_test_with_move_across_rulebases():
    config_builder = MockFwConfigNormalizedBuilder()
    previous_config, fwconfig_import_rule = set_up_test_for_ruleorder_test_with_defaults()

    source_rulebase_uids = list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.keys())
    target_rulebase_uids = list(fwconfig_import_rule.normalized_config.rulebases[1].Rules.keys())

    _, deleted_rule = delete_rule_from_config(fwconfig_import_rule, 0, 0, source_rulebase_uids)
    insert_rule_in_config(fwconfig_import_rule, 1, 0, target_rulebase_uids, config_builder, deleted_rule)

    return previous_config, fwconfig_import_rule, source_rulebase_uids, target_rulebase_uids


def set_up_test_for_ruleorder_test_with_move_to_beginning_middle_and_end_of_rulebase():
    config_builder = MockFwConfigNormalizedBuilder()
    previous_config, fwconfig_import_rule = set_up_test_for_ruleorder_test_with_defaults()

    rule_uids = list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.keys())

    move_rule_in_config(fwconfig_import_rule, 0, (len(rule_uids) - 1)//2, 0, rule_uids)  # Move to beginning
    move_rule_in_config(fwconfig_import_rule, 0, 1, (len(rule_uids) - 1)//2, rule_uids)  # Move to middle
    move_rule_in_config(fwconfig_import_rule, 0, 2, len(rule_uids) - 1, rule_uids)  # Move to end

    return previous_config, fwconfig_import_rule, rule_uids


def reorder_rulebase_rules_dict(fwconfig_import_rule, rulebase_index, rule_uids):
    """
        Imitates the changes in order in the config dict.
    """
    
    rules = copy.deepcopy(fwconfig_import_rule.normalized_config.rulebases[rulebase_index].Rules)
    fwconfig_import_rule.normalized_config.rulebases[rulebase_index].Rules = {}
    for rule_uid in rule_uids:
        fwconfig_import_rule.normalized_config.rulebases[rulebase_index].Rules[rule_uid] = rules[rule_uid]


def delete_rule_from_config(fwconfig_import_rule, rulebase_index, rule_position, rule_uids):
    """
        Imitates the deletion of a rule in the config dict.
    """

    rule_uid = list(fwconfig_import_rule.normalized_config.rulebases[rulebase_index].Rules.keys())[rule_position]
    rule = fwconfig_import_rule.normalized_config.rulebases[rulebase_index].Rules.pop(rule_uid)
    rule_uids.pop(rule_position)

    return rule_uid, rule


def insert_rule_in_config(fwconfig_import_rule, rulebase_index, rule_position, rule_uids, config_builder, rule = None):
    """
        Imitates the insertion of a rule in the config dict.
    """

    if rule is None:
        inserted_rule = config_builder.add_rule(fwconfig_import_rule.normalized_config, fwconfig_import_rule.normalized_config.rulebases[rulebase_index].uid)
    else:
        inserted_rule = rule
        fwconfig_import_rule.normalized_config.rulebases[rulebase_index].Rules[inserted_rule.rule_uid] = inserted_rule

    rule_uids.insert(rule_position, inserted_rule.rule_uid)

    reorder_rulebase_rules_dict(fwconfig_import_rule, rulebase_index, rule_uids)
    

def move_rule_in_config(fwconfig_import_rule, rulebase_index, source_position, target_position, rule_uids):
    """
        Imitates the moving of a rule in the config dict.
    """

    rule_uid = list(fwconfig_import_rule.normalized_config.rulebases[rulebase_index].Rules.keys())[source_position]
    rule = fwconfig_import_rule.normalized_config.rulebases[rulebase_index].Rules.pop(rule_uid)
    fwconfig_import_rule.normalized_config.rulebases[rulebase_index].Rules[rule_uid] = rule
    rule_uids.pop(source_position)
    rule_uids.insert(target_position, rule_uid)

    reorder_rulebase_rules_dict(fwconfig_import_rule, rulebase_index, rule_uids)
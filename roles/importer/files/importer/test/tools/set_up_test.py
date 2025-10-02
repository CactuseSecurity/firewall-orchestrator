import copy

from fwo_const import rule_num_numeric_steps

from models.fwconfig_normalized import FwConfigNormalized
from models.rulebase_link import RulebaseLink, RulebaseLinkUidBased

from test.mocking.mock_config import MockFwConfigNormalizedBuilder
from test.mocking.mock_fwconfig_import_rule import MockFwConfigImportRule


def set_up_config_for_import_consistency_test():

    config_builder = MockFwConfigNormalizedBuilder()
    config, _ = config_builder.build_config(
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
    previous_config, mgm_uid = config_builder.build_config(
        {
            "rule_config": [10,10,10],
            "network_object_config": 10,
            "service_config": 10,
            "user_config": 10
        }
    )

    fwconfig_import_rule = MockFwConfigImportRule()
    fwconfig_import_rule.normalized_config = copy.deepcopy(previous_config)

    update_rule_num_numerics(previous_config)
    update_rule_map_and_rulebase_map(previous_config, fwconfig_import_rule.import_details)

    return previous_config, fwconfig_import_rule, config_builder, mgm_uid


def reorder_rulebase_rules_dict(config: FwConfigNormalized, rulebase_uid, rule_uids):
    """
        Imitates the changes in order in the config dict.
    """
    
    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)

    if rulebase:
        rules = copy.deepcopy(rulebase.Rules)
        rulebase.Rules = {}
        for rule_uid in rule_uids:
            rulebase.Rules[rule_uid] = rules[rule_uid]


def delete_rule_from_config(config: FwConfigNormalized, rulebase_index, rule_position, rule_uids):
    """
        Imitates the deletion of a rule in the config dict.
    """

    rule_uid = list(config.rulebases[rulebase_index].Rules.keys())[rule_position]
    rule = config.rulebases[rulebase_index].Rules.pop(rule_uid)
    rule_uids.pop(rule_position)

    return rule_uid, rule


def insert_rule_in_config(config: FwConfigNormalized, rulebase_uid, rule_position, rule_uids, config_builder, rule = None):
    """
        Imitates the insertion of a rule in the config dict.
    """

    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)

    if rulebase:

        if rule is None:
            inserted_rule = config_builder.add_rule(config, rulebase_uid)
        else:
            inserted_rule = rule
            rulebase.Rules[inserted_rule.rule_uid] = inserted_rule

        rule_uids.insert(rule_position, inserted_rule.rule_uid)

        reorder_rulebase_rules_dict(config, rulebase_uid, rule_uids)
    

def move_rule_in_config(config: FwConfigNormalized, rulebase_uid, source_position, target_position, rule_uids):
    """
        Imitates the moving of a rule in the config dict.
    """

    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)

    if rulebase:
        rule_uid = list(rulebase.Rules.keys())[source_position]
        rule = rulebase.Rules.pop(rule_uid)
        rulebase.Rules[rule_uid] = rule
        rule_uids.pop(source_position)
        rule_uids.insert(target_position, rule_uid)

        reorder_rulebase_rules_dict(config, rulebase.uid, rule_uids)


def update_rule_map_and_rulebase_map(config, import_state):

    import_state.RulebaseMap = {}
    import_state.RuleMap = {}

    rulebase_id = 1
    rule_id = 1

    for rulebase in config.rulebases:
        import_state.RulebaseMap[rulebase.uid] = rulebase_id
        rulebase_id += 1
        for rule in rulebase.Rules.values():
            import_state.RuleMap[rule.rule_uid] = rule_id
            rule_id += 1


def update_rule_num_numerics(config):
    
    for rulebase in config.rulebases:
        new_num_numeric = 0
        for rule in rulebase.Rules.values():
            new_num_numeric += rule_num_numeric_steps
            rule.rule_num_numeric = new_num_numeric


def update_rb_links(rulebase_links: list[RulebaseLinkUidBased], gateway_id, fwconfig_import_gateway):

    new_rb_links: list[RulebaseLink] = []
    link_id = 0

    for link in rulebase_links:

        link_id += 1

        link_type = 0
        match link.link_type:
            case "ordered":
                link_type = 2
            case "inline":
                link_type = 3
            case "concatenated":
                link_type = 4
            case "domain":
                link_type = 5
            case _:
                link_type = 0               

        new_rb_links.append(RulebaseLink(
            id = link_id,
            gw_id = gateway_id,
            from_rule_id = fwconfig_import_gateway._global_state.import_state.lookupRule(link.from_rule_uid),
            from_rulebase_id = fwconfig_import_gateway._global_state.import_state.lookupRulebaseId(link.from_rulebase_uid),
            to_rulebase_id = fwconfig_import_gateway._global_state.import_state.lookupRulebaseId(link.to_rulebase_uid),
            link_type = link_type,
            is_initial = link.is_initial,
            is_global = link.is_global,
            is_section = link.is_section,
            created = 0
        ))

    fwconfig_import_gateway._rb_link_controller.rb_links = new_rb_links


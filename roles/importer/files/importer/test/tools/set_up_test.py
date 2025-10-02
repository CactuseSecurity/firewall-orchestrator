import copy

from fwo_base import init_service_provider
from fwo_const import rule_num_numeric_steps

from models.rulebase_link import RulebaseLink, RulebaseLinkUidBased

from test.mocking.mock_config import MockFwConfigNormalizedBuilder
from test.mocking.mock_fwconfig_import_rule import MockFwConfigImportRule
from test.mocking.mock_fwconfig_import_gateway import MockFwConfigImportGateway


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


def set_up_test_for_ruleorder_test_with_delete_insert_move():
        
        previous_config, fwconfig_import_rule, config_builder, _ = set_up_test_for_ruleorder_test_with_defaults()

        rule_uids = list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.keys())

        delete_rule_from_config(fwconfig_import_rule, 0, 0, rule_uids)
        insert_rule_in_config(fwconfig_import_rule, 0, 0, rule_uids, config_builder)
        move_rule_in_config(fwconfig_import_rule, 0, 9, 0, rule_uids)

        return previous_config, fwconfig_import_rule, rule_uids


def set_up_test_for_ruleorder_test_with_consecutive_insertions():
        
        previous_config, fwconfig_import_rule, config_builder, _ = set_up_test_for_ruleorder_test_with_defaults()

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

    previous_config, fwconfig_import_rule, config_builder, _ = set_up_test_for_ruleorder_test_with_defaults()

    source_rulebase_uids = list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.keys())
    target_rulebase_uids = list(fwconfig_import_rule.normalized_config.rulebases[1].Rules.keys())

    _, deleted_rule = delete_rule_from_config(fwconfig_import_rule, 0, 0, source_rulebase_uids)
    insert_rule_in_config(fwconfig_import_rule, 1, 0, target_rulebase_uids, config_builder, deleted_rule)

    return previous_config, fwconfig_import_rule, source_rulebase_uids, target_rulebase_uids


def set_up_test_for_ruleorder_test_with_move_to_beginning_middle_and_end_of_rulebase():

    previous_config, fwconfig_import_rule, _, _ = set_up_test_for_ruleorder_test_with_defaults()

    rule_uids = list(fwconfig_import_rule.normalized_config.rulebases[0].Rules.keys())

    move_rule_in_config(fwconfig_import_rule, 0, (len(rule_uids) - 1)//2, 0, rule_uids)  # Move to beginning
    move_rule_in_config(fwconfig_import_rule, 0, 1, (len(rule_uids) - 1)//2, rule_uids)  # Move to middle
    move_rule_in_config(fwconfig_import_rule, 0, 2, len(rule_uids) - 1, rule_uids)  # Move to end

    return previous_config, fwconfig_import_rule, rule_uids


def set_up_test_for_ruleorder_test_with_delete_of_section_header():

    previous_config, fwconfig_import_rule, config_builder, mgm_uid = set_up_test_for_ruleorder_test_with_defaults()

    # Move last five rules of last rulebase to new rulebase (previous config).

    last_rulebase = previous_config.rulebases[-1]
    last_five_rules_uids = list(last_rulebase.Rules.keys())[-5:]

    _, new_rulebase_uid = config_builder.add_rulebase(previous_config, mgm_uid)

    for rule_uid in last_five_rules_uids:
        rule = last_rulebase.Rules.pop(rule_uid)
        config_builder.add_rule(previous_config, new_rulebase_uid, rule.model_dump())
    
    # Create rulebase link for cp_section header (previous config)

    last_rulebase_last_rule_uid = list(last_rulebase.Rules.keys())[-1]
    gateway = previous_config.gateways[0]
    config_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase_uid, last_rulebase_last_rule_uid)

    update_rule_map_and_rulebase_map(previous_config, fwconfig_import_rule.import_details)
    update_rule_num_numerics(previous_config)

    return previous_config, fwconfig_import_rule, [r for rb in fwconfig_import_rule.normalized_config.rulebases for r in rb.Rules.keys()]


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


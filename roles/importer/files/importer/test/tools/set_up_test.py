import copy

from fwo_const import RULE_NUM_NUMERIC_STEPS
from models.fwconfig_normalized import FwConfigNormalized
from models.rulebase_link import RulebaseLink, RulebaseLinkUidBased
from test.mocking.mock_config import MockFwConfigNormalizedBuilder
from test.mocking.mock_fwconfig_import_rule import MockFwConfigImportRule
from test.mocking.mock_import_state import MockImportStateController


def set_up_config_for_import_consistency_test():
    config_builder = MockFwConfigNormalizedBuilder()
    config, _ = config_builder.build_config(
        {"rule_config": [10, 10, 10], "network_object_config": 10, "service_config": 10, "user_config": 10}
    )
    config_builder.add_rule_with_nested_groups(config)

    return config


def set_up_test_for_ruleorder_test_with_defaults():
    config_builder = MockFwConfigNormalizedBuilder()
    previous_config, mgm_uid = config_builder.build_config(
        {"rule_config": [10, 10, 10], "network_object_config": 10, "service_config": 10, "user_config": 10}
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
        rules = copy.deepcopy(rulebase.rules)
        rulebase.rules = {}
        for rule_uid in rule_uids:
            rulebase.rules[rule_uid] = rules[rule_uid]


def remove_rule_from_rulebase(
    config: FwConfigNormalized, rulebase_uid: str, rule_uid: str, uid_sequence: list[str] | None = None
):
    """
    Imitates the deletion of a rule in the config dict.
    """
    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)

    if rulebase:
        rule = rulebase.rules.pop(rule_uid)

        if uid_sequence:
            uid_sequence[:] = [uid for uid in uid_sequence if uid != rule_uid]

    return rule


def insert_rule_in_config(
    config: FwConfigNormalized, rulebase_uid, rule_position, rule_uids, config_builder, rule=None
):
    """
    Imitates the insertion of a rule in the config dict.
    """
    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)
    inserted_rule_uid = ""

    if rulebase:
        if rule is None:
            inserted_rule = config_builder.add_rule(config, rulebase_uid)
        else:
            inserted_rule = rule
            rulebase.rules[inserted_rule.rule_uid] = inserted_rule

        rule_uids.insert(rule_position, inserted_rule.rule_uid)

        reorder_rulebase_rules_dict(config, rulebase_uid, rule_uids)

        inserted_rule_uid = inserted_rule.rule_uid

    return inserted_rule_uid


def move_rule_in_config(config: FwConfigNormalized, rulebase_uid, source_position, target_position, rule_uids):
    """
    Imitates the moving of a rule in the config dict.
    """
    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)
    moved_rule_uid = ""

    if rulebase:
        rule_uid = list(rulebase.rules.keys())[source_position]
        rule = rulebase.rules.pop(rule_uid)
        rulebase.rules[rule_uid] = rule
        rule_uids.pop(source_position)
        rule_uids.insert(target_position, rule_uid)

        reorder_rulebase_rules_dict(config, rulebase.uid, rule_uids)

        moved_rule_uid = rule_uid

    return moved_rule_uid


def update_rule_map_and_rulebase_map(config, import_state: MockImportStateController):
    import_state.state.rulebase_map = {}
    import_state.state.rule_map = {}

    rulebase_id = 1
    rule_id = 1

    for rulebase in config.rulebases:
        import_state.state.rulebase_map[rulebase.uid] = rulebase_id
        rulebase_id += 1
        for rule in rulebase.rules.values():
            import_state.state.rule_map[rule.rule_uid] = rule_id
            rule_id += 1


def update_rule_num_numerics(config):
    for rulebase in config.rulebases:
        new_num_numeric = 0
        for rule in rulebase.rules.values():
            new_num_numeric += RULE_NUM_NUMERIC_STEPS
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

        new_rb_links.append(
            RulebaseLink(
                id=link_id,
                gw_id=gateway_id,
                from_rule_id=fwconfig_import_gateway._global_state.import_state.state.lookup_rule(link.from_rule_uid),
                from_rulebase_id=fwconfig_import_gateway._global_state.import_state.state.lookup_rulebase_id(
                    link.from_rulebase_uid
                )
                if link.from_rulebase_uid
                else None,
                to_rulebase_id=fwconfig_import_gateway._global_state.import_state.state.lookup_rulebase_id(
                    link.to_rulebase_uid
                ),
                link_type=link_type,
                is_initial=link.is_initial,
                is_global=link.is_global,
                is_section=link.is_section,
                created=0,
            )
        )

    fwconfig_import_gateway._rb_link_controller.rb_links = new_rb_links


def lookup_ids_for_rulebase_link(
    import_state: MockImportStateController,
    from_rule_uid: str = "",
    from_rulebase_uid: str = "",
    to_rulebase_uid: str = "",
):
    from_rule_id = None
    from_rulebase_id = None
    to_rulebase_id = None

    if from_rule_uid != "":
        from_rule_id = import_state.state.lookup_rule(from_rule_uid)
    if from_rulebase_uid != "":
        from_rulebase_id = import_state.state.lookup_rulebase_id(from_rulebase_uid)
    if to_rulebase_uid != "":
        to_rulebase_id = import_state.state.lookup_rulebase_id(to_rulebase_uid)

    return from_rule_id, from_rulebase_id, to_rulebase_id

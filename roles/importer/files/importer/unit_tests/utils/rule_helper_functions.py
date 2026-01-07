import copy

from models.fwconfig_normalized import FwConfigNormalized
from models.rule import RuleNormalized
from unit_tests.utils.config_builder import FwConfigBuilder


def remove_rule_from_rulebase(
    config: FwConfigNormalized, rulebase_uid: str, rule_uid: str, uid_sequence: list[str] | None = None
) -> RuleNormalized:
    """
    Imitates the deletion of a rule in the config dict.
    """
    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)

    if rulebase:
        rule = rulebase.rules.pop(rule_uid)

        if uid_sequence:
            uid_sequence[:] = [uid for uid in uid_sequence if uid != rule_uid]

        return rule
    raise ValueError(f"Rulebase with UID {rulebase_uid} not found.")


def reorder_rulebase_rules_dict(config: FwConfigNormalized, rulebase_uid: str, rule_uids: list[str]):
    """
    Imitates the changes in order in the config dict.
    """
    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)

    if rulebase:
        rules = copy.deepcopy(rulebase.rules)
        rulebase.rules = {}
        for rule_uid in rule_uids:
            rulebase.rules[rule_uid] = rules[rule_uid]


def insert_rule_in_config(
    config: FwConfigNormalized,
    rulebase_uid: str,
    rule_position: int,
    rule_uids: list[str],
    config_builder: FwConfigBuilder,
    rule: RuleNormalized | None = None,
):
    """
    Imitates the insertion of a rule in the config dict.
    """
    rulebase = next((rb for rb in config.rulebases if rb.uid == rulebase_uid), None)
    inserted_rule_uid = None

    if rulebase:
        if rule is None:
            inserted_rule = config_builder.add_rule(config, rulebase_uid)
        else:
            inserted_rule = rule
            assert inserted_rule.rule_uid is not None
            rulebase.rules[inserted_rule.rule_uid] = inserted_rule

        assert inserted_rule.rule_uid is not None
        rule_uids.insert(rule_position, inserted_rule.rule_uid)

        reorder_rulebase_rules_dict(config, rulebase_uid, rule_uids)

        inserted_rule_uid = inserted_rule.rule_uid

    return inserted_rule_uid


def move_rule_in_config(
    config: FwConfigNormalized, rulebase_uid: str, source_position: int, target_position: int, rule_uids: list[str]
):
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


def get_rule(normalized_config: FwConfigNormalized, rulebase_index: int, rule_uid: str) -> RuleNormalized | None:
    """
    Helper method to get a rule from the normalized config.
    """
    rulebase = normalized_config.rulebases[rulebase_index]
    return rulebase.rules.get(rule_uid, None)

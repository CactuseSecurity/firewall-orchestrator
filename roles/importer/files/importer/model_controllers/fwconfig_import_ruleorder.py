from typing import TYPE_CHECKING

from fwo_base import compute_min_moves
from fwo_const import RULE_NUM_NUMERIC_STEPS
from fwo_log import FWOLogger

if TYPE_CHECKING:
    from models.fwconfig_normalized import FwConfigNormalized
    from models.rulebase import Rulebase


def update_rule_order_diffs(previous_config: "FwConfigNormalized", normalized_config: "FwConfigNormalized") -> None:
    """
    Sets all rule_num_numeric values in normalized_config based on previous config, changes in the rule order and new inserts.

    Args:
        previous_config (FwConfigNormalized): The previous normalized configuration.
        normalized_config (FwConfigNormalized): The current normalized configuration to update.

    """
    for rulebase in normalized_config.rulebases:
        previous_rulebase = next((rb for rb in previous_config.rulebases if rb.uid == rulebase.uid), None)
        if previous_rulebase is None:
            calculate_initial_rule_nums(rulebase)
        else:
            update_rule_nums_based_on_previous(rulebase, previous_rulebase)


def calculate_initial_rule_nums(rulebase: "Rulebase") -> None:
    """Sets initial rule_num_numeric values for all rules in a rulebase."""
    current_rule_num_numeric = 0
    for rule in rulebase.rules.values():
        current_rule_num_numeric += RULE_NUM_NUMERIC_STEPS
        rule.rule_num_numeric = current_rule_num_numeric


def update_rule_nums_based_on_previous(current_rulebase: "Rulebase", previous_rulebase: "Rulebase") -> None:
    """Calculates and sets rule_num_numeric values based on previous rulebase, moves and inserts."""
    min_moves_result = compute_min_moves(list(previous_rulebase.rules.keys()), list(current_rulebase.rules.keys()))
    _deletions = {uid for _, uid in min_moves_result["deletions"]}
    insertions = {uid for _, uid in min_moves_result["insertions"]}
    reposition_moves = {uid for _, uid, _pos in min_moves_result["reposition_moves"]}
    # calculate new rule_num_numerics for moved and inserted rules
    uids_num_unchanged = current_rulebase.rules.keys() - insertions - reposition_moves
    for rule_uid in uids_num_unchanged:
        # preserve rule_num_numeric for rules that existed and were not moved
        current_rulebase.rules[rule_uid].rule_num_numeric = previous_rulebase.rules[rule_uid].rule_num_numeric
    # Precompute ordered lists of rule UIDs and rule objects to avoid repeated list construction in the loop
    rule_uids = list(current_rulebase.rules.keys())
    rule_list = list(current_rulebase.rules.values())
    for index, rule in enumerate(rule_list):
        if rule.rule_num_numeric:
            continue  # already set, no need to update
        prev_num_numeric = 0.0
        if index > 0:
            prev_rule_uid = rule_uids[index - 1]
            prev_num_numeric = current_rulebase.rules[prev_rule_uid].rule_num_numeric
        next_num_numeric = next(
            (rule.rule_num_numeric for idx, rule in enumerate(rule_list) if idx > index and rule.rule_num_numeric),
            None,
        )
        if next_num_numeric is not None:
            rule.rule_num_numeric = (prev_num_numeric + next_num_numeric) / 2
        else:
            rule.rule_num_numeric = prev_num_numeric + RULE_NUM_NUMERIC_STEPS

    # if there are any rule_num_numeric collisions, recalculate all rule_num_numeric values
    rule_num_numerics = [rule.rule_num_numeric for rule in current_rulebase.rules.values()]
    if len(rule_num_numerics) != len(set(rule_num_numerics)):
        FWOLogger.info(
            f"Rule number collisions detected in rulebase {current_rulebase.name} ({current_rulebase.uid}). Recalculating all rule numbers."
        )
        calculate_initial_rule_nums(current_rulebase)

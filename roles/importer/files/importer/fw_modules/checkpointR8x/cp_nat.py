import json
from typing import Any

from fw_modules.checkpointR8x.cp_rule import check_and_add_section_header, parse_single_rule
from fwo_log import FWOLogger
from models.import_state import ImportState
from models.rulebase import Rulebase


def normalize_nat_rules(native_config: dict[str, Any], import_state: ImportState, normalized_config: dict[str, Any]):
    native_nat_rulebases = native_config.get("nat_rulebases", [])
    if not native_nat_rulebases:
        return
    for nat_rulebase in native_nat_rulebases:
        if "nat_rule_chunks" in nat_rulebase:
            # parse chunks
            pass
        else:
            # parse rulebase
            pass


def parse_nat_rulebase(
    src_rulebase: dict[str, Any],
    target_rulebase: Rulebase,
    layer_name: str,
    import_id: str,
    section_header_uids: set[str],
    parent_uid: str,
    gateway: dict[str, Any],
    policy_structure: list[dict[str, Any]],
    debug_level: int = 0,
    recursion_level: int = 1,
):
    if recursion_level > 1000000:
        raise Exception("ImportRecursionLimitReached(parse_nat_rulebase_json) from None")

    if "nat_rule_chunks" in src_rulebase:
        for chunk in src_rulebase["nat_rule_chunks"]:
            if "rulebase" in chunk:
                for rules_chunk in chunk["rulebase"]:
                    parse_nat_rulebase(
                        rules_chunk,
                        target_rulebase,
                        layer_name,
                        import_id,
                        section_header_uids,
                        parent_uid,
                        gateway,
                        policy_structure,
                        debug_level=debug_level,
                        recursion_level=recursion_level + 1,
                    )
            else:
                FWOLogger.warning(f"parse_rule: found no rulebase in chunk:\n{json.dumps(chunk, indent=2)}")
    else:
        if "rulebase" in src_rulebase:
            check_and_add_section_header(src_rulebase, target_rulebase, layer_name, import_id, section_header_uids)

            for rule in src_rulebase["rulebase"]:
                (rule_match, rule_xlate) = parse_nat_rule_transform(rule)
                parse_single_rule(rule_match, target_rulebase, layer_name, parent_uid, gateway, policy_structure)
                parse_single_rule(  # do not increase rule_num here
                    rule_xlate, target_rulebase, layer_name, parent_uid, gateway, policy_structure
                )

        if "rule-number" in src_rulebase:  # rulebase is just a single rule (xlate rules do not count)
            (rule_match, rule_xlate) = parse_nat_rule_transform(src_rulebase)
            parse_single_rule(rule_match, target_rulebase, layer_name, parent_uid, gateway, policy_structure)
            parse_single_rule(  # do not increase rule_num here (xlate rules do not count)
                rule_xlate, target_rulebase, layer_name, parent_uid, gateway, policy_structure
            )


def parse_nat_rule_transform(xlate_rule_in: dict[str, Any]) -> tuple[dict[str, Any], dict[str, Any]]:
    # TODO: cleanup certain fields (install-on, ....)
    rule_match = {
        "uid": xlate_rule_in["uid"],
        "source": [xlate_rule_in["original-source"]],
        "destination": [xlate_rule_in["original-destination"]],
        "service": [xlate_rule_in["original-service"]],
        "action": {"name": "Drop"},
        "track": {"type": {"name": "None"}},
        "type": "nat",
        "rule-number": 0,
        "source-negate": False,
        "destination-negate": False,
        "service-negate": False,
        "install-on": [{"name": "Policy Targets"}],
        "time": [{"name": "Any"}],
        "enabled": xlate_rule_in["enabled"],
        "comments": xlate_rule_in["comments"],
        "rule_type": "access",
    }
    rule_xlate = {
        "uid": xlate_rule_in["uid"],
        "source": [xlate_rule_in["translated-source"]],
        "destination": [xlate_rule_in["translated-destination"]],
        "service": [xlate_rule_in["translated-service"]],
        "action": {"name": "Drop"},
        "track": {"type": {"name": "None"}},
        "type": "nat",
        "rule-number": 0,
        "enabled": True,
        "source-negate": False,
        "destination-negate": False,
        "service-negate": False,
        "install-on": [{"name": "Policy Targets"}],
        "time": [{"name": "Any"}],
        "rule_type": "nat",
    }
    return (rule_match, rule_xlate)

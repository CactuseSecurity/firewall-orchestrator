from typing import Any

from fw_modules.checkpointR8x.cp_rule import parse_single_rule
from fwo_log import FWOLogger
from models.import_state import ImportState
from models.rulebase import Rulebase


def normalize_nat_rules(
    native_config: dict[str, Any],
    import_state: ImportState,
    normalized_config: dict[str, Any],
):
    native_nat_rulebases = native_config.get("nat_rulebases", [])
    if not native_nat_rulebases:
        return
    for gateway in native_config["gateways"]:
        for nat_rulebase in native_nat_rulebases:
            if "nat_rule_chunks" not in nat_rulebase:
                continue

            normalized_nat_rulebase = Rulebase(
                uid="nat-rulebase-" + gateway["uid"],
                mgm_uid=import_state.mgm_details.uid,
                name="NAT",
                rules={},
            )

            if not any(rb for rb in normalized_config["policies"] if rb.uid == normalized_nat_rulebase.uid):
                normalized_config["policies"].append(normalized_nat_rulebase)

            normalized_gateway = next((gw for gw in normalized_config["gateways"] if gw["Uid"] == gateway["uid"]), None)

            if normalized_gateway is None:
                FWOLogger.warning(
                    "Could not find normalized gateway for NAT rulebase, skipping: " + str(gateway["uid"])
                )
                continue

            if not any(
                link
                for link in normalized_gateway["RulebaseLinks"]
                if link["to_rulebase_uid"] == normalized_nat_rulebase.uid
                and link["link_type"] == "nat"
                and link["from_rulebase_uid"] == normalized_gateway["RulebaseLinks"][0]["to_rulebase_uid"]
            ):
                normalized_gateway["RulebaseLinks"].append(
                    {
                        "from_rulebase_uid": normalized_gateway["RulebaseLinks"][0]["to_rulebase_uid"],
                        "to_rulebase_uid": normalized_nat_rulebase.uid,
                        "link_type": "ordered",
                        "is_initial": False,
                        "is_global": False,
                        "is_section": False,
                    }
                )

            for chunk in nat_rulebase["nat_rule_chunks"]:
                if "rulebase" not in chunk:
                    continue
                for src_rulebase in chunk["rulebase"]:
                    if "rulebase" in src_rulebase:
                        section_rulebase = Rulebase(
                            uid=src_rulebase["uid"],
                            mgm_uid=import_state.mgm_details.uid,
                            name=src_rulebase["name"],
                            rules={},
                        )

                        if not any(rb for rb in normalized_config["policies"] if rb.uid == section_rulebase.uid):
                            normalized_config["policies"].append(section_rulebase)

                        if not any(
                            link
                            for link in normalized_gateway["RulebaseLinks"]
                            if link["to_rulebase_uid"] == section_rulebase.uid
                            and link["link_type"] == "nat"
                            and link["from_rulebase_uid"] == normalized_nat_rulebase.uid
                        ):
                            normalized_gateway["RulebaseLinks"].append(
                                {
                                    "from_rulebase_uid": normalized_nat_rulebase.uid,
                                    "to_rulebase_uid": section_rulebase.uid,
                                    "link_type": "concatenated",
                                    "is_initial": False,
                                    "is_global": False,
                                    "is_section": False,
                                }
                            )

                        for rule in src_rulebase["rulebase"]:
                            (rule_match, rule_xlate) = parse_nat_rule_transform(rule)
                            parse_single_rule(
                                rule_match,
                                section_rulebase,
                                section_rulebase.name,
                                section_rulebase.uid,
                                gateway,
                                native_config["policies"],
                            )
                            parse_single_rule(  # do not increase rule_num here
                                rule_xlate,
                                section_rulebase,
                                section_rulebase.name,
                                section_rulebase.uid,
                                gateway,
                                native_config["policies"],
                            )

                    if "rule-number" in src_rulebase:  # rulebase is just a single rule (xlate rules do not count)
                        (rule_match, rule_xlate) = parse_nat_rule_transform(src_rulebase)
                        parse_single_rule(
                            rule_match,
                            normalized_nat_rulebase,
                            normalized_nat_rulebase.name,
                            normalized_nat_rulebase.uid,
                            gateway,
                            native_config["policies"],
                        )
                        parse_single_rule(  # do not increase rule_num here (xlate rules do not count)
                            rule_xlate,
                            normalized_nat_rulebase,
                            normalized_nat_rulebase.name,
                            normalized_nat_rulebase.uid,
                            gateway,
                            native_config["policies"],
                        )


def parse_nat_rule_transform(nat_rule: dict[str, Any]) -> tuple[dict[str, Any], dict[str, Any]]:
    nat_in_rule = {
        "uid": nat_rule["uid"],
        "source": [nat_rule["original-source"]],
        "destination": [nat_rule["original-destination"]],
        "service": [nat_rule["original-service"]],
        "action": [{"name": "accept", "type": "nat-action", "uid": nat_rule["uid"] + "_original-action"}],
        "track": [{"type": "nat", "name": "None", "uid": nat_rule["uid"]}],
        "type": "nat",
        "rule-number": 0,
        "source-negate": False,
        "destination-negate": False,
        "service-negate": False,
        "install-on": nat_rule["install-on"],
        "time": "",
        "enabled": nat_rule["enabled"],
        "comments": nat_rule["comments"],
        "nat_rule": True,
        "xlate_rule_uid": nat_rule["uid"] + "_translated",
        "access_rule": False,
    }
    nat_out_rule = {
        "uid": nat_rule["uid"] + "_translated",
        "source": [nat_rule["translated-source"]],
        "destination": [nat_rule["translated-destination"]],
        "service": [nat_rule["translated-service"]],
        "action": [{"name": "accept", "type": "nat-action", "uid": nat_rule["uid"] + "_translated-action"}],
        "track": [{"type": "nat", "name": "None", "uid": nat_rule["uid"] + "_translated"}],
        "type": "nat",
        "rule-number": 0,
        "enabled": True,
        "source-negate": False,
        "destination-negate": False,
        "service-negate": False,
        "install-on": nat_rule["install-on"],
        "time": "",
        "nat_rule": True,
        "access_rule": False,
    }
    return (nat_in_rule, nat_out_rule)

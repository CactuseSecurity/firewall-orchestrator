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
        parse_native_nat_rulebases(gateway, native_nat_rulebases, import_state, normalized_config, native_config)


def get_initial_nat_rulebase_link(gateway: dict[str, Any], normalized_config: dict[str, Any]) -> dict[str, Any] | None:
    normalized_gateway = next((gw for gw in normalized_config["gateways"] if gw["Uid"] == gateway["uid"]), None)

    if normalized_gateway is None:
        FWOLogger.warning("Could not find normalized gateway for initial NAT rulebase link: " + str(gateway["uid"]))
        return None

    initial_gateway_link = next(
        (
            link
            for link in normalized_gateway["RulebaseLinks"]
            if link.get("is_initial") and link.get("link_type") == "ordered"
        ),
        None,
    )

    if initial_gateway_link is None:
        FWOLogger.warning("Could not find initial gateway rulebase link for NAT rulebase: " + str(gateway["uid"]))
        return None

    return initial_gateway_link


def parse_native_nat_rulebases(
    gateway: dict[str, Any],
    native_nat_rulebases: list[dict[str, Any]],
    import_state: ImportState,
    normalized_config: dict[str, Any],
    native_config: dict[str, Any],
):
    for nat_rulebase in native_nat_rulebases:
        if "nat_rule_chunks" not in nat_rulebase:
            continue

        normalized_nat_rulebase = insert_parent_nat_rulebase(gateway, import_state, normalized_config)
        normalized_gateway = next((gw for gw in normalized_config["gateways"] if gw["Uid"] == gateway["uid"]), None)

        if normalized_gateway is None:
            FWOLogger.warning("Could not find normalized gateway for NAT rulebase, skipping: " + str(gateway["uid"]))
            continue

        initial_gateway_link = get_initial_nat_rulebase_link(gateway, normalized_config)

        if initial_gateway_link is None:
            continue

        initial_to_rulebase_uid = initial_gateway_link.get("to_rulebase_uid")
        if not initial_to_rulebase_uid:
            FWOLogger.warning(
                "Initial gateway rulebase link is missing to_rulebase_uid for NAT rulebase, skipping: "
                + str(gateway["uid"])
            )
            continue

        insert_rulebase_link(
            from_rulebase_uid=initial_to_rulebase_uid,
            to_rulebase_uid=normalized_nat_rulebase.uid,
            link_type="nat",
            normalized_gateway=normalized_gateway,
        )

        for chunk in nat_rulebase["nat_rule_chunks"]:
            parse_nat_rule_chunk(
                chunk,
                normalized_nat_rulebase,
                gateway,
                native_config,
                import_state,
                normalized_config,
                normalized_gateway,
            )


def insert_parent_nat_rulebase(
    gateway: dict[str, Any],
    import_state: ImportState,
    normalized_config: dict[str, Any],
) -> Rulebase:
    normalized_nat_rulebase = Rulebase(
        uid="nat-rulebase-" + gateway["uid"],
        mgm_uid=import_state.mgm_details.uid,
        name="NAT",
        rules={},
    )

    if not any(rb for rb in normalized_config["policies"] if rb.uid == normalized_nat_rulebase.uid):
        normalized_config["policies"].append(normalized_nat_rulebase)

    return normalized_nat_rulebase


def insert_rulebase_link(
    from_rulebase_uid: str,
    to_rulebase_uid: str,
    link_type: str,
    normalized_gateway: dict[str, Any],
) -> None:
    if not any(
        link
        for link in normalized_gateway["RulebaseLinks"]
        if link["to_rulebase_uid"] == to_rulebase_uid
        and link["link_type"] == link_type
        and link["from_rulebase_uid"] == from_rulebase_uid
    ):
        normalized_gateway["RulebaseLinks"].append(
            {
                "from_rulebase_uid": from_rulebase_uid,
                "to_rulebase_uid": to_rulebase_uid,
                "link_type": link_type,
                "is_initial": False,
                "is_global": False,
                "is_section": False,
            }
        )


def parse_nat_rulebase(
    src_rulebase: dict[str, Any],
    normalized_nat_rulebase: Rulebase,
    gateway: dict[str, Any],
    native_config: dict[str, Any],
    import_state: ImportState,
    normalized_config: dict[str, Any],
    normalized_gateway: dict[str, Any],
):
    section_rulebase = Rulebase(
        uid=src_rulebase["uid"],
        mgm_uid=import_state.mgm_details.uid,
        name=src_rulebase["name"],
        rules={},
    )

    if not any(rb for rb in normalized_config["policies"] if rb.uid == section_rulebase.uid):
        normalized_config["policies"].append(section_rulebase)

    insert_rulebase_link(
        from_rulebase_uid=normalized_nat_rulebase.uid,
        to_rulebase_uid=section_rulebase.uid,
        link_type="nat",
        normalized_gateway=normalized_gateway,
    )

    for rule in src_rulebase["rulebase"]:
        parse_nat_rule(rule, section_rulebase, gateway, native_config)


def parse_nat_rule(
    src_rulebase: dict[str, Any],
    rulebase: Rulebase,
    gateway: dict[str, Any],
    native_config: dict[str, Any],
):
    (rule_match, rule_xlate) = parse_nat_rule_transform(src_rulebase)
    parse_single_rule(
        rule_match,
        rulebase,
        rulebase.name,
        rulebase.uid,
        gateway,
        native_config["policies"],
    )
    parse_single_rule(  # do not increase rule_num here (xlate rules do not count)
        rule_xlate,
        rulebase,
        rulebase.name,
        rulebase.uid,
        gateway,
        native_config["policies"],
    )


def parse_nat_rule_chunk(
    chunk: dict[str, Any],
    normalized_nat_rulebase: Rulebase,
    gateway: dict[str, Any],
    native_config: dict[str, Any],
    import_state: ImportState,
    normalized_config: dict[str, Any],
    normalized_gateway: dict[str, Any],
):
    if "rulebase" not in chunk:
        return

    for src_rulebase in chunk["rulebase"]:
        if "rulebase" in src_rulebase:
            parse_nat_rulebase(
                src_rulebase,
                normalized_nat_rulebase,
                gateway,
                native_config,
                import_state,
                normalized_config,
                normalized_gateway,
            )
        if "rule-number" in src_rulebase:  # rulebase is just a single rule (xlate rules do not count)
            parse_nat_rule(src_rulebase, normalized_nat_rulebase, gateway, native_config)


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

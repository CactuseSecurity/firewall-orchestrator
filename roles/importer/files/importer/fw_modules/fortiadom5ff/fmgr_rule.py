import copy
import ipaddress
import json
from datetime import datetime, timezone
from typing import Any

from fw_modules.fortiadom5ff import fmgr_getter
from fw_modules.fortiadom5ff.fmgr_consts import EXPECTED_NATIP_LIST_LENGTH, nat_types
from fw_modules.fortiadom5ff.fmgr_network import create_network_object
from fw_modules.fortiadom5ff.fmgr_service import create_svc_object
from fw_modules.fortiadom5ff.fmgr_zone import find_zones_in_normalized_config
from fwo_const import ANY_IP_END, ANY_IP_START, LIST_DELIMITER
from fwo_exceptions import (
    FwoDeviceWithoutLocalPackageError,
    FwoImporterErrorInconsistenciesError,
)
from fwo_log import FWOLogger
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType
from models.rulebase import Rulebase
from netaddr import IPNetwork

NETWORK_OBJECT = "network_object"
STRING_PKG = "/pkg/"
STRING_PM_CONFIG_GLOBAL_PKG = "/pm/config/global/pkg/"
STRING_PM_CONFIG_ADOM = "/pm/config/adom/"
rule_access_scope_v4 = [
    "rules_global_header_v4",
    "rules_adom_v4",
    "rules_global_footer_v4",
]
rule_access_scope_v6 = [
    "rules_global_header_v6",
    "rules_adom_v6",
    "rules_global_footer_v6",
]
rule_access_scope = rule_access_scope_v6 + rule_access_scope_v4
rule_nat_scope = ["rules_global_nat", "rules_adom_nat"]
rule_scope = rule_access_scope + rule_nat_scope
ip_v4_type = 4
ip_v6_type = 6


def normalize_rulebases(
    mgm_uid: str,
    native_config: dict[str, Any],
    native_config_global: dict[str, Any],
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    is_global_loop_iteration: bool,
) -> None:
    normalized_config_adom["policies"] = []
    fetched_rulebase_uids: list[str] = []
    if normalized_config_global != {}:
        fetched_rulebase_uids = [
            normalized_rulebase_global.uid
            for normalized_rulebase_global in normalized_config_global.get("policies", [])
        ]
    for gateway in native_config["gateways"]:
        normalize_rulebases_for_each_link_destination(
            gateway,
            mgm_uid,
            fetched_rulebase_uids,
            native_config,
            native_config_global,
            is_global_loop_iteration,
            normalized_config_adom,
            normalized_config_global,
        )


def normalize_rulebases_for_each_link_destination(
    gateway: dict[str, Any],
    mgm_uid: str,
    fetched_rulebase_uids: list[str],
    native_config: dict[str, Any],
    native_config_global: dict[str, Any],
    is_global_loop_iteration: bool,
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
):
    # Iterate over a snapshot because we may append NAT links while processing.
    for rulebase_link in list(gateway["rulebase_links"]):
        if _should_skip_rulebase_link(rulebase_link, fetched_rulebase_uids):
            normalize_nat_rulebase(rulebase_link, native_config, normalized_config_adom, normalized_config_global)
            continue

        rulebase_to_parse, found_rulebase_in_global = _find_rulebase_to_parse_for_link(
            rulebase_link,
            native_config,
            native_config_global,
            is_global_loop_iteration,
        )
        if rulebase_to_parse == {}:
            FWOLogger.warning("found to_rulebase link without rulebase in nativeConfig: " + str(rulebase_link))
            continue

        normalized_rulebase = initialize_normalized_rulebase(rulebase_to_parse, mgm_uid)
        parse_rulebase(
            normalized_config_adom,
            normalized_config_global,
            rulebase_to_parse,
            normalized_rulebase,
            found_rulebase_in_global,
        )
        fetched_rulebase_uids.append(rulebase_link["to_rulebase_uid"])
        _append_normalized_rulebase(
            normalized_config_adom,
            normalized_config_global,
            normalized_rulebase,
            found_rulebase_in_global,
        )

        new_process_nat_rules_for_rulebase(
            gateway,
            normalized_config_adom,
            normalized_config_global,
            rulebase_to_parse,
            normalized_rulebase,
        )

        # normalizing nat rulebases is work in progress
        normalize_nat_rulebase(rulebase_link, native_config, normalized_config_adom, normalized_config_global)


def _should_skip_rulebase_link(rulebase_link: dict[str, Any], fetched_rulebase_uids: list[str]) -> bool:
    link_type = rulebase_link.get("link_type", rulebase_link.get("type", "ordered"))
    if link_type == "nat":
        # NAT links are generated during normalization and do not exist in native rulebases.
        return True
    return not (
        rulebase_link["to_rulebase_uid"] not in fetched_rulebase_uids and rulebase_link["to_rulebase_uid"] != ""
    )


def _find_rulebase_to_parse_for_link(
    rulebase_link: dict[str, Any],
    native_config: dict[str, Any],
    native_config_global: dict[str, Any],
    is_global_loop_iteration: bool,
) -> tuple[dict[str, Any], bool]:
    rulebase_to_parse = find_rulebase_to_parse(native_config["rulebases"], rulebase_link["to_rulebase_uid"])
    found_rulebase_in_global = False
    if rulebase_to_parse == {} and not is_global_loop_iteration and native_config_global != {}:
        rulebase_to_parse = find_rulebase_to_parse(native_config_global["rulebases"], rulebase_link["to_rulebase_uid"])
        found_rulebase_in_global = True
    return rulebase_to_parse, found_rulebase_in_global


def find_rulebase_to_parse(rulebase_list: list[dict[str, Any]], rulebase_uid: str) -> dict[str, Any]:
    for rulebase in rulebase_list:
        if rulebase["uid"] == rulebase_uid:
            return rulebase
    return {}


def _append_normalized_rulebase(
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    normalized_rulebase: Rulebase,
    found_rulebase_in_global: bool,
) -> None:
    if found_rulebase_in_global:
        normalized_config_global["policies"].append(normalized_rulebase)
    else:
        normalized_config_adom["policies"].append(normalized_rulebase)


def initialize_normalized_rulebase(rulebase_to_parse: dict[str, Any], mgm_uid: str) -> Rulebase:
    """
    We use 'type' as uid/name since a rulebase may have a v4 and a v6 part
    """
    rulebase_name = rulebase_to_parse["type"]
    rulebase_uid = rulebase_to_parse["type"]
    return Rulebase(uid=rulebase_uid, name=rulebase_name, mgm_uid=mgm_uid, rules={})


def parse_rulebase(
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    rulebase_to_parse: dict[str, Any],
    normalized_rulebase: Rulebase,
    found_rulebase_in_global: bool,
):
    """Parses a native Fortinet rulebase into a normalized rulebase."""
    for native_rule in rulebase_to_parse["data"]:
        parse_single_rule(
            normalized_config_adom,
            normalized_config_global,
            native_rule,
            normalized_rulebase,
        )
    if not found_rulebase_in_global:
        add_implicit_deny_rule(normalized_config_adom, normalized_config_global, normalized_rulebase)


def add_implicit_deny_rule(
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    rulebase: Rulebase,
):
    deny_rule = {
        "srcaddr": ["all"],
        "srcaddr6": ["all"],
        "dstaddr": ["all"],
        "dstaddr6": ["all"],
        "service": ["ALL"],
        "srcintf": ["any"],
        "dstintf": ["any"],
    }

    rule_src_list, rule_src_refs_list = rule_parse_addresses(
        deny_rule, "src", normalized_config_adom, normalized_config_global, is_nat=False
    )
    rule_dst_list, rule_dst_refs_list = rule_parse_addresses(
        deny_rule, "dst", normalized_config_adom, normalized_config_global, is_nat=False
    )
    rule_svc_list, rule_svc_refs_list = rule_parse_service(deny_rule)
    rule_src_zones = find_zones_in_normalized_config(
        deny_rule.get("srcintf", []), normalized_config_adom, normalized_config_global
    )
    rule_dst_zones = find_zones_in_normalized_config(
        deny_rule.get("dstintf", []), normalized_config_adom, normalized_config_global
    )

    rule_normalized = RuleNormalized(
        rule_num=0,
        rule_num_numeric=0,
        rule_disabled=False,
        rule_src_neg=False,
        rule_src=LIST_DELIMITER.join(rule_src_list),
        rule_src_refs=LIST_DELIMITER.join(rule_src_refs_list),
        rule_dst_neg=False,
        rule_dst=LIST_DELIMITER.join(rule_dst_list),
        rule_dst_refs=LIST_DELIMITER.join(rule_dst_refs_list),
        rule_svc_neg=False,
        rule_svc=LIST_DELIMITER.join(rule_svc_list),
        rule_svc_refs=LIST_DELIMITER.join(rule_svc_refs_list),
        rule_action=RuleAction.DROP,
        rule_track=RuleTrack.NONE,  # I guess this could also have different values
        rule_installon=None,
        rule_time=None,  # Time-based rules not commonly used in basic Fortinet configs
        rule_name="Implicit Deny",
        rule_uid=f"{rulebase.uid}_implicit_deny",
        rule_custom_fields=str({}),
        rule_implied=True,
        rule_type=RuleType.ACCESS,
        last_change_admin=None,
        parent_rule_uid=None,
        last_hit=None,
        rule_comment=None,
        rule_src_zone=LIST_DELIMITER.join(rule_src_zones),
        rule_dst_zone=LIST_DELIMITER.join(rule_dst_zones),
        rule_head_text=None,
    )

    if rule_normalized.rule_uid is None:
        raise FwoImporterErrorInconsistenciesError("rule_normalized.rule_uid is None when adding implicit deny rule")
    rulebase.rules[rule_normalized.rule_uid] = rule_normalized


def parse_single_rule(
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    native_rule: dict[str, Any],
    rulebase: Rulebase,
):
    """Parses a single native Fortinet rule into a normalized rule and adds it to the given rulebase."""
    is_nat_rule = any(key in native_rule and native_rule[key] == 1 for key in ["nat", "nat46", "nat64"])

    # Extract basic rule information
    rule_disabled = True  # Default to disabled
    if "status" in native_rule and (native_rule["status"] == 1 or native_rule["status"] == "enable"):
        rule_disabled = False

    rule_action = rule_parse_action(native_rule)

    rule_track = rule_parse_tracking_info(native_rule)

    rule_src_list, rule_src_refs_list = rule_parse_addresses(
        native_rule,
        "src",
        normalized_config_adom,
        normalized_config_global,
        is_nat=is_nat_rule,
    )
    rule_dst_list, rule_dst_refs_list = rule_parse_addresses(
        native_rule,
        "dst",
        normalized_config_adom,
        normalized_config_global,
        is_nat=is_nat_rule,
    )

    rule_svc_list, rule_svc_refs_list = rule_parse_service(native_rule)

    rule_src_zones = find_zones_in_normalized_config(
        native_rule.get("srcintf", []), normalized_config_adom, normalized_config_global
    )
    rule_dst_zones = find_zones_in_normalized_config(
        native_rule.get("dstintf", []), normalized_config_adom, normalized_config_global
    )

    rule_src_neg, rule_dst_neg, rule_svc_neg = rule_parse_negation_flags(native_rule)
    rule_installon = rule_parse_installon(native_rule)

    last_hit = rule_parse_last_hit(native_rule)

    time = rule_parse_time(native_rule)

    # Create the normalized access rule
    rule_normalized = RuleNormalized(
        rule_num=0,
        rule_num_numeric=0,
        rule_disabled=rule_disabled,
        rule_src_neg=rule_src_neg,
        rule_src=LIST_DELIMITER.join(rule_src_list),
        rule_src_refs=LIST_DELIMITER.join(rule_src_refs_list),
        rule_dst_neg=rule_dst_neg,
        rule_dst=LIST_DELIMITER.join(rule_dst_list),
        rule_dst_refs=LIST_DELIMITER.join(rule_dst_refs_list),
        rule_svc_neg=rule_svc_neg,
        rule_svc=LIST_DELIMITER.join(rule_svc_list),
        rule_svc_refs=LIST_DELIMITER.join(rule_svc_refs_list),
        rule_action=rule_action,
        rule_track=rule_track,
        rule_installon=rule_installon,
        rule_time=time,
        rule_name=native_rule.get("name"),
        rule_uid=native_rule.get("uuid"),
        rule_custom_fields=str(native_rule.get("meta fields", {})),
        rule_implied=False,
        rule_type=RuleType.ACCESS,
        last_change_admin=None,  # native_rule.get('_last-modified-by', ''), not handled yet -> leave out to prevent mismatches
        parent_rule_uid=None,
        last_hit=last_hit,
        rule_comment=native_rule.get("comments"),
        rule_src_zone=LIST_DELIMITER.join(rule_src_zones),
        rule_dst_zone=LIST_DELIMITER.join(rule_dst_zones),
        rule_head_text=None,
        access_rule=True,
        nat_rule=False,
    )

    if rule_normalized.rule_uid is None:
        raise FwoImporterErrorInconsistenciesError("rule_normalized.rule_uid is None when parsing single rule")

    # Add the rule to the rulebase
    rulebase.rules[rule_normalized.rule_uid] = rule_normalized


def rule_parse_action(native_rule: dict[str, Any]) -> RuleAction:
    # Extract action - Fortinet uses 0 for deny/drop, 1 for accept
    if native_rule.get("action", 0) == 0:
        return RuleAction.DROP
    return RuleAction.ACCEPT


def rule_parse_tracking_info(native_rule: dict[str, Any]) -> RuleTrack:
    # TODO: Implement more detailed logging level extraction (difference between 1/2/3?)
    logtraffic = native_rule.get("logtraffic", 0)
    if (isinstance(logtraffic, int) and logtraffic > 0) or (isinstance(logtraffic, str) and logtraffic != "disable"):
        return RuleTrack.LOG
    return RuleTrack.NONE


def rule_parse_service(native_rule: dict[str, Any]) -> tuple[list[str], list[str]]:
    """
    Parses services to ordered (!) name list and reference list.
    """
    rule_svc_list: list[str] = []
    rule_svc_refs_list: list[str] = []
    for svc in sorted(native_rule.get("service", [])):
        rule_svc_list.append(svc)
        rule_svc_refs_list.append(svc)
    if rule_svc_list == [] and "internet-service-name" in native_rule and len(native_rule["internet-service-name"]) > 0:
        rule_svc_list.append("ALL")
        rule_svc_refs_list.append("ALL")
    if (
        rule_svc_list == []
        and "internet-service-src-name" in native_rule
        and len(native_rule["internet-service-src-name"]) > 0
    ):
        rule_svc_list.append("ALL")
        rule_svc_refs_list.append("ALL")

    return rule_svc_list, rule_svc_refs_list


def rule_parse_addresses(
    native_rule: dict[str, Any],
    target: str,
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    is_nat: bool,
) -> tuple[list[str], list[str]]:
    """
    Parses addresses to ordered (!) name list and reference list for source or destination addresses.
    """
    if target not in ["src", "dst"]:
        raise FwoImporterErrorInconsistenciesError(f"target '{target}' must either be src or dst.")
    addr_list: list[str] = []
    addr_ref_list: list[str] = []
    if not is_nat:
        build_addr_list(
            native_rule,
            target,
            normalized_config_adom,
            normalized_config_global,
            addr_list,
            addr_ref_list,
            is_v4=True,
        )
        build_addr_list(
            native_rule,
            target,
            normalized_config_adom,
            normalized_config_global,
            addr_list,
            addr_ref_list,
            is_v4=False,
        )
    else:
        build_nat_addr_list(
            native_rule,
            target,
            normalized_config_adom,
            normalized_config_global,
            addr_list,
            addr_ref_list,
        )
    return addr_list, addr_ref_list


def build_addr_list(
    native_rule: dict[str, Any],
    target: str,
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    addr_list: list[str],
    addr_ref_list: list[str],
    is_v4: bool,
) -> None:
    """
    Builds ordered (!) address list and address reference list for source or destination addresses.
    """
    if is_v4 and target == "src":
        for addr in sorted(native_rule.get("srcaddr", [])) + sorted(native_rule.get("internet-service-src-name", [])):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global))
    elif not is_v4 and target == "src":
        for addr in sorted(native_rule.get("srcaddr6", [])):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global))
    elif is_v4 and target == "dst":
        for addr in sorted(native_rule.get("dstaddr", [])) + sorted(native_rule.get("internet-service-name", [])):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global))
    else:
        for addr in sorted(native_rule.get("dstaddr6", [])):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global))


def build_nat_addr_list(
    native_rule: dict[str, Any],
    target: str,
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    addr_list: list[str],
    addr_ref_list: list[str],
) -> None:
    is_ipv6 = bool(native_rule.get("srcaddr6") or native_rule.get("dstaddr6"))
    if target == "src":
        source_addrs = native_rule.get("srcaddr6", []) if is_ipv6 else native_rule.get("srcaddr", [])
        for addr in sorted(source_addrs):
            addr_list.append(addr)
            addr_ref_list.append(
                find_addr_ref(
                    addr,
                    is_v4=not is_ipv6,
                    normalized_config_adom=normalized_config_adom,
                    normalized_config_global=normalized_config_global,
                )
            )
    if target == "dst":
        destination_addrs = native_rule.get("dstaddr6", []) if is_ipv6 else native_rule.get("dstaddr", [])
        for addr in sorted(destination_addrs):
            addr_list.append(addr)
            addr_ref_list.append(
                find_addr_ref(
                    addr,
                    is_v4=not is_ipv6,
                    normalized_config_adom=normalized_config_adom,
                    normalized_config_global=normalized_config_global,
                )
            )


def ensure_original_objects(normalized_config_adom: dict[str, Any], normalized_config_global: dict[str, Any]) -> None:
    """
    Ensure that a standard 'Original' network and service object exist in the normalized config.
    If missing, create minimal placeholder objects in the ADOM normalized config.
    """
    # Ensure lists exist
    normalized_config_adom.setdefault("network_objects", [])
    normalized_config_adom.setdefault("service_objects", [])

    # Check network objects in ADOM and global
    combined_nw = normalized_config_adom["network_objects"] + normalized_config_global.get("network_objects", [])
    if not any(obj.get("obj_name") == "Original" for obj in combined_nw):
        normalized_config_adom["network_objects"].append(
            create_network_object(
                name="Original",
                obj_type="network",
                ip=ANY_IP_START,
                ip_end=ANY_IP_END,
                uid="Original",
                color="black",
                comment='"original" network object created by FWO importer for NAT purposes',
                zone="global",
            )
        )

    # Check service objects in ADOM and global
    combined_svc = normalized_config_adom["service_objects"] + normalized_config_global.get("service_objects", [])
    if not any(svc.get("svc_name") == "Original" for svc in combined_svc):
        normalized_config_adom["service_objects"].append(
            create_svc_object(
                name="Original",
                proto=0,
                color="foreground",
                port=None,
                comment='"original" service object created by FWO importer for NAT purposes',
            )
        )

    if not any(obj.get("obj_name") == "Outgoing Interface IP" for obj in combined_nw):
        normalized_config_adom["network_objects"].append(
            create_network_object(
                name="Outgoing Interface IP",
                obj_type="network",
                ip=ANY_IP_START,
                ip_end=ANY_IP_END,
                uid="Outgoing_Interface_IP",
                color="black",
                comment='"Outgoing Interface IP" network object created by FWO importer for NAT purposes',
                zone="global",
            )
        )


def find_addr_ref(
    addr: str,
    is_v4: bool,
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
) -> str:
    for nw_obj in normalized_config_adom["network_objects"] + normalized_config_global.get("network_objects", []):
        if addr == nw_obj["obj_name"] and (
            (is_v4 and ip_type(nw_obj) == ip_v4_type) or (not is_v4 and ip_type(nw_obj) == ip_v6_type)
        ):
            return nw_obj["obj_uid"]
    raise FwoImporterErrorInconsistenciesError(f"No ref found for '{addr}'.")


def ip_type(nw_obj: dict[str, Any]) -> int:
    # default to v4
    first_ip = nw_obj.get("obj_ip", "0.0.0.0/32")
    if first_ip == "":
        first_ip = "0.0.0.0/32"
    net = ipaddress.ip_network(str(first_ip))
    return net.version


def rule_parse_negation_flags(native_rule: dict[str, Any]) -> tuple[bool, bool, bool]:
    # if customer decides to mix internet-service and "normal" addr obj in src/dst and mix negates this will prob. not work correctly
    if "srcaddr-negate" in native_rule:
        rule_src_neg = native_rule["srcaddr-negate"] == 1 or native_rule["srcaddr-negate"] == "disable"
    elif "internet-service-src-negate" in native_rule:
        rule_src_neg = (
            native_rule["internet-service-src-negate"] == 1 or native_rule["internet-service-src-negate"] == "disable"
        )
    else:
        rule_src_neg = False
    rule_dst_neg = "dstaddr-negate" in native_rule and (
        native_rule["dstaddr-negate"] == 1 or native_rule["dstaddr-negate"] == "disable"
    )  # TODO: last part does not make sense?
    rule_svc_neg = "service-negate" in native_rule and (
        native_rule["service-negate"] == 1 or native_rule["service-negate"] == "disable"
    )
    return rule_src_neg, rule_dst_neg, rule_svc_neg


def rule_parse_installon(native_rule: dict[str, Any]) -> str | None:
    rule_installon = None
    if native_rule.get("scope_member"):
        rule_installon = LIST_DELIMITER.join(
            sorted({vdom["name"] + "_" + vdom["vdom"] for vdom in native_rule["scope_member"]})
        )
    return rule_installon


def rule_parse_last_hit(native_rule: dict[str, Any]) -> str | None:
    last_hit = native_rule.get("_last_hit")
    if last_hit is not None:
        # FortiManager reports epoch seconds; preserve the local offset in the serialized value.
        last_hit = datetime.fromtimestamp(float(last_hit), tz=timezone.utc).astimezone().isoformat(timespec="seconds")
    return last_hit


def rule_parse_time(native_rule: dict[str, Any]) -> str | None:
    schedule: list[str] | None = native_rule.get("schedule")

    if schedule is None:
        return None

    return "|".join(schedule)


def get_access_policy(
    sid: str,
    fm_api_url: str,
    native_config_adom: dict[str, Any],
    native_config_global: dict[str, Any],
    adom_device_vdom_policy_package_structure: dict[str, Any],
    adom_name: str,
    mgm_details_device: dict[str, Any],
    device_config: dict[str, Any],
    limit: int,
):
    previous_rulebase = None
    link_list: list[Any] = []
    local_pkg_name, global_pkg_name = find_packages(
        adom_device_vdom_policy_package_structure, adom_name, mgm_details_device
    )
    options = ["extra info", "scope member", "get meta"]

    previous_rulebase = get_and_link_global_rulebase(
        "header",
        previous_rulebase,
        global_pkg_name,
        native_config_global,
        sid,
        fm_api_url,
        options,
        limit,
        link_list,
    )

    previous_rulebase = get_and_link_local_rulebase(
        "rules_adom",
        previous_rulebase,
        adom_name,
        local_pkg_name,
        native_config_adom,
        sid,
        fm_api_url,
        options,
        limit,
        link_list,
    )

    previous_rulebase = get_and_link_global_rulebase(
        "footer",
        previous_rulebase,
        global_pkg_name,
        native_config_global,
        sid,
        fm_api_url,
        options,
        limit,
        link_list,
    )

    device_config["rulebase_links"].extend(link_list)


def get_and_link_global_rulebase(
    header_or_footer: str,
    previous_rulebase: str | None,
    global_pkg_name: str,
    native_config_global: dict[str, Any],
    sid: str,
    fm_api_url: str,
    options: list[str],
    limit: int,
    link_list: list[Any],
) -> Any:
    rulebase_type_prefix = "rules_global_" + header_or_footer
    if global_pkg_name != "":
        if not is_rulebase_already_fetched(
            native_config_global["rulebases"],
            rulebase_type_prefix + "_v4_" + global_pkg_name,
        ):
            fmgr_getter.update_config_with_fortinet_api_call(
                native_config_global["rulebases"],
                sid,
                fm_api_url,
                STRING_PM_CONFIG_GLOBAL_PKG + global_pkg_name + "/global/" + header_or_footer + "/policy",
                rulebase_type_prefix + "_v4_" + global_pkg_name,
                options=options,
                limit=limit,
            )
        if not is_rulebase_already_fetched(
            native_config_global["rulebases"],
            rulebase_type_prefix + "_v6_" + global_pkg_name,
        ):
            # delete_v: hier auch options=options?
            fmgr_getter.update_config_with_fortinet_api_call(
                native_config_global["rulebases"],
                sid,
                fm_api_url,
                STRING_PM_CONFIG_GLOBAL_PKG + global_pkg_name + "/global/" + header_or_footer + "/policy6",
                rulebase_type_prefix + "_v6_" + global_pkg_name,
                limit=limit,
            )
        previous_rulebase = link_rulebase(
            link_list,
            native_config_global["rulebases"],
            global_pkg_name,
            rulebase_type_prefix,
            previous_rulebase,
            is_global=True,
        )
    return previous_rulebase


def get_and_link_local_rulebase(
    rulebase_type_prefix: str,
    previous_rulebase: str | None,
    adom_name: str,
    local_pkg_name: str,
    native_config_adom: dict[str, Any],
    sid: str,
    fm_api_url: str,
    options: list[str],
    limit: int,
    link_list: list[Any],
) -> Any:
    if not is_rulebase_already_fetched(native_config_adom["rulebases"], rulebase_type_prefix + "_v4_" + local_pkg_name):
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_adom["rulebases"],
            sid,
            fm_api_url,
            STRING_PM_CONFIG_ADOM + adom_name + STRING_PKG + local_pkg_name + "/firewall/policy",
            rulebase_type_prefix + "_v4_" + local_pkg_name,
            options=options,
            limit=limit,
        )
    if not is_rulebase_already_fetched(native_config_adom["rulebases"], rulebase_type_prefix + "_v6_" + local_pkg_name):
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_adom["rulebases"],
            sid,
            fm_api_url,
            STRING_PM_CONFIG_ADOM + adom_name + STRING_PKG + local_pkg_name + "/firewall/policy6",
            rulebase_type_prefix + "_v6_" + local_pkg_name,
            limit=limit,
        )
    return link_rulebase(
        link_list,
        native_config_adom["rulebases"],
        local_pkg_name,
        rulebase_type_prefix,
        previous_rulebase,
        is_global=False,
    )


def find_packages(
    adom_device_vdom_policy_package_structure: dict[str, Any],
    adom_name: str,
    mgm_details_device: dict[str, Any],
) -> tuple[str, str]:
    for device in adom_device_vdom_policy_package_structure[adom_name]:
        for vdom in adom_device_vdom_policy_package_structure[adom_name][device]:
            if mgm_details_device["name"] == device + "_" + vdom:
                device_dict = adom_device_vdom_policy_package_structure[adom_name][device]
                if (
                    "local" in device_dict[vdom]
                    and "global" in adom_device_vdom_policy_package_structure[adom_name][device][vdom]
                ):
                    return device_dict[vdom]["local"], adom_device_vdom_policy_package_structure[adom_name][device][
                        vdom
                    ]["global"]
                return "", ""
    raise FwoDeviceWithoutLocalPackageError(
        "Could not find local package for " + mgm_details_device["name"] + " in Fortimanager Config"
    ) from None


def is_rulebase_already_fetched(rulebases: list[dict[str, Any]], typ: str) -> bool:
    return any(rulebase["type"] == typ for rulebase in rulebases)


def link_rulebase(
    link_list: list[Any],
    rulebases: list[dict[str, Any]],
    pkg_name: str,
    rulebase_type_prefix: str,
    previous_rulebase: str | None,
    is_global: bool,
) -> str | None:
    for version in ["v4", "v6"]:
        full_pkg_name = rulebase_type_prefix + "_" + version + "_" + pkg_name
        has_data = has_rulebase_data(rulebases, full_pkg_name, is_global, version, pkg_name)
        if has_data:
            link_list.append(build_link(previous_rulebase, full_pkg_name, is_global))
            previous_rulebase = full_pkg_name

    return previous_rulebase


def build_link(previous_rulebase: str | None, full_pkg_name: str, is_global: bool) -> dict[str, Any]:
    if previous_rulebase is None:
        is_initial = True
        previous_rulebase = None
    else:
        is_initial = False
    return {
        "from_rulebase_uid": previous_rulebase,
        "from_rule_uid": None,
        "to_rulebase_uid": full_pkg_name,
        "type": "ordered",
        "is_global": is_global,
        "is_initial": is_initial,
        "is_section": False,
    }


def has_rulebase_data(
    rulebases: list[dict[str, Any]],
    full_pkg_name: str,
    is_global: bool,
    version: str,
    pkg_name: str,
) -> bool:
    """Adds name and uid to rulebase and removes empty global rulebases"""
    has_data = False
    is_v4 = version == "v4"
    for rulebase in rulebases:
        if rulebase["type"] == full_pkg_name:
            rulebase.update(
                {
                    "name": full_pkg_name,
                    "uid": full_pkg_name,
                    "is_global": is_global,
                    "is_v4": is_v4,
                    "package": pkg_name,
                }
            )
            if len(rulebase["data"]) > 0:
                has_data = True
            elif is_global:
                rulebases.remove(rulebase)
    return has_data


def handle_combined_nat_rule(
    rule: dict[str, Any],
    rule_orig: dict[str, Any],
    config2import: dict[str, Any],
    nat_rule_number: int,
    dev_id: int,
) -> dict[str, Any] | None:
    # TODO: see fOS_rule for reference implementation
    raise NotImplementedError("handle_combined_nat_rule is not implemented yet")


def add_users_to_rule(rule_orig: dict[str, Any], rule: dict[str, Any]) -> None:
    if "groups" in rule_orig:
        add_users(rule_orig["groups"], rule)
    if "users" in rule_orig:
        add_users(rule_orig["users"], rule)


def add_users(users: list[str], rule: dict[str, Any]) -> None:
    for user in users:
        rule_src_with_users = [user + "@" + src for src in rule["rule_src"].split(LIST_DELIMITER)]

        rule["rule_src"] = LIST_DELIMITER.join(rule_src_with_users)

        # here user ref is the user name itself
        rule_src_refs_with_users = [user + "@" + src for src in rule["rule_src_refs"].split(LIST_DELIMITER)]
        rule["rule_src_refs"] = LIST_DELIMITER.join(rule_src_refs_with_users)


###################
# NAT STARTS HERE #
###################


def get_nat_policy(
    sid: str,
    fm_api_url: str,
    native_config: dict[str, Any],
    adom_device_vdom_policy_package_structure: dict[str, Any],
    adom_name: str,
    mgm_details_device: dict[str, Any],
    limit: int,
):
    local_pkg_name, global_pkg_name = find_packages(
        adom_device_vdom_policy_package_structure, adom_name, mgm_details_device
    )
    if adom_name == "":
        for nat_type in nat_types:
            fmgr_getter.update_config_with_fortinet_api_call(
                native_config["nat_rulebases"],
                sid,
                fm_api_url,
                STRING_PM_CONFIG_GLOBAL_PKG + global_pkg_name + "/" + nat_type,
                nat_type + "_global_" + global_pkg_name,
                limit=limit,
            )
    else:
        for nat_type in nat_types:
            fmgr_getter.update_config_with_fortinet_api_call(
                native_config["nat_rulebases"],
                sid,
                fm_api_url,
                STRING_PM_CONFIG_ADOM + adom_name + STRING_PKG + local_pkg_name + "/" + nat_type,
                nat_type + "_adom_" + adom_name + "_" + local_pkg_name,
                limit=limit,
            )


# delete_v: ab hier kann sehr viel weg, ich lasses vorerst zB für die nat
# pure nat rules


def parse_nat_rulebase(
    nat_rulebase: list[dict[str, Any]],
    nat_type_string: str,
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
) -> list[RuleNormalized]:
    nat_rules: list[RuleNormalized] = []
    rule_number = 0
    for rule_number, rule_orig in enumerate(nat_rulebase):
        rule_src_list, rule_src_refs_list = rule_parse_addresses(
            rule_orig, "src", normalized_config_adom, normalized_config_global, is_nat=True
        )  # because of is_nat = True, this will look for orig-addr
        rule_dst_list, rule_dst_refs_list = rule_parse_addresses(
            rule_orig, "dst", normalized_config_adom, normalized_config_global, is_nat=True
        )  # because of is_nat = True, this will look for dst-addr

        rule_svc_list, rule_svc_refs_list = rule_parse_service(rule_orig)

        rule_src_zones = find_zones_in_normalized_config(
            rule_orig.get("srcintf", []),
            normalized_config_adom,
            normalized_config_global,
        )
        rule_dst_zones = find_zones_in_normalized_config(
            rule_orig.get("dstintf", []),
            normalized_config_adom,
            normalized_config_global,
        )

        rule_normalized = RuleNormalized(
            rule_num=rule_number,
            rule_num_numeric=0,
            rule_disabled=False,
            rule_src_neg=False,
            rule_src=LIST_DELIMITER.join(rule_src_list),
            rule_src_refs=LIST_DELIMITER.join(rule_src_refs_list),
            rule_dst_neg=False,
            rule_dst=LIST_DELIMITER.join(rule_dst_list),
            rule_dst_refs=LIST_DELIMITER.join(rule_dst_refs_list),
            rule_svc_neg=False,
            rule_svc=LIST_DELIMITER.join(rule_svc_list),
            rule_svc_refs=LIST_DELIMITER.join(rule_svc_refs_list),
            rule_action=RuleAction.DROP,
            rule_track=RuleTrack.NONE,
            rule_installon=nat_type_string,
            rule_time="",  # Time-based rules not commonly used in basic Fortinet configs
            rule_name=rule_orig.get("name", ""),
            rule_uid=rule_orig.get("uuid"),
            rule_custom_fields=str({}),
            rule_implied=False,
            rule_type=RuleType.NAT,
            last_change_admin=rule_orig.get("_last-modified-by", ""),
            parent_rule_uid=None,
            last_hit=rule_parse_last_hit(rule_orig),
            rule_comment=rule_orig.get("comments"),
            rule_src_zone=LIST_DELIMITER.join(rule_src_zones),
            rule_dst_zone=LIST_DELIMITER.join(rule_dst_zones),
            rule_head_text=None,
            xlate_rule_uid=f"{rule_orig.get('uuid')}_translated" if rule_orig.get("uuid") else None,
            nat_rule=True,
        )

        xlate_rule = RuleNormalized(
            rule_num=rule_number,
            rule_num_numeric=0,
            rule_disabled=False,
            rule_src_neg=False,
            rule_src=LIST_DELIMITER.join(rule_src_list),
            rule_src_refs=LIST_DELIMITER.join(rule_src_refs_list),
            rule_dst_neg=False,
            rule_dst="Original",
            rule_dst_refs=LIST_DELIMITER.join(rule_dst_refs_list),
            rule_svc_neg=False,
            rule_svc=LIST_DELIMITER.join(rule_svc_list),
            rule_svc_refs=LIST_DELIMITER.join(rule_svc_refs_list),
            rule_action=RuleAction.DROP,
            rule_track=RuleTrack.NONE,
            rule_installon=nat_type_string,
            rule_time="",  # Time-based rules not commonly used in basic Fortinet configs
            rule_name=rule_orig.get("name", ""),
            rule_uid=f"{rule_orig.get('uuid')}_translated" if rule_orig.get("uuid") else None,
            rule_custom_fields=str({}),
            rule_implied=False,
            rule_type=RuleType.NAT,
            last_change_admin=rule_orig.get("_last-modified-by", ""),
            parent_rule_uid=None,
            last_hit=rule_parse_last_hit(rule_orig),
            rule_comment=rule_orig.get("comments"),
            rule_src_zone=LIST_DELIMITER.join(rule_src_zones),
            rule_dst_zone=LIST_DELIMITER.join(rule_dst_zones),
            rule_head_text=None,
            nat_rule=True,
        )

        nat_rules.append(rule_normalized)
        nat_rules.append(xlate_rule)
    normalized_config_adom["rules"].extend(nat_rules)
    return nat_rules


def create_xlate_rule(rule: dict[str, Any]) -> dict[str, Any]:
    xlate_rule = copy.deepcopy(rule)
    rule["rule_type"] = "combined"
    xlate_rule["rule_type"] = "xlate"
    xlate_rule["rule_comment"] = None
    xlate_rule["rule_disabled"] = False
    xlate_rule["rule_src"] = "Original"
    xlate_rule["rule_src_refs"] = "Original"
    xlate_rule["rule_dst"] = "Original"
    xlate_rule["rule_dst_refs"] = "Original"
    xlate_rule["rule_svc"] = "Original"
    xlate_rule["rule_svc_refs"] = "Original"
    return xlate_rule


def extract_nat_objects(nwobj_list: list[str], all_nwobjects: list[dict[str, str]]) -> list[dict[str, str]]:
    nat_obj_list: list[dict[str, str]] = []
    for obj in nwobj_list:
        for obj2 in all_nwobjects:
            if obj2["obj_name"] == obj:
                if "obj_nat_ip" in obj2:
                    nat_obj_list.append(obj2)
                break
    return nat_obj_list


def is_nat_rule(
    native_rule: dict[str, Any],
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
) -> tuple[bool, bool]:
    is_snat = any(key in native_rule and native_rule[key] == 1 for key in ["nat", "nat46", "nat64"])

    dst_addrs = native_rule.get("dstaddr", []) + native_rule.get("dstaddr6", [])

    vip_objects: list[dict[str, Any]] = []
    network_objects = normalized_config_adom.get("network_objects", []) + normalized_config_global.get(
        "network_objects", []
    )

    for nw_obj in network_objects:
        if "firewall/vip" in nw_obj.get("obj_native_type", ""):
            vip_objects.extend([nw_obj])

    for addr in dst_addrs:
        if any(addr == vip_obj.get("obj_name") for vip_obj in vip_objects):
            return is_snat, True

    return is_snat, False


def parse_nat_ip(
    entries: list[str],
    native_rule: dict[str, Any],
    normalized_config_adom: dict[str, Any],
) -> tuple[list[str], list[str]]:
    """
    Example entries: ["1.2.3.4", "255.255.255.255"]

    Creates a network object for
    """
    if len(entries) != EXPECTED_NATIP_LIST_LENGTH:
        FWOLogger.warning(f"Unexpected number of entries for NAT IP parsing: {len(entries)}. Expected 2.")
        return [], []

    parsed_ip = str(IPNetwork(f"{entries[0]}/{entries[1]}"))
    uid = f"{native_rule.get('uuid', 'Translated_IP')}_Translated_IP"
    normalized_config_adom["network_objects"].append(
        create_network_object(
            name=native_rule.get("name", "Translated_IP"),
            obj_type="network",
            ip=parsed_ip,
            ip_end=parsed_ip,
            uid=uid,
            color="black",
            comment="Translated IP network object created by FWO importer for NAT purposes",
            zone="global",
        )
    )

    return [parsed_ip], [uid]


def prepare_translated_nat_fields(
    rule_src_list: list[str],
    rule_dst_list: list[str],
    rule_svc_list: list[str],
    translated_src_list: list[str],
    translated_src_refs_list: list[str],
    translated_dst_list: list[str],
    translated_dst_refs_list: list[str],
    translated_svc_list: list[str],
    translated_svc_refs_list: list[str],
    native_rule: dict[str, Any],
    is_snat: bool,
    is_dnat: bool,
) -> tuple[list[str], list[str], list[str], list[str], list[str], list[str]]:
    translated_dst_list_local = list(translated_dst_list)
    translated_dst_refs_list_local = list(translated_dst_refs_list)
    translated_svc_list_local = list(translated_svc_list)
    translated_svc_refs_list_local = list(translated_svc_refs_list)

    if set(translated_src_list) == set(rule_src_list):
        translated_src_list = ["Original"]
        translated_src_refs_list = ["Original"]

    if is_snat and native_rule.get("ippool") == 0:
        translated_src_list = ["Outgoing Interface IP"]
        translated_src_refs_list = ["Outgoing_Interface_IP"]

    if set(translated_dst_list_local) == set(rule_dst_list) and not is_dnat:
        translated_dst_list_local = ["Original"]
        translated_dst_refs_list_local = ["Original"]

    if set(translated_svc_list_local) == set(rule_svc_list):
        translated_svc_list_local = ["Original"]
        translated_svc_refs_list_local = ["Original"]

    return (
        translated_src_list,
        translated_src_refs_list,
        translated_dst_list_local,
        translated_dst_refs_list_local,
        translated_svc_list_local,
        translated_svc_refs_list_local,
    )


def parse_nat_rules_in_rulebase(
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    rulebase_to_parse: dict[str, Any],
    normalized_nat_rulebase: Rulebase,
):
    """
    Extracts NAT rules from a rulebase and creates normalized NAT rules.
    Creates two RuleNormalized objects per NAT rule (original + translated).
    """
    rule_num = 0
    for native_rule in rulebase_to_parse.get("data", []):
        # Check if this is a NAT rule
        is_snat, is_dnat = is_nat_rule(native_rule, normalized_config_adom, normalized_config_global)

        if not is_snat and not is_dnat:
            continue

        rule_disabled = True
        if "status" in native_rule and (native_rule["status"] == 1 or native_rule["status"] == "enable"):
            rule_disabled = False

        # Parse addresses for original rule
        rule_src_list, rule_src_refs_list = rule_parse_addresses(
            native_rule, "src", normalized_config_adom, normalized_config_global, is_nat=True
        )
        rule_dst_list, rule_dst_refs_list = rule_parse_addresses(
            native_rule, "dst", normalized_config_adom, normalized_config_global, is_nat=True
        )
        translated_src_list, translated_src_refs_list = get_nat_translated_source(
            native_rule, normalized_config_adom, normalized_config_global
        )

        rule_svc_list, rule_svc_refs_list = rule_parse_service(native_rule)

        rule_src_zones = find_zones_in_normalized_config(
            native_rule.get("srcintf", []), normalized_config_adom, normalized_config_global
        )
        rule_dst_zones = find_zones_in_normalized_config(
            native_rule.get("dstintf", []), normalized_config_adom, normalized_config_global
        )

        # Extract NAT config fields
        nat_config_fields = extract_nat_config_fields(native_rule)

        rule_uid = native_rule.get("uuid")
        if not rule_uid:
            FWOLogger.warning("NAT rule without UUID, skipping")
            continue

        # Prepare translated fields: if a translated field equals the original,
        # replace it with the standard placeholder object "Original".
        ensure_original_objects(normalized_config_adom, normalized_config_global)

        translated_dst_list = list(rule_dst_list)
        translated_dst_refs_list = list(rule_dst_refs_list)
        translated_svc_list = list(rule_svc_list)
        translated_svc_refs_list = list(rule_svc_refs_list)

        (
            translated_src_list,
            translated_src_refs_list,
            translated_dst_list_local,
            translated_dst_refs_list_local,
            translated_svc_list_local,
            translated_svc_refs_list_local,
        ) = prepare_translated_nat_fields(
            rule_src_list,
            rule_dst_list,
            rule_svc_list,
            translated_src_list,
            translated_src_refs_list,
            translated_dst_list,
            translated_dst_refs_list,
            translated_svc_list,
            translated_svc_refs_list,
            native_rule,
            is_snat,
            is_dnat,
        )

        if native_rule.get("rtp-nat") == 1:
            translated_src_list, translated_src_refs_list = parse_nat_ip(
                native_rule.get("natip", []), native_rule, normalized_config_adom
            )

        # Create original rule (match phase)
        rule_original_uid = f"{rule_uid}-original"
        rule_translated_uid = f"{rule_uid}-translated"

        rule_original = RuleNormalized(
            rule_num=rule_num,
            rule_num_numeric=0,
            rule_disabled=rule_disabled,
            rule_src_neg=False,
            rule_src=LIST_DELIMITER.join(rule_src_list),
            rule_src_refs=LIST_DELIMITER.join(rule_src_refs_list),
            rule_dst_neg=False,
            rule_dst=LIST_DELIMITER.join(rule_dst_list),
            rule_dst_refs=LIST_DELIMITER.join(rule_dst_refs_list),
            rule_svc_neg=False,
            rule_svc=LIST_DELIMITER.join(rule_svc_list),
            rule_svc_refs=LIST_DELIMITER.join(rule_svc_refs_list),
            rule_action=rule_parse_action(native_rule),
            rule_track=rule_parse_tracking_info(native_rule),
            rule_installon=rule_parse_installon(native_rule),
            rule_time=rule_parse_time(native_rule),
            rule_name=native_rule.get("name", ""),
            rule_uid=rule_original_uid,
            rule_custom_fields=None,
            rule_implied=False,
            rule_type=RuleType.NAT,
            last_change_admin=None,
            parent_rule_uid=None,
            last_hit=rule_parse_last_hit(native_rule),
            rule_comment=native_rule.get("comments"),
            rule_src_zone=LIST_DELIMITER.join(rule_src_zones),
            rule_dst_zone=LIST_DELIMITER.join(rule_dst_zones),
            rule_head_text=None,
            access_rule=False,
            nat_rule=True,
            xlate_rule_uid=rule_translated_uid,
        )

        # Create translated rule (translation phase)
        # Keep the original destination and service; translate the source to the NAT pool.
        rule_translated = RuleNormalized(
            rule_num=rule_num,
            rule_num_numeric=0,
            rule_disabled=rule_disabled,
            rule_src_neg=False,
            rule_src=LIST_DELIMITER.join(translated_src_list),
            rule_src_refs=LIST_DELIMITER.join(translated_src_refs_list),
            rule_dst_neg=False,
            rule_dst=LIST_DELIMITER.join(translated_dst_list_local),
            rule_dst_refs=LIST_DELIMITER.join(translated_dst_refs_list_local),
            rule_svc_neg=False,
            rule_svc=LIST_DELIMITER.join(translated_svc_list_local),
            rule_svc_refs=LIST_DELIMITER.join(translated_svc_refs_list_local),
            rule_action=rule_parse_action(native_rule),
            rule_track=rule_parse_tracking_info(native_rule),
            rule_installon=rule_parse_installon(native_rule),
            rule_time=rule_parse_time(native_rule),
            rule_name=native_rule.get("name", ""),
            rule_uid=rule_translated_uid,
            rule_custom_fields=nat_config_fields,
            rule_implied=False,
            rule_type=RuleType.NAT,
            last_change_admin=None,
            parent_rule_uid=None,
            last_hit=rule_parse_last_hit(native_rule),
            rule_comment=native_rule.get("comments"),
            rule_src_zone=LIST_DELIMITER.join(rule_src_zones),
            rule_dst_zone=LIST_DELIMITER.join(rule_dst_zones),
            rule_head_text=None,
            access_rule=False,
            nat_rule=True,
            xlate_rule_uid=None,
        )

        # Add both rules to the NAT rulebase
        if rule_original.rule_uid:
            normalized_nat_rulebase.rules[rule_original.rule_uid] = rule_original
        if rule_translated.rule_uid:
            normalized_nat_rulebase.rules[rule_translated.rule_uid] = rule_translated

        rule_num += 1


def extract_nat_config_fields(native_rule: dict[str, Any]) -> str:
    """
    Extracts NAT-specific configuration fields from a native rule.
    Returns a JSON string with NAT translation metadata.
    """
    nat_config: dict[str, Any] = {}

    if native_rule.get("ippool") == 1:
        nat_config["ippool"] = 1
        poolname6 = native_rule.get("poolname6")
        if isinstance(poolname6, list) and poolname6:
            nat_config["poolname6"] = poolname6
        elif isinstance(poolname6, str) and poolname6:
            nat_config["poolname6"] = [poolname6]

        poolname = native_rule.get("poolname")
        if isinstance(poolname, list) and poolname:
            nat_config["poolname"] = poolname
        elif isinstance(poolname, str) and poolname:
            nat_config["poolname"] = [poolname]

    if "fixedport" in native_rule:
        nat_config["fixedport"] = native_rule.get("fixedport")

    if "nat" in native_rule and native_rule["nat"] == 1:
        nat_config["nat_type"] = "nat"
    elif "nat46" in native_rule and native_rule["nat46"] == 1:
        nat_config["nat_type"] = "nat46"
    elif "nat64" in native_rule and native_rule["nat64"] == 1:
        nat_config["nat_type"] = "nat64"

    return json.dumps(nat_config, sort_keys=True) if nat_config else "{}"


def get_nat_translated_source(
    native_rule: dict[str, Any],
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
) -> tuple[list[str], list[str]]:
    if native_rule.get("ippool") == 1:
        is_ipv6 = "poolname6" in native_rule and native_rule.get("poolname6") not in (None, [], "")
        poolname = native_rule.get("poolname6" if is_ipv6 else "poolname", [])
        if isinstance(poolname, str):
            poolname = [poolname]
        translated_src_list = sorted(poolname)
        translated_src_refs_list = [
            find_addr_ref(
                pool,
                is_v4=not is_ipv6,
                normalized_config_adom=normalized_config_adom,
                normalized_config_global=normalized_config_global,
            )
            for pool in translated_src_list
        ]
        return translated_src_list, translated_src_refs_list

    rule_src_list, rule_src_refs_list = rule_parse_addresses(
        native_rule, "src", normalized_config_adom, normalized_config_global, is_nat=True
    )
    return rule_src_list, rule_src_refs_list


def new_process_nat_rules_for_rulebase(
    gateway: dict[str, Any],
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    rulebase_to_parse: dict[str, Any],
    normalized_rulebase: Rulebase,
) -> None:
    has_nat_rules = any(
        any(key in native_rule and native_rule[key] == 1 for key in ["nat", "nat46", "nat64"])
        for native_rule in rulebase_to_parse.get("data", [])
    )

    if not has_nat_rules:
        return

    normalized_nat_rulebase = insert_parent_nat_rulebase(
        normalized_config_adom,
        normalized_config_global,
        normalized_rulebase.uid,
        normalized_rulebase.mgm_uid,
    )

    insert_nat_rulebase_link(
        from_rulebase_uid=normalized_rulebase.uid,
        to_rulebase_uid=normalized_nat_rulebase.uid,
        gateway=gateway,
    )

    parse_nat_rules_in_rulebase(
        normalized_config_adom,
        normalized_config_global,
        rulebase_to_parse,
        normalized_nat_rulebase,
    )


def normalize_nat_rulebase(
    rulebase_link: dict[str, Any],
    native_config: dict[str, Any],
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
):
    normalized_config_adom.setdefault("nat_policies", [])
    link_type = rulebase_link.get("link_type", rulebase_link.get("type", "ordered"))
    if link_type == "nat":
        return

    if not rulebase_link["is_section"]:
        for nat_type in nat_types:
            nat_type_string = nat_type + "_" + rulebase_link["to_rulebase_uid"]
            nat_rulebase = get_native_nat_rulebase(native_config, nat_type_string)
            parse_nat_rulebase(
                nat_rulebase,
                nat_type_string,
                normalized_config_adom,
                normalized_config_global,
            )

            normalized_config_adom["nat_policies"].extend(nat_rulebase)  # pyright: ignore[reportUnknownMemberType]


def get_native_nat_rulebase(native_config: dict[str, Any], nat_type_string: str) -> list[dict[str, Any]]:
    for nat_rulebase in native_config["nat_rulebases"]:
        if nat_type_string == nat_rulebase["type"]:
            return nat_rulebase["data"]
    FWOLogger.warning("no nat data for " + nat_type_string)
    return []


def insert_parent_nat_rulebase(
    normalized_config_adom: dict[str, Any],
    _normalized_config_global: dict[str, Any],
    rulebase_uid: str,
    mgm_uid: str,
) -> Rulebase:
    # Creates a NAT rulebase for the given access rulebase.
    nat_rulebase_uid = "nat-rulebase-" + rulebase_uid
    normalized_nat_rulebase = Rulebase(
        uid=nat_rulebase_uid,
        mgm_uid=mgm_uid,
        name="NAT",
        rules={},
    )

    # Add to adom policies (avoid duplicates)
    if not any(rb for rb in normalized_config_adom["policies"] if rb.uid == normalized_nat_rulebase.uid):
        normalized_config_adom["policies"].append(normalized_nat_rulebase)

    return normalized_nat_rulebase


def insert_nat_rulebase_link(
    from_rulebase_uid: str,
    to_rulebase_uid: str,
    gateway: dict[str, Any],
) -> None:
    # Creates a RulebaseLink with link_type='nat' connecting access rulebase to NAT rulebase.
    if not any(
        link
        for link in gateway["rulebase_links"]
        if link.get("to_rulebase_uid") == to_rulebase_uid
        and link.get("link_type") == "nat"
        and link.get("from_rulebase_uid") == from_rulebase_uid
    ):
        gateway["rulebase_links"].append(
            {
                "from_rulebase_uid": from_rulebase_uid,
                "to_rulebase_uid": to_rulebase_uid,
                "type": "nat",
                "is_initial": False,
                "is_global": False,
                "is_section": False,
            }
        )

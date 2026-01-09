from collections.abc import Generator

import fwo_const
from fw_modules.fortiosmanagementREST import fos_zone
from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig, Rule
from fwo_exceptions import FwoImporterError
from model_controllers.management_controller import ManagementController
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType


def normalize_rule_addresses(rule: Rule, nw_obj_lookup_dict: dict[str, str]) -> tuple[str, str, str, str]:
    """
    Normalize rule addresses from a FortiOS rule.

    Args:
        rule: The FortiOS rule object.
        nw_obj_lookup_dict: Lookup dictionary for network object names to UIDs.

    Returns:
        tuple[str, str, str, str]: The normalized source addresses, source references,
            destination addresses, destination references.

    """
    if rule.internet_service_src_name:
        rule_src_names = [src.name for src in rule.internet_service_src_name]
    else:
        rule_src_names = [src.name for src in rule.srcaddr + rule.srcaddr6]

    if not rule_src_names:
        raise FwoImporterError(f"Rule '{rule.name}' has no source addresses defined.")
    for src in rule_src_names:
        if src not in nw_obj_lookup_dict:
            raise FwoImporterError(f"Source service object '{src}' not found in network object lookup.")

    if rule.internet_service_name:
        rule_dst_names = [dst.name for dst in rule.internet_service_name]
    else:
        rule_dst_names = [dst.name for dst in rule.dstaddr + rule.dstaddr6]

    if not rule_dst_names:
        raise FwoImporterError(f"Rule '{rule.name}' has no destination addresses defined.")
    for dst in rule_dst_names:
        if dst not in nw_obj_lookup_dict:
            raise FwoImporterError(f"Destination service object '{dst}' not found in network object lookup.")

    rule_src = fwo_const.LIST_DELIMITER.join(rule_src_names)
    rule_src_refs = fwo_const.LIST_DELIMITER.join(nw_obj_lookup_dict[src] for src in rule_src_names)
    rule_dst = fwo_const.LIST_DELIMITER.join(rule_dst_names)
    rule_dst_refs = fwo_const.LIST_DELIMITER.join(nw_obj_lookup_dict[dst] for dst in rule_dst_names)

    return rule_src, rule_src_refs, rule_dst, rule_dst_refs


def normalize_rule_services(rule: Rule) -> tuple[str, str]:
    """
    Normalize rule services from a FortiOS rule.

    Args:
        rule: The FortiOS rule object.

    Returns:
        tuple[str, str]: The normalized service names and service references.

    """
    rule_svc_names = [svc.name for svc in rule.service]

    if rule.internet_service_name or rule.internet_service_src_name:
        rule_svc_names.append("Internet Service")

    if not rule_svc_names:
        raise FwoImporterError(f"Rule '{rule.name}' has no services defined.")

    rule_svc = fwo_const.LIST_DELIMITER.join(rule_svc_names)
    rule_svc_refs = rule_svc  # Service objects use names as UIDs

    return rule_svc, rule_svc_refs


def normalize_rule_zones(rule: Rule) -> tuple[str | None, str | None]:
    """
    Normalize rule zones from a FortiOS rule.

    Args:
        rule: The FortiOS rule object.

    Returns:
        tuple[str, str]: The normalized source zones and destination zones.

    """
    rule_src_zone = None
    rule_dst_zone = None

    rule_src_zone_names = [fos_zone.normalize_zone(intf.name) for intf in rule.srcintf]
    rule_dst_zone_names = [fos_zone.normalize_zone(intf.name) for intf in rule.dstintf]

    if rule_src_zone_names:
        rule_src_zone = fwo_const.LIST_DELIMITER.join(rule_src_zone_names)
    if rule_dst_zone_names:
        rule_dst_zone = fwo_const.LIST_DELIMITER.join(rule_dst_zone_names)

    return rule_src_zone, rule_dst_zone


def normalize_access_rules(
    native_config: FortiOSConfig, mgm_details: ManagementController, nw_obj_lookup_dict: dict[str, str]
) -> Generator[RuleNormalized]:
    """
    Normalize access rules from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.
        mgm_details (ManagementController): The management details object.
        nw_obj_lookup_dict (dict[str, str]): Lookup dictionary for network object names to UIDs.

    Yields:
        RuleNormalized: The normalized access rule.

    """
    if len(mgm_details.devices) == 0 or "name" not in mgm_details.devices[0]:
        raise FwoImporterError("Management details must contain at least one device with a name.")

    rule_installon = mgm_details.devices[0]["name"]

    for rule in native_config.rules:
        rule_type = RuleType.ACCESS
        rule_name = rule.name
        rule_uid = rule.uuid
        # TODO: rule_ruleid from rule.policyid
        rule_implied = False
        rule_comment = rule.comments
        rule_src, rule_src_refs, rule_dst, rule_dst_refs = normalize_rule_addresses(rule, nw_obj_lookup_dict)
        rule_svc, rule_svc_refs = normalize_rule_services(rule)
        rule_src_neg = rule.srcaddr_negate == "enable"
        rule_dst_neg = rule.dstaddr_negate == "enable"
        rule_svc_neg = rule.service_negate == "enable"
        rule_src_zone, rule_dst_zone = normalize_rule_zones(rule)
        rule_action = RuleAction.DROP if rule.action == "deny" else RuleAction.ACCEPT
        rule_disabled = rule.status not in {"enable", 1}
        rule_track = RuleTrack.NONE if rule.logtraffic == "disable" else RuleTrack.LOG

        yield RuleNormalized(
            rule_num=0,
            rule_num_numeric=0.0,
            rule_name=rule_name,
            rule_type=rule_type,
            rule_uid=rule_uid,
            rule_implied=rule_implied,
            rule_comment=rule_comment,
            rule_src=rule_src,
            rule_src_refs=rule_src_refs,
            rule_dst=rule_dst,
            rule_dst_refs=rule_dst_refs,
            rule_svc=rule_svc,
            rule_svc_refs=rule_svc_refs,
            rule_src_neg=rule_src_neg,
            rule_dst_neg=rule_dst_neg,
            rule_svc_neg=rule_svc_neg,
            rule_src_zone=rule_src_zone,
            rule_dst_zone=rule_dst_zone,
            rule_action=rule_action,
            rule_disabled=rule_disabled,
            rule_installon=rule_installon,
            rule_track=rule_track,
        )

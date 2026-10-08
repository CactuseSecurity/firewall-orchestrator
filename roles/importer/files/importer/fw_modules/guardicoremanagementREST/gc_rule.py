from __future__ import annotations

from datetime import datetime, timezone
from typing import TYPE_CHECKING

import fwo_const
from fw_modules.guardicoremanagementREST import gc_const
from fw_modules.guardicoremanagementREST.gc_service import build_rule_services
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger
from models.rule import RuleAction, RuleLabels, RuleNormalized, RuleTrack, RuleType
from models.rulebase import Rulebase
from models.rulebase_link import RulebaseLinkUidBased

if TYPE_CHECKING:
    from fw_modules.guardicoremanagementREST.gc_models import GcRule, GcRuleSide, GuardicoreConfig
    from fw_modules.guardicoremanagementREST.gc_network import GcNetworkObjectCollector
    from model_controllers.management_controller import ManagementController
    from models.serviceobject import ServiceObject

MILLISECONDS_PER_SECOND = 1000

GC_RULE_ACTIONS: dict[str, RuleAction] = {
    gc_const.GC_ACTION_ALLOW: RuleAction.ACCEPT,
    gc_const.GC_ACTION_ALLOW_AND_ENCRYPT: RuleAction.ACCEPT,
    gc_const.GC_ACTION_ALERT: RuleAction.INFORM,
    gc_const.GC_ACTION_BLOCK: RuleAction.DROP,
    gc_const.GC_ACTION_BLOCK_AND_ALERT: RuleAction.DROP,
}


def get_rule_installon(mgm_details: ManagementController) -> str:
    if mgm_details.devices and "name" in mgm_details.devices[0]:
        return mgm_details.devices[0]["name"]
    if mgm_details.name:
        return mgm_details.name
    if mgm_details.hostname:
        return mgm_details.hostname
    raise FwoImporterError("Management details must contain a device name or management name/hostname.")


def get_rulebase_name(rule: GcRule) -> str:
    """
    Name the rulebase of a rule after its section and ruleset (app), e.g. "04 Allow APP-1234".

    The leading section number keeps the alphabetical order of the rulebases equal to the Guardicore evaluation order.
    """
    section = gc_const.GC_SECTIONS.get(rule.section_position.upper())
    if section is None:
        raise FwoImporterError(f"Guardicore rule {rule.id} has unknown section position '{rule.section_position}'")
    name = f"{section.number:02d} {section.name}"
    ruleset_name = (rule.ruleset_name or gc_const.GC_RULESET_NAME_NONE).strip()
    return f"{name} {ruleset_name}" if ruleset_name else name


def get_rule_action(rule: GcRule) -> RuleAction:
    action = GC_RULE_ACTIONS.get(rule.action.upper())
    if action is None:
        raise FwoImporterError(f"Guardicore rule {rule.id} has unknown action '{rule.action}'")
    return action


def get_rule_track(rule: GcRule) -> RuleTrack:
    """Alert rules raise incidents, all other connections are recorded in the Guardicore network log."""
    return RuleTrack.ALERT if rule.action.upper() in gc_const.GC_ALERTING_ACTIONS else RuleTrack.LOG


def convert_last_hit(last_hit: int | None) -> str | None:
    if last_hit is None:
        return None
    return datetime.fromtimestamp(last_hit / MILLISECONDS_PER_SECOND, tz=timezone.utc).isoformat(timespec="seconds")


def get_side_labels(side: GcRuleSide, collector: GcNetworkObjectCollector) -> RuleLabels | None:
    """
    Return the labels of a rule side as {"label-key": "label-value"}, several values of one key as sorted list.

    Returns None if the side has no labels.
    """
    values_by_key: dict[str, list[str]] = {}
    for and_labels in side.labels.or_labels if side.labels is not None else []:
        for label_ref in and_labels.and_labels:
            label = collector.labels_by_id.get(label_ref.id)
            key = label.key if label is not None else label_ref.key
            value = label.value if label is not None else label_ref.value
            if key is None or value is None:
                FWOLogger.warning(
                    f"Guardicore label {label_ref.id} has no key or value, leaving it out of the rule labels"
                )
                continue
            values = values_by_key.setdefault(key, [])
            if value not in values:
                values.append(value)
    if len(values_by_key) == 0:
        return None
    return {key: values[0] if len(values) == 1 else sorted(values) for key, values in values_by_key.items()}


def normalize_rule(
    rule: GcRule,
    collector: GcNetworkObjectCollector,
    services: list[ServiceObject],
    rule_installon: str,
) -> RuleNormalized:
    src_uids = collector.resolve_rule_side(rule.source, rule.id)
    dst_uids = collector.resolve_rule_side(rule.destination, rule.id)
    svc_uids = [service.svc_uid for service in services]
    return RuleNormalized(
        rule_num_numeric=0,  # set during import
        rule_disabled=not rule.enabled,
        rule_src_neg=False,
        rule_src=fwo_const.LIST_DELIMITER.join(collector.get_names(src_uids)),
        rule_src_refs=fwo_const.LIST_DELIMITER.join(src_uids),
        rule_dst_neg=False,
        rule_dst=fwo_const.LIST_DELIMITER.join(collector.get_names(dst_uids)),
        rule_dst_refs=fwo_const.LIST_DELIMITER.join(dst_uids),
        rule_svc_neg=False,
        rule_svc=fwo_const.LIST_DELIMITER.join(service.svc_name for service in services),
        rule_svc_refs=fwo_const.LIST_DELIMITER.join(svc_uids),
        rule_action=get_rule_action(rule),
        rule_track=get_rule_track(rule),
        rule_installon=rule_installon,
        rule_name=rule.ruleset_name or None,
        rule_uid=rule.id,
        rule_implied=False,
        rule_type=RuleType.ACCESS,
        last_change_admin=rule.author.username if rule.author is not None else None,
        last_hit=convert_last_hit(rule.last_hit),
        rule_comment=rule.comments or None,
        rule_src_labels=get_side_labels(rule.source, collector),
        rule_dst_labels=get_side_labels(rule.destination, collector),
    )


def normalize_rules(
    native_config: GuardicoreConfig,
    collector: GcNetworkObjectCollector,
    mgm_details: ManagementController,
    service_objects: dict[str, ServiceObject],
) -> list[Rulebase]:
    """
    Normalize the Guardicore segmentation rules into one rulebase per section and ruleset, sorted by name.

    Rules keep the order the API returns them in, which is the evaluation order within a section.
    The services used by the rules are added to service_objects.
    """
    rule_installon = get_rule_installon(mgm_details)
    rulebases: dict[str, Rulebase] = {}
    for rule in native_config.rules:
        services = build_rule_services(rule)
        if len(services) == 0:
            FWOLogger.warning(f"skipping Guardicore rule {rule.id} as it matches no service")
            continue
        for service in services:
            service_objects.setdefault(service.svc_uid, service)
        rulebase_name = get_rulebase_name(rule)
        rulebase = rulebases.setdefault(
            rulebase_name, Rulebase(uid=rulebase_name, name=rulebase_name, mgm_uid=mgm_details.uid, rules={})
        )
        rulebase.rules[rule.id] = normalize_rule(rule, collector, services, rule_installon)
    return [rulebases[name] for name in sorted(rulebases)]


def build_rulebase_links(rulebases: list[Rulebase]) -> list[RulebaseLinkUidBased]:
    """Chain the rulebases in their order, starting with the first one."""
    links: list[RulebaseLinkUidBased] = []
    for index, rulebase in enumerate(rulebases):
        links.append(
            RulebaseLinkUidBased(
                from_rulebase_uid=rulebases[index - 1].uid if index > 0 else None,
                to_rulebase_uid=rulebase.uid,
                link_type="ordered",
                is_initial=index == 0,
                is_global=False,
                is_section=False,
            )
        )
    return links

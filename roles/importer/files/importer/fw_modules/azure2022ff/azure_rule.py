from __future__ import annotations

from typing import TYPE_CHECKING

import fwo_const
from fw_modules.azure2022ff.azure_const import AZURE_ACTION_DENY, AZURE_FILTER_RULE_COLLECTION, AZURE_NETWORK_RULE
from fw_modules.azure2022ff.azure_network import (
    add_network_object,
    create_fqdn_object,
    create_kube_selector_group_object,
    parse_address,
    resolve_ip_group,
)
from fw_modules.azure2022ff.azure_service import get_rule_services
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType

if TYPE_CHECKING:
    from collections.abc import Generator

    from fw_modules.azure2022ff.azure_models import (
        AzureFirewallPolicy,
        AzureRule,
        AzureRuleCollection,
        AzureRuleCollectionGroup,
    )
    from models.networkobject import NetworkObject
    from models.serviceobject import ServiceObject


class AzureRuleNormalizer:
    """Normalizes the network rules of an Azure firewall policy and collects the objects they reference."""

    def __init__(
        self,
        network_objects: dict[str, NetworkObject],
        service_objects: dict[str, ServiceObject],
        ip_groups: dict[str, NetworkObject],
        skip_unsupported_rules: bool = True,
    ) -> None:
        self.network_objects = network_objects
        self.service_objects = service_objects
        self.ip_groups = ip_groups
        self.skip_unsupported_rules = skip_unsupported_rules

    def is_supported(self, rule: AzureRule, rule_uid: str) -> bool:
        """
        Check a rule for features the normalized model cannot represent.

        Unsupported rules are skipped with a warning or fail the import, depending on the importer setting.

        Args:
            rule: The native Azure rule.
            rule_uid: The uid of the rule.

        Returns:
            bool: True if the rule can be imported, False if it is skipped.

        """
        # geo-location filters restrict the matching addresses, importing the rule without them would broaden it
        if not rule.source_geo_locations and not rule.destination_geo_locations:
            return True
        message = f"Azure rule '{rule_uid}' uses geo-location filters, which are not supported"
        if not self.skip_unsupported_rules:
            raise FwoImporterError(f"{message}. Enable skipping unsupported rules in the importer settings to skip it.")
        FWOLogger.warning(f"{message} - skipping the rule")
        return False

    def normalize_policy(self, policy: AzureFirewallPolicy, rule_installon: str) -> Generator[RuleNormalized]:
        """
        Normalize all network rules of filter rule collections of a firewall policy.

        NAT rule collections and application rules are not imported.
        Azure lists rule collection groups and rule collections in no particular order, the firewall processes them
        by ascending priority, first of the groups, then of the collections within a group.

        Args:
            policy: The native Azure firewall policy.
            rule_installon: The gateways the policy is enforced on.

        Yields:
            RuleNormalized: The normalized access rules in processing order.

        """
        rule_collection_groups = sorted(
            policy.rule_collection_groups, key=lambda group: priority_sort_key(group.properties.priority)
        )
        for rule_collection_group in rule_collection_groups:
            rule_collections = sorted(
                rule_collection_group.properties.rule_collections,
                key=lambda collection: priority_sort_key(collection.priority),
            )
            for rule_collection in rule_collections:
                if rule_collection.rule_collection_type != AZURE_FILTER_RULE_COLLECTION:
                    FWOLogger.debug(f"Skipping Azure rule collection '{rule_collection.name}' of unsupported type")
                    continue
                yield from self.normalize_rule_collection(rule_collection_group, rule_collection, rule_installon)

    def normalize_rule_collection(
        self,
        rule_collection_group: AzureRuleCollectionGroup,
        rule_collection: AzureRuleCollection,
        rule_installon: str,
    ) -> Generator[RuleNormalized]:
        is_deny = rule_collection.action is not None and rule_collection.action.type == AZURE_ACTION_DENY
        rule_action = RuleAction.DROP if is_deny else RuleAction.ACCEPT
        for rule in rule_collection.rules:
            if rule.rule_type != AZURE_NETWORK_RULE:
                FWOLogger.debug(f"Skipping Azure rule '{rule.name}' of unsupported type '{rule.rule_type}'")
                continue
            # Azure rules have no id of their own, names are unique within their rule collection
            rule_uid = f"{rule_collection_group.id.lower()}/ruleCollections/{rule_collection.name}/rules/{rule.name}"
            if not self.is_supported(rule, rule_uid):
                continue
            try:
                yield self.normalize_network_rule(rule, rule_uid, rule_action, rule_installon)
            except FwoImporterError as e:
                raise FwoImporterError(f"Azure rule '{rule_uid}': {e}") from None

    def normalize_network_rule(
        self, rule: AzureRule, rule_uid: str, rule_action: RuleAction, rule_installon: str
    ) -> RuleNormalized:
        rule_src, rule_src_refs = join_address_refs(self.get_source_objects(rule))
        rule_dst, rule_dst_refs = join_address_refs(self.get_destination_objects(rule))
        services = [
            self.service_objects.setdefault(svc.svc_uid, svc)
            for svc in get_rule_services(rule.ip_protocols, rule.destination_ports)
        ]
        return RuleNormalized(
            rule_num_numeric=0,
            rule_name=rule.name,
            rule_uid=rule_uid,
            rule_type=RuleType.ACCESS,
            rule_implied=False,
            rule_comment=rule.description,
            rule_src=rule_src,
            rule_src_refs=rule_src_refs,
            rule_dst=rule_dst,
            rule_dst_refs=rule_dst_refs,
            rule_svc=fwo_const.LIST_DELIMITER.join(svc.svc_name for svc in services),
            rule_svc_refs=fwo_const.LIST_DELIMITER.join(svc.svc_uid for svc in services),
            rule_src_neg=False,
            rule_dst_neg=False,
            rule_svc_neg=False,
            rule_action=rule_action,
            rule_disabled=False,
            rule_installon=rule_installon,
            rule_track=RuleTrack.NONE,
        )

    def get_address_objects(self, addresses: list[str], ip_group_ids: list[str]) -> list[NetworkObject]:
        """Collect the objects of inline addresses ('*', IPs, prefixes, ranges, service tags) and IP groups."""
        nw_objects = [add_network_object(self.network_objects, parse_address(a)) for a in addresses]
        nw_objects += [resolve_ip_group(ip_group_id, self.ip_groups) for ip_group_id in ip_group_ids]
        return nw_objects

    def get_source_objects(self, rule: AzureRule) -> list[NetworkObject]:
        nw_objects = self.get_address_objects(rule.source_addresses, rule.source_ip_groups)
        nw_objects += [
            add_network_object(self.network_objects, create_kube_selector_group_object(group_id))
            for group_id in rule.source_kube_selector_groups
        ]
        return nw_objects

    def get_destination_objects(self, rule: AzureRule) -> list[NetworkObject]:
        nw_objects = self.get_address_objects(rule.destination_addresses, rule.destination_ip_groups)
        nw_objects += [
            add_network_object(self.network_objects, create_fqdn_object(fqdn)) for fqdn in rule.destination_fqdns
        ]
        return nw_objects


def priority_sort_key(priority: int | None) -> tuple[bool, int]:
    """
    Sort key for Azure priorities: ascending, entries without priority (only in hand-made native configs) last.

    Args:
        priority: The priority of a rule collection group or rule collection.

    Returns:
        tuple[bool, int]: The sort key.

    """
    return priority is None, priority or 0


def join_address_refs(nw_objects: list[NetworkObject]) -> tuple[str, str]:
    """
    Join the network objects of a rule side.

    Args:
        nw_objects: The network objects of the rule side.

    Returns:
        tuple[str, str]: Joined object names and joined object uids.

    """
    if not nw_objects:
        raise FwoImporterError("no addresses, IP groups, Kubernetes selector groups or FQDNs defined.")
    names = fwo_const.LIST_DELIMITER.join(nw_obj.obj_name for nw_obj in nw_objects)
    refs = fwo_const.LIST_DELIMITER.join(nw_obj.obj_uid for nw_obj in nw_objects)
    return names, refs

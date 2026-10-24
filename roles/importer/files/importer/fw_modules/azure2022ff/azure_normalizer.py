from __future__ import annotations

from typing import TYPE_CHECKING

import fwo_const
from fw_modules.azure2022ff.azure_const import AZURE_MAX_POLICY_DEPTH
from fw_modules.azure2022ff.azure_network import normalize_ip_groups
from fw_modules.azure2022ff.azure_rule import AzureRuleNormalizer
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from models.rulebase import Rulebase
from models.rulebase_link import RulebaseLinkUidBased

if TYPE_CHECKING:
    from fw_modules.azure2022ff.azure_models import AzureConfig, AzureDevice, AzureFirewallPolicy
    from models.networkobject import NetworkObject
    from models.rule import RuleNormalized
    from models.serviceobject import ServiceObject


def normalize_config(
    native_config: AzureConfig, devices: list[AzureDevice], mgm_uid: str, skip_unsupported_rules: bool = True
) -> FwConfigNormalized:
    """
    Normalize the native Azure configuration.

    Each firewall policy becomes one rulebase, each device a gateway. A gateway is linked to the rulebase of the
    topmost parent policy of its policy, followed by the rulebases of the child policies down to its own policy,
    as Azure processes inherited rules before the rules of the child policy.

    Args:
        native_config: The native Azure configuration.
        devices: The devices to import with their firewall policy names.
        mgm_uid: The uid of the management.
        skip_unsupported_rules: Skip rules with unsupported features with a warning instead of failing.

    Returns:
        FwConfigNormalized: The normalized configuration.

    """
    network_objects: dict[str, NetworkObject] = {}
    service_objects: dict[str, ServiceObject] = {}
    ip_groups = normalize_ip_groups(native_config.ip_groups, network_objects)
    rule_normalizer = AzureRuleNormalizer(network_objects, service_objects, ip_groups, skip_unsupported_rules)

    policy_index = AzurePolicyIndex(native_config.firewall_policies)
    device_policy_chains = [(device, policy_index.get_policy_chain(device.policy_name)) for device in devices]
    gateway_names_by_policy = get_gateway_names_by_policy(device_policy_chains)
    rulebases = [
        normalize_rulebase(
            policy_index.policies_by_name[policy_name],
            rule_normalizer,
            fwo_const.LIST_DELIMITER.join(gateway_names),
            mgm_uid,
        )
        for policy_name, gateway_names in sorted(gateway_names_by_policy.items())
    ]

    normalized_config = FwConfigNormalized()
    normalized_config.network_objects = network_objects
    normalized_config.service_objects = service_objects
    normalized_config.rulebases = rulebases
    normalized_config.gateways = [create_gateway(device, policy_chain) for device, policy_chain in device_policy_chains]
    FWOLogger.debug(
        f"Normalized {len(network_objects)} network objects, {len(service_objects)} service objects "
        f"and {sum(len(rb.rules) for rb in rulebases)} rules."
    )
    return normalized_config


class AzurePolicyIndex:
    """Looks up the firewall policies of a native config by name and by resource id."""

    def __init__(self, policies: list[AzureFirewallPolicy]) -> None:
        self.policies_by_name: dict[str, AzureFirewallPolicy] = {}
        self.policies_by_id: dict[str, AzureFirewallPolicy] = {}
        for policy in policies:
            known_policy = self.policies_by_name.setdefault(policy.name, policy)
            # policy names become rulebase uids, so they must be unique even across resource groups
            if known_policy is not policy:
                raise FwoImporterError(
                    f"Azure firewall policy name '{policy.name}' is used by several policies: "
                    f"'{known_policy.id}' and '{policy.id}'."
                )
            if policy.id is not None:
                self.policies_by_id[policy.id.lower()] = policy

    def get_policy_chain(self, policy_name: str) -> list[AzureFirewallPolicy]:
        """
        Get a firewall policy with all its parent policies.

        Args:
            policy_name: The name of the firewall policy.

        Returns:
            list[AzureFirewallPolicy]: The policies in processing order, the topmost parent first.

        """
        policy = self.policies_by_name.get(policy_name)
        if policy is None:
            raise FwoImporterError(f"Azure firewall policy '{policy_name}' not found in native config.")
        policy_chain = [policy]
        while policy.base_policy is not None:
            if len(policy_chain) >= AZURE_MAX_POLICY_DEPTH:
                raise FwoImporterError(
                    f"Azure firewall policy '{policy_name}' has more than {AZURE_MAX_POLICY_DEPTH} levels of parent "
                    "policies or a cyclic parent policy reference."
                )
            parent_policy = self.policies_by_id.get(policy.base_policy.id.lower())
            if parent_policy is None:
                raise FwoImporterError(
                    f"Parent policy '{policy.base_policy.id}' of Azure firewall policy '{policy.name}' "
                    "not found in native config."
                )
            policy_chain.append(parent_policy)
            policy = parent_policy
        policy_chain.reverse()
        return policy_chain


def get_gateway_names_by_policy(
    device_policy_chains: list[tuple[AzureDevice, list[AzureFirewallPolicy]]],
) -> dict[str, list[str]]:
    """Collect the gateways each policy is enforced on, parent policies are enforced on the gateways of children."""
    gateway_names_by_policy: dict[str, list[str]] = {}
    for device, policy_chain in device_policy_chains:
        for policy in policy_chain:
            gateway_names_by_policy.setdefault(policy.name, []).append(device.name)
    return gateway_names_by_policy


def normalize_rulebase(
    policy: AzureFirewallPolicy, rule_normalizer: AzureRuleNormalizer, rule_installon: str, mgm_uid: str
) -> Rulebase:
    rules: dict[str, RuleNormalized] = {}
    for rule in rule_normalizer.normalize_policy(policy, rule_installon):
        if rule.rule_uid in rules:
            raise FwoImporterError(f"Duplicate Azure rule '{rule.rule_uid}'.")
        rules[str(rule.rule_uid)] = rule
    return Rulebase(uid=policy.name, name=policy.name, mgm_uid=mgm_uid, is_global=False, rules=rules)


def create_gateway(device: AzureDevice, policy_chain: list[AzureFirewallPolicy]) -> Gateway:
    """
    Create the gateway of a device.

    The rulebase of the topmost parent policy is the initial one. The rulebases of the child policies follow as
    sections, since Azure processes inherited and own rules as one sequence in which the first match wins.

    Args:
        device: The device.
        policy_chain: The policy of the device with its parent policies, the topmost parent first.

    Returns:
        Gateway: The gateway.

    """
    rulebase_links = [
        RulebaseLinkUidBased(
            to_rulebase_uid=policy_chain[0].name,
            link_type="ordered",
            is_initial=True,
            is_global=False,
            is_section=False,
        )
    ]
    rulebase_links += [
        RulebaseLinkUidBased(
            from_rulebase_uid=policy_chain[index - 1].name,
            to_rulebase_uid=policy_chain[index].name,
            link_type="concatenated",
            is_initial=False,
            is_global=False,
            is_section=True,
        )
        for index in range(1, len(policy_chain))
    ]
    return Gateway(
        Uid=device.uid,
        Name=device.name,
        Routing=[],
        RulebaseLinks=rulebase_links,
        GlobalPolicyUid=None,
        EnforcedPolicyUids=[],
        EnforcedNatPolicyUids=[],
        ImportDisabled=False,
        ShowInUI=True,
    )

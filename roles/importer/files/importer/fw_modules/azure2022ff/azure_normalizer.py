from __future__ import annotations

from typing import TYPE_CHECKING

import fwo_const
from fw_modules.azure2022ff.azure_network import normalize_ip_groups
from fw_modules.azure2022ff.azure_rule import AzureRuleNormalizer
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from models.rulebase import Rulebase
from models.rulebase_link import RulebaseLinkUidBased

if TYPE_CHECKING:
    from fw_modules.azure2022ff.azure_models import AzureConfig, AzureDevice
    from models.networkobject import NetworkObject
    from models.rule import RuleNormalized
    from models.serviceobject import ServiceObject


def normalize_config(
    native_config: AzureConfig, devices: list[AzureDevice], mgm_uid: str, skip_unsupported_rules: bool = True
) -> FwConfigNormalized:
    """
    Normalize the native Azure configuration.

    Each firewall policy becomes one rulebase, each device a gateway linked to the rulebase of its policy.

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

    policies = {policy.name: policy for policy in native_config.firewall_policies}
    rulebases: list[Rulebase] = []
    for policy_name in sorted({device.policy_name for device in devices}):
        policy = policies.get(policy_name)
        if policy is None:
            raise FwoImporterError(f"Azure firewall policy '{policy_name}' not found in native config.")
        rule_installon = fwo_const.LIST_DELIMITER.join(
            device.name for device in devices if device.policy_name == policy_name
        )
        rules: dict[str, RuleNormalized] = {}
        for rule in rule_normalizer.normalize_policy(policy, rule_installon):
            if rule.rule_uid in rules:
                raise FwoImporterError(f"Duplicate Azure rule '{rule.rule_uid}'.")
            rules[str(rule.rule_uid)] = rule
        rulebases.append(Rulebase(uid=policy_name, name=policy_name, mgm_uid=mgm_uid, is_global=False, rules=rules))

    normalized_config = FwConfigNormalized()
    normalized_config.network_objects = network_objects
    normalized_config.service_objects = service_objects
    normalized_config.rulebases = rulebases
    normalized_config.gateways = [create_gateway(device) for device in devices]
    FWOLogger.debug(
        f"Normalized {len(network_objects)} network objects, {len(service_objects)} service objects "
        f"and {sum(len(rb.rules) for rb in rulebases)} rules."
    )
    return normalized_config


def create_gateway(device: AzureDevice) -> Gateway:
    rulebase_link = RulebaseLinkUidBased(
        to_rulebase_uid=device.policy_name,
        link_type="ordered",
        is_initial=True,
        is_global=False,
        is_section=False,
    )
    return Gateway(
        Uid=device.uid,
        Name=device.name,
        Routing=[],
        RulebaseLinks=[rulebase_link],
        GlobalPolicyUid=None,
        EnforcedPolicyUids=[],
        EnforcedNatPolicyUids=[],
        ImportDisabled=False,
        ShowInUI=True,
    )

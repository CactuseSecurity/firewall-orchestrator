from __future__ import annotations

from typing import TYPE_CHECKING

from fw_modules.guardicoremanagementREST.gc_network import GcNetworkObjectCollector
from fw_modules.guardicoremanagementREST.gc_rule import build_rulebase_links, get_rule_installon, normalize_rules
from fwo_log import FWOLogger
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from models.label import LabelNormalized

if TYPE_CHECKING:
    from fw_modules.guardicoremanagementREST.gc_models import GuardicoreConfig
    from model_controllers.management_controller import ManagementController
    from models.serviceobject import ServiceObject


def normalize_labels(native_config: GuardicoreConfig) -> list[LabelNormalized]:
    return [LabelNormalized(key_name=label.key, value=label.value) for label in native_config.labels]


def normalize_config(native_config: GuardicoreConfig, mgm_details: ManagementController) -> FwConfigNormalized:
    """
    Normalize a Guardicore configuration.

    Labels become network object groups of their IP addresses (from IP criteria and from the assets carrying the
    label) and are also written to the labelling tables. Rules are split into chained rulebases per section and
    ruleset.

    Args:
        native_config (GuardicoreConfig): The native Guardicore configuration.
        mgm_details (ManagementController): The management details object.

    Returns:
        FwConfigNormalized: The normalized configuration.

    """
    normalized_config = FwConfigNormalized()
    collector = GcNetworkObjectCollector(native_config)
    collector.add_label_groups()

    service_objects: dict[str, ServiceObject] = {}
    rulebases = normalize_rules(native_config, collector, mgm_details, service_objects)

    normalized_config.network_objects = collector.network_objects
    normalized_config.service_objects = service_objects
    normalized_config.labels = normalize_labels(native_config)
    normalized_config.rulebases = rulebases

    gateway_name = get_rule_installon(mgm_details)
    normalized_config.gateways = [
        Gateway(
            Uid=gateway_name,
            Name=gateway_name,
            Routing=[],
            RulebaseLinks=build_rulebase_links(rulebases),
            GlobalPolicyUid=None,
            EnforcedPolicyUids=[],
            EnforcedNatPolicyUids=[],
            ImportDisabled=False,
            ShowInUI=True,
        )
    ]
    FWOLogger.debug(
        f"normalized {len(normalized_config.network_objects)} network objects, {len(service_objects)} services, "
        f"{len(normalized_config.labels)} labels and {len(rulebases)} rulebases"
    )
    return normalized_config

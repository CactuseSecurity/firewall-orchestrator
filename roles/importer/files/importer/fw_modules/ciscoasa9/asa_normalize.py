"""ASA Configuration Normalization

This module handles the top-level normalization of ASA configurations,
orchestrating the conversion from native ASA format to the normalized
format used by the firewall orchestrator.
"""

from fwo_log import FWOLogger
from models.fwconfig_normalized import FwConfigNormalized
from fw_modules.ciscoasa9.asa_models import Config
from fwo_enums import ConfigAction
from models.gateway import Gateway
from models.rulebase_link import RulebaseLinkUidBased
from fw_modules.ciscoasa9.asa_rule import build_rulebases_from_access_lists
from models.networkobject import NetworkObject
from models.serviceobject import ServiceObject
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController

# Import the new modular functions
from fw_modules.ciscoasa9.asa_network import (
    normalize_names,
    normalize_network_objects, 
    normalize_network_object_groups
)
from fw_modules.ciscoasa9.asa_service import (
    normalize_service_objects,
    create_protocol_any_service_objects,
    normalize_service_object_groups
)





def normalize_all_network_objects(native_config: Config) -> dict[str, NetworkObject]:
    """
    Normalize all network objects from the native ASA configuration.

    This function processes:
    - Named hosts (from 'names' command)
    - Network objects (hosts and subnets)
    - Network object groups

    Args:
        native_config: Parsed ASA configuration containing network objects.
        logger: Logger instance for warnings and debug messages.

    Returns:
        Dictionary of normalized network objects keyed by obj_uid.
    """
    # Start with names (simple IP-to-name mappings)
    network_objects = normalize_names(native_config.names)

    # Add individual network objects
    network_objects.update(normalize_network_objects(native_config.objects))

    # Add network object groups
    normalize_network_object_groups(native_config.object_groups, network_objects)

    return network_objects


def normalize_all_service_objects(native_config: Config) -> dict[str, ServiceObject]:
    """
    Normalize all service objects from the native ASA configuration.

    This function processes:
    - Individual service objects (with specific ports or port ranges)
    - Default 'any' service objects for common protocols
    - Service object groups (including mixed protocol groups)

    Args:
        native_config: Parsed ASA configuration containing service objects.

    Returns:
        Dictionary of normalized service objects keyed by svc_uid.
    """
    # Start with individual service objects
    service_objects = normalize_service_objects(native_config.service_objects)

    # Add default 'any' protocol service objects
    service_objects.update(create_protocol_any_service_objects())

    # Add service object groups
    normalize_service_object_groups(native_config.service_object_groups, service_objects)

    return service_objects


def normalize_config(config_in: FwConfigManagerListController, import_state: ImportStateController) -> FwConfigManagerListController:
    """
    Normalize the ASA configuration into a structured format for the database.

    This function orchestrates the normalization process:
    1. Parse the native configuration
    2. Normalize network objects (hosts, networks, groups)
    3. Normalize service objects (ports, protocols, groups)
    4. Build rulebases from access lists (including inline object creation)
    5. Create gateway and rulebase links
    6. Construct the final normalized configuration

    Args:
        config_in: Configuration input details containing native config.
        importState: Current import state with management details.

    Returns:
        Updated config_in with normalized configuration.
    """

    # Parse the native configuration into structured objects
    native_config: Config = Config.model_validate(config_in.native_config)

    # Step 1: Normalize network objects (names, objects, object-groups)
    FWOLogger.debug("Normalizing network objects...")
    network_objects = normalize_all_network_objects(native_config)

    # Step 2: Normalize service objects (service objects with ports/protocols)
    FWOLogger.debug("Normalizing service objects...")
    service_objects = normalize_all_service_objects(native_config)

    # Step 3: Build rulebases from access lists (this will create additional objects as needed)
    FWOLogger.debug("Building rulebases from access lists...")
    rulebases = build_rulebases_from_access_lists(
        native_config.access_lists,
        import_state.mgm_details.uid,
        protocol_groups=native_config.protocol_groups,
        network_objects=network_objects,
        service_objects=service_objects
    )

    # Step 4: Create rulebase links (ordered chain of rulebases)
    rulebase_links: list[RulebaseLinkUidBased] = []
    if len(rulebases) > 0:
        # First rulebase is the initial entry point
        rulebase_links.append(RulebaseLinkUidBased(
            to_rulebase_uid=rulebases[0].uid,
            link_type="ordered",
            is_initial=True,
            is_global=False,
            is_section=False
        ))
        # Link subsequent rulebases in order
        for idx in range(1, len(rulebases)):
            rulebase_links.append(RulebaseLinkUidBased(
                from_rulebase_uid=rulebases[idx-1].uid,
                to_rulebase_uid=rulebases[idx].uid,
                link_type="ordered",
                is_initial=False,
                is_global=False,
                is_section=False
            ))

    # Step 5: Create gateway object representing the ASA device
    FWOLogger.debug("Creating gateway object...")
    gateway = Gateway(
        Uid=native_config.hostname,
        Name=native_config.hostname,
        Routing=[],
        RulebaseLinks=rulebase_links,
        GlobalPolicyUid=None,
        EnforcedPolicyUids=[],
        EnforcedNatPolicyUids=[],
        ImportDisabled=False,
        ShowInUI=True
    )

    # Step 6: Construct the normalized configuration
    FWOLogger.debug("Constructing normalized configuration...")
    normalized_config = FwConfigNormalized(
        action=ConfigAction.INSERT,
        network_objects=network_objects,
        service_objects=service_objects,
        zone_objects={},  # ASA doesn't use zones like other firewalls
        rulebases=rulebases,
        gateways=[gateway]
    )

    # Update the configuration input with normalized data
    config_in.ManagerSet[0].configs = [normalized_config]
    config_in.ManagerSet[0].manager_uid = import_state.mgm_details.uid

    return config_in
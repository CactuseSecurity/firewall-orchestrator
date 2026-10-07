"""
ASA Rule and Rulebase Management

This module handles the creation of rules and rulebases from ASA access lists.
It processes ACL entries and converts them into normalized rules with proper
service, source, and destination references.
"""

import fwo_base
import fwo_const
from fw_modules.ciscoasa9.asa_models import AccessList, AccessListEntry, AsaProtocolGroup, EndpointKind
from fw_modules.ciscoasa9.asa_network import get_network_rule_endpoint, get_subnet_endpoint_address
from fw_modules.ciscoasa9.asa_service import (
    create_service_for_acl_entry,
    create_service_for_protocol_entry,
    restrict_service_to_protocol,
)
from fwo_log import FWOLogger
from models.networkobject import NetworkObject
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType
from models.rulebase import Rulebase
from models.serviceobject import ServiceObject
from netaddr import IPNetwork


def create_service_for_protocol_group_entry(
    entry: AccessListEntry, protocol_groups: list[AsaProtocolGroup], service_objects: dict[str, ServiceObject]
) -> str:
    """
    Resolve the service reference of an entry whose protocol is a protocol group.

    Every protocol of the group gets the destination of the entry (e.g. "object-group TCPUDP any any eq domain" is
    domain on tcp and on udp). A service group as destination only applies to the protocols it has services of.

    Args:
        entry: Access list entry with a protocol group as protocol
        protocol_groups: List of protocol groups for resolving references
        service_objects: Dictionary of service objects to update if needed

    Returns:
        Service reference string

    Raises:
        ValueError: if the protocol group is unknown or the destination applies to none of its protocols

    """
    protocol_group = next((pg for pg in protocol_groups if pg.name == entry.protocol.value), None)
    if protocol_group is None or not protocol_group.protocols:
        raise ValueError(f"Protocol group '{entry.protocol.value}' is unknown or empty.")

    svc_refs: list[str] = []
    for proto in protocol_group.protocols:
        if entry.dst_port.kind in ("service", "service-group"):
            restricted = restrict_service_to_protocol(entry.dst_port.value, proto, service_objects)
            if restricted is not None:
                svc_refs.append(restricted)
            continue
        single_protocol_entry = entry.model_copy(update={"protocol": EndpointKind(kind="protocol", value=proto)})
        svc_refs.extend(
            create_service_for_protocol_entry(single_protocol_entry, service_objects).split(fwo_const.LIST_DELIMITER)
        )
    if not svc_refs:
        raise ValueError(f"Service '{entry.dst_port.value}' contains no service of protocol group '{entry.protocol.value}'.")
    return fwo_base.sort_and_join(svc_refs)


def resolve_service_reference_for_rule(
    entry: AccessListEntry, protocol_groups: list[AsaProtocolGroup], service_objects: dict[str, ServiceObject]
) -> str:
    """
    Resolve service reference for a rule entry.

    Args:
        entry: Access list entry
        protocol_groups: List of protocol groups for resolving protocol-group references
        service_objects: Dictionary of service objects to update if needed

    Returns:
        Service reference string

    """
    if entry.protocol.kind == "protocol-group":
        # Protocol group - resolve to list of protocols
        return create_service_for_protocol_group_entry(entry, protocol_groups, service_objects)
    # Handle other protocol types using existing function
    return create_service_for_acl_entry(entry, service_objects)


def resolve_network_reference_for_rule(endpoint: EndpointKind, network_objects: dict[str, NetworkObject]) -> str:
    """
    Resolve network reference for a rule endpoint.

    Args:
        endpoint: Access list entry endpoint (src or dst)
        network_objects: Dictionary of network objects to update if needed

    Returns:
        Network reference string

    """
    # Create network object if needed and get reference
    network_obj = get_network_rule_endpoint(endpoint, network_objects)

    # Return reference - convert subnet mask to CIDR if present (the address may be a name alias)
    if hasattr(endpoint, "mask") and endpoint.mask is not None:
        return str(IPNetwork(f"{get_subnet_endpoint_address(endpoint, network_objects)}/{endpoint.mask}"))
    return network_obj.obj_uid


def create_rule_from_acl_entry(
    access_list_name: str,
    entry: AccessListEntry,
    protocol_groups: list[AsaProtocolGroup],
    network_objects: dict[str, NetworkObject],
    service_objects: dict[str, ServiceObject],
) -> RuleNormalized:
    """
    Create a normalized rule from an ACL entry.

    Args:
        access_list_name: Name of the access list
        idx: Rule index (1-based)
        entry: Access list entry to convert
        protocol_groups: List of protocol groups for resolving references
        network_objects: Dictionary of network objects to update if needed
        service_objects: Dictionary of service objects to update if needed

    Returns:
        Normalized rule object

    """
    # Generate unique rule UID by hashing entry dict; time_range is left out when unset, so that
    # the UIDs of entries without a time-range stay the same as before the field existed
    rule_uid = fwo_base.generate_hash_from_dict(
        entry.model_dump(exclude={"time_range"}) if entry.time_range is None else entry.model_dump()
    )

    # Resolve service reference
    svc_ref = resolve_service_reference_for_rule(entry, protocol_groups, service_objects)

    # Resolve source and destination references
    src_ref = resolve_network_reference_for_rule(entry.src, network_objects)
    dst_ref = resolve_network_reference_for_rule(entry.dst, network_objects)

    # Create normalized rule
    return RuleNormalized(
        rule_num_numeric=0,  # will be set later
        rule_disabled=entry.inactive,
        rule_src_neg=False,
        rule_src=src_ref,
        rule_src_refs=src_ref,
        rule_dst_neg=False,
        rule_dst=dst_ref,
        rule_dst_refs=dst_ref,
        rule_svc_neg=False,
        rule_svc=svc_ref,
        rule_svc_refs=svc_ref,
        rule_action=RuleAction.ACCEPT if entry.action == "permit" else RuleAction.DROP,
        rule_track=RuleTrack.NONE,
        rule_installon=None,  # gateway_uid, TODO: commented out for now to avoid duplication issues
        rule_time=entry.time_range,
        rule_name=access_list_name,
        rule_uid=rule_uid,
        rule_custom_fields=None,
        rule_implied=False,
        rule_type=RuleType.ACCESS,
        last_change_admin=None,
        parent_rule_uid=None,
        last_hit=None,
        rule_comment=entry.description,
        rule_src_zone=None,
        rule_dst_zone=None,
        rule_head_text=None,
    )


def build_rulebases_from_access_lists(
    access_lists: list[AccessList],
    mgm_uid: str,
    protocol_groups: list[AsaProtocolGroup],
    network_objects: dict[str, NetworkObject],
    service_objects: dict[str, ServiceObject],
) -> list[Rulebase]:
    """
    Build rulebases from ASA access lists.

    Each access list becomes a separate rulebase containing normalized rules.
    Rules are created from ACL entries with proper service, source, and destination references.

    Args:
        access_lists: List of parsed ASA access lists
        mgm_uid: Management UID for the device
        protocol_groups: List of protocol groups for resolving protocol-group references
        network_objects: Dictionary of network objects to update if needed
        service_objects: Dictionary of service objects to update if needed

    Returns:
        List of normalized rulebases

    """
    rulebases: list[Rulebase] = []

    for access_list in access_lists:
        rules: dict[str, RuleNormalized] = {}

        for entry in access_list.entries:
            rule = create_rule_from_acl_entry(
                access_list.name, entry, protocol_groups, network_objects, service_objects
            )
            if rule.rule_uid is None:
                FWOLogger.error(f"Failed to create rule UID for ACL entry: {entry}")
                raise ValueError("Rule UID generation failed.")
            rules[rule.rule_uid] = rule

        # Create rulebase for this access list
        rulebase = Rulebase(uid=access_list.name, name=access_list.name, mgm_uid=mgm_uid, is_global=False, rules=rules)
        rulebases.append(rulebase)

    return rulebases

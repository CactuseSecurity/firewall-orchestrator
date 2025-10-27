"""ASA Rule and Rulebase Management

This module handles the creation of rules and rulebases from ASA access lists.
It processes ACL entries and converts them into normalized rules with proper
service, source, and destination references.
"""

from typing import List, Dict
from netaddr import IPNetwork
from models.rule import RuleNormalized, RuleAction, RuleTrack, RuleType
from models.rulebase import Rulebase
from ciscoasa9.asa_models import AccessList, AccessListEntry, AsaProtocolGroup
from ciscoasa9.asa_service import create_service_for_acl_entry, create_any_protocol_service
from ciscoasa9.asa_network import get_network_rule_endpoint
from fwo_log import getFwoLogger
import fwo_base


def resolve_service_reference_for_rule(entry: AccessListEntry, protocol_groups: List[AsaProtocolGroup], service_objects: Dict) -> str:
    """Resolve service reference for a rule entry.

    Args:
        entry: Access list entry
        protocol_groups: List of protocol groups for resolving protocol-group references
        service_objects: Dictionary of service objects to update if needed

    Returns:
        Service reference string
    """
    if entry.protocol.kind == "protocol-group":
        # Protocol group - resolve to list of protocols
        allowed_protocols = []
        for pg in protocol_groups:
            if pg.name == entry.protocol.value:
                allowed_protocols = pg.protocols
                break

        if allowed_protocols:
            svc_refs = []
            for proto in allowed_protocols:
                svc_ref = create_any_protocol_service(proto, service_objects)
                svc_refs.append(svc_ref)
            return fwo_base.sort_and_join(svc_refs)
        else:
            # Fallback if protocol group not found
            logger = getFwoLogger()
            logger.warning(f"Protocol group '{entry.protocol.value}' not found. Defaulting to tcp/udp/icmp any.")
            svc_refs = []
            for proto in ("tcp", "udp", "icmp"):
                svc_refs.append(create_any_protocol_service(proto, service_objects))
            return fwo_base.sort_and_join(svc_refs)
    else:
        # Handle other protocol types using existing function
        return create_service_for_acl_entry(entry, service_objects)


def resolve_network_reference_for_rule(endpoint, network_objects: Dict) -> str:
    """Resolve network reference for a rule endpoint.

    Args:
        endpoint: Access list entry endpoint (src or dst)
        network_objects: Dictionary of network objects to update if needed

    Returns:
        Network reference string
    """
    # Create network object if needed and get reference
    network_obj = get_network_rule_endpoint(endpoint, network_objects)

    # Return reference - convert subnet mask to CIDR if present
    if hasattr(endpoint, 'mask') and endpoint.mask is not None:
        return str(IPNetwork(f"{endpoint.value}/{endpoint.mask}"))
    else:
        return network_obj.obj_uid


def create_rule_from_acl_entry(access_list_name: str, idx: int, entry: AccessListEntry, 
                              protocol_groups: List[AsaProtocolGroup], 
                              network_objects: Dict, service_objects: Dict,
                              gateway_uid: str) -> RuleNormalized:
    """Create a normalized rule from an ACL entry.

    Args:
        access_list_name: Name of the access list
        idx: Rule index (1-based)
        entry: Access list entry to convert
        protocol_groups: List of protocol groups for resolving references
        network_objects: Dictionary of network objects to update if needed
        service_objects: Dictionary of service objects to update if needed
        gateway_uid: UID of the gateway object representing the ASA device

    Returns:
        Normalized rule object
    """
    rule_uid = f"{access_list_name}-{idx:04d}"

    # Resolve service reference
    svc_ref = resolve_service_reference_for_rule(entry, protocol_groups, service_objects)

    # Resolve source and destination references
    src_ref = resolve_network_reference_for_rule(entry.src, network_objects)
    dst_ref = resolve_network_reference_for_rule(entry.dst, network_objects)

    # Create normalized rule
    rule = RuleNormalized(
        rule_num=idx,
        rule_num_numeric=float(idx),
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
        rule_installon=gateway_uid,
        rule_time="",
        rule_name=f"{access_list_name}-{idx:03d}",
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
        rule_head_text=None
    )

    return rule


def build_rulebases_from_access_lists(access_lists: List[AccessList], mgm_uid: str, 
                                     protocol_groups: List[AsaProtocolGroup],
                                     network_objects: Dict, service_objects: Dict,
                                     gateway_uid: str) -> List[Rulebase]:
    """Build rulebases from ASA access lists.

    Each access list becomes a separate rulebase containing normalized rules.
    Rules are created from ACL entries with proper service, source, and destination references.

    Args:
        access_lists: List of parsed ASA access lists
        mgm_uid: Management UID for the device
        protocol_groups: List of protocol groups for resolving protocol-group references
        network_objects: Dictionary of network objects to update if needed
        service_objects: Dictionary of service objects to update if needed
        gateway_uid: UID of the gateway object representing the ASA device

    Returns:
        List of normalized rulebases
    """
    rulebases = []

    for access_list in access_lists:
        rules = {}

        for idx, entry in enumerate(access_list.entries, start=1):
            rule = create_rule_from_acl_entry(
                access_list.name, 
                idx, 
                entry, 
                protocol_groups, 
                network_objects, 
                service_objects,
                gateway_uid
            )
            rules[rule.rule_uid] = rule

        # Create rulebase for this access list
        rulebase = Rulebase(
            uid=access_list.name,
            name=access_list.name,
            mgm_uid=mgm_uid,
            is_global=False,
            Rules=rules
        )
        rulebases.append(rulebase)

    return rulebases

"""ASA Network Object Management

This module handles the normalization of network objects from ASA configurations.
It manages both explicit network objects/groups and implicit network objects created from
inline ACL or group definitions.
"""

from typing import Dict, List, Optional
from netaddr import IPAddress, IPNetwork
from ciscoasa9.asa_models import AsaNetworkObject, AsaNetworkObjectGroup, AsaNetworkObjectGroupMember, EndpointKind, Names
from models.networkobject import NetworkObject
import fwo_const
import fwo_base


def create_network_host(name: str, ip_address: str, comment: Optional[str], ip_version: int) -> NetworkObject:
    """Create a normalized host network object.

    Args:
        name: Object name/UID
        ip_address: IP address
        comment: Optional description
        ip_version: IP version (4 or 6)

    Returns:
        Normalized NetworkObject instance
    """
    if ip_version == 6:
        obj_ip = IPNetwork(f"{ip_address}/128", version=6)
    else:
        obj_ip = IPNetwork(f"{ip_address}/32")
    return NetworkObject(
        obj_uid=name,
        obj_name=name,
        obj_typ="host",
        obj_ip=obj_ip,
        obj_ip_end=obj_ip,
        obj_color=fwo_const.defaultColor,
        obj_comment=comment
    )


def create_network_subnet(name: str, ip_address: str, subnet_mask: Optional[str], comment: Optional[str], ip_version: int) -> NetworkObject:
    """Create a normalized network object.

    Args:
        name: Object name/UID
        ip_address: Network address
        subnet_mask: Subnet mask
        comment: Optional description
        ip_version: IP version (4 or 6)

    Returns:
        Normalized NetworkObject instance
    """
    if ip_version == 6:
        # ip_address is expected to be in CIDR notation for IPv6
        network = IPNetwork(ip_address, version=6)
        ip_start = IPNetwork(f"{IPAddress(network.first)}/128", version=6)
        ip_end = IPNetwork(f"{IPAddress(network.last)}/128", version=6)
    else:
        if subnet_mask is None:
            raise ValueError("Subnet mask is required for IPv4 subnet objects.")
        network = IPNetwork(f"{ip_address}/{subnet_mask}")
        ip_start = IPNetwork(f"{ip_address}/32")
        ip_end = IPNetwork(f"{IPAddress(network.first + network.size - 1)}/32")

    return NetworkObject(
        obj_uid=name,
        obj_name=name,
        obj_typ="network",
        obj_ip=ip_start,
        obj_ip_end=ip_end,
        obj_color=fwo_const.defaultColor,
        obj_comment=comment
    )


def create_network_range(name: str, ip_start: str, ip_end: str, comment: Optional[str]) -> NetworkObject:
    """Create a normalized range network object.

    Args:
        name: Object name/UID
        ip_start: Start IP address
        ip_end: End IP address
        comment: Optional description

    Returns:
        Normalized NetworkObject instance
    """
    return NetworkObject(
        obj_uid=name,
        obj_name=name,
        obj_typ="ip_range",
        obj_ip=IPNetwork(f"{ip_start}/32"),
        obj_ip_end=IPNetwork(f"{ip_end}/32"),
        obj_color=fwo_const.defaultColor,
        obj_comment=comment
    )


def create_network_group_object(name: str, member_refs: List[str], comment: Optional[str] = None) -> NetworkObject:
    """Create a network group object.

    Args:
        name: Group name/UID
        member_refs: List of member network object references
        comment: Optional description

    Returns:
        Normalized NetworkObject group instance
    """
    return NetworkObject(
        obj_uid=name,
        obj_name=name,
        obj_typ="group",
        obj_member_names=fwo_base.sort_and_join(member_refs),
        obj_member_refs=fwo_base.sort_and_join(member_refs),
        obj_color=fwo_const.defaultColor,
        obj_comment=comment
    )


def create_any_network_object() -> NetworkObject:
    """Create the special 'any' network object representing all addresses.

    Returns:
        Normalized NetworkObject for 'any'
    """
    return NetworkObject(
        obj_uid="any",
        obj_name="any",
        obj_typ="network",
        obj_member_names="",
        obj_member_refs="",
        obj_ip=IPNetwork("0.0.0.0/32"),
        obj_ip_end=IPNetwork("255.255.255.255/32"),
        obj_color=fwo_const.defaultColor,
        obj_comment="network object created during import"
    )


def normalize_names(names: List[Names]) -> Dict[str, NetworkObject]:
    """Normalize 'names' entries (simple IP-to-name mappings).

    Args:
        names: List of Names objects from ASA configuration

    Returns:
        Dictionary of normalized network objects keyed by obj_uid
    """
    network_objects = {}

    for name in names:
        obj = create_network_host(name.name, name.ip_address, name.description, ip_version=4)
        network_objects[name.name] = obj

    return network_objects


def normalize_network_objects(network_objects_list: List[AsaNetworkObject], logger) -> Dict[str, NetworkObject]:
    """Normalize network objects from ASA configuration.

    Args:
        network_objects_list: List of AsaNetworkObject instances
        logger: Logger instance for warnings

    Returns:
        Dictionary of normalized network objects keyed by obj_uid
    """
    network_objects = {}

    for obj in network_objects_list:
        if obj.fqdn is not None:
            # handle FQDN objects as empty group for now /TODO
            network_obj = create_network_group_object(obj.name, [], obj.description)
            network_objects[obj.name] = network_obj
        elif obj.ip_address and obj.subnet_mask:
            # Network object with subnet mask
            network_obj = create_network_subnet(obj.name, obj.ip_address, obj.subnet_mask, obj.description, ip_version=4)
            network_objects[obj.name] = network_obj
        elif obj.ip_address and obj.ip_address_end:
            network_obj = create_network_range(obj.name, obj.ip_address, obj.ip_address_end, obj.description)
            network_objects[obj.name] = network_obj
        elif obj.ip_address:
            # Host object (single IP address)
            network_obj = create_network_host(obj.name, obj.ip_address, obj.description, ip_version=4)
            network_objects[obj.name] = network_obj

    return network_objects


def normalize_network_object_groups(object_groups: List[AsaNetworkObjectGroup], 
                                   network_objects: Dict[str, NetworkObject], 
                                   logger) -> Dict[str, NetworkObject]:
    """Normalize network object groups from ASA configuration.

    Args:
        object_groups: List of AsaNetworkObjectGroup instances
        network_objects: Existing network objects dictionary to update
        logger: Logger instance for warnings

    Returns:
        Updated network objects dictionary including groups
    """
    for group in object_groups:
        member_refs = []

        for member in group.objects:
            try:
                # Use the modular function to create/get the member object
                network_obj = get_network_group_member(member, network_objects)

                # Add the reference to the member list
                member_refs.append(network_obj.obj_uid)

            except ValueError as e:
                logger.warning(f"Error processing member in network object group '{group.name}': {e}")

        group_obj = create_network_group_object(group.name, member_refs, group.description)
        network_objects[group.name] = group_obj

    return network_objects


def get_network_group_member(member: AsaNetworkObjectGroupMember, network_objects: Dict[str, NetworkObject]) -> NetworkObject:
    """Get network object for a network object group member reference. If it does not exist, create it.

    Args:
        member: Network object group member
        network_objects: Dictionary of existing network objects

    Returns:
        NetworkObject instance
    """
    network_object = None
    if member.kind == "host":
        ref = member.value
        if ref in network_objects:
            return network_objects[ref]
        network_object = create_network_host(ref, member.value, None, ip_version=4)
    elif member.kind == "hostv6":
        ref = member.value
        if ref in network_objects:
            return network_objects[ref]
        network_object = create_network_host(ref, member.value, None, ip_version=6)
    elif member.kind == "subnet":
        ref = f"{member.value}/{member.mask}"
        if ref in network_objects:
            return network_objects[ref]
        if member.mask is None:
            raise ValueError("Subnet mask is required for subnet member kind.")
        network_object = create_network_subnet(ref, member.value, member.mask, None, ip_version=4)
    elif member.kind == "subnetv6":
        # member.value is already in CIDR notation for IPv6
        ref = member.value
        if ref in network_objects:
            return network_objects[ref]
        network_object = create_network_subnet(ref, member.value, None, None, ip_version=6)
    elif member.kind in ("object", "object-group"):
        # Reference to existing object or object-group - assume it already exists
        ref = member.value
        ref_obj = network_objects.get(ref)
        if not ref_obj:
            raise ValueError(f"Referenced network object '{ref}' not found in configuration.")
        return ref_obj
    else:
        raise ValueError(f"Unsupported member kind '{member.kind}' in network object group.")

    network_objects[network_object.obj_uid] = network_object
    return network_object


def get_network_rule_endpoint(endpoint: EndpointKind, network_objects: Dict[str, NetworkObject]) -> NetworkObject:
    """Get network object for a rule endpoint. If it does not exist, create it.

    Args:
        endpoint: Rule endpoint (src or dst)
        network_objects: Dictionary of existing network objects

    Returns:
        NetworkObject instance
    """
    network_object = None
    if endpoint.kind == "host":
        # Single host IP (e.g., 'host 10.0.0.1')
        ref = endpoint.value
        if ref in network_objects:
            return network_objects[ref]
        network_object = create_network_host(endpoint.value, endpoint.value, None, ip_version=4)
    elif endpoint.kind == "subnet":
        # Subnet with mask (e.g., '10.0.0.0 255.255.255.0')
        # Object name is subnet in CIDR notation
        ref = str(IPNetwork(f"{endpoint.value}/{endpoint.mask}"))
        if ref in network_objects:
            return network_objects[ref]
        if endpoint.mask is None:
            raise ValueError("Subnet mask is required for subnet endpoint kind.")
        network_object = create_network_subnet(ref, endpoint.value, endpoint.mask, None, ip_version=4)
    elif endpoint.kind == "any":
        # 'any' keyword (0.0.0.0 - 255.255.255.255)
        if "any" in network_objects:
            return network_objects["any"]
        network_object = create_any_network_object()
    elif endpoint.kind in ("object", "object-group"):
        # Reference to existing object or object-group - assume it already exists
        ref = endpoint.value
        ref_obj = network_objects.get(ref)
        if not ref_obj:
            raise ValueError(f"Referenced network object '{ref}' not found in configuration.")
        return ref_obj
    else:
        raise ValueError(f"Unknown endpoint kind: {endpoint.kind}")

    network_objects[network_object.obj_uid] = network_object
    return network_object
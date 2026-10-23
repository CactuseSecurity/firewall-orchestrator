from __future__ import annotations

from typing import TYPE_CHECKING

import fwo_const
from fw_modules.azure2022ff.azure_const import ANY_OBJECT_NAME, AZURE_ANY
from fwo_exceptions import FwoImporterError
from models.networkobject import NetworkObject
from netaddr import AddrFormatError, IPAddress, IPNetwork

if TYPE_CHECKING:
    from fw_modules.azure2022ff.azure_models import AzureIpGroup

IPV4_VERSION = 4
IPV4_HOST_BITS = 32
IPV6_HOST_BITS = 128


def _host_network(address: IPAddress) -> IPNetwork:
    host_bits = IPV4_HOST_BITS if address.version == IPV4_VERSION else IPV6_HOST_BITS
    return IPNetwork(f"{address}/{host_bits}")


def create_any_network_object() -> NetworkObject:
    """
    Create the network object representing Azure's '*' address.

    Returns:
        NetworkObject: The 'any' network object.

    """
    return NetworkObject(
        obj_uid=ANY_OBJECT_NAME,
        obj_name=ANY_OBJECT_NAME,
        obj_typ="network",
        obj_ip=IPNetwork(fwo_const.ANY_IP_START),
        obj_ip_end=IPNetwork(fwo_const.ANY_IP_END),
        obj_color=fwo_const.DEFAULT_COLOR,
        obj_comment="network object created during import",
    )


def _parse_ip_range(address: str) -> NetworkObject | None:
    start_raw, end_raw = address.split("-", 1)
    try:
        ip_start = IPAddress(start_raw.strip())
        ip_end = IPAddress(end_raw.strip())
    except (AddrFormatError, ValueError):
        return None
    if ip_start.version != ip_end.version or ip_start > ip_end:
        raise FwoImporterError(f"Invalid Azure IP range '{address}'.")
    return NetworkObject(
        obj_uid=address,
        obj_name=address,
        obj_typ="host" if ip_start == ip_end else "ip_range",
        obj_ip=_host_network(ip_start),
        obj_ip_end=_host_network(ip_end),
        obj_color=fwo_const.DEFAULT_COLOR,
    )


def _parse_ip_network(address: str) -> NetworkObject | None:
    try:
        network = IPNetwork(address)
    except (AddrFormatError, ValueError):
        return None
    ip_start = IPAddress(network.first, network.version)
    ip_end = IPAddress(network.last, network.version)
    return NetworkObject(
        obj_uid=address,
        obj_name=address,
        obj_typ="network" if network.size > 1 else "host",
        obj_ip=_host_network(ip_start),
        obj_ip_end=_host_network(ip_end),
        obj_color=fwo_const.DEFAULT_COLOR,
    )


def parse_address(address: str) -> NetworkObject:
    """
    Normalize an Azure address string into a network object.

    Azure addresses are single IPs, CIDR prefixes, IP ranges ('a-b'), '*' or service tags.
    Service tags resolve to Microsoft-managed prefixes and are imported as dynamic objects.

    Args:
        address: The Azure address string.

    Returns:
        NetworkObject: The normalized network object, its uid is the address string.

    """
    address = address.strip()
    if address == AZURE_ANY:
        return create_any_network_object()
    nw_obj = _parse_ip_range(address) if "-" in address else None
    if nw_obj is None:
        nw_obj = _parse_ip_network(address)
    if nw_obj is not None:
        return nw_obj
    return NetworkObject(
        obj_uid=address,
        obj_name=address,
        obj_typ="dynamic_net_obj",
        obj_color=fwo_const.DEFAULT_COLOR,
        obj_comment="Azure service tag",
    )


def create_fqdn_object(fqdn: str) -> NetworkObject:
    return NetworkObject(
        obj_uid=fqdn,
        obj_name=fqdn,
        obj_typ="domain",
        obj_color=fwo_const.DEFAULT_COLOR,
    )


def add_network_object(network_objects: dict[str, NetworkObject], nw_obj: NetworkObject) -> NetworkObject:
    """Add a network object unless an object with the same uid exists, return the stored object."""
    return network_objects.setdefault(nw_obj.obj_uid, nw_obj)


def normalize_ip_groups(
    ip_groups: list[AzureIpGroup], network_objects: dict[str, NetworkObject]
) -> dict[str, NetworkObject]:
    """
    Normalize Azure IP groups into group objects with one member object per address.

    Args:
        ip_groups: The native Azure IP groups.
        network_objects: Collected network objects, member objects are added here.

    Returns:
        dict[str, NetworkObject]: The group objects by lowercased resource id.

    """
    groups: dict[str, NetworkObject] = {}
    for ip_group in ip_groups:
        members = [add_network_object(network_objects, parse_address(a)) for a in ip_group.properties.ip_addresses]
        # Azure resource ids are case-insensitive and rules may reference them with different casing
        group_uid = ip_group.id.lower()
        group = NetworkObject(
            obj_uid=group_uid,
            obj_name=ip_group.name,
            obj_typ="group",
            obj_member_names=fwo_const.LIST_DELIMITER.join(member.obj_name for member in members),
            obj_member_refs=fwo_const.LIST_DELIMITER.join(member.obj_uid for member in members),
            obj_color=fwo_const.DEFAULT_COLOR,
        )
        groups[group_uid] = add_network_object(network_objects, group)
    return groups


def resolve_ip_group(ip_group_id: str, ip_groups: dict[str, NetworkObject]) -> NetworkObject:
    """Look up a referenced IP group by its resource id, failing the import for unknown groups."""
    ip_group = ip_groups.get(ip_group_id.lower())
    if ip_group is None:
        raise FwoImporterError(f"Referenced Azure IP group '{ip_group_id}' not found in subscription.")
    return ip_group

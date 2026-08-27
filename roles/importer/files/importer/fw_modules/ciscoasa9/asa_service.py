"""
ASA Service Object Management

This module handles the creation and normalization of service objects from ASA configurations.
It manages both explicit service objects/groups and implicit service objects created from
inline ACL definitions.
"""

from __future__ import annotations

from typing import TYPE_CHECKING

import fwo_base
import fwo_const
from fw_modules.ciscoasa9.asa_maps import name_to_port, protocol_map
from fwo_log import FWOLogger
from models.serviceobject import ServiceObject

if TYPE_CHECKING:
    from fw_modules.ciscoasa9.asa_models import AccessListEntry, AsaServiceObject, AsaServiceObjectGroup


ASA_ANY_PROTOCOL_SERVICE_UID = "ANY"


def _get_ip_protocol_id(protocol: str) -> int:
    return fwo_const.ANY_IP_PROTOCOL_ID if protocol == "ip" else protocol_map.get(protocol, 0)


def create_service_object(
    name: str, port: int, port_end: int, protocol: str, comment: str | None = None
) -> ServiceObject:
    """
    Create a normalized service object.

    Args:
        name: Service object name/UID
        port: Start port number
        port_end: End port number
        protocol: Protocol name (tcp, udp, icmp, etc.)
        comment: Optional description

    Returns:
        Normalized ServiceObject instance

    """
    return ServiceObject(
        svc_uid=name,
        svc_name=name,
        svc_port=None if protocol == "ip" else port,
        svc_port_end=None if protocol == "ip" else port_end,
        svc_color=fwo_const.DEFAULT_COLOR,
        svc_typ="simple",
        ip_proto=_get_ip_protocol_id(protocol),
        svc_comment=comment,
    )


def create_protocol_service_object(name: str, protocol: str, comment: str | None = None) -> ServiceObject:
    """
    Create a service object for a protocol without specific ports.

    Args:
        name: Service object name/UID
        protocol: Protocol name
        comment: Optional description

    Returns:
        Normalized ServiceObject instance

    """
    return ServiceObject(
        svc_uid=name,
        svc_name=name,
        svc_color=fwo_const.DEFAULT_COLOR,
        svc_typ="simple",
        ip_proto=_get_ip_protocol_id(protocol),
        svc_comment=comment,
    )


def create_service_group_object(name: str, member_refs: list[str], comment: str | None = None) -> ServiceObject:
    """
    Create a service group object.

    Args:
        name: Group name/UID
        member_refs: List of member service object references
        comment: Optional description

    Returns:
        Normalized ServiceObject group instance

    """
    return ServiceObject(
        svc_uid=name,
        svc_name=name,
        svc_typ="group",
        svc_member_names=fwo_base.sort_and_join(member_refs),
        svc_member_refs=fwo_base.sort_and_join(member_refs),
        svc_color=fwo_const.DEFAULT_COLOR,
        svc_comment=comment,
    )


def normalize_service_objects(service_objects: list[AsaServiceObject]) -> dict[str, ServiceObject]:
    """
    Normalize individual service objects from ASA configuration.

    Args:
        service_objects: List of parsed ASA service objects

    Returns:
        Dictionary of normalized service objects keyed by svc_uid

    """
    normalized: dict[str, ServiceObject] = {}

    for svc in service_objects:
        if svc.dst_port_eq:
            # Service with specific port (eq)
            port = svc.dst_port_eq
            if not port.isdigit():
                port = name_to_port[port]["port"]

            obj = create_service_object(svc.name, int(port), int(port), svc.protocol, svc.description)
            normalized[svc.name] = obj

        elif svc.dst_port_range:
            # Service with port range
            start, end = svc.dst_port_range
            if not start.isdigit():
                start = name_to_port[start]["port"]
            if not end.isdigit():
                end = name_to_port[end]["port"]
            obj = create_service_object(svc.name, int(start), int(end), svc.protocol, svc.description)
            normalized[svc.name] = obj

        else:
            # Protocol-only service (no specific ports)
            obj = create_protocol_service_object(svc.name, svc.protocol, svc.description)
            normalized[svc.name] = obj

    return normalized


def _canonical_any_protocol(obj: ServiceObject) -> str | None:
    """
    Identify the protocol of a synthetic protocol-agnostic 'any-<proto>' service object, as
    opposed to a same-named object/group taken from the actual ASA configuration.

    Returns:
        The protocol name if obj is a canonical any-protocol service, else None

    """
    if obj.svc_typ != "simple" or not obj.svc_name.startswith("any-"):
        return None
    proto = obj.svc_name.removeprefix("any-")
    if proto not in ("tcp", "udp", "icmp", "ip") or obj.ip_proto != _get_ip_protocol_id(proto):
        return None
    expected_port, expected_port_end = (0, 65535) if proto in ("tcp", "udp") else (None, None)
    if obj.svc_port != expected_port or obj.svc_port_end != expected_port_end:
        return None
    return proto


def _preferred_any_protocol_uid(proto: str) -> str:
    return ASA_ANY_PROTOCOL_SERVICE_UID if proto == "ip" else f"any-{proto}"


def _find_canonical_any_protocol_service_uid(proto: str, service_objects: dict[str, ServiceObject]) -> str | None:
    """Return the UID currently holding the canonical any-<proto> object, wherever it lives."""
    for uid, obj in service_objects.items():
        if _canonical_any_protocol(obj) == proto:
            return uid
    return None


def _unused_any_protocol_conflict_uid(proto: str, service_objects: dict[str, ServiceObject]) -> str:
    """Probe for a free conflict-safe UID, reusing a slot that already holds the canonical object."""
    prefix = f"_FWO_ANY_{proto.upper()}_PROTOCOL_"
    uid = prefix
    suffix = 1
    while uid in service_objects and _canonical_any_protocol(service_objects[uid]) != proto:
        suffix += 1
        uid = f"{prefix}{suffix}"
    return uid


def _unused_any_protocol_uid(proto: str, service_objects: dict[str, ServiceObject]) -> str:
    """
    Resolve the UID for the synthetic 'any-<proto>' service object.

    Reuses the canonical object wherever it currently lives (it may have been relocated away
    from its preferred UID due to a name conflict), otherwise prefers the legacy/natural UID for
    backward compatibility with existing imports, falling back to a conflict-safe UID if an
    ASA-configured object/group already uses that exact name.

    Args:
        proto: Protocol name
        service_objects: Existing service objects dictionary

    Returns:
        A UID that either already holds the canonical object or is free to use

    """
    existing_uid = _find_canonical_any_protocol_service_uid(proto, service_objects)
    if existing_uid is not None:
        return existing_uid
    preferred_uid = _preferred_any_protocol_uid(proto)
    if preferred_uid not in service_objects:
        return preferred_uid
    return _unused_any_protocol_conflict_uid(proto, service_objects)


def _make_room_for_named_object(name: str, service_objects: dict[str, ServiceObject]) -> None:
    """
    Relocate a synthetic 'any-<proto>' service object out of the way before an ASA-configured
    object/group claims its name, so neither one silently overwrites the other.

    Args:
        name: The name an ASA-configured object/group is about to be stored under
        service_objects: Service objects dictionary to update in place

    """
    existing = service_objects.get(name)
    if existing is None:
        return
    proto = _canonical_any_protocol(existing)
    if proto is None:
        return
    del service_objects[name]
    relocated_uid = _unused_any_protocol_conflict_uid(proto, service_objects)
    existing.svc_uid = relocated_uid
    service_objects[relocated_uid] = existing


def create_protocol_any_service_objects(service_objects: dict[str, ServiceObject]) -> dict[str, ServiceObject]:
    """
    Create default 'any' service objects for common protocols.

    Args:
        service_objects: Existing service objects dictionary to update

    Returns:
        Updated service objects dictionary including the protocol-any service objects

    """
    for proto in ("tcp", "udp", "icmp", "ip"):
        is_any_ip_protocol = proto == "ip"
        obj_uid = _unused_any_protocol_uid(proto, service_objects)
        if obj_uid in service_objects:
            continue
        obj = ServiceObject(
            svc_uid=obj_uid,
            svc_name=f"any-{proto}",
            svc_port=None if is_any_ip_protocol else 0,
            svc_port_end=None if is_any_ip_protocol else 65535,
            svc_color=fwo_const.DEFAULT_COLOR,
            svc_typ="simple",
            ip_proto=_get_ip_protocol_id(proto),
            svc_comment=f"any {proto}",
        )
        service_objects[obj_uid] = obj

    return service_objects


def create_service_for_port(port: str, proto: str, service_objects: dict[str, ServiceObject]) -> str:
    """
    Create a service object for a single port and protocol if it doesn't exist.

    Args:
        port: Port number or name
        proto: Protocol name
        service_objects: Dictionary to update with new service object

    Returns:
        Service object name/UID

    """
    if proto == "icmp":
        obj = create_protocol_service_object(f"icmp-{port}", "icmp", None)
        service_objects[obj.svc_uid] = obj
        return obj.svc_uid
    obj_name = f"{port}-{proto}"
    if obj_name not in service_objects:
        description = None
        if not port.isdigit():
            description = name_to_port[port]["description"]
            port = name_to_port[port]["port"]
        obj = create_service_object(obj_name, int(port), int(port), proto, description)
        service_objects[obj_name] = obj
    return obj_name


def create_service_for_port_range(
    port_range: tuple[str, str], proto: str, service_objects: dict[str, ServiceObject]
) -> str:
    """
    Create a service object for a port range and protocol if it doesn't exist.

    Args:
        port_range: Tuple of (start_port, end_port)
        proto: Protocol name
        service_objects: Dictionary to update with new service object

    Returns:
        Service object name/UID

    """
    obj_name = (
        f"{port_range[0]}-{port_range[1]}-{proto}" if port_range[0] != port_range[1] else f"{port_range[0]}-{proto}"
    )
    if obj_name not in service_objects:
        start, end = port_range
        description = None
        if not start.isdigit():
            description = f"{start}: {name_to_port[start]['description']}"
            start = name_to_port[start]["port"]
        if not end.isdigit():
            if not description:
                description = f"{end}: {name_to_port[end]['description']}"
            else:
                description += f"; {end}: {name_to_port[end]['description']}"
            end = name_to_port[end]["port"]
        obj = create_service_object(obj_name, int(start), int(end), proto, description)
        service_objects[obj_name] = obj
    return obj_name


def create_any_protocol_service(proto: str, service_objects: dict[str, ServiceObject]) -> str:
    """
    Create an 'any' service object for a protocol if it doesn't exist.

    Args:
        proto: Protocol name
        service_objects: Dictionary to update with new service object

    Returns:
        Service object name/UID

    """
    obj_name = f"any-{proto}"
    obj_uid = _unused_any_protocol_uid(proto, service_objects)
    if obj_uid not in service_objects:
        port_range = (0, 65535) if proto in ("tcp", "udp") else (None, None)
        obj = ServiceObject(
            svc_uid=obj_uid,
            svc_name=obj_name,
            svc_port=port_range[0],
            svc_port_end=port_range[1],
            svc_color=fwo_const.DEFAULT_COLOR,
            svc_typ="simple",
            ip_proto=_get_ip_protocol_id(proto),
            svc_comment=f"any {proto}",
        )
        service_objects[obj_uid] = obj
    return obj_uid


def create_service_for_protocol_entry_with_single_protocol(
    entry: AccessListEntry, service_objects: dict[str, ServiceObject]
) -> str:
    """
    Create service reference for a protocol entry with set protocol.

    Args:
        entry: Access list entry with protocol
        service_objects: Dictionary to update with new service objects
    Returns:
        Service reference string (single object or delimited list)

    """
    if entry.dst_port.kind == "eq":
        # Single port (e.g., 'eq 443' or 'eq https')
        return create_service_for_port(entry.dst_port.value, entry.protocol.value, service_objects)

    if entry.dst_port.kind == "range":
        # Port range (e.g., 'range 1024 65535')
        ports = entry.dst_port.value.split()  # expecting "start end"
        return create_service_for_port_range((ports[0], ports[1]), entry.protocol.value, service_objects)

    if entry.dst_port.kind == "any":
        # Any port for the protocol
        return create_any_protocol_service(entry.protocol.value, service_objects)

    if entry.dst_port.kind in ("service", "service-group"):
        # Reference to existing service object/group
        return entry.dst_port.value
    # Default to any port for the protocol
    return create_any_protocol_service(entry.protocol.value, service_objects)


def create_service_for_protocol_entry(entry: AccessListEntry, service_objects: dict[str, ServiceObject]) -> str:
    """
    Create service reference for a protocol group entry.

    Args:
        entry: Access list entry with protocol group
        service_objects: Dictionary to update with new service objects
    Returns:
        Service reference string (single object or delimited list)

    """
    if entry.protocol.value in ("tcp", "udp", "icmp"):
        return create_service_for_protocol_entry_with_single_protocol(entry, service_objects)

    if entry.protocol.value == "ip":
        return create_any_protocol_service("ip", service_objects)
    # Unknown protocol, default to any for the protocol
    return create_any_protocol_service(entry.protocol.value, service_objects)


def create_service_for_acl_entry(entry: AccessListEntry, service_objects: dict[str, ServiceObject]) -> str:
    """
    Create service object(s) for an ACL entry and return the service reference.

    Args:
        entry: Access list entry with protocol and port information
        service_objects: Dictionary to update with new service objects

    Returns:
        Service reference string (single object or delimited list)

    """
    if entry.protocol.kind == "protocol":
        return create_service_for_protocol_entry(entry, service_objects)

    if entry.protocol.kind in ("service-group", "service"):
        # Reference to service object or group
        return entry.protocol.value

    if entry.protocol.kind == "protocol-group":
        # Protocol group - will be resolved by caller
        return entry.protocol.value

    # Default to all common protocols
    svc_refs = [create_any_protocol_service(proto, service_objects) for proto in ("tcp", "udp", "icmp")]
    return fwo_base.sort_and_join(svc_refs)


def process_mixed_protocol_eq_ports(
    group: AsaServiceObjectGroup, service_objects: dict[str, ServiceObject]
) -> list[str]:
    """Process equal ports for mixed protocol groups."""
    obj_names: list[str] = []
    for protos, eq_ports in group.ports_eq.items():
        for proto in protos.split("-"):  # handles "tcp-udp"
            for port in eq_ports:
                obj_name = create_service_for_port(port, proto, service_objects)
                obj_names.append(obj_name)
    return obj_names


def process_mixed_protocol_range_ports(
    group: AsaServiceObjectGroup, service_objects: dict[str, ServiceObject]
) -> list[str]:
    """Process port ranges for mixed protocol groups."""
    obj_names: list[str] = []
    for proto, ranges in group.ports_range.items():
        for pr in ranges:
            obj_name = create_service_for_port_range(pr, proto, service_objects)
            obj_names.append(obj_name)
    return obj_names


def process_fully_enabled_protocols(
    group: AsaServiceObjectGroup, service_objects: dict[str, ServiceObject]
) -> list[str]:
    """Process protocols that allow all ports."""
    obj_names: list[str] = []
    for proto in group.protocols:
        obj_name = create_any_protocol_service(proto, service_objects)
        obj_names.append(obj_name)
    return obj_names


def process_mixed_protocol_group(group: AsaServiceObjectGroup, service_objects: dict[str, ServiceObject]) -> list[str]:
    """Process a mixed protocol service group."""
    obj_names: list[str] = []

    # Process ports_eq (single port values)
    obj_names.extend(process_mixed_protocol_eq_ports(group, service_objects))

    # Process ports_range (port ranges)
    obj_names.extend(process_mixed_protocol_range_ports(group, service_objects))

    # Process any-protocol references
    obj_names.extend(process_fully_enabled_protocols(group, service_objects))

    # Process nested references
    obj_names.extend(group.nested_refs)

    return obj_names


def process_single_protocol_eq_ports(
    protocol: str, ports: list[str], service_objects: dict[str, ServiceObject]
) -> list[str]:
    """Process equal ports for single protocol groups."""
    obj_names: list[str] = []
    for port in ports:
        obj_name = create_service_for_port(port, protocol, service_objects)
        obj_names.append(obj_name)
    return obj_names


def process_single_protocol_range_ports(
    protocol: str, ranges: list[tuple[str, str]], service_objects: dict[str, ServiceObject]
) -> list[str]:
    """Process port ranges for single protocol groups."""
    obj_names: list[str] = []
    for obj_range in ranges:
        obj_name = create_service_for_port_range(obj_range, protocol, service_objects)
        obj_names.append(obj_name)
    return obj_names


def process_single_protocol_group(group: AsaServiceObjectGroup, service_objects: dict[str, ServiceObject]) -> list[str]:
    """Process a single-protocol service group."""
    obj_names: list[str] = []

    if not group.proto_mode:
        raise ValueError(f"Service object group {group.name} missing proto_mode")

    for protocol in group.proto_mode.split("-"):  # handles "tcp-udp"
        if protocol not in protocol_map:
            raise ValueError(f"Unknown protocol in service object group: {protocol}")

        # Process single port values
        obj_names.extend(
            process_single_protocol_eq_ports(protocol, group.ports_eq.get(group.proto_mode, []), service_objects)
        )

        # Process port ranges
        obj_names.extend(
            process_single_protocol_range_ports(protocol, group.ports_range.get(group.proto_mode, []), service_objects)
        )

        # Process nested references
        obj_names.extend(group.nested_refs)

    return obj_names


def normalize_service_object_groups(
    service_groups: list[AsaServiceObjectGroup], service_objects: dict[str, ServiceObject]
) -> dict[str, ServiceObject]:
    """
    Normalize service object groups from ASA configuration.

    Args:
        service_groups: List of parsed ASA service object groups
        service_objects: Existing service objects dictionary to update

    Returns:
        Updated service objects dictionary including groups

    """
    # Process each service group
    for group in service_groups:
        # Relocate a conflicting synthetic any-ip-protocol object before this group's own
        # members are resolved, so a member referencing it never captures a stale UID.
        _make_room_for_named_object(group.name, service_objects)

        if group.proto_mode:
            obj_names = process_single_protocol_group(group, service_objects)
        else:
            obj_names = process_mixed_protocol_group(group, service_objects)

        # look for duplicates and remove them
        unique_obj_names = list(set(obj_names))
        if len(unique_obj_names) < len(obj_names):
            duplicates = [x for x in obj_names if obj_names.count(x) > 1]
            FWOLogger.debug(f"Removed duplicate service object references found in group {group.name}: {duplicates}")

        # Create the group object
        group_obj = create_service_group_object(group.name, unique_obj_names, group.description)
        service_objects[group.name] = group_obj

    return service_objects

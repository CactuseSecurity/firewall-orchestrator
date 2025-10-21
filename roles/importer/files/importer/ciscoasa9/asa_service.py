"""ASA Service Object Management

This module handles the creation and normalization of service objects from ASA configurations.
It manages both explicit service objects/groups and implicit service objects created from
inline ACL definitions.
"""

from typing import Dict, List, Optional, Tuple
from models.serviceobject import ServiceObject
from ciscoasa9.asa_models import AsaServiceObject, AsaServiceObjectGroup, AccessListEntry
from ciscoasa9.asa_maps import name_to_port, protocol_map
import fwo_const


def create_service_object(name: str, port: int, port_end: int, protocol: str, comment: Optional[str] = None) -> ServiceObject:
    """Create a normalized service object.

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
        svc_port=port,
        svc_port_end=port_end,
        svc_color=fwo_const.defaultColor,
        svc_typ="simple",
        ip_proto=protocol_map.get(protocol, 0),
        svc_comment=comment
    )


def create_protocol_service_object(name: str, protocol: str, comment: Optional[str] = None) -> ServiceObject:
    """Create a service object for a protocol without specific ports.

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
        svc_color=fwo_const.defaultColor,
        svc_typ="simple",
        ip_proto=protocol_map.get(protocol, 0),
        svc_comment=comment
    )


def create_service_group_object(name: str, member_refs: List[str], comment: Optional[str] = None) -> ServiceObject:
    """Create a service group object.

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
        svc_member_names=fwo_const.list_delimiter.join(member_refs),
        svc_member_refs=fwo_const.list_delimiter.join(member_refs),
        svc_color=fwo_const.defaultColor,
        svc_comment=comment
    )


def normalize_service_objects(service_objects: List[AsaServiceObject]) -> Dict[str, ServiceObject]:
    """Normalize individual service objects from ASA configuration.

    Args:
        service_objects: List of parsed ASA service objects

    Returns:
        Dictionary of normalized service objects keyed by svc_uid
    """
    normalized = {}

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
            obj = create_service_object( svc.name, int(start), int(end), svc.protocol, svc.description)
            normalized[svc.name] = obj

        else:
            # Protocol-only service (no specific ports)
            obj = create_protocol_service_object(svc.name, svc.protocol, svc.description)
            normalized[svc.name] = obj

    return normalized


def create_protocol_any_service_objects() -> Dict[str, ServiceObject]:
    """Create default 'any' service objects for common protocols.

    Returns:
        Dictionary of protocol-any service objects
    """
    service_objects = {}

    for proto in ("tcp", "udp", "icmp", "ip"):
        obj_name = f"any-{proto}"
        obj = ServiceObject(
            svc_uid=obj_name,
            svc_name=obj_name,
            svc_port=0,
            svc_port_end=65535,
            svc_color=fwo_const.defaultColor,
            svc_typ="simple",
            ip_proto=protocol_map.get(proto, 0),
            svc_comment=f"any {proto}"
        )
        service_objects[obj_name] = obj

    return service_objects


def create_service_for_port(port: str, proto: str, service_objects: Dict[str, ServiceObject]) -> str:
    """Create a service object for a single port and protocol if it doesn't exist.

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


def create_service_for_port_range(port_range: Tuple[str, str], proto: str, service_objects: Dict[str, ServiceObject]) -> str:
    """Create a service object for a port range and protocol if it doesn't exist.

    Args:
        port_range: Tuple of (start_port, end_port)
        proto: Protocol name
        service_objects: Dictionary to update with new service object

    Returns:
        Service object name/UID
    """
    obj_name = f"{port_range[0]}-{port_range[1]}-{proto}" if port_range[0] != port_range[1] else f"{port_range[0]}-{proto}"
    if obj_name not in service_objects:
        start, end = port_range
        description = None
        if not start.isdigit():
            description = f"{start}: {name_to_port[start]['description']}"
            start = name_to_port[start]["port"]
        if not end.isdigit():
            if not description:
                description = ""
            description += f"{end}: {name_to_port[end]['description']}"
            end = name_to_port[end]["port"]
        obj = create_service_object(obj_name, int(start), int(end), proto, description)
        service_objects[obj_name] = obj
    return obj_name


def create_any_protocol_service(proto: str, service_objects: Dict[str, ServiceObject]) -> str:
    """Create an 'any' service object for a protocol if it doesn't exist.

    Args:
        proto: Protocol name
        service_objects: Dictionary to update with new service object

    Returns:
        Service object name/UID
    """
    obj_name = f"any-{proto}"
    if obj_name not in service_objects:
        obj = ServiceObject(
            svc_uid=obj_name,
            svc_name=obj_name,
            svc_port=0,
            svc_port_end=65535,
            svc_color=fwo_const.defaultColor,
            svc_typ="simple",
            ip_proto=protocol_map.get(proto, 0),
            svc_comment=f"any {proto}"
        )
        service_objects[obj_name] = obj
    return obj_name


def create_service_for_acl_entry(entry: AccessListEntry, service_objects: Dict[str, ServiceObject]) -> str:
    """Create service object(s) for an ACL entry and return the service reference.

    Args:
        entry: Access list entry with protocol and port information
        service_objects: Dictionary to update with new service objects

    Returns:
        Service reference string (single object or delimited list)
    """
    creation_comment = "service object created during import"

    if entry.protocol.kind == "protocol":
        if entry.protocol.value in ("tcp", "udp", "icmp"):
            if entry.dst_port.kind == "eq":
                # Single port (e.g., 'eq 443' or 'eq https')
                return create_service_for_port(entry.dst_port.value, entry.protocol.value, service_objects)

            elif entry.dst_port.kind == "range":
                # Port range (e.g., 'range 1024 65535')
                ports = entry.dst_port.value.split() # expecting "start end"
                return create_service_for_port_range((ports[0], ports[1]), entry.protocol.value, service_objects)

            elif entry.dst_port.kind == "any":
                # Any port for the protocol
                return create_any_protocol_service(entry.protocol.value, service_objects)

            elif entry.dst_port.kind in ("service", "service-group"):
                # Reference to existing service object/group
                return entry.dst_port.value
            else:
                # Default to any port for the protocol
                return create_any_protocol_service(entry.protocol.value, service_objects)

        elif entry.protocol.value == "ip":
            # 'ip' protocol means all protocols (tcp, udp, icmp)
            svc_refs = []
            for proto in ("tcp", "udp", "icmp"):
                svc_refs.append(create_any_protocol_service(proto, service_objects))
            return fwo_const.list_delimiter.join(svc_refs)
        else:
            # Unknown protocol, default to any for the protocol
            return create_any_protocol_service(entry.protocol.value, service_objects)

    elif entry.protocol.kind in ("service-group", "service"):
        # Reference to service object or group
        return entry.protocol.value

    elif entry.protocol.kind == "protocol-group":
        # Protocol group - will be resolved by caller
        return entry.protocol.value

    else:
        # Default to all common protocols
        svc_refs = []
        for proto in ("tcp", "udp", "icmp"):
            svc_refs.append(create_any_protocol_service(proto, service_objects))
        return fwo_const.list_delimiter.join(svc_refs)


def normalize_service_object_groups(service_groups: List[AsaServiceObjectGroup], service_objects: Dict[str, ServiceObject]) -> Dict[str, ServiceObject]:
    """Normalize service object groups from ASA configuration.

    Args:
        service_groups: List of parsed ASA service object groups
        service_objects: Existing service objects dictionary to update

    Returns:
        Updated service objects dictionary including groups
    """
    def process_mixed_protocol_eq_ports(group: AsaServiceObjectGroup, service_objects: Dict[str, ServiceObject]) -> List[str]:
        """Process equal ports for mixed protocol groups."""
        obj_names = []
        if hasattr(group, "ports_eq"):
            for protos, eq_ports in group.ports_eq.items():
                for proto in protos.split("-"): # handles "tcp-udp"
                    for port in eq_ports:
                        obj_name = create_service_for_port(port, proto, service_objects)
                        obj_names.append(obj_name)
        return obj_names

    def process_mixed_protocol_range_ports(group: AsaServiceObjectGroup, service_objects: Dict[str, ServiceObject]) -> List[str]:
        """Process port ranges for mixed protocol groups."""
        obj_names = []
        if hasattr(group, "ports_range"):
            for proto, ranges in group.ports_range.items():
                for pr in ranges:
                    obj_name = create_service_for_port_range(pr, proto, service_objects)
                    obj_names.append(obj_name)
        return obj_names

    def process_mixed_protocol_group(group: AsaServiceObjectGroup, service_objects: Dict[str, ServiceObject]) -> List[str]:
        """Process a mixed protocol service group."""
        obj_names = []

        # Process ports_eq (single port values)
        obj_names.extend(process_mixed_protocol_eq_ports(group, service_objects))

        # Process ports_range (port ranges)
        obj_names.extend(process_mixed_protocol_range_ports(group, service_objects))

        # Process nested references
        obj_names.extend(getattr(group, "nested_refs", []))

        return obj_names

    def process_service_protocol_group(group: AsaServiceObjectGroup) -> List[str]:
        """Process a service group that references other services."""
        obj_names = []

        # Add nested service references
        obj_names.extend(group.nested_refs)

        # Add any-protocol references
        for pr in group.protocols:
            obj_name = f"any-{pr}"
            obj_names.append(obj_name)

        return obj_names

    def process_single_protocol_eq_ports(group: AsaServiceObjectGroup, protocol: str, service_objects: Dict[str, ServiceObject]) -> List[str]:
        """Process equal ports for single protocol groups."""
        obj_names = []
        for pr in group.ports_eq.get(group.proto_mode, []):
            obj_name = create_service_for_port(pr, protocol, service_objects)
            obj_names.append(obj_name)
        return obj_names

    def process_single_protocol_range_ports(group: AsaServiceObjectGroup, protocol: str, service_objects: Dict[str, ServiceObject]) -> List[str]:
        """Process port ranges for single protocol groups."""
        obj_names = []
        for pr in group.ports_range.get(group.proto_mode, []):
            obj_name = create_service_for_port_range(pr, protocol, service_objects)
            obj_names.append(obj_name)
        return obj_names

    def process_single_protocol_group(group: AsaServiceObjectGroup, service_objects: Dict[str, ServiceObject]) -> List[str]:
        """Process a single-protocol service group."""
        obj_names = []

        for protocol in group.proto_mode.split("-"): # handles "tcp-udp"
            if protocol not in protocol_map and protocol != "service":
                raise ValueError(f"Unknown protocol in service object group: {protocol}")

            if protocol == "service":
                obj_names.extend(process_service_protocol_group(group))
                continue

            # Process single port values
            obj_names.extend(process_single_protocol_eq_ports(group, protocol, service_objects))

            # Process port ranges
            obj_names.extend(process_single_protocol_range_ports(group, protocol, service_objects))

        return obj_names

    # Process each service group
    for group in service_groups:
        if group.proto_mode == "mixed":
            obj_names = process_mixed_protocol_group(group, service_objects)
        else:
            obj_names = process_single_protocol_group(group, service_objects)

        # Create the group object
        group_obj = create_service_group_object(group.name, obj_names, group.description)
        service_objects[group.name] = group_obj

    return service_objects
"""ASA Service Object Management

This module handles the creation and normalization of service objects from ASA configurations.
It manages both explicit service objects/groups and implicit service objects created from
inline ACL definitions.
"""

from models.serviceobject import ServiceObject
from ciscoasa9.asa_models import AsaServiceObject, AsaServiceObjectGroup, AccessListEntry
from ciscoasa9.asa_maps import name_to_port, protocol_map
import fwo_const
import fwo_base
from fwo_log import FWOLogger


def create_service_object(name: str, port: int, port_end: int, protocol: str, comment: str | None = None) -> ServiceObject:
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


def create_protocol_service_object(name: str, protocol: str, comment: str | None = None) -> ServiceObject:
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


def create_service_group_object(name: str, member_refs: list[str], comment: str | None = None) -> ServiceObject:
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
        svc_member_names=fwo_base.sort_and_join(member_refs),
        svc_member_refs=fwo_base.sort_and_join(member_refs),
        svc_color=fwo_const.defaultColor,
        svc_comment=comment
    )


def normalize_service_objects(service_objects: list[AsaServiceObject]) -> dict[str, ServiceObject]:
    """Normalize individual service objects from ASA configuration.

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
            obj = create_service_object( svc.name, int(start), int(end), svc.protocol, svc.description)
            normalized[svc.name] = obj

        else:
            # Protocol-only service (no specific ports)
            obj = create_protocol_service_object(svc.name, svc.protocol, svc.description)
            normalized[svc.name] = obj

    return normalized


def create_protocol_any_service_objects() -> dict[str, ServiceObject]:
    """Create default 'any' service objects for common protocols.

    Returns:
        Dictionary of protocol-any service objects
    """
    service_objects: dict[str, ServiceObject] = {}

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


def create_service_for_port(port: str, proto: str, service_objects: dict[str, ServiceObject]) -> str:
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


def create_service_for_port_range(port_range: tuple[str, str], proto: str, service_objects: dict[str, ServiceObject]) -> str:
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
                description = f"{end}: {name_to_port[end]['description']}"
            else:
                description += f"; {end}: {name_to_port[end]['description']}"
            end = name_to_port[end]["port"]
        obj = create_service_object(obj_name, int(start), int(end), proto, description)
        service_objects[obj_name] = obj
    return obj_name


def create_any_protocol_service(proto: str, service_objects: dict[str, ServiceObject]) -> str:
    """Create an 'any' service object for a protocol if it doesn't exist.

    Args:
        proto: Protocol name
        service_objects: Dictionary to update with new service object

    Returns:
        Service object name/UID
    """
    obj_name = f"any-{proto}"
    if obj_name not in service_objects:
        port_range = (0, 65535) if proto in ("tcp", "udp") else (None, None)
        obj = ServiceObject(
            svc_uid=obj_name,
            svc_name=obj_name,
            svc_port=port_range[0],
            svc_port_end=port_range[1],
            svc_color=fwo_const.defaultColor,
            svc_typ="simple",
            ip_proto=protocol_map.get(proto, 0),
            svc_comment=f"any {proto}"
        )
        service_objects[obj_name] = obj
    return obj_name



def create_service_for_protocol_entry_with_single_protocol(entry: AccessListEntry, service_objects: dict[str, ServiceObject]) -> str:
    """Create service reference for a protocol entry with set protocol.
    Args:
        entry: Access list entry with protocol
        service_objects: Dictionary to update with new service objects
    Returns:
        Service reference string (single object or delimited list)
    """
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


def create_service_for_protocol_entry(entry: AccessListEntry, service_objects: dict[str, ServiceObject]) -> str:
    """Create service reference for a protocol group entry.
    Args:
        entry: Access list entry with protocol group
        service_objects: Dictionary to update with new service objects
    Returns:
        Service reference string (single object or delimited list)
    """

    if entry.protocol.value in ("tcp", "udp", "icmp"):
        return create_service_for_protocol_entry_with_single_protocol(entry, service_objects)

    elif entry.protocol.value == "ip":
        svc_refs: list[str] = []
        for proto in protocol_map.keys():
            svc_refs.append(create_any_protocol_service(proto, service_objects))
        

        reference_string = fwo_base.sort_and_join(svc_refs)
        # create a service group for all protocols
        service_objects["ANY"] = ServiceObject(
            svc_uid="ANY",
            svc_name="ANY",
            svc_color=fwo_const.defaultColor,
            svc_typ="group",
            svc_member_names=reference_string,
            svc_member_refs=reference_string,
        )
        return "ANY"
    else:
        # Unknown protocol, default to any for the protocol
        return create_any_protocol_service(entry.protocol.value, service_objects)


def create_service_for_acl_entry(entry: AccessListEntry, service_objects: dict[str, ServiceObject]) -> str:
    """Create service object(s) for an ACL entry and return the service reference.

    Args:
        entry: Access list entry with protocol and port information
        service_objects: Dictionary to update with new service objects

    Returns:
        Service reference string (single object or delimited list)
    """
    if entry.protocol.kind == "protocol":
        return create_service_for_protocol_entry(entry, service_objects)

    elif entry.protocol.kind in ("service-group", "service"):
        # Reference to service object or group
        return entry.protocol.value

    elif entry.protocol.kind == "protocol-group":
        # Protocol group - will be resolved by caller
        return entry.protocol.value

    else:
        # Default to all common protocols
        svc_refs: list[str] = []
        for proto in ("tcp", "udp", "icmp"):
            svc_refs.append(create_any_protocol_service(proto, service_objects))
        return fwo_base.sort_and_join(svc_refs)







def process_mixed_protocol_eq_ports(group: AsaServiceObjectGroup, service_objects: dict[str, ServiceObject]) -> list[str]:
        """Process equal ports for mixed protocol groups."""
        obj_names: list[str] = []
        for protos, eq_ports in group.ports_eq.items():
            for proto in protos.split("-"): # handles "tcp-udp"
                for port in eq_ports:
                    obj_name = create_service_for_port(port, proto, service_objects)
                    obj_names.append(obj_name)
        return obj_names

def process_mixed_protocol_range_ports(group: AsaServiceObjectGroup, service_objects: dict[str, ServiceObject]) -> list[str]:
    """Process port ranges for mixed protocol groups."""
    obj_names: list[str] = []
    for proto, ranges in group.ports_range.items():
        for pr in ranges:
            obj_name = create_service_for_port_range(pr, proto, service_objects)
            obj_names.append(obj_name)
    return obj_names

def process_fully_enabled_protocols(group: AsaServiceObjectGroup, service_objects: dict[str, ServiceObject]) -> list[str]:
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

def process_single_protocol_eq_ports(protocol: str, ports: list[str], service_objects: dict[str, ServiceObject]) -> list[str]:
    """Process equal ports for single protocol groups."""
    obj_names: list[str] = []
    for port in ports:
        obj_name = create_service_for_port(port, protocol, service_objects)
        obj_names.append(obj_name)
    return obj_names

def process_single_protocol_range_ports(protocol: str, ranges: list[tuple[str, str]], service_objects: dict[str, ServiceObject]) -> list[str]:
    """Process port ranges for single protocol groups."""
    obj_names: list[str] = []
    for range in ranges:
        obj_name = create_service_for_port_range(range, protocol, service_objects)
        obj_names.append(obj_name)
    return obj_names

def process_single_protocol_group(group: AsaServiceObjectGroup, service_objects: dict[str, ServiceObject]) -> list[str]:
    """Process a single-protocol service group."""
    obj_names: list[str] = []

    if not group.proto_mode:
        raise ValueError(f"Service object group {group.name} missing proto_mode")

    for protocol in group.proto_mode.split("-"): # handles "tcp-udp"
        if protocol not in protocol_map:
            raise ValueError(f"Unknown protocol in service object group: {protocol}")

        # Process single port values
        obj_names.extend(process_single_protocol_eq_ports(protocol, group.ports_eq.get(group.proto_mode, []), service_objects))

        # Process port ranges
        obj_names.extend(process_single_protocol_range_ports(protocol, group.ports_range.get(group.proto_mode, []), service_objects))

        # Process nested references
        obj_names.extend(group.nested_refs)

    return obj_names



def normalize_service_object_groups(service_groups: list[AsaServiceObjectGroup], service_objects: dict[str, ServiceObject]) -> dict[str, ServiceObject]:
    """Normalize service object groups from ASA configuration.

    Args:
        service_groups: List of parsed ASA service object groups
        service_objects: Existing service objects dictionary to update

    Returns:
        Updated service objects dictionary including groups
    """
    
    # Process each service group
    for group in service_groups:
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
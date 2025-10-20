from netaddr import IPAddress, IPNetwork
import fwo_const
from pathlib import Path
from scrapli.driver import GenericDriver
import time

from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanagerlist import FwConfigManagerList
from fwo_log import getFwoLogger
from models.networkobject import NetworkObject
from ciscoasa9.asa_models import AccessGroupBinding, AccessList, AccessListEntry, AsaEnablePassword,\
    AsaNetworkObject, AsaNetworkObjectGroup, AsaProtocolGroup, AsaServiceModule, AsaServiceObject, AsaServiceObjectGroup,\
    ClassMap, Config, DnsInspectParameters, EndpointKind, InspectionAction, Interface, MgmtAccessRule,\
    Names, NatRule, PolicyClass, PolicyMap, Route, ServicePolicyBinding  
from fwo_enums import ConfigAction
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from model_controllers.management_controller import ManagementController
from models.serviceobject import ServiceObject
from ciscoasa9.asa_parser import parse_asa_config
from ciscoasa9.asa_maps import name_to_port, protocol_map
from models.rulebase import Rulebase
from models.rule import RuleNormalized, RuleAction, RuleTrack, RuleType
from models.gateway import Gateway
from models.rulebase_link import RulebaseLinkUidBased
from fwo_base import write_native_config_to_file


def normalize_network_objects(native_config: Config, logger) -> dict:
    """
    Normalize network objects from the native ASA configuration.
    
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
    network_objects = []

    # Process 'names' - these are simple IP-to-name mappings
    for name in native_config.names:
        obj = NetworkObject(
            obj_uid=name.name,
            obj_name=name.name,
            obj_typ="host",
            obj_ip=IPNetwork(f"{name.ip_address}/32"),
            obj_ip_end=IPNetwork(f"{name.ip_address}/32"),
            obj_color=fwo_const.defaultColor,
            obj_comment=name.description
        )
        network_objects.append(obj)

    # Process network objects (hosts and subnets)
    for obj in native_config.objects:
        if obj.fqdn is not None:
            logger.warning(f"Skipping FQDN object {obj.name}")
            continue  # FQDN objects not yet supported

        if obj.ip_address and obj.subnet_mask:
            # Network object with subnet mask
            network = IPNetwork(f"{obj.ip_address}/{obj.subnet_mask}")
            ip_start = IPNetwork(f"{obj.ip_address}/32")
            ip_end = IPNetwork(f"{IPAddress(network.first + network.size - 1)}/32")
            network_obj = NetworkObject(
                obj_uid=obj.name,
                obj_name=obj.name,
                obj_typ="network",
                obj_ip=ip_start,
                obj_ip_end=ip_end,
                obj_color=fwo_const.defaultColor,
                obj_comment=obj.description
            )
            network_objects.append(network_obj)
        elif obj.ip_address:
            # Host object (single IP address)
            network_obj = NetworkObject(
                obj_uid=obj.name,
                obj_name=obj.name,
                obj_typ="host",
                obj_ip=IPNetwork(f"{obj.ip_address}/32"),
                obj_ip_end=IPNetwork(f"{obj.ip_address}/32"),
                obj_color=fwo_const.defaultColor,
                obj_comment=obj.description
            )
            network_objects.append(network_obj)

    # Process network object groups
    for group in native_config.object_groups:
        obj = NetworkObject(
            obj_uid=group.name,
            obj_name=group.name,
            obj_typ="group",
            obj_member_names="|".join(group.objects),
            obj_member_refs=fwo_const.list_delimiter.join(group.objects),
            obj_color=fwo_const.defaultColor,
            obj_comment=group.description
        )
        network_objects.append(obj)

    # Convert list to dictionary keyed by obj_uid
    return FwConfigNormalizedController.convertListToDict(
        [nwobj.model_dump() for nwobj in network_objects], 'obj_uid'
    )


def normalize_service_objects(native_config: Config) -> dict:
    """
    Normalize service objects from the native ASA configuration.
    
    This function processes:
    - Individual service objects (with specific ports or port ranges)
    - Default 'any' service objects for common protocols
    - Service object groups (including mixed protocol groups)
    
    Args:
        native_config: Parsed ASA configuration containing service objects.
    
    Returns:
        Dictionary of normalized service objects keyed by svc_uid.
    """
    service_objects = {}

    # Process individual service objects
    for svc in native_config.service_objects:
        if svc.dst_port_eq and len(svc.dst_port_eq) > 0:
            # Service with specific port (eq)
            obj = ServiceObject(
                svc_uid=svc.name,
                svc_name=svc.name,
                svc_port=int(svc.dst_port_eq) if svc.dst_port_eq.isdigit() else name_to_port[svc.dst_port_eq]["port"],
                svc_port_end=int(svc.dst_port_eq) if svc.dst_port_eq.isdigit() else name_to_port[svc.dst_port_eq]["port"],
                svc_color=fwo_const.defaultColor,
                svc_typ="simple",
                ip_proto=protocol_map.get(svc.protocol, 0),
                svc_comment=svc.description if svc.dst_port_eq.isdigit() else name_to_port[svc.dst_port_eq]["description"]
            )
            service_objects[svc.name] = obj
        elif svc.dst_port_range and len(svc.dst_port_range) == 2:
            # Service with port range
            obj = ServiceObject(
                svc_uid=svc.name,
                svc_name=svc.name,
                svc_port=int(svc.dst_port_range[0]),
                svc_port_end=int(svc.dst_port_range[1]),
                svc_color=fwo_const.defaultColor,
                svc_typ="simple",
                ip_proto=protocol_map.get(svc.protocol, 0),
                svc_comment=svc.description
            )
            service_objects[svc.name] = obj
        elif svc.dst_port_eq is not None and len(svc.dst_port_eq) == 0:
            # Protocol without specific port
            obj = ServiceObject(
                svc_uid=svc.name,
                svc_name=svc.name,
                svc_color=fwo_const.defaultColor,
                svc_typ="simple",
                ip_proto=protocol_map.get(svc.protocol, 0),
                svc_comment=svc.description
            )
            service_objects[svc.name] = obj

    return service_objects


def create_protocol_any_service_objects(service_objects: dict) -> dict:
        # Create default 'any' service objects for common protocols
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


def _create_service_for_port(port: str, proto: str, description: str, service_objects: dict) -> str:
    """Create a service object for a single port and protocol."""
    obj_name = f"{port}-{proto}"
    if obj_name not in service_objects:
        obj = ServiceObject(
            svc_uid=obj_name,
            svc_name=obj_name,
            svc_port=int(port) if str(port).isdigit() else name_to_port[port]["port"],
            svc_port_end=int(port) if str(port).isdigit() else name_to_port[port]["port"],
            svc_color=fwo_const.defaultColor,
            svc_typ="simple",
            ip_proto=protocol_map.get(proto, 0),
            svc_comment=description
        )
        service_objects[obj_name] = obj
    return obj_name


def _create_service_for_port_range(port_range: tuple, proto: str, description: str, service_objects: dict) -> str:
    """Create a service object for a port range and protocol."""
    obj_name = f"{port_range[0]}-{port_range[1]}-{proto}" if port_range[0] != port_range[1] else f"{port_range[0]}-{proto}"
    if obj_name not in service_objects:
        obj = ServiceObject(
            svc_uid=obj_name,
            svc_name=obj_name,
            svc_port=int(port_range[0]),
            svc_port_end=int(port_range[1]),
            svc_color=fwo_const.defaultColor,
            svc_typ="simple",
            ip_proto=protocol_map.get(proto, 0),
            svc_comment=description
        )
        service_objects[obj_name] = obj
    return obj_name


def _process_mixed_protocol_eq_ports(group, service_objects: dict) -> list:
    """Process equal ports for mixed protocol groups."""
    obj_names = []
    if hasattr(group, "ports_eq"):
        for protos, eq_ports in group.ports_eq.items():
            for proto in protos.split("-"):
                for port in eq_ports:
                    obj_name = _create_service_for_port(port, proto, group.description, service_objects)
                    obj_names.append(obj_name)
    return obj_names


def _process_mixed_protocol_range_ports(group, service_objects: dict) -> list:
    """Process port ranges for mixed protocol groups."""
    obj_names = []
    if hasattr(group, "ports_range"):
        for proto, ranges in group.ports_range.items():
            for pr in ranges:
                obj_name = _create_service_for_port_range(pr, proto, group.description, service_objects)
                obj_names.append(obj_name)
    return obj_names


def _process_mixed_protocol_group(group, service_objects: dict) -> list:
    """Process a mixed protocol service group."""
    obj_names = []
    
    # Process ports_eq (single port values)
    obj_names.extend(_process_mixed_protocol_eq_ports(group, service_objects))
    
    # Process ports_range (port ranges)
    obj_names.extend(_process_mixed_protocol_range_ports(group, service_objects))
    
    # Process nested references
    obj_names.extend(getattr(group, "nested_refs", []))
    
    return obj_names


def _process_service_protocol_group(group, protocol: str, service_objects: dict) -> list:
    """Process a service group that references other services."""
    obj_names = []
    
    # Add nested service references
    obj_names.extend(group.nested_refs)
    
    # Add any-protocol references
    for pr in group.protocols:
        obj_name = f"any-{pr}"
        obj_names.append(obj_name)
    
    return obj_names


def _process_single_protocol_eq_ports(group, protocol: str, service_objects: dict) -> list:
    """Process equal ports for single protocol groups."""
    obj_names = []
    for pr in group.ports_eq.get(group.proto_mode, []):
        obj_name = _create_service_for_port(pr, protocol, group.description, service_objects)
        obj_names.append(obj_name)
    return obj_names


def _process_single_protocol_range_ports(group, protocol: str, service_objects: dict) -> list:
    """Process port ranges for single protocol groups."""
    obj_names = []
    for pr in group.ports_range.get(group.proto_mode, []):
        obj_name = _create_service_for_port_range(pr, protocol, group.description, service_objects)
        obj_names.append(obj_name)
    return obj_names


def _process_single_protocol_group(group, service_objects: dict) -> list:
    """Process a single-protocol service group."""
    obj_names = []
    
    for protocol in group.proto_mode.split("-"):
        if protocol not in protocol_map and protocol != "service":
            raise ValueError(f"Unknown protocol in service object group: {protocol}")

        if protocol == "service":
            obj_names.extend(_process_service_protocol_group(group, protocol, service_objects))
            continue

        # Process single port values
        obj_names.extend(_process_single_protocol_eq_ports(group, protocol, service_objects))
        
        # Process port ranges
        obj_names.extend(_process_single_protocol_range_ports(group, protocol, service_objects))
    
    return obj_names


def _create_service_group_object(group_name: str, obj_names: list, description: str|None, service_objects: dict):
    """Create the final service group object."""
    obj = ServiceObject(
        svc_uid=group_name,
        svc_name=group_name,
        svc_typ="group",
        svc_member_names=fwo_const.list_delimiter.join(obj_names),
        svc_member_refs=fwo_const.list_delimiter.join(obj_names),
        svc_color=fwo_const.defaultColor,
        svc_comment=description
    )
    service_objects[group_name] = obj


def normalize_service_object_groups(native_config: Config, service_objects: dict) -> dict:
    """
    Normalize service object groups from the native ASA configuration.
    
    Handles both single-protocol and mixed-protocol service groups.
    Creates individual service objects for each port/protocol combination
    and groups them together.
    
    Args:
        native_config: Parsed ASA configuration containing service object groups.
        service_objects: Existing dictionary of service objects to update.
    
    Returns:
        Updated dictionary of service objects including groups.
    """
    for group in native_config.service_object_groups:
        if group.proto_mode == "mixed":
            obj_names = _process_mixed_protocol_group(group, service_objects)
        else:
            obj_names = _process_single_protocol_group(group, service_objects)
        
        _create_service_group_object(group.name, obj_names, group.description, service_objects)

    return service_objects


def create_objects_for_access_lists(access_lists: AccessList, network_objects: dict, service_objects: dict, importState: ImportStateController):
    """
    Create network and service objects from inline definitions in access list entries.
    
    ASA access lists can reference objects inline (e.g., 'host 10.0.0.1' or 'eq 443').
    This function creates normalized objects for these inline references so they can
    be properly stored in the database.
    
    Args:
        access_lists: AccessList object containing ACL entries.
        network_objects: Dictionary to update with newly created network objects.
        service_objects: Dictionary to update with newly created service objects.
        importState: Import state controller (currently unused but kept for consistency).
    """
    cnt = 1
    for entry in access_lists.entries:
        rule_name = f"{access_lists.name}-{cnt:03d}"
        src_ref = entry.src.value
        dst_ref = entry.dst.value

        # Create service objects for inline service definitions
        if entry.protocol.kind == "protocol":
            if entry.protocol.value in ("tcp", "udp", "icmp"):
                if entry.dst_port.kind == "eq":
                    # Single port (e.g., 'eq 443' or 'eq https')
                    obj_name = f"{entry.dst_port.value}-{entry.protocol.value}"
                    if obj_name not in service_objects.keys():
                        obj = ServiceObject(
                            svc_uid=obj_name,
                            svc_name=obj_name,
                            svc_port=int(entry.dst_port.value) if entry.dst_port.value.isdigit() else name_to_port[entry.dst_port.value]["port"],
                            svc_port_end=int(entry.dst_port.value) if entry.dst_port.value.isdigit() else name_to_port[entry.dst_port.value]["port"],
                            svc_color=fwo_const.defaultColor,
                            svc_typ="simple",
                            ip_proto=protocol_map.get(entry.protocol.value, 0),
                            svc_comment="service object created during import"
                        )
                        service_objects[obj_name] = obj
                elif entry.dst_port.kind == "range":
                    # Port range (e.g., 'range 1024 65535')
                    ports = entry.dst_port.value.split("-")
                    if len(ports) == 2 and ports[0].isdigit() and ports[1].isdigit():
                        obj_name = f"{ports[0]}-{ports[1]}-{entry.protocol.value}"
                        if obj_name not in service_objects.keys():
                            obj = ServiceObject(
                                svc_uid=obj_name,
                                svc_name=obj_name,
                                svc_port=int(ports[0]),
                                svc_port_end=int(ports[1]),
                                svc_color=fwo_const.defaultColor,
                                svc_typ="simple",
                                ip_proto=protocol_map.get(entry.protocol.value, 0),
                                svc_comment="service object created during import"
                            )
                            service_objects[obj_name] = obj
                elif entry.dst_port.kind == "any":
                    # Any port for the protocol
                    obj_name = f"any-{entry.protocol.value}"
                    if obj_name not in service_objects.keys():
                        obj = ServiceObject(
                            svc_uid=obj_name,
                            svc_name=obj_name,
                            svc_port=0,
                            svc_port_end=65535,
                            svc_color=fwo_const.defaultColor,
                            svc_typ="simple",
                            ip_proto=protocol_map.get(entry.protocol.value, 0),
                            svc_comment="service object created during import"
                        )
                        service_objects[obj_name] = obj
            elif entry.protocol.value == "ip":
                # 'ip' protocol means all protocols (tcp, udp, icmp)
                for pr in ("tcp", "udp", "icmp"):
                    obj_name = f"any-{pr}"
                    if obj_name not in service_objects.keys():
                        obj = ServiceObject(
                            svc_uid=obj_name,
                            svc_name=obj_name,
                            svc_port=0,
                            svc_port_end=65535,
                            svc_color=fwo_const.defaultColor,
                            svc_typ="simple",
                            ip_proto=protocol_map.get(pr, 0),
                            svc_comment="service object created during import"
                        )
                        service_objects[obj_name] = obj

        # Create network objects for inline network definitions
        for ep in (entry.src, entry.dst):
            if ep.kind == "host":
                # Single host IP (e.g., 'host 10.0.0.1')
                if ep.value not in network_objects.keys():
                    obj = NetworkObject(
                        obj_uid=ep.value,
                        obj_name=ep.value,
                        obj_typ="host",
                        obj_ip=IPNetwork(f"{ep.value}/32"),
                        obj_ip_end=IPNetwork(f"{ep.value}/32"),
                        obj_color=fwo_const.defaultColor,
                        obj_comment="network object created during import"
                    )
                    network_objects[ep.value] = obj
            elif ep.kind == "subnet":
                # Subnet with mask (e.g., '10.0.0.0 255.255.255.0')
                # Object name is subnet in CIDR notation
                obj_name = str(IPNetwork(f"{ep.value}/{ep.mask}"))
                if obj_name not in network_objects.keys():
                    network = IPNetwork(f"{ep.value}/{ep.mask}")
                    ip_start = IPNetwork(f"{ep.value}/32")
                    ip_end = IPNetwork(f"{IPAddress(network.first + network.size - 1)}/32")
                    obj = NetworkObject(
                        obj_uid=obj_name,
                        obj_name=obj_name,
                        obj_typ="network",
                        obj_ip=ip_start,
                        obj_ip_end=ip_end,
                        obj_color=fwo_const.defaultColor,
                        obj_comment="network object created during import"
                    )
                    network_objects[obj_name] = obj
            elif ep.kind == "any":
                # 'any' keyword (0.0.0.0 - 255.255.255.255)
                if "any" not in network_objects.keys():
                    obj = NetworkObject(
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
                    network_objects["any"] = obj
            elif ep.kind in ("object", "object-group"):
                # Reference to existing object or object-group - assume it already exists
                pass
            else:
                raise ValueError(f"Unknown endpoint kind: {ep.kind}")

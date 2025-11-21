from __future__ import annotations

from typing import Literal

from pydantic import BaseModel


class AsaEnablePassword(BaseModel):
    password: str
    encryption_function: str

class AsaServiceModule(BaseModel):
    name: str
    keepalive_timeout: int
    keepalive_counter: int

class Names(BaseModel):
    name: str
    ip_address: str
    description: str | None = None

class Interface(BaseModel):
    name: str
    nameif: str
    bridge_group: str | None = None
    security_level: int
    ip_address: str | None = None
    subnet_mask: str | None = None
    additional_settings: list[str]
    description: str | None = None

class AsaNetworkObject(BaseModel):
    name: str
    ip_address: str
    ip_address_end: str | None = None # for range objects
    subnet_mask: str | None = None
    fqdn: str | None = None
    description: str | None = None

class AsaNetworkObjectGroup(BaseModel):
    name: str
    objects: list[AsaNetworkObjectGroupMember]
    description: str | None = None

class AsaNetworkObjectGroupMember(BaseModel):
    kind: Literal["object", "object-group", "host", "hostv6", "subnet", "subnetv6"]
    value: str
    mask: str | None = None

class AsaServiceObject(BaseModel):
    name: str
    protocol: Literal["tcp", "udp", "ip", "tcp-udp", "icmp", "gre"]
    dst_port_eq: str | None = None
    dst_port_range: tuple[str, str] | None = None
    description: str | None = None

class AsaServiceObjectGroup(BaseModel):
    name: str
    proto_mode: Literal["tcp", "udp", "tcp-udp"] | None
    ports_eq: dict[str, list[str]]  # protocol -> list of ports
    ports_range: dict[str, list[tuple[str, str]]]  # protocol -> list of (start_port, end_port)
    nested_refs: list[str]
    protocols: list[str]
    description: str | None

class AsaProtocolGroup(BaseModel):
    name: str
    protocols: list[str] 
    description: str | None = None

class EndpointKind(BaseModel):
    kind: Literal["any", "host", "subnet", "object", "object-group", "service", "protocol-group", "protocol", "eq", "range", "service-group"]
    value: str
    mask: str | None = None

class AccessListEntry(BaseModel):
    acl_name: str
    action: Literal["permit", "deny"]
    protocol: EndpointKind  # Changed to use EndpointKind for kind and value
    src: EndpointKind
    dst: EndpointKind
    dst_port: EndpointKind  # Changed to use EndpointKind for kind and value
    inactive: bool = False  # Added field for inactive flag
    description: str | None = None

class AccessList(BaseModel):
    name: str
    entries: list[AccessListEntry]

class AccessGroupBinding(BaseModel):
    acl_name: str
    direction: Literal["in", "out"]
    interface: str

class NatRule(BaseModel):
    object_name: str
    src_if: str
    dst_if: str
    nat_type: Literal["dynamic", "static"] = "dynamic"
    translated_object: str | None = None

class Route(BaseModel):
    interface: str
    destination: str
    netmask: str
    next_hop: str
    distance: int | None = None

class MgmtAccessRule(BaseModel):
    protocol: Literal["http", "ssh", "telnet"]
    source_ip: str
    source_mask: str
    interface: str


class ClassMap(BaseModel):
    name: str
    matches: list[str] = []   # e.g., ["default-inspection-traffic"]

class DnsInspectParameters(BaseModel):
    message_length_max_client: Literal["auto", "default"] | int | None = None
    message_length_max: int | None = None
    tcp_inspection: bool = True  # "no tcp-inspection" -> False

class InspectionAction(BaseModel):
    protocol: str                 # e.g., "dns", "ftp"
    policy_map: str | None = None  # e.g., "preset_dns_map" after "inspect dns preset_dns_map"

class PolicyClass(BaseModel):
    class_name: str               # e.g., "inspection_default"
    inspections: list[InspectionAction] = []

class PolicyMap(BaseModel):
    name: str                     # e.g., "global_policy" or "preset_dns_map"
    type_str: str | None = None  # e.g., "inspect dns" for typed maps
    parameters_dns: DnsInspectParameters | None = None
    classes: list[PolicyClass] = []

class ServicePolicyBinding(BaseModel):
    policy_map: str               # e.g., "global_policy"
    scope: Literal["global", "interface"] = "global"
    interface: str | None = None

class Config(BaseModel):
    asa_version: str
    hostname: str
    enable_password: AsaEnablePassword
    service_modules: list[AsaServiceModule]
    additional_settings: list[str]
    interfaces: list[Interface]
    objects: list[AsaNetworkObject]
    object_groups: list[AsaNetworkObjectGroup]
    service_objects: list[AsaServiceObject] = []
    service_object_groups: list[AsaServiceObjectGroup] = []
    access_lists: list[AccessList] = []
    access_group_bindings: list[AccessGroupBinding] = []
    nat_rules: list[NatRule] = []
    routes: list[Route] = []
    mgmt_access: list[MgmtAccessRule] = []
    names: list[Names] = []
    class_maps: list[ClassMap] = []
    policy_maps: list[PolicyMap] = []
    service_policies: list[ServicePolicyBinding] = []
    protocol_groups: list[AsaProtocolGroup] = []
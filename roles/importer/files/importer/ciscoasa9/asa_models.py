from __future__ import annotations

from typing import List, Union, Optional, Literal, Tuple

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
    brigde_group: str | None = None
    security_level: int
    ip_address: str | None = None
    subnet_mask: str | None = None
    additional_settings: List[str]
    description: str | None = None

class AsaNetworkObject(BaseModel):
    name: str
    ip_address: str
    subnet_mask: str | None = None
    fqdn: str | None = None
    description: str | None = None

class AsaNetworkObjectGroup(BaseModel):
    name: str
    objects: List[str]
    description: str | None = None

class ServiceObject(BaseModel):
    name: str
    protocol: Literal["tcp", "udp", "icmp", "ip"]
    dst_port_eq: str | None = None
    dst_port_range: Tuple[int, int] | None = None
    description: str | None = None

class ServiceObjectGroup(BaseModel):
    name: str
    proto_mode: Literal["tcp", "udp", "tcp-udp"] = "tcp"
    ports_eq: List[str] = []
    ports_range: List[Tuple[int, int]] = []
    description: str | None = None


class EndpointKind(BaseModel):
    kind: Literal["any", "host", "object", "object-group", "subnet"]
    value: str
    mask: str | None = None

class AccessListEntry(BaseModel):
    acl_name: str
    action: Literal["permit", "deny"]
    protocol: str
    src: EndpointKind
    dst: EndpointKind
    dst_port_eq: str | None = None
    description: str | None = None

class AccessList(BaseModel):
    name: str
    entries: List[AccessListEntry]

class AccessGroupBinding(BaseModel):
    acl_name: str
    direction: Literal["in", "out"]
    interface: str

class NatRule(BaseModel):
    object_name: str
    src_if: str
    dst_if: str
    nat_type: Literal["dynamic-interface", "dynamic", "static"] = "dynamic-interface"
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
    matches: List[str] = []   # e.g., ["default-inspection-traffic"]

class DnsInspectParameters(BaseModel):
    message_length_max_client: Literal["auto", "default"] | int | None = None
    message_length_max: int | None = None
    tcp_inspection: bool = True  # "no tcp-inspection" -> False

class InspectionAction(BaseModel):
    protocol: str                 # e.g., "dns", "ftp"
    policy_map: str | None = None  # e.g., "preset_dns_map" after "inspect dns preset_dns_map"

class PolicyClass(BaseModel):
    class_name: str               # e.g., "inspection_default"
    inspections: List[InspectionAction] = []

class PolicyMap(BaseModel):
    name: str                     # e.g., "global_policy" or "preset_dns_map"
    type_str: str | None = None  # e.g., "inspect dns" for typed maps
    parameters_dns: DnsInspectParameters | None = None
    classes: List[PolicyClass] = []

class ServicePolicyBinding(BaseModel):
    policy_map: str               # e.g., "global_policy"
    scope: Literal["global", "interface"] = "global"
    interface: str | None = None

class Config(BaseModel):
    asa_version: str
    hostname: str
    enable_password: AsaEnablePassword
    service_modules: List[AsaServiceModule]
    additional_settings: List[str]
    interfaces: List[Interface]
    objects: List[Union[AsaNetworkObject, AsaNetworkObjectGroup]]
    service_objects: List[ServiceObject] = []
    service_object_groups: List[ServiceObjectGroup] = []
    access_lists: List[AccessList] = []
    access_group_bindings: List[AccessGroupBinding] = []
    nat_rules: List[NatRule] = []
    routes: List[Route] = []
    mgmt_access: List[MgmtAccessRule] = []
    names: List[Names] = []
    class_maps: List[ClassMap] = []
    policy_maps: List[PolicyMap] = []
    service_policies: List[ServicePolicyBinding] = []
import json
import re
from pathlib import Path
from typing import Dict, List, Optional, Union, Literal, Tuple
from ciscoasa9.asa_models import AccessGroupBinding, AccessList, AccessListEntry, AsaEnablePassword,\
    AsaNetworkObject, AsaNetworkObjectGroup, AsaNetworkObjectGroupMember, AsaServiceModule, AsaServiceObject, AsaServiceObjectGroup,\
    ClassMap, Config, DnsInspectParameters, EndpointKind, InspectionAction, Interface, MgmtAccessRule,\
    Names, NatRule, PolicyClass, PolicyMap, Route, ServicePolicyBinding, AsaProtocolGroup
from fwo_log import getFwoLogger


def _clean_lines(text: str) -> List[str]:
    lines = []
    for raw in text.splitlines():
        line = raw.rstrip()
        # Skip leading metadata/comment lines starting with ':' (as in "show run")
        if line.strip().startswith(":"):
            continue
        lines.append(line)
    return lines

def _consume_block(lines: List[str], start_idx: int, start_re: re.Pattern) -> Tuple[List[str], int]:
    """
    Consume a block that starts at start_idx (matching start_re) and continues
    until next top-level directive (blank line or line not starting with space)
    or a '!' separator. Returns (block_lines, next_index).
    """
    block = [lines[start_idx]]
    i = start_idx + 1
    while i < len(lines):
        l = lines[i]
        if l.strip() == "!":
            i += 1
            break
        if l.startswith(" "):  # continuation/indented
            block.append(l)
            i += 1
            continue
        # Next top-level directive (not indented)
        if start_re.match(l):
            # a new block of same type begins; end this block
            break
        # another directive starts; end this block
        break
    return block, i

def _parse_endpoint(tokens: List[str]) -> Tuple[EndpointKind, int]:
    """
    Parse an ACL endpoint from tokens; returns (EndpointKind, tokens_consumed).
    Supported:
      any
      host A.B.C.D
      object NAME
      object-group NAME
      A.B.C.D MASK
    """
    if not tokens:
        return EndpointKind(kind="any", value="any"), 0

    t0 = tokens[0]
    if t0 == "any":
        return EndpointKind(kind="any", value="any"), 1
    if t0 == "host" and len(tokens) >= 2:
        return EndpointKind(kind="host", value=tokens[1]), 2
    if t0 == "object" and len(tokens) >= 2:
        return EndpointKind(kind="object", value=tokens[1]), 2
    if t0 == "object-group" and len(tokens) >= 2:
        return EndpointKind(kind="object-group", value=tokens[1]), 2
    # subnet notation: ip + mask
    if len(tokens) >= 2 and re.fullmatch(r"\d{1,3}(?:\.\d{1,3}){3}", tokens[0]) and \
       re.fullmatch(r"\d{1,3}(?:\.\d{1,3}){3}", tokens[1]):
        return EndpointKind(kind="subnet", value=tokens[0], mask=tokens[1]), 2
    # fallback
    return EndpointKind(kind="any", value="any"), 1


def _find_description(blocks: List[str]) -> Optional[str]:
    """Helper to find description line in a block."""
    return _find_line_with_prefix(list(blocks), "description ")


def _find_line_with_prefix(block: List[str], prefix: str, only_first: bool = False) -> Optional[str]:
    """Helper to find a single value in an interface block by prefix."""
    v = None
    for b in list(block):
        s = b.strip()
        if s.startswith(prefix):
            if only_first:
                v = s.split()[1]
            else:
                v = s[len(prefix):].strip()
            block.remove(b)
    return v


def _parse_interface_block_find_ip_address(block: List[str], prefix: str) -> Tuple[Optional[str], Optional[str]]:
    """Helper to find IP address and mask in an interface block."""
    ip = None
    mask = None
    for b in list(block):
        s = b.strip()
        if s.startswith(prefix):
            parts = s.split()
            if len(parts) >= 4:
                is_valid_ip = parts[2].count(".") == 3 and parts[3].count(".") == 3
                if is_valid_ip:
                    ip, mask = parts[2], parts[3]
                    block.remove(b)
    return ip, mask


def _parse_interface_block(block: List[str]) -> Interface:
    """Parse an interface block and return an Interface object."""
    if_name = block[0].split()[1]
    blocks = list(block)[1:]

    # Extract values and remove consumed lines from blocks
    nameif = _find_line_with_prefix(blocks, "nameif ", True)
    br = _find_line_with_prefix(blocks, "bridge-group ", True)
    sec = _find_line_with_prefix(blocks, "security-level ", True)
    sec = int(sec) if sec is not None else 0
    ip, mask = _parse_interface_block_find_ip_address(blocks, "ip address ")
    desc = _find_line_with_prefix(blocks, "description ")
    # All non-consumed lines remain in blocks as additional

    # Defaults for missing bits
    nameif = nameif or if_name

    return Interface(
        name=if_name, nameif=nameif, brigde_group=br, security_level=sec,
        ip_address=ip, subnet_mask=mask, additional_settings=blocks, description=desc
    )


def _parse_network_object_block(block: List[str]) -> Tuple[Optional[AsaNetworkObject], Optional[NatRule]]:
    """Parse an object network block. Returns (network_object, nat_rule)."""
    obj_name = block[0].split()[2]
    host = None
    range = None
    subnet = None
    mask = None
    fqdn = None
    desc = _find_description(block[1:])
    pending_nat = None

    for b in block[1:]:
        s = b.strip()
        mhost = re.match(r"^host\s+(\S+)$", s, re.I)
        msub = re.match(r"^subnet\s+(\S+)\s+(\S+)$", s, re.I)
        mrange = re.match(r"^range\s+(\S+)\s+(\S+)$", s, re.I)
        mfqdn = re.match(r"^fqdn\s+v4\s+(\S+)$", s, re.I)
        mnat  = re.match(r"^nat\s+\(([^,]+),([^)]+)\)\s+(dynamic|static)\s+(\S+)$", s, re.I)

        if mhost:
            host = mhost.group(1)
        elif msub:
            subnet, mask = msub.group(1), msub.group(2)
        elif mrange:
            range = mrange.group(1), mrange.group(2)
        elif mfqdn:
            fqdn = mfqdn.group(1)
        elif mnat:
            src_if = mnat.group(1).strip()
            dst_if = mnat.group(2).strip()
            ntype  = mnat.group(3).lower()
            tobj   = mnat.group(4).lower()
            if ntype == "dynamic":
                nat_type = "dynamic"
            elif ntype == "static":
                nat_type = "static"
            else:
                raise ValueError(f"Unsupported NAT type in line: {s}")
            pending_nat = NatRule(
                object_name=obj_name, src_if=src_if, dst_if=dst_if,
                nat_type=nat_type, translated_object=(None if tobj == "interface" else tobj)
            )

    # Create network object if we have host/subnet/fqdn
    net_obj = None
    if host or subnet or range or fqdn:
        if host and not subnet:
            net_obj = AsaNetworkObject(name=obj_name, ip_address=host, ip_address_end=None, subnet_mask=None, fqdn=None, description=desc)
        elif subnet:
            net_obj = AsaNetworkObject(name=obj_name, ip_address=subnet, ip_address_end=None, subnet_mask=mask, fqdn=None, description=desc)
        elif range:
            net_obj = AsaNetworkObject(name=obj_name, ip_address=range[0], ip_address_end=range[1], subnet_mask=None, fqdn=None, description=desc)
        elif fqdn:
            net_obj = AsaNetworkObject(name=obj_name, ip_address="", ip_address_end=None, subnet_mask=None, fqdn=fqdn, description=desc)

    return net_obj, pending_nat


def _parse_network_object_group_block(block: List[str]) -> AsaNetworkObjectGroup:
    """Parse an object-group network block."""
    grp_name = block[0].split()[2]
    desc = _find_description(block[1:])
    members: List[AsaNetworkObjectGroupMember] = []

    for b in block[1:]:
        s = b.strip()
        mobj  = re.match(r"^network-object\s+object\s+(\S+)$", s, re.I)
        mhost = re.match(r"^network-object\s+host\s+(\d+\.\d+\.\d+\.\d+)$", s, re.I)
        mhostv6 = re.match(r"^network-object\s+host\s+(\S+)$", s, re.I)  # e.g. 2001:db8:abcd::1
        msub  = re.match(r"^network-object\s+(\d+\.\d+\.\d+\.\d+)\s+(\d+\.\d+\.\d+\.\d+)$", s, re.I)
        msubv6 = re.match(r"^network-object\s+(\S+)$", s, re.I) # e.g. 2001:db8:abcd::/40 or 2001::/12
        mgroup = re.match(r"^group-object\s+(\S+)$", s, re.I)
        
        if mobj:
            ref = mobj.group(1)
            members.append(AsaNetworkObjectGroupMember(kind="object", value=ref))
        elif mhost:
            ip = mhost.group(1)
            members.append(AsaNetworkObjectGroupMember(kind="host", value=ip))
        elif mhostv6:
            ip = mhostv6.group(1)
            members.append(AsaNetworkObjectGroupMember(kind="hostv6", value=ip))
        elif msub:
            ip = msub.group(1)
            mask = msub.group(2)
            members.append(AsaNetworkObjectGroupMember(kind="subnet", value=ip, mask=mask))
        elif msubv6:
            ip = msubv6.group(1)
            members.append(AsaNetworkObjectGroupMember(kind="subnetv6", value=ip))
        elif mgroup:
            ref = mgroup.group(1)
            members.append(AsaNetworkObjectGroupMember(kind="object-group", value=ref))

    return AsaNetworkObjectGroup(name=grp_name, objects=members, description=desc)


def _parse_service_object_block(block: List[str]) -> AsaServiceObject | None:
    """Parse an object service block."""
    name = block[0].split()[2]
    protocol = None
    eq = None
    prange = None
    desc = _find_description(block[1:])

    for b in block[1:]:
        s = b.strip()
        # e.g., "service tcp destination eq 1234"
        meq = re.match(r"^service\s+(tcp|udp|ip)\s+destination\s+eq\s+(\S+)$", s, re.I)
        mrange = re.match(r"^service\s+(tcp|udp|ip)\s+destination\s+range\s+(\S+)\s+(\S+)$", s, re.I)
        micmp = re.match(r"^service\s+icmp.*$", s, re.I)
        msvc = re.match(r"^service\s+(\S+)$", s, re.I)

        if meq:
            protocol = meq.group(1).lower()
            eq = meq.group(2)
        elif mrange:
            protocol = mrange.group(1).lower()
            prange = (mrange.group(2), mrange.group(3))
        elif micmp:
            protocol = "icmp"
            #TODO: handle ICMP type/code if needed
        elif msvc:
            protocol = msvc.group(1).lower()

    if protocol is None or protocol not in ("tcp", "udp", "ip", "icmp", "gre"):
        logger = getFwoLogger()
        logger.warning(f"Unsupported or missing protocol {protocol} in service object {name}")
        return None  # unsupported protocol

    return AsaServiceObject(name=name, protocol=protocol, dst_port_eq=eq, dst_port_range=prange, description=desc)


def _convert_ports_to_dicts(
    ports_eq: List[Tuple[str, str]], 
    ports_range: List[Tuple[str, Tuple[str, str]]]
) -> Tuple[Dict[str, List[str]], Dict[str, List[Tuple[str, str]]]]:
    """
    Convert port lists to dictionaries grouped by protocol.
    Returns (ports_eq_dict, ports_range_dict).
    """
    ports_eq_dict: Dict[str, List[str]] = {}
    for proto, port in ports_eq:
        if proto not in ports_eq_dict:
            ports_eq_dict[proto] = []
        ports_eq_dict[proto].append(port)

    ports_range_dict: Dict[str, List[Tuple[str, str]]] = {}
    for proto, prange in ports_range:
        if proto not in ports_range_dict:
            ports_range_dict[proto] = []
        ports_range_dict[proto].append(prange)

    return ports_eq_dict, ports_range_dict


def _parse_service_object_group_block(block: List[str]) -> AsaServiceObjectGroup:
    """Parse an object-group service block."""
    hdr = block[0].split()
    name = hdr[2]
    proto_mode = "tcp"
    if len(hdr) >= 4:
        pm = hdr[3].lower()
        if pm in ("tcp", "udp", "tcp-udp"):
            proto_mode = pm

    desc = _find_description(block[1:])
    ports_eq: List[Tuple[str, str]] = []
    ports_range: List[Tuple[str, Tuple[str, str]]] = []
    nested_groups: List[str] = []
    protocols: List[str] = []

    for b in block[1:]:
        s = b.strip()
        meq = re.match(r"^port-object\s*eq\s+(\S+)$", s, re.I)
        mrange = re.match(r"^port-object\srange\s+(\d+)\s+(\d+)$", s, re.I)
        mobj = re.match(r"^service-object\s+object\s+(\S+)$", s, re.I)
        mproto = re.match(r"^service-object\s+(tcp|udp|icmp)$", s, re.I)

        if meq:
            ports_eq.append((proto_mode, meq.group(1)))
        elif mrange:
            ports_range.append((proto_mode, (mrange.group(1), mrange.group(2))))
        elif mobj:
            nested_groups.append(mobj.group(1))
        elif mproto and len(mproto.groups()) == 1:
            protocols.append(mproto.group(1).lower())

    # Convert port lists to dictionaries using helper function
    ports_eq_dict, ports_range_dict = _convert_ports_to_dicts(ports_eq, ports_range)

    return AsaServiceObjectGroup(
        name=name,
        proto_mode=proto_mode,
        ports_eq=ports_eq_dict,
        ports_range=ports_range_dict,
        nested_refs=nested_groups,
        protocols=protocols,
        description=desc
    )


def _parse_class_map_block(block: List[str]) -> ClassMap:
    """Parse a class-map block."""
    name = block[0].split()[1]
    matches: List[str] = []

    for b in block[1:]:
        s = b.strip()
        mat = _find_line_with_prefix([s], "match ")
        if mat:
            matches.append(mat)

    return ClassMap(name=name, matches=matches)


def _parse_dns_parameters_block(block: List[str], start_idx: int) -> Tuple[DnsInspectParameters, int]:
    """
    Parse a 'parameters' sub-block within a DNS inspect policy-map.
    Returns (DnsInspectParameters, next_index).
    """
    params = DnsInspectParameters()
    k = start_idx + 1
    while k < len(block) and block[k].startswith("  "):  # double indent
        t = block[k].strip()
        m1 = re.match(r"^message-length\s+maximum\s+client\s+(auto|\d+)$", t, re.I)
        m2 = re.match(r"^message-length\s+maximum\s+(\d+)$", t, re.I)
        m3 = re.match(r"^no\s+tcp-inspection$", t, re.I)

        if m1:
            v = m1.group(1).lower()
            params.message_length_max_client = "auto" if v == "auto" else int(v)
        elif m2:
            params.message_length_max = int(m2.group(1))
        elif m3:
            params.tcp_inspection = False
        k += 1
    return params, k


def _parse_dns_inspect_policy_map_block(block: List[str], pm_name: str) -> PolicyMap:
    """Parse a policy-map type inspect dns block."""
    pm = PolicyMap(name=pm_name, type_str="inspect dns")
    params = DnsInspectParameters()

    j = 1
    while j < len(block):
        s = block[j].strip()
        if s == "parameters":
            params, j = _parse_dns_parameters_block(block, j)
            continue
        j += 1

    pm.parameters_dns = params
    return pm


def _parse_policy_class_block(block: List[str], start_idx: int) -> Tuple[Optional[PolicyClass], int]:
    """
    Parse a 'class <NAME>' sub-block starting at start_idx.
    Returns (PolicyClass or None, next_index).
    """
    if start_idx >= len(block):
        return None, start_idx + 1

    header = block[start_idx].strip()
    mc = re.match(r"^class\s+(\S+)$", header, re.I)
    if not mc:
        return None, start_idx + 1

    class_name = mc.group(1)
    inspections: List[InspectionAction] = []
    idx = start_idx + 1
    # collect lines under this class (1 indent)
    while idx < len(block) and block[idx].startswith(" "):
        t = block[idx].strip()
        mi = re.match(r"^inspect\s+(\S+)(?:\s+(\S+))?$", t, re.I)
        if mi:
            inspections.append(
                InspectionAction(
                    protocol=mi.group(1).lower(),
                    policy_map=(mi.group(2) if mi.group(2) else None)
                )
            )
        # ignore other class-level lines for now
        idx += 1

    return PolicyClass(class_name=class_name, inspections=inspections), idx


def _parse_policy_map_block(block: List[str], pm_name: str) -> PolicyMap:
    """Parse a regular policy-map block."""
    pm = PolicyMap(name=pm_name)

    idx = 1
    while idx < len(block):
        cls, next_idx = _parse_policy_class_block(block, idx)
        if cls is not None:
            pm.classes.append(cls)
        idx = next_idx

    return pm


def _parse_access_list_entry_protocol(parts: List[str], protocol_groups: List[AsaProtocolGroup], svc_objects: List[AsaServiceObject], svc_obj_groups: List[AsaServiceObjectGroup]) -> Tuple[EndpointKind, List[str]]:
    """
    Parse the protocol part of an access-list entry.
    Returns (protocol EndpointKind, remaining tokens List[str]).
    """
    # Determine protocol
    protocol = None
    tokens = []  # Ensure tokens is always initialized
    if parts[4] == "object-group":
        group_name = parts[5]
        if any(group.name == group_name for group in protocol_groups):
            protocol = EndpointKind(kind="protocol-group", value=group_name)
        elif any(group.name == group_name for group in svc_obj_groups):
            protocol = EndpointKind(kind="service-group", value=group_name)
        else:
            raise ValueError(f"Unknown object-group: {group_name}")
        tokens = parts[6:]
    elif parts[4] == "object":
        obj_name = parts[5]
        if any(obj.name == obj_name for obj in svc_objects):
            protocol = EndpointKind(kind="service", value=obj_name)
        else:
            raise ValueError(f"Unknown service object: {obj_name}")
        tokens = parts[6:]
    else:
        protocol = EndpointKind(kind="protocol", value=parts[4].lower())
        tokens = parts[5:]

    return protocol, tokens


def _parse_access_list_entry_dest_port(tokens: List[str], protocol: EndpointKind) -> Tuple[EndpointKind, List[str]]:
    """
    Parse the destination port part of an access-list entry.
    Returns (dst_port EndpointKind, remaining tokens List[str]).
    """
    dst_port = EndpointKind(kind="any", value="any")  # Default value
    if len(tokens) >= 2 and tokens[0] == "eq":
        dst_port = EndpointKind(kind="eq", value=tokens[1])
        tokens = tokens[2:]
    elif len(tokens) >= 3 and tokens[0] == "range":
        dst_port = EndpointKind(kind="range", value=f"{tokens[1]} {tokens[2]}")
        tokens = tokens[3:]
    elif len(tokens) >= 2 and tokens[0] == "object-group":
        dst_port = EndpointKind(kind="service-group", value=tokens[1])
        tokens = tokens[2:]
    elif len(tokens) >= 2 and tokens[0] == "object":
        dst_port = EndpointKind(kind="service", value=tokens[1])
        tokens = tokens[2:]

    # If protocol is a service-group and dst_port is empty, set dst_port to the group name
    if protocol.kind == "service-group" and dst_port.value == "any":
        dst_port = EndpointKind(kind="service-group", value=protocol.value)
    elif protocol.kind == "service" and dst_port.value == "any":
        dst_port = EndpointKind(kind="service", value=protocol.value)

    return dst_port, tokens


def _parse_access_list_entry(line: str, protocol_groups: List[AsaProtocolGroup], svc_objects: List[AsaServiceObject], svc_obj_groups: List[AsaServiceObjectGroup]) -> AccessListEntry:
    """
    Parse an access-list entry line and return an AccessListEntry object.
    Handles various formats as specified in the requirements.
    """
    # Tokenize the line after 'access-list'
    parts = line.split()
    acl_name = parts[1]  # Access list name
    action = parts[3].lower()  # Action (permit/deny)

    # Parse protocol or protocol/service object-group
    protocol, tokens = _parse_access_list_entry_protocol(parts, protocol_groups, svc_objects, svc_obj_groups)

    # Parse source endpoint
    src, consumed = _parse_endpoint(tokens)
    tokens = tokens[consumed:]

    # Parse destination endpoint
    dst, consumed = _parse_endpoint(tokens)
    tokens = tokens[consumed:]

    # Parse destination port
    dst_port, tokens = _parse_access_list_entry_dest_port(tokens, protocol)

    # Optional inactive flag
    inactive = "inactive" in tokens

    # Ensure action is either 'permit' or 'deny' for type safety
    action_literal = 'permit' if action == 'permit' else 'deny'

    return AccessListEntry(
        acl_name=acl_name,
        action=action_literal,
        protocol=protocol,
        src=src,
        dst=dst,
        dst_port=dst_port,
        inactive=inactive
    )

def _parse_protocol_object_group_block(block: List[str]) -> AsaProtocolGroup:
    """Parse an object-group protocol block."""
    name = block[0].split()[2]
    desc = _find_description(block[1:])
    protocols: List[str] = []

    for b in block[1:]:
        s = b.strip()
        mproto = re.match(r"^protocol-object\s+(\S+)$", s, re.I)
        
        if mproto:
            protocols.append(mproto.group(1))

    return AsaProtocolGroup(
        name=name,
        protocols=protocols,
        description=desc
    )


def _parse_service_object_group_block_without_inline_protocol(block: List[str]) -> AsaServiceObjectGroup:
    """Parse an object-group service block without inline protocol in the header line."""
    grp_name = block[0].split()[2]
    desc = _find_description(block[1:])
    ports_eq: List[Tuple[str, str]] = []
    ports_range: List[Tuple[str, Tuple[str, str]]] = []
    nested_refs: List[str] = []
    protocols: List[str] = [] # list of fully enabled protocols

    mgrp = re.compile(r"^group-object\s+(\S+)$", re.I)
    mobj = re.compile(r"^service-object\s+object\s+(\S+)$", re.I)
    mproto = re.compile(r"^service-object\s+(tcp|udp|icmp|tcp-udp)$", re.I)
    # Handle lines like: service-object tcp destination eq https
    msvc_eq = re.compile(r"^service-object\s+(tcp|udp|icmp|tcp-udp)\s+(?:destination\s+)?eq\s+(\S+)$", re.I)
    msvc_range = re.compile(r"^service-object\s+(tcp|udp|icmp|tcp-udp)\s+(?:destination\s+)?range\s+(\S+)\s+(\S+)$", re.I)

    for b in block[1:]:
        s = b.strip()
    
        mobj_match = mobj.match(s)
        mgrp_match = mgrp.match(s)
        mproto_match = mproto.match(s)
        msvc_eq_match = msvc_eq.match(s)
        msvc_range_match = msvc_range.match(s)
        
        if mobj_match:
            nested_refs.append(mobj_match.group(1))
        elif mgrp_match:
            nested_refs.append(mgrp_match.group(1))
        elif mproto_match:
            protocols.append(mproto_match.group(1))
        elif msvc_eq_match:
            proto = msvc_eq_match.group(1)
            port = msvc_eq_match.group(2)
            ports_eq.append((proto, port))
        elif msvc_range_match:
            proto = msvc_range_match.group(1)
            port1 = msvc_range_match.group(2)
            port2 = msvc_range_match.group(3)
            ports_range.append((proto, (port1, port2)))

    # Convert port lists to dictionaries using existing helper
    ports_eq_dict, ports_range_dict = _convert_ports_to_dicts(ports_eq, ports_range)

    return AsaServiceObjectGroup(
        name=grp_name,
        proto_mode=None,
        ports_eq=ports_eq_dict,
        ports_range=ports_range_dict,
        nested_refs=nested_refs,
        protocols=protocols,
        description=desc
    )

def _parse_icmp_object_group_block(block: List[str]) -> AsaServiceObjectGroup:
    """Parse an object-group icmp-type block."""
    grp_name = block[0].split()[2]
    desc = _find_description(block[1:])
    objects: List[str] = []
    for b in block[1:]:
        s = b.strip()
        mobj = _find_line_with_prefix([s], "icmp-object ", True)
        if mobj:
            objects.append(mobj)
    
    return AsaServiceObjectGroup(
        name=grp_name,
        proto_mode="icmp",
        ports_eq={"icmp": objects},
        ports_range={},
        nested_refs=[],
        protocols=["icmp"],
        description=desc
    )
import json
import re
from pathlib import Path
from typing import Dict, List, Optional, Union, Literal, Tuple
from ciscoasa9.asa_models import AccessGroupBinding, AccessList, AccessListEntry, AsaEnablePassword,\
    AsaNetworkObject, AsaNetworkObjectGroup, AsaNetworkObjectGroupMember, AsaServiceModule, AsaServiceObject, AsaServiceObjectGroup,\
    ClassMap, Config, DnsInspectParameters, EndpointKind, InspectionAction, Interface, MgmtAccessRule,\
    Names, NatRule, PolicyClass, PolicyMap, Route, ServicePolicyBinding, AsaProtocolGroup
from fwo_log import getFwoLogger


_ws = r"[ \t]+"
description_re = r"^description\s+(.+)$"

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


def _parse_interface_block(block: List[str]) -> Interface:
    """Parse an interface block and return an Interface object."""
    if_name = block[0].split()[1]
    nameif = None
    br = None
    sec = None
    ip = None
    mask = None
    desc = None
    additional: List[str] = []

    def _set_nameif(s: str):
        nonlocal nameif
        parts = s.split()
        if len(parts) >= 2:
            nameif = parts[1]

    def _set_bridge(s: str):
        nonlocal br
        parts = s.split()
        if len(parts) >= 2:
            br = parts[1]

    def _set_security(s: str):
        nonlocal sec
        parts = s.split()
        if len(parts) >= 2:
            try:
                sec = int(parts[1])
            except ValueError:
                pass

    def _handle_ip(s: str):
        nonlocal ip, mask
        parts = s.split()
        # Expect forms like: "ip address A.B.C.D M.M.M.M" or "ip address dhcp ..."
        if len(parts) >= 4 and parts[2].count(".") == 3 and parts[3].count(".") == 3:
            ip, mask = parts[2], parts[3]
        elif "dhcp" in parts:
            additional.append(s)

    def _handle_management(s: str):
        additional.append("management-only")

    def _handle_description(s: str):
        nonlocal desc
        desc = s[len("description "):] if len(s) > len("description ") else ""

    def _handle_other(s: str):
        additional.append(s)

    handlers = [
        ("nameif ", _set_nameif),
        ("bridge-group ", _set_bridge),
        ("security-level ", _set_security),
        ("ip address ", _handle_ip),
        ("management-only", _handle_management),
        ("description ", _handle_description),
    ]

    for b in block[1:]:
        s = b.strip()
        for prefix, handler in handlers:
            if s.startswith(prefix):
                handler(s)
                break
        else:
            _handle_other(s)

    # Defaults for missing bits
    nameif = nameif or if_name
    sec = sec if sec is not None else 0

    return Interface(
        name=if_name, nameif=nameif, brigde_group=br, security_level=sec,
        ip_address=ip, subnet_mask=mask, additional_settings=additional, description=desc
    )


def _parse_network_object_block(block: List[str]) -> Tuple[Optional[AsaNetworkObject], Optional[NatRule]]:
    """Parse an object network block. Returns (network_object, nat_rule)."""
    obj_name = block[0].split()[2]
    host: Optional[str] = None
    range_: Optional[Tuple[str, str]] = None
    subnet: Optional[str] = None
    mask: Optional[str] = None
    fqdn: Optional[str] = None
    desc: Optional[str] = None
    pending_nat: Optional[NatRule] = None

    # compile regexes once
    patterns = [
        (re.compile(r"^host\s+(\S+)$", re.I), lambda m: _assign_host(m)),
        (re.compile(r"^subnet\s+(\S+)\s+(\S+)$", re.I), lambda m: _assign_subnet(m)),
        (re.compile(r"^range\s+(\S+)\s+(\S+)$", re.I), lambda m: _assign_range(m)),
        (re.compile(r"^fqdn\s+v4\s+(\S+)$", re.I), lambda m: _assign_fqdn(m)),
        (re.compile(description_re, re.I), lambda m: _assign_desc(m)),
        (re.compile(r"^nat\s+\(([^,]+),([^)]+)\)\s+(dynamic|static)\s+(\S+)$", re.I), lambda m: _assign_nat(m)),
    ]

    # helper setters to keep loop simple
    def _assign_host(m: re.Match):
        nonlocal host
        host = m.group(1)

    def _assign_subnet(m: re.Match):
        nonlocal subnet, mask
        subnet, mask = m.group(1), m.group(2)

    def _assign_range(m: re.Match):
        nonlocal range_
        range_ = (m.group(1), m.group(2))

    def _assign_fqdn(m: re.Match):
        nonlocal fqdn
        fqdn = m.group(1)

    def _assign_desc(m: re.Match):
        nonlocal desc
        desc = m.group(1)

    def _assign_nat(m: re.Match):
        nonlocal pending_nat
        src_if = m.group(1).strip()
        dst_if = m.group(2).strip()
        ntype = m.group(3).lower()
        tobj = m.group(4).lower()
        if ntype not in ("dynamic", "static"):
            raise ValueError(f"Unsupported NAT type in line: {m.string}")
        translated = None if tobj == "interface" else tobj
        pending_nat = NatRule(
            object_name=obj_name, src_if=src_if, dst_if=dst_if,
            nat_type=ntype, translated_object=translated
        )

    # iterate lines and apply first-matching handler
    for b in block[1:]:
        s = b.strip()
        for regex, handler in patterns:
            m = regex.match(s)
            if m:
                handler(m)
                break

    # Build network object according to priority: host, subnet, range, fqdn
    net_obj: Optional[AsaNetworkObject] = None
    if host:
        net_obj = AsaNetworkObject(name=obj_name, ip_address=host, ip_address_end=None,
                                   subnet_mask=None, fqdn=None, description=desc)
    elif subnet:
        net_obj = AsaNetworkObject(name=obj_name, ip_address=subnet, ip_address_end=None,
                                   subnet_mask=mask, fqdn=None, description=desc)
    elif range_:
        net_obj = AsaNetworkObject(name=obj_name, ip_address=range_[0], ip_address_end=range_[1],
                                   subnet_mask=None, fqdn=None, description=desc)
    elif fqdn:
        net_obj = AsaNetworkObject(name=obj_name, ip_address="", ip_address_end=None,
                                   subnet_mask=None, fqdn=fqdn, description=desc)

    return net_obj, pending_nat

def _parse_network_object_group_block(block: List[str]) -> AsaNetworkObjectGroup:
    """Parse an object-group network block."""
    grp_name = block[0].split()[2]
    desc = None
    members: List[AsaNetworkObjectGroupMember] = []

    for b in block[1:]:
        s = b.strip()
        mdesc = re.match(description_re, s, re.I)
        mobj  = re.match(r"^network-object\s+object\s+(\S+)$", s, re.I)
        mhost = re.match(r"^network-object\s+host\s+(\d+\.\d+\.\d+\.\d+)$", s, re.I)
        mhostv6 = re.match(r"^network-object\s+host\s+(\S+)$", s, re.I)  # e.g. 2001:db8:abcd::1
        msub  = re.match(r"^network-object\s+(\d+\.\d+\.\d+\.\d+)\s+(\d+\.\d+\.\d+\.\d+)$", s, re.I)
        msubv6 = re.match(r"^network-object\s+(\S+)$", s, re.I) # e.g. 2001:db8:abcd::/40 or 2001::/12
        mgroup = re.match(r"^group-object\s+(\S+)$", s, re.I)

        if mdesc:
            desc = mdesc.group(1)
        elif mobj:
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
    desc = None

    for b in block[1:]:
        s = b.strip()
        # e.g., "service tcp destination eq 1234"
        meq = re.match(r"^service\s+(tcp|udp|ip)\s+destination\s+eq\s+(\S+)$", s, re.I)
        mrange = re.match(r"^service\s+(tcp|udp|ip)\s+destination\s+range\s+(\S+)\s+(\S+)$", s, re.I)
        mdesc = re.match(description_re, s, re.I)
        micmp = re.match(r"^service\s+icmp.*$", s, re.I)
        msvc = re.match(r"^service\s+(\S+)$", s, re.I)

        if meq:
            protocol = meq.group(1).lower()
            eq = meq.group(2)
        elif mrange:
            protocol = mrange.group(1).lower()
            prange = (mrange.group(2), mrange.group(3))
        elif mdesc:
            desc = mdesc.group(1)
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

    desc = None
    ports_eq: List[Tuple[str, str]] = []
    ports_range: List[Tuple[str, Tuple[str, str]]] = []
    nested_groups: List[str] = []
    protocols: List[str] = []

    for b in block[1:]:
        s = b.strip()
        mdesc = re.match(description_re, s, re.I)
        meq = re.match(r"^port-object\s*eq\s+(\S+)$", s, re.I)
        mrange = re.match(r"^port-object\srange\s+(\d+)\s+(\d+)$", s, re.I)
        mobj = re.match(r"^service-object\s+object\s+(\S+)$", s, re.I)
        mproto = re.match(r"^service-object\s+(tcp|udp|icmp)$", s, re.I)

        if mdesc:
            desc = mdesc.group(1)
        elif meq:
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
        mm = re.match(r"^match\s+(.+)$", s, re.I)
        if mm:
            matches.append(mm.group(1))

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


def _parse_access_list_protocol_group_block(parts: List[str], protocol_groups: List[AsaProtocolGroup], svc_objects: List[AsaServiceObject], svc_obj_groups: List[AsaServiceObjectGroup]) -> Tuple[EndpointKind, List[str]]:
    """
    Parse the protocol part of an access-list entry.
    Returns (protocol EndpointKind, remaining tokens List[str]).
    """
    # Determine protocol
    protocol = None
    tokens = []  # Ensure tokens is always initialized
    if parts[4] in ("tcp", "udp", "icmp", "ip"):
        protocol = EndpointKind(kind="protocol", value=parts[4].lower())
        tokens = parts[5:]
    elif parts[4] == "object-group":
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
    protocol, tokens = _parse_access_list_protocol_group_block(parts, protocol_groups, svc_objects, svc_obj_groups)

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
    desc = None
    protocols: List[str] = []

    for b in block[1:]:
        s = b.strip()
        mdesc = re.match(description_re, s, re.I)
        mproto = re.match(r"^protocol-object\s+(\S+)$", s, re.I)

        if mdesc:
            desc = mdesc.group(1)
        elif mproto:
            protocols.append(mproto.group(1))

    return AsaProtocolGroup(
        name=name,
        protocols=protocols,
        description=desc
    )


def _parse_service_object_group_block_without_inline_protocol(block: List[str]) -> AsaServiceObjectGroup:
    """Parse an object-group service block without inline protocol in the header line."""
    grp_name = block[0].split()[2]
    desc = None
    ports_eq: List[Tuple[str, str]] = []
    ports_range: List[Tuple[str, Tuple[str, str]]] = []
    nested_refs: List[str] = []
    protocols: List[str] = []

    current_proto_mode: str = "tcp"  # Default protocol mode
    for b in block[1:]:
        s = b.strip()
        mdesc = re.match(description_re, s, re.I)
        meq = re.match(r"^port-object\s+eq\s+(\S+)$", s, re.I)
        mrange = re.match(r"^port-object\s+range\s+(\S+)\s+(\S+)$", s, re.I)
        mobj = re.match(r"^service-object\s+object\s+(\S+)$", s, re.I)
        mproto = re.match(r"^service-object\s+(tcp|udp|icmp|tcp-udp)$", s, re.I)
        # Handle lines like: service-object tcp destination eq https
        msvc_eq = re.match(r"^service-object\s+(tcp|udp|icmp|tcp-udp)\s+(?:destination\s+)?eq\s+(\S+)$", s, re.I)
        msvc_range = re.match(r"^service-object\s+(tcp|udp|icmp|tcp-udp)\s+(?:destination\s+)?range\s+(\S+)\s+(\S+)$", s, re.I)

        if mdesc:
            desc = mdesc.group(1)
        elif meq:
            ports_eq.append((current_proto_mode, meq.group(1)))
        elif mrange:
            ports_range.append((current_proto_mode, (mrange.group(1), mrange.group(2))))
        elif mobj:
            nested_refs.append(mobj.group(1))
        elif mproto: 
            if len(mproto.groups()) == 1:
                protocols.append(mproto.group(1).lower())

            proto = mproto.group(1).lower()
            current_proto_mode = proto
        elif msvc_eq:
            proto = msvc_eq.group(1).lower()
            port = msvc_eq.group(2)
            ports_eq.append((proto, port))
            current_proto_mode = proto
        elif msvc_range:
            proto = msvc_range.group(1).lower()
            port1 = msvc_range.group(2)
            port2 = msvc_range.group(3)
            ports_range.append((proto, (port1, port2)))
            current_proto_mode = proto

    # Determine proto_mode based on contents
    proto_mode: Literal['tcp', 'udp', 'tcp-udp', 'service', 'mixed'] = "mixed"  # default value
    if protocols and not (ports_eq or ports_range or nested_refs):
        proto_mode = "service"
    elif len(protocols) == 1 and not (ports_eq or ports_range or nested_refs):
        if protocols[0] in ("tcp", "udp", "tcp-udp"):
            proto_mode = protocols[0]  # type: ignore
        else:
            proto_mode = "service"
    elif len(protocols) > 1 and not (ports_eq or ports_range or nested_refs):
        proto_mode = "mixed"

    # Convert ports_eq from List[Tuple[str, str]] to Dict[str, List[str]]
    ports_eq_dict: Dict[str, List[str]] = {}
    for proto, port in ports_eq:
        if proto not in ports_eq_dict:
            ports_eq_dict[proto] = []
        ports_eq_dict[proto].append(port)

    # Convert ports_range from List[Tuple[str, Tuple[str, str]]] to Dict[str, List[Tuple[str, str]]]
    ports_range_dict: Dict[str, List[Tuple[str, str]]] = {}
    for proto, prange in ports_range:
        if proto not in ports_range_dict:
            ports_range_dict[proto] = []
        ports_range_dict[proto].append((str(prange[0]), str(prange[1])))

    return AsaServiceObjectGroup(
        name=grp_name,
        proto_mode=proto_mode,
        ports_eq=ports_eq_dict,
        ports_range=ports_range_dict,
        nested_refs=nested_refs,
        protocols=protocols,
        description=desc
    )

def _parse_icmp_object_group_block(block: List[str]) -> AsaServiceObjectGroup:
    """Parse an object-group icmp-type block."""
    grp_name = block[0].split()[2]
    desc = None
    objects: List[str] = []
    for b in block[1:]:
        s = b.strip()
        mdesc = re.match(description_re, s, re.I)
        mobj = re.match(r"^icmp-object\s+(\S+)$", s, re.I)

        if mdesc:
            desc = mdesc.group(1)
        elif mobj:
            objects.append(mobj.group(1))
    
    return AsaServiceObjectGroup(
        name=grp_name,
        proto_mode="icmp",
        ports_eq={"icmp": objects},
        ports_range={},
        nested_refs=[],
        protocols=["icmp"],
        description=desc
    )
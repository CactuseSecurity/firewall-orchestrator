import json
import re
from pathlib import Path
from typing import Dict, List, Optional, Union, Literal, Tuple
from ciscoasa9.asa_models import AccessGroupBinding, AccessList, AccessListEntry, AsaEnablePassword,\
    AsaNetworkObject, AsaNetworkObjectGroup, AsaServiceModule, AsaServiceObject, AsaServiceObjectGroup,\
    ClassMap, Config, DnsInspectParameters, EndpointKind, InspectionAction, Interface, MgmtAccessRule,\
    Names, NatRule, PolicyClass, PolicyMap, Route, ServicePolicyBinding, AsaProtocolGroup


_ws = r"[ \t]+"

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
    additional = []
    
    for b in block[1:]:
        s = b.strip()
        if s.startswith("nameif "):
            nameif = s.split()[1]
        elif s.startswith("bridge-group "):
            br = s.split()[1]
        elif s.startswith("security-level "):
            sec = int(s.split()[1])
        elif s.startswith("ip address "):
            parts = s.split()
            is_valid_ip = len(parts) >= 4 and parts[2].count(".") == 3 and parts[3].count(".") == 3
            if is_valid_ip:
                ip, mask = parts[2], parts[3]
            elif "dhcp" in parts:
                additional.append(s)
        elif s.startswith("management-only"):
            additional.append("management-only")
        elif s.startswith("description "):
            desc = s[len("description "):]
        else:
            # keep less common interface settings
            additional.append(s)
    
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
    host = None
    subnet = None
    mask = None
    fqdn = None
    desc = None
    pending_nat = None
    
    for b in block[1:]:
        s = b.strip()
        mhost = re.match(r"^host\s+(\S+)$", s, re.I)
        msub = re.match(r"^subnet\s+(\S+)\s+(\S+)$", s, re.I)
        mfqdn = re.match(r"^fqdn\s+v4\s+(\S+)$", s, re.I)
        mdesc = re.match(r"^description\s+(.+)$", s, re.I)
        mnat  = re.match(r"^nat\s+\(([^,]+),([^)]+)\)\s+(dynamic|static)\s+(\S+)$", s, re.I)
        
        if mhost:
            host = mhost.group(1)
        elif msub:
            subnet, mask = msub.group(1), msub.group(2)
        elif mfqdn:
            fqdn = mfqdn.group(1)
        elif mdesc:
            desc = mdesc.group(1)
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
    if host or subnet or fqdn:
        if host and not subnet:
            net_obj = AsaNetworkObject(name=obj_name, ip_address=host, subnet_mask=None, fqdn=None, description=desc)
        elif subnet:
            net_obj = AsaNetworkObject(name=obj_name, ip_address=subnet, subnet_mask=mask, fqdn=None, description=desc)
        elif fqdn:
            net_obj = AsaNetworkObject(name=obj_name, ip_address="", subnet_mask=None, fqdn=fqdn, description=desc)
    
    return net_obj, pending_nat

def _parse_network_object_group_block(block: List[str]) -> AsaNetworkObjectGroup:
    """Parse an object-group network block."""
    grp_name = block[0].split()[2]
    desc = None
    members: List[str] = []

    for b in block[1:]:
        s = b.strip()
        mdesc = re.match(r"^description\s+(.+)$", s, re.I)
        mobj  = re.match(r"^network-object\s+object\s+(\S+)$", s, re.I)
        mhost = re.match(r"^network-object\s+host\s+(\S+)$", s, re.I)
        msub  = re.match(r"^network-object\s+(\d+\.\d+\.\d+\.\d+)\s+(\d+\.\d+\.\d+\.\d+)$", s, re.I)
        mgroup = re.match(r"^group-object\s+(\S+)$", s, re.I)

        if mdesc:
            desc = mdesc.group(1)
        elif mobj:
            members.append(mobj.group(1))
        elif mhost:
            members.append(f"{mhost.group(1)}")
        elif msub:
            members.append(f"{msub.group(1)}/{msub.group(2)}")
        elif mgroup:
            members.append(f"{mgroup.group(1)}")

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
        msvc = re.match(r"^service\s+(tcp|udp|icmp|ip)\s+(?:destination\s+)?(?:eq\s+(\S+)|range\s+(\d+)\s+(\d+))$", s, re.I)
        mdesc = re.match(r"^description\s+(.+)$", s, re.I)
        meq = re.match(r"^port-object\s+(tcp|udp|icmp|ip)?\s*eq\s+(\S+)$", s, re.I)  # Match port-object eq with optional proto
        mrange = re.match(r"^port-object\s+(tcp|udp|icmp|ip)?\s*range\s+(\d+)\s+(\d+)$", s, re.I)  # Match port-object range with optional proto

        if msvc:
            protocol = msvc.group(1).lower()
            if msvc.group(2):
                eq = msvc.group(2)
            elif msvc.group(3) and msvc.group(4):
                prange = (int(msvc.group(3)), int(msvc.group(4)))
        elif mdesc:
            desc = mdesc.group(1)
        elif meq:
            # If protocol is specified in port-object, use it
            proto_mode = meq.group(1).lower() if meq.group(1) else protocol
            eq = (proto_mode, meq.group(2))
        elif mrange:
            proto_mode = mrange.group(1).lower() if mrange.group(1) else protocol
            prange = (proto_mode, (int(mrange.group(2)), int(mrange.group(3))))

    if protocol is None or protocol not in ("tcp", "udp", "icmp", "ip"):
        # raise ValueError(f"Unsupported or missing protocol in service object: {block[0]}")
        return None  # skip unsupported service objects

    # eq and prange can be either str or tuple, normalize for AsaServiceObject
    dst_port_eq = eq[1] if isinstance(eq, tuple) else eq
    dst_port_range = prange[1] if isinstance(prange, tuple) else prange

    # Ensure dst_port_range is a tuple of two ints or None
    if isinstance(dst_port_range, int):
        dst_port_range = None

    return AsaServiceObject(name=name, protocol=protocol, dst_port_eq=dst_port_eq, dst_port_range=dst_port_range, description=desc)


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
    ports_range: List[Tuple[str, Tuple[int, int]]] = []
    nested_groups: List[str] = []
    protocols: List[str] = []

    for b in block[1:]:
        s = b.strip()
        mdesc = re.match(r"^description\s+(.+)$", s, re.I)
        meq = re.match(r"^port-object\s*eq\s+(\S+)$", s, re.I)
        mrange = re.match(r"^port-object\srange\s+(\d+)\s+(\d+)$", s, re.I)
        mobj = re.match(r"^service-object\s+object\s+(\S+)$", s, re.I)
        mproto = re.match(r"^service-object\s+(tcp|udp|icmp)$", s, re.I)

        if mdesc:
            desc = mdesc.group(1)
        elif meq:
            ports_eq.append((proto_mode, meq.group(1)))
        elif mrange:
            ports_range.append((proto_mode, (int(mrange.group(1)), int(mrange.group(2)))))
        elif mobj:
            nested_groups.append(mobj.group(1))
        elif mproto and len(mproto.groups()) == 1:
            protocols.append(mproto.group(1).lower())

            
    # Convert ports_eq from List[Tuple[str, str]] to Dict[str, List[str]]
    ports_eq_dict: Dict[str, List[str]] = {}
    for proto, port in ports_eq:
        if proto not in ports_eq_dict:
            ports_eq_dict[proto] = []
        ports_eq_dict[proto].append(port)

    # Convert ports_range from List[Tuple[str, Tuple[int, int]]] to Dict[str, List[Tuple[int, int]]]
    ports_range_dict: Dict[str, List[Tuple[int, int]]] = {}
    for proto, prange in ports_range:
        if proto not in ports_range_dict:
            ports_range_dict[proto] = []
        ports_range_dict[proto].append(prange)

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


def _parse_dns_inspect_policy_map_block(block: List[str], pm_name: str) -> PolicyMap:
    """Parse a policy-map type inspect dns block."""
    pm = PolicyMap(name=pm_name, type_str="inspect dns")
    params = DnsInspectParameters()
    
    j = 1
    while j < len(block):
        s = block[j].strip()
        if s == "parameters":
            # consume sub-block of parameters (indented further)
            k = j + 1
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
            j = k
            continue
        j += 1
    
    pm.parameters_dns = params
    return pm


def _parse_policy_map_block(block: List[str], pm_name: str) -> PolicyMap:
    """Parse a regular policy-map block."""
    pm = PolicyMap(name=pm_name)
    
    # find "class <NAME>" blocks within
    idx = 1
    while idx < len(block):
        s = block[idx].strip()
        mc = re.match(r"^class\s+(\S+)$", s, re.I)
        if mc:
            class_name = mc.group(1)
            inspections: List[InspectionAction] = []
            idx += 1
            # collect lines under this class (1 indent)
            while idx < len(block) and block[idx].startswith(" "):
                t = block[idx].strip()
                mi = re.match(r"^inspect\s+(\S+)(?:\s+(\S+))?$", t, re.I)
                if mi:
                    inspections.append(
                        InspectionAction(protocol=mi.group(1).lower(),
                                       policy_map=(mi.group(2) if mi.group(2) else None))
                    )
                else:
                    # ignore other class-level lines for now
                    pass
                idx += 1
            pm.classes.append(PolicyClass(class_name=class_name, inspections=inspections))
        else:
            idx += 1
    
    return pm

def _parse_access_list_entry(line: str, protocol_groups: List[AsaProtocolGroup], svc_objects: List[AsaServiceObject], svc_obj_groups: List[AsaServiceObjectGroup]) -> AccessListEntry:
    """
    Parse an access-list entry line and return an AccessListEntry object.
    Handles various formats as specified in the requirements.
    """
    # Tokenize the line after 'access-list'
    parts = line.split()
    acl_name = parts[1]  # Access list name
    action = parts[3].lower()  # Action (permit/deny)

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

    # Parse source endpoint
    src, consumed = _parse_endpoint(tokens)
    tokens = tokens[consumed:]

    # Parse destination endpoint
    dst, consumed = _parse_endpoint(tokens)
    tokens = tokens[consumed:]

    # Parse destination port
    dst_port = EndpointKind(kind="any", value="any")  # Default value
    if len(tokens) >= 2 and tokens[0] == "eq":
        dst_port = EndpointKind(kind="eq", value=tokens[1])
        tokens = tokens[2:]
    elif len(tokens) >= 3 and tokens[0] == "range":
        dst_port = EndpointKind(kind="range", value=f"{tokens[1]}-{tokens[2]}")
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
        mdesc = re.match(r"^description\s+(.+)$", s, re.I)
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



def _parse_service_object_block_with_inline_protocol(block: List[str]) -> AsaServiceObject:
    """Parse an object service block that includes protocol in the header line."""
    hdr = block[0].split()
    name = hdr[2]
    if len(hdr) < 4:
        raise ValueError(f"Missing protocol in service object header: {block[0]}")
    protocol = hdr[3].lower()
    if protocol not in ("tcp", "udp", "tcp-udp", "icmp", "ip"):
        raise ValueError(f"Unsupported protocol '{protocol}' in service object: {block[0]}")

    eq = None
    prange = None
    desc = None

    for b in block[1:]:
        s = b.strip()
        msvc = re.match(r"^service\s+(?:destination\s+)?(?:eq\s+(\S+)|range\s+(\d+)\s+(\d+))$", s, re.I)
        mdesc = re.match(r"^description\s+(.+)$", s, re.I)
        meq = re.match(r"^port-object\s+eq\s+(\S+)$", s, re.I)  # Match port-object eq
        mrange = re.match(r"^port-object\s+range\s+(\d+)\s+(\d+)$", s, re.I)  # Match port-object range

        if msvc:
            if msvc.group(1):
                eq = msvc.group(1)
            elif msvc.group(2) and msvc.group(3):
                prange = (int(msvc.group(2)), int(msvc.group(3)))
        elif mdesc:
            desc = mdesc.group(1)
        elif meq:
            eq = meq.group(1)  # Handle port-object eq
        elif mrange:
            prange = (int(mrange.group(1)), int(mrange.group(2)))  # Handle port-object range

    return AsaServiceObject(name=name, protocol=protocol, dst_port_eq=eq, dst_port_range=prange, description=desc)




def _parse_service_object_group_block_without_inline_protocol(block: List[str]) -> AsaServiceObjectGroup:
    """Parse an object-group service block without inline protocol in the header line."""
    grp_name = block[0].split()[2]
    desc = None
    ports_eq: List[Tuple[str, str]] = []
    ports_range: List[Tuple[str, Tuple[int, int]]] = []
    nested_groups: List[str] = []
    protocols: List[str] = []

    current_proto_mode: str = "tcp"  # Default protocol mode
    for b in block[1:]:
        s = b.strip()
        mdesc = re.match(r"^description\s+(.+)$", s, re.I)
        meq = re.match(r"^port-object\s+eq\s+(\S+)$", s, re.I)
        mrange = re.match(r"^port-object\s+range\s+(\d+)\s+(\d+)$", s, re.I)
        mobj = re.match(r"^service-object\s+object\s+(\S+)$", s, re.I)
        mproto = re.match(r"^service-object\s+(tcp|udp|icmp|tcp-udp)$", s, re.I)
        # Handle lines like: service-object tcp destination eq https
        msvc_eq = re.match(r"^service-object\s+(tcp|udp|icmp|tcp-udp)\s+(?:destination\s+)?eq\s+(\S+)$", s, re.I)
        msvc_range = re.match(r"^service-object\s+(tcp|udp|icmp|tcp-udp)\s+(?:destination\s+)?range\s+(\d+)\s+(\d+)$", s, re.I)

        if mdesc:
            desc = mdesc.group(1)
        elif meq:
            ports_eq.append((current_proto_mode, meq.group(1)))
        elif mrange:
            ports_range.append((current_proto_mode, (int(mrange.group(1)), int(mrange.group(2)))))
        elif mobj:
            nested_groups.append(mobj.group(1))
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
            port1 = int(msvc_range.group(2))
            port2 = int(msvc_range.group(3))
            ports_range.append((proto, (port1, port2)))
            current_proto_mode = proto

    # Determine proto_mode based on contents
    proto_mode: Literal['tcp', 'udp', 'tcp-udp', 'service', 'mixed'] = "mixed"  # default value
    if protocols and not (ports_eq or ports_range or nested_groups):
        proto_mode = "service"
    elif len(protocols) == 1 and not (ports_eq or ports_range or nested_groups):
        if protocols[0] in ("tcp", "udp", "tcp-udp"):
            proto_mode = protocols[0]  # type: ignore
        else:
            proto_mode = "service"
    elif len(protocols) > 1 and not (ports_eq or ports_range or nested_groups):
        proto_mode = "mixed"

    # Convert ports_eq from List[Tuple[str, str]] to Dict[str, List[str]]
    ports_eq_dict: Dict[str, List[str]] = {}
    for proto, port in ports_eq:
        if proto not in ports_eq_dict:
            ports_eq_dict[proto] = []
        ports_eq_dict[proto].append(port)

    # Convert ports_range from List[Tuple[str, Tuple[int, int]]] to Dict[str, List[Tuple[int, int]]]
    ports_range_dict: Dict[str, List[Tuple[int, int]]] = {}
    for proto, prange in ports_range:
        if proto not in ports_range_dict:
            ports_range_dict[proto] = []
        ports_range_dict[proto].append(prange)

    return AsaServiceObjectGroup(
        name=grp_name,
        proto_mode=proto_mode,
        ports_eq=ports_eq_dict,
        ports_range=ports_range_dict,
        nested_refs=nested_groups,
        protocols=protocols,
        description=desc
    )
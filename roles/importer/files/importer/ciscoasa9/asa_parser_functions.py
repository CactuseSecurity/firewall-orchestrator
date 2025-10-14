
import json
import re
from pathlib import Path
from typing import List, Optional, Union, Literal, Tuple
from ciscoasa9.asa_models import AccessGroupBinding, AccessList, AccessListEntry, AsaEnablePassword,\
    AsaNetworkObject, AsaNetworkObjectGroup, AsaServiceModule, AsaServiceObject, AsaServiceObjectGroup,\
    ClassMap, Config, DnsInspectParameters, EndpointKind, InspectionAction, Interface, MgmtAccessRule,\
    Names, NatRule, PolicyClass, PolicyMap, Route, ServicePolicyBinding 


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
            if len(parts) >= 4 and parts[2].count(".") == 3 and parts[3].count(".") == 3:
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
        
        if mdesc:
            desc = mdesc.group(1)
        elif mobj:
            members.append(mobj.group(1))
        elif mhost:
            members.append(f"host:{mhost.group(1)}")
        elif msub:
            members.append(f"subnet:{msub.group(1)}/{msub.group(2)}")
    
    return AsaNetworkObjectGroup(name=grp_name, objects=members, description=desc)


def _parse_service_object_block(block: List[str]) -> AsaServiceObject:
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
        
        if msvc:
            protocol = msvc.group(1).lower()
            if msvc.group(2):
                eq = msvc.group(2)
            elif msvc.group(3) and msvc.group(4):
                prange = (int(msvc.group(3)), int(msvc.group(4)))
        elif mdesc:
            desc = mdesc.group(1)
    
    if protocol is None or protocol not in ("tcp", "udp", "icmp", "ip"):
        raise ValueError(f"Unsupported or missing protocol in service object: {block[0]}")
    
    return AsaServiceObject(name=name, protocol=protocol, dst_port_eq=eq, dst_port_range=prange, description=desc)


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
    ports_range: List[Tuple[int, int]] = []
    
    for b in block[1:]:
        s = b.strip()
        mdesc = re.match(r"^description\s+(.+)$", s, re.I)
        meq = re.match(r"^port-object\s+eq\s+(\S+)$", s, re.I)
        mrange = re.match(r"^port-object\s+range\s+(\d+)\s+(\d+)$", s, re.I)
        
        if mdesc:
            desc = mdesc.group(1)
        elif meq:
            ports_range.append((int(meq.group(1)), int(meq.group(1))))
        elif mrange:
            ports_range.append((int(mrange.group(1)), int(mrange.group(2))))
    
    return AsaServiceObjectGroup(name=name, proto_mode=proto_mode, ports_range=ports_range, description=desc)


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
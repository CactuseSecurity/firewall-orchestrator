import json
import re
from pathlib import Path
from typing import List, Optional, Union, Literal, Tuple
from asa_models import *

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

# ───────────────────────── Parsers ─────────────────────────

def parse_asa_config(raw_config: str) -> Config:
    lines = _clean_lines(raw_config)

    asa_version = ""
    hostname = ""
    enable_password: Optional[AsaEnablePassword] = None
    service_modules: List[AsaServiceModule] = []
    names: List[Names] = []
    interfaces: List[Interface] = []
    net_objects: List[AsaNetworkObject] = []
    net_obj_groups: List[AsaNetworkObjectGroup] = []
    svc_objects: List[AsaServiceObject] = []
    svc_obj_groups: List[AsaServiceObjectGroup] = []
    access_lists_map: dict[str, List[AccessListEntry]] = {}
    access_groups: List[AccessGroupBinding] = []
    nat_rules: List[NatRule] = []
    routes: List[Route] = []
    mgmt_access: List[MgmtAccessRule] = []
    additional_settings: List[str] = []
    class_maps: List[ClassMap] = []
    policy_maps: dict[str, PolicyMap] = {}  # name -> PolicyMap (so we can fill in pieces)
    service_policies: List[ServicePolicyBinding] = []

    i = 0
    while i < len(lines):
        line = lines[i].strip()

        # separators or blanks
        if line == "" or line == "!":
            i += 1
            continue

        # ASA version
        m = re.match(r"^ASA Version\s+(.+)$", line, re.I)
        if m:
            asa_version = m.group(1).strip()
            i += 1
            continue

        # hostname
        m = re.match(r"^hostname\s+(\S+)$", line, re.I)
        if m:
            hostname = m.group(1)
            i += 1
            continue

        # enable password
        m = re.match(r"^enable password\s+(\S+)\s+(\S+)$", line, re.I)
        if m:
            enable_password = AsaEnablePassword(password=m.group(1), encryption_function=m.group(2))
            i += 1
            continue

        # service-module keepalives (either "service-module 1 ..." or "service-module sfr ...")
        m = re.match(r"^service-module\s+(\S+)\s+keepalive-timeout\s+(\d+)$", line, re.I)
        if m:
            name = m.group(1)
            timeout = int(m.group(2))
            # look ahead for matching counter line for same module
            # (could come in any order; we'll scan nearby couple of lines)
            keepalive_counter = None
            # Attempt to find a matching counter in the next 3 lines
            for j in range(i + 1, min(i + 5, len(lines))):
                mm = re.match(rf"^service-module\s+{re.escape(name)}\s+keepalive-counter\s+(\d+)$",
                              lines[j].strip(), re.I)
                if mm:
                    keepalive_counter = int(mm.group(1))
                    break
            if keepalive_counter is None:
                # fallback: 0
                keepalive_counter = 0
            service_modules.append(AsaServiceModule(name=name, keepalive_timeout=timeout, keepalive_counter=keepalive_counter))
            i += 1
            continue
        m = re.match(r"^service-module\s+(\S+)\s+keepalive-counter\s+(\d+)$", line, re.I)
        if m:
            # already handled (or no matching timeout yet) – we’ll defer adding if not present
            i += 1
            continue

        # name (alias) lines
        m = re.match(r"^name\s+(\d{1,3}(?:\.\d{1,3}){3})\s+(\S+)(?:\s+description\s+(.+))?$", line, re.I)
        if m:
            ip, alias, desc = m.group(1), m.group(2), m.group(3)
            names.append(Names(name=alias, ip_address=ip, description=desc.strip() if desc else None))
            i += 1
            continue

        # interface block
        if re.match(r"^interface\s+\S+", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^interface\s+\S+", re.I))
            # parse the block
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
            interfaces.append(Interface(
                name=if_name, nameif=nameif, brigde_group=br, security_level=sec,
                ip_address=ip, subnet_mask=mask, additional_settings=additional, description=desc
            ))
            continue

        # object network block
        if re.match(r"^object\s+network\s+\S+$", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^object\s+network\s+\S+$", re.I))
            obj_name = block[0].split()[2]
            host = None; subnet = None; mask = None; fqdn = None; desc = None
            # Also, an object-block can carry a 'nat (...) ...' line; capture for NatRule
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
                    nat_type = "dynamic-interface" if (ntype == "dynamic" and tobj == "interface") else ntype
                    pending_nat = NatRule(
                        object_name=obj_name, src_if=src_if, dst_if=dst_if,
                        nat_type=nat_type, translated_object=(None if tobj == "interface" else tobj)
                    )
            # If the object had only NAT, no host/subnet/fqdn, we still keep the NAT rule and skip object creation.
            if host or subnet or fqdn:
                if host and not subnet:
                    net_objects.append(AsaNetworkObject(name=obj_name, ip_address=host, subnet_mask=None, fqdn=None, description=desc))
                elif subnet:
                    net_objects.append(AsaNetworkObject(name=obj_name, ip_address=subnet, subnet_mask=mask, fqdn=None, description=desc))
                elif fqdn:
                    net_objects.append(AsaNetworkObject(name=obj_name, ip_address="", subnet_mask=None, fqdn=fqdn, description=desc))
            if pending_nat:
                nat_rules.append(pending_nat)
            continue

        # object-group network
        if re.match(r"^object-group\s+network\s+\S+$", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^object-group\s+network\s+\S+$", re.I))
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
            net_obj_groups.append(AsaNetworkObjectGroup(name=grp_name, objects=members, description=desc))
            continue

        # object service
        if re.match(r"^object\s+service\s+\S+$", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^object\s+service\s+\S+$", re.I))
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
                        prange = (int(eq), int(eq))
                        eq = None
                    elif msvc.group(3) and msvc.group(4):
                        prange = (int(msvc.group(3)), int(msvc.group(4)))
                elif mdesc:
                    desc = mdesc.group(1)
            if protocol is None:
                protocol = "tcp"  # fallback
            svc_objects.append(AsaServiceObject(name=name, protocol=protocol, dst_port_eq=eq, dst_port_range=prange, description=desc))
            continue

        # object-group service
        if re.match(r"^object-group\s+service\s+\S+", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^object-group\s+service\s+\S+", re.I))
            # header can be "object-group service NAME" or "object-group service NAME tcp-udp"
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
            svc_obj_groups.append(AsaServiceObjectGroup(name=name, proto_mode=proto_mode, ports_range=ports_range, description=desc))
            continue

        # access-list (extended) – simple parser that matches your sample and common cases
        if re.match(r"^access-list\s+\S+\s+extended\s+(permit|deny)\s+", line, re.I):
            # tokenize after 'access-list'
            parts = line.split()
            acl_name = parts[1]
            action = parts[3].lower()
            protocol = parts[4].lower()
            tokens = parts[5:]

            # src
            src, consumed = _parse_endpoint(tokens)
            tokens = tokens[consumed:]
            # dst
            dst, consumed = _parse_endpoint(tokens)
            tokens = tokens[consumed:]

            # optional 'eq <port>'
            dst_port_eq = None
            if len(tokens) >= 2 and tokens[0] == "eq":
                dst_port_eq = tokens[1]

            entry = AccessListEntry(
                acl_name=acl_name, action=action, protocol=protocol, src=src, dst=dst, dst_port_eq=dst_port_eq
            )
            access_lists_map.setdefault(acl_name, []).append(entry)
            i += 1
            continue

        # access-group
        m = re.match(r"^access-group\s+(\S+)\s+(in|out)\s+interface\s+(\S+)$", line, re.I)
        if m:
            access_groups.append(AccessGroupBinding(acl_name=m.group(1), direction=m.group(2), interface=m.group(3)))
            i += 1
            continue

        # standalone NAT blocks like:
        # object network NAME
        #  nat (inside,outside) dynamic interface
        # (handled above inside object block); but some configs duplicate "object network" just to hold NAT.
        # If present outside, they'll still be parsed by the object-block logic earlier.

        # route
        m = re.match(r"^route\s+(\S+)\s+(\d+\.\d+\.\d+\.\d+)\s+(\d+\.\d+\.\d+\.\d+)\s+(\d+\.\d+\.\d+\.\d+)(?:\s+(\d+))?$", line, re.I)
        if m:
            routes.append(Route(
                interface=m.group(1),
                destination=m.group(2),
                netmask=m.group(3),
                next_hop=m.group(4),
                distance=int(m.group(5)) if m.group(5) else None
            ))
            i += 1
            continue

        # management access (http/ssh/telnet)
        m = re.match(r"^(http|ssh|telnet)\s+(\d+\.\d+\.\d+\.\d+)\s+(\d+\.\d+\.\d+\.\d+)\s+(\S+)$", line, re.I)
        if m:
            mgmt_access.append(MgmtAccessRule(protocol=m.group(1).lower(), source_ip=m.group(2), source_mask=m.group(3), interface=m.group(4)))
            i += 1
            continue

        # Sometimes NAT rules appear as a 2-line mini block repeated later:
        # object network NAME
        #  nat (X,Y) dynamic interface
        # (already accounted for by object-block)

        # If a line is interesting and not covered above, keep it as additional setting
        # (skip very noisy things like "pager lines", "mtu", "timeout ..." unless you want them)
        interesting_prefixes = (
            "ftp mode", "same-security-traffic", "dynamic-access-policy-record",
            "service-policy", "user-identity", "aaa ", "icmp ", "arp ", "ssh version",
            "no ssh", "ssh cipher", "ssh key-exchange", "ssh timeout", "http server enable",
            "no asdm", "asdm ", "crypto ", "threat-detection", "ssl cipher",
        )
        for pref in interesting_prefixes:
            if line.startswith(pref):
                additional_settings.append(line)
                break

        # class-map <NAME>
        if re.match(r"^class-map\s+\S+", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^class-map\s+\S+", re.I))
            name = block[0].split()[1]
            matches: List[str] = []
            for b in block[1:]:
                s = b.strip()
                mm = re.match(r"^match\s+(.+)$", s, re.I)
                if mm:
                    matches.append(mm.group(1))
            class_maps.append(ClassMap(name=name, matches=matches))
            continue

        # policy-map type inspect dns <NAME>
        m = re.match(r"^policy-map\s+type\s+inspect\s+dns\s+(\S+)$", line, re.I)
        if m:
            block, i = _consume_block(lines, i, re.compile(r"^policy-map\s+type\s+inspect\s+dns\s+\S+$", re.I))
            pm_name = m.group(1)
            pm = policy_maps.get(pm_name) or PolicyMap(name=pm_name, type_str="inspect dns")
            params = DnsInspectParameters()
            # inside we expect "parameters" sub-block
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
            policy_maps[pm_name] = pm
            continue

        m = re.match(r"^policy-map\s+(\S+)$", line, re.I)
        if m:
            block, i = _consume_block(lines, i, re.compile(r"^policy-map\s+\S+$", re.I))
            pm_name = m.group(1)
            pm = policy_maps.get(pm_name) or PolicyMap(name=pm_name)

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

            policy_maps[pm_name] = pm
            continue

        m = re.match(r"^service-policy\s+(\S+)\s+(global|interface\s+\S+)$", line, re.I)
        if m:
            pm_name = m.group(1)
            scope_part = m.group(2).lower()
            if scope_part == "global":
                service_policies.append(ServicePolicyBinding(policy_map=pm_name, scope="global"))
            else:
                iface = scope_part.split()[1]
                service_policies.append(ServicePolicyBinding(policy_map=pm_name, scope="interface", interface=iface))
            i += 1
            continue

        i += 1

    # Build AccessList objects
    access_lists: List[AccessList] = [
        AccessList(name=name, entries=entries) for name, entries in access_lists_map.items()
    ]

    # Sanity/defaults
    if not asa_version:
        asa_version = "unknown"
    if not hostname:
        hostname = "unknown"
    if not enable_password:
        enable_password = AsaEnablePassword(password="", encryption_function="")

    cfg = Config(
        asa_version=asa_version,
        hostname=hostname,
        enable_password=enable_password,
        service_modules=service_modules,
        additional_settings=additional_settings,
        interfaces=interfaces,
        objects=net_objects,
        object_groups=net_obj_groups,
        service_objects=svc_objects,
        service_object_groups=svc_obj_groups,
        access_lists=access_lists,
        access_group_bindings=access_groups,
        nat_rules=nat_rules,
        routes=routes,
        mgmt_access=mgmt_access,
        names=names,
        class_maps=class_maps,
        policy_maps=list(policy_maps.values()),
        service_policies=service_policies,
    )
    return cfg

# ───────────────────────── Example usage ─────────────────────────
if __name__ == "__main__":

    cfg_file = Path("asa.conf")

    with cfg_file.open("r", encoding="utf-8") as f:
        text = f.read()

    config = parse_asa_config(text)

    # You can dump the entire parsed config as JSON
    print(json.dumps(config.model_dump(exclude_none=True), indent=2))

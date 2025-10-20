import json
import re
from pathlib import Path
from typing import List, Optional
from ciscoasa9.asa_models import AccessGroupBinding, AccessList, AccessListEntry, AsaEnablePassword,\
    AsaNetworkObject, AsaNetworkObjectGroup, AsaProtocolGroup, AsaServiceModule, AsaServiceObject, AsaServiceObjectGroup,\
    ClassMap, Config, DnsInspectParameters, EndpointKind, InspectionAction, Interface, MgmtAccessRule,\
    Names, NatRule, PolicyClass, PolicyMap, Route, ServicePolicyBinding
from ciscoasa9.asa_parser_functions import _clean_lines, _consume_block, _parse_class_map_block, \
    _parse_dns_inspect_policy_map_block, _parse_interface_block, _parse_network_object_block, \
    _parse_network_object_group_block, _parse_policy_map_block, _parse_service_object_block, _parse_service_object_block_with_inline_protocol, \
    _parse_service_object_group_block, _parse_endpoint, _parse_protocol_object_group_block, \
    _parse_access_list_entry, _parse_service_object_group_block_without_inline_protocol


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
    protocol_groups: List[AsaProtocolGroup] = []  # For object-group protocol

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
            interfaces.append(_parse_interface_block(block))
            continue

        # object network block
        if re.match(r"^object\s+network\s+\S+$", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^object\s+network\s+\S+$", re.I))
            net_obj, pending_nat = _parse_network_object_block(block)
            if net_obj:
                net_objects.append(net_obj)
            if pending_nat:
                nat_rules.append(pending_nat)
            continue

        # object-group network
        if re.match(r"^object-group\s+network\s+\S+$", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^object-group\s+network\s+\S+$", re.I))
            net_obj_groups.append(_parse_network_object_group_block(block))
            continue

        # object service
        if re.match(r"^object\s+service\s+\S+$", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^object\s+service\s+\S+$", re.I))
            svc_obj = _parse_service_object_block(block)
            if svc_obj:
                svc_objects.append(svc_obj)
            continue

        # object service with inline protocol definition
        if re.match(r"^object service \S+ (tcp|udp|icmp|ip|tcp-udp)( .+)?$", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^object service \S+ (tcp|udp|icmp|ip|tcp-udp)( .+)?$", re.I))
            svc_objects.append(_parse_service_object_block_with_inline_protocol(block))
            continue

        # object-group service with inline protocol (e.g. "object-group service NAME check if tcp udp or tcp-udp")
        if re.match(r"^object-group\s+service\s+\S+ (tcp|udp|icmp|ip|tcp-udp)( .+)?$", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^object-group\s+service\s+\S+", re.I))
            svc_obj_groups.append(_parse_service_object_group_block(block))
            continue

        # object-group service without inliine protocol (will be defined in each line)
        if re.match(r"^object-group\s+service\s+\S+\s*$", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^object-group\s+service\s+\S+\s*$", re.I))
            svc_obj_groups.append(_parse_service_object_group_block_without_inline_protocol(block))
            continue

        # object-group protocol
        if re.match(r"^object-group\s+protocol\s+\S+$", line, re.I):
            block, i = _consume_block(lines, i, re.compile(r"^object-group\s+protocol\s+\S+$", re.I))
            protocol_groups.append(_parse_protocol_object_group_block(block))
            continue

        # access-list (extended) – updated to use the new AccessListEntry structure
        if re.match(r"^access-list\s+\S+\s+extended\s+(permit|deny)\s+", line, re.I):
            try:
                entry = _parse_access_list_entry(line, protocol_groups, svc_objects, svc_obj_groups)  # Use the updated parser function
                access_lists_map.setdefault(entry.acl_name, []).append(entry)
                i += 1
                continue
            except Exception as e:
                print(f"Warning: Failed to parse access-list line: {line}. Error: {e}")
                i += 1
                continue
        # access-group
        m = re.match(r"^access-group\s+(\S+)\s+(in|out)\s+interface\s+(\S+)$", line, re.I)
        if m:
            direction = m.group(2)
            if direction not in ("in", "out"):
                raise ValueError(f"Invalid direction value: {direction}")
            access_groups.append(AccessGroupBinding(acl_name=m.group(1), direction=direction, interface=m.group(3)))
            i += 1
            continue

        # standalone NAT blocks like:
        # object network NAME
        #  nat (inside,outside) dynamic interface
        # (handled above inside object block); but some configs duplicate "object network" just to hold NAT.
        # If present outside, they'll still be parsed by the object-block logic earlier.

        # route
        m = re.match(r"^route\s+(\S+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)(?:\s+(\d+))?$", line, re.I)
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
            protocol_str = m.group(1).lower()
            if protocol_str not in ("http", "ssh", "telnet"):
                raise ValueError(f"Invalid protocol for MgmtAccessRule: {protocol_str}")
            mgmt_access.append(MgmtAccessRule(protocol=protocol_str, source_ip=m.group(2), source_mask=m.group(3), interface=m.group(4)))
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
            class_maps.append(_parse_class_map_block(block))
            continue

        # policy-map type inspect dns <NAME>
        m = re.match(r"^policy-map\s+type\s+inspect\s+dns\s+(\S+)$", line, re.I)
        if m:
            block, i = _consume_block(lines, i, re.compile(r"^policy-map\s+type\s+inspect\s+dns\s+\S+$", re.I))
            pm_name = m.group(1)
            pm = _parse_dns_inspect_policy_map_block(block, pm_name)
            policy_maps[pm_name] = pm
            continue

        m = re.match(r"^policy-map\s+(\S+)$", line, re.I)
        if m:
            block, i = _consume_block(lines, i, re.compile(r"^policy-map\s+\S+$", re.I))
            pm_name = m.group(1)
            pm = _parse_policy_map_block(block, pm_name)
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
        protocol_groups=protocol_groups,
    )
    return cfg


# ───────────────────────── Example usage ─────────────────────────
if __name__ == "__main__":

    cfg_file = Path("ciscoasa9/asa.conf")

    with cfg_file.open("r", encoding="utf-8") as f:
        text = f.read()

    config = parse_asa_config(text)

    # You can dump the entire parsed config as JSON
    print(json.dumps(config.model_dump(exclude_none=True)["names"], indent=2))

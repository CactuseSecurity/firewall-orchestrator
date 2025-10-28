import json
import re
from pathlib import Path
from typing import List, Optional
from ciscoasa9.asa_models import AccessGroupBinding, AccessList, AccessListEntry, AsaEnablePassword,\
    AsaNetworkObject, AsaNetworkObjectGroup, AsaProtocolGroup, AsaServiceModule, AsaServiceObject, AsaServiceObjectGroup,\
    ClassMap, Config, DnsInspectParameters, EndpointKind, InspectionAction, Interface, MgmtAccessRule,\
    Names, NatRule, PolicyClass, PolicyMap, Route, ServicePolicyBinding
from ciscoasa9.asa_parser_functions import _clean_lines, _consume_block, _parse_class_map_block, \
    _parse_dns_inspect_policy_map_block, _parse_icmp_object_group_block, _parse_interface_block, _parse_network_object_block, \
    _parse_network_object_group_block, _parse_policy_map_block, _parse_service_object_block, \
    _parse_service_object_group_block, _parse_endpoint, _parse_protocol_object_group_block, \
    _parse_access_list_entry, _parse_service_object_group_block_without_inline_protocol


def parse_asa_config(raw_config: str) -> Config:
    lines = _clean_lines(raw_config)
    
    # Initialize state
    state = _ParserState()
    
    # Handler registry: (pattern, handler_function)
    handlers = [
        (re.compile(r"^ASA Version\s+(\S+)$", re.I), _handle_asa_version),
        (re.compile(r"^hostname\s+(\S+)$", re.I), _handle_hostname),
        (re.compile(r"^enable password\s+(\S+)\s+(\S+)$", re.I), _handle_enable_password),
        (re.compile(r"^service-module\s+(\S+)\s+keepalive-timeout\s+(\d+)$", re.I), _handle_service_module_timeout),
        (re.compile(r"^service-module\s+(\S+)\s+keepalive-counter\s+(\d+)$", re.I), _handle_service_module_counter),
        (re.compile(r"^name\s+(\d{1,3}(?:\.\d{1,3}){3})\s+(\S+)(?:\s+description\s)?", re.I), _handle_name),
        (re.compile(r"^interface\s+\S+", re.I), _handle_interface_block),
        (re.compile(r"^object\s+network\s+\S+$", re.I), _handle_network_object_block),
        (re.compile(r"^object-group\s+network\s+\S+$", re.I), _handle_network_object_group_block),
        (re.compile(r"^object\s+service\s+\S+$", re.I), _handle_service_object_block),
        (re.compile(r"^object-group\s+service\s+\S+ (tcp|udp|icmp|ip|tcp-udp)( .+)?$", re.I), _handle_service_object_group_with_protocol),
        (re.compile(r"^object-group\s+service\s+\S+\s*$", re.I), _handle_service_object_group_without_protocol),
        (re.compile(r"^object-group\s+icmp-type\s+\S+$", re.I), _handle_icmp_object_group_block),
        (re.compile(r"^object-group\s+protocol\s+\S+$", re.I), _handle_protocol_object_group_block),
        (re.compile(r"^access-list\s+\S+\s+extended\s+(permit|deny)\s+", re.I), _handle_access_list_entry),
        (re.compile(r"^access-group\s+(\S+)\s+(in|out)\s+interface\s+(\S+)$", re.I), _handle_access_group),
        (re.compile(r"^route\s+(\S+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)(?:\s+(\d+))?$", re.I), _handle_route),
        (re.compile(r"^(http|ssh|telnet)\s+(\d+\.\d+\.\d+\.\d+)\s+(\d+\.\d+\.\d+\.\d+)\s+(\S+)$", re.I), _handle_mgmt_access),
        (re.compile(r"^class-map\s+\S+", re.I), _handle_class_map_block),
        (re.compile(r"^policy-map\s+type\s+inspect\s+dns\s+(\S+)$", re.I), _handle_dns_inspect_policy_map_block),
        (re.compile(r"^policy-map\s+(\S+)$", re.I), _handle_policy_map_block),
        (re.compile(r"^service-policy\s+(\S+)\s+(global|interface\s+\S+)$", re.I), _handle_service_policy),
    ]
    
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        
        if not line or line == "!":
            i += 1
            continue
        
        handled = False
        for pattern, handler in handlers:
            match = pattern.match(line)
            if match:
                i = handler(match, line, lines, i, state)
                handled = True
                break
        
        if not handled:
            _handle_additional_settings(line, state)
            i += 1
    
    return _build_config(state)


class _ParserState:
    def __init__(self):
        self.asa_version = ""
        self.hostname = ""
        self.enable_password: Optional[AsaEnablePassword] = None
        self.service_modules: List[AsaServiceModule] = []
        self.names: List[Names] = []
        self.interfaces: List[Interface] = []
        self.net_objects: List[AsaNetworkObject] = []
        self.net_obj_groups: List[AsaNetworkObjectGroup] = []
        self.svc_objects: List[AsaServiceObject] = []
        self.svc_obj_groups: List[AsaServiceObjectGroup] = []
        self.access_lists_map: dict[str, List[AccessListEntry]] = {}
        self.access_groups: List[AccessGroupBinding] = []
        self.nat_rules: List[NatRule] = []
        self.routes: List[Route] = []
        self.mgmt_access: List[MgmtAccessRule] = []
        self.additional_settings: List[str] = []
        self.class_maps: List[ClassMap] = []
        self.policy_maps: dict[str, PolicyMap] = {}
        self.service_policies: List[ServicePolicyBinding] = []
        self.protocol_groups: List[AsaProtocolGroup] = []


def _handle_asa_version(match, line, lines, i, state):
    state.asa_version = match.group(1).strip()
    return i + 1


def _handle_hostname(match, line, lines, i, state):
    state.hostname = match.group(1)
    return i + 1


def _handle_enable_password(match, line, lines, i, state):
    state.enable_password = AsaEnablePassword(password=match.group(1), encryption_function=match.group(2))
    return i + 1


def _handle_service_module_timeout(match, line, lines, i, state):
    name = match.group(1)
    timeout = int(match.group(2))
    keepalive_counter = _find_keepalive_counter(lines, i, name)
    state.service_modules.append(AsaServiceModule(name=name, keepalive_timeout=timeout, keepalive_counter=keepalive_counter))
    return i + 1


def _find_keepalive_counter(lines, i, name):
    for j in range(i + 1, min(i + 5, len(lines))):
        m = re.match(rf"^service-module\s+{re.escape(name)}\s+keepalive-counter\s+(\d+)$", lines[j].strip(), re.I)
        if m:
            return int(m.group(1))
    return 0


def _handle_service_module_counter(match, line, lines, i, state):
    return i + 1


def _handle_name(match, line, lines, i, state):
    ip, alias = match.group(1), match.group(2)
    desc = line[match.end():].strip() or None
    state.names.append(Names(name=alias, ip_address=ip, description=desc))
    return i + 1


def _handle_interface_block(match, line, lines, i, state):
    block, new_i = _consume_block(lines, i, re.compile(r"^interface\s+\S+", re.I))
    state.interfaces.append(_parse_interface_block(block))
    return new_i


def _handle_network_object_block(match, line, lines, i, state):
    block, new_i = _consume_block(lines, i, re.compile(r"^object\s+network\s+\S+$", re.I))
    net_obj, pending_nat = _parse_network_object_block(block)
    if net_obj:
        state.net_objects.append(net_obj)
    if pending_nat:
        state.nat_rules.append(pending_nat)
    return new_i


def _handle_network_object_group_block(match, line, lines, i, state):
    block, new_i = _consume_block(lines, i, re.compile(r"^object-group\s+network\s+\S+$", re.I))
    state.net_obj_groups.append(_parse_network_object_group_block(block))
    return new_i


def _handle_service_object_block(match, line, lines, i, state):
    block, new_i = _consume_block(lines, i, re.compile(r"^object\s+service\s+\S+$", re.I))
    svc_obj = _parse_service_object_block(block)
    if svc_obj:
        state.svc_objects.append(svc_obj)
    return new_i


def _handle_service_object_group_with_protocol(match, line, lines, i, state):
    block, new_i = _consume_block(lines, i, re.compile(r"^object-group\s+service\s+\S+", re.I))
    state.svc_obj_groups.append(_parse_service_object_group_block(block))
    return new_i


def _handle_service_object_group_without_protocol(match, line, lines, i, state):
    block, new_i = _consume_block(lines, i, re.compile(r"^object-group\s+service\s+\S+\s*$", re.I))
    state.svc_obj_groups.append(_parse_service_object_group_block_without_inline_protocol(block))
    return new_i


def _handle_icmp_object_group_block(match, line, lines, i, state):
    block, new_i = _consume_block(lines, i, re.compile(r"^object-group\s+icmp-type\s+\S+$", re.I))
    state.svc_obj_groups.append(_parse_icmp_object_group_block(block))
    return new_i


def _handle_protocol_object_group_block(match, line, lines, i, state):
    block, new_i = _consume_block(lines, i, re.compile(r"^object-group\s+protocol\s+\S+$", re.I))
    state.protocol_groups.append(_parse_protocol_object_group_block(block))
    return new_i


def _handle_access_list_entry(match, line, lines, i, state):
    try:
        entry = _parse_access_list_entry(line, state.protocol_groups, state.svc_objects, state.svc_obj_groups)
        state.access_lists_map.setdefault(entry.acl_name, []).append(entry)
    except Exception as e:
        print(f"Warning: Failed to parse access-list line: {line}. Error: {e}")
    return i + 1


def _handle_access_group(match, line, lines, i, state):
    direction = match.group(2)
    if direction not in ("in", "out"):
        raise ValueError(f"Invalid direction value: {direction}")
    state.access_groups.append(AccessGroupBinding(acl_name=match.group(1), direction=direction, interface=match.group(3)))
    return i + 1


def _handle_route(match, line, lines, i, state):
    state.routes.append(Route(
        interface=match.group(1),
        destination=match.group(2),
        netmask=match.group(3),
        next_hop=match.group(4),
        distance=int(match.group(5)) if match.group(5) else None
    ))
    return i + 1


def _handle_mgmt_access(match, line, lines, i, state):
    protocol_str = match.group(1).lower()
    if protocol_str not in ("http", "ssh", "telnet"):
        raise ValueError(f"Invalid protocol for MgmtAccessRule: {protocol_str}")
    state.mgmt_access.append(MgmtAccessRule(protocol=protocol_str, source_ip=match.group(2), source_mask=match.group(3), interface=match.group(4)))
    return i + 1


def _handle_class_map_block(match, line, lines, i, state):
    block, new_i = _consume_block(lines, i, re.compile(r"^class-map\s+\S+", re.I))
    state.class_maps.append(_parse_class_map_block(block))
    return new_i


def _handle_dns_inspect_policy_map_block(match, line, lines, i, state):
    block, new_i = _consume_block(lines, i, re.compile(r"^policy-map\s+type\s+inspect\s+dns\s+\S+$", re.I))
    pm_name = match.group(1)
    pm = _parse_dns_inspect_policy_map_block(block, pm_name)
    state.policy_maps[pm_name] = pm
    return new_i


def _handle_policy_map_block(match, line, lines, i, state):
    block, new_i = _consume_block(lines, i, re.compile(r"^policy-map\s+\S+$", re.I))
    pm_name = match.group(1)
    pm = _parse_policy_map_block(block, pm_name)
    state.policy_maps[pm_name] = pm
    return new_i


def _handle_service_policy(match, line, lines, i, state):
    pm_name = match.group(1)
    scope_part = match.group(2).lower()
    if scope_part == "global":
        state.service_policies.append(ServicePolicyBinding(policy_map=pm_name, scope="global"))
    else:
        iface = scope_part.split()[1]
        state.service_policies.append(ServicePolicyBinding(policy_map=pm_name, scope="interface", interface=iface))
    return i + 1


def _handle_additional_settings(line, state):
    interesting_prefixes = (
        "ftp mode", "same-security-traffic", "dynamic-access-policy-record",
        "service-policy", "user-identity", "aaa ", "icmp ", "arp ", "ssh version",
        "no ssh", "ssh cipher", "ssh key-exchange", "ssh timeout", "http server enable",
        "no asdm", "asdm ", "crypto ", "threat-detection", "ssl cipher",
    )
    for pref in interesting_prefixes:
        if line.startswith(pref):
            state.additional_settings.append(line)
            break


def _build_config(state: _ParserState) -> Config:
    access_lists = [AccessList(name=name, entries=entries) for name, entries in state.access_lists_map.items()]
    
    return Config(
        asa_version=state.asa_version or "unknown",
        hostname=state.hostname or "unknown",
        enable_password=state.enable_password or AsaEnablePassword(password="", encryption_function=""),
        service_modules=state.service_modules,
        additional_settings=state.additional_settings,
        interfaces=state.interfaces,
        objects=state.net_objects,
        object_groups=state.net_obj_groups,
        service_objects=state.svc_objects,
        service_object_groups=state.svc_obj_groups,
        access_lists=access_lists,
        access_group_bindings=state.access_groups,
        nat_rules=state.nat_rules,
        routes=state.routes,
        mgmt_access=state.mgmt_access,
        names=state.names,
        class_maps=state.class_maps,
        policy_maps=list(state.policy_maps.values()),
        service_policies=state.service_policies,
        protocol_groups=state.protocol_groups,
    )


# ───────────────────────── Example usage ─────────────────────────
if __name__ == "__main__":

    cfg_file = Path("ciscoasa9/asa.conf")

    with cfg_file.open("r", encoding="utf-8") as f:
        text = f.read()

    config = parse_asa_config(text)

    # You can dump the entire parsed config as JSON
    print(json.dumps(config.model_dump(exclude_none=True)["names"], indent=2))

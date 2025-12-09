import json
import re
from pathlib import Path
from typing import TYPE_CHECKING

from fw_modules.ciscoasa9.asa_models import (
    AccessGroupBinding,
    AccessList,
    AccessListEntry,
    AsaEnablePassword,
    AsaNetworkObject,
    AsaNetworkObjectGroup,
    AsaProtocolGroup,
    AsaServiceModule,
    AsaServiceObject,
    AsaServiceObjectGroup,
    ClassMap,
    Config,
    Interface,
    MgmtAccessRule,
    Names,
    NatRule,
    PolicyMap,
    Route,
    ServicePolicyBinding,
)
from fw_modules.ciscoasa9.asa_parser_functions import (
    clean_lines,
    consume_block,
    parse_access_list_entry,
    parse_class_map_block,
    parse_dns_inspect_policy_map_block,
    parse_icmp_object_group_block,
    parse_interface_block,
    parse_network_object_block,
    parse_network_object_group_block,
    parse_policy_map_block,
    parse_protocol_object_group_block,
    parse_service_object_block,
    parse_service_object_group_block,
)
from fwo_log import FWOLogger

if TYPE_CHECKING:
    from collections.abc import Callable


def parse_asa_config(raw_config: str) -> Config:
    lines = clean_lines(raw_config)

    # Initialize state
    state = _ParserState()

    # Handler registry: (pattern, handler_function)
    handlers: list[tuple[re.Pattern[str], Callable[[re.Match[str], str, list[str], int, _ParserState], int]]] = [
        (re.compile(r"^ASA Version\s+(\S+)$", re.IGNORECASE), _handle_asa_version),
        (re.compile(r"^hostname\s+(\S+)$", re.IGNORECASE), _handle_hostname),
        (re.compile(r"^enable password\s+(\S+)\s+(\S+)$", re.IGNORECASE), _handle_enable_password),
        (
            re.compile(r"^service-module\s+(\S+)\s+keepalive-timeout\s+(\d+)$", re.IGNORECASE),
            _handle_service_module_timeout,
        ),
        (
            re.compile(r"^service-module\s+(\S+)\s+keepalive-counter\s+(\d+)$", re.IGNORECASE),
            _handle_service_module_counter,
        ),
        (re.compile(r"^name\s+(\d{1,3}(?:\.\d{1,3}){3})\s+(\S+)(?:\s+description\s)?", re.IGNORECASE), _handle_name),
        (re.compile(r"^interface\s+\S+", re.IGNORECASE), _handle_interface_block),
        (re.compile(r"^object\s+network\s+\S+$", re.IGNORECASE), _handle_network_object_block),
        (re.compile(r"^object-group\s+network\s+\S+$", re.IGNORECASE), _handle_network_object_group_block),
        (re.compile(r"^object\s+service\s+\S+$", re.IGNORECASE), _handle_service_object_block),
        (
            re.compile(r"^object-group\s+service\s+\S+", re.IGNORECASE),
            _handle_service_object_group,
        ),  # left intentionally without $
        (re.compile(r"^object-group\s+icmp-type\s+\S+$", re.IGNORECASE), _handle_icmp_object_group_block),
        (re.compile(r"^object-group\s+protocol\s+\S+$", re.IGNORECASE), _handle_protocol_object_group_block),
        (re.compile(r"^access-list\s+\S+\s+extended\s+(permit|deny)\s+", re.IGNORECASE), _handle_access_list_entry),
        (re.compile(r"^access-group\s+(\S+)\s+(in|out)\s+interface\s+(\S+)$", re.IGNORECASE), _handle_access_group),
        (re.compile(r"^route\s+(\S+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)(?:\s+(\d+))?$", re.IGNORECASE), _handle_route),
        (
            re.compile(r"^(http|ssh|telnet)\s+(\d+\.\d+\.\d+\.\d+)\s+(\d+\.\d+\.\d+\.\d+)\s+(\S+)$", re.IGNORECASE),
            _handle_mgmt_access,
        ),
        (re.compile(r"^class-map\s+\S+", re.IGNORECASE), _handle_class_map_block),
        (
            re.compile(r"^policy-map\s+type\s+inspect\s+dns\s+(\S+)$", re.IGNORECASE),
            _handle_dns_inspect_policy_map_block,
        ),
        (re.compile(r"^policy-map\s+(\S+)$", re.IGNORECASE), _handle_policy_map_block),
        (re.compile(r"^service-policy\s+(\S+)\s+(global|interface\s+\S+)$", re.IGNORECASE), _handle_service_policy),
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
        self.enable_password: AsaEnablePassword | None = None
        self.service_modules: list[AsaServiceModule] = []
        self.names: list[Names] = []
        self.interfaces: list[Interface] = []
        self.net_objects: list[AsaNetworkObject] = []
        self.net_obj_groups: list[AsaNetworkObjectGroup] = []
        self.svc_objects: list[AsaServiceObject] = []
        self.svc_obj_groups: list[AsaServiceObjectGroup] = []
        self.access_lists_map: dict[str, list[AccessListEntry]] = {}
        self.access_groups: list[AccessGroupBinding] = []
        self.nat_rules: list[NatRule] = []
        self.routes: list[Route] = []
        self.mgmt_access: list[MgmtAccessRule] = []
        self.additional_settings: list[str] = []
        self.class_maps: list[ClassMap] = []
        self.policy_maps: dict[str, PolicyMap] = {}
        self.service_policies: list[ServicePolicyBinding] = []
        self.protocol_groups: list[AsaProtocolGroup] = []


def _handle_asa_version(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    state.asa_version = match.group(1).strip()
    return i + 1


def _handle_hostname(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    state.hostname = match.group(1)
    return i + 1


def _handle_enable_password(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    state.enable_password = AsaEnablePassword(password=match.group(1), encryption_function=match.group(2))
    return i + 1


def _handle_service_module_timeout(
    match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState
) -> int:
    name = match.group(1)
    timeout = int(match.group(2))
    keepalive_counter = _find_keepalive_counter(lines, i, name)
    state.service_modules.append(
        AsaServiceModule(name=name, keepalive_timeout=timeout, keepalive_counter=keepalive_counter)
    )
    return i + 1


def _find_keepalive_counter(lines: list[str], i: int, name: str) -> int:
    for j in range(i + 1, min(i + 5, len(lines))):
        m = re.match(
            rf"^service-module\s+{re.escape(name)}\s+keepalive-counter\s+(\d+)$", lines[j].strip(), re.IGNORECASE
        )
        if m:
            return int(m.group(1))
    return 0


def _handle_service_module_counter(
    match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState
) -> int:
    return i + 1


def _handle_name(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    ip, alias = match.group(1), match.group(2)
    desc = line[match.end() :].strip() or None
    state.names.append(Names(name=alias, ip_address=ip, description=desc))
    return i + 1


def _handle_interface_block(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    block, new_i = consume_block(lines, i)
    state.interfaces.append(parse_interface_block(block))
    return new_i


def _handle_network_object_block(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    block, new_i = consume_block(lines, i)
    net_obj, pending_nat = parse_network_object_block(block)
    if net_obj:
        state.net_objects.append(net_obj)
    if pending_nat:
        state.nat_rules.append(pending_nat)
    return new_i


def _handle_network_object_group_block(
    match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState
) -> int:
    block, new_i = consume_block(lines, i)
    state.net_obj_groups.append(parse_network_object_group_block(block))
    return new_i


def _handle_service_object_block(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    block, new_i = consume_block(lines, i)
    svc_obj = parse_service_object_block(block)
    if svc_obj:
        state.svc_objects.append(svc_obj)
    return new_i


def _handle_service_object_group(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    block, new_i = consume_block(lines, i)
    state.svc_obj_groups.append(parse_service_object_group_block(block))
    return new_i


def _handle_icmp_object_group_block(
    match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState
) -> int:
    block, new_i = consume_block(lines, i)
    state.svc_obj_groups.append(parse_icmp_object_group_block(block))
    return new_i


def _handle_protocol_object_group_block(
    match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState
) -> int:
    block, new_i = consume_block(lines, i)
    state.protocol_groups.append(parse_protocol_object_group_block(block))
    return new_i


def _handle_access_list_entry(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    try:
        entry = parse_access_list_entry(line, state.protocol_groups, state.svc_objects, state.svc_obj_groups)
        state.access_lists_map.setdefault(entry.acl_name, []).append(entry)
    except Exception:
        FWOLogger.warning(f"Failed to parse access-list entry: {line}")
    return i + 1


def _handle_access_group(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    direction = match.group(2)
    if direction not in ("in", "out"):
        raise ValueError(f"Invalid direction value: {direction}")
    state.access_groups.append(
        AccessGroupBinding(acl_name=match.group(1), direction=direction, interface=match.group(3))
    )
    return i + 1


def _handle_route(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    state.routes.append(
        Route(
            interface=match.group(1),
            destination=match.group(2),
            netmask=match.group(3),
            next_hop=match.group(4),
            distance=int(match.group(5)) if match.group(5) else None,
        )
    )
    return i + 1


def _handle_mgmt_access(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    protocol_str = match.group(1).lower()
    if protocol_str not in ("http", "ssh", "telnet"):
        raise ValueError(f"Invalid protocol for MgmtAccessRule: {protocol_str}")
    state.mgmt_access.append(
        MgmtAccessRule(
            protocol=protocol_str, source_ip=match.group(2), source_mask=match.group(3), interface=match.group(4)
        )
    )
    return i + 1


def _handle_class_map_block(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    block, new_i = consume_block(lines, i)
    state.class_maps.append(parse_class_map_block(block))
    return new_i


def _handle_dns_inspect_policy_map_block(
    match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState
) -> int:
    block, new_i = consume_block(lines, i)
    pm_name = match.group(1)
    pm = parse_dns_inspect_policy_map_block(block, pm_name)
    state.policy_maps[pm_name] = pm
    return new_i


def _handle_policy_map_block(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    block, new_i = consume_block(lines, i)
    pm_name = match.group(1)
    pm = parse_policy_map_block(block, pm_name)
    state.policy_maps[pm_name] = pm
    return new_i


def _handle_service_policy(match: re.Match[str], line: str, lines: list[str], i: int, state: _ParserState) -> int:
    pm_name = match.group(1)
    scope_part = match.group(2).lower()
    if scope_part == "global":
        state.service_policies.append(ServicePolicyBinding(policy_map=pm_name, scope="global"))
    else:
        iface = scope_part.split()[1]
        state.service_policies.append(ServicePolicyBinding(policy_map=pm_name, scope="interface", interface=iface))
    return i + 1


def _handle_additional_settings(line: str, state: _ParserState) -> None:
    interesting_prefixes = (
        "ftp mode",
        "same-security-traffic",
        "dynamic-access-policy-record",
        "service-policy",
        "user-identity",
        "aaa ",
        "icmp ",
        "arp ",
        "ssh version",
        "no ssh",
        "ssh cipher",
        "ssh key-exchange",
        "ssh timeout",
        "http server enable",
        "no asdm",
        "asdm ",
        "crypto ",
        "threat-detection",
        "ssl cipher",
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
    FWOLogger.debug(json.dumps(config.model_dump(exclude_none=True)["names"], indent=2))

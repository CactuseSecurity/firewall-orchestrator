"""
Strict parser for ASA extended access-list entries.

Every token of an entry must be understood. Unknown, truncated or unsupported syntax raises an
AsaConfigParseError, which fails the import: a guessed meaning (e.g. "any" for an address that was not
understood) could make the imported policy more permissive than the real one.
"""

from __future__ import annotations

import re
from ipaddress import IPv4Address, IPv6Address, IPv6Network

from fw_modules.ciscoasa9.asa_maps import name_to_port
from fw_modules.ciscoasa9.asa_models import (
    AccessListEntry,
    AsaProtocolGroup,
    AsaServiceObject,
    AsaServiceObjectGroup,
    EndpointKind,
)
from fwo_exceptions import FwoImporterError

PORT_OPERATORS = ("eq", "neq", "lt", "gt", "range")
OPTION_KEYWORDS = ("log", "time-range", "inactive")
ICMP_PROTOCOLS = ("icmp", "icmp6")
ICMP_TYPE_NAMES = (
    "alternate-address",
    "conversion-error",
    "echo",
    "echo-reply",
    "information-reply",
    "information-request",
    "mask-reply",
    "mask-request",
    "membership-query",
    "membership-reduction",
    "membership-report",
    "mobile-redirect",
    "neighbor-advertisement",
    "neighbor-redirect",
    "neighbor-solicitation",
    "packet-too-big",
    "parameter-problem",
    "redirect",
    "router-advertisement",
    "router-renumbering",
    "router-solicitation",
    "source-quench",
    "time-exceeded",
    "timestamp-reply",
    "timestamp-request",
    "traceroute",
    "unreachable",
)
MAX_ICMP_TYPE = 255
# address arguments that restrict an entry beyond ip addresses (identity, security groups, the box itself)
UNSUPPORTED_ADDRESS_KEYWORDS = (
    "interface",
    "user",
    "user-group",
    "object-group-user",
    "security-group",
    "object-group-security",
)
LOG_LEVELS = (
    "emergencies",
    "alerts",
    "critical",
    "errors",
    "warnings",
    "notifications",
    "informational",
    "debugging",
)
MAX_LOG_LEVEL = 7
MIN_PORT = 0
MAX_PORT = 65535
ACL_HEADER_TOKENS = 2  # "access-list" NAME
LINE_NUMBER_TOKENS = 2  # optional "line" N
ENDPOINT_WITH_ARGUMENT_TOKENS = 2  # e.g. "host" A.B.C.D or A.B.C.D MASK
IPV4_PATTERN = re.compile(r"\d{1,3}(?:\.\d{1,3}){3}")
IPV4_ALL_BITS = 0xFFFFFFFF


class AsaConfigParseError(FwoImporterError):
    """An ASA config construct the importer does not understand; the import stops instead of guessing."""

    def __init__(
        self,
        reason: str,
        token_position: int | None = None,
        line: str | None = None,
        line_number: int | None = None,
    ) -> None:
        self.reason = reason
        self.token_position = token_position
        self.line = line
        self.line_number = line_number
        super().__init__(self._format())

    def _format(self) -> str:
        location = "ASA config"
        if self.line_number is not None:
            location += f" line {self.line_number}"
        if self.token_position is not None:
            location += f" token {self.token_position + 1}"
        message = f"{location}: {self.reason}"
        if self.line is not None:
            message += f" in '{self.line}'"
        return message + " - import stopped, the entry would otherwise be imported with a different meaning"

    def at_line(self, line: str, line_number: int | None) -> AsaConfigParseError:
        """Return the same error with the config line it occurred in."""
        return AsaConfigParseError(self.reason, self.token_position, line, line_number)


def parse_access_list_entry(
    line: str,
    protocol_groups: list[AsaProtocolGroup],
    svc_objects: list[AsaServiceObject],
    svc_obj_groups: list[AsaServiceObjectGroup],
) -> AccessListEntry:
    """
    Parse an extended access-list entry; every token must be understood.

    Syntax: access-list NAME [line N] extended {permit|deny} PROTOCOL SOURCE [SOURCE_PORT] DESTINATION
            [PORT | ICMP_TYPE] [log [LEVEL] [interval SECS] | log disable | log default] [time-range NAME] [inactive]

    Raises:
        AsaConfigParseError: for unknown, truncated or unsupported syntax (e.g. source ports, interface addresses)

    """
    parts = line.split()
    pos = _parse_acl_header(parts)
    action = _expect_token(parts, pos, "action").lower()
    if action not in ("permit", "deny"):
        raise AsaConfigParseError(f"unknown action '{parts[pos]}'", pos)

    protocol, pos = _parse_protocol(parts, pos + 1, protocol_groups, svc_objects, svc_obj_groups)
    service_group_names = {group.name for group in svc_obj_groups}
    src, pos = parse_endpoint(parts, pos)
    _reject_source_port(parts, pos, service_group_names)
    dst, pos = parse_endpoint(parts, pos)
    dst_port, pos = _parse_destination_service(parts, pos, protocol)
    inactive, time_range = _parse_options(parts, pos)

    return AccessListEntry(
        acl_name=parts[1],
        action="permit" if action == "permit" else "deny",
        protocol=protocol,
        src=src,
        dst=dst,
        dst_port=dst_port,
        inactive=inactive,
        time_range=time_range,
    )


def parse_endpoint(parts: list[str], pos: int) -> tuple[EndpointKind, int]:
    """
    Parse an ACL address argument starting at parts[pos]; returns (EndpointKind, next position).

    Supported: any, any4, any6, host ADDRESS|NAME, object NAME, object-group NAME,
    A.B.C.D MASK (A.B.C.D may be a name alias), IPv6 prefix X:X::X/N.
    """
    keyword = _expect_token(parts, pos, "address").lower()
    if keyword in ("any", "any4"):
        return EndpointKind(kind="any", value="any"), pos + 1
    if keyword == "any6":
        return EndpointKind(kind="any6", value="any6"), pos + 1
    if keyword in ("host", "object", "object-group"):
        value = _expect_token(parts, pos + 1, f"{keyword} name or address")
        if keyword != "host":
            return EndpointKind(kind=keyword, value=value), pos + ENDPOINT_WITH_ARGUMENT_TOKENS
        kind = "hostv6" if _is_ipv6_address(value) else "host"  # a non-ip host value is a name alias
        return EndpointKind(kind=kind, value=value), pos + ENDPOINT_WITH_ARGUMENT_TOKENS
    if keyword in UNSUPPORTED_ADDRESS_KEYWORDS:
        construct = " ".join(parts[pos : pos + ENDPOINT_WITH_ARGUMENT_TOKENS])
        raise AsaConfigParseError(f"unsupported address '{construct}'", pos)
    if _is_ipv6_prefix(parts[pos]):
        return EndpointKind(kind="subnetv6", value=parts[pos]), pos + 1
    if pos + 1 < len(parts) and _is_ipv4_netmask(parts[pos + 1]):
        # A.B.C.D MASK; with "names" enabled the address can be a name alias
        return EndpointKind(kind="subnet", value=parts[pos], mask=parts[pos + 1]), pos + ENDPOINT_WITH_ARGUMENT_TOKENS
    raise AsaConfigParseError(f"unsupported address '{parts[pos]}'", pos)


def _parse_acl_header(parts: list[str]) -> int:
    """Check 'access-list NAME [line N] extended' and return the position of the action."""
    if len(parts) < ACL_HEADER_TOKENS or parts[0].lower() != "access-list":
        raise AsaConfigParseError("not an access-list entry", 0)
    pos = ACL_HEADER_TOKENS
    if _expect_token(parts, pos, "'extended'").lower() == "line":
        _expect_token(parts, pos + 1, "line number")
        pos += LINE_NUMBER_TOKENS
    if _expect_token(parts, pos, "'extended'").lower() != "extended":
        raise AsaConfigParseError(f"unsupported access-list type '{parts[pos]}'", pos)
    return pos + 1


def _parse_protocol(
    parts: list[str],
    pos: int,
    protocol_groups: list[AsaProtocolGroup],
    svc_objects: list[AsaServiceObject],
    svc_obj_groups: list[AsaServiceObjectGroup],
) -> tuple[EndpointKind, int]:
    """Parse the protocol argument: a protocol, object-group (protocol or service) or object (service)."""
    keyword = _expect_token(parts, pos, "protocol")
    if keyword.lower() == "object-group":
        group_name = _expect_token(parts, pos + 1, "object-group name")
        if any(group.name == group_name for group in protocol_groups):
            return EndpointKind(kind="protocol-group", value=group_name), pos + 2
        if any(group.name == group_name for group in svc_obj_groups):
            return EndpointKind(kind="service-group", value=group_name), pos + 2
        raise AsaConfigParseError(f"unknown protocol or service object-group '{group_name}'", pos + 1)
    if keyword.lower() == "object":
        obj_name = _expect_token(parts, pos + 1, "service object name")
        if any(obj.name == obj_name for obj in svc_objects):
            return EndpointKind(kind="service", value=obj_name), pos + 2
        raise AsaConfigParseError(f"unknown service object '{obj_name}'", pos + 1)
    return EndpointKind(kind="protocol", value=keyword.lower()), pos + 1


def _reject_source_port(parts: list[str], pos: int, service_group_names: set[str]) -> None:
    """Source ports cannot be imported; ignoring them would widen the entry to all source ports."""
    if pos >= len(parts):
        return
    keyword = parts[pos].lower()
    if keyword in PORT_OPERATORS:
        construct = " ".join(parts[pos : pos + 2])
        raise AsaConfigParseError(f"unsupported source port '{construct}'", pos)
    if keyword == "object-group" and pos + 1 < len(parts) and parts[pos + 1] in service_group_names:
        raise AsaConfigParseError(f"unsupported source port object-group '{parts[pos + 1]}'", pos)


def _parse_destination_service(parts: list[str], pos: int, protocol: EndpointKind) -> tuple[EndpointKind, int]:
    """Parse the optional destination port (or icmp type) argument following the destination address."""
    dst_port, pos = _parse_explicit_destination_service(parts, pos, protocol)
    if dst_port is not None:
        return dst_port, pos
    # without a port argument the protocol argument defines the service
    if protocol.kind in ("service-group", "service"):
        return EndpointKind(kind=protocol.kind, value=protocol.value), pos
    return EndpointKind(kind="any", value="any"), pos


def _parse_explicit_destination_service(
    parts: list[str], pos: int, protocol: EndpointKind
) -> tuple[EndpointKind | None, int]:
    if pos >= len(parts):
        return None, pos
    keyword = parts[pos].lower()
    if keyword in PORT_OPERATORS:
        return _parse_port_operator(parts, pos)
    if keyword == "object-group":
        return EndpointKind(kind="service-group", value=_expect_token(parts, pos + 1, "service object-group")), pos + 2
    if keyword == "object":
        return EndpointKind(kind="service", value=_expect_token(parts, pos + 1, "service object")), pos + 2
    if protocol.kind == "protocol" and protocol.value in ICMP_PROTOCOLS and keyword not in OPTION_KEYWORDS:
        return _parse_icmp_type(parts, pos)
    return None, pos


def _parse_port_operator(parts: list[str], pos: int) -> tuple[EndpointKind, int]:
    """
    Parse eq/lt/gt/range; neq cannot be represented as a single port range.

    Every port must be a number from 0 to 65535 or a known port name. eq and range keep the port as written,
    so the rule uids of valid entries stay the same.
    """
    operator = parts[pos].lower()
    if operator == "neq":
        raise AsaConfigParseError(f"unsupported port operator 'neq {' '.join(parts[pos + 1 : pos + 2])}'", pos)
    port = _expect_token(parts, pos + 1, f"port after '{operator}'")
    port_number = _port_number(port, pos + 1)
    if operator == "eq":
        return EndpointKind(kind="eq", value=port), pos + 2
    if operator == "range":
        port_end = _expect_token(parts, pos + 2, "end port of range")
        if _port_number(port_end, pos + 2) < port_number:
            raise AsaConfigParseError(f"port range '{port} {port_end}' ends before it starts", pos)
        return EndpointKind(kind="range", value=f"{port} {port_end}"), pos + 3
    if operator == "lt":
        if port_number <= MIN_PORT:
            raise AsaConfigParseError(f"port range 'lt {port}' is empty", pos)
        return EndpointKind(kind="range", value=f"{MIN_PORT} {port_number - 1}"), pos + 2
    if port_number >= MAX_PORT:
        raise AsaConfigParseError(f"port range 'gt {port}' is empty", pos)
    return EndpointKind(kind="range", value=f"{port_number + 1} {MAX_PORT}"), pos + 2


def _parse_icmp_type(parts: list[str], pos: int) -> tuple[EndpointKind, int]:
    """Parse an icmp type; an icmp code (a number after the type) cannot be imported."""
    icmp_type = parts[pos]
    if not _is_icmp_type(icmp_type):
        raise AsaConfigParseError(f"unknown icmp type '{icmp_type}'", pos)
    if pos + 1 < len(parts) and parts[pos + 1].isdigit():
        raise AsaConfigParseError(f"unsupported icmp code '{icmp_type} {parts[pos + 1]}'", pos + 1)
    return EndpointKind(kind="eq", value=icmp_type), pos + 1


def _parse_options(parts: list[str], pos: int) -> tuple[bool, str | None]:
    """Parse the trailing options; returns (inactive, time_range). Unknown tokens are rejected."""
    inactive = False
    time_range: str | None = None
    while pos < len(parts):
        keyword = parts[pos].lower()
        if keyword == "log":
            pos = _skip_log_options(parts, pos + 1)
        elif keyword == "time-range":
            time_range = _expect_token(parts, pos + 1, "time-range name")
            pos += 2
        elif keyword == "inactive":
            inactive = True
            pos += 1
        else:
            raise AsaConfigParseError(f"unexpected token '{parts[pos]}'", pos)
    return inactive, time_range


def _skip_log_options(parts: list[str], pos: int) -> int:
    """Skip the arguments of 'log': [LEVEL] [interval SECS] | disable | default (logging does not change matching)."""
    if pos < len(parts) and parts[pos].lower() in ("disable", "default"):
        return pos + 1
    if pos < len(parts) and _is_log_level(parts[pos]):
        pos += 1
    if pos < len(parts) and parts[pos].lower() == "interval":
        interval = _expect_token(parts, pos + 1, "log interval")
        if not interval.isdigit():
            raise AsaConfigParseError(f"invalid log interval '{interval}'", pos + 1)
        pos += 2
    return pos


def _expect_token(parts: list[str], pos: int, expected: str) -> str:
    if pos >= len(parts):
        raise AsaConfigParseError(f"entry ends where {expected} is expected", pos)
    return parts[pos]


def _port_number(port: str, pos: int) -> int:
    if port.isdigit():
        number = int(port)
    elif port in name_to_port:
        number = int(name_to_port[port]["port"])
    else:
        raise AsaConfigParseError(f"unknown port '{port}'", pos)
    if not MIN_PORT <= number <= MAX_PORT:
        raise AsaConfigParseError(f"port '{port}' out of range", pos)
    return number


def _is_log_level(token: str) -> bool:
    return (token.isdigit() and int(token) <= MAX_LOG_LEVEL) or token.lower() in LOG_LEVELS


def _is_ipv4_address(token: str) -> bool:
    if not IPV4_PATTERN.fullmatch(token):
        return False
    try:
        IPv4Address(token)
    except ValueError:
        return False
    return True


def _is_ipv4_netmask(token: str) -> bool:
    """True for a contiguous netmask such as 255.255.255.0 (ASA ACLs use netmasks, not wildcard masks)."""
    if not _is_ipv4_address(token):
        return False
    host_bits = ~int(IPv4Address(token)) & IPV4_ALL_BITS
    return host_bits & (host_bits + 1) == 0


def _is_icmp_type(token: str) -> bool:
    return (token.isdigit() and int(token) <= MAX_ICMP_TYPE) or token.lower() in ICMP_TYPE_NAMES


def _is_ipv6_address(token: str) -> bool:
    try:
        IPv6Address(token)
    except ValueError:
        return False
    return True


def _is_ipv6_prefix(token: str) -> bool:
    if "/" not in token or ":" not in token:
        return False
    try:
        IPv6Network(token, strict=False)
    except ValueError:
        return False
    return True

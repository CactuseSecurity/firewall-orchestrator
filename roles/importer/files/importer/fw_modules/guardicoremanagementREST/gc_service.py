from __future__ import annotations

from typing import TYPE_CHECKING

import fwo_const
from fw_modules.guardicoremanagementREST import gc_const
from fwo_log import FWOLogger
from models.serviceobject import ServiceObject

if TYPE_CHECKING:
    from fw_modules.guardicoremanagementREST.gc_models import GcIcmpMatch, GcRule

PortRange = tuple[int, int]
FULL_PORT_RANGE: PortRange = (gc_const.MIN_PORT, gc_const.MAX_PORT)


def merge_port_ranges(ranges: list[PortRange]) -> list[PortRange]:
    """Sort the port ranges and merge overlapping or adjacent ones."""
    merged: list[PortRange] = []
    for start, end in sorted(ranges):
        if merged and start <= merged[-1][1] + 1:
            merged[-1] = (merged[-1][0], max(merged[-1][1], end))
        else:
            merged.append((start, end))
    return merged


def subtract_port_ranges(ranges: list[PortRange], excluded: list[PortRange]) -> list[PortRange]:
    """Remove the excluded ports from the port ranges."""
    remaining = merge_port_ranges(ranges)
    for ex_start, ex_end in merge_port_ranges(excluded):
        next_remaining: list[PortRange] = []
        for start, end in remaining:
            if ex_end < start or ex_start > end:
                next_remaining.append((start, end))
                continue
            if start < ex_start:
                next_remaining.append((start, ex_start - 1))
            if end > ex_end:
                next_remaining.append((ex_end + 1, end))
        remaining = next_remaining
    return remaining


def get_rule_port_ranges(rule: GcRule) -> list[PortRange]:
    """Return the destination port ranges of a rule; no ports in the rule means all ports."""
    included: list[PortRange] = [(port, port) for port in rule.ports]
    included += [(port_range.start, port_range.end) for port_range in rule.port_ranges]
    excluded: list[PortRange] = [(port, port) for port in rule.exclude_ports]
    excluded += [(port_range.start, port_range.end) for port_range in rule.exclude_port_ranges]
    return subtract_port_ranges(included or [FULL_PORT_RANGE], excluded)


def get_protocol_number(protocol: str) -> int | None:
    proto_number = gc_const.GC_IP_PROTOCOLS.get(protocol.upper())
    if proto_number is not None:
        return proto_number
    if protocol.isdigit():
        return int(protocol)
    return None


def build_port_service(proto_name: str, proto_number: int, port_range: PortRange) -> ServiceObject:
    start, end = port_range
    if port_range == FULL_PORT_RANGE:
        name = proto_name
    elif start == end:
        name = f"{proto_name}/{start}"
    else:
        name = f"{proto_name}/{start}-{end}"
    return ServiceObject(
        svc_uid=name,
        svc_name=name,
        svc_port=start,
        svc_port_end=end,
        svc_color=fwo_const.DEFAULT_COLOR,
        svc_typ="simple",
        ip_proto=proto_number,
    )


def build_protocol_service(proto_name: str, proto_number: int, icmp_match: GcIcmpMatch | None) -> ServiceObject:
    """Build a service for a whole protocol, narrowed to an ICMP type and codes if given."""
    name = proto_name
    if icmp_match is not None and icmp_match.icmp_type is not None:
        name = f"{proto_name} type {icmp_match.icmp_type}"
        if icmp_match.icmp_codes:
            name += " code " + ",".join(str(code) for code in sorted(icmp_match.icmp_codes))
    return ServiceObject(
        svc_uid=name,
        svc_name=name,
        svc_color=fwo_const.DEFAULT_COLOR,
        svc_typ="simple",
        ip_proto=proto_number,
    )


def build_any_service() -> ServiceObject:
    return ServiceObject(
        svc_uid=gc_const.GC_ANY_SERVICE_UID,
        svc_name=gc_const.GC_ANY_SERVICE_NAME,
        svc_color=fwo_const.DEFAULT_COLOR,
        svc_typ="simple",
        ip_proto=fwo_const.ANY_IP_PROTOCOL_ID,
    )


def build_rule_services(rule: GcRule) -> list[ServiceObject]:
    """
    Build the services of a rule: every protocol combined with the rule's ports (TCP, UDP) or ICMP matches.

    A rule without protocols matches any service. If no service is left (unknown protocols, all ports excluded),
    the list is empty.
    """
    if len(rule.ip_protocols) == 0:
        return [build_any_service()]
    services: list[ServiceObject] = []
    for protocol in rule.ip_protocols:
        proto_number = get_protocol_number(protocol)
        if proto_number is None:
            FWOLogger.warning(f"Guardicore rule {rule.id}: ignoring unknown protocol '{protocol}'")
            continue
        proto_name = protocol.lower()
        if proto_number in gc_const.GC_PORTED_PROTOCOLS:
            services += [build_port_service(proto_name, proto_number, rng) for rng in get_rule_port_ranges(rule)]
        elif proto_number in {gc_const.IP_PROTO_ICMP, gc_const.IP_PROTO_ICMPV6}:
            matches: list[GcIcmpMatch | None] = list(rule.icmp_matches) or [None]
            services += [build_protocol_service(proto_name, proto_number, match) for match in matches]
        else:
            services.append(build_protocol_service(proto_name, proto_number, None))
    return services

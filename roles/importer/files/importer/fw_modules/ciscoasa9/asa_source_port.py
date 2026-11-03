"""Preserve source-port restrictions when resolving ASA services and service groups."""

from __future__ import annotations

from typing import Final

import fwo_base
import fwo_const
from fw_modules.ciscoasa9.asa_maps import name_to_port, protocol_map
from fw_modules.ciscoasa9.asa_models import AsaServiceObject, EndpointKind
from models.serviceobject import ServiceObject

k_max_port: Final[int] = 65535
k_max_group_depth: Final[int] = 100
k_port_protocols: Final[tuple[int, ...]] = (6, 17, 132)


def normalize_source_service(service: AsaServiceObject) -> list[ServiceObject]:
    """Normalize a named or inline transport service with both source and destination ports."""
    if service.src_port is None:
        raise ValueError(f"Service '{service.name}' has no source ports.")
    start, end = port_bounds(service.src_port)
    destination = EndpointKind(kind="range", value=f"0 {k_max_port}")
    if service.dst_port_eq is not None:
        destination = EndpointKind(kind="eq", value=service.dst_port_eq)
    elif service.dst_port_range is not None:
        destination = EndpointKind(kind="range", value=" ".join(service.dst_port_range))
    port, port_end = port_bounds(destination)
    return [
        ServiceObject(
            svc_uid=service.name,
            svc_name=service.name,
            svc_color=fwo_const.DEFAULT_COLOR,
            svc_typ="simple",
            ip_proto=protocol_map[protocol],
            svc_comment=service.description,
            svc_port=port,
            svc_port_end=port_end,
            svc_source_port=start,
            svc_source_port_end=end,
        )
        for protocol in service.protocol.split("-")
    ]


def port_bounds(endpoint: EndpointKind) -> tuple[int, int]:
    """Return a validated parser port selection as numeric inclusive bounds."""
    values = endpoint.value.split()
    start = int(values[0]) if values[0].isdigit() else int(name_to_port[values[0]]["port"])
    if endpoint.kind == "eq":
        return start, start
    if endpoint.kind == "range":
        end = int(values[1]) if values[1].isdigit() else int(name_to_port[values[1]]["port"])
        return start, end
    raise ValueError(f"Unsupported source port '{endpoint.kind} {endpoint.value}'.")


def service_leaves(reference: str, services: dict[str, ServiceObject], depth: int = 0) -> list[ServiceObject]:
    """Resolve service-group leaves with a bound on cyclic or deeply nested input."""
    if depth > k_max_group_depth:
        raise ValueError(f"Service group '{reference}' exceeds {k_max_group_depth} levels.")
    service = services.get(reference)
    if service is None:
        raise ValueError(f"Unknown service '{reference}' in source-port restriction.")
    if service.svc_typ != "group":
        return [service]
    leaves: list[ServiceObject] = []
    for member in (service.svc_member_refs or "").split(fwo_const.LIST_DELIMITER):
        if member:
            leaves.extend(service_leaves(member, services, depth + 1))
    return leaves


def store_qualified_service(
    service: ServiceObject, services: dict[str, ServiceObject], reserved_names: frozenset[str] = frozenset()
) -> str:
    """Store a generated service without overwriting a configured object of the same name."""
    digest = fwo_base.generate_hash_from_dict(service.model_dump(exclude={"svc_uid", "svc_name"}))
    reference = f"_FWO_SERVICE_{digest}"
    while reference in services or reference in reserved_names:
        if reference not in reserved_names and reference in services:
            existing = services[reference]
            if existing.model_dump(exclude={"svc_uid", "svc_name"}) == service.model_dump(
                exclude={"svc_uid", "svc_name"}
            ):
                return reference
        reference += "_"
    services[reference] = service.model_copy(update={"svc_uid": reference})
    return reference


def _source_ranges(
    source: EndpointKind, protocol: int | None, services: dict[str, ServiceObject]
) -> list[tuple[int, int]]:
    """Read a source port operator or the port members of a source port group."""
    if source.kind != "service-group":
        return [port_bounds(source)]
    ranges: list[tuple[int, int]] = []
    for service in service_leaves(source.value, services):
        if service.ip_proto != protocol:
            continue
        if service.svc_source_port is not None:
            raise ValueError(f"Source port group '{source.value}' contains a service with source restrictions.")
        ranges.append(_port_group_member_bounds(service))
    return ranges


def _port_group_member_bounds(service: ServiceObject) -> tuple[int, int]:
    """Return the port range a source port group member contributes; a member without ports covers all."""
    start = service.svc_port if service.svc_port is not None else 0
    if service.svc_port_end is not None:
        return start, service.svc_port_end
    if service.svc_port is not None:
        return start, service.svc_port
    return start, k_max_port


def _intersect_source_range(service: ServiceObject, source_start: int, source_end: int) -> tuple[int, int] | None:
    """Intersect a source range with the service's own source restriction; None if they are disjoint."""
    current_start = service.svc_source_port if service.svc_source_port is not None else 0
    current_end = service.svc_source_port_end if service.svc_source_port_end is not None else k_max_port
    start = max(source_start, current_start)
    end = min(source_end, current_end)
    return (start, end) if start <= end else None


def _qualify_service(service: ServiceObject, source: EndpointKind, services: dict[str, ServiceObject]) -> list[str]:
    """Store one source-qualified copy of a destination service per overlapping source range."""
    if service.ip_proto not in k_port_protocols:
        raise ValueError(f"Source ports require TCP, UDP or SCTP, got '{service.ip_proto}'.")
    references: list[str] = []
    for source_start, source_end in _source_ranges(source, service.ip_proto, services):
        bounds = _intersect_source_range(service, source_start, source_end)
        if bounds is not None:
            start, end = bounds
            qualified = service.model_copy(update={"svc_source_port": start, "svc_source_port_end": end})
            references.append(store_qualified_service(qualified, services))
    return references


def restrict_source_ports(reference: str, source: EndpointKind, services: dict[str, ServiceObject]) -> str:
    """Intersect source constraints with every destination service, preserving both port pairs."""
    references: list[str] = []
    for base_ref in reference.split(fwo_const.LIST_DELIMITER):
        for service in service_leaves(base_ref, services):
            references.extend(_qualify_service(service, source, services))
    if not references:
        raise ValueError(f"Source port '{source.value}' matches no service in '{reference}'.")
    return fwo_base.sort_and_join(list(set(references)))

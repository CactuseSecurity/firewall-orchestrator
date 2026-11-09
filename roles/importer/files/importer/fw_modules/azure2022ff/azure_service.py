from __future__ import annotations

import fwo_const
from fw_modules.azure2022ff.azure_const import (
    ANY_OBJECT_NAME,
    AZURE_ANY,
    AZURE_IP_PROTOCOL_NUMBERS,
    AZURE_PORT_MAX,
    AZURE_PORT_MIN,
    AZURE_PROTOCOL_ANY,
    AZURE_PROTOCOL_ICMP,
)
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger
from models.serviceobject import ServiceObject


def create_any_service_object() -> ServiceObject:
    """
    Create the service object representing any protocol on any port.

    Returns:
        ServiceObject: The 'any' service object.

    """
    return ServiceObject(
        svc_uid=ANY_OBJECT_NAME,
        svc_name=ANY_OBJECT_NAME,
        svc_typ="simple",
        ip_proto=fwo_const.ANY_IP_PROTOCOL_ID,
        svc_color=fwo_const.DEFAULT_COLOR,
        svc_comment="service object created during import",
    )


def parse_port_range(port: str) -> tuple[int, int]:
    """
    Parse an Azure destination port: a single port, a range 'a-b' or '*'.

    Args:
        port: The Azure port string.

    Returns:
        tuple[int, int]: Start and end port.

    """
    port = port.strip()
    if port == AZURE_ANY:
        return AZURE_PORT_MIN, AZURE_PORT_MAX
    start_raw, _, end_raw = port.partition("-")
    try:
        port_start = int(start_raw)
        port_end = int(end_raw) if end_raw else port_start
    except ValueError:
        raise FwoImporterError(f"Invalid Azure destination port '{port}'.") from None
    if not AZURE_PORT_MIN <= port_start <= port_end <= AZURE_PORT_MAX:
        raise FwoImporterError(f"Invalid Azure destination port '{port}'.")
    return port_start, port_end


def _get_ip_proto(protocol: str) -> int | None:
    if protocol == AZURE_PROTOCOL_ANY:
        return fwo_const.ANY_IP_PROTOCOL_ID
    ip_proto = AZURE_IP_PROTOCOL_NUMBERS.get(protocol.upper())
    if ip_proto is None:
        FWOLogger.warning(f"Unknown Azure IP protocol '{protocol}' - creating service without protocol number")
    return ip_proto


def create_port_service(protocol: str, port: str) -> ServiceObject:
    """
    Create a service object for a protocol and destination port of an Azure network rule.

    Args:
        protocol: The Azure IP protocol (TCP, UDP, Any or other).
        port: The Azure destination port.

    Returns:
        ServiceObject: The service object, its uid is derived from protocol and port.

    """
    port_start, port_end = parse_port_range(port)
    if port.strip() == AZURE_ANY:
        port_text = ANY_OBJECT_NAME
    elif port_start == port_end:
        port_text = str(port_start)
    else:
        port_text = f"{port_start}-{port_end}"
    name = f"{protocol}_{port_text}"
    return ServiceObject(
        svc_uid=name,
        svc_name=name,
        svc_typ="simple",
        ip_proto=_get_ip_proto(protocol),
        svc_port=port_start,
        svc_port_end=port_end,
        svc_color=fwo_const.DEFAULT_COLOR,
    )


def create_icmp_service() -> ServiceObject:
    return ServiceObject(
        svc_uid=AZURE_PROTOCOL_ICMP,
        svc_name=AZURE_PROTOCOL_ICMP,
        svc_typ="simple",
        ip_proto=AZURE_IP_PROTOCOL_NUMBERS[AZURE_PROTOCOL_ICMP],
        svc_color=fwo_const.DEFAULT_COLOR,
    )


def get_rule_services(ip_protocols: list[str], destination_ports: list[str]) -> list[ServiceObject]:
    """
    Build the services of an Azure network rule from its IP protocols and destination ports.

    ICMP has no ports, so it results in a single ICMP service. 'Any' protocol on any port is the 'any' service.

    Args:
        ip_protocols: The rule's IP protocols.
        destination_ports: The rule's destination ports.

    Returns:
        list[ServiceObject]: The services in rule order without duplicates.

    """
    if not ip_protocols:
        raise FwoImporterError("Azure network rule without IP protocols.")
    services: dict[str, ServiceObject] = {}
    for protocol in ip_protocols:
        if protocol.upper() == AZURE_PROTOCOL_ICMP:
            service = create_icmp_service()
            services.setdefault(service.svc_uid, service)
            continue
        if protocol == AZURE_PROTOCOL_ANY and AZURE_ANY in (p.strip() for p in destination_ports):
            service = create_any_service_object()
            services.setdefault(service.svc_uid, service)
            continue
        for port in destination_ports:
            service = create_port_service(protocol, port)
            services.setdefault(service.svc_uid, service)
    if not services:
        raise FwoImporterError("Azure network rule without destination ports.")
    return list(services.values())

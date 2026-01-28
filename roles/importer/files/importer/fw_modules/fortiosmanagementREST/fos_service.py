from collections.abc import Generator

import fwo_const
from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig, SvcObjCustom
from models.serviceobject import ServiceObject


def normalize_app_service_objects(native_config: FortiOSConfig) -> Generator[ServiceObject]:
    """
    Normalize service objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.

    Yields:
        ServiceObject: The normalized service object.

    """
    for svc_obj in native_config.svc_obj_application_list:
        yield ServiceObject(
            svc_uid=svc_obj.name,
            svc_name=svc_obj.name,
            svc_color=fwo_const.DEFAULT_COLOR,
            svc_typ="simple",
        )


def parse_ports(ports: str) -> list[tuple[int | None, int | None]]:
    """
    Parse ports string into a list of port ranges. Possible formats:
    - "80"
    - "80-90"
    - "80 90 100-110"
    - "513:512-514" (destination:source) -> only destination ports are considered

    Args:
        ports (str): The ports string.

    Returns:
        list[tuple[int | None, int | None]]: List of port ranges as tuples (start, end).

    """
    port_ranges: list[tuple[int | None, int | None]] = []
    for part in ports.split():
        port_str = part
        if ":" in port_str:
            dest_port, _ = port_str.split(":", 1)
            port_str = dest_port
        if "-" in port_str:
            start_str, end_str = port_str.split("-", 1)
            port_ranges.append((int(start_str), int(end_str)))
        else:
            port = int(port_str)
            port_ranges.append((port, port))
    return port_ranges


def get_svcobjs_from_portrange(
    portrange: str, base_name: str, comment: str | None, ip_proto: int, members: list[str]
) -> Generator[ServiceObject]:
    """
    Generate service objects from a port range string.

    Args:
        portrange (str): The port range string.
        base_name (str): The base name for the service object.
        comment (str | None): The comment for the service object.
        ip_proto (int): The IP protocol number.
        members (list[str]): List reference to collect members for parent group object.


    Yields:
        ServiceObject: The generated service object.

    """
    port_ranges = parse_ports(portrange)
    for svc_port, svc_port_end in port_ranges:
        name = base_name
        if len(port_ranges) > 1:
            name += f"_{svc_port}"
            if svc_port != svc_port_end:
                name += f"-{svc_port_end}"
        members.append(name)
        yield ServiceObject(
            svc_name=name,
            svc_uid=name,
            svc_typ="simple",
            ip_proto=ip_proto,
            svc_port=svc_port,
            svc_port_end=svc_port_end,
            svc_color=fwo_const.DEFAULT_COLOR,
            svc_comment=comment if len(port_ranges) == 1 else None,  # only assign comment if single object
        )


def normalize_single_custom_service_object(svc_obj: SvcObjCustom) -> Generator[ServiceObject]:
    """
    Normalize a single custom service object from the native FortiOS configuration.

    Args:
        svc_obj (SvcObjCustom): The native custom service object.

    Yields:
        ServiceObject: The normalized service object.

    """
    # determine if multiple port ranges are defined
    split = (
        sum(1 for port_range in [svc_obj.tcp_portrange, svc_obj.udp_portrange, svc_obj.sctp_portrange] if port_range)
        >= 2  # noqa: PLR2004
    )
    members: list[str] = []
    # only assign comment to parent group object
    comment = svc_obj.comment if svc_obj.comment and not split else None
    if svc_obj.tcp_portrange:
        base_name = f"{svc_obj.name}_tcp" if split else svc_obj.name
        yield from get_svcobjs_from_portrange(svc_obj.tcp_portrange, base_name, comment, ip_proto=6, members=members)
    if svc_obj.udp_portrange:
        base_name = f"{svc_obj.name}_udp" if split else svc_obj.name
        yield from get_svcobjs_from_portrange(svc_obj.udp_portrange, base_name, comment, ip_proto=17, members=members)
    if svc_obj.sctp_portrange:
        base_name = f"{svc_obj.name}_sctp" if split else svc_obj.name
        yield from get_svcobjs_from_portrange(svc_obj.sctp_portrange, base_name, comment, ip_proto=132, members=members)

    match svc_obj.protocol:
        case "TCP/UDP/SCTP" if len(members) > 1:
            # create parent group object
            yield ServiceObject(
                svc_name=svc_obj.name,
                svc_uid=svc_obj.name,
                svc_typ="group",
                svc_member_names=fwo_const.LIST_DELIMITER.join(members),
                svc_member_refs=fwo_const.LIST_DELIMITER.join(members),
                svc_color=fwo_const.DEFAULT_COLOR,
                svc_comment=svc_obj.comment,
            )
        case "IP":
            # IP protocol service object
            yield ServiceObject(  # TODO: check if ports really not available in this case
                svc_name=svc_obj.name,
                svc_uid=svc_obj.name,
                svc_typ="simple",
                ip_proto=svc_obj.protocol_number,
                svc_color=fwo_const.DEFAULT_COLOR,
                svc_comment=svc_obj.comment,
            )
        case "ICMP" | "ICMP6":
            yield ServiceObject(
                svc_name=svc_obj.name,
                svc_uid=svc_obj.name,
                svc_typ="simple",
                ip_proto=1 if svc_obj.protocol == "ICMP" else 58,
                svc_color=fwo_const.DEFAULT_COLOR,
                svc_comment=svc_obj.comment,
            )
        case "ALL":
            yield ServiceObject(
                svc_name=svc_obj.name,
                svc_uid=svc_obj.name,
                svc_typ="simple",
                ip_proto=0,
                svc_color=fwo_const.DEFAULT_COLOR,
                svc_comment=svc_obj.comment,
            )
        case _:
            pass  # unknown protocol or TCP/UDP/SCTP with only one port range handled above


def normalize_custom_service_objects(native_config: FortiOSConfig) -> Generator[ServiceObject]:
    """
    Normalize custom service objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.

    Yields:
        ServiceObject: The normalized service object.

    """
    for svc_obj in native_config.svc_obj_custom:
        yield from normalize_single_custom_service_object(svc_obj)


def normalize_service_object_groups(native_config: FortiOSConfig) -> Generator[ServiceObject]:
    """
    Normalize service object groups from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.

    Yields:
        ServiceObject: The normalized service object group.

    """
    for svc_obj in native_config.svc_obj_group:
        yield ServiceObject(
            svc_name=svc_obj.name,
            svc_uid=svc_obj.name,
            svc_typ="group",
            svc_member_names=(
                fwo_const.LIST_DELIMITER.join([member.name for member in svc_obj.member]) if svc_obj.member else None
            ),
            svc_member_refs=(
                fwo_const.LIST_DELIMITER.join([member.name for member in svc_obj.member]) if svc_obj.member else None
            ),
            svc_color=fwo_const.DEFAULT_COLOR,
            svc_comment=svc_obj.comment,
        )

    for svc_obj in native_config.svc_obj_application_group:  # TODO: test with data if correct
        yield ServiceObject(
            svc_name=svc_obj.name,
            svc_uid=svc_obj.name,
            svc_typ="group",
            svc_member_names=(
                fwo_const.LIST_DELIMITER.join([app.name for app in svc_obj.application])
                if svc_obj.application
                else None
            ),
            svc_member_refs=(
                fwo_const.LIST_DELIMITER.join([app.name for app in svc_obj.application])
                if svc_obj.application
                else None
            ),
            svc_color=fwo_const.DEFAULT_COLOR,
            svc_comment=svc_obj.comment,
        )


def normalize_service_objects(native_config: FortiOSConfig) -> Generator[ServiceObject]:
    """
    Normalize all service objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.

    Yields:
        ServiceObject: The normalized service object.

    """
    yield from normalize_app_service_objects(native_config)
    yield from normalize_custom_service_objects(native_config)
    yield from normalize_service_object_groups(native_config)
    # "Original" service object for natting
    yield ServiceObject(
        svc_name="Original",
        svc_uid="Original",
        svc_typ="simple",
        svc_color=fwo_const.DEFAULT_COLOR,
        svc_comment="'original' service object created by FWO importer for NAT purposes",
    )
    # "Internet service" object for internet services
    yield ServiceObject(
        svc_name="Internet Service",
        svc_uid="Internet Service",
        svc_typ="group",
        svc_color=fwo_const.DEFAULT_COLOR,
        svc_comment="'internet service' group object created by FWO importer for internet services",
    )

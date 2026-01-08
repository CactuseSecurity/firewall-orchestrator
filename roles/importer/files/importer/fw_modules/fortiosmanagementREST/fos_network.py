from collections.abc import Generator
from ipaddress import IPv6Address

import fwo_const
from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig
from fwo_log import FWOLogger
from models.networkobject import NetworkObject
from netaddr import IPAddress, IPNetwork

DEFAULT_IPv4 = (IPNetwork("0.0.0.0/32"), IPNetwork("255.255.255.255/32"))


def normalize_ipv4_network_objects(
    native_config: FortiOSConfig, nw_obj_lookup_dict: dict[str, str]
) -> Generator[NetworkObject]:
    """
    Normalize IPv4 network objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.
        nw_obj_lookup_dict: Lookup dictionary for network object names to UIDs.

    Yields:
        NetworkObject: The normalized network object.

    """
    for ip4_obj in native_config.nw_obj_address:
        obj_typ = "host"
        if ip4_obj.subnet:
            host, mask = ip4_obj.subnet.split(" ")
            # get ip_start/32 and ip_end/32 from subnet
            network = IPNetwork(f"{host}/{mask}")
            ip_start = IPNetwork(f"{host}/32")
            ip_end = IPNetwork(f"{IPAddress(network.first + network.size - 1)}/32")
            if network.size > 1:
                obj_typ = "network"
        elif ip4_obj.start_ip:
            ip_start = IPNetwork(f"{ip4_obj.start_ip}/32")
            ip_end = IPNetwork(f"{ip4_obj.end_ip}/32")
            obj_typ = "ip_range"
        else:
            FWOLogger.warning(
                f"normalize_ipv4_network_objects: Unable to determine IP range for network object {ip4_obj.name}, setting to full range."
            )
            ip_start, ip_end = DEFAULT_IPv4

        nw_obj_lookup_dict[ip4_obj.name] = ip4_obj.uuid

        yield NetworkObject(
            obj_name=ip4_obj.name,
            obj_uid=ip4_obj.uuid,
            obj_typ=obj_typ,
            obj_ip=ip_start,
            obj_ip_end=ip_end,
            obj_color=fwo_const.DEFAULT_COLOR,
            obj_comment=ip4_obj.comment,
        )


def normalize_ipv6_network_objects(
    native_config: FortiOSConfig, nw_obj_lookup_dict: dict[str, str]
) -> Generator[NetworkObject]:
    """
    Normalize IPv6 network objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.
        nw_obj_lookup_dict: Lookup dictionary for network object names to UIDs.

    Yields:
        NetworkObject: The normalized network object.

    """
    for ip6_obj in native_config.nw_obj_address6:
        obj_typ = "host"
        if ip6_obj.ip6:
            network = IPNetwork(ip6_obj.ip6, version=6)
            ip_start = IPNetwork(f"{IPv6Address(network.first)}/128", version=6)
            if ip6_obj.end_ip:
                ip_end = IPNetwork(f"{ip6_obj.end_ip}/128", version=6)
                if ip_start != ip_end:
                    obj_typ = "ip_range"
            else:
                ip_end = IPNetwork(f"{IPv6Address(network.last)}/128", version=6)
                if network.size > 1:
                    obj_typ = "network"
        else:
            FWOLogger.warning(
                f"normalize_ipv6_network_objects: Unable to determine IP range for network object {ip6_obj.name}, setting to full range."
            )
            ip_start = IPNetwork("::/128", version=6)
            ip_end = IPNetwork("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128", version=6)

        nw_obj_lookup_dict[ip6_obj.name] = ip6_obj.uuid

        yield NetworkObject(
            obj_name=ip6_obj.name,
            obj_uid=ip6_obj.uuid,
            obj_typ=obj_typ,
            obj_ip=ip_start,
            obj_ip_end=ip_end,
            obj_color=fwo_const.DEFAULT_COLOR,
            obj_comment=ip6_obj.comment,
        )


def normalize_nwobj_groups(
    native_config: FortiOSConfig, nw_obj_lookup_dict: dict[str, str]
) -> Generator[NetworkObject]:
    """
    Normalize address, address6 and internet service group objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.
        nw_obj_lookup_dict: Lookup dictionary for network object names to UIDs.

    Yields:
        NetworkObject: The normalized network object.

    """
    for addrgrp_obj in (
        native_config.nw_obj_addrgrp + native_config.nw_obj_addrgrp6 + native_config.nw_obj_internet_service_group
    ):
        members = [member.name for member in addrgrp_obj.member]
        member_refs: list[str] = []
        for member in members:
            if member not in nw_obj_lookup_dict:
                FWOLogger.warning(
                    f"normalize_nwobj_groups: Member object '{member}' of group '{addrgrp_obj.name}' not found in network object lookup."
                )
            else:
                member_refs.append(nw_obj_lookup_dict[member])

        # uid is uuid for addrgrp and addrgrp6, name for internet service group
        obj_uid = getattr(addrgrp_obj, "uuid", None) or addrgrp_obj.name
        nw_obj_lookup_dict[addrgrp_obj.name] = obj_uid

        yield NetworkObject(
            obj_name=addrgrp_obj.name,
            obj_uid=obj_uid,
            obj_typ="group",
            obj_ip=None,
            obj_ip_end=None,
            obj_member_names=fwo_const.LIST_DELIMITER.join(members),
            obj_member_refs=fwo_const.LIST_DELIMITER.join(member_refs),
            obj_color=fwo_const.DEFAULT_COLOR,
            obj_comment=addrgrp_obj.comment,
        )


def normalize_ip_pools(native_config: FortiOSConfig, nw_obj_lookup_dict: dict[str, str]) -> Generator[NetworkObject]:
    """
    Normalize IP pool objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.
        nw_obj_lookup_dict: Lookup dictionary for network object names to UIDs.

    Yields:
        NetworkObject: The normalized network object.

    """
    for ippool_obj in native_config.nw_obj_ippool:
        if ippool_obj.startip and ippool_obj.endip:
            ip_start = IPNetwork(f"{ippool_obj.startip}/32")
            ip_end = IPNetwork(f"{ippool_obj.endip}/32")
        else:
            FWOLogger.warning(
                f"normalize_ip_pools: Unable to determine IP range for IP pool object {ippool_obj.name}, setting to full range."
            )
            ip_start, ip_end = DEFAULT_IPv4
        nw_obj_lookup_dict[ippool_obj.name] = ippool_obj.name

        yield NetworkObject(
            obj_name=ippool_obj.name,
            obj_uid=ippool_obj.name,
            obj_typ="ip_range",
            obj_ip=ip_start,
            obj_ip_end=ip_end,
            obj_color=fwo_const.DEFAULT_COLOR,
            obj_comment=ippool_obj.comments,
        )


def normalize_vips(native_config: FortiOSConfig, nw_obj_lookup_dict: dict[str, str]) -> Generator[NetworkObject]:
    """
    Normalize VIP objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.
        nw_obj_lookup_dict: Lookup dictionary for network object names to UIDs.

    Yields:
        NetworkObject: The normalized network object.

    """
    raise NotImplementedError("normalize_vips is not yet implemented.")  # TODO: need test data


def normalize_internet_services(
    native_config: FortiOSConfig, nw_obj_lookup_dict: dict[str, str]
) -> Generator[NetworkObject]:
    """
    Normalize internet service objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.
        nw_obj_lookup_dict: Lookup dictionary for network object names to UIDs.

    Yields:
        NetworkObject: The normalized network object.

    """
    for is_obj in native_config.nw_obj_internet_service:
        start_ip, end_ip = DEFAULT_IPv4
        nw_obj_lookup_dict[is_obj.name] = is_obj.name
        yield NetworkObject(
            obj_name=is_obj.name,
            obj_uid=is_obj.name,
            obj_typ="network",
            obj_ip=start_ip,
            obj_ip_end=end_ip,
            obj_color=fwo_const.DEFAULT_COLOR,
            obj_comment=None,
        )


def normalize_network_objects(
    native_config: FortiOSConfig, nw_obj_lookup_dict: dict[str, str]
) -> Generator[NetworkObject]:
    """
    Normalize all network objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.
        nw_obj_lookup_dict: Lookup dictionary for network object names to UIDs.

    Yields:
        NetworkObject: The normalized network object.

    """
    yield from normalize_ipv4_network_objects(native_config, nw_obj_lookup_dict)
    yield from normalize_ipv6_network_objects(native_config, nw_obj_lookup_dict)
    # groups may use any of the above objects as members
    yield from normalize_nwobj_groups(native_config, nw_obj_lookup_dict)
    yield from normalize_ip_pools(native_config, nw_obj_lookup_dict)
    # yield from normalize_vips(native_config, nw_obj_lookup_dict) #TODO: implement  # noqa: ERA001
    yield from normalize_internet_services(native_config, nw_obj_lookup_dict)
    # "Original" network object for natting
    yield NetworkObject(
        obj_name="Original",
        obj_uid="Original",
        obj_typ="network",
        obj_ip=DEFAULT_IPv4[0],
        obj_ip_end=DEFAULT_IPv4[1],
        obj_color=fwo_const.DEFAULT_COLOR,
        obj_comment="'original' network object created by FWO importer for NAT purposes",
    )

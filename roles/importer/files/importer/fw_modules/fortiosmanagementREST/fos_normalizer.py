from collections.abc import Generator
from typing import Any

import fwo_const
from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig
from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.networkobject import NetworkObject
from netaddr import IPAddress, IPNetwork


def normalize_config(config_in: FwConfigManagerListController, import_state: ImportStateController) -> None:
    """
    Normalize FortiOS Management REST native configuration.

    Args:
        config_in (FwConfigManagerListController): The configuration manager list controller.
        import_state (ImportStateController): The import state controller.

    """


def normalize_users(native_config: FortiOSConfig) -> list[dict[str, Any]]:
    """
    Normalize a user object.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.

    Returns:
        list[dict[str, Any]]: The list of normalized user objects.

    """
    # user/local
    users: list[dict[str, Any]] = [
        {
            "user_name": user_obj.name,
            "user_uid": user_obj.name,
            "user_typ": "simple",
            "user_color": fwo_const.DEFAULT_COLOR,
            "user_comment": None,
            "user_member_refs": None,
            "user_member_names": None,
        }
        for user_obj in native_config.user_obj_local
    ]

    # user/group
    users.extend(
        [
            {
                "user_name": user_obj.name,
                "user_uid": user_obj.name,
                "user_typ": "group",
                "user_color": fwo_const.DEFAULT_COLOR,
                "user_comment": None,
                "user_member_refs": (
                    fwo_const.LIST_DELIMITER.join([member.name for member in user_obj.member])
                    if user_obj.member
                    else None
                ),
                "user_member_names": (
                    fwo_const.LIST_DELIMITER.join([member.name for member in user_obj.member])
                    if user_obj.member
                    else None
                ),
            }
            for user_obj in native_config.user_obj_group
        ]
    )

    return users


# nw_obj_types = ['firewall/address', 'firewall/address6', 'firewall/addrgrp',
#             'firewall/addrgrp6', 'firewall/ippool', 'firewall/vip',
#             'firewall/internet-service', 'firewall/internet-service-group']


def normalize_ipv4_network_objects(native_config: FortiOSConfig) -> Generator[NetworkObject]:
    """
    Normalize IPv4 network objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.

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
            obj_typ = "range"
        else:
            FWOLogger.warning(
                f"normalize_ipv4_network_objects: Unable to determine IP range for network object {ip4_obj.name}, setting to full range."
            )
            ip_start = IPNetwork("0.0.0.0/32")
            ip_end = IPNetwork("255.255.255.255/32")

        yield NetworkObject(
            obj_name=ip4_obj.name,
            obj_uid=ip4_obj.name,
            obj_typ=obj_typ,
            obj_ip=ip_start,
            obj_ip_end=ip_end,
            obj_color=fwo_const.DEFAULT_COLOR,
            obj_comment=ip4_obj.comment,
        )


def normalize_ipv6_network_objects(native_config: FortiOSConfig) -> Generator[NetworkObject]:
    """
    Normalize IPv6 network objects from the native FortiOS configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.

    Yields:
        NetworkObject: The normalized network object.

    """
    for ip6_obj in native_config.nw_obj_address6:
        obj_typ = "host"
        if ip6_obj.ip6:
            network = IPNetwork(ip6_obj.ip6, version=6)
            ip_start = IPNetwork(f"{IPAddress(network.first)}/128", version=6)
            if ip6_obj.end_ip:
                ip_end = IPNetwork(f"{ip6_obj.end_ip}/128", version=6)
                obj_typ = "range"
            else:
                ip_end = IPNetwork(f"{IPAddress(network.last)}/128", version=6)
                if network.size > 1:
                    obj_typ = "network"
        else:
            FWOLogger.warning(
                f"normalize_ipv6_network_objects: Unable to determine IP range for network object {ip6_obj.name}, setting to full range."
            )
            ip_start = IPNetwork("::/128", version=6)
            ip_end = IPNetwork("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128", version=6)

        yield NetworkObject(
            obj_name=ip6_obj.name,
            obj_uid=ip6_obj.name,
            obj_typ=obj_typ,
            obj_ip=ip_start,
            obj_ip_end=ip_end,
            obj_color=fwo_const.DEFAULT_COLOR,
            obj_comment=ip6_obj.comment,
        )

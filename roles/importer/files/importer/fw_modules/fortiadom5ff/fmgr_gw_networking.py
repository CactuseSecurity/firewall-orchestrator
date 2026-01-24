import traceback
from functools import cmp_to_key
from typing import Any

import fmgr_getter
from fwo_log import FWOLogger
from model_controllers.interface_controller import Interface
from model_controllers.route_controller import Route, get_route_destination
from netaddr import IPAddress, IPNetwork

# Constants
SUBFETCH_HIDDEN = "subfetch hidden"
INTERFACES_PER_DEVICE = "interfaces_per_device/"


def process_ipv4_routing(
    native_config: dict[str, Any], normalized_config: dict[str, Any], dev_id: int, full_vdom_name: str
) -> None:
    """Process IPv4 routing table for a single device/vdom."""
    routing_key = "routing-table-ipv4/" + full_vdom_name

    if routing_key not in native_config:
        FWOLogger.warning("could not find routing data " + routing_key)
        FWOLogger.warning("native configs contains the following keys " + str(native_config.keys()))
        return

    for route in native_config[routing_key]:
        norm_route = Route(
            dev_id,
            route["gateway"],
            route["ip_mask"],
            interface=route["interface"],
            metric=route["metric"],
            distance=route["distance"],
        )
        normalized_config["routing"].append(norm_route)


def process_ipv6_routing(
    native_config: dict[str, Any], normalized_config: dict[str, Any], dev_id: int, full_vdom_name: str
) -> None:
    """Process IPv6 routing table for a single device/vdom."""
    routing_key = "routing-table-ipv6/" + full_vdom_name

    if routing_key not in native_config:
        FWOLogger.warning("could not find routing data " + routing_key)
        if FWOLogger.is_debug_level(6):
            FWOLogger.warning("native configs contains the following keys " + str(native_config.keys()))
        return

    for route in native_config[routing_key]:
        norm_route = Route(
            dev_id,
            route["gateway"],
            route["ip_mask"],
            metric=route["metric"],
            distance=route["distance"],
            interface=route["interface"],
            ip_version=6,
        )
        normalized_config["routing"].append(norm_route)


def process_device_interfaces(
    native_config: dict[str, Any], normalized_config: dict[str, Any], dev_id: int, full_vdom_name: str
) -> None:
    """Process interfaces for a single device/vdom."""
    interfaces_key = INTERFACES_PER_DEVICE + full_vdom_name

    if interfaces_key not in native_config:
        return

    for interface in native_config[interfaces_key]:
        # Process IPv6 interface
        if "ipv6" in interface and "ip6-address" in interface["ipv6"] and interface["ipv6"]["ip6-address"] != "::/0":
            ipv6, netmask_bits = interface["ipv6"]["ip6-address"].split("/")
            norm_if_v6 = Interface(dev_id, interface["name"], IPAddress(ipv6), netmask_bits, ip_version=6)
            normalized_config["interfaces"].append(norm_if_v6)

        # Process IPv4 interface
        if "ip" in interface and interface["ip"] != ["0.0.0.0", "0.0.0.0"]:
            ipv4 = IPAddress(interface["ip"][0])
            netmask_bits = IPAddress(interface["ip"][1]).netmask_bits()
            norm_if_v4 = Interface(dev_id, interface["name"], ipv4, netmask_bits, ip_version=4)
            normalized_config["interfaces"].append(norm_if_v4)


def normalize_network_data(
    native_config: dict[str, Any], normalized_config: dict[str, Any], mgm_details: dict[str, Any]
) -> None:
    """Normalize network data by processing routing tables and interfaces for all devices."""
    normalized_config.update({"routing": [], "interfaces": []})

    for dev_id, _, _, full_vdom_name in get_all_dev_names(mgm_details["devices"]):
        # Process routing tables
        process_ipv4_routing(native_config, normalized_config, dev_id, full_vdom_name)
        process_ipv6_routing(native_config, normalized_config, dev_id, full_vdom_name)

        # Process interfaces
        process_device_interfaces(native_config, normalized_config, dev_id, full_vdom_name)

    # Sort routing table by destination
    normalized_config["routing"].sort(key=get_route_destination, reverse=True)


def get_matching_route(destination_ip: IPAddress, routing_table: list[dict[str, Any]]) -> dict[str, Any] | None:
    def route_matches(ip: IPAddress, destination: str) -> bool:
        ip_n = IPNetwork(ip).cidr
        dest_n = IPNetwork(destination).cidr
        return ip_n in dest_n or dest_n in ip_n

    if len(routing_table) == 0:
        FWOLogger.error("src nat behind interface: encountered empty routing table")
        return None

    for route in routing_table:
        if route_matches(destination_ip, route["destination"]):
            return route

    FWOLogger.warning("src nat behind interface: found no matching route in routing table - no default route?!")
    return None


def get_ip_of_interface(interface: str, interface_list: list[dict[str, Any]] | None = None) -> str | None:
    if interface_list is None:
        interface_list = []
    interface_details = next((sub for sub in interface_list if sub["name"] == interface), None)

    if interface_details is not None and "ipv4" in interface_details:
        return interface_details["ipv4"]
    return None


def sort_reverse(ar_in: list[dict[str, Any]], key: str) -> list[dict[str, Any]]:
    def comp(left: dict[str, Any], right: dict[str, Any]) -> int:
        l_submask = int(left[key].split("/")[1])
        r_submask = int(right[key].split("/")[1])
        return l_submask - r_submask

    return sorted(ar_in, key=cmp_to_key(comp), reverse=True)


# strip off last part of a string separated by separator
def strip_off_last_part(string_in: str, separator: str = "_") -> str:
    string_out = string_in
    if separator in string_in:  # strip off final _xxx part
        str_ar = string_in.split(separator)
        str_ar.pop()
        string_out = separator.join(str_ar)
    return string_out


def get_last_part(string_in: str, separator: str = "_") -> str:
    string_out = ""
    if separator in string_in:  # strip off _vdom_name
        str_ar = string_in.split(separator)
        string_out = str_ar.pop()
    return string_out


def get_plain_device_names_without_vdoms(devices: list[dict[str, Any]]) -> list[str]:
    device_array: list[str] = []
    for dev in devices:
        dev_name = strip_off_last_part(dev["name"])
        if dev_name not in device_array:
            device_array.append(dev_name)
    return device_array


# only getting one vdom as currently assuming routing to be
# the same for all vdoms on a device
def get_device_names_plus_one_vdom(devices: list[dict[str, Any]]) -> list[list[str]]:
    device_array: list[str] = []
    device_array_with_vdom: list[list[str]] = []
    for dev in devices:
        dev_name = strip_off_last_part(dev["name"])
        vdom_name = get_last_part(dev["name"])
        if dev_name not in device_array:
            device_array.append(dev_name)
            device_array_with_vdom.append([dev_name, vdom_name])
    return device_array_with_vdom


# getting devices and their vdom names
def get_device_plus_full_vdom_names(devices: list[dict[str, Any]]) -> list[list[str]]:
    device_array_with_vdom: list[list[str]] = []
    for dev in devices:
        dev_name = strip_off_last_part(dev["name"])
        vdom_name = dev["name"]
        device_array_with_vdom.append([dev_name, vdom_name])
    return device_array_with_vdom


# getting devices and their vdom names
def get_all_dev_names(devices: list[dict[str, Any]]) -> list[list[Any]]:
    device_array_with_vdom: list[list[Any]] = []
    for dev in devices:
        dev_id = dev["id"]
        dev_name = strip_off_last_part(dev["name"])
        plain_vdom_name = get_last_part(dev["name"])
        full_vdom_name = dev["name"]
        device_array_with_vdom.append([dev_id, dev_name, plain_vdom_name, full_vdom_name])
    return device_array_with_vdom


# get network information (currently only used for source nat)
def create_interfaces_payload(plain_vdom_name: str) -> dict[str, Any]:
    """Create payload for fetching interface information."""
    return {
        "id": 1,
        "params": [
            {
                "fields": ["name", "ip"],
                "filter": ["vdom", "==", plain_vdom_name],
                "sub fetch": {
                    "client-options": {SUBFETCH_HIDDEN: 1},
                    "dhcp-snooping-server-list": {SUBFETCH_HIDDEN: 1},
                    "egress-queues": {SUBFETCH_HIDDEN: 1},
                    "ipv6": {
                        "fields": ["ip6-address"],
                        "sub fetch": {
                            "dhcp6-iapd-list": {SUBFETCH_HIDDEN: 1},
                            "ip6-delegated-prefix-list": {SUBFETCH_HIDDEN: 1},
                            "ip6-extra-addr": {SUBFETCH_HIDDEN: 1},
                            "ip6-prefix-list": {SUBFETCH_HIDDEN: 1},
                            "vrrp6": {SUBFETCH_HIDDEN: 1},
                        },
                    },
                    "l2tp-client-settings": {SUBFETCH_HIDDEN: 1},
                    "secondaryip": {SUBFETCH_HIDDEN: 1},
                    "tagging": {SUBFETCH_HIDDEN: 1},
                    "vrrp": {SUBFETCH_HIDDEN: 1},
                    "wifi-networks": {SUBFETCH_HIDDEN: 1},
                },
            }
        ],
    }


def fetch_device_interfaces(
    sid: str,
    fm_api_url: str,
    native_config: dict[str, Any],
    plain_dev_name: str,
    plain_vdom_name: str,
    full_vdom_name: str,
    limit: int,
) -> None:
    """Fetch interface information for a single device."""
    try:
        all_interfaces_payload = create_interfaces_payload(plain_vdom_name)
        # The API call expects a list but we need to work with a dict,
        # so we work around this by using the pattern from the original code
        temp_result: list[dict[str, Any]] = []
        fmgr_getter.update_config_with_fortinet_api_call(
            temp_result,
            sid,
            fm_api_url,
            "/pm/config/device/" + plain_dev_name + "/global/system/interface",
            INTERFACES_PER_DEVICE + full_vdom_name,
            payload=all_interfaces_payload,
            limit=limit,
            method="get",
        )
        # Extract the data from the result and store in native_config
        for item in temp_result:
            if item["type"] == INTERFACES_PER_DEVICE + full_vdom_name:
                native_config[INTERFACES_PER_DEVICE + full_vdom_name] = item["data"]
    except Exception:
        FWOLogger.warning(
            "error while getting interfaces of device "
            + plain_vdom_name
            + ", vdom="
            + plain_vdom_name
            + ", ignoring, traceback: "
            + str(traceback.format_exc())
        )


def fetch_routing_table(
    sid: str,
    fm_api_url: str,
    native_config: dict[str, Any],
    adom_name: str,
    plain_dev_name: str,
    plain_vdom_name: str,
    full_vdom_name: str,
    ip_version: str,
    limit: int,
) -> None:
    """Fetch routing table for a specific IP version and device."""
    payload: dict[str, Any] = {
        "params": [
            {
                "data": {
                    "target": ["adom/" + adom_name + "/device/" + plain_dev_name],
                    "action": "get",
                    "resource": "/api/v2/monitor/router/" + ip_version + "/select?&vdom=" + plain_vdom_name,
                }
            }
        ]
    }

    try:
        routing_helper: list[dict[str, Any]] = []
        routing_table: list[Any] = []
        fmgr_getter.update_config_with_fortinet_api_call(
            routing_helper,
            sid,
            fm_api_url,
            "/sys/proxy/json",
            "routing-table-" + ip_version + "/" + full_vdom_name,
            payload=payload,
            limit=limit,
            method="exec",
        )

        # Extract routing table data from the result
        routing_key = "routing-table-" + ip_version + "/" + full_vdom_name
        for item in routing_helper:
            if item["type"] == routing_key:
                routing_data = item["data"]
                if len(routing_data) > 0 and "response" in routing_data[0] and "results" in routing_data[0]["response"]:
                    routing_table = routing_data[0]["response"]["results"]
                else:
                    FWOLogger.warning(
                        "got empty " + ip_version + " routing table from device " + full_vdom_name + ", ignoring"
                    )
                    routing_table = []
                break
    except Exception:
        FWOLogger.warning("could not get routing table for device " + full_vdom_name + ", ignoring")
        routing_table = []

    # Store the routing table
    native_config.update({"routing-table-" + ip_version + "/" + full_vdom_name: routing_table})


def get_interfaces_and_routing(
    sid: str, fm_api_url: str, native_config: dict[str, Any], adom_name: str, devices: list[dict[str, Any]], limit: int
) -> None:
    """Get network information (interfaces and routing) for all devices."""
    device_array = get_all_dev_names(devices)

    for _, plain_dev_name, plain_vdom_name, full_vdom_name in device_array:
        FWOLogger.info("dev_name: " + plain_dev_name + ", full vdom_name: " + full_vdom_name)

        # Fetch device interfaces
        fetch_device_interfaces(sid, fm_api_url, native_config, plain_dev_name, plain_vdom_name, full_vdom_name, limit)

        # Fetch routing information for both IP versions
        for ip_version in ["ipv4", "ipv6"]:
            fetch_routing_table(
                sid,
                fm_api_url,
                native_config,
                adom_name,
                plain_dev_name,
                plain_vdom_name,
                full_vdom_name,
                ip_version,
                limit,
            )


def get_device_from_package(package_name: str, mgm_details: dict[str, Any]) -> str | None:
    for dev in mgm_details["devices"]:
        if dev["local_rulebase_name"] == package_name:
            return dev["id"]
    FWOLogger.debug('get_device_from_package - could not find device for package "' + package_name + '"')
    return None

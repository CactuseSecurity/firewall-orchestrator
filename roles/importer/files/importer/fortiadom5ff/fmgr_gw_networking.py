from typing import Any
from fwo_log import get_fwo_logger
from netaddr import IPAddress, IPNetwork
from functools import cmp_to_key
import traceback
import fmgr_getter
import fwo_globals
from model_controllers.interface_controller import Interface
from model_controllers.route_controller import Route, get_route_destination

def normalize_network_data(native_config: dict[str, Any], normalized_config: dict[str, Any], mgm_details: dict[str, Any]) -> None:

    # currently only a single IP (v4+v6) per interface ;-)
    #
    # route: {
    #     "ip_mask": "1.2.3.0/24",
    #     "gateway": "2.3.4.5",
    #     "metric": 20,
    #     "distance": 10,
    #     "type": "static",
    #     "interface": "eth12",
    #     "comment": "blah"
    # }
    #
    # interface: {
    #     "name": "eth12",
    #     "ipv4": "2.3.4.5",
    #     "maskv4": 24,
    #     "ipv6": "2::1",
    #     "maskv6": 48
    # }

    logger = get_fwo_logger()

    normalized_config.update({'routing': {}, 'interfaces': {} })

    for dev_id, _, _, full_vdom_name in get_all_dev_names(mgm_details['devices']):
        normalized_config.update({'routing': [], 'interfaces': []})

        if 'routing-table-ipv4/' + full_vdom_name not in native_config:
            logger.warning('could not find routing data routing-table-ipv4/' + full_vdom_name)
            logger.warning('native configs contains the following keys ' + str(native_config.keys()))
            normalized_config['networking'][full_vdom_name]['routingv4'] = []
        else:
            for route in native_config['routing-table-ipv4/' + full_vdom_name]:
                #gateway = None if route['gateway']=='0.0.0.0' else route['gateway'] # local network
                normRoute = Route(dev_id, route['gateway'], route['ip_mask'], interface=route['interface'], metric=route['metric'], distance=route['distance'])
                normalized_config['routing'].append(normRoute)

        if 'routing-table-ipv6/' + full_vdom_name not in native_config:
            logger.warning('could not find routing data routing-table-ipv6/' + full_vdom_name)
            if fwo_globals.debug_level>5:
                logger.warning('native configs contains the following keys ' + str(native_config.keys()))
            normalized_config['networking'][full_vdom_name]['routingv6'] = []
        else:
            for route in native_config['routing-table-ipv6/' + full_vdom_name]:
                #gateway = None if route['gateway']=='::' else route['gateway'] # local network
                normRoute = Route(dev_id, route['gateway'], route['ip_mask'], metric=route['metric'], 
                    distance=route['distance'], interface=route['interface'], ip_version=6)
                normalized_config['routing'].append(normRoute)

        normalized_config['routing'].sort(key=get_route_destination,reverse=True)
        
        for interface in native_config['interfaces_per_device/' + full_vdom_name]:
            if 'ipv6' in interface and 'ip6-address' in interface['ipv6'] and interface['ipv6']['ip6-address']!='::/0':
                ipv6, netmask_bits = interface['ipv6']['ip6-address'].split('/')
                normIfV6 = Interface(dev_id, interface['name'], IPAddress(ipv6), netmask_bits, ip_version=6)
                normalized_config['interfaces'].append(normIfV6)

            if 'ip' in interface and interface['ip']!=['0.0.0.0','0.0.0.0']:
                ipv4 = IPAddress(interface['ip'][0])
                netmask_bits = IPAddress(interface['ip'][1]).netmask_bits()
                normIfV4 = Interface(dev_id, interface['name'], ipv4, netmask_bits, ip_version=4)
                normalized_config['interfaces'].append(normIfV4)

    #devices_without_default_route = get_devices_without_default_route(normalized_config)
    #if len(devices_without_default_route)>0:
    #    logger.warning('found devices without default route')


def get_matching_route(destination_ip: IPAddress, routing_table: list[dict[str, Any]]) -> dict[str, Any] | None:

    logger = get_fwo_logger()

    def route_matches(ip: IPAddress, destination: str) -> bool:
        ip_n = IPNetwork(ip).cidr
        dest_n = IPNetwork(destination).cidr
        return ip_n in dest_n or dest_n in ip_n


    if len(routing_table)==0:
        logger.error('src nat behind interface: encountered empty routing table')
        return None

    for route in routing_table:
        if route_matches(destination_ip, route['destination']):
            return route 

    logger.warning('src nat behind interface: found no matching route in routing table - no default route?!')
    return None


def get_ip_of_interface(interface: str, interface_list: list[dict[str, Any]] = []) -> str | None:

    interface_details = next((sub for sub in interface_list if sub['name'] == interface), None)

    if interface_details is not None and 'ipv4' in interface_details:
        return interface_details['ipv4']
    else:
        return None


def sort_reverse(ar_in: list[dict[str, Any]], key: str) -> list[dict[str, Any]]:

    def comp(left: dict[str, Any], right: dict[str, Any]) -> int:
        l_submask = int(left[key].split("/")[1])
        r_submask = int(right[key].split("/")[1])
        return l_submask - r_submask

    return sorted(ar_in, key=cmp_to_key(comp), reverse=True)


# strip off last part of a string separated by separator
def strip_off_last_part(string_in: str, separator: str = '_') -> str:
    string_out = string_in
    if separator in string_in:  # strip off final _xxx part
        str_ar = string_in.split(separator)
        str_ar.pop()
        string_out = separator.join(str_ar)
    return string_out


def get_last_part(string_in: str, separator: str = '_') -> str:
    string_out = ''
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
def getInterfacesAndRouting(sid: str, fm_api_url: str, nativeConfig: list[dict[str, Any]], adom_name: str, devices: list[dict[str, Any]], limit: int) -> None:
                                                        #TYPING: DICT OR LIST??? 
    logger = get_fwo_logger()
    # strip off vdom names, just deal with the plain device
    device_array = get_all_dev_names(devices)

    for _, plain_dev_name, plain_vdom_name, full_vdom_name in device_array:
        logger.info("dev_name: " + plain_dev_name + ", full vdom_name: " + full_vdom_name)

        # getting interfaces of device
        all_interfaces_payload: dict[str, Any] = {
            "id": 1,
            "params": [
                {
                    "fields": [ "name", "ip" ],
                    "filter": [ "vdom", "==", plain_vdom_name ],                    
                    "sub fetch": {
                        "client-options": {
                            "subfetch hidden": 1
                        },
                        "dhcp-snooping-server-list": {
                            "subfetch hidden": 1
                        },
                        "egress-queues": {
                            "subfetch hidden": 1
                        },
                        "ipv6": {
                            "fields": [
                                "ip6-address"
                            ],
                            "sub fetch": {
                                "dhcp6-iapd-list": {
                                    "subfetch hidden": 1
                                },
                                "ip6-delegated-prefix-list": {
                                    "subfetch hidden": 1
                                },
                                "ip6-extra-addr": {
                                    "subfetch hidden": 1
                                },
                                "ip6-prefix-list": {
                                    "subfetch hidden": 1
                                },
                                "vrrp6": {
                                    "subfetch hidden": 1
                                }
                            }
                        },
                        "l2tp-client-settings": {
                            "subfetch hidden": 1
                        },
                        "secondaryip": {
                            "subfetch hidden": 1
                        },
                        "tagging": {
                            "subfetch hidden": 1
                        },
                        "vrrp": {
                            "subfetch hidden": 1
                        },
                        "wifi-networks": {
                            "subfetch hidden": 1
                        }
                    }
                }
            ]
        }
        # get_interfaces_payload = {
        #     "id": 1,
        #     "params": [
        #         {
        #             "fields": [ "name", "ip" ],
        #             "filter": [ "vdom", "==", plain_vdom_name ],
        #             "option": [ "no loadsub" ],
        #         }
        #     ]
        # }
        try:    # get interfaces from top level device (not vdom)
            fmgr_getter.update_config_with_fortinet_api_call(
                nativeConfig, sid, fm_api_url, "/pm/config/device/" + plain_dev_name + "/global/system/interface",
                "interfaces_per_device/" + full_vdom_name, payload=all_interfaces_payload, limit=limit, method="get")
        except Exception:
            logger.warning("error while getting interfaces of device " + plain_vdom_name + ", vdom=" + plain_vdom_name + ", ignoring, traceback: " + str(traceback.format_exc()))

        # now getting routing information
        for ip_version in ["ipv4", "ipv6"]:
            payload: dict[str, Any] = { "params": [ { "data": {
                            "target": ["adom/" + adom_name + "/device/" + plain_dev_name],
                            "action": "get",
                            "resource": "/api/v2/monitor/router/" + ip_version + "/select?&vdom="+ plain_vdom_name } } ] }
            try:    # get routing table per vdom
                routing_helper: list[Any] = []
                routing_table: list[Any] = []
                fmgr_getter.update_config_with_fortinet_api_call(
                    routing_helper, sid, fm_api_url, "/sys/proxy/json",
                    "routing-table-" + ip_version + '/' + full_vdom_name,
                    payload=payload, limit=limit, method="exec")
                
                if "routing-table-" + ip_version + '/' + full_vdom_name in routing_helper:
                    routing_helper = routing_helper["routing-table-" + ip_version + '/' + full_vdom_name]
                    if len(routing_helper)>0 and 'response' in routing_helper[0] and 'results' in routing_helper[0]['response']:
                        routing_table = routing_helper[0]['response']['results']
                    else:
                        logger.warning("got empty " + ip_version + " routing table from device " + full_vdom_name + ", ignoring")
                        routing_table = []
            except Exception:
                logger.warning("could not get routing table for device " + full_vdom_name + ", ignoring") # exception " + str(traceback.format_exc()))
                routing_table = []

            # now storing the routing table:
            nativeConfig.update({"routing-table-" + ip_version + '/' + full_vdom_name: routing_table}) #type: ignore #TYPING: dict or list??? broo


def get_device_from_package(package_name: str, mgm_details: dict[str, Any]) -> str | None:
    logger = get_fwo_logger()
    for dev in mgm_details['devices']:
        if dev['local_rulebase_name'] == package_name:
            return dev['id']
    logger.debug('get_device_from_package - could not find device for package "' + package_name +  '"')
    return None

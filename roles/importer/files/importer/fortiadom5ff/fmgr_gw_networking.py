import logging
import fmgr_getter
from netaddr import IPAddress, IPNetwork
import netaddr
# import ipaddress
from functools import cmp_to_key
import traceback
import json

def normalize_network_data(native_config, normalized_config, mgm_details):

    # currently only a single IP (v4+v6) per interface ;-)

    # normalized_config {
    #   "networking": {
    #       "dev1": {
    #           "routingv4": [ route ],
    #           "routingv6": [ route ],
    #           "interfaces": [ interface ]
    #       }
    #    }
    # }
    #
    # route: {
    #     "destination": "1.2.3.0/24",
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

    normalized_config.update({'networking': {}})
    for dev in get_plain_device_names_without_vdoms(mgm_details['devices']):
        normalized_config['networking'].update({dev: {
            'routingv4': [],
            'routingv6': [],
            'interfaces': [],
        }})
        for route in native_config['routing-table-ipv4/' + dev]:
            gateway = None if route['gateway']=='0.0.0.0' else route['gateway'] # local network
            normalized_config['networking'][dev]['routingv4'].append({
                'destination': route['ip_mask'],
                'interface': route['interface'],
                'gateway': gateway,
                'metric': route['metric'],
                'distance': route['distance'],
                'type': route['type']
            })
        normalized_config['networking'][dev]['routingv4'] = sort_reverse(normalized_config['networking'][dev]['routingv4'], 'destination')

        if not test_if_default_route_exists(normalized_config['networking'][dev]['routingv4']):
            logging.error('found no default route in ipv4 table of device ' + dev )

        for route in native_config['routing-table-ipv6/' + dev]:
            gateway = None if route['gateway']=='::' else route['gateway'] # local network
            normalized_config['networking'][dev]['routingv6'].append({
                'destination': route['ip_mask'],
                'interface': route['interface'],
                'gateway': gateway,
                'metric': route['metric'],
                'distance': route['distance'],
                'type': route['type']
            })
        normalized_config['networking'][dev]['routingv6'] = sort_reverse(normalized_config['networking'][dev]['routingv6'], 'destination')

        if test_if_default_route_exists(normalized_config['networking'][dev]['routingv6']):
            logging.error('found no default route in ipv6 table of device ' + dev )

        for interface in native_config['interfaces_per_device/' + dev]:
            ipv6, ipv6mask = interface['ipv6']['ip6-address'].split('/')
            if ipv6=='::' and ipv6mask=='0':
                ipv6=None
                ipv6mask = None
            if interface['ip'][0]=='0.0.0.0' and interface['ip'][1]=='0.0.0.0':
                ipv4 = None
                ipv4mask = None
            else:
                ipv4 = interface['ip'][0]
                ipv4mask = IPAddress(interface['ip'][1]).netmask_bits()
            normalized_config['networking'][dev]['interfaces'].append({
                'name': interface['name'],
                'ipv4': ipv4,
                'maskv4': ipv4mask,
                'ipv6': ipv6,
                'maskv6': ipv6mask
            })


def get_matching_route(destination_ip, routing_table):


    def route_matches(ip, destination):
        if (ip == destination):
            logging.info('found an exact routing match')
        ip_n = IPNetwork(ip).cidr
        dest_n = IPNetwork(destination).cidr
        return ip_n in dest_n or dest_n in ip_n
        # if IPNetwork(ip) in IPNetwork(destination):
        #     return True
        # else:
        #     return False

#        return ipaddress.ip_address(ip) in ipaddress.ip_network(destination)


    if len(routing_table)==0:
        logging.error('src nat behind interface: encountered empty routing table')
        return None

    for route in routing_table:
        if route_matches(destination_ip, route['destination']):
            return route 

    logging.error('src nat behind interface: found no matching route in routing table - no default route?!')
    return None


def get_ip_of_interface(interface, interface_list=[]):
    if interface in interface_list:
        return interface_list['ipv4']
    else:
        return None


def sort_reverse(ar_in, key):

    def comp(left, right):
        l_submask = int(left[key].split("/")[1])
        r_submask = int(right[key].split("/")[1])
        return l_submask - r_submask

    return sorted(ar_in, key=cmp_to_key(comp), reverse=True)


# strip off vdom name, just deal with the plain device
def strip_off_last_part(string_in, separator='_'):
    string_out = string_in
    if separator in string_in:  # strip off final _xxx part
        str_ar = string_in.split(separator)
        str_ar.pop()
        string_out = separator.join(str_ar)
    return string_out


def get_last_part(string_in, separator='_'):
    string_out = ''
    if separator in string_in:  # strip off _vdom_name
        str_ar = string_in.split(separator)
        string_out = str_ar.pop()
    return string_out


def get_plain_device_names_without_vdoms(devices):
    device_array = []
    for dev in devices:
        dev_name = strip_off_last_part(dev["name"])
        if dev_name not in device_array:
            device_array.append(dev_name)
    return device_array


# only getting one vdom as currntly assuming routing to be
# the same for all vdoms on a device
def get_device_names_plus_one_vdom(devices):
    device_array = []
    device_array_with_vdom = []
    for dev in devices:
        dev_name = strip_off_last_part(dev["name"])
        vdom_name = get_last_part(dev["name"])
        if dev_name not in device_array:
            device_array.append(dev_name)
            device_array_with_vdom.append([dev_name, vdom_name])
    return device_array_with_vdom


# get network information (currently only used for source nat)
def getInterfacesAndRouting(sid, fm_api_url, raw_config, adom_name, devices, limit, debug_level):

    # strip off vdom names, just deal with the plain device
    device_array = get_device_names_plus_one_vdom(devices)

    for dev_name, vdom_name in device_array:
        logging.info("dev_name: " + dev_name + ", vdom_name: " + vdom_name)
        payload = {
            "id": 1,
            "params": [
                {
                    "fields": [
                        "name",
                        "ip"
                    ],
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

        try:    # get interfaces
            fmgr_getter.update_config_with_fortinet_api_call(
                raw_config, sid, fm_api_url, "/pm/config/device/" + dev_name + "/global/system/interface",
                "interfaces_per_device/" + dev_name, payload=payload, debug=debug_level, limit=limit, method="get")
        except:
            logging.warning("import_management - error while getting interfaces of device " + dev_name + ", ignoring, traceback: " + str(traceback.format_exc()))

        for ip_version in ["ipv4", "ipv6"]:
            payload = { "params": [ { "data": {
                            "target": ["adom/" + adom_name + "/device/" + dev_name],
                            "action": "get",
                            "resource": "/api/v2/monitor/router/" + ip_version + "/select?" + vdom_name } } ] }
            try:    # get routing table
                routing_helper = {}
                fmgr_getter.update_config_with_fortinet_api_call(
                    routing_helper, sid, fm_api_url, "/sys/proxy/json",
                    "routing-table-" + ip_version + '/' + dev_name,
                    payload=payload, debug=debug_level, limit=limit, method="exec")
                if len(routing_helper)>0 and 'response' in routing_helper[0] and 'results' in routing_helper[0]['response']:
                    raw_config.update({ "routing-table-" + ip_version + '/' + dev_name: routing_helper[0]['response']['results']})
                else:
                    logging.warning("import_management - error while getting routing table of device " + dev_name + ", ignoring")
                    logging.info("got the following response: " + json.dumps(routing_helper))
                    raw_config.update({"routing-table-" + ip_version + '/' + dev_name: []})
            except:
                logging.warning(
                    "import_management - error while getting routing table of device " + dev_name + ", ignoring")


def get_device_from_package(package_name, mgm_details):
    
    for dev in mgm_details['devices']:
        if dev['local_rulebase_name'] == package_name:
            return strip_off_last_part(dev['name'])

    logging.debug('get_device_from_package - could not find device for package "' + package_name +  '"')
    return None


def test_if_default_route_exists(routing_table):
    # for r in routing_table:
    #     logging.info(r['destination'])
    default_route_v4 = list(filter(lambda default_route: default_route['destination'] == '0.0.0.0/0', routing_table))
    default_route_v6 =  list(filter(lambda default_route: default_route['destination'] == '::/0', routing_table))
    if default_route_v4 == [] and default_route_v6 == []:
        return False
    else:
        return True

from typing import Any
from fwo_log import getFwoLogger
from netaddr import IPAddress


class Interface:
    def __init__(self, device_id: int, name: str, ip: IPAddress, netmask_bits: int, state_up: bool = True, ip_version: int = 4):
        self.routing_device = int(device_id)
        # check if routing device id exists?
        self.name = str(name)
        self.ip =  IPAddress(ip)
        netmask_bits = int(netmask_bits)
        if netmask_bits<0 or netmask_bits>128:
            logger = getFwoLogger()
            logger.error('interface ' + self.name + ' with invalid bitmask: ' + str(netmask_bits))
        else:
            self.netmask_bits = netmask_bits
        self.state_up = bool(state_up)
        ip_version = int(ip_version)
        if ip_version != 4 and ip_version != 6:
            logger = getFwoLogger()
            logger.error('interface ' + self.name + ' with invalid ip protocal: ' + str(ip_version))
        else:
            self.ip_version = ip_version

        self.ip_version = ip_version


class InterfaceSerializable(Interface):
    name : str
    routing_device : int
    ip : str
    netmask_bits : int
    state_up : bool
    ip_version : int
    #TYPING: check if these types are correct
    def __init__(self, ifaceIn: dict[Any, Any] | Interface):
        if type(ifaceIn) is dict:
            self.name = ifaceIn['name']
            self.routing_device = ifaceIn['routing_device']
            self.ip = str(ifaceIn['ip'])
            self.netmask_bits = ifaceIn['netmask_bits']
            self.state_up = ifaceIn['state_up']
            self.ip_version = ifaceIn['ip_version']
        elif isinstance(ifaceIn, Interface):
            self.name = ifaceIn.name
            self.routing_device = ifaceIn.routing_device
            self.ip = str(ifaceIn.ip)
            self.netmask_bits = ifaceIn.netmask_bits
            self.state_up = ifaceIn.state_up
            self.ip_version = ifaceIn.ip_version



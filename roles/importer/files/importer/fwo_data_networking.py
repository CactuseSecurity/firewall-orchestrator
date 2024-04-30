from fwo_log import getFwoLogger
from netaddr import IPAddress, IPNetwork


class Interface:
    def __init__(self, device_id, name, ip, netmask_bits, state_up=True, ip_version=4):
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
    def __init__(self, ifaceIn):
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


class Route:
    def __init__(self, device_id, target_gateway, destination, 
            static=True, source=None, interface=None, metric=None, distance=None, ip_version=4):
        self.routing_device = int(device_id)
        if interface is not None:
            self.interface = str(interface)
        else:
            self.interface = None
        self.target_gateway = IPAddress(target_gateway)
        self.destination = IPNetwork(destination)
        if source is not None:
            self.source = IPNetwork(source)
        else:
            self.source = None
        self.static = bool(static)
        if metric is not None:
            self.metric = int(metric)
        if distance is not None:
            self.distance = int(distance)
        ip_version = int(ip_version)
        if ip_version != 4 and ip_version != 6:
            logger = getFwoLogger()
            logger.error('found route for destination ' + str(self.destination) + ' with invalid ip protocal: ' + str(ip_version))
        else:
            self.ip_version = ip_version


    def isDefaultRoute(self):
        return self.isDefaultRouteV4() or self.isDefaultRouteV6()


    def isDefaultRouteV4(self):
        return self.ip_version == 4 and self.destination == IPNetwork('0.0.0.0/0')


    def isDefaultRouteV6(self):
        return self.ip_version==6 and self.destination == IPNetwork('::/0')


    def routeMatches(self, destination, dev_id):
        ip_n = IPNetwork(self.destination).cidr
        dest_n = IPNetwork(destination).cidr
        return dev_id == self.routing_device and (ip_n in dest_n or dest_n in ip_n)


class RouteSerializable(Route):
    def __init__(self, routeIn):
        if type(routeIn) is dict:
            self.routing_device = routeIn['routing_device']
            self.interface = routeIn['interface']
            self.target_gateway = str(routeIn['target_gateway'])
            self.destination = str(routeIn['destination'])
            if routeIn['source'] is None:
                self.source = None
            else:
                self.source = str(routeIn['source'])
            self.static = routeIn['static']
            self.metric = routeIn['metric']
            self.distance = routeIn['distance']
            self.ip_version = routeIn['ip_version']
        elif isinstance(routeIn, Route):
            self.routing_device = routeIn.routing_device
            self.interface = routeIn.interface
            self.target_gateway = str(routeIn.target_gateway)
            self.destination = str(routeIn.destination)
            if routeIn.source is None:
                self.source = None
            else:
                self.source = str(routeIn.source)
            self.static = routeIn.static
            self.metric = routeIn.metric
            self.distance = routeIn.distance
            self.ip_version = routeIn.ip_version


def getRouteDestination(obj):
    return obj.destination


# def test_if_default_route_exists_obj(routing_table):
#     default_route_v4 = list(filter(lambda default_route: default_route.destination == IPNetwork('0.0.0.0/0'), routing_table))
#     default_route_v6 =  list(filter(lambda default_route: default_route.destination == IPNetwork('::/0'), routing_table))
#     if default_route_v4 == [] and default_route_v6 == []:
#         return False
#     else:
#         return True


# def get_devices_without_default_route(routing_table):
#     dev_ids = vars(routing_table)
#     default_route_v4 = list(filter(lambda default_route: default_route.destination == IPNetwork('0.0.0.0/0'), routing_table))
#     default_route_v6 =  list(filter(lambda default_route: default_route.destination == IPNetwork('::/0'), routing_table))
#     return default_route_v4.append(default_route_v6)


def get_matching_route_obj(destination_ip, routing_table, dev_id):

    logger = getFwoLogger()

    if len(routing_table)==0:
        logger.error('found empty routing table for device id ' + str(dev_id))
        return None

    # assuiming routing table to be in sorted state already
    for route in routing_table:
        if route.routeMatches(destination_ip, dev_id):
            return route

    logger.warning('src nat behind interface: found no matching route in routing table - no default route?!')
    return None


def get_ip_of_interface_obj(interface_name, dev_id, interface_list=[]):
    interface_details = next((sub for sub in interface_list if sub.name == interface_name and sub.routing_device==dev_id), None)

    if interface_details is not None:
        return interface_details.ip
    else:
        return None

from typing import Any

from fwo_log import FWOLogger
from model_controllers.interface_controller import InterfaceSerializable
from netaddr import IPAddress, IPNetwork


class Route:
    def __init__(
        self,
        device_id: int,
        target_gateway: str,
        destination: str,
        static: bool = True,
        source: str | None = None,
        interface: str | None = None,
        metric: int | None = None,
        distance: int | None = None,
        ip_version: int = 4,
    ):
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
        if ip_version not in {4, 6}:
            FWOLogger.error(
                "found route for destination " + str(self.destination) + " with invalid ip protocal: " + str(ip_version)
            )
        else:
            self.ip_version = ip_version

    def is_default_route(self):
        return self.is_default_route_v4() or self.is_default_route_v6()

    def is_default_route_v4(self):
        return self.ip_version == 4 and self.destination == IPNetwork("0.0.0.0/0")

    def is_default_route_v6(self):
        return self.ip_version == 6 and self.destination == IPNetwork("::/0")

    def route_matches(self, destination: str, dev_id: int) -> bool:
        ip_n = IPNetwork(self.destination).cidr
        dest_n = IPNetwork(destination).cidr
        return dev_id == self.routing_device and (ip_n in dest_n or dest_n in ip_n)

    def get_route_destination(self):
        return self.destination


class RouteSerializable(Route):
    def __init__(self, route_in: dict[str, Any] | Route):
        if type(route_in) is dict:
            self.routing_device = route_in["routing_device"]
            self.interface = route_in["interface"]
            self.target_gateway = str(route_in["target_gateway"])
            self.destination = str(route_in["destination"])
            if route_in["source"] is None:
                self.source = None
            else:
                self.source = str(route_in["source"])
            self.static = route_in["static"]
            self.metric = route_in["metric"]
            self.distance = route_in["distance"]
            self.ip_version = route_in["ip_version"]
        elif isinstance(route_in, Route):
            self.routing_device = route_in.routing_device
            self.interface = route_in.interface
            self.target_gateway = str(route_in.target_gateway)
            self.destination = str(route_in.destination)
            if route_in.source is None:
                self.source = None
            else:
                self.source = str(route_in.source)
            self.static = route_in.static
            self.metric = route_in.metric
            self.distance = route_in.distance
            self.ip_version = route_in.ip_version


def get_route_destination(obj: Route):
    return obj.destination


def get_matching_route_obj(destination_ip: str, routing_table: list[Route], dev_id: int) -> Route | None:
    if len(routing_table) == 0:
        FWOLogger.error("found empty routing table for device id " + str(dev_id))
        return None

    # assuiming routing table to be in sorted state already
    for route in routing_table:
        if route.route_matches(destination_ip, dev_id):
            return route

    FWOLogger.warning("src nat behind interface: found no matching route in routing table - no default route?!")
    return None


def get_ip_of_interface_obj(
    interface_name: str | None, dev_id: int, interface_list: list[InterfaceSerializable]
) -> str | None:
    interface_details = next(
        (sub for sub in interface_list if sub.name == interface_name and sub.routing_device == dev_id), None
    )

    if interface_details is not None:
        return interface_details.ip
    return None

import json

from fwo_base import ConfFormat, ConfigAction
from models.rulebase import Rulebase
from netaddr import IPNetwork


class FwConfigJsonEncoder(json.JSONEncoder):
    def default(self, o: object) -> object:
        if isinstance(o, (ConfigAction, ConfFormat)):
            return o.name

        if isinstance(o, Rulebase):
            return o.to_json()

        if isinstance(o, IPNetwork):
            return str(o)

        return json.JSONEncoder.default(self, o)


"""
    the configuraton of a firewall management to import
    could be normalized or native config
    management could be standard of super manager (MDS, fortimanager)
"""

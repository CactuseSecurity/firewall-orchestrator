import json
from fwo_base import ConfFormat, ConfigAction
from models.rulebase import Rulebase
from netaddr import IPNetwork

class FwoEncoder(json.JSONEncoder):

    def default(self, obj: object) -> object: # type: ignore

        if isinstance(obj, ConfigAction) or isinstance(obj, ConfFormat):
            return obj.name
        
        if isinstance(obj, Rulebase):
            return obj.toJson() # type: ignore

        if isinstance(obj, IPNetwork):
            return str(obj)

        return json.JSONEncoder.default(self, obj)

"""
    the configuraton of a firewall management to import
    could be normalized or native config
    management could be standard of super manager (MDS, fortimanager)
"""

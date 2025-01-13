import json
from fwo_base import ConfFormat, ConfigAction
from roles.importer.files.importer.models.rulebase import Rulebase
from models.fwconfig import FwConfig
from netaddr import IPNetwork

class FwoEncoder(json.JSONEncoder):

    def default(self, obj):

        if isinstance(obj, ConfigAction) or isinstance(obj, ConfFormat):
            return obj.name
        
        if isinstance(obj, Rulebase):
            return obj.toJson()

        if isinstance(obj, IPNetwork):
            return str(obj)

        return json.JSONEncoder.default(self, obj)

"""
    the configuraton of a firewall management to import
    could be normalized or native config
    management could be standard of super manager (MDS, fortimanager)
"""

class FwConfigController(FwConfig):

    def IsLegacy(self):
        return self.ConfigFormat in [ConfFormat.NORMALIZED_LEGACY, ConfFormat.CHECKPOINT_LEGACY, 
                                    ConfFormat.CISCOFIREPOWER_LEGACY, ConfFormat.FORTINET_LEGACY, 
                                    ConfFormat.PALOALTO_LEGACY]

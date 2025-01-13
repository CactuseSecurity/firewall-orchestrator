import json
from pydantic import BaseModel
from fwo_base import ConfFormat, ConfigAction
from roles.importer.files.importer.models.rulebase import Rulebase

# class FwoEncoder(json.JSONEncoder):

#     def default(self, obj):

#         if isinstance(obj, ConfigAction) or isinstance(obj, ConfFormat):
#             return obj.name
        
#         if isinstance(obj, Policy):
#             return obj.toJson()
        
#         return json.JSONEncoder.default(self, obj)

"""
    the configuraton of a firewall management to import
    could be normalized or native config
    management could be standard of super manager (MDS, fortimanager)
"""
class FwConfig(BaseModel):
    ConfigFormat: ConfFormat
    FwConf: dict

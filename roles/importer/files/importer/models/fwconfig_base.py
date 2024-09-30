import json

from fwo_base import ConfFormat, ConfigAction
from models.policy import Policy

class FwoEncoder(json.JSONEncoder):

    def default(self, obj):

        if isinstance(obj, ConfigAction) or isinstance(obj, ConfFormat):
            return obj.name
        
        if isinstance(obj, Policy):
            return obj.toJson()
        
        return json.JSONEncoder.default(self, obj)


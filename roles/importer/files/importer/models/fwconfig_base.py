import json

from fwo_base import ConfFormat, ConfigAction
from models.rulebase import Rulebase

class FwoEncoder(json.JSONEncoder):

    
    def default(self, o: object) -> object:

        if isinstance(o, ConfigAction) or isinstance(o, ConfFormat):
            return o.name
        
        if isinstance(o, Rulebase):
            return o.to_json()
        
        return json.JSONEncoder.default(self, o)


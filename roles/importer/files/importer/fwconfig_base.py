import json
from fwo_enums import ConfFormat, ConfigAction


class FwoEncoder(json.JSONEncoder):

    def default(self, obj):

        if isinstance(obj, ConfigAction) or isinstance(obj, ConfFormat):
            return obj.name
        
        return json.JSONEncoder.default(self, obj)


def replaceNoneWithEmpty(s):
    if s is None or s == '':
        return '<EMPTY>'
    else:
        return str(s)
    
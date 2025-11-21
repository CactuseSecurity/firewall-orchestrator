import json
from fwo_enums import ConfFormat, ConfigAction


class FwoEncoder(json.JSONEncoder):

    def default(self, obj: object) -> object: # type: ignore

        if isinstance(obj, ConfigAction) or isinstance(obj, ConfFormat):
            return obj.name
        
        return json.JSONEncoder.default(self, obj)


def replace_none_with_empty(s: str | None) -> str:
    if s is None or s == '':
        return '<EMPTY>'
    else:
        return str(s)
    
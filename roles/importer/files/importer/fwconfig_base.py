import json

from fwo_enums import ConfFormat, ConfigAction


class FwoEncoder(json.JSONEncoder):
    def default(self, o: object) -> object:
        if isinstance(o, (ConfigAction, ConfFormat)):
            return o.name

        return json.JSONEncoder.default(self, o)


def replace_none_with_empty(s: str | None) -> str:  # TYPING: make a utils file and move there
    if s is None or s == "":
        return "<EMPTY>"
    return str(s)

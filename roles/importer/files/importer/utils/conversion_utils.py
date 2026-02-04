from typing import Any

from fwo_log import FWOLogger


def convert_list_to_dict(list_in: list[Any], id_field: str) -> dict[Any, Any]:
    result: dict[Any, Any] = {}
    for item in list_in:
        if id_field in item:
            key = item[id_field]
            result[key] = item
        else:
            FWOLogger.error(f"dict {item!s} does not contain id field {id_field}")
    return result  # { listIn[idField]: listIn for listIn in listIn }

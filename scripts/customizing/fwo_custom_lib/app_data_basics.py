__version__ = "2025-11-20-01"
# revision history:
# 2025-11-20-01, initial version

import json
import logging
from pathlib import Path
from typing import Any

from scripts.customizing.fwo_custom_lib.app_data_models import Owner


def transform_owner_dict_to_list(app_data: dict[str, Owner]) -> dict[str, list[dict[str, Any]]]:
    owner_data: dict[str, list[dict[str, Any]]] = {"owners": []}
    for owner in app_data.values():
        owner_data["owners"].append(owner.to_json())
    return owner_data


def transform_app_list_to_dict(app_list: list[Owner]) -> dict[str, Owner]:
    app_data_dict: dict[str, Owner] = {}
    app: Owner
    for app in app_list:
        app_data_dict[app.app_id_external] = app
    return app_data_dict


def build_owner_json_path(script_file_path: str) -> str:
    script_path = Path(script_file_path)
    return str(script_path.parent / f"{script_path.stem}.json")


def write_owners_to_json(
    app_dict: dict[str, Owner],
    script_file_path: str,
    file_out: str | None = None,
    logger: logging.Logger | None = None,
) -> str:
    if file_out is None:
        file_out = build_owner_json_path(script_file_path)
    if logger:
        logger.info("dumping into file %s", file_out)
    with open(file_out, "w", encoding="utf-8") as out_fh:
        json.dump(transform_owner_dict_to_list(app_dict), out_fh, indent=3)
    return file_out

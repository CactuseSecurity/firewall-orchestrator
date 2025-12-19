__version__ = "2025-11-20-01"
# revision history:
# 2025-11-20-01, initial version

import json
import os
from pathlib import Path


def transform_owner_dict_to_list(app_data):
    owner_data = {"owners": []}
    for app_id in app_data:
        owner_data['owners'].append(app_data[app_id].to_json())
    return owner_data


def transform_app_list_to_dict(app_list):
    app_data_dict = {}
    for app in app_list:
        app_data_dict[app.app_id_external] = app
    return app_data_dict


def build_owner_json_path(script_file_path):
    base_dir = os.path.dirname(script_file_path)
    file_name = Path(os.path.basename(script_file_path)).stem + ".json"
    return os.path.join(base_dir, file_name)


def write_owners_to_json(app_dict, script_file_path, file_out=None, logger=None):
    if file_out is None:
        file_out = build_owner_json_path(script_file_path)
    if logger:
        logger.info("dumping into file " + file_out)
    with open(file_out, "w") as out_fh:
        json.dump(transform_owner_dict_to_list(app_dict), out_fh, indent=3)
    return file_out

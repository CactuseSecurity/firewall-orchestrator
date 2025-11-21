__version__ = "2025-11-20-01"
# revision history:
# 2025-11-20-01, initial version

import json
import logging
import sys
import traceback


def read_custom_config(config_filename, key_to_get, logger):
    try:
        with open(config_filename, "r") as custom_config_fh:
            custom_config = json.loads(custom_config_fh.read())
        return custom_config[key_to_get]

    except Exception:
        logger.error("could not read key '" + key_to_get + "' from config file " + config_filename + ", Exception: " + str(traceback.format_exc()))
        sys.exit(1)


def get_logger(debug_level_in=0):
    debug_level = int(debug_level_in)
    if debug_level >= 1:
        log_level = logging.DEBUG
    else:
        log_level = logging.INFO

    logger = logging.getLogger('import-fworch-app-data')
    logformat = "%(asctime)s [%(levelname)-5.5s] [%(filename)-10.10s:%(funcName)-10.10s:%(lineno)4d] %(message)s"
    logging.basicConfig(format=logformat, datefmt="%Y-%m-%dT%H:%M:%S%z", level=log_level)
    logger.setLevel(log_level)

    connection_log = logging.getLogger("urllib3.connectionpool")
    connection_log.setLevel(logging.WARNING)
    connection_log.propagate = True

    if debug_level > 8:
        logger.debug("debug_level=" + str(debug_level))
    return logger


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

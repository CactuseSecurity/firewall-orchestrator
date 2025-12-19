__version__ = "2025-11-20-01"
# revision history:
# 2025-11-20-01, initial version


import json
import logging
import sys
import traceback
from typing import Any


def read_custom_config(config_filename: str, key_to_get: str, logger: logging.Logger) -> Any:
    try:
        with open(config_filename, "r", encoding="utf-8") as custom_config_fh:
            custom_config: dict[str, Any] = json.loads(custom_config_fh.read())
        return custom_config[key_to_get]

    except Exception:
        logger.error("could not read key '" + key_to_get + "' from config file " + config_filename + ", Exception: " + str(traceback.format_exc()))
        sys.exit(1)


def read_custom_config_with_default(
    config_filename: str,
    key_to_get: str,
    default_value: Any,
    logger: logging.Logger,
) -> Any:
    try:
        with open(config_filename, "r", encoding="utf-8") as custom_config_fh:
            custom_config: dict[str, Any] = json.loads(custom_config_fh.read())
        return custom_config.get(key_to_get, default_value)

    except Exception:
        logger.error("could not read key '" + key_to_get + "' from config file " + config_filename + ", Exception: " + str(traceback.format_exc()))
        sys.exit(1)


def get_logger(debug_level_in: int = 0) -> logging.Logger:
    debug_level: int = int(debug_level_in)
    if debug_level >= 1:
        log_level = logging.DEBUG
    else:
        log_level = logging.INFO

    logger: logging.Logger = logging.getLogger('import-fworch-app-data')
    logformat = "%(asctime)s [%(levelname)-5.5s] [%(filename)-10.10s:%(funcName)-10.10s:%(lineno)4d] %(message)s"
    logging.basicConfig(format=logformat, datefmt="%Y-%m-%dT%H:%M:%S%z", level=log_level)
    logger.setLevel(log_level)

    connection_log: logging.Logger = logging.getLogger("urllib3.connectionpool")
    connection_log.setLevel(logging.WARNING)
    connection_log.propagate = True

    if debug_level > 8:
        logger.debug("debug_level=" + str(debug_level))
    return logger

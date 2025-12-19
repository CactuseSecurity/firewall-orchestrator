__version__ = "2025-11-20-01"
# revision history:
# 2025-11-20-01, initial version


import json
import logging
import sys
import traceback
from typing import Any


class FWOLogger(logging.Logger):
    def __init__(self, name: str, level: int = logging.NOTSET) -> None:
        super().__init__(name, level)
        self.debug_level: int = 0

    def configure_debug_level(self, debug_level: int) -> None:
        self.debug_level = int(debug_level)
        log_level = logging.DEBUG if self.debug_level >= 1 else logging.INFO
        self.setLevel(log_level)
        logging.getLogger().setLevel(log_level)

    def is_debug_level(self, min_debug: int) -> bool:
        return self.debug_level >= min_debug

    def debug_if(self, min_debug: int, msg: str, *args: Any, **kwargs: Any) -> None:
        if self.is_debug_level(min_debug):
            self.debug(msg, *args, **kwargs)

    def info_if(self, min_debug: int, msg: str, *args: Any, **kwargs: Any) -> None:
        if self.is_debug_level(min_debug):
            self.info(msg, *args, **kwargs)

    def warning_if(self, min_debug: int, msg: str, *args: Any, **kwargs: Any) -> None:
        if self.is_debug_level(min_debug):
            self.warning(msg, *args, **kwargs)


def read_custom_config(config_filename: str, key_to_get: str, logger: FWOLogger) -> Any:
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
    logger: FWOLogger,
) -> Any:
    try:
        with open(config_filename, "r", encoding="utf-8") as custom_config_fh:
            custom_config: dict[str, Any] = json.loads(custom_config_fh.read())
        return custom_config.get(key_to_get, default_value)

    except Exception:
        logger.error("could not read key '" + key_to_get + "' from config file " + config_filename + ", Exception: " + str(traceback.format_exc()))
        sys.exit(1)


def get_logger(debug_level_in: int = 0) -> FWOLogger:
    debug_level: int = int(debug_level_in)
    log_level = logging.DEBUG if debug_level >= 1 else logging.INFO

    logging.setLoggerClass(FWOLogger)
    logger = logging.getLogger('import-fworch-app-data')
    if not isinstance(logger, FWOLogger):
        logger = FWOLogger('import-fworch-app-data')
        logging.Logger.manager.loggerDict['import-fworch-app-data'] = logger
    logformat = "%(asctime)s [%(levelname)-5.5s] [%(filename)-10.10s:%(funcName)-10.10s:%(lineno)4d] %(message)s"
    logging.basicConfig(format=logformat, datefmt="%Y-%m-%dT%H:%M:%S%z", level=log_level)
    logger.configure_debug_level(debug_level)

    connection_log: logging.Logger = logging.getLogger("urllib3.connectionpool")
    connection_log.setLevel(logging.WARNING)
    connection_log.propagate = True

    if debug_level > 8:
        logger.debug("debug_level=" + str(debug_level))
    return logger

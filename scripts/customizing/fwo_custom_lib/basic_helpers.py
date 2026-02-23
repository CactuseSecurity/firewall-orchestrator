__version__ = "2025-11-20-01"
# revision history:
# 2025-11-20-01, initial version


import json
import logging
import sys
from typing import Any

DEBUG_LEVEL_VERBOSE: int = 8


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


def _strip_json_comments(config_content: str) -> str:
    result_chars: list[str] = []
    in_string: bool = False
    escaped: bool = False
    in_line_comment: bool = False
    in_block_comment: bool = False
    i: int = 0

    while i < len(config_content):
        current_char: str = config_content[i]
        next_char: str = config_content[i + 1] if i + 1 < len(config_content) else ""

        if in_line_comment:
            if current_char == "\n":
                in_line_comment = False
                result_chars.append(current_char)
            i += 1
            continue

        if in_block_comment:
            if current_char == "*" and next_char == "/":
                in_block_comment = False
                i += 2
            else:
                i += 1
            continue

        if in_string:
            result_chars.append(current_char)
            if escaped:
                escaped = False
            elif current_char == "\\":
                escaped = True
            elif current_char == '"':
                in_string = False
            i += 1
            continue

        if current_char == '"':
            in_string = True
            result_chars.append(current_char)
            i += 1
            continue
        if current_char == "/" and next_char == "/":
            in_line_comment = True
            i += 2
            continue
        if current_char == "/" and next_char == "*":
            in_block_comment = True
            i += 2
            continue
        if current_char == "#":
            in_line_comment = True
            i += 1
            continue

        result_chars.append(current_char)
        i += 1

    return "".join(result_chars)


def _strip_trailing_commas(config_content: str) -> str:
    result_chars: list[str] = []
    in_string: bool = False
    escaped: bool = False
    i: int = 0

    while i < len(config_content):
        current_char: str = config_content[i]

        if in_string:
            result_chars.append(current_char)
            if escaped:
                escaped = False
            elif current_char == "\\":
                escaped = True
            elif current_char == '"':
                in_string = False
            i += 1
            continue

        if current_char == '"':
            in_string = True
            result_chars.append(current_char)
            i += 1
            continue

        if current_char == ",":
            lookahead_index: int = i + 1
            while lookahead_index < len(config_content) and config_content[lookahead_index].isspace():
                lookahead_index += 1
            if lookahead_index < len(config_content) and config_content[lookahead_index] in ("]", "}"):
                i += 1
                continue

        result_chars.append(current_char)
        i += 1

    return "".join(result_chars)


def _load_custom_config(config_filename: str) -> dict[str, Any]:
    with open(config_filename, encoding="utf-8") as custom_config_fh:
        config_content: str = custom_config_fh.read()
    commentless_content: str = _strip_json_comments(config_content)
    sanitized_content: str = _strip_trailing_commas(commentless_content)
    return json.loads(sanitized_content)


def read_custom_config(config_filename: str, key_to_get: str, logger: logging.Logger) -> Any:
    try:
        custom_config: dict[str, Any] = _load_custom_config(config_filename)
        return custom_config[key_to_get]

    except KeyError:
        logger.warning("could not read key %s from config file %s", key_to_get, config_filename)
    except Exception:
        logger.exception("could not read config file %s", config_filename)
        sys.exit(1)


def read_custom_config_with_default(
    config_filename: str,
    key_to_get: str,
    default_value: Any,
    logger: logging.Logger,
) -> Any:
    try:
        custom_config: dict[str, Any] = _load_custom_config(config_filename)
        return custom_config.get(key_to_get, default_value)

    except Exception:
        logger.exception("could not read key %s from config file %s", key_to_get, config_filename)
        sys.exit(1)


def get_logger(debug_level_in: int = 0) -> FWOLogger:
    debug_level: int = int(debug_level_in)
    log_level = logging.DEBUG if debug_level >= 1 else logging.INFO

    logging.setLoggerClass(FWOLogger)
    logger = logging.getLogger("import-fworch-app-data")
    if not isinstance(logger, FWOLogger):
        logger = FWOLogger("import-fworch-app-data")
        logging.Logger.manager.loggerDict["import-fworch-app-data"] = logger
    logformat = "%(asctime)s [%(levelname)-5.5s] [%(filename)-10.10s:%(funcName)-10.10s:%(lineno)4d] %(message)s"
    logging.basicConfig(format=logformat, datefmt="%Y-%m-%dT%H:%M:%S%z", level=log_level)
    logger.configure_debug_level(debug_level)

    connection_log: logging.Logger = logging.getLogger("urllib3.connectionpool")
    connection_log.setLevel(logging.WARNING)
    connection_log.propagate = True

    if debug_level > DEBUG_LEVEL_VERBOSE:
        logger.debug("debug_level=%s", debug_level)
    return logger

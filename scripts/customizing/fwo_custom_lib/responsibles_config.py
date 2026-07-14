import argparse
import logging
import shlex
from typing import Any, cast

from scripts.customizing.fwo_custom_lib.basic_helpers import read_custom_config_with_default


def _parse_level_mapping(entry_value: str) -> tuple[str, str] | None:
    split_entry: list[str] = entry_value.split(":", 1)
    if len(split_entry) != 2:  # noqa: PLR2004
        return None
    return split_entry[0].strip(), split_entry[1].strip()


def _expand_entries(entries: list[str]) -> list[str]:
    expanded_entries: list[str] = []
    entry_value: str
    for entry_value in entries:
        if '"' in entry_value or "'" in entry_value:
            expanded_entries.extend(shlex.split(entry_value))
        else:
            expanded_entries.append(entry_value)
    return expanded_entries


def _normalize_responsibles_level_name(level_name: Any) -> str:
    if not isinstance(level_name, str) or level_name.strip() == "":
        raise argparse.ArgumentTypeError("config key responsiblesColumns must use non-empty string levels")
    return level_name.strip()


def _normalize_responsibles_headers(level_name: str, headers_value: Any) -> tuple[str, ...]:
    if not isinstance(headers_value, list):
        raise argparse.ArgumentTypeError(
            f"config key responsiblesColumns level '{level_name}' must contain a JSON array of headers"
        )
    headers_list: list[Any] = cast("list[Any]", headers_value)

    normalized_headers: list[str] = []
    header_value: Any
    for header_value in headers_list:
        if not isinstance(header_value, str) or header_value.strip() == "":
            raise argparse.ArgumentTypeError(
                f"config key responsiblesColumns level '{level_name}' must contain non-empty string headers"
            )
        normalized_headers.append(header_value.strip())

    if len(normalized_headers) == 0:
        raise argparse.ArgumentTypeError(
            f"config key responsiblesColumns level '{level_name}' must contain at least one header"
        )
    return tuple(normalized_headers)


def _validate_responsibles_columns(responsibles: dict[str, list[str]]) -> None:
    if not responsibles:
        raise argparse.ArgumentTypeError("responsiblesColumns must contain at least one LEVEL:HEADER mapping")
    level_name: str
    headers_list: list[str]
    for level_name, headers_list in responsibles.items():
        if len(headers_list) == 0:
            raise argparse.ArgumentTypeError(
                f"invalid responsiblesColumns entry for level '{level_name}', expected at least one header"
            )
        if any(header == "" for header in headers_list):
            raise argparse.ArgumentTypeError(
                f"invalid responsiblesColumns entry for level '{level_name}', headers must not be empty"
            )


def parse_responsibles_columns(columns_entries: list[str]) -> dict[str, tuple[str, ...]]:
    responsibles_columns: dict[str, list[str]] = {}
    current_level: str | None = None
    expanded_entries: list[str] = _expand_entries(columns_entries)
    entry: str
    for entry in expanded_entries:
        level_mapping: tuple[str, str] | None = _parse_level_mapping(entry)
        if level_mapping is not None:
            level: str = level_mapping[0]
            first_header: str = level_mapping[1]
            if level == "":
                raise argparse.ArgumentTypeError(f"invalid responsiblesColumns entry '{entry}', expected LEVEL:HEADER")
            current_level = level
            responsibles_columns.setdefault(current_level, [])
            if first_header != "":
                responsibles_columns[current_level].append(first_header)
            continue
        if current_level is None:
            raise argparse.ArgumentTypeError(f"invalid responsiblesColumns entry '{entry}', expected LEVEL:HEADER")
        responsibles_columns[current_level].append(entry.strip())

    _validate_responsibles_columns(responsibles_columns)

    normalized_responsibles_columns: dict[str, tuple[str, ...]] = {
        level: tuple(headers) for level, headers in responsibles_columns.items()
    }
    return normalized_responsibles_columns


def _normalize_responsibles_columns_from_dict(
    responsibles_columns_value: dict[Any, Any],
) -> dict[str, tuple[str, ...]]:
    normalized_responsibles_columns: dict[str, tuple[str, ...]] = {}
    level_name: Any
    headers_value: Any
    for level_name, headers_value in responsibles_columns_value.items():
        normalized_level_name: str = _normalize_responsibles_level_name(level_name)
        normalized_headers: tuple[str, ...] = _normalize_responsibles_headers(normalized_level_name, headers_value)
        normalized_responsibles_columns[normalized_level_name] = normalized_headers
    return normalized_responsibles_columns


def resolve_responsibles_columns_headers(
    config_file: str,
    logger: logging.Logger,
    cli_responsibles_columns: list[str] | None = None,
) -> dict[str, tuple[str, ...]] | None:
    if cli_responsibles_columns:
        return parse_responsibles_columns(cli_responsibles_columns)

    responsibles_columns_value: Any = read_custom_config_with_default(config_file, "responsiblesColumns", None, logger)
    if responsibles_columns_value is None:
        responsibles_columns_value = read_custom_config_with_default(config_file, "responsibles_columns", None, logger)
    if responsibles_columns_value is None:
        return None
    if isinstance(responsibles_columns_value, list):
        responsibles_columns_list: list[Any] = cast("list[Any]", responsibles_columns_value)
        string_entries: list[str] = []
        entry_value: Any
        for entry_value in responsibles_columns_list:
            if not isinstance(entry_value, str):
                raise argparse.ArgumentTypeError(
                    "config key responsiblesColumns list must contain only LEVEL:HEADER strings"
                )
            string_entries.append(entry_value)
        return parse_responsibles_columns(string_entries)
    if isinstance(responsibles_columns_value, dict):
        responsibles_columns_dict: dict[Any, Any] = cast("dict[Any, Any]", responsibles_columns_value)
        return _normalize_responsibles_columns_from_dict(responsibles_columns_dict)
    raise argparse.ArgumentTypeError(
        "config key responsiblesColumns must be a JSON object or a list of LEVEL:HEADER entries"
    )

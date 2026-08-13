#!/usr/bin/python3
"""Convert log CSV files from a Git repository into FWO log-import JSON."""

import argparse
import csv
import json
import logging
import sys
import urllib.parse
from collections.abc import Mapping
from datetime import datetime
from pathlib import Path
from typing import TextIO, cast

from scripts.customizing.fwo_custom_lib.basic_helpers import get_logger, read_custom_config
from scripts.customizing.fwo_custom_lib.git_helpers import (
    commit_and_push_deletions,
    parse_git_depth_arg,
    update_git_repo,
)

DEFAULT_CONFIG_FILE: str = "/usr/local/fworch/etc/secrets/customizingConfig.json"
DEFAULT_REPOSITORY_DIRECTORY: str = "/usr/local/fworch/etc/logDataRepo"
CSV_PATTERN: str = "*.csv"
OUTPUT_FILE: Path = Path(__file__).with_suffix(".json")
MANIFEST_FILE_NAME: str = ".fwo-log-import-manifest.json"
COMMIT_MESSAGE: str = "chore: remove imported log data"
REQUIRED_COLUMNS: set[str] = {"App ID", "Log count", "Src IP", "Dst IP", "Port"}
OPTIONAL_COLUMNS: dict[str, str] = {"Log timestamp": "log_time", "Rule name": "rule_name"}
PORT_PROTOCOLS: tuple[int, int] = (6, 17)
LogDataEntry = dict[str, str | int | None]


def get_optional_value(config_file: str, key: str, default: str, logger: logging.Logger) -> str:
    try:
        return read_custom_config(config_file, key, logger=logger)
    except (KeyError, ValueError):
        return default


def parse_optional_int(value: str) -> int | None:
    stripped_value: str = value.strip()
    return int(stripped_value) if stripped_value else None


def convert_csv_file(csv_file: Path, repository_directory: Path, logger: logging.Logger) -> list[LogDataEntry]:
    converted: list[LogDataEntry] = []
    with csv_file.open(newline="", encoding="utf-8-sig") as file_handle:
        typed_file_handle: TextIO = file_handle
        reader: csv.DictReader[str] = csv.DictReader(typed_file_handle)
        if reader.fieldnames is None or not REQUIRED_COLUMNS.issubset(reader.fieldnames):
            raise ValueError(f"{csv_file} is missing one or more mandatory columns")
        for line_number, row in enumerate(reader, start=2):
            converted_row: LogDataEntry | None = convert_row_or_log_error(
                row, csv_file, repository_directory, line_number, logger
            )
            if converted_row is not None:
                converted.append(converted_row)
    return converted


def convert_csv_file_or_log_error(
    csv_file: Path,
    repository_directory: Path,
    logger: logging.Logger,
) -> list[LogDataEntry] | None:
    """Convert one file, an unusable file is reported and skipped so the other files still import."""
    try:
        return convert_csv_file(csv_file, repository_directory, logger)
    except (OSError, UnicodeDecodeError, ValueError) as exception:
        logger.warning("ignoring %s: %s", csv_file.relative_to(repository_directory), exception)
        return None


def convert_row_or_log_error(
    row: Mapping[str, str | None],
    csv_file: Path,
    repository_directory: Path,
    line_number: int,
    logger: logging.Logger,
) -> LogDataEntry | None:
    try:
        return convert_row(row)
    except (TypeError, ValueError) as exception:
        logger.warning("ignoring %s line %s: %s", csv_file.relative_to(repository_directory), line_number, exception)
        return None


def convert_row(row: Mapping[str, str | None]) -> LogDataEntry:
    app_id: str = (row.get("App ID") or "").strip()
    source: str = (row.get("Src IP") or "").strip()
    destination: str = (row.get("Dst IP") or "").strip()
    log_count: int = int((row.get("Log count") or "").strip())
    if not app_id or not source or not destination or log_count < 1:
        raise ValueError("App ID, Log count, Src IP and Dst IP must be present")
    protocol, port = parse_service(row)
    result: LogDataEntry = {
        "app_id": app_id,
        "log_count": log_count,
        "source": source,
        "destination": destination,
        "protocol": protocol,
        "port": port,
        "action": (row.get("Action") or "accept").strip() or "accept",
    }
    add_optional_columns(result, row)
    return result


def parse_service(row: Mapping[str, str | None]) -> tuple[int | None, int | None]:
    """Read protocol and port of the logged flow, a port is only accepted for TCP and UDP."""
    protocol: int | None = parse_optional_int(row.get("Protocol") or "")
    port: int | None = parse_optional_int(row.get("Port") or "")
    if port is not None and protocol not in PORT_PROTOCOLS:
        raise ValueError(f"Port is only valid with Protocol {' or '.join(str(p) for p in PORT_PROTOCOLS)}")
    return protocol, port


def add_optional_columns(result: LogDataEntry, row: Mapping[str, str | None]) -> None:
    """Copy the columns which may be missing in the source file into the converted entry."""
    for csv_column, json_column in OPTIONAL_COLUMNS.items():
        value: str = (row.get(csv_column) or "").strip()
        if value:
            result[json_column] = normalize_log_time(value) if json_column == "log_time" else value


def normalize_log_time(value: str) -> str:
    """
    Convert a log timestamp into the ISO 8601 form the importer can deserialize.

    A value which is not a timestamp is rejected here: the importer reads the whole generated
    file at once, so one unparsable timestamp would make it discard every entry of the file.
    """
    try:
        parsed_time: datetime = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exception:
        raise ValueError(f"'{value}' is not a valid log timestamp") from exception
    return parsed_time.isoformat()


def write_import_file(entries: list[LogDataEntry], csv_files: list[Path], repository_directory: Path) -> None:
    OUTPUT_FILE.write_text(json.dumps({"logs": entries}, indent=2), encoding="utf-8")
    manifest_path: Path = repository_directory / MANIFEST_FILE_NAME
    relative_files: list[str] = [str(csv_file.relative_to(repository_directory)) for csv_file in csv_files]
    manifest_path.write_text(json.dumps({"csv_files": relative_files}, indent=2), encoding="utf-8")


def import_data(config_file: str, depth: int | None, logger: logging.Logger) -> int:
    git_repo: str = read_custom_config(config_file, "logDataGitRepo", logger=logger)
    git_user: str = read_custom_config(config_file, "logDataGitUser", logger=logger)
    git_password: str = read_custom_config(config_file, "logDataGitPassword", logger=logger)
    repository_directory: Path = Path(
        get_optional_value(config_file, "logDataGitRepoTargetDir", DEFAULT_REPOSITORY_DIRECTORY, logger)
    )
    branch: str = get_optional_value(config_file, "logDataGitBranch", "", logger)
    repo_url: str = f"https://{git_user}:{urllib.parse.quote(git_password, safe='')}@{git_repo}"
    if not update_git_repo(repo_url, str(repository_directory), logger, branch=branch or None, depth=depth):
        return 1
    csv_files: list[Path] = sorted(repository_directory.rglob(CSV_PATTERN))
    entries: list[LogDataEntry] = []
    converted_files: list[Path] = []
    for csv_file in csv_files:
        file_entries: list[LogDataEntry] | None = convert_csv_file_or_log_error(csv_file, repository_directory, logger)
        if file_entries is None:
            continue
        entries.extend(file_entries)
        converted_files.append(csv_file)
    write_import_file(entries, converted_files, repository_directory)
    logger.info("converted %s CSV files into %s log entries", len(converted_files), len(entries))
    return 0


def acknowledge_import(config_file: str, logger: logging.Logger) -> int:
    repository_directory: Path = Path(
        get_optional_value(config_file, "logDataGitRepoTargetDir", DEFAULT_REPOSITORY_DIRECTORY, logger)
    )
    manifest_path: Path = repository_directory / MANIFEST_FILE_NAME
    if not manifest_path.exists():
        logger.warning("no log data import manifest found; nothing to acknowledge")
        return 0
    manifest_data: object = json.loads(manifest_path.read_text(encoding="utf-8"))
    if not isinstance(manifest_data, dict):
        raise TypeError("Log data import manifest must be an object")
    manifest: dict[str, object] = cast("dict[str, object]", manifest_data)
    csv_file_names: object = manifest.get("csv_files", [])
    if not isinstance(csv_file_names, list):
        raise TypeError("Log data import manifest must contain a list of CSV file names")
    untyped_csv_file_names: list[object] = cast("list[object]", csv_file_names)
    if not all(isinstance(file_name, str) for file_name in untyped_csv_file_names):
        raise ValueError("Log data import manifest must contain a list of CSV file names")
    valid_csv_file_names: list[str] = [cast("str", file_name) for file_name in untyped_csv_file_names]
    csv_files: list[Path] = [repository_directory / file_name for file_name in valid_csv_file_names]
    git_user: str = read_custom_config(config_file, "logDataGitUser", logger=logger)
    git_password: str = read_custom_config(config_file, "logDataGitPassword", logger=logger)
    if not commit_and_push_deletions(
        str(repository_directory), csv_files, COMMIT_MESSAGE, logger, git_user, git_password
    ):
        return 1
    manifest_path.unlink(missing_ok=True)
    OUTPUT_FILE.unlink(missing_ok=True)
    return 0


def main() -> int:
    parser: argparse.ArgumentParser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", default=DEFAULT_CONFIG_FILE, help="Customizing configuration file")
    parser.add_argument("--depth", type=parse_git_depth_arg, default=None, help="Optional Git clone depth")
    parser.add_argument(
        "--acknowledge-import", action="store_true", help="Delete and push successfully imported CSV files"
    )
    arguments: argparse.Namespace = parser.parse_args()
    config_file: str = cast("str", arguments.config)
    depth: int | None = cast("int | None", arguments.depth)
    acknowledge: bool = cast("bool", arguments.acknowledge_import)
    logger: logging.Logger = get_logger(debug_level_in=2)
    return acknowledge_import(config_file, logger) if acknowledge else import_data(config_file, depth, logger)


if __name__ == "__main__":  # pragma: no cover
    sys.exit(main())

#!/usr/bin/python3
"""Generate matching normalized app data and importer-compatible log data."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import cast

if __package__ is None or __package__ == "":
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from scripts.customizing.app_data_import import generate_owner_data
from scripts.customizing.log_data_import import generate_log_data

MAX_GENERATED_OWNERS: int = min(
    generate_owner_data.MAX_GENERATED_OWNERS,
    generate_log_data.MAX_GENERATED_LOGS,
)
CSV_LOG_FORMAT: str = "csv"
JSON_LOG_FORMAT: str = "json"


def parse_owner_count(value: str) -> int:
    """Parse an owner count which can also receive one log flow per owner."""
    owner_count: int = generate_owner_data.parse_positive_count(value)
    if owner_count > MAX_GENERATED_OWNERS:
        raise argparse.ArgumentTypeError(
            f"owner count must not exceed {MAX_GENERATED_OWNERS} when generating matching log data"
        )
    return owner_count


def parse_arguments(argv: list[str] | None) -> argparse.Namespace:
    """Parse the generator's command-line arguments."""
    parser: argparse.ArgumentParser = argparse.ArgumentParser(
        description="Generate matching normalized app-data JSON and importer-compatible log data."
    )
    parser.add_argument("owner_count", type=parse_owner_count, help="number of applications to generate")
    parser.add_argument("app_data", type=Path, help="app-data JSON file to create")
    parser.add_argument("log_data", type=Path, help="log-data file to create")
    parser.add_argument(
        "--log-count",
        type=generate_log_data.parse_positive_count,
        help="number of log flows to generate; defaults to one flow per application",
    )
    parser.add_argument(
        "--log-format",
        choices=(CSV_LOG_FORMAT, JSON_LOG_FORMAT),
        default=JSON_LOG_FORMAT,
        help="log-data output format; defaults to json",
    )
    parser.add_argument("--overwrite", action="store_true", help="replace existing output files")
    return parser.parse_args(argv)


def outputs_are_distinct(app_data_file: Path, log_data_file: Path) -> bool:
    """Return whether the JSON and CSV output paths resolve to different files."""
    return app_data_file.resolve() != log_data_file.resolve()


def outputs_can_be_written(app_data_file: Path, log_data_file: Path, overwrite: bool) -> bool:
    """Return whether both output files can be created without data loss."""
    if not outputs_are_distinct(app_data_file, log_data_file):
        write_error("app-data and log-data output files must be different")
        return False
    if not overwrite and (app_data_file.exists() or log_data_file.exists()):
        write_error("refusing to overwrite an existing output file")
        return False
    return True


def generate_matching_data(
    owner_count: int, log_count: int
) -> tuple[dict[str, list[dict[str, object]]], list[dict[str, str | int]]]:
    """Build app data and log flows that share generated application IDs and server addresses."""
    owner_data: dict[str, list[dict[str, object]]] = generate_owner_data.generate_owner_data(owner_count)
    applications: list[generate_log_data.Application] = []
    for owner_index, owner in enumerate(owner_data["owners"]):
        application_id: object = owner["app_id_external"]
        server_values: object = owner["app_servers"]
        if not isinstance(application_id, str) or not isinstance(server_values, list) or not server_values:
            raise ValueError(f"generated owner {owner_index + 1} is missing its application ID or server address")
        servers: list[object] = cast("list[object]", server_values)
        server_value: object = servers[0]
        if not isinstance(server_value, dict):
            raise TypeError(f"generated owner {owner_index + 1} has an invalid server")
        server: dict[str, object] = cast("dict[str, object]", server_value)
        address_value: object = server.get("ip")
        if not isinstance(address_value, str):
            raise TypeError(f"generated owner {owner_index + 1} is missing its server address")
        destination = generate_log_data.parse_ip_address(address_value)
        if destination is None:
            raise ValueError(f"generated owner {owner_index + 1} has an invalid server address")
        applications.append(generate_log_data.Application(application_id, destination))
    return owner_data, generate_log_data.generate_log_entries(log_count, applications)


def write_json_log_data(output_file: Path, log_entries: list[dict[str, str | int]]) -> None:
    """Write generated flows in the JSON contract consumed by the log-data importer."""
    json_entries: list[dict[str, str | int]] = [
        {
            "app_id": str(log_entry["App ID"]),
            "log_count": int(log_entry["Log count"]),
            "source": str(log_entry["Src IP"]),
            "destination": str(log_entry["Dst IP"]),
            "port": int(log_entry["Port"]),
            "protocol": int(log_entry["Protocol"]),
            "action": str(log_entry["Action"]),
        }
        for log_entry in log_entries
    ]
    output_file.parent.mkdir(parents=True, exist_ok=True)
    output_file.write_text(json.dumps({"logs": json_entries}, indent=2), encoding="utf-8")


def write_log_data(output_file: Path, log_entries: list[dict[str, str | int]], log_format: str) -> None:
    """Write generated flows in the selected CSV or JSON log-data format."""
    if log_format == JSON_LOG_FORMAT:
        write_json_log_data(output_file, log_entries)
        return
    generate_log_data.write_log_data(output_file, log_entries)


def main(argv: list[str] | None = None) -> int:
    """Generate matching data files and return a process exit code."""
    arguments: argparse.Namespace = parse_arguments(argv)
    app_data_file: Path = arguments.app_data
    log_data_file: Path = arguments.log_data
    if not outputs_can_be_written(app_data_file, log_data_file, arguments.overwrite):
        return 1
    log_count: int = arguments.log_count if arguments.log_count is not None else arguments.owner_count
    try:
        owner_data: dict[str, list[dict[str, object]]]
        log_entries: list[dict[str, str | int]]
        owner_data, log_entries = generate_matching_data(arguments.owner_count, log_count)
        generate_owner_data.write_owner_data(app_data_file, owner_data)
        write_log_data(log_data_file, log_entries, arguments.log_format)
    except (OSError, TypeError, ValueError) as exception:
        write_error(f"could not generate matching app and log data: {exception}")
        return 1
    return 0


def write_error(message: str) -> None:
    """Write one command-line error without a traceback."""
    sys.stderr.write(f"{message}\n")


if __name__ == "__main__":
    raise SystemExit(main())

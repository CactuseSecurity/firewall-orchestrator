#!/usr/bin/python3
"""Generate importer-compatible log data for applications in normalized app-data JSON."""

from __future__ import annotations

import argparse
import csv
import ipaddress
import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import cast

CSV_COLUMNS: tuple[str, ...] = ("App ID", "Log count", "Src IP", "Dst IP", "Port", "Protocol", "Action")
DEFAULT_LOG_COUNT: int = 1
DEFAULT_PORT: int = 443
DEFAULT_PROTOCOL: int = 6
DEFAULT_ACTION: str = "accept"
TEST_SOURCE_IPV4_NETWORK: ipaddress.IPv4Network = ipaddress.IPv4Network("198.18.0.0/15")
TEST_SOURCE_IPV6_NETWORK: ipaddress.IPv6Network = ipaddress.IPv6Network("2001:2::/48")
MAX_GENERATED_LOGS: int = TEST_SOURCE_IPV4_NETWORK.num_addresses - 2
IPV4_VERSION: int = 4
JsonObject = dict[str, object]


@dataclass(frozen=True)
class Application:
    """Application which can receive generated log data."""

    app_id: str
    destination: ipaddress.IPv4Address | ipaddress.IPv6Address


def parse_positive_count(value: str) -> int:
    """Parse a positive number of generated log flows."""
    try:
        count: int = int(value)
    except ValueError as exception:
        raise argparse.ArgumentTypeError("log count must be an integer") from exception
    if count < 1:
        raise argparse.ArgumentTypeError("log count must be at least one")
    if count > MAX_GENERATED_LOGS:
        raise argparse.ArgumentTypeError(f"log count must not exceed {MAX_GENERATED_LOGS}")
    return count


def parse_ip_address(value: str) -> ipaddress.IPv4Address | ipaddress.IPv6Address | None:
    """Return an address from an app-server IP value, accepting an optional CIDR suffix."""
    try:
        return ipaddress.ip_address(value.strip().split("/", maxsplit=1)[0])
    except ValueError:
        return None


def load_applications(app_data_file: Path) -> list[Application]:
    """Load applications with usable server addresses from app-data import JSON."""
    raw_data: object = json.loads(app_data_file.read_text(encoding="utf-8"))
    if not isinstance(raw_data, dict):
        raise TypeError("app data must be an object containing an owners list")
    owner_values: object = cast("JsonObject", raw_data).get("owners")
    if not isinstance(owner_values, list):
        raise TypeError("app data must contain an owners list")

    typed_owner_values: list[object] = cast("list[object]", owner_values)
    applications: list[Application] = []
    for owner_value in typed_owner_values:
        application: Application | None = parse_application(owner_value)
        if application is not None:
            applications.append(application)
    if not applications:
        raise ValueError("app data contains no application with a valid app_id_external and server IP")
    return applications


def parse_application(owner_value: object) -> Application | None:
    """Read one app-data owner, skipping it when it has no usable server address."""
    if not isinstance(owner_value, dict):
        raise TypeError("each owner must be an object")
    owner: JsonObject = cast("JsonObject", owner_value)
    app_id_value: object = owner.get("app_id_external")
    if not isinstance(app_id_value, str) or not app_id_value.strip():
        raise ValueError("each owner must contain a non-empty app_id_external")
    server_values: object = owner.get("app_servers")
    if not isinstance(server_values, list):
        raise TypeError(f"application {app_id_value} must contain an app_servers list")

    typed_server_values: list[object] = cast("list[object]", server_values)
    for server_value in typed_server_values:
        destination: ipaddress.IPv4Address | ipaddress.IPv6Address | None = parse_server_address(server_value)
        if destination is not None:
            return Application(app_id=app_id_value.strip(), destination=destination)
    return None


def parse_server_address(server_value: object) -> ipaddress.IPv4Address | ipaddress.IPv6Address | None:
    """Extract a valid address from an app-server object."""
    if not isinstance(server_value, dict):
        return None
    ip_value: object = cast("JsonObject", server_value).get("ip")
    return parse_ip_address(ip_value) if isinstance(ip_value, str) else None


def generate_log_entries(log_count: int, applications: list[Application]) -> list[dict[str, str | int]]:
    """Generate distinct TCP log flows, distributing them round-robin across applications."""
    if log_count < 1 or log_count > MAX_GENERATED_LOGS:
        raise ValueError(f"log count must be between 1 and {MAX_GENERATED_LOGS}")
    if not applications:
        raise ValueError("at least one application is required")

    entries: list[dict[str, str | int]] = []
    log_index: int
    for log_index in range(log_count):
        application: Application = applications[log_index % len(applications)]
        entries.append(
            {
                "App ID": application.app_id,
                "Log count": DEFAULT_LOG_COUNT,
                "Src IP": str(generate_source_address(log_index, application.destination.version)),
                "Dst IP": str(application.destination),
                "Port": DEFAULT_PORT,
                "Protocol": DEFAULT_PROTOCOL,
                "Action": DEFAULT_ACTION,
            }
        )
    return entries


def generate_source_address(log_index: int, version: int) -> ipaddress.IPv4Address | ipaddress.IPv6Address:
    """Return the unique test source address assigned to a generated flow."""
    source_offset: int = log_index + 1
    if version == IPV4_VERSION:
        return ipaddress.IPv4Address(int(TEST_SOURCE_IPV4_NETWORK.network_address) + source_offset)
    return ipaddress.IPv6Address(int(TEST_SOURCE_IPV6_NETWORK.network_address) + source_offset)


def write_log_data(output_file: Path, entries: list[dict[str, str | int]]) -> None:
    """Write generated entries in the CSV contract consumed by the log-data importer."""
    output_file.parent.mkdir(parents=True, exist_ok=True)
    with output_file.open("w", newline="", encoding="utf-8") as file_handle:
        writer: csv.DictWriter[str] = csv.DictWriter(file_handle, fieldnames=CSV_COLUMNS)
        writer.writeheader()
        writer.writerows(entries)


def parse_arguments(argv: list[str] | None) -> argparse.Namespace:
    """Parse the generator's command-line arguments."""
    parser: argparse.ArgumentParser = argparse.ArgumentParser(
        description="Generate distinct importer-compatible log flows for normalized app-data JSON."
    )
    parser.add_argument("log_count", type=parse_positive_count, help="number of distinct log flows to generate")
    parser.add_argument("app_data", type=Path, help="normalized app-data JSON containing an owners list")
    parser.add_argument("output", type=Path, help="CSV file to create")
    parser.add_argument("--overwrite", action="store_true", help="replace an existing output file")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    """Generate a CSV file and return a process exit code."""
    arguments: argparse.Namespace = parse_arguments(argv)
    output_file: Path = arguments.output
    if output_file.exists() and not arguments.overwrite:
        write_error(f"refusing to overwrite existing file: {output_file}")
        return 1
    try:
        applications: list[Application] = load_applications(arguments.app_data)
        entries: list[dict[str, str | int]] = generate_log_entries(arguments.log_count, applications)
        write_log_data(output_file, entries)
    except (OSError, TypeError, ValueError) as exception:
        write_error(f"could not generate log data: {exception}")
        return 1
    return 0


def write_error(message: str) -> None:
    """Write one command-line error without a traceback."""
    sys.stderr.write(f"{message}\n")


if __name__ == "__main__":
    raise SystemExit(main())

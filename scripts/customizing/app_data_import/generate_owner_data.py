#!/usr/bin/python3
"""Generate normalized owner data for local testing and log-data generation."""

import argparse
import ipaddress
import json
import sys
from pathlib import Path

APP_ID_PREFIX: str = "APP-"
APP_ID_WIDTH: int = 6
DEFAULT_IMPORT_SOURCE: str = "generated-owner-data"
DEFAULT_RECERT_PERIOD_DAYS: int = 365
DEFAULT_OWNER_LIFECYCLE_STATE: str = "active"
DEFAULT_SERVER_TYPE: str = "server"
TEST_OWNER_IPV4_NETWORK: ipaddress.IPv4Network = ipaddress.IPv4Network("10.0.0.0/8")
MAX_GENERATED_OWNERS: int = TEST_OWNER_IPV4_NETWORK.num_addresses - 2


def parse_positive_count(value: str) -> int:
    """Parse a positive number of owners to generate."""
    try:
        count: int = int(value)
    except ValueError as exception:
        raise argparse.ArgumentTypeError("owner count must be an integer") from exception
    if count < 1:
        raise argparse.ArgumentTypeError("owner count must be at least one")
    if count > MAX_GENERATED_OWNERS:
        raise argparse.ArgumentTypeError(f"owner count must not exceed {MAX_GENERATED_OWNERS}")
    return count


def generate_owner_data(owner_count: int) -> dict[str, list[dict[str, object]]]:
    """Generate normalized owners with one unique server address each."""
    if owner_count < 1 or owner_count > MAX_GENERATED_OWNERS:
        raise ValueError(f"owner count must be between 1 and {MAX_GENERATED_OWNERS}")
    owners: list[dict[str, object]] = [generate_owner(owner_index) for owner_index in range(owner_count)]
    return {"owners": owners}


def generate_owner(owner_index: int) -> dict[str, object]:
    """Generate one owner in the app-data import format."""
    owner_number: int = owner_index + 1
    app_id: str = f"{APP_ID_PREFIX}{owner_number:0{APP_ID_WIDTH}d}"
    server_ip: str = str(ipaddress.IPv4Address(int(TEST_OWNER_IPV4_NETWORK.network_address) + owner_number))
    return {
        "name": f"Generated Application {owner_number}",
        "app_id_external": app_id,
        "import_source": DEFAULT_IMPORT_SOURCE,
        "app_servers": [
            {
                "name": f"Generated Server {owner_number}",
                "app_id_external": app_id,
                "ip": server_ip,
                "ip_end": server_ip,
                "type": DEFAULT_SERVER_TYPE,
            }
        ],
        "recert_active": False,
        "recert_period_days": DEFAULT_RECERT_PERIOD_DAYS,
        "days_until_first_recert": DEFAULT_RECERT_PERIOD_DAYS,
        "owner_lifecycle_state": DEFAULT_OWNER_LIFECYCLE_STATE,
    }


def write_owner_data(output_file: Path, owner_data: dict[str, list[dict[str, object]]]) -> None:
    """Write owner data to a UTF-8 JSON file."""
    output_file.parent.mkdir(parents=True, exist_ok=True)
    output_file.write_text(json.dumps(owner_data, indent=2), encoding="utf-8")


def parse_arguments(argv: list[str] | None) -> argparse.Namespace:
    """Parse the generator's command-line arguments."""
    parser: argparse.ArgumentParser = argparse.ArgumentParser(
        description="Generate normalized owner data with server IPs for local testing."
    )
    parser.add_argument("owner_count", type=parse_positive_count, help="number of owners to generate")
    parser.add_argument("output", type=Path, help="JSON file to create")
    parser.add_argument("--overwrite", action="store_true", help="replace an existing output file")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    """Generate an owner-data file and return a process exit code."""
    arguments: argparse.Namespace = parse_arguments(argv)
    output_file: Path = arguments.output
    if output_file.exists() and not arguments.overwrite:
        write_error(f"refusing to overwrite existing file: {output_file}")
        return 1
    try:
        write_owner_data(output_file, generate_owner_data(arguments.owner_count))
    except (OSError, ValueError) as exception:
        write_error(f"could not generate owner data: {exception}")
        return 1
    return 0


def write_error(message: str) -> None:
    """Write one command-line error without a traceback."""
    sys.stderr.write(f"{message}\n")


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/python3
"""Generate normalized owner data for local testing and log-data generation."""

from __future__ import annotations

import argparse
import ipaddress
import json
import socket
import sys
from collections.abc import Callable
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

APP_ID_PREFIX: str = "APP-"
APP_ID_WIDTH: int = 6
DEFAULT_IMPORT_SOURCE: str = "generated-owner-data"
DEFAULT_RECERT_PERIOD_DAYS: int = 365
DEFAULT_OWNER_LIFECYCLE_STATE: str = "active"
DEFAULT_SERVER_TYPE: str = "server"
TEST_OWNER_IPV4_NETWORK: ipaddress.IPv4Network = ipaddress.IPv4Network("10.0.0.0/8")
MAX_GENERATED_OWNERS: int = TEST_OWNER_IPV4_NETWORK.num_addresses - 2
# Public resolver addresses whose PTR records are stable, so log data generated for these owners
# carries a source or destination name after the reverse lookup of the log data import. Addresses
# of the private test network have no PTR record in a typical test environment.
REVERSE_DNS_CANDIDATE_ADDRESSES: list[str] = [
    "8.8.8.8",
    "8.8.4.4",
    "1.1.1.1",
    "1.0.0.1",
    "9.9.9.9",
    "149.112.112.112",
    "208.67.222.222",
    "208.67.220.220",
    "208.67.222.220",
    "208.67.220.222",
    "4.2.2.1",
    "4.2.2.2",
    "4.2.2.3",
    "4.2.2.4",
    "4.2.2.5",
    "4.2.2.6",
    "185.228.168.9",
    "185.228.169.9",
    "94.140.14.14",
    "94.140.15.15",
    "76.76.2.0",
    "76.76.10.0",
    "156.154.70.1",
    "156.154.71.1",
    "8.26.56.26",
    "8.20.247.20",
    "2001:4860:4860::8888",
    "2001:4860:4860::8844",
    "2606:4700:4700::1111",
    "2606:4700:4700::1001",
    "2620:fe::fe",
    "2620:fe::9",
]
# an address without PTR record only answers after the resolver timed out, so the lookups overlap
REVERSE_LOOKUP_PARALLELISM: int = 16

ReverseLookup = Callable[[str], str]


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


def generate_owner_data(
    owner_count: int, reverse_dns_resolvable: bool = False, reverse_lookup: ReverseLookup | None = None
) -> dict[str, list[dict[str, object]]]:
    """
    Generate normalized owners with one unique server address each.

    With reverse_dns_resolvable, the server addresses are taken from the public candidates whose
    reverse lookup succeeds on this host instead of from the private test network.
    """
    if owner_count < 1 or owner_count > MAX_GENERATED_OWNERS:
        raise ValueError(f"owner count must be between 1 and {MAX_GENERATED_OWNERS}")
    server_ips: list[str] = (
        find_reverse_dns_resolvable_addresses(owner_count, reverse_lookup or reverse_lookup_name)
        if reverse_dns_resolvable
        else [get_test_network_address(owner_index) for owner_index in range(owner_count)]
    )
    owners: list[dict[str, object]] = [
        generate_owner(owner_index, server_ip) for owner_index, server_ip in enumerate(server_ips)
    ]
    return {"owners": owners}


def get_test_network_address(owner_index: int) -> str:
    """Return the unique address of an owner in the private test network."""
    return str(ipaddress.IPv4Address(int(TEST_OWNER_IPV4_NETWORK.network_address) + owner_index + 1))


def find_reverse_dns_resolvable_addresses(owner_count: int, reverse_lookup: ReverseLookup) -> list[str]:
    """Return the first owner_count candidate addresses whose reverse lookup yields a name."""

    def resolve(address: str) -> tuple[str, str]:
        return address, reverse_lookup(address)

    with ThreadPoolExecutor(max_workers=REVERSE_LOOKUP_PARALLELISM) as executor:
        resolved_names: list[tuple[str, str]] = list(executor.map(resolve, REVERSE_DNS_CANDIDATE_ADDRESSES))
    resolvable_addresses: list[str] = [address for address, name in resolved_names if name]
    if len(resolvable_addresses) < owner_count:
        raise ValueError(
            f"only {len(resolvable_addresses)} of {len(REVERSE_DNS_CANDIDATE_ADDRESSES)} candidate addresses "
            f"are reverse DNS resolvable on this host, but {owner_count} owners were requested; "
            "use --no-reverse-dns-resolvable to generate addresses of the private test network instead"
        )
    return resolvable_addresses[:owner_count]


def reverse_lookup_name(address: str) -> str:
    """Return the name a reverse lookup of the address yields, or an empty string."""
    try:
        return socket.gethostbyaddr(address)[0]
    except OSError:
        return ""


def generate_owner(owner_index: int, server_ip: str) -> dict[str, object]:
    """Generate one owner in the app-data import format."""
    owner_number: int = owner_index + 1
    app_id: str = f"{APP_ID_PREFIX}{owner_number:0{APP_ID_WIDTH}d}"
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
    add_reverse_dns_argument(parser, default=False)
    return parser.parse_args(argv)


def add_reverse_dns_argument(parser: argparse.ArgumentParser, default: bool) -> None:
    """Add the switch between reverse DNS resolvable server addresses and the private test network."""
    parser.add_argument(
        "--reverse-dns-resolvable",
        action=argparse.BooleanOptionalAction,
        default=default,
        help=(
            "use public server addresses which are reverse DNS resolvable on this host instead of the "
            f"private test network; limits the owner count to {len(REVERSE_DNS_CANDIDATE_ADDRESSES)}"
        ),
    )


def main(argv: list[str] | None = None) -> int:
    """Generate an owner-data file and return a process exit code."""
    arguments: argparse.Namespace = parse_arguments(argv)
    output_file: Path = arguments.output
    reverse_dns_resolvable: bool = arguments.reverse_dns_resolvable
    if output_file.exists() and not arguments.overwrite:
        write_error(f"refusing to overwrite existing file: {output_file}")
        return 1
    try:
        write_owner_data(output_file, generate_owner_data(arguments.owner_count, reverse_dns_resolvable))
    except (OSError, ValueError) as exception:
        write_error(f"could not generate owner data: {exception}")
        return 1
    return 0


def write_error(message: str) -> None:
    """Write one command-line error without a traceback."""
    sys.stderr.write(f"{message}\n")


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/python3
"""Generate sample area IP data covering the app servers of an app-data file."""

from __future__ import annotations

import argparse
import ipaddress
import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import cast

AREA_NAME_PREFIX: str = "Generated Area "
# NA00 is used for the internet area by convert_area_ip_data_from_git.py, so generated areas start at NA01
AREA_ID_PREFIX: str = "NA"
AREA_ID_WIDTH: int = 2
DEFAULT_AREA_COUNT: int = 3

JsonObject = dict[str, object]
AreaIpData = dict[str, list[JsonObject]]


@dataclass(frozen=True)
class AppServer:
    """Address range of one app server and the name its area subnets are given."""

    name: str
    start: ipaddress.IPv4Address | ipaddress.IPv6Address
    end: ipaddress.IPv4Address | ipaddress.IPv6Address


def parse_positive_count(value: str) -> int:
    """Parse a positive number of areas to generate."""
    try:
        count: int = int(value)
    except ValueError as exception:
        raise argparse.ArgumentTypeError("area count must be an integer") from exception
    if count < 1:
        raise argparse.ArgumentTypeError("area count must be at least one")
    return count


def load_app_servers(app_data_file: Path) -> list[AppServer]:
    """Load the app servers with a usable address range from an app-data import JSON file."""
    return parse_app_servers(json.loads(app_data_file.read_text(encoding="utf-8")))


def parse_app_servers(app_data: object) -> list[AppServer]:
    """
    Read the app servers of all owners, keeping the first server of every distinct address range.

    Servers without a usable address range are skipped, as the app-data import would not create them.
    """
    if not isinstance(app_data, dict):
        raise TypeError("app data must be an object containing an owners list")
    owner_values: object = cast("JsonObject", app_data).get("owners")
    if not isinstance(owner_values, list):
        raise TypeError("app data must contain an owners list")
    servers: list[AppServer] = []
    known_ranges: set[
        tuple[ipaddress.IPv4Address | ipaddress.IPv6Address, ipaddress.IPv4Address | ipaddress.IPv6Address]
    ] = set()
    for owner_value in cast("list[object]", owner_values):
        for server in parse_owner_servers(owner_value):
            if (server.start, server.end) not in known_ranges:
                known_ranges.add((server.start, server.end))
                servers.append(server)
    if not servers:
        raise ValueError("app data contains no app server with a valid IP address")
    return servers


def parse_owner_servers(owner_value: object) -> list[AppServer]:
    """Read the usable app servers of one owner."""
    if not isinstance(owner_value, dict):
        raise TypeError("each owner must be an object")
    server_values: object = cast("JsonObject", owner_value).get("app_servers")
    if not isinstance(server_values, list):
        return []
    servers: list[AppServer] = []
    for server_value in cast("list[object]", server_values):
        server: AppServer | None = parse_app_server(server_value)
        if server is not None:
            servers.append(server)
    return servers


def parse_app_server(server_value: object) -> AppServer | None:
    """
    Read the address range of one app server.

    Without ip_end, an ip with a netmask stands for the whole network, as the app-data import treats it.
    """
    if not isinstance(server_value, dict):
        return None
    server: JsonObject = cast("JsonObject", server_value)
    ip_value: object = server.get("ip")
    ip_end_value: object = server.get("ip_end")
    if not isinstance(ip_value, str):
        return None
    try:
        start_interface: ipaddress.IPv4Interface | ipaddress.IPv6Interface = ipaddress.ip_interface(ip_value.strip())
        if isinstance(ip_end_value, str) and ip_end_value.strip():
            start: ipaddress.IPv4Address | ipaddress.IPv6Address = start_interface.ip
            end: ipaddress.IPv4Address | ipaddress.IPv6Address = ipaddress.ip_interface(ip_end_value.strip()).ip
        else:
            start = start_interface.network.network_address
            end = start_interface.network.broadcast_address
    except ValueError:
        return None
    if not is_ascending_range(start, end):
        return None
    name_value: object = server.get("name")
    name: str = name_value.strip() if isinstance(name_value, str) and name_value.strip() else str(start)
    return AppServer(name=name, start=start, end=end)


def is_ascending_range(
    start: ipaddress.IPv4Address | ipaddress.IPv6Address, end: ipaddress.IPv4Address | ipaddress.IPv6Address
) -> bool:
    """Return whether start and end belong to one address family and start does not exceed end."""
    if isinstance(start, ipaddress.IPv4Address) and isinstance(end, ipaddress.IPv4Address):
        return start <= end
    if isinstance(start, ipaddress.IPv6Address) and isinstance(end, ipaddress.IPv6Address):
        return start <= end
    return False


def generate_area_ip_data(servers: list[AppServer], area_count: int = DEFAULT_AREA_COUNT) -> AreaIpData:
    """
    Distribute the app servers over area_count areas in the format read by ImportIpDataJob.

    Consecutive servers share an area and the area sizes differ by at most one. Fewer areas are
    generated when there are fewer servers than areas, as an area without subnets matches nothing.
    """
    if area_count < 1:
        raise ValueError("area count must be at least one")
    if not servers:
        raise ValueError("at least one app server is required")
    effective_area_count: int = min(area_count, len(servers))
    base_size: int
    remainder: int
    base_size, remainder = divmod(len(servers), effective_area_count)
    areas: list[JsonObject] = []
    first_server: int = 0
    for area_index in range(effective_area_count):
        area_size: int = base_size + (1 if area_index < remainder else 0)
        areas.append(generate_area(area_index, servers[first_server : first_server + area_size]))
        first_server += area_size
    return {"areas": areas}


def generate_area(area_index: int, servers: list[AppServer]) -> JsonObject:
    """Generate one area whose subnets cover exactly the given app servers."""
    area_number: int = area_index + 1
    subnets: list[JsonObject] = [
        {"name": server.name, "ip": network.with_prefixlen}
        for server in servers
        for network in summarize_range(server.start, server.end)
    ]
    return {
        "name": f"{AREA_NAME_PREFIX}{area_number}",
        "id_string": f"{AREA_ID_PREFIX}{area_number:0{AREA_ID_WIDTH}d}",
        "subnets": subnets,
    }


def summarize_range(
    start: ipaddress.IPv4Address | ipaddress.IPv6Address, end: ipaddress.IPv4Address | ipaddress.IPv6Address
) -> list[ipaddress.IPv4Network | ipaddress.IPv6Network]:
    """
    Return the networks covering exactly the range from start to end.

    The area import accepts a single network per subnet, so a range is split into several subnets.
    """
    if isinstance(start, ipaddress.IPv4Address) and isinstance(end, ipaddress.IPv4Address):
        return list(ipaddress.summarize_address_range(start, end))
    if isinstance(start, ipaddress.IPv6Address) and isinstance(end, ipaddress.IPv6Address):
        return list(ipaddress.summarize_address_range(start, end))
    raise ValueError(f"range {start}-{end} mixes IPv4 and IPv6")


def write_area_ip_data(output_file: Path, area_ip_data: AreaIpData) -> None:
    """Write area IP data to a UTF-8 JSON file."""
    output_file.parent.mkdir(parents=True, exist_ok=True)
    output_file.write_text(json.dumps(area_ip_data, indent=2), encoding="utf-8")


def add_area_count_argument(parser: argparse.ArgumentParser) -> None:
    """Add the number of areas the app servers are distributed over."""
    parser.add_argument(
        "--area-count",
        type=parse_positive_count,
        default=DEFAULT_AREA_COUNT,
        help=f"number of areas to distribute the app servers over; defaults to {DEFAULT_AREA_COUNT}",
    )


def parse_arguments(argv: list[str] | None) -> argparse.Namespace:
    """Parse the generator's command-line arguments."""
    parser: argparse.ArgumentParser = argparse.ArgumentParser(
        description="Generate area IP data for ImportIpDataJob covering the app servers of an app-data file."
    )
    parser.add_argument("app_data", type=Path, help="app-data JSON file to read the app servers from")
    parser.add_argument("output", type=Path, help="area IP data JSON file to create")
    add_area_count_argument(parser)
    parser.add_argument("--overwrite", action="store_true", help="replace an existing output file")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    """Generate an area IP data file and return a process exit code."""
    arguments: argparse.Namespace = parse_arguments(argv)
    app_data_file: Path = arguments.app_data
    output_file: Path = arguments.output
    area_count: int = arguments.area_count
    if output_file.exists() and not arguments.overwrite:
        write_error(f"refusing to overwrite existing file: {output_file}")
        return 1
    try:
        write_area_ip_data(output_file, generate_area_ip_data(load_app_servers(app_data_file), area_count))
    except (OSError, TypeError, ValueError) as exception:
        write_error(f"could not generate area IP data: {exception}")
        return 1
    return 0


def write_error(message: str) -> None:
    """Write one command-line error without a traceback."""
    sys.stderr.write(f"{message}\n")


if __name__ == "__main__":
    raise SystemExit(main())

"""Validated numeric source and destination port ranges shared by vendor importers."""

from __future__ import annotations

from typing import Final

k_max_port: Final[int] = 65535


def parse_port_range(value: str) -> tuple[int, int]:
    """Parse a single numeric range; reject syntax that would otherwise lose a restriction."""
    value = value.strip()
    if value.lower() == "any":
        return 1, k_max_port
    if value.startswith(">"):
        start, end = int(value[1:]) + 1, k_max_port
    elif value.startswith("<"):
        start, end = 1, int(value[1:]) - 1
    elif "-" in value:
        first, last = value.split("-", 1)
        start, end = int(first), int(last)
    else:
        start = end = int(value)
    if not 0 <= start <= end <= k_max_port:
        raise ValueError(f"Invalid port range '{value}'.")
    return start, end


def parse_transport_ports(value: str) -> tuple[int, int, int | None, int | None]:
    """Parse Fortinet destination[:source] syntax, keeping each source/destination pair together."""
    destination, separator, source = value.partition(":")
    port, port_end = parse_port_range(destination)
    source_port, source_port_end = parse_port_range(source) if separator else (None, None)
    return port, port_end, source_port, source_port_end

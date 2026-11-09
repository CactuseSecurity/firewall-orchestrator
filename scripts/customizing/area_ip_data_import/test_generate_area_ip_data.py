from __future__ import annotations

import ipaddress
import json
from typing import TYPE_CHECKING, cast

import pytest

from scripts.customizing.app_data_import import generate_owner_data as owner_generator
from scripts.customizing.area_ip_data_import import generate_area_ip_data as generator

if TYPE_CHECKING:
    from pathlib import Path


def new_owner(app_id: str, servers: list[object]) -> dict[str, object]:
    """Return an app-data owner with the given app servers."""
    return {"app_id_external": app_id, "app_servers": servers}


def new_server(ip: str, ip_end: str | None = None, name: str = "server") -> dict[str, object]:
    """Return an app-data server, omitting ip_end when it is not given."""
    server: dict[str, object] = {"name": name, "ip": ip}
    if ip_end is not None:
        server["ip_end"] = ip_end
    return server


def get_subnets(area: dict[str, object]) -> list[dict[str, str]]:
    """Return the subnets of a generated area."""
    return cast("list[dict[str, str]]", area["subnets"])


def test_command_line_annotations_are_postponed_for_python_39() -> None:
    """Keep CLI annotations from being evaluated while importing on Python 3.9."""
    assert generator.parse_arguments.__annotations__["argv"] == "list[str] | None"
    assert generator.main.__annotations__["argv"] == "list[str] | None"


def test_parse_app_servers_reads_ranges_networks_and_names() -> None:
    app_data: dict[str, object] = {
        "owners": [
            new_owner("APP-1", [new_server("10.0.0.1", "10.0.0.1", "host"), new_server("10.0.1.0/30", name="")]),
            new_owner("APP-2", [new_server("10.0.2.1/32", "10.0.2.4/32", "range"), new_server("2001:db8::1")]),
        ]
    }

    servers: list[generator.AppServer] = generator.parse_app_servers(app_data)

    assert [(server.name, str(server.start), str(server.end)) for server in servers] == [
        ("host", "10.0.0.1", "10.0.0.1"),
        ("10.0.1.0", "10.0.1.0", "10.0.1.3"),
        ("range", "10.0.2.1", "10.0.2.4"),
        ("server", "2001:db8::1", "2001:db8::1"),
    ]


def test_parse_app_servers_skips_unusable_and_repeated_servers() -> None:
    app_data: dict[str, object] = {
        "owners": [
            new_owner("APP-1", [new_server("10.0.0.1", name="first"), new_server("not-an-ip")]),
            new_owner("APP-2", [new_server("10.0.0.1", name="shared"), new_server("10.0.0.9", "10.0.0.2")]),
            new_owner("APP-3", [new_server("10.0.0.3", "2001:db8::3"), "not-a-server"]),
            {"app_id_external": "APP-4"},
        ]
    }

    servers: list[generator.AppServer] = generator.parse_app_servers(app_data)

    assert [(server.name, str(server.start)) for server in servers] == [("first", "10.0.0.1")]


@pytest.mark.parametrize(
    ("app_data", "error_type"),
    [
        ([], TypeError),
        ({"owners": {}}, TypeError),
        ({"owners": ["not-an-owner"]}, TypeError),
        ({"owners": [new_owner("APP-1", [])]}, ValueError),
    ],
)
def test_parse_app_servers_rejects_unusable_app_data(app_data: object, error_type: type[Exception]) -> None:
    with pytest.raises(error_type):
        generator.parse_app_servers(app_data)


def test_generate_area_ip_data_distributes_consecutive_servers_evenly() -> None:
    servers: list[generator.AppServer] = [
        generator.AppServer(
            f"server {host}", ipaddress.IPv4Address(f"10.0.0.{host}"), ipaddress.IPv4Address(f"10.0.0.{host}")
        )
        for host in range(1, 8)
    ]

    areas: list[dict[str, object]] = generator.generate_area_ip_data(servers, 3)["areas"]

    assert [area["id_string"] for area in areas] == ["NA01", "NA02", "NA03"]
    assert [area["name"] for area in areas] == ["Generated Area 1", "Generated Area 2", "Generated Area 3"]
    assert [[subnet["ip"] for subnet in get_subnets(area)] for area in areas] == [
        ["10.0.0.1/32", "10.0.0.2/32", "10.0.0.3/32"],
        ["10.0.0.4/32", "10.0.0.5/32"],
        ["10.0.0.6/32", "10.0.0.7/32"],
    ]
    assert get_subnets(areas[0])[0]["name"] == "server 1"


def test_generate_area_ip_data_creates_no_area_without_servers_to_cover() -> None:
    address: ipaddress.IPv6Address = ipaddress.IPv6Address("2001:db8::1")

    areas: list[dict[str, object]] = generator.generate_area_ip_data([generator.AppServer("v6", address, address)], 4)[
        "areas"
    ]

    assert len(areas) == 1
    assert get_subnets(areas[0]) == [{"name": "v6", "ip": "2001:db8::1/128"}]


def test_generate_area_ip_data_splits_a_range_into_covering_networks() -> None:
    server: generator.AppServer = generator.AppServer(
        "range", ipaddress.IPv4Address("10.0.0.1"), ipaddress.IPv4Address("10.0.0.4")
    )

    areas: list[dict[str, object]] = generator.generate_area_ip_data([server], 1)["areas"]

    assert [subnet["ip"] for subnet in get_subnets(areas[0])] == ["10.0.0.1/32", "10.0.0.2/31", "10.0.0.4/32"]


def test_generate_area_ip_data_rejects_an_area_count_below_one() -> None:
    address: ipaddress.IPv4Address = ipaddress.IPv4Address("10.0.0.1")

    with pytest.raises(ValueError, match="area count must be at least one"):
        generator.generate_area_ip_data([generator.AppServer("server", address, address)], 0)


def test_generate_area_ip_data_rejects_missing_servers() -> None:
    with pytest.raises(ValueError, match="at least one app server"):
        generator.generate_area_ip_data([], 1)


def test_summarize_range_rejects_mixed_address_families() -> None:
    with pytest.raises(ValueError, match="mixes IPv4 and IPv6"):
        generator.summarize_range(ipaddress.IPv4Address("10.0.0.1"), ipaddress.IPv6Address("2001:db8::1"))


@pytest.mark.parametrize("value", ["0", "-1", "x"])
def test_parse_positive_count_rejects_invalid_area_counts(value: str) -> None:
    with pytest.raises(Exception, match="area count"):
        generator.parse_positive_count(value)


def test_main_generates_area_ip_data_for_generated_owner_data(tmp_path: Path) -> None:
    app_data_file: Path = tmp_path / "app-data.json"
    output_file: Path = tmp_path / "area-ip-data.json"
    owner_generator.write_owner_data(app_data_file, owner_generator.generate_owner_data(4))

    result: int = generator.main([str(app_data_file), str(output_file), "--area-count", "2"])

    areas: list[dict[str, object]] = json.loads(output_file.read_text(encoding="utf-8"))["areas"]
    assert result == 0
    assert [[subnet["ip"] for subnet in get_subnets(area)] for area in areas] == [
        ["10.0.0.1/32", "10.0.0.2/32"],
        ["10.0.0.3/32", "10.0.0.4/32"],
    ]
    assert get_subnets(areas[0])[0]["name"] == "Generated Server 1"


def test_main_preserves_an_existing_file_unless_overwrite_was_requested(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    app_data_file: Path = tmp_path / "app-data.json"
    output_file: Path = tmp_path / "area-ip-data.json"
    owner_generator.write_owner_data(app_data_file, owner_generator.generate_owner_data(1))
    output_file.write_text("keep", encoding="utf-8")

    refused: int = generator.main([str(app_data_file), str(output_file)])
    kept_content: str = output_file.read_text(encoding="utf-8")
    overwritten: int = generator.main([str(app_data_file), str(output_file), "--overwrite"])

    assert refused == 1
    assert kept_content == "keep"
    assert "refusing to overwrite" in capsys.readouterr().err
    assert overwritten == 0
    assert json.loads(output_file.read_text(encoding="utf-8"))["areas"][0]["id_string"] == "NA01"


def test_main_reports_unreadable_app_data_without_writing(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    app_data_file: Path = tmp_path / "app-data.json"
    output_file: Path = tmp_path / "area-ip-data.json"
    app_data_file.write_text("not json", encoding="utf-8")

    result: int = generator.main([str(app_data_file), str(output_file)])

    assert result == 1
    assert not output_file.exists()
    assert "could not generate area IP data" in capsys.readouterr().err

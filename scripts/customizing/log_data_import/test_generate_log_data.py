import csv
import ipaddress
import json
from pathlib import Path

import pytest

from scripts.customizing.log_data_import import generate_log_data as generator


def write_app_data(app_data_file: Path) -> None:
    """Write normalized app-data JSON with IPv4 and IPv6 application servers."""
    app_data_file.write_text(
        json.dumps(
            {
                "owners": [
                    {"app_id_external": "APP-1", "app_servers": [{"ip": "192.0.2.10"}]},
                    {"app_id_external": "APP-2", "app_servers": [{"ip": "2001:db8::20"}]},
                ]
            }
        ),
        encoding="utf-8",
    )


def test_main_generates_distinct_flows_for_the_supplied_applications(tmp_path: Path) -> None:
    app_data_file: Path = tmp_path / "owners.json"
    output_file: Path = tmp_path / "generated-logs.csv"
    write_app_data(app_data_file)

    result: int = generator.main(["4", str(app_data_file), str(output_file)])

    with output_file.open(newline="", encoding="utf-8") as file_handle:
        rows: list[dict[str, str]] = list(csv.DictReader(file_handle))
    assert result == 0
    assert [row["App ID"] for row in rows] == ["APP-1", "APP-2", "APP-1", "APP-2"]
    assert [row["Dst IP"] for row in rows] == ["192.0.2.10", "2001:db8::20", "192.0.2.10", "2001:db8::20"]
    assert len({row["Src IP"] for row in rows}) == 4
    assert all(row["Log count"] == "1" for row in rows)
    assert all(row["Port"] == "443" and row["Protocol"] == "6" and row["Action"] == "accept" for row in rows)


def test_load_applications_skips_owners_without_server_addresses(tmp_path: Path) -> None:
    app_data_file: Path = tmp_path / "owners.json"
    app_data_file.write_text(
        json.dumps(
            {
                "owners": [
                    {"app_id_external": "APP-WITHOUT-SERVER", "app_servers": []},
                    {"app_id_external": "APP-WITH-SERVER", "app_servers": [{"ip": "192.0.2.20/24"}]},
                ]
            }
        ),
        encoding="utf-8",
    )

    applications: list[generator.Application] = generator.load_applications(app_data_file)

    assert applications == [generator.Application("APP-WITH-SERVER", ipaddress.IPv4Address("192.0.2.20"))]


@pytest.mark.parametrize("content", ["[]", "{}", '{"owners": []}'])
def test_load_applications_rejects_invalid_or_unusable_app_data(tmp_path: Path, content: str) -> None:
    app_data_file: Path = tmp_path / "owners.json"
    app_data_file.write_text(content, encoding="utf-8")

    with pytest.raises((TypeError, ValueError), match=r"owners|no application"):
        generator.load_applications(app_data_file)


def test_generate_log_entries_rejects_unsupported_log_counts() -> None:
    application: generator.Application = generator.Application("APP-1", ipaddress.IPv4Address("192.0.2.10"))

    with pytest.raises(ValueError, match="between 1"):
        generator.generate_log_entries(0, [application])


def test_main_preserves_an_existing_file_unless_overwrite_was_requested(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    app_data_file: Path = tmp_path / "owners.json"
    output_file: Path = tmp_path / "generated-logs.csv"
    write_app_data(app_data_file)
    output_file.write_text("keep", encoding="utf-8")

    result: int = generator.main(["1", str(app_data_file), str(output_file)])

    assert result == 1
    assert output_file.read_text(encoding="utf-8") == "keep"
    assert "refusing to overwrite" in capsys.readouterr().err


def test_main_overwrites_an_existing_file_when_explicitly_requested(tmp_path: Path) -> None:
    app_data_file: Path = tmp_path / "owners.json"
    output_file: Path = tmp_path / "generated-logs.csv"
    write_app_data(app_data_file)
    output_file.write_text("replace", encoding="utf-8")

    result: int = generator.main(["1", str(app_data_file), str(output_file), "--overwrite"])

    assert result == 0
    assert output_file.read_text(encoding="utf-8").startswith("App ID,Log count")

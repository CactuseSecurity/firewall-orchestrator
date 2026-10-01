import csv
import json
from pathlib import Path
from typing import cast

import pytest

from scripts.customizing import generate_app_and_log_data as generator


def get_application_address(owner: dict[str, object]) -> tuple[str, str]:
    """Return an application's ID and the address of its generated server."""
    application_id: object = owner["app_id_external"]
    server_values: object = owner["app_servers"]
    assert isinstance(application_id, str)
    assert isinstance(server_values, list)
    assert server_values
    servers: list[object] = cast("list[object]", server_values)
    server_value: object = servers[0]
    assert isinstance(server_value, dict)
    server: dict[str, object] = cast("dict[str, object]", server_value)
    address: object = server["ip"]
    assert isinstance(address, str)
    return application_id, address


def test_main_generates_logs_matching_each_generated_application(tmp_path: Path) -> None:
    app_data_file: Path = tmp_path / "app-data.json"
    log_data_file: Path = tmp_path / "log-data.csv"

    result: int = generator.main(["3", str(app_data_file), str(log_data_file), "--log-format", "csv"])

    owners: list[dict[str, object]] = cast(
        "list[dict[str, object]]", json.loads(app_data_file.read_text(encoding="utf-8"))["owners"]
    )
    with log_data_file.open(newline="", encoding="utf-8") as file_handle:
        rows: list[dict[str, str]] = list(csv.DictReader(file_handle))
    application_addresses: dict[str, str] = dict(get_application_address(owner) for owner in owners)

    assert result == 0
    assert [row["App ID"] for row in rows] == ["APP-000001", "APP-000002", "APP-000003"]
    assert [row["Dst IP"] for row in rows] == ["10.0.0.1", "10.0.0.2", "10.0.0.3"]
    assert all(application_addresses[row["App ID"]] == row["Dst IP"] for row in rows)
    assert len({row["Src IP"] for row in rows}) == 3


def test_main_distributes_additional_logs_only_across_generated_applications(tmp_path: Path) -> None:
    app_data_file: Path = tmp_path / "app-data.json"
    log_data_file: Path = tmp_path / "log-data.csv"

    result: int = generator.main(
        ["2", str(app_data_file), str(log_data_file), "--log-count", "5", "--log-format", "csv"]
    )

    with log_data_file.open(newline="", encoding="utf-8") as file_handle:
        rows: list[dict[str, str]] = list(csv.DictReader(file_handle))
    assert result == 0
    assert [row["App ID"] for row in rows] == ["APP-000001", "APP-000002", "APP-000001", "APP-000002", "APP-000001"]
    assert [row["Dst IP"] for row in rows] == ["10.0.0.1", "10.0.0.2", "10.0.0.1", "10.0.0.2", "10.0.0.1"]


def test_main_generates_json_logs_by_default_matching_generated_applications(tmp_path: Path) -> None:
    app_data_file: Path = tmp_path / "app-data.json"
    log_data_file: Path = tmp_path / "log-data.json"

    result: int = generator.main(["2", str(app_data_file), str(log_data_file)])

    log_data: dict[str, list[dict[str, object]]] = json.loads(log_data_file.read_text(encoding="utf-8"))
    logs: list[dict[str, object]] = log_data["logs"]
    assert result == 0
    assert [log["app_id"] for log in logs] == ["APP-000001", "APP-000002"]
    assert [log["destination"] for log in logs] == ["10.0.0.1", "10.0.0.2"]
    assert all(log["log_count"] == 1 for log in logs)
    assert all(log["protocol"] == 6 and log["port"] == 443 and log["action"] == "accept" for log in logs)


def test_main_preserves_both_existing_output_files(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    app_data_file: Path = tmp_path / "app-data.json"
    log_data_file: Path = tmp_path / "log-data.csv"
    app_data_file.write_text("keep-app-data", encoding="utf-8")
    log_data_file.write_text("keep-log-data", encoding="utf-8")

    result: int = generator.main(["1", str(app_data_file), str(log_data_file)])

    assert result == 1
    assert app_data_file.read_text(encoding="utf-8") == "keep-app-data"
    assert log_data_file.read_text(encoding="utf-8") == "keep-log-data"
    assert "refusing to overwrite" in capsys.readouterr().err


def test_main_rejects_using_one_file_for_both_outputs(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    output_file: Path = tmp_path / "output.json"

    result: int = generator.main(["1", str(output_file), str(output_file)])

    assert result == 1
    assert not output_file.exists()
    assert "must be different" in capsys.readouterr().err

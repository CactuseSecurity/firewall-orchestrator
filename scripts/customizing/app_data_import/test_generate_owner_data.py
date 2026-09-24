import ipaddress
import json
from pathlib import Path
from typing import cast

import pytest

from scripts.customizing.app_data_import import generate_owner_data as generator
from scripts.customizing.log_data_import import generate_log_data as log_generator


def test_command_line_annotations_are_postponed_for_python_39() -> None:
    """Keep CLI annotations from being evaluated while importing on Python 3.9."""
    parse_arguments_hints: dict[str, object] = generator.parse_arguments.__annotations__
    main_hints: dict[str, object] = generator.main.__annotations__

    assert parse_arguments_hints["argv"] == "list[str] | None"
    assert main_hints["argv"] == "list[str] | None"


def test_main_generates_owner_data_usable_by_the_log_data_generator(tmp_path: Path) -> None:
    output_file: Path = tmp_path / "owners.json"

    result: int = generator.main(["3", str(output_file)])

    owner_data: dict[str, list[dict[str, object]]] = json.loads(output_file.read_text(encoding="utf-8"))
    owners: list[dict[str, object]] = owner_data["owners"]
    applications: list[log_generator.Application] = log_generator.load_applications(output_file)
    assert result == 0
    assert [owner["app_id_external"] for owner in owners] == ["APP-000001", "APP-000002", "APP-000003"]
    assert [application.app_id for application in applications] == ["APP-000001", "APP-000002", "APP-000003"]
    assert [str(application.destination) for application in applications] == ["10.0.0.1", "10.0.0.2", "10.0.0.3"]
    assert all(owner["owner_lifecycle_state"] == "active" for owner in owners)


def test_generate_owner_data_creates_unique_owner_and_server_identifiers() -> None:
    owner_data: dict[str, list[dict[str, object]]] = generator.generate_owner_data(2)
    owners: list[dict[str, object]] = owner_data["owners"]
    first_server: dict[str, object] = get_first_server(owners[0])
    second_server: dict[str, object] = get_first_server(owners[1])

    assert owners[0]["name"] == "Generated Application 1"
    assert first_server["app_id_external"] == owners[0]["app_id_external"]
    assert first_server["ip"] == first_server["ip_end"]
    assert ipaddress.ip_address(str(first_server["ip"])) != ipaddress.ip_address(str(second_server["ip"]))


def get_first_server(owner: dict[str, object]) -> dict[str, object]:
    """Return the generated server of an owner from the test data."""
    server_values: object = owner["app_servers"]
    assert isinstance(server_values, list)
    servers: list[object] = cast("list[object]", server_values)
    assert len(servers) == 1
    return cast("dict[str, object]", servers[0])


@pytest.mark.parametrize("owner_count", [0, generator.MAX_GENERATED_OWNERS + 1])
def test_generate_owner_data_rejects_unsupported_owner_counts(owner_count: int) -> None:
    with pytest.raises(ValueError, match=r"between 1"):
        generator.generate_owner_data(owner_count)


def test_main_preserves_an_existing_file_unless_overwrite_was_requested(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    output_file: Path = tmp_path / "owners.json"
    output_file.write_text("keep", encoding="utf-8")

    result: int = generator.main(["1", str(output_file)])

    assert result == 1
    assert output_file.read_text(encoding="utf-8") == "keep"
    assert "refusing to overwrite" in capsys.readouterr().err


def test_main_overwrites_an_existing_file_when_explicitly_requested(tmp_path: Path) -> None:
    output_file: Path = tmp_path / "owners.json"
    output_file.write_text("replace", encoding="utf-8")

    result: int = generator.main(["1", str(output_file), "--overwrite"])

    assert result == 0
    assert json.loads(output_file.read_text(encoding="utf-8"))["owners"][0]["app_id_external"] == "APP-000001"

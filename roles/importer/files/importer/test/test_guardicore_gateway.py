import json
from pathlib import Path
from typing import Any

import pytest
from fw_modules.guardicoremanagementREST.gc_gateway import ensure_policy_gateway
from fwo_exceptions import FwoImporterError
from model_controllers.import_state_controller import ImportStateController
from pytest_mock import MockerFixture


def test_registers_missing_gateway(import_state_controller: ImportStateController, mocker: MockerFixture) -> None:
    api_call = mocker.patch.object(
        import_state_controller.api_call,
        "call",
        side_effect=[
            {"data": {"stm_dev_typ": [{"dev_typ_id": 34}]}},
            {"data": {"insert_device": {"returning": [{"newId": 42}]}}},
        ],
    )

    ensure_policy_gateway(import_state_controller)
    ensure_policy_gateway(import_state_controller)

    assert api_call.call_count == 2
    assert api_call.call_args.kwargs["query_variables"] == {
        "name": "Mock Management",
        "uid": "Mock Management",
        "devTypeId": 34,
        "managementId": 3,
    }
    assert import_state_controller.state.lookup_gateway_id("Mock Management") == 42


def test_reuses_registered_gateway(import_state_controller: ImportStateController, mocker: MockerFixture) -> None:
    import_state_controller.state.gateway_map = {3: {"existing-uid": 42}}
    api_call = mocker.patch.object(import_state_controller.api_call, "call")

    ensure_policy_gateway(import_state_controller)

    api_call.assert_not_called()
    assert import_state_controller.state.gateway_map == {3: {"existing-uid": 42}}


def test_registration_permission_is_guardicore_only() -> None:
    repo_root = next(parent for parent in Path(__file__).resolve().parents if (parent / "roles/api").is_dir())
    metadata = json.loads((repo_root / "roles/api/files/replace_metadata.json").read_text(encoding="utf-8"))
    tables = metadata["args"]["metadata"]["sources"][0]["tables"]
    device = next(table for table in tables if table["table"] == {"name": "device", "schema": "public"})
    permission = next(entry["permission"] for entry in device["insert_permissions"] if entry["role"] == "importer")

    assert set(permission["columns"]) == {"dev_name", "dev_uid", "dev_typ_id", "mgm_id"}
    assert permission["set"] == {
        "do_not_import": False,
        "hide_in_gui": False,
        "dev_comment": "Dummy gateway grouping host-based Guardicore policies.",
    }
    assert permission["check"] == {
        "stm_dev_typ": {"dev_typ_name": {"_eq": "Guardicore Gateway"}, "dev_typ_version": {"_eq": "REST"}},
        "management": {
            "stm_dev_typ": {"dev_typ_name": {"_eq": "Guardicore Management"}, "dev_typ_version": {"_eq": "REST"}},
        },
    }
    assert not any(entry["role"] == "importer" for entry in device.get("update_permissions", []))
    assert not any(entry["role"] == "importer" for entry in device.get("delete_permissions", []))


@pytest.mark.parametrize("response", [{"data": {"stm_dev_typ": []}}, {"errors": [{"message": "failed"}]}])
def test_rejects_missing_device_type(
    response: dict[str, Any], import_state_controller: ImportStateController, mocker: MockerFixture
) -> None:
    api_call = mocker.patch.object(import_state_controller.api_call, "call", return_value=response)

    with pytest.raises(FwoImporterError, match="type not found"):
        ensure_policy_gateway(import_state_controller)

    api_call.assert_called_once()
    assert import_state_controller.state.gateway_map == {}


@pytest.mark.parametrize(
    "response",
    [
        {"errors": [{"message": "failed"}]},
        {"data": {"insert_device": {"returning": []}}},
        {"data": {"insert_device": {"returning": [{"newId": None}]}}},
    ],
)
def test_rejects_failed_registration(
    response: dict[str, Any], import_state_controller: ImportStateController, mocker: MockerFixture
) -> None:
    mocker.patch.object(
        import_state_controller.api_call,
        "call",
        side_effect=[{"data": {"stm_dev_typ": [{"dev_typ_id": 34}]}}, response],
    )

    with pytest.raises(FwoImporterError, match="Cannot register Guardicore policy gateway"):
        ensure_policy_gateway(import_state_controller)

    assert import_state_controller.state.gateway_map == {}

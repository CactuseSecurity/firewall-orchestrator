from __future__ import annotations

from typing import TYPE_CHECKING, Any
from unittest.mock import MagicMock

import pytest
from common import is_responsible_importer
from fwo_exceptions import FwoApiFailureError
from model_controllers.management_controller import ManagementController

if TYPE_CHECKING:
    from pytest_mock import MockerFixture

k_mgm_id: int = 100
k_device_type_id: int = 9


def _node(node_type: str, host_name: Any | None) -> dict[str, Any]:
    values: list[dict[str, Any]] = [] if host_name is None else [{"config_value": host_name}]
    return {"node_type": node_type, "values": values}


def _api_returning(nodes: list[dict[str, Any]] | None) -> MagicMock:
    api = MagicMock()
    api.call.return_value = {"data": {"device_specific_config_node": nodes}} if nodes is not None else {"errors": []}
    return api


@pytest.fixture(autouse=True)
def query_code(mocker: MockerFixture) -> None:
    mocker.patch("fwo_api.FwoApi.get_graphql_code", return_value="query")


@pytest.mark.parametrize(
    ("nodes", "expected"),
    [
        ([], ""),
        ([_node("global", "importer-a")], "importer-a"),
        ([_node("global", "importer-a"), _node("device_type", "importer-b")], "importer-b"),
        (
            [_node("global", "importer-a"), _node("device_type", "importer-b"), _node("management", " importer-c ")],
            "importer-c",
        ),
        ([_node("management", None), _node("device_type", None), _node("global", "importer-a")], "importer-a"),
        ([_node("global", 42)], ""),
    ],
)
def test_most_specific_importer_host_name_wins(nodes: list[dict[str, Any]], expected: str) -> None:
    api = _api_returning(nodes)

    assert ManagementController.get_importer_host_name(api, k_mgm_id, k_device_type_id) == expected
    assert api.call.call_args.kwargs["query_variables"] == {
        "deviceTypeKey": str(k_device_type_id),
        "managementKey": str(k_mgm_id),
    }


def test_missing_settings_data_fails() -> None:
    with pytest.raises(FwoApiFailureError):
        ManagementController.get_importer_host_name(_api_returning(None), k_mgm_id, k_device_type_id)


def test_get_mgm_details_adds_effective_importer_host_name(
    mocker: MockerFixture, management_controller: ManagementController
) -> None:
    mocker.patch.object(ManagementController, "decrypt_additional_credentials")
    api = MagicMock()
    api.call.side_effect = [
        {
            "data": {
                "management": [{"hostname": "https://example.invalid/config", "deviceType": {"id": k_device_type_id}}]
            }
        },
        {"data": {"device_specific_config_node": [_node("global", "importer-a")]}},
    ]

    management = management_controller.get_mgm_details(api, k_mgm_id)

    assert management["importerHostname"] == "importer-a"


@pytest.mark.parametrize(("host_name", "expected"), [("", True), ("other-importer-host", False)])
def test_is_responsible_importer(host_name: str, expected: bool) -> None:
    assert is_responsible_importer(host_name) is expected


def test_is_responsible_importer_for_own_host(mocker: MockerFixture) -> None:
    mocker.patch("common.gethostname", return_value="importer-a")

    assert is_responsible_importer("importer-a") is True

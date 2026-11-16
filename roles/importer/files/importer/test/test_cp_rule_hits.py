from __future__ import annotations

from contextlib import contextmanager
from copy import deepcopy
from typing import TYPE_CHECKING, Any
from unittest.mock import MagicMock

import pytest
from fw_modules.checkpointR8x import cp_rule_hits
from fw_modules.checkpointR8x.fwcommon import CheckpointR8xCommon
from models.import_state import ImportState

if TYPE_CHECKING:
    from collections.abc import Generator

    from model_controllers.import_state_controller import ImportStateController
    from model_controllers.management_controller import ManagementController
    from pytest_mock import MockerFixture

GATEWAY_UID = "gw-uid"
OTHER_GATEWAY_UID = "gw-other"
POLICY_NAME = "Standard"
LAYER_UID = "layer-1"
HIT = "2026-11-06T10:15+0200"
NORMALIZED_HIT = "2026-11-06T08:15:00+00:00"
API_FETCH_SIZE = 150


def _import_state(manager: ManagementController) -> ImportState:
    import_state = ImportState()
    import_state.mgm_details = manager
    import_state.fwo_config = MagicMock(api_fetch_size=API_FETCH_SIZE)
    return import_state


def _hit(iso_date: str) -> dict[str, Any]:
    return {"hits": {"last-date": {"iso-8601": iso_date}, "value": 3}}


def _native_rulebases() -> list[dict[str, Any]]:
    return [
        {
            "uid": LAYER_UID,
            "chunks": [
                {
                    "rulebase": [
                        {"type": "access-rule", "uid": "r1", **_hit(HIT)},
                        {"type": "access-rule", "uid": "r2", "hits": {"value": 0}},
                        {
                            "type": "access-section",
                            "uid": "s1",
                            "rulebase": [
                                {"type": "access-rule", "uid": "r3", **_hit("2026-11-01T00:00+0000")},
                                {"type": "place-holder", "uid": "p1"},
                            ],
                        },
                    ]
                },
                {"total": 0},
            ],
        }
    ]


def test_collect_last_hits_reads_rules_in_chunks_and_sections() -> None:
    assert cp_rule_hits.collect_last_hits(_native_rulebases()) == {
        "r1": HIT,
        "r3": "2026-11-01T00:00+0000",
    }


@pytest.mark.parametrize("native_rule", [{}, {"hits": {}}, {"hits": {"last-date": "never"}}])
def test_rule_without_last_date_has_no_last_hit(native_rule: dict[str, Any]) -> None:
    assert cp_rule_hits.get_last_hit_of_rule(native_rule) is None


def test_too_deep_sections_are_ignored(mocker: MockerFixture) -> None:
    warning = mocker.patch("fw_modules.checkpointR8x.cp_rule_hits.FWOLogger.warning")
    entries: list[dict[str, Any]] = [{"type": "access-rule", "uid": "deep-rule", **_hit(HIT)}]
    for _ in range(cp_rule_hits.MAX_SECTION_DEPTH + 2):
        entries = [{"type": "access-section", "rulebase": entries}]

    assert cp_rule_hits.collect_last_hits([{"chunks": [{"rulebase": entries}]}]) == {}
    warning.assert_called_once()


def test_rule_hits_params_request_the_hits_of_one_gateway(management_controller: ManagementController) -> None:
    params = cp_rule_hits.get_rule_hits_params(_import_state(management_controller), GATEWAY_UID)

    assert params["show-hits"] is True
    assert params["hits-settings"] == {"target": GATEWAY_UID}


@pytest.fixture
def cp_manager(management_controller: ManagementController) -> ManagementController:
    manager = deepcopy(management_controller)
    manager.device_type_name = "Check Point"
    manager.devices = [
        {"uid": GATEWAY_UID, "name": "gw", "importDisabled": False},
        {"uid": OTHER_GATEWAY_UID, "name": "gw-other", "importDisabled": False},
        {"uid": "gw-disabled", "name": "gw-disabled", "importDisabled": True},
    ]
    return manager


@pytest.fixture
def cp_api(mocker: MockerFixture) -> dict[str, Any]:
    @contextmanager
    def api_session(_manager: ManagementController) -> Generator[str, None, None]:
        yield "sid"

    def fill_policy_structure(*_args: Any, policy_structure: list[dict[str, Any]], **_kwargs: Any) -> None:
        policy_structure.append(
            {
                "name": POLICY_NAME,
                "uid": "policy-uid",
                "targets": [{"uid": "all"}],
                "access-layers": [{"uid": LAYER_UID, "domain": "mock-domain-uid"}],
            }
        )

    def fetch_layers(*args: Any, **_kwargs: Any) -> list[str]:
        native_rulebases: dict[str, Any] = args[4]
        native_rulebases["rulebases"].extend(_native_rulebases())
        return []

    gateways: list[dict[str, Any]] = [
        {"uid": GATEWAY_UID, "policy": {"access-policy-name": POLICY_NAME}},
        {"uid": OTHER_GATEWAY_UID, "policy": {}},
    ]
    return {
        "session": mocker.patch("fw_modules.checkpointR8x.cp_rule_hits.cp_getter.api_session", side_effect=api_session),
        "policy": mocker.patch(
            "fw_modules.checkpointR8x.cp_rule_hits.cp_getter.get_policy_structure", side_effect=fill_policy_structure
        ),
        "gateways": mocker.patch(
            "fw_modules.checkpointR8x.cp_rule_hits.cp_getter.get_gateways_and_servers", return_value=gateways
        ),
        "layers": mocker.patch(
            "fw_modules.checkpointR8x.cp_rule_hits.add_ordered_layers_to_native_config", side_effect=fetch_layers
        ),
    }


def test_rule_hits_are_read_per_gateway(cp_manager: ManagementController, cp_api: dict[str, Any]) -> None:
    import_state = _import_state(cp_manager)

    rule_hits = cp_rule_hits.get_rule_hits(import_state)

    assert {(hit.rule_uid, hit.gateway_uid) for hit in rule_hits} == {("r1", GATEWAY_UID), ("r3", GATEWAY_UID)}
    assert all(hit.rule_mgm_id == cp_manager.mgm_id == hit.gateway_mgm_id for hit in rule_hits)
    assert next(hit for hit in rule_hits if hit.rule_uid == "r1").last_hit == NORMALIZED_HIT
    # only the gateway with a policy is fetched, with its own hits target
    cp_api["layers"].assert_called_once()
    assert cp_api["layers"].call_args.args[1]["hits-settings"] == {"target": GATEWAY_UID}


def test_managers_without_gateways_or_disabled_are_skipped(
    cp_manager: ManagementController, cp_api: dict[str, Any]
) -> None:
    import_state = _import_state(cp_manager)
    cp_manager.devices = []

    assert cp_rule_hits.get_rule_hits(import_state) == []

    cp_manager.devices = [{"uid": GATEWAY_UID, "name": "gw", "importDisabled": False}]
    cp_manager.import_disabled = True

    assert cp_rule_hits.get_rule_hits(import_state) == []
    cp_api["session"].assert_not_called()


def test_checkpoint_module_supports_rule_hits(
    import_state_controller: ImportStateController, mocker: MockerFixture
) -> None:
    get_rule_hits = mocker.patch("fw_modules.checkpointR8x.cp_rule_hits.get_rule_hits", return_value=[])
    module = CheckpointR8xCommon()

    assert module.supports_rule_hits() is True
    assert module.get_rule_hits(import_state_controller) == []
    get_rule_hits.assert_called_once_with(import_state_controller.state)

from __future__ import annotations

from types import SimpleNamespace
from typing import TYPE_CHECKING, Any, cast
from unittest.mock import MagicMock, call, patch

import pytest
from fw_modules.checkpointR8x import cp_getter, fwcommon
from fwo_exceptions import FwApiError

if TYPE_CHECKING:
    from model_controllers.management_controller import ManagementController
    from models.import_state import ImportState

CP_URL = "https://mgm.invalid/web_api/"
GLOBAL_ASSIGNMENTS: list[dict[str, Any]] = [{"global-domain": {"uid": "global-domain-uid"}}]


def manager_mock(is_super_manager: bool = False) -> ManagementController:
    mock = SimpleNamespace(
        is_super_manager=is_super_manager,
        import_disabled=False,
        domain_uid="",
        build_fw_api_string=lambda: CP_URL,
    )
    return cast("ManagementController", mock)


def import_state_mock() -> ImportState:
    mock = SimpleNamespace(
        fwo_config=SimpleNamespace(api_fetch_size=100),
        mgm_details=manager_mock(is_super_manager=True),
        force_import=False,
    )
    return cast("ImportState", mock)


def logged_out_sessions(logout: MagicMock) -> list[str]:
    return [logout_call.args[1] for logout_call in logout.call_args_list]


def test_objects_session_is_logged_out_after_an_error() -> None:
    with (
        patch.object(cp_getter, "login", return_value="sid-objects"),
        patch.object(cp_getter, "logout") as logout,
        patch.object(fwcommon, "get_objects_per_type", side_effect=FwApiError("transport")),
        pytest.raises(FwApiError),
    ):
        fwcommon.get_objects_per_domain(manager_mock(), {"objects": []}, ["hosts"], {})

    assert logout.call_args_list == [call(CP_URL, "sid-objects")]


def test_super_manager_keeps_global_session_open_for_sub_managers() -> None:
    with (
        patch.object(cp_getter, "login", side_effect=["sid-mds", "sid-global"]),
        patch.object(cp_getter, "logout") as logout,
        patch.object(cp_getter, "get_global_assignments", return_value=GLOBAL_ASSIGNMENTS),
        patch.object(cp_getter, "get_policy_structure"),
    ):
        result = fwcommon.handle_super_manager(manager_mock(is_super_manager=True), CP_URL, {})

    assert result[3] == "sid-global"
    assert logged_out_sessions(logout) == ["sid-mds"]


def test_super_manager_logs_out_both_sessions_after_an_error() -> None:
    with (
        patch.object(cp_getter, "login", side_effect=["sid-mds", "sid-global"]),
        patch.object(cp_getter, "logout") as logout,
        patch.object(cp_getter, "get_global_assignments", return_value=GLOBAL_ASSIGNMENTS),
        patch.object(cp_getter, "get_policy_structure", side_effect=FwApiError("transport")),
        pytest.raises(FwApiError),
    ):
        fwcommon.handle_super_manager(manager_mock(is_super_manager=True), CP_URL, {})

    assert logged_out_sessions(logout) == ["sid-mds", "sid-global"]


def test_rules_sessions_are_logged_out_after_an_error() -> None:
    with (
        patch.object(fwcommon, "create_ordered_manager_list", return_value=[manager_mock(is_super_manager=True)]),
        patch.object(fwcommon, "handle_super_manager", return_value=([], None, None, "sid-global")),
        patch.object(cp_getter, "login", return_value="sid-manager"),
        patch.object(cp_getter, "logout") as logout,
        patch.object(cp_getter, "get_policy_structure", side_effect=FwApiError("transport")),
        pytest.raises(FwApiError),
    ):
        fwcommon.get_rules({"domains": [{}]}, import_state_mock())

    assert logged_out_sessions(logout) == ["sid-manager", "sid-global"]

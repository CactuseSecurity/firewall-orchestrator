from __future__ import annotations

import json
import unittest.mock
from typing import TYPE_CHECKING, Any

import import_mgm
import pytest
from model_controllers.import_state_controller import ImportStateController

if TYPE_CHECKING:
    from collections.abc import Callable

    from pytest_mock import MockerFixture

MGM_ID = 1
FWO_CONFIG: dict[str, Any] = {
    "fwo_api_base_url": "https://fwo.example/api",
    "fwo_major_version": 9,
    "user_management_api_base_url": "https://fwo.example/",
}


class _FakeServiceProvider:
    def get_fwo_config(self) -> dict[str, Any]:
        return FWO_CONFIG


@pytest.fixture
def patch_login_and_config(mocker: MockerFixture) -> Callable[[str], None]:
    """
    Patches everything main() needs before it gets to parsing the login response:
    config lookup, the importer-password file read, and the login call itself.
    Returns a function the test uses to supply the raw login JSON.
    """

    def _apply(json_raw: str) -> None:
        mocker.patch.object(import_mgm, "init_service_provider", return_value=_FakeServiceProvider())
        mocker.patch.object(import_mgm, "open", unittest.mock.mock_open(read_data="secret-pwd"), create=True)
        mocker.patch.object(import_mgm, "get_fwo_jwt", return_value=json_raw)

    return _apply


class TestMainRequiresRefreshToken:
    """
    import_mgm.main() now requires a RefreshToken alongside the AccessToken - covers the guard
    added so a login response without one can't produce an FwoApi that can never recover from
    an expired JWT.
    """

    def test_bails_out_when_refresh_token_is_absent_from_login_response(
        self, mocker: MockerFixture, patch_login_and_config: Callable[[str], None]
    ) -> None:
        patch_login_and_config(json.dumps({"AccessToken": "jwt-value"}))
        mock_fwo_api = mocker.patch.object(import_mgm, "FwoApi")
        mock_logger_error = mocker.patch("fwo_log.FWOLogger.error")

        import_mgm.main(MGM_ID)

        mock_fwo_api.assert_not_called()
        assert mock_logger_error.call_args[0][0] == "JWT could not be parsed"

    def test_bails_out_when_refresh_token_is_explicitly_null(
        self, mocker: MockerFixture, patch_login_and_config: Callable[[str], None]
    ) -> None:
        patch_login_and_config(json.dumps({"AccessToken": "jwt-value", "RefreshToken": None}))
        mock_fwo_api = mocker.patch.object(import_mgm, "FwoApi")

        import_mgm.main(MGM_ID)

        mock_fwo_api.assert_not_called()

    def test_bails_out_when_access_token_is_explicitly_null(
        self, mocker: MockerFixture, patch_login_and_config: Callable[[str], None]
    ) -> None:
        patch_login_and_config(json.dumps({"AccessToken": None, "RefreshToken": "refresh-value"}))
        mock_fwo_api = mocker.patch.object(import_mgm, "FwoApi")

        import_mgm.main(MGM_ID)

        mock_fwo_api.assert_not_called()

    def test_constructs_fwo_api_with_both_tokens_when_present(
        self, mocker: MockerFixture, patch_login_and_config: Callable[[str], None]
    ) -> None:
        patch_login_and_config(json.dumps({"AccessToken": "jwt-value", "RefreshToken": "refresh-value"}))
        mock_fwo_api = mocker.patch.object(import_mgm, "FwoApi")
        mocker.patch.object(import_mgm, "FwoApiCall")
        mocker.patch.object(ImportStateController, "initialize_import")
        mocker.patch.object(import_mgm, "register_global_state")
        mock_import_management = mocker.patch.object(import_mgm, "import_management")

        import_mgm.main(MGM_ID)

        mock_fwo_api.assert_called_once_with(FWO_CONFIG["fwo_api_base_url"], "jwt-value", "refresh-value")
        mock_import_management.assert_called_once()

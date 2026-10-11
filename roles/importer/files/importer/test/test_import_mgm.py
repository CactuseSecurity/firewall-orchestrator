from __future__ import annotations

from typing import TYPE_CHECKING, Any

import fwo_globals
import import_mgm
import pytest
from fwo_exceptions import FwoApiLoginFailedError

if TYPE_CHECKING:
    from collections.abc import Iterator
    from unittest.mock import MagicMock

    from pytest_mock import MockerFixture

MGM_ID = 1


@pytest.fixture(autouse=True)
def restore_certificate_globals() -> Iterator[None]:
    saved = (fwo_globals.verify_certs, fwo_globals.suppress_cert_warnings, fwo_globals.cli_verify_certs)
    yield
    fwo_globals.verify_certs, fwo_globals.suppress_cert_warnings, fwo_globals.cli_verify_certs = saved


@pytest.fixture
def import_mocks(mocker: MockerFixture) -> dict[str, MagicMock]:
    """
    Patches the collaborators main() wires together, so a test only sees what is handed to them.
    """
    return {
        "global_state": mocker.patch.object(import_mgm, "GlobalState"),
        "import_state": mocker.patch.object(import_mgm, "ImportState"),
        "import_management": mocker.patch.object(import_mgm, "import_management"),
    }


class TestMain:
    def test_imports_management_with_global_and_import_state(self, import_mocks: dict[str, Any]) -> None:
        import_mgm.main(MGM_ID, file="config.json", limit=200, clear_management_data=True, force=True)

        global_state = import_mocks["global_state"].return_value
        import_state = import_mocks["import_state"].return_value
        assert import_mocks["global_state"].call_args.kwargs["force"] is True
        assert import_mocks["global_state"].call_args.kwargs["clear"] is True
        import_mocks["import_state"].assert_called_once_with(
            fwo_api=global_state.fwo_api,
            fwo_api_call=global_state.fwo_api_call,
            mgm_id=MGM_ID,
            input_file="config.json",
        )
        import_mocks["import_management"].assert_called_once_with(global_state, import_state)

    def test_hands_command_line_settings_to_config(self, import_mocks: dict[str, Any]) -> None:
        import_mgm.main(MGM_ID, limit=200, suppress_consistency_check=True)

        import_mocks["global_state"].return_value.fwo_config_controller.update_settings.assert_called_once_with(
            suppress_consistency_check=True,
            api_fetch_size=200,
        )

    @pytest.mark.usefixtures("import_mocks")
    @pytest.mark.parametrize("verify_flag", [True, False])
    def test_command_line_verify_flag_is_remembered(self, verify_flag: bool) -> None:
        import_mgm.main(MGM_ID, verify_certificates_default=verify_flag)

        # the command line can only force certificate checking on, GlobalState combines it with the config value
        assert fwo_globals.cli_verify_certs is verify_flag

    def test_login_failure_stops_before_importing(self, import_mocks: dict[str, Any]) -> None:
        import_mocks["global_state"].side_effect = FwoApiLoginFailedError("login failed")

        with pytest.raises(FwoApiLoginFailedError):
            import_mgm.main(MGM_ID)

        import_mocks["import_management"].assert_not_called()

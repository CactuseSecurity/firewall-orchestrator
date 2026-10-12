import signal
from typing import Any, Protocol, cast
from unittest.mock import MagicMock

import fwo_globals
import pytest
from common import handle_shutdown_exception, import_management, roll_back_exception_handler
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoApiServiceUnavailableError
from fwo_signalling import handle_shutdown_signal
from model_controllers.import_state_controller import ImportStateController
from pytest_mock import MockerFixture
from services.service_provider import ServiceProvider


class MockAssertions(Protocol):
    def assert_not_called(self) -> None: ...

    def assert_called_once(self) -> None: ...


class TestImportManagementServiceUnavailable:
    def test_middleware_unavailable_rolls_back_and_re_raises(
        self,
        mocker: MockerFixture,
        service_provider: ServiceProvider,
        import_state_controller: ImportStateController,
        api_call: FwoApiCall,
    ) -> None:
        # Arrange - the service_provider fixture registers the mocked global state that
        # import_management() reads via ServiceProvider() internally
        assert service_provider is ServiceProvider()
        import_state_controller.state.import_id = 7
        error = FwoApiServiceUnavailableError("FWO Middleware API HTTP error 503 (middleware died?)")
        mocker.patch("common._import_management", side_effect=error)
        mock_rollback = cast("MockAssertions", mocker.patch("common.roll_back_exception_handler"))

        # Act & Assert
        with pytest.raises(FwoApiServiceUnavailableError):
            import_management(
                mgm_id=1,
                api_call=api_call,
                limit=100,
                clear_management_data=False,
            )

        # Assert - rolled back once, and import completion is still reported (finally block)
        mock_rollback.assert_called_once()
        cast("MockAssertions", api_call.complete_import).assert_called_once()


class TestRollBackDefersShutdown:
    def test_signal_during_rollback_does_not_abort_it(
        self,
        mocker: MockerFixture,
        monkeypatch: pytest.MonkeyPatch,
        import_state_controller: ImportStateController,
    ) -> None:
        monkeypatch.setattr(fwo_globals, "shutdown_requested", False)
        monkeypatch.setattr(fwo_globals, "shutdown_deferral_active", False)

        def rollback_hit_by_sigint(**_kwargs: Any) -> None:
            handle_shutdown_signal(signal.SIGINT, None)

        rollback = mocker.patch("common.FwConfigImportRollback").return_value
        rollback.rollback_current_import.side_effect = rollback_hit_by_sigint
        mock_delete_import = mocker.patch.object(import_state_controller, "delete_import")

        roll_back_exception_handler(import_state_controller, config_importer=MagicMock(), exc=ValueError("boom"))

        mock_delete_import.assert_called_once()
        assert fwo_globals.shutdown_requested is True


class TestShutdownWithoutRollbackKeepsApiRetries:
    def test_lock_is_removed_inside_a_deferral(
        self,
        mocker: MockerFixture,
        monkeypatch: pytest.MonkeyPatch,
        import_state_controller: ImportStateController,
    ) -> None:
        monkeypatch.setattr(fwo_globals, "shutdown_requested", True)
        monkeypatch.setattr(fwo_globals, "shutdown_deferral_active", False)
        import_state_controller.state.import_id = 7
        import_state_controller.state.rollback_required = False
        deferral_active_during_delete: list[bool] = []
        mocker.patch.object(
            import_state_controller,
            "delete_import",
            side_effect=lambda: deferral_active_during_delete.append(fwo_globals.shutdown_deferral_active),
        )

        handle_shutdown_exception(import_state_controller)

        assert deferral_active_during_delete == [True]
        assert fwo_globals.shutdown_deferral_active is False

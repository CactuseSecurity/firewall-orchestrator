from typing import Protocol, cast

import pytest
from common import import_management
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoApiServiceUnavailableError
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
                ssl_verification=True,
                limit=100,
                clear_management_data=False,
                suppress_cert_warnings=False,
            )

        # Assert - rolled back once, and import completion is still reported (finally block)
        mock_rollback.assert_called_once()
        cast("MockAssertions", api_call.complete_import).assert_called_once()

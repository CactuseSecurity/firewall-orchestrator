import pytest
from common import get_config_uri, set_filename
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import ManagementController
from models.import_state import ImportState


@pytest.fixture
def import_state_controller(
    management_state: ManagementController,
    api_call: FwoApiCall,
    api_connection: FwoApi,
) -> ImportStateController:
    import_state = ImportState()
    import_state.mgm_details = management_state
    controller = ImportStateController(state=import_state, api_call=api_call)
    controller.state = import_state
    controller.api_call = api_call
    controller.api_connection = api_connection
    return controller


class TestCommonConfigUri:
    def test_get_config_uri_prefers_hostname_uri(
        self,
        import_state_controller: ImportStateController,
    ):
        import_state = import_state_controller
        import_state.state.mgm_details.hostname = "https://example.com/config.json"
        import_state.state.mgm_details.domain_name = "https://example.com/ignored.json"

        assert get_config_uri(import_state) == "https://example.com/config.json"

    def test_get_config_uri_falls_back_to_config_path_uri(
        self,
        import_state_controller: ImportStateController,
    ):
        import_state = import_state_controller
        import_state.state.mgm_details.hostname = "fw.example.com"
        import_state.state.mgm_details.domain_name = "file:///tmp/config.json"

        assert get_config_uri(import_state) == "file:///tmp/config.json"

    def test_set_filename_uses_config_uri_when_present(
        self,
        import_state_controller: ImportStateController,
    ):
        import_state = import_state_controller
        import_state.state.mgm_details.hostname = "fw.example.com"
        import_state.state.mgm_details.domain_name = "https://example.com/config.json"

        set_filename(import_state)

        assert import_state.state.import_file_name == "https://example.com/config.json"

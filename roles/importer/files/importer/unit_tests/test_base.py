import unittest.mock

import pytest
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoImporterError
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import ManagementController
from models.fwconfig_normalized import FwConfigNormalized
from models.import_state import ImportState
from pytest_mock import MockerFixture
from services.enums import Lifetime, Services
from services.global_state import GlobalState
from services.service_provider import ServiceProvider


@pytest.fixture
def api_call(mocker: MockerFixture) -> FwoApiCall:
    fwo_api_call: FwoApiCall = unittest.mock.create_autospec(FwoApiCall)
    fwo_api_call.call = mocker.MagicMock()
    return fwo_api_call


@pytest.fixture
def fwconfig_import_gateway(
    management_controller: ManagementController,
    api_call: FwoApiCall,
) -> FwConfigImportGateway:
    import_state = ImportState()
    import_state.mgm_details = management_controller
    import_state_controller: ImportStateController = unittest.mock.create_autospec(ImportStateController)
    import_state_controller.state = import_state

    import_state_controller.api_call = api_call

    global_state = GlobalState(import_state_controller)
    global_state.normalized_config = FwConfigNormalized()
    global_state.global_normalized_config = FwConfigNormalized()
    global_state.previous_config = FwConfigNormalized()
    global_state.previous_global_config = FwConfigNormalized()

    service_provider = ServiceProvider()
    service_provider.reset()
    service_provider.register(Services.GLOBAL_STATE, lambda: global_state, Lifetime.SINGLETON)
    return FwConfigImportGateway()


@pytest.fixture
def management_controller() -> ManagementController:
    mgm_controller: ManagementController = unittest.mock.create_autospec(ManagementController)
    mgm_controller.current_mgm_id = 1
    return mgm_controller


def test_fwconfig_import_gateway_init(
    fwconfig_import_gateway: FwConfigImportGateway,
    api_call: FwoApiCall,
    mocker: MockerFixture,
):
    gateway_id = 69
    from_rule_id = 123
    from_rulebase_id = 456
    to_rulebase_id = 789
    link_type = 1

    api_call.call = unittest.mock.Mock(
        return_value={
            "data": {
                "rulebase_link": [
                    {
                        "gw_id": gateway_id,
                        "from_rule_id": from_rule_id,
                        "from_rulebase_id": from_rulebase_id,
                        "to_rulebase_id": to_rulebase_id,
                        "link_type": link_type,
                        "is_initial": True,
                        "is_global": False,
                        "is_section": False,
                        "created": 1234567890,
                        "removed": None,
                    },
                ]
            }
        }
    )

    get_graphql_code = mocker.patch.object(FwoApi, "get_graphql_code", return_value="lol")

    import_state = fwconfig_import_gateway.get_global_state().import_state.state

    import_state.gateway_map = {1: {"uid": 69}}

    fwconfig_import_gateway.update_gateway_diffs()
    get_graphql_code.assert_called_once()
    assert fwconfig_import_gateway.get_rb_link_controller().rb_links is not None
    assert len(fwconfig_import_gateway.get_rb_link_controller().rb_links) == 1
    assert fwconfig_import_gateway.get_rb_link_controller().rb_links[0].from_rule_id == from_rule_id
    assert fwconfig_import_gateway.get_rb_link_controller().rb_links[0].from_rulebase_id == from_rulebase_id
    assert fwconfig_import_gateway.get_rb_link_controller().rb_links[0].to_rulebase_id == to_rulebase_id
    assert fwconfig_import_gateway.get_rb_link_controller().rb_links[0].link_type == link_type


def test_fwconfig_import_gateway_init_no_links(
    fwconfig_import_gateway: FwConfigImportGateway,
    api_call: FwoApiCall,
    mocker: MockerFixture,
):
    api_call.call = unittest.mock.Mock(return_value={"data": {"rulebase_link": []}})

    get_graphql_code = mocker.patch.object(FwoApi, "get_graphql_code", return_value="lol")

    import_state = fwconfig_import_gateway.get_global_state().import_state.state

    import_state.gateway_map = {}
    get_graphql_code.assert_not_called()

    fwconfig_import_gateway.update_gateway_diffs()
    assert fwconfig_import_gateway.get_rb_link_controller().rb_links is not None
    assert len(fwconfig_import_gateway.get_rb_link_controller().rb_links) == 0


def test_fwconfig_import_gateway_init_no_gateway_ids(
    fwconfig_import_gateway: FwConfigImportGateway,
    api_call: FwoApiCall,
    mocker: MockerFixture,
):
    api_call.call = unittest.mock.Mock(return_value={"data": {"rulebase_link": []}})

    get_graphql_code = mocker.patch.object(FwoApi, "get_graphql_code", return_value="lol")

    import_state = fwconfig_import_gateway.get_global_state().import_state.state

    import_state.gateway_map = {1: {}}

    fwconfig_import_gateway.update_gateway_diffs()
    get_graphql_code.assert_not_called()
    assert fwconfig_import_gateway.get_rb_link_controller().rb_links is not None
    assert len(fwconfig_import_gateway.get_rb_link_controller().rb_links) == 0


def test_fwconfig_import_gateway_update_rulebase_link_diffs_no_configs(
    fwconfig_import_gateway: FwConfigImportGateway,
):
    fwconfig_import_gateway.get_global_state().normalized_config = None
    with pytest.raises(FwoImporterError) as excinfo:
        fwconfig_import_gateway.update_rulebase_link_diffs()
    assert "normalized_config is None in update_rulebase_link_diffs" in str(excinfo.value)

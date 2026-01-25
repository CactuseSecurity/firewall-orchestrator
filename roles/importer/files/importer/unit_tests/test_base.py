import unittest.mock

import pytest
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoImporterError
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.import_state_controller import ImportStateController
from pytest_mock import MockerFixture
from unit_tests.utils.test_utils import mock_get_graphql_code


def test_fwconfig_import_gateway_init(
    import_state_controller: ImportStateController,
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

    get_graphql_code = mock_get_graphql_code(mocker, "lol")

    import_state_controller.state.gateway_map = {3: {"uid": 69}}

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

    get_graphql_code = mock_get_graphql_code(mocker, "lol")

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

    get_graphql_code = mock_get_graphql_code(mocker, "lol")

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

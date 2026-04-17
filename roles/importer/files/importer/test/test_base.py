import copy
import unittest.mock

import pytest
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoImporterError
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.rulebase_link_controller import RulebaseLinkController
from pytest_mock import MockerFixture
from states.global_state import GlobalState
from states.import_state import ImportState
from states.management_state import ManagementState
from test.utils.config_builder import FwConfigBuilder
from test.utils.test_utils import mock_get_graphql_code


def test_fwconfig_import_gateway_init(
    global_state: GlobalState,
    import_state: ImportState,
    management_state: ManagementState,
    fwconfig_import_gateway: FwConfigImportGateway,
    api_call: FwoApiCall,
    rb_link_controller: RulebaseLinkController,
    fwconfig_builder: FwConfigBuilder,
):

    config, mgm_id = fwconfig_builder.build_config(
        management_state.uid2id_mapper,
        network_object_count=10,
        service_object_count=10,
        rulebase_count=3,
        rules_per_rulebase_count=10,
    )

    management_state.normalized_config = copy.deepcopy(config)
    management_state.previous_config = copy.deepcopy(config)
    import_state.mgm_details.uid = mgm_id

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

    global_state.stm_mapper.gateway_map = {import_state.mgm_details.mgm_id: {"uid": 69}}

    fwconfig_import_gateway.update_gateway_diffs(global_state, import_state, management_state, rb_link_controller)

    assert rb_link_controller.rb_links is not None
    assert len(rb_link_controller.rb_links) == 1
    assert rb_link_controller.rb_links[0].from_rule_id == from_rule_id
    assert rb_link_controller.rb_links[0].from_rulebase_id == from_rulebase_id
    assert rb_link_controller.rb_links[0].to_rulebase_id == to_rulebase_id
    assert rb_link_controller.rb_links[0].link_type == link_type


def test_fwconfig_import_gateway_init_no_links(
    global_state: GlobalState,
    import_state: ImportState,
    management_state: ManagementState,
    fwconfig_import_gateway: FwConfigImportGateway,
    api_call: FwoApiCall,
    mocker: MockerFixture,
    rb_link_controller: RulebaseLinkController,
    fwconfig_builder: FwConfigBuilder,
):
    config, _ = fwconfig_builder.build_config(
        management_state.uid2id_mapper,
        network_object_count=1,
        service_object_count=1,
        rulebase_count=1,
        rules_per_rulebase_count=1,
    )
    management_state.normalized_config = copy.deepcopy(config)
    management_state.previous_config = copy.deepcopy(config)

    api_call.call = unittest.mock.Mock(return_value={"data": {"rulebase_link": []}})

    get_graphql_code = mock_get_graphql_code(mocker, "lol")

    global_state.stm_mapper.gateway_map = {}
    get_graphql_code.assert_not_called()

    fwconfig_import_gateway.update_gateway_diffs(global_state, import_state, management_state, rb_link_controller)
    assert rb_link_controller.rb_links is not None
    assert len(rb_link_controller.rb_links) == 0


def test_fwconfig_import_gateway_init_no_gateway_ids(
    global_state: GlobalState,
    import_state: ImportState,
    management_state: ManagementState,
    fwconfig_import_gateway: FwConfigImportGateway,
    api_call: FwoApiCall,
    mocker: MockerFixture,
    rb_link_controller: RulebaseLinkController,
    fwconfig_builder: FwConfigBuilder,
):
    config, _ = fwconfig_builder.build_config(
        management_state.uid2id_mapper,
        network_object_count=1,
        service_object_count=1,
        rulebase_count=1,
        rules_per_rulebase_count=1,
    )
    management_state.normalized_config = copy.deepcopy(config)
    management_state.previous_config = copy.deepcopy(config)

    api_call.call = unittest.mock.Mock(return_value={"data": {"rulebase_link": []}})

    get_graphql_code = mock_get_graphql_code(mocker, "lol")
    global_state.stm_mapper.gateway_map = {import_state.mgm_details.mgm_id: {}}

    fwconfig_import_gateway.update_gateway_diffs(global_state, import_state, management_state, rb_link_controller)
    get_graphql_code.assert_not_called()
    assert rb_link_controller.rb_links is not None
    assert len(rb_link_controller.rb_links) == 0


def test_fwconfig_import_gateway_update_rulebase_link_diffs_no_configs(
    global_state: GlobalState,
    import_state: ImportState,
    management_state: ManagementState,
    fwconfig_import_gateway: FwConfigImportGateway,
    rb_link_controller: RulebaseLinkController,
):
    management_state.normalized_config = None
    with pytest.raises(FwoImporterError) as excinfo:
        fwconfig_import_gateway.update_rulebase_link_diffs(
            global_state, import_state, management_state, rb_link_controller
        )
    assert "normalized_config is None in update_rulebase_link_diffs" in str(excinfo.value)

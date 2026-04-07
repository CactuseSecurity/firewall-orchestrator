import unittest.mock

import pytest
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.rulebase_link_controller import RulebaseLinkController
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from models.rulebase_link import RulebaseLink
from states.global_state import GlobalState
from states.import_state import ImportState
from states.management_state import ManagementState


@pytest.fixture
def rulebase_link_controller() -> RulebaseLinkController:
    rulebase_link_controller = RulebaseLinkController()
    rulebase_link_controller.rb_links = []

    def get_rulebase_links(_import_state: ImportState) -> None:
        rulebase_link_controller.rb_links = [
            RulebaseLink(
                id=1,
                gw_id=1,
                from_rule_id=None,
                from_rulebase_id=None,
                to_rulebase_id=2,
                link_type=1,
                is_initial=True,
                is_global=False,
                is_section=True,
                created=1,
            )
        ]

    rulebase_link_controller.get_rulebase_links = unittest.mock.Mock(
        side_effect=get_rulebase_links,
    )

    return rulebase_link_controller



class TestFwconfigImportGatewayClear:
    def test_update_gateway_diffs_removes_links_on_clear(
        self,
        import_state: ImportState,
        global_state: GlobalState,
        management_state: ManagementState,
    ):
        global_state.fwo_config_controller.fwo_config.clear = True

        management_state.normalized_config = FwConfigNormalized(gateways=[])
        management_state.previous_config = FwConfigNormalized(gateways=[Gateway(Uid="gw-1")])

        gateway_importer = FwConfigImportGateway()
        rulebase_link_controller = RulebaseLinkController()

        gateway_importer.update_gateway_diffs(global_state, import_state, management_state, rulebase_link_controller)

        assert rulebase_link_controller.rb_links == []

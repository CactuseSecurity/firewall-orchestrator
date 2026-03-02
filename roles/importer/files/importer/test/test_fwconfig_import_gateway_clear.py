import unittest.mock

import pytest
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.import_state_controller import ImportStateController
from model_controllers.rulebase_link_controller import RulebaseLinkController
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from models.rulebase_link import RulebaseLink
from services.global_state import GlobalState


@pytest.fixture
def rulebase_link_controller() -> RulebaseLinkController:
    rulebase_link_controller = RulebaseLinkController()
    rulebase_link_controller.rb_links = []

    def get_rulebase_links():
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


@pytest.fixture
def fwconfig_import_gateway(
    rulebase_link_controller: RulebaseLinkController,
) -> FwConfigImportGateway:
    import_gateway = FwConfigImportGateway()
    import_gateway._rb_link_controller = unittest.mock.MagicMock()  # pyright: ignore[reportPrivateUsage]
    import_gateway.get_rb_link_controller = unittest.mock.MagicMock(
        return_value=rulebase_link_controller,  # pyright: ignore[reportPrivateUsage]
    )

    return import_gateway


class TestFwconfigImportGatewayClear:
    def test_update_gateway_diffs_removes_links_on_clear(
        self,
        import_state_controller: ImportStateController,
        global_state: GlobalState,
        rulebase_link_controller: RulebaseLinkController,
    ):
        import_state = import_state_controller
        import_state.state.is_clearing_import = True

        global_state.import_state = import_state
        global_state.normalized_config = FwConfigNormalized(gateways=[])
        global_state.previous_config = FwConfigNormalized(gateways=[Gateway(Uid="gw-1")])
        insert_called = False

        gateway_importer = FwConfigImportGateway()
        gateway_importer._rb_link_controller = unittest.mock.MagicMock()  # pyright: ignore[reportPrivateUsage]

        gateway_importer.update_gateway_diffs()

        assert rulebase_link_controller.rb_links == []
        assert insert_called is False

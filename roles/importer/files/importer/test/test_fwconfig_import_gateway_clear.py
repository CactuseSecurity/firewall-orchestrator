from fwo_base import init_service_provider
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from models.rulebase_link import RulebaseLink
from test.mocking.mock_import_state import MockImportStateController


def test_update_gateway_diffs_removes_links_on_clear():
    service_provider = init_service_provider()
    import_state = MockImportStateController(stub_setCoreData=True)
    import_state.state.is_clearing_import = True

    global_state = service_provider.get_global_state()
    global_state.import_state = import_state
    global_state.normalized_config = FwConfigNormalized(gateways=[])
    global_state.previous_config = FwConfigNormalized(gateways=[Gateway(Uid="gw-1")])

    removed_ids: list[int] = []
    insert_called = False

    class StubRulebaseLinkController:
        def __init__(self):
            self.rb_links: list[RulebaseLink] = []

        def get_rulebase_links(self, *_args, **_kwargs):
            self.rb_links = [
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

        def insert_rulebase_links(self, *_args, **_kwargs):
            nonlocal insert_called
            insert_called = True

        def remove_rulebase_links(self, _api_call, _stats, _import_id, removed_link_ids):
            removed_ids.extend(removed_link_ids)

    gateway_importer = FwConfigImportGateway()
    gateway_importer._rb_link_controller = StubRulebaseLinkController()

    gateway_importer.update_gateway_diffs()

    assert removed_ids == [1]
    assert insert_called is False

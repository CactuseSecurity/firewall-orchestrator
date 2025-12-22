import copy

from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.import_state_controller import ImportStateController
from services.global_state import GlobalState
from unit_tests.utils.config_builder import FwConfigBuilder


def test_add_cp_section_header_at_the_bottom(
    global_state: GlobalState,
    import_state_controller: ImportStateController,
    fwconfig_import_gateway: FwConfigImportGateway,
    fwconfig_builder: FwConfigBuilder,
):
    # Arrange
    config, mgm_id = fwconfig_builder.build_config(
        network_object_count=10, service_object_count=10, rulebases=1, rules_per_rulebase=10
    )
    global_state.normalized_config = config
    global_state.previous_config = copy.deepcopy(config)
    last_rulebase = config.rulebases[-1]
    last_rulebase_last_rule_uid = list(last_rulebase.rules.keys())[-1]
    new_rulebase = fwconfig_builder.add_rulebase(config, mgm_id)
    gateway = config.gateways[0]
    fwconfig_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase.uid, last_rulebase_last_rule_uid)

    fwconfig_builder.update_rule_map_and_rulebase_map(config, import_state_controller.state)
    to_rulebase_id = import_state_controller.state.lookup_rulebase_id(new_rulebase.uid)
    from_rulebase_id = import_state_controller.state.lookup_rulebase_id(last_rulebase.uid)
    fwconfig_builder.update_rb_links(gateway.RulebaseLinks, 1, fwconfig_import_gateway)

    import_state_controller.state.gateway_map[3] = {global_state.normalized_config.gateways[0].Uid or "": 1}

    # Act

    new_links, _ = fwconfig_import_gateway.update_rulebase_link_diffs()

    # Assert

    assert len(new_links) == 1, f"expected {1} new rulebase link, got {len(new_links)}"
    assert new_links[0]["from_rulebase_id"] == from_rulebase_id, (
        f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {new_links[0]['from_rulebase_id']}"
    )
    assert new_links[0]["to_rulebase_id"] == to_rulebase_id, (
        f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {new_links[0]['to_rulebase_id']}"
    )

    assert new_links[0]["is_section"], "expected last rulebase link to have is_section true, got false"

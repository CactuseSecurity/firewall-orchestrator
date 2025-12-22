import copy

import pytest
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized
from services.global_state import GlobalState
from unit_tests.utils.config_builder import FwConfigBuilder


@pytest.fixture
def config_tuple(fwconfig_builder: FwConfigBuilder) -> tuple[FwConfigNormalized, str]:
    config, mgm_id = fwconfig_builder.build_config(
        network_object_count=10, service_object_count=10, rulebases=1, rules_per_rulebase=10
    )
    return config, mgm_id


def test_add_cp_section_header_at_the_bottom(
    global_state: GlobalState,
    import_state_controller: ImportStateController,
    fwconfig_import_gateway: FwConfigImportGateway,
    config_tuple: tuple[FwConfigNormalized, str],
    fwconfig_builder: FwConfigBuilder,
):
    # Arrange
    config, mgm_id = config_tuple
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


def test_add_cp_section_header_in_existing_rulebase(
    global_state: GlobalState,
    import_state_controller: ImportStateController,
    fwconfig_import_gateway: FwConfigImportGateway,
    fwconfig_builder: FwConfigBuilder,
    config_tuple: tuple[FwConfigNormalized, str],
):
    # Arrange
    config, mgm_id = config_tuple
    global_state.normalized_config = config
    global_state.previous_config = copy.deepcopy(config)
    import_state_controller.state.mgm_details.uid = mgm_id

    last_rulebase = global_state.normalized_config.rulebases[-1]
    last_rulebase_last_rule_uid = list(last_rulebase.rules.keys())[-1]
    last_rulebase_last_rule = last_rulebase.rules.pop(last_rulebase_last_rule_uid)

    new_rulebase = fwconfig_builder.add_rulebase(
        global_state.normalized_config, import_state_controller.state.mgm_details.uid
    )
    fwconfig_builder.add_rule(global_state.normalized_config, new_rulebase.uid, last_rulebase_last_rule)
    gateway = global_state.normalized_config.gateways[0]
    fwconfig_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase.uid, last_rulebase_last_rule_uid)

    fwconfig_builder.update_rule_map_and_rulebase_map(global_state.normalized_config, import_state_controller.state)
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


def test_delete_cp_section_header(
    global_state: GlobalState,
    import_state_controller: ImportStateController,
    fwconfig_import_gateway: FwConfigImportGateway,
    fwconfig_builder: FwConfigBuilder,
    config_tuple: tuple[FwConfigNormalized, str],
):
    # Arrange
    config, mgm_id = config_tuple
    global_state.normalized_config = config

    # Move last five rules of last rulebase to new rulebase (previous config).
    global_state.previous_config = copy.deepcopy(global_state.normalized_config)
    import_state_controller.state.mgm_details.uid = mgm_id

    last_rulebase = global_state.previous_config.rulebases[-1]
    last_five_rules_uids = list(last_rulebase.rules.keys())[-5:]

    new_rulebase = fwconfig_builder.add_rulebase(global_state.previous_config, mgm_id)

    for rule_uid in last_five_rules_uids:
        rule = last_rulebase.rules.pop(rule_uid)
        fwconfig_builder.add_rule(global_state.previous_config, new_rulebase.uid, rule)
    # Create rulebase link for cp_section header (previous config)

    last_rulebase_last_rule_uid = list(last_rulebase.rules.keys())[-1]
    gateway = global_state.previous_config.gateways[0]
    fwconfig_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase.uid, last_rulebase_last_rule_uid)

    fwconfig_builder.update_rule_map_and_rulebase_map(global_state.previous_config, import_state_controller.state)
    fwconfig_builder.update_rule_num_numerics(global_state.previous_config)
    fwconfig_builder.update_rb_links(gateway.RulebaseLinks, 1, fwconfig_import_gateway)
    import_state_controller.state.gateway_map[3] = {global_state.normalized_config.gateways[0].Uid or "": 1}

    # Act

    _, deleted_links_ids = fwconfig_import_gateway.update_rulebase_link_diffs()
    # Assert

    assert deleted_links_ids[0] == fwconfig_import_gateway.get_rb_link_controller().rb_links[-1].id


def test_add_inline_layer(
    global_state: GlobalState,
    import_state_controller: ImportStateController,
    fwconfig_import_gateway: FwConfigImportGateway,
    fwconfig_builder: FwConfigBuilder,
    config_tuple: tuple[FwConfigNormalized, str],
):
    # Arrange
    config, mgm_id = config_tuple
    global_state.normalized_config = config
    global_state.previous_config = copy.deepcopy(config)
    import_state_controller.state.mgm_details.uid = mgm_id

    from_rulebase = config.rulebases[-1]
    from_rule = next(iter(from_rulebase.rules.values()))

    added_rulebase = fwconfig_builder.add_rulebase(config, mgm_id)
    fwconfig_builder.add_rule(config, added_rulebase.uid)

    gateway = config.gateways[0]
    fwconfig_builder.add_inline_layer(gateway, from_rulebase.uid, from_rule.rule_uid or "", added_rulebase.uid)
    fwconfig_builder.update_rule_map_and_rulebase_map(config, import_state_controller.state)
    from_rule_id, from_rulebase_id, to_rulebase_id = import_state_controller.state.lookup_ids_for_rulebase_link(
        from_rule.rule_uid, from_rulebase.uid, added_rulebase.uid
    )
    fwconfig_builder.update_rb_links(gateway.RulebaseLinks, 1, fwconfig_import_gateway)
    import_state_controller.state.gateway_map[3] = {global_state.normalized_config.gateways[0].Uid or "": 1}
    # Act

    new_links, _ = fwconfig_import_gateway.update_rulebase_link_diffs()

    # Assert

    assert len(new_links) == 1, f"expected {1} new rulebase link, got {len(new_links)}"
    assert new_links[0]["from_rule_id"] == from_rule_id, (
        f"expected last rulebase link to have from_rule_id {from_rule_id}, got {new_links[0]['from_rule_id']}"
    )
    assert new_links[0]["from_rulebase_id"] == from_rulebase_id, (
        f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {new_links[0]['from_rulebase_id']}",
    )
    assert new_links[0]["to_rulebase_id"] == to_rulebase_id, (
        f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {new_links[0]['to_rulebase_id']}",
    )
    assert not new_links[0]["is_section"], "expected last rulebase link to have is_section false, got true"


def test_delete_inline_layer(
    global_state: GlobalState,
    import_state_controller: ImportStateController,
    fwconfig_import_gateway: FwConfigImportGateway,
    fwconfig_builder: FwConfigBuilder,
    config_tuple: tuple[FwConfigNormalized, str],
):
    # Arrange
    config, mgm_id = config_tuple
    global_state.normalized_config = config
    global_state.previous_config = copy.deepcopy(global_state.normalized_config)
    import_state_controller.state.mgm_details.uid = mgm_id

    from_rulebase = global_state.previous_config.rulebases[-1]
    from_rule = next(iter(from_rulebase.rules.values()))

    added_rulebase = fwconfig_builder.add_rulebase(global_state.previous_config, mgm_id)
    fwconfig_builder.add_rule(global_state.previous_config, added_rulebase.uid)

    gateway = global_state.previous_config.gateways[0]
    fwconfig_builder.add_inline_layer(gateway, from_rulebase.uid, from_rule.rule_uid or "", added_rulebase.uid)

    fwconfig_builder.update_rule_map_and_rulebase_map(global_state.previous_config, import_state_controller.state)
    _from_rule_id, _from_rulebase_id, _to_rulebase_id = import_state_controller.state.lookup_ids_for_rulebase_link(
        from_rule.rule_uid, from_rulebase.uid, added_rulebase.uid
    )
    fwconfig_builder.update_rb_links(gateway.RulebaseLinks, 1, fwconfig_import_gateway)
    import_state_controller.state.gateway_map[3] = {global_state.normalized_config.gateways[0].Uid or "": 1}

    # Act

    _, deleted_links_ids = fwconfig_import_gateway.update_rulebase_link_diffs()
    # Assert

    assert len(deleted_links_ids) == 1, f"expected {1} new rulebase link, got {len(deleted_links_ids)}"
    assert deleted_links_ids[0] == fwconfig_import_gateway.get_rb_link_controller().rb_links[-1].id


def test_move_inline_layer(
    global_state: GlobalState,
    import_state_controller: ImportStateController,
    fwconfig_import_gateway: FwConfigImportGateway,
    fwconfig_builder: FwConfigBuilder,
    config_tuple: tuple[FwConfigNormalized, str],
):
    # Arrange
    config, mgm_id = config_tuple
    global_state.normalized_config = config
    global_state.previous_config = copy.deepcopy(global_state.normalized_config)
    import_state_controller.state.mgm_details.uid = mgm_id

    from_rulebase_previous = global_state.previous_config.rulebases[-1]
    from_rule_previous = next(iter(from_rulebase_previous.rules.values()))

    from_rulebase_normalized = global_state.normalized_config.rulebases[0]
    from_rule_normalized = next(iter(from_rulebase_normalized.rules.values()))

    added_rulebase = fwconfig_builder.add_rulebase(global_state.previous_config, mgm_id)
    fwconfig_builder.add_rule(global_state.previous_config, added_rulebase.uid)
    added_rulebase_copy = copy.deepcopy(added_rulebase)
    fwconfig_builder.add_rulebase(global_state.normalized_config, mgm_id, added_rulebase_copy)

    gateway_previous = global_state.previous_config.gateways[0]
    fwconfig_builder.add_inline_layer(
        gateway_previous, from_rulebase_previous.uid, from_rule_previous.rule_uid or "", added_rulebase.uid
    )
    gateway_normalized = global_state.normalized_config.gateways[0]
    fwconfig_builder.add_inline_layer(
        gateway_normalized, from_rulebase_normalized.uid, from_rule_normalized.rule_uid or "", added_rulebase_copy.uid
    )

    fwconfig_builder.update_rule_map_and_rulebase_map(global_state.previous_config, import_state_controller.state)
    from_rule_id, from_rulebase_id, to_rulebase_id = import_state_controller.state.lookup_ids_for_rulebase_link(
        from_rule_normalized.rule_uid,
        from_rulebase_normalized.uid,
        added_rulebase_copy.uid,
    )
    fwconfig_builder.update_rb_links(gateway_previous.RulebaseLinks, 1, fwconfig_import_gateway)

    # Act

    new_links, deleted_links_ids = fwconfig_import_gateway.update_rulebase_link_diffs()

    # Assert

    assert len(new_links) == 1, f"expected {1} new rulebase link, got {len(new_links)}"
    assert new_links[0]["from_rule_id"] == from_rule_id, (
        f"expected last rulebase link to have from_rule_id {from_rule_id}, got {new_links[0]['from_rule_id']}"
    )
    assert new_links[0]["from_rulebase_id"] == from_rulebase_id, (
        f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {new_links[0]['from_rulebase_id']}"
    )
    assert new_links[0]["to_rulebase_id"] == to_rulebase_id, (
        f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {new_links[0]['to_rulebase_id']}"
    )
    assert not new_links[0]["is_section"], "expected last rulebase link to have is_section false, got true"
    assert len(deleted_links_ids) == 1, f"expected {1} new rulebase link, got {len(deleted_links_ids)}"
    assert deleted_links_ids[0] == fwconfig_import_gateway.get_rb_link_controller().rb_links[-1].id

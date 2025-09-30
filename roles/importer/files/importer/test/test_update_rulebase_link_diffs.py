import unittest

from test.tools.set_up_test import set_up_test_for_rulebase_link_test_with_defaults, set_up_test_for_ruleorder_test_with_delete_of_section_header, update_rule_map_and_rulebase_map
from test.mocking.mock_fwconfig_import_gateway import MockFwConfigImportGateway
from fwo_base import init_service_provider

class TestUpdateRulebaseLinkDiffs(unittest.TestCase):

    def test_add_cp_section_header_at_the_bottom(self):
        
        # Arrange

        config_builder, fw_config_import_gateway, mgm_uid = set_up_test_for_rulebase_link_test_with_defaults()

        last_rulebase = fw_config_import_gateway._global_state.normalized_config.rulebases[-1]
        last_rulebase_last_rule_uid = list(last_rulebase.Rules.keys())[-1]
        _, new_rulebase_uid = config_builder.add_rulebase(fw_config_import_gateway._global_state.normalized_config, mgm_uid)
        gateway = fw_config_import_gateway._global_state.normalized_config.gateways[0]
        config_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase_uid, last_rulebase_last_rule_uid)
        
        update_rule_map_and_rulebase_map(fw_config_import_gateway._global_state.normalized_config, fw_config_import_gateway._global_state.import_state)
        to_rulebase_id = fw_config_import_gateway._global_state.import_state.lookupRulebaseId(new_rulebase_uid)
        from_rulebase_id = fw_config_import_gateway._global_state.import_state.lookupRulebaseId(last_rulebase.uid)

        # Act

        new_links, _ = fw_config_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(new_links) == 1, f"expected {1} new rulebase link, got {len(new_links)}")
        self.assertTrue(new_links[0]['from_rulebase_id'] == from_rulebase_id, f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {new_links[0]['from_rulebase_id']}")
        self.assertTrue(new_links[0]['to_rulebase_id'] == to_rulebase_id, f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {new_links[0]['to_rulebase_id']}")
        self.assertTrue(new_links[0]['is_section'], "expected last rulebase link to have is_section true, got false")


    def test_add_cp_section_header_in_existing_rulebase(self):
        
        # Arrange

        config_builder, fw_config_import_gateway, mgm_uid = set_up_test_for_rulebase_link_test_with_defaults()

        last_rulebase = fw_config_import_gateway._global_state.normalized_config.rulebases[-1]
        last_rulebase_last_rule_uid = list(last_rulebase.Rules.keys())[-1]
        last_rulebase_last_rule = last_rulebase.Rules.pop(last_rulebase_last_rule_uid)

        _, new_rulebase_uid = config_builder.add_rulebase(fw_config_import_gateway._global_state.normalized_config, mgm_uid)
        config_builder.add_rule(fw_config_import_gateway._global_state.normalized_config, new_rulebase_uid, last_rulebase_last_rule.model_dump())
        gateway = fw_config_import_gateway._global_state.normalized_config.gateways[0]
        config_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase_uid, last_rulebase_last_rule_uid)

        update_rule_map_and_rulebase_map(fw_config_import_gateway._global_state.normalized_config, fw_config_import_gateway._global_state.import_state)
        to_rulebase_id = fw_config_import_gateway._global_state.import_state.lookupRulebaseId(new_rulebase_uid)
        from_rulebase_id = fw_config_import_gateway._global_state.import_state.lookupRulebaseId(last_rulebase.uid)

        # Act

        new_links, _ = fw_config_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(new_links) == 1, f"expected {1} new rulebase link, got {len(new_links)}")
        self.assertTrue(new_links[0]['from_rulebase_id'] == from_rulebase_id, f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {new_links[0]['from_rulebase_id']}")
        self.assertTrue(new_links[0]['to_rulebase_id'] == to_rulebase_id, f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {new_links[0]['to_rulebase_id']}")
        self.assertTrue(new_links[0]['is_section'], "expected last rulebase link to have is_section true, got false")


    def test_delete_cp_section_header(self):

        # Arrange
        
        previous_config, fwconfig_import_rule, _ = set_up_test_for_ruleorder_test_with_delete_of_section_header()

        fw_config_import_gateway = MockFwConfigImportGateway()
        fw_config_import_gateway._global_state.normalized_config = fwconfig_import_rule.normalized_config
        fw_config_import_gateway._global_state.previous_config = previous_config
        fw_config_import_gateway._global_state.import_state = fwconfig_import_rule.import_details

        _, deleted_links_ids = fw_config_import_gateway.update_rulebase_link_diffs()

        self.assertTrue(False, "Unit test is only partially implemented.")


    @unittest.skip("Temporary deactivated, because test is not implemented.")
    def test_move_cp_section_header(self):
        raise NotImplementedError()


    def test_add_inline_layer(self):
                
        # Arrange

        config_builder, fw_config_import_gateway, mgm_uid = set_up_test_for_rulebase_link_test_with_defaults()

        last_rulebase = fw_config_import_gateway._global_state.normalized_config.rulebases[-1]
        last_rulebase_last_rule_uid = list(last_rulebase.Rules.keys())[-1]
        last_rulebase_last_rule = last_rulebase.Rules.pop(last_rulebase_last_rule_uid)

        new_rulebase, new_rulebase_uid = config_builder.add_rulebase(fw_config_import_gateway._global_state.normalized_config, mgm_uid)
        config_builder.add_rule(fw_config_import_gateway._global_state.normalized_config, new_rulebase_uid, last_rulebase_last_rule.model_dump())
        gateway = fw_config_import_gateway._global_state.normalized_config.gateways[0]
        new_last_rulebase_last_rule_uid = list(last_rulebase.Rules.keys())[-1]
        index = len(gateway.RulebaseLinks)
        config_builder.add_inline_layer(gateway, index, last_rulebase.uid, new_rulebase_uid, new_last_rulebase_last_rule_uid)

        update_rule_map_and_rulebase_map(fw_config_import_gateway._global_state.normalized_config, fw_config_import_gateway._global_state.import_state)
        from_rule_id = fw_config_import_gateway._global_state.import_state.lookupRule(new_last_rulebase_last_rule_uid)
        to_rulebase_id = fw_config_import_gateway._global_state.import_state.lookupRulebaseId(new_rulebase_uid)
        from_rulebase_id = fw_config_import_gateway._global_state.import_state.lookupRulebaseId(last_rulebase.uid)

        # Act

        new_links, _ = fw_config_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(new_links) == 1, f"expected {1} new rulebase link, got {len(new_links)}")
        self.assertTrue(new_links[0]['from_rule_id'] == from_rule_id, f"expected last rulebase link to have from_rule_id {from_rule_id}, got {new_links[0]['from_rule_id']}")
        self.assertTrue(new_links[0]['from_rulebase_id'] == from_rulebase_id, f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {new_links[0]['from_rulebase_id']}")
        self.assertTrue(new_links[0]['to_rulebase_id'] == to_rulebase_id, f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {new_links[0]['to_rulebase_id']}")
        self.assertTrue(new_links[0]['is_section'] == False, "expected last rulebase link to have is_section false, got true")


    @unittest.skip("Temporary deactivated, because test is not implemented.")
    def test_delete_inline_layer(self):
        raise NotImplementedError()


    @unittest.skip("Temporary deactivated, because test is not implemented.")
    def test_move_inline_layer(self):
        raise NotImplementedError()
    

import unittest

from test.tools.set_up_test import set_up_test_for_rulebase_link_test_with_defaults, update_rule_map_and_rulebase_map
from test.mocking.mock_fwconfig_import_gateway import MockFwConfigImportGateway
from fwo_base import init_service_provider

class TestUpdateRulebaseLinkDiffs(unittest.TestCase):

    def test_add_cp_section_header_at_the_bottom(self):
        
        # Arrange

        config_builder, fw_config_import_gateway, mgm_uid = set_up_test_for_rulebase_link_test_with_defaults()

        last_rulebase = fw_config_import_gateway._global_state.normalized_config.rulebases[-1]
        new_rulebase, new_rulebase_uid = config_builder.add_rulebase(fw_config_import_gateway._global_state.normalized_config, mgm_uid)
        gateway = fw_config_import_gateway._global_state.normalized_config.gateways[0]
        config_builder.add_cp_section_header(fw_config_import_gateway._global_state.normalized_config, gateway, len(gateway.RulebaseLinks), new_rulebase_uid, last_rulebase.uid)
        
        update_rule_map_and_rulebase_map(fw_config_import_gateway._global_state.normalized_config, fw_config_import_gateway._global_state.import_state)
        to_rulebase_id = fw_config_import_gateway._global_state.import_state.lookupRulebaseId(new_rulebase_uid)
        from_rulebase_id = fw_config_import_gateway._global_state.import_state.lookupRulebaseId(last_rulebase.uid)

        # Act

        rb_link_list = fw_config_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(rb_link_list) == 1, f"expected {1} new rulebase link, got {len(rb_link_list)}")
        self.assertTrue(rb_link_list[-1]['from_rulebase_id'] == from_rulebase_id, f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {rb_link_list[-1]['from_rulebase_id']}")
        self.assertTrue(rb_link_list[-1]['to_rulebase_id'] == to_rulebase_id, f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {rb_link_list[-1]['to_rulebase_id']}")
        self.assertTrue(rb_link_list[-1]['is_section'], f"expected last rulebase link to have is_section true, got false")


    def test_add_cp_section_header_in_existing_rulebase(self):
        
        # Arrange

        config_builder, fw_config_import_gateway, mgm_uid = set_up_test_for_rulebase_link_test_with_defaults()

        last_rulebase = fw_config_import_gateway._global_state.normalized_config.rulebases[-1]
        last_rulebase_last_rule_uid = list(last_rulebase.Rules.keys())[-1]
        last_rulebase_last_rule = last_rulebase.Rules.pop(last_rulebase_last_rule_uid)

        new_rulebase, new_rulebase_uid = config_builder.add_rulebase(fw_config_import_gateway._global_state.normalized_config, mgm_uid)
        config_builder.add_rule(fw_config_import_gateway._global_state.normalized_config, new_rulebase_uid, last_rulebase_last_rule.model_dump())
        gateway = fw_config_import_gateway._global_state.normalized_config.gateways[0]
        config_builder.add_cp_section_header(fw_config_import_gateway._global_state.normalized_config, gateway, len(gateway.RulebaseLinks), new_rulebase_uid, last_rulebase.uid)

        update_rule_map_and_rulebase_map(fw_config_import_gateway._global_state.normalized_config, fw_config_import_gateway._global_state.import_state)
        to_rulebase_id = fw_config_import_gateway._global_state.import_state.lookupRulebaseId(new_rulebase_uid)
        from_rulebase_id = fw_config_import_gateway._global_state.import_state.lookupRulebaseId(last_rulebase.uid)

        # Act

        rb_link_list = fw_config_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(rb_link_list) == 1, f"expected {1} new rulebase link, got {len(rb_link_list)}")
        self.assertTrue(rb_link_list[-1]['from_rulebase_id'] == from_rulebase_id, f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {rb_link_list[-1]['from_rulebase_id']}")
        self.assertTrue(rb_link_list[-1]['to_rulebase_id'] == to_rulebase_id, f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {rb_link_list[-1]['to_rulebase_id']}")
        self.assertTrue(rb_link_list[-1]['is_section'], f"expected last rulebase link to have is_section true, got false")



    @unittest.skip("Temporary deactivated, because test is not implemented.")
    def test_delete_cp_section_header(self):
        raise NotImplementedError()


    @unittest.skip("Temporary deactivated, because test is not implemented.")
    def test_move_cp_section_header(self):
        raise NotImplementedError()


    @unittest.skip("Temporary deactivated, because test is not implemented.")
    def test_add_inline_layer(self):
        raise NotImplementedError()


    @unittest.skip("Temporary deactivated, because test is not implemented.")
    def test_delete_inline_layer(self):
        raise NotImplementedError()


    @unittest.skip("Temporary deactivated, because test is not implemented.")
    def test_move_inline_layer(self):
        raise NotImplementedError()
    

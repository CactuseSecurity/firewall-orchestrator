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

        # Act

        rb_link_list = fw_config_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(rb_link_list) == len(gateway.RulebaseLinks), f"expected {len(gateway.RulebaseLinks)} new rulebase link, got {len(rb_link_list)}")
        



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
    

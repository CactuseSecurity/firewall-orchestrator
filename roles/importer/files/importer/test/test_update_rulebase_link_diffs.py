import copy
import unittest

from fwo_base import init_service_provider

from models.fwconfig_normalized import FwConfigNormalized

from services.service_provider import ServiceProvider

from test.mocking.mock_import_state import MockImportStateController
from test.mocking.mock_config import MockFwConfigNormalizedBuilder
from test.mocking.mock_fwconfig_import_gateway import MockFwConfigImportGateway
from test.tools.set_up_test import set_up_test_for_ruleorder_test_with_delete_of_section_header, update_rb_links, update_rule_map_and_rulebase_map

class TestUpdateRulebaseLinkDiffs(unittest.TestCase):

    _config_builder: MockFwConfigNormalizedBuilder
    _fwconfig_import_gateway: MockFwConfigImportGateway
    _service_provider: ServiceProvider
    _mgm_uid: str
    _previous_config: FwConfigNormalized
    _normalized_config: FwConfigNormalized
    _import_state: MockImportStateController


    @classmethod
    def setUpClass(cls):
        """
            Gets invoked once before running any test of this class.
        """

        cls._service_provider = init_service_provider() 
        cls._config_builder = MockFwConfigNormalizedBuilder()
        


    @classmethod
    def tearDownClass(cls):
        """
            Gets invoked once after running every test of this class.
        """
        pass


    def setUp(self):
        """
            Gets invoked one time per test method before running it.
        """

        self._config_builder.set_up()
        self._fwconfig_import_gateway = MockFwConfigImportGateway()

        self._previous_config, self._mgm_uid = self._config_builder.build_config(
            {
                "rule_config": [10,10,10],
                "network_object_config": 10,
                "service_config": 10,
                "user_config": 10
            }
        )

        self._normalized_config = copy.deepcopy(self._previous_config)

        self._fwconfig_import_gateway._global_state.normalized_config = self._normalized_config
        self._fwconfig_import_gateway._global_state.previous_config = self._previous_config
        self._import_state = self._fwconfig_import_gateway._global_state.import_state


    def tearDown(self):
        """
            Gets invoked one time per test method after running it.
        """
        pass


    def test_add_cp_section_header_at_the_bottom(self):
        
        # Arrange

        last_rulebase = self._normalized_config.rulebases[-1]
        last_rulebase_last_rule_uid = list(last_rulebase.Rules.keys())[-1]
        _, new_rulebase_uid = self._config_builder.add_rulebase(self._normalized_config, self._mgm_uid)
        gateway = self._normalized_config.gateways[0]
        self._config_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase_uid, last_rulebase_last_rule_uid)
        
        update_rule_map_and_rulebase_map(self._normalized_config, self._import_state)
        to_rulebase_id = self._import_state.lookupRulebaseId(new_rulebase_uid)
        from_rulebase_id = self._import_state.lookupRulebaseId(last_rulebase.uid)

        # Act

        new_links, _ = self._fwconfig_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(new_links) == 1, f"expected {1} new rulebase link, got {len(new_links)}")
        self.assertTrue(new_links[0]['from_rulebase_id'] == from_rulebase_id, f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {new_links[0]['from_rulebase_id']}")
        self.assertTrue(new_links[0]['to_rulebase_id'] == to_rulebase_id, f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {new_links[0]['to_rulebase_id']}")
        self.assertTrue(new_links[0]['is_section'], "expected last rulebase link to have is_section true, got false")


    def test_add_cp_section_header_in_existing_rulebase(self):
        
        # Arrange

        last_rulebase = self._normalized_config.rulebases[-1]
        last_rulebase_last_rule_uid = list(last_rulebase.Rules.keys())[-1]
        last_rulebase_last_rule = last_rulebase.Rules.pop(last_rulebase_last_rule_uid)

        _, new_rulebase_uid = self._config_builder.add_rulebase(self._normalized_config, self._mgm_uid)
        self._config_builder.add_rule(self._normalized_config, new_rulebase_uid, last_rulebase_last_rule.model_dump())
        gateway = self._normalized_config.gateways[0]
        self._config_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase_uid, last_rulebase_last_rule_uid)

        update_rule_map_and_rulebase_map(self._normalized_config, self._import_state)
        to_rulebase_id = self._import_state.lookupRulebaseId(new_rulebase_uid)
        from_rulebase_id = self._import_state.lookupRulebaseId(last_rulebase.uid)

        # Act

        new_links, _ = self._fwconfig_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(new_links) == 1, f"expected {1} new rulebase link, got {len(new_links)}")
        self.assertTrue(new_links[0]['from_rulebase_id'] == from_rulebase_id, f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {new_links[0]['from_rulebase_id']}")
        self.assertTrue(new_links[0]['to_rulebase_id'] == to_rulebase_id, f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {new_links[0]['to_rulebase_id']}")
        self.assertTrue(new_links[0]['is_section'], "expected last rulebase link to have is_section true, got false")


    def test_delete_cp_section_header(self):

        # Arrange
        
        self._previous_config, fwconfig_import_rule, _ = set_up_test_for_ruleorder_test_with_delete_of_section_header()

        self._normalized_config = fwconfig_import_rule.normalized_config
        self._import_state = fwconfig_import_rule.import_details

        self._fwconfig_import_gateway._global_state.normalized_config = self._normalized_config
        self._fwconfig_import_gateway._global_state.previous_config = self._previous_config
        self._fwconfig_import_gateway._global_state.import_state = self._import_state


        gateway = self._previous_config.gateways[0]
        update_rb_links(gateway.RulebaseLinks, 1, self._fwconfig_import_gateway)

        # Act

        _, deleted_links_ids = self._fwconfig_import_gateway.update_rulebase_link_diffs()

        # Assert
        
        self.assertTrue(deleted_links_ids[0] == self._fwconfig_import_gateway._rb_link_controller.rb_links[-1].id)


    def test_add_inline_layer(self):
                
        # Arrange

        from_rule_id, to_rulebase_id, from_rulebase_id = self.set_up_inline_layer_test(self._config_builder, self._normalized_config, self._import_state, self._mgm_uid, self._fwconfig_import_gateway)
        
        # Act

        new_links, _ = self._fwconfig_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(new_links) == 1, f"expected {1} new rulebase link, got {len(new_links)}")
        self.assertTrue(new_links[0]['from_rule_id'] == from_rule_id, f"expected last rulebase link to have from_rule_id {from_rule_id}, got {new_links[0]['from_rule_id']}")
        self.assertTrue(new_links[0]['from_rulebase_id'] == from_rulebase_id, f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {new_links[0]['from_rulebase_id']}")
        self.assertTrue(new_links[0]['to_rulebase_id'] == to_rulebase_id, f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {new_links[0]['to_rulebase_id']}")
        self.assertTrue(new_links[0]['is_section'] == False, "expected last rulebase link to have is_section false, got true")


    def test_delete_inline_layer(self):

        # Arrange

        _, _, _ = self.set_up_inline_layer_test(self._config_builder, self._previous_config, self._import_state, self._mgm_uid, self._fwconfig_import_gateway)

        # Act

        _, deleted_links_ids = self._fwconfig_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(deleted_links_ids) == 1, f"expected {1} new rulebase link, got {len(deleted_links_ids)}")
        self.assertTrue(deleted_links_ids[0] == self._fwconfig_import_gateway._rb_link_controller.rb_links[-1].id)


    @unittest.skip("Temporary deactivated, because test is not implemented.")
    def test_move_inline_layer(self):
        raise NotImplementedError()
    

    def set_up_inline_layer_test(self, config_builder, config, import_state, mgm_uid, fw_config_import_gateway):

        last_rulebase = config.rulebases[-1]
        last_rulebase_last_rule_uid = list(last_rulebase.Rules.keys())[-1]
        last_rulebase_last_rule = last_rulebase.Rules.pop(last_rulebase_last_rule_uid)

        _, new_rulebase_uid = self._config_builder.add_rulebase(config, mgm_uid)
        self._config_builder.add_rule(config, new_rulebase_uid, last_rulebase_last_rule.model_dump())
        gateway = config.gateways[0]
        new_last_rulebase_last_rule_uid = list(last_rulebase.Rules.keys())[-1]
        index = len(gateway.RulebaseLinks)
        self._config_builder.add_inline_layer(gateway, index, last_rulebase.uid, new_rulebase_uid, new_last_rulebase_last_rule_uid)

        update_rule_map_and_rulebase_map(config, import_state)
        from_rule_id = import_state.lookupRule(new_last_rulebase_last_rule_uid)
        to_rulebase_id = import_state.lookupRulebaseId(new_rulebase_uid)
        from_rulebase_id = import_state.lookupRulebaseId(last_rulebase.uid)

        update_rb_links(gateway.RulebaseLinks, 1, fw_config_import_gateway)

        return from_rule_id, to_rulebase_id, from_rulebase_id
    

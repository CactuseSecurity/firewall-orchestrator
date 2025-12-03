import copy
import unittest

from fwo_base import init_service_provider, register_global_state

from models.fwconfig_normalized import FwConfigNormalized

from models.rulebase import Rulebase
from fwo_log import FWOLogger
from services.service_provider import ServiceProvider

from test.mocking.mock_import_state import MockImportStateController
from test.mocking.mock_config import MockFwConfigNormalizedBuilder
from test.mocking.mock_fwconfig_import_gateway import MockFwConfigImportGateway
from test.tools.set_up_test import update_rb_links, update_rule_map_and_rulebase_map, update_rule_num_numerics, remove_rule_from_rulebase, lookup_ids_for_rulebase_link

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

        FWOLogger(2)
        cls._service_provider = init_service_provider() 
        register_global_state(MockImportStateController(import_id=1, stub_setCoreData=True))
        cls._config_builder = MockFwConfigNormalizedBuilder()


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


    def test_add_cp_section_header_at_the_bottom(self):
        
        # Arrange

        last_rulebase = self._normalized_config.rulebases[-1]
        last_rulebase_last_rule_uid = list(last_rulebase.rules.keys())[-1]
        new_rulebase = self._config_builder.add_rulebase(self._normalized_config, self._mgm_uid)
        gateway = self._normalized_config.gateways[0]
        self._config_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase.uid, last_rulebase_last_rule_uid)
        
        update_rule_map_and_rulebase_map(self._normalized_config, self._import_state)
        to_rulebase_id = self._import_state.lookupRulebaseId(new_rulebase.uid)
        from_rulebase_id = self._import_state.lookupRulebaseId(last_rulebase.uid)
        update_rb_links(gateway.RulebaseLinks, 1, self._fwconfig_import_gateway)

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
        last_rulebase_last_rule_uid = list(last_rulebase.rules.keys())[-1]
        last_rulebase_last_rule = last_rulebase.rules.pop(last_rulebase_last_rule_uid)

        new_rulebase = self._config_builder.add_rulebase(self._normalized_config, self._mgm_uid)
        self._config_builder.add_rule(self._normalized_config, new_rulebase.uid, last_rulebase_last_rule.model_dump())
        gateway = self._normalized_config.gateways[0]
        self._config_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase.uid, last_rulebase_last_rule_uid)

        update_rule_map_and_rulebase_map(self._normalized_config, self._import_state)
        to_rulebase_id = self._import_state.lookupRulebaseId(new_rulebase.uid)
        from_rulebase_id = self._import_state.lookupRulebaseId(last_rulebase.uid)
        update_rb_links(gateway.RulebaseLinks, 1, self._fwconfig_import_gateway)

        # Act

        new_links, _ = self._fwconfig_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(new_links) == 1, f"expected {1} new rulebase link, got {len(new_links)}")
        self.assertTrue(new_links[0]['from_rulebase_id'] == from_rulebase_id, f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {new_links[0]['from_rulebase_id']}")
        self.assertTrue(new_links[0]['to_rulebase_id'] == to_rulebase_id, f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {new_links[0]['to_rulebase_id']}")
        self.assertTrue(new_links[0]['is_section'], "expected last rulebase link to have is_section true, got false")


    def test_delete_cp_section_header(self):

        # Arrange
        
        # Move last five rules of last rulebase to new rulebase (previous config).

        last_rulebase = self._previous_config.rulebases[-1]
        last_five_rules_uids = list(last_rulebase.rules.keys())[-5:]

        new_rulebase = self._config_builder.add_rulebase(self._previous_config, self._mgm_uid)

        for rule_uid in last_five_rules_uids:
            rule = last_rulebase.rules.pop(rule_uid)
            self._config_builder.add_rule(self._previous_config, new_rulebase.uid, rule.model_dump())
        
        # Create rulebase link for cp_section header (previous config)

        last_rulebase_last_rule_uid = list(last_rulebase.rules.keys())[-1]
        gateway = self._previous_config.gateways[0]
        self._config_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase.uid, last_rulebase_last_rule_uid)

        update_rule_map_and_rulebase_map(self._previous_config, self._import_state)
        update_rule_num_numerics(self._previous_config)
        update_rb_links(gateway.RulebaseLinks, 1, self._fwconfig_import_gateway)

        # Act

        _, deleted_links_ids = self._fwconfig_import_gateway.update_rulebase_link_diffs()

        # Assert
        
        self.assertTrue(deleted_links_ids[0] == self._fwconfig_import_gateway._rb_link_controller.rb_links[-1].id)


    def test_add_inline_layer(self):
                
        # Arrange

        from_rulebase = self._normalized_config.rulebases[-1]
        from_rule = list(from_rulebase.rules.values())[0]

        added_rulebase = self._config_builder.add_rulebase(self._normalized_config, self._mgm_uid)
        self._config_builder.add_rule(self._normalized_config, added_rulebase.uid)

        gateway = self._normalized_config.gateways[0]
        self._config_builder.add_inline_layer(gateway, from_rulebase.uid, from_rule.rule_uid, added_rulebase.uid)

        update_rule_map_and_rulebase_map(self._normalized_config, self._import_state)
        from_rule_id, from_rulebase_id, to_rulebase_id = lookup_ids_for_rulebase_link(self._import_state, from_rule.rule_uid, from_rulebase.uid, added_rulebase.uid)
        update_rb_links(gateway.RulebaseLinks, 1, self._fwconfig_import_gateway)

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

        from_rulebase = self._previous_config.rulebases[-1]
        from_rule = list(from_rulebase.rules.values())[0]

        added_rulebase = self._config_builder.add_rulebase(self._previous_config, self._mgm_uid)
        self._config_builder.add_rule(self._previous_config, added_rulebase.uid)

        gateway = self._previous_config.gateways[0]
        self._config_builder.add_inline_layer(gateway, from_rulebase.uid, from_rule.rule_uid, added_rulebase.uid)
        
        update_rule_map_and_rulebase_map(self._previous_config, self._import_state)
        from_rule_id, from_rulebase_id, to_rulebase_id = lookup_ids_for_rulebase_link(self._import_state, from_rule.rule_uid, from_rulebase.uid, added_rulebase.uid)
        update_rb_links(gateway.RulebaseLinks,1,self._fwconfig_import_gateway)

        # Act

        _, deleted_links_ids = self._fwconfig_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(deleted_links_ids) == 1, f"expected {1} new rulebase link, got {len(deleted_links_ids)}")
        self.assertTrue(deleted_links_ids[0] == self._fwconfig_import_gateway._rb_link_controller.rb_links[-1].id)


    def test_move_inline_layer(self):
        # Arrange

        from_rulebase_previous = self._previous_config.rulebases[-1]
        from_rule_previous = list(from_rulebase_previous.rules.values())[0]

        from_rulebase_normalized = self._normalized_config.rulebases[0]
        from_rule_normalized = list(from_rulebase_normalized.rules.values())[0]

        added_rulebase = self._config_builder.add_rulebase(self._previous_config, self._mgm_uid)
        self._config_builder.add_rule(self._previous_config, added_rulebase.uid)
        added_rulebase_copy = copy.deepcopy(added_rulebase)
        self._config_builder.add_rulebase(self._normalized_config, self._mgm_uid, added_rulebase_copy)

        gateway_previous = self._previous_config.gateways[0]
        self._config_builder.add_inline_layer(gateway_previous, from_rulebase_previous.uid, from_rule_previous.rule_uid, added_rulebase.uid)
        gateway_normalized = self._normalized_config.gateways[0]
        self._config_builder.add_inline_layer(gateway_normalized, from_rulebase_normalized.uid, from_rule_normalized.rule_uid, added_rulebase_copy.uid)

        update_rule_map_and_rulebase_map(self._previous_config, self._import_state)
        from_rule_id, from_rulebase_id, to_rulebase_id = lookup_ids_for_rulebase_link(self._import_state, from_rule_normalized.rule_uid, from_rulebase_normalized.uid, added_rulebase_copy.uid)
        update_rb_links(gateway_previous.RulebaseLinks,1,self._fwconfig_import_gateway)

        # Act

        new_links, deleted_links_ids = self._fwconfig_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(new_links) == 1, f"expected {1} new rulebase link, got {len(new_links)}")
        self.assertTrue(new_links[0]['from_rule_id'] == from_rule_id, f"expected last rulebase link to have from_rule_id {from_rule_id}, got {new_links[0]['from_rule_id']}")
        self.assertTrue(new_links[0]['from_rulebase_id'] == from_rulebase_id, f"expected last rulebase link to have from_rulebase_id {from_rulebase_id}, got {new_links[0]['from_rulebase_id']}")
        self.assertTrue(new_links[0]['to_rulebase_id'] == to_rulebase_id, f"expected last rulebase link to point to new rulebase id {to_rulebase_id}, got {new_links[0]['to_rulebase_id']}")
        self.assertTrue(new_links[0]['is_section'] == False, "expected last rulebase link to have is_section false, got true")
        self.assertTrue(len(deleted_links_ids) == 1, f"expected {1} new rulebase link, got {len(deleted_links_ids)}")
        self.assertTrue(deleted_links_ids[0] == self._fwconfig_import_gateway._rb_link_controller.rb_links[-1].id)
    

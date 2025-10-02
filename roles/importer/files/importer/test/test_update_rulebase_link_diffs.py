import copy
import unittest

from fwo_base import init_service_provider

from models.fwconfig_normalized import FwConfigNormalized

from models.rulebase import Rulebase
from services.service_provider import ServiceProvider

from test.mocking.mock_import_state import MockImportStateController
from test.mocking.mock_config import MockFwConfigNormalizedBuilder
from test.mocking.mock_fwconfig_import_gateway import MockFwConfigImportGateway
from test.tools.set_up_test import update_rb_links, update_rule_map_and_rulebase_map, update_rule_num_numerics, remove_rule_from_rulebase

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
        
        # Move last five rules of last rulebase to new rulebase (previous config).

        last_rulebase = self._previous_config.rulebases[-1]
        last_five_rules_uids = list(last_rulebase.Rules.keys())[-5:]

        _, new_rulebase_uid = self._config_builder.add_rulebase(self._previous_config, self._mgm_uid)

        for rule_uid in last_five_rules_uids:
            rule = last_rulebase.Rules.pop(rule_uid)
            self._config_builder.add_rule(self._previous_config, new_rulebase_uid, rule.model_dump())
        
        # Create rulebase link for cp_section header (previous config)

        last_rulebase_last_rule_uid = list(last_rulebase.Rules.keys())[-1]
        gateway = self._previous_config.gateways[0]
        self._config_builder.add_cp_section_header(gateway, last_rulebase.uid, new_rulebase_uid, last_rulebase_last_rule_uid)

        update_rule_map_and_rulebase_map(self._previous_config, self._import_state)
        update_rule_num_numerics(self._previous_config)
        update_rb_links(gateway.RulebaseLinks, 1, self._fwconfig_import_gateway)

        # Act

        _, deleted_links_ids = self._fwconfig_import_gateway.update_rulebase_link_diffs()

        # Assert
        
        self.assertTrue(deleted_links_ids[0] == self._fwconfig_import_gateway._rb_link_controller.rb_links[-1].id)


    def test_add_inline_layer(self):
                
        # Arrange

        from_rule_id, from_rulebase_id, to_rulebase_id = self._add_inline_layer(self._normalized_config)
        
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

        _, _, _ = self._add_inline_layer(self._previous_config)

        # Act

        _, deleted_links_ids = self._fwconfig_import_gateway.update_rulebase_link_diffs()

        # Assert

        self.assertTrue(len(deleted_links_ids) == 1, f"expected {1} new rulebase link, got {len(deleted_links_ids)}")
        self.assertTrue(deleted_links_ids[0] == self._fwconfig_import_gateway._rb_link_controller.rb_links[-1].id)


    def test_move_inline_layer(self):
        # Arrange

        from_rule_id, from_rulebase_id, to_rulebase_id = self._add_inline_layer(self._previous_config)
        inline_layer_rulebase = next((rb for rb in self._previous_config.rulebases if rb.uid == to_rulebase_id), None)
        inline_layer_rulebase_copy = copy.deepcopy(inline_layer_rulebase)
        first_rulebase = self._normalized_config.rulebases[0]
        from_rule_uid = list(first_rulebase.Rules.keys())[0]
        from_rulebase_uid = first_rulebase.uid
        from_rule_id, from_rulebase_id, _ = self._add_inline_layer(self._normalized_config, from_rulebase_uid, from_rule_uid, rulebase = inline_layer_rulebase_copy)

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
    

    def _add_inline_layer(self, config: FwConfigNormalized, from_rulebase_uid: str = "", from_rule_uid: str = "", index: int = 0, rulebase: Rulebase = None):

        if from_rulebase_uid == "":
            rulebase = config.rulebases[-1]
            from_rulebase_uid = rulebase.uid

        if from_rule_uid == "":

            if not rulebase:
                rulebase = next((rb for rb in config.rulebases if rb.uid == from_rulebase_uid), None)
                from_rule_uid = list(rulebase.Rules.keys())[-1]

        if not rulebase:
            _, to_rulebase_uid = self._config_builder.add_rulebase(config, self._mgm_uid)
            self._config_builder.add_rule(config, to_rulebase_uid)
        else:
            to_rulebase_uid = rulebase.uid
            config.rulebases.append(rulebase)

        gateway = config.gateways[0]
        self._config_builder.add_inline_layer(gateway, from_rulebase_uid, to_rulebase_uid, from_rule_uid, index)

        update_rule_map_and_rulebase_map(config, self._import_state)
        update_rb_links(gateway.RulebaseLinks, 1, self._fwconfig_import_gateway)

        return self._lookup_ids_for_rulebase_link(from_rule_uid, from_rulebase_uid, to_rulebase_uid)
    

    def _lookup_ids_for_rulebase_link(self, from_rule_uid : str = "", from_rulebase_uid : str = "", to_rulebase_uid : str = ""):

        from_rule_id = None
        from_rulebase_id = None
        to_rulebase_id = None

        if from_rule_uid != "":
            from_rule_id = self._import_state.lookupRule(from_rule_uid)
        if from_rulebase_uid != "":
            from_rulebase_id = self._import_state.lookupRulebaseId(from_rulebase_uid)
        if to_rulebase_uid != "":
            to_rulebase_id = self._import_state.lookupRulebaseId(to_rulebase_uid)

        return from_rule_id, from_rulebase_id, to_rulebase_id


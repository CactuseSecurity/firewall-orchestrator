import unittest
import sys
import os
import copy

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

from importer.models.rule import RuleNormalized
from importer.model_controllers.fwconfig_import_ruleorder import RuleOrderService
from test.tools.set_up_test import set_up_test_for_ruleorder_test_with_relevant_changes
from test.mocking.mock_config import MockFwConfigNormalized
from test.mocking.mock_fwconfig_import_rule import MockFwConfigImportRule

class TestFwoConfigImportRule(unittest.TestCase):

        
    def test_initialized(self):

        # Arrange
        previous_config, fwconfig_import_rule, rule_uids = set_up_test_for_ruleorder_test_with_relevant_changes()
        rule_order_service = RuleOrderService()
        # fwconfig_import_rule.ImportDetails.setup_response((1, 2), {'a': 3}, 'Erwartete Antwort')

        # Act
        rule_order_service.initialize(previous_config, fwconfig_import_rule)
        # fwconfig_import_rule.ImportDetails.call(1, 3, a=3)

        # Assert
        # self.assertEqual(fwconfig_import_rule.ImportDetails.call_log, [((1, 2), {'a': 3})]) 
        # self.assertFalse(True)
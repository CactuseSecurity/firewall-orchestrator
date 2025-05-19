import unittest
import sys
import os

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

from importer.model_controllers.fwconfig_import_ruleorder import RuleOrderService
from test.tools.set_up_test import set_up_test_for_ruleorder_test_with_relevant_changes


class TestFwoConfigImportRule(unittest.TestCase):

    @unittest.skip("Temporary deactivated, because necessary feature in mock class (mocking api calls) is not implemented yet.")    
    def test_initialized(self):

        # Arrange
        previous_config, fwconfig_import_rule, _ = set_up_test_for_ruleorder_test_with_relevant_changes()
        rule_order_service = RuleOrderService()
        fwconfig_import_rule.ImportDetails.setup_response((1, 2), {'a': 3}, 'Erwartete Antwort')

        # Act
        rule_order_service.initialize(previous_config, fwconfig_import_rule)

        # Assert


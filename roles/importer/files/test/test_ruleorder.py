import asyncio
import unittest
import sys
import os

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

from importer.model_controllers.fwconfig_import_rule import resetOrderNumbersAsync
from test.mocking.mock_config import ConfigMocker
from test.mocking.mock_import_state import MockImportState

class TestRuleOrdering(unittest.TestCase):
    config_mocker = ConfigMocker()

    async def test_rule_ordering_new_import_creates_correct_mutation(self):
        # arrange
        number_config = [10] # one rulebase with ten rules
        config, rules_uids = self.config_mocker.create_config(True, number_config=number_config)
        rules_uids_with_order_number = {}
        count = 1
        for rule_uid in rules_uids:
            rules_uids_with_order_number[rule_uid] = count
            count += 1
        mock_import_state = MockImportState() # instead of a real request this mock gathers the arguments that fwo_api_oo.call would get in a real scenario
        batch_size = 2
        expected_query = '\n        mutation UpdateRuleOrder($updates: [rule_updates!]!) {\n            update_rule_many(updates: $updates) {\n                affected_rows\n            }\n        }\n    '

        # act
        await resetOrderNumbersAsync(config.rulebases, batch_size, 2, mock_import_state)

        # assert
        self.assertTrue(len(mock_import_state.previous_calls) == batch_size) # one call per batch should be sent
        count_asserts = 1
        for mocked_call in mock_import_state.previous_calls:
            self.assertTrue(mocked_call["query"]==expected_query) # the query is always the same
            for update in mocked_call["variables"]["updates"]:
                # check rule_uid
                expected_rule_uid = rules_uids[count_asserts -1]
                actual_rule_uid = update["where"]["rule_uid"]["_eq"]
                self.assertEqual(expected_rule_uid, actual_rule_uid)

                # check rule_num_numeric
                expected_rule_num_numeric = rules_uids_with_order_number[actual_rule_uid]
                actual_rule_num_numeric = update["_set"]["rule_num_numeric"]
                self.assertEqual(expected_rule_num_numeric, actual_rule_num_numeric)

                count_asserts += 1


if __name__ == '__main__':
    unittest.main()

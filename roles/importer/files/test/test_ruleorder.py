import asyncio
import unittest
import sys
import os
import copy

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

from test.mocking.mock_fwconfig_import_rule import MockFwConfigImportRule
from importer.model_controllers.fwconfig_import_rule import compute_min_moves
from importer.models.fwconfig_normalized import FwConfigNormalized
from test.mocking.mock_config import ConfigMocker, MockFwConfigNormalized

class TestRuleOrdering(unittest.TestCase):
    config_mocker = ConfigMocker()


    def test_compute_min_moves_on_insert(self):
        # arrange
        previousConfigRuleUids = self.mock_previous_config_rule_uids()
        currentConfigRuleUids = copy.deepcopy(previousConfigRuleUids)
        new_uid = "500d884f-be27-4db4-9dd8-ec2b87ece474"
        currentConfigRuleUids.insert(4, new_uid)
        expectedResult = { 
            "moves": 1, 
            "operations": ["Insert element '500d884f-be27-4db4-9dd8-ec2b87ece474' at target position 4."],
            "insertions": [(4, '500d884f-be27-4db4-9dd8-ec2b87ece474')],
            "deletions": [],
            "reposition_moves": []
        }

        # act
        compute_min_moves_result = compute_min_moves(previousConfigRuleUids, currentConfigRuleUids)

        # assert
        self.assertEqual(compute_min_moves_result["moves"], expectedResult["moves"])
        self.assertEqual(compute_min_moves_result["operations"], expectedResult["operations"])
        self.assertEqual(compute_min_moves_result["insertions"], expectedResult["insertions"])
        self.assertEqual(compute_min_moves_result["deletions"], expectedResult["deletions"])     
        self.assertEqual(compute_min_moves_result["reposition_moves"], expectedResult["reposition_moves"])      

    def test_compute_min_moves_on_delete(self):
        # arrange
        previousConfigRuleUids = self.mock_previous_config_rule_uids()
        currentConfigRuleUids = copy.deepcopy(previousConfigRuleUids)
        del currentConfigRuleUids[4]
        expectedResult = { 
            "moves": 1, 
            "operations": ["Delete element 'e40d5b53-67e0-4ba7-923e-226909fc3c82' at source index 4."],
            "insertions": [],
            "deletions": [(4, 'e40d5b53-67e0-4ba7-923e-226909fc3c82')],
            "reposition_moves": []
        }

        # act
        compute_min_moves_result = compute_min_moves(previousConfigRuleUids, currentConfigRuleUids)

        # assert
        self.assertEqual(compute_min_moves_result["moves"], expectedResult["moves"])
        self.assertEqual(compute_min_moves_result["operations"], expectedResult["operations"])
        self.assertEqual(compute_min_moves_result["insertions"], expectedResult["insertions"])
        self.assertEqual(compute_min_moves_result["deletions"], expectedResult["deletions"])     
        self.assertEqual(compute_min_moves_result["reposition_moves"], expectedResult["reposition_moves"])      



    def test_compute_min_moves_on_move(self):
        # arrange
        previousConfigRuleUids = self.mock_previous_config_rule_uids()
        currentConfigRuleUids = copy.deepcopy(previousConfigRuleUids)
        moving_element = currentConfigRuleUids.pop(8)
        currentConfigRuleUids.insert(4, moving_element)
        expectedResult = { "moves": 1, "operations": ["Pop element 'a5d42650-43d5-4cab-9bc8-48cbf902fa34' from source index 8 and reinsert at target position 4."]}
        expectedResult = { 
            "moves": 1, 
            "operations": ["Pop element 'a5d42650-43d5-4cab-9bc8-48cbf902fa34' from source index 8 and reinsert at target position 4."],
            "insertions": [],
            "deletions": [],
            "reposition_moves": [(8, 'a5d42650-43d5-4cab-9bc8-48cbf902fa34', 4)]
        }
        # act
        compute_min_moves_result = compute_min_moves(previousConfigRuleUids, currentConfigRuleUids)

        # assert
        self.assertEqual(compute_min_moves_result["moves"], expectedResult["moves"])
        self.assertEqual(compute_min_moves_result["operations"], expectedResult["operations"])
        self.assertEqual(compute_min_moves_result["insertions"], expectedResult["insertions"])
        self.assertEqual(compute_min_moves_result["deletions"], expectedResult["deletions"])     
        self.assertEqual(compute_min_moves_result["reposition_moves"], expectedResult["reposition_moves"])      

    def test_compute_min_moves_all_cases(self):
        # arrange
        previousConfigRuleUids = self.mock_previous_config_rule_uids()
        currentConfigRuleUids = copy.deepcopy(previousConfigRuleUids)

        del currentConfigRuleUids[2]

        new_uid = "500d884f-be27-4db4-9dd8-ec2b87ece474"
        currentConfigRuleUids.insert(3, new_uid)

        moving_element = currentConfigRuleUids.pop(8)
        currentConfigRuleUids.insert(4, moving_element)

        expectedResult = { 
            "moves": 3, 
            "operations": [
                "Delete element 'f31d21f5-2b5d-47e8-9011-cd16695f5644' at source index 2.", 
                "Insert element '500d884f-be27-4db4-9dd8-ec2b87ece474' at target position 3.",
                "Pop element 'a5d42650-43d5-4cab-9bc8-48cbf902fa34' from source index 8 and reinsert at target position 4."
            ],
            "insertions": [(3, '500d884f-be27-4db4-9dd8-ec2b87ece474')],
            "deletions": [(2, 'f31d21f5-2b5d-47e8-9011-cd16695f5644')],
            "reposition_moves": [(8, 'a5d42650-43d5-4cab-9bc8-48cbf902fa34', 4)]
        }


        # act
        compute_min_moves_result = compute_min_moves(previousConfigRuleUids, currentConfigRuleUids)

        # assert
        self.assertEqual(compute_min_moves_result["moves"], expectedResult["moves"])
        self.assertEqual(compute_min_moves_result["operations"], expectedResult["operations"])
        self.assertEqual(compute_min_moves_result["insertions"], expectedResult["insertions"])
        self.assertEqual(compute_min_moves_result["deletions"], expectedResult["deletions"])     
        self.assertEqual(compute_min_moves_result["reposition_moves"], expectedResult["reposition_moves"])       

    def test_compute_min_moves_complex_case(self):
        # arrange
        previousConfigRuleUids = self.mock_previous_config_rule_uids(longer_version=True)
        currentConfigRuleUids = copy.deepcopy(previousConfigRuleUids)

        del currentConfigRuleUids[3]
        del currentConfigRuleUids[11]
        currentConfigRuleUids.insert(5, "ec4d9310-0ebe-4321-a342-015a31863029")
        currentConfigRuleUids.insert(11, "8dea22c7-ad09-4f2f-8896-f911d5eb12f4")
        moving_element = currentConfigRuleUids.pop(9)
        currentConfigRuleUids.insert(7, moving_element)
        moving_element = currentConfigRuleUids.pop(13)
        currentConfigRuleUids.insert(1, moving_element)

        expectedResult = {
            "moves": 6,
            "operations": [
                "Delete element 'fb914729-4858-491f-9816-a78ec17a8b83' at source index 3.", 
                "Delete element '10b2e5d2-5143-402c-940a-716a8242dc38' at source index 12.", 
                "Insert element 'ec4d9310-0ebe-4321-a342-015a31863029' at target position 6.", 
                "Insert element '8dea22c7-ad09-4f2f-8896-f911d5eb12f4' at target position 12.", 
                "Pop element '2c18bbd4-fa8e-475a-9268-f15f1d4dd6f5' from source index 9 and reinsert at target position 8.", 
                "Pop element '4fd40964-61b4-471c-aa47-b28c6aa56a63' from source index 13 and reinsert at target position 1."
            ],
            "insertions": [(6, 'ec4d9310-0ebe-4321-a342-015a31863029'), (12, '8dea22c7-ad09-4f2f-8896-f911d5eb12f4')],
            "deletions": [(3, 'fb914729-4858-491f-9816-a78ec17a8b83'), (12, '10b2e5d2-5143-402c-940a-716a8242dc38')],
            "reposition_moves": [(9, '2c18bbd4-fa8e-475a-9268-f15f1d4dd6f5', 8), (13, '4fd40964-61b4-471c-aa47-b28c6aa56a63', 1)]
        }

        # act
        compute_min_moves_result = compute_min_moves(previousConfigRuleUids, currentConfigRuleUids)

        # assert
        self.assertEqual(compute_min_moves_result["moves"], expectedResult["moves"])
        self.assertEqual(compute_min_moves_result["operations"], expectedResult["operations"])
        self.assertEqual(compute_min_moves_result["insertions"], expectedResult["insertions"])
        self.assertEqual(compute_min_moves_result["deletions"], expectedResult["deletions"])     
        self.assertEqual(compute_min_moves_result["reposition_moves"], expectedResult["reposition_moves"])      

    def test_update_rulebase_diffs_same_config(self):
        # arrange
        previous_config = MockFwConfigNormalized()
        previous_config.initialize_config(
            {
                "rule_config": [10,10,10]
            }
        )

        fwconfig_import_rule = MockFwConfigImportRule()
        fwconfig_import_rule.NormalizedConfig = copy.deepcopy(previous_config)

        # act
        fwconfig_import_rule.update_rulebase_diffs(previous_config)

        # assert
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleAddCount, 0)
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleDeleteCount, 0)
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleChangeCount, 0)
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleMoveCount, 0)

    def test_update_rulebase_diffs_delete(self):
        # arrange
        previous_config = MockFwConfigNormalized()
        previous_config.initialize_config(
            {
                "rule_config": [10,10,10]
            }
        )

        fwconfig_import_rule = MockFwConfigImportRule()
        fwconfig_import_rule.NormalizedConfig = copy.deepcopy(previous_config)
        deleted_rule = list(fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules.keys())[0]
        del fwconfig_import_rule.NormalizedConfig.rulebases[0].Rules[deleted_rule]


        # act
        fwconfig_import_rule.update_rulebase_diffs(previous_config)

        # assert
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleAddCount, 0)
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleDeleteCount, 1)
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleChangeCount, 0)
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleMoveCount, 0)

    def test_update_rulebase_diffs_insert(self):
        # arrange
        previous_config = MockFwConfigNormalized()
        previous_config.initialize_config(
            {
                "rule_config": [10,10,10]
            }
        )

        fwconfig_import_rule = MockFwConfigImportRule()
        fwconfig_import_rule.NormalizedConfig = copy.deepcopy(previous_config)
        fwconfig_import_rule.NormalizedConfig.add_rule_to_rulebase(fwconfig_import_rule.NormalizedConfig.rulebases[0].uid)

        # act
        fwconfig_import_rule.update_rulebase_diffs(previous_config)

        # assert
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleAddCount, 1)
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleDeleteCount, 0)
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleChangeCount, 0)
        self.assertEqual(fwconfig_import_rule.ImportDetails.Stats.RuleMoveCount, 0)

    # TODO: Do that via mock_config.py

    def mock_previous_config_rule_uids(self, with_rule_num_numeric = False, longer_version = False):
        data = {
            "5fe42dfa-47a0-41b9-b90e-cdde198cd651": 1,
            "55f11a6a-f14f-4802-9af2-52309c81af23": 2,
            "f31d21f5-2b5d-47e8-9011-cd16695f5644": 3,
            "fb914729-4858-491f-9816-a78ec17a8b83": 4,
            "e40d5b53-67e0-4ba7-923e-226909fc3c82": 5,
            "d96a70ca-bb57-49a6-93c9-53344caa5f02": 6,
            "1b4658f3-807b-4956-a01e-be5bf5090803": 7,
            "41925458-36ec-42ab-9e25-90220a19a4d9": 8,
            "a5d42650-43d5-4cab-9bc8-48cbf902fa34": 9,
            "2c18bbd4-fa8e-475a-9268-f15f1d4dd6f5": 10
        }

        if longer_version:
            data["b9018dcd-1362-4c5f-911b-445936b065f6"] = 11
            data["1c82f131-1202-4c1b-9919-85f3a5bbbc2f"] = 12
            data["10b2e5d2-5143-402c-940a-716a8242dc38"] = 13
            data["4fd40964-61b4-471c-aa47-b28c6aa56a63"] = 14
            data["ec783b63-5feb-40cf-8bc6-72094fabbc45"] = 15

        if with_rule_num_numeric:
            return data
        else:
            return list(data.keys())

if __name__ == '__main__':
    unittest.main()

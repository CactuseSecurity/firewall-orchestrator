import unittest
import sys
import os
import unittest
import time
from unittest.mock import patch, Mock, MagicMock

# Adding the relative path to sys.path
sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

# Import the function to test

from fwconfig_import_rule import FwConfigImportRule
from fwoBaseImport import ImportState
from fwo_base import ConfigAction
from fwconfig_base import FwConfig
from fwo_globals import setGlobalValues
from fwo_const import dummy_ip




class TestApiDatabaseIntegration(unittest.TestCase):

    @staticmethod
    def mock_import_state():
        # Mock the ImportState class
        mock_import_state = MagicMock()

        # Set the default attribute values that the __init__ method would have initialized
        mock_import_state.ErrorCount = 0
        mock_import_state.ChangeCount = 0
        mock_import_state.ErrorString = ''
        mock_import_state.StartTime = int(time.time())
        mock_import_state.DebugLevel = 3  # Example debug level
        mock_import_state.Config2import = {
            "network_objects": [],
            "service_objects": [],
            "user_objects": [],
            "zone_objects": [],
            "rules": []
        }
        mock_import_state.ConfigChangedSinceLastImport = True
        mock_import_state.FwoConfig = MagicMock()  # Mocked FworchConfig object
        mock_import_state.MgmDetails = MagicMock()  # Mocked ManagementDetails object
        mock_import_state.FullMgmDetails = {}
        mock_import_state.ImportId = None
        mock_import_state.Jwt = 'mock_jwt_token'
        mock_import_state.ImportFileName = None
        mock_import_state.ForceImport = 'mock_force_value'
        mock_import_state.ImportVersion = 8
        mock_import_state.DataRetentionDays = 30
        mock_import_state.DaysSinceLastFullImport = 5
        mock_import_state.LastFullImportId = 12345
        mock_import_state.IsFullImport = False

        return mock_import_state

        # Example usage of the mock
        #print(mock_import_state.DebugLevel)  # Should print the mocked DebugLevel


    @staticmethod
    def mockNormalizedConfig():

        svcObj1 = {'svc_uid': 'svcObj1Uid', 'svc_name': 'tcp_1234', 'svc_color': 'black', 'svc_comment': 'test svc1', 'svc_typ': 'simple', 'svc_port': 1234, 'svc_port_end': 1234, 'svc_member_refs': '', 'svc_member_names': '', 'ip_proto': 6, 'svc_timeout': None, 'rpc_nr': None}
        svcObj2 = {'svc_uid': 'svcObj2Uid', 'svc_name': 'tcp_456', 'svc_color': 'black', 'svc_comment': 'test svc1', 'svc_typ': 'simple', 'svc_port': 456, 'svc_port_end': 456, 'svc_member_refs': '', 'svc_member_names': '', 'ip_proto': 6, 'svc_timeout': None, 'rpc_nr': None}
        nwObj1 = {'obj_uid': 'nwObj1Uid', 'obj_name': 'nwObj1', 'obj_color': 'black', 'obj_comment': None, 'obj_typ': 'host', 'obj_ip': '10.5.1.100/32', 'obj_ip_end': '10.5.1.100/32', 'obj_member_refs': None, 'obj_member_names': None}
        nwObj2 = {'obj_uid': 'nwObj2Uid', 'obj_name': 'nwObj2', 'obj_color': 'black', 'obj_comment': None, 'obj_typ': 'network', 'obj_ip': '192.168.200.0/32', 'obj_ip_end': '192.168.200.255/32', 'obj_member_refs': None, 'obj_member_names': None}
        nwObj3 = {'obj_uid': 'AnyUid', 'obj_name': 'Any', 'obj_color': 'black', 'obj_comment': None, 'obj_typ': 'network', 'obj_ip': '0.0.0.0/32', 'obj_ip_end': '255.255.255.255/32', 'obj_member_refs': None, 'obj_member_names': None}
        rule1 = {'rule_num': 0, 'rule_disabled': True, 'rule_src_neg': False, 'rule_src': 'nwObj1|nwObj2', 'rule_src_refs': 'nwObj1Uid|nwObj2Uid', 'rule_dst_neg': False, 'rule_dst': 'nwobj2', 'rule_dst_refs': 'nwObj2Uid', 'rule_svc_neg': False, 'rule_svc': 'svcObj1|svcObj2', 'rule_svc_refs': 'svcObj1Uid|svcObj2Uid', 'rule_action': 'Drop', 'rule_track': 'Log', 'rule_installon': 'Policy Targets', 'rule_time': 'Any', 'rule_name': None, 'rule_uid': '828b0f42-4b18-4352-8bdf-c9c864d692eb', 'rule_custom_fields': "{'field-1': '', 'field-2': '', 'field-3': ''}", 'rule_implied': False, 'rule_type': 'access', 'rule_last_change_admin': 'tim-admin', 'parent_rule_uid': None, 'last_hit': None, 'rule_comment': 'cooment with apostrophes .,,j'}
        newRule = {'rule_num': 1, 'rule_uid': 'newRuleUid', 'rule_disabled': False, 'rule_src_neg': False, 'rule_src': 'Any', 'rule_src_refs': 'nwObj3Uid', 'rule_dst_neg': False, 'rule_dst': 'nwObj2', 'rule_dst_refs': 'nwObj2Uid', 'rule_svc_neg': False, 'rule_svc': 'svcobj2', 'rule_svc_refs': 'svcObj2Uid', 'rule_action': 'Accept', 'rule_track': 'Log', 'rule_installon': 'Policy Targets', 'rule_time': 'Any', 'rule_implied': False, 'rule_head_text': '', 'parent_rule_uid': ''}
        rule2 = {'rule_num': 2, 'rule_uid': 'rule2Uid', 'rule_disabled': False, 'rule_src_neg': False, 'rule_src': 'Any', 'rule_src_refs': 'AnyUid', 'rule_dst_neg': False, 'rule_dst': 'nwObj3', 'rule_dst_refs': 'nwObj3Uid', 'rule_svc_neg': False, 'rule_svc': 'svcobj2', 'rule_svc_refs': 'svcObj2Uid', 'rule_action': 'Accept', 'rule_track': 'Log', 'rule_installon': 'Policy Targets', 'rule_time': 'Any', 'rule_implied': False, 'rule_head_text': 'DMZ_E', 'parent_rule_uid': ''}
        gw1 ={ 'EnforcedNatPolicyUids': ['TestPolicyWithLayers']}

        mock_fw_config = MagicMock()

        # Mock the FwConfigNormalized class
        mock_fw_config_normalized = MagicMock()
        
        # Set the attributes
        mock_fw_config_normalized.action = MagicMock()  # Mocked ConfigAction object
        mock_fw_config_normalized.action = ConfigAction.INSERT
        mock_fw_config_normalized.network_objects = { 'nwObj1Uid': nwObj1, 'nwObj2Uid': nwObj2, 'nwObj3Uid': nwObj3 }
        mock_fw_config_normalized.service_objects = { 'svcObj1Uid': svcObj1, 'svcObj2Uid': svcObj2}
        mock_fw_config_normalized.users = {}
        mock_fw_config_normalized.zone_objects = {}
        mock_fw_config_normalized.Rules = { 
            'rulebase1': {'Name': 'rulebase1', 'Uid': 'rulebase1', 'Rules': { 'rule1Uid': rule1, 'newRuleUid': newRule, 'rule2Uid': rule2 } }, 
            'rulebase2': {'Name': 'rulebase2', 'Uid': 'rulebase2', 'Rules': { 'rule1Uid': rule2, 'rule2Uid': rule1 }} 
        }
        mock_fw_config_normalized.gateways = []
        mock_fw_config_normalized.IsSuperManagerConfig = False

        return mock_fw_config_normalized


    @patch('fwconfig_base.FwConfig')  # Mocking the parent class
    @patch('fwconfig_normalized.FwConfigNormalized')  # Mocking the child class
    @patch('fwconfig_import_rule.FwConfigImportRule.setNewRulesNumbering')
    @patch('fwconfig_import_rule.FwConfigImportRule.addNewRules')
    def test_api_data_insertion(self, MockFwConfigNormalized, MockFwConfig, mock_addNewRules, mock_getCurrentRules):
        importState = self.mock_import_state()

        mock_fw_config = MockFwConfig.return_value
        mock_fw_config.action = 'INSERT'

        # config = self.mockNormalizedConfig()


        svcObj1 = {'svc_uid': 'svcObj1Uid', 'svc_name': 'tcp_1234', 'svc_color': 'black', 'svc_comment': 'test svc1', 'svc_typ': 'simple', 'svc_port': 1234, 'svc_port_end': 1234, 'svc_member_refs': '', 'svc_member_names': '', 'ip_proto': 6, 'svc_timeout': None, 'rpc_nr': None}
        svcObj2 = {'svc_uid': 'svcObj2Uid', 'svc_name': 'tcp_456', 'svc_color': 'black', 'svc_comment': 'test svc1', 'svc_typ': 'simple', 'svc_port': 456, 'svc_port_end': 456, 'svc_member_refs': '', 'svc_member_names': '', 'ip_proto': 6, 'svc_timeout': None, 'rpc_nr': None}
        nwObj1 = {'obj_uid': 'nwObj1Uid', 'obj_name': 'nwObj1', 'obj_color': 'black', 'obj_comment': None, 'obj_typ': 'host', 'obj_ip': '10.5.1.100/32', 'obj_ip_end': '10.5.1.100/32', 'obj_member_refs': None, 'obj_member_names': None}
        nwObj2 = {'obj_uid': 'nwObj2Uid', 'obj_name': 'nwObj2', 'obj_color': 'black', 'obj_comment': None, 'obj_typ': 'network', 'obj_ip': '192.168.200.0/32', 'obj_ip_end': '192.168.200.255/32', 'obj_member_refs': None, 'obj_member_names': None}
        nwObj3 = {'obj_uid': 'AnyUid', 'obj_name': 'Any', 'obj_color': 'black', 'obj_comment': None, 'obj_typ': 'network', 'obj_ip': '0.0.0.0/32', 'obj_ip_end': '255.255.255.255/32', 'obj_member_refs': None, 'obj_member_names': None}
        rule1 = {'rule_num': 0, 'rule_disabled': True, 'rule_src_neg': False, 'rule_src': 'nwObj1|nwObj2', 'rule_src_refs': 'nwObj1Uid|nwObj2Uid', 'rule_dst_neg': False, 'rule_dst': 'nwobj2', 'rule_dst_refs': 'nwObj2Uid', 'rule_svc_neg': False, 'rule_svc': 'svcObj1|svcObj2', 'rule_svc_refs': 'svcObj1Uid|svcObj2Uid', 'rule_action': 'Drop', 'rule_track': 'Log', 'rule_installon': 'Policy Targets', 'rule_time': 'Any', 'rule_name': None, 'rule_uid': '828b0f42-4b18-4352-8bdf-c9c864d692eb', 'rule_custom_fields': "{'field-1': '', 'field-2': '', 'field-3': ''}", 'rule_implied': False, 'rule_type': 'access', 'rule_last_change_admin': 'tim-admin', 'parent_rule_uid': None, 'last_hit': None, 'rule_comment': 'cooment with apostrophes .,,j'}
        newRule = {'rule_num': 1, 'rule_uid': 'newRuleUid', 'rule_disabled': False, 'rule_src_neg': False, 'rule_src': 'Any', 'rule_src_refs': 'nwObj3Uid', 'rule_dst_neg': False, 'rule_dst': 'nwObj2', 'rule_dst_refs': 'nwObj2Uid', 'rule_svc_neg': False, 'rule_svc': 'svcobj2', 'rule_svc_refs': 'svcObj2Uid', 'rule_action': 'Accept', 'rule_track': 'Log', 'rule_installon': 'Policy Targets', 'rule_time': 'Any', 'rule_implied': False, 'rule_head_text': '', 'parent_rule_uid': ''}
        rule2 = {'rule_num': 2, 'rule_uid': 'rule2Uid', 'rule_disabled': False, 'rule_src_neg': False, 'rule_src': 'Any', 'rule_src_refs': 'AnyUid', 'rule_dst_neg': False, 'rule_dst': 'nwObj3', 'rule_dst_refs': 'nwObj3Uid', 'rule_svc_neg': False, 'rule_svc': 'svcobj2', 'rule_svc_refs': 'svcObj2Uid', 'rule_action': 'Accept', 'rule_track': 'Log', 'rule_installon': 'Policy Targets', 'rule_time': 'Any', 'rule_implied': False, 'rule_head_text': 'DMZ_E', 'parent_rule_uid': ''}
        gw1 ={ 'EnforcedNatPolicyUids': ['TestPolicyWithLayers']}

        rulesMocked = [
            {'rule_num': 4711, 'rule_num_numeric': 0.0, 'rule_uid': 'rule1Uid'},
            {'rule_num': 23, 'rule_num_numeric': 1.0, 'rule_uid': 'rule2Uid'}
        ]

        currentMockedRules = { 
            'rulebase1': {'Name': 'rulebase1', 'Uid': 'rulebase1', 'Rules': { 'rule1Uid': rule1, 'newRuleUid': newRule, 'rule2Uid': rule2 } }, 
            'rulebase2': {'Name': 'rulebase2', 'Uid': 'rulebase2', 'Rules': { 'rule1Uid': rule2, 'rule2Uid': rule1 }} 
        }

        mock_fw_config = MagicMock()

        # Mock the FwConfigNormalized class
        mock_fw_config_normalized = MagicMock()
        
        # Set the attributes
        mock_fw_config_normalized.action = MagicMock()  # Mocked ConfigAction object
        mock_fw_config_normalized.action = ConfigAction.INSERT
        mock_fw_config_normalized.network_objects = { 'nwObj1Uid': nwObj1, 'nwObj2Uid': nwObj2, 'nwObj3Uid': nwObj3 }
        mock_fw_config_normalized.service_objects = { 'svcObj1Uid': svcObj1, 'svcObj2Uid': svcObj2}
        mock_fw_config_normalized.users = {}
        mock_fw_config_normalized.zone_objects = {}
        mock_fw_config_normalized.Rules = currentMockedRules
        mock_fw_config_normalized.gateways = []
        mock_fw_config_normalized.IsSuperManagerConfig = False

        # mock_fw_config_normalized.assert_called_once()

        # Mock API response
        mock_getCurrentRules.return_value = rulesMocked

        previousRulesRb1 = [
            {'rule_num': 0, 'rule_uid': 'rule1Uid'},
            {'rule_num': 1, 'rule_uid': 'rule2Uid'}
        ]

        newRulesRb1 = [
            {'rule_num': 0, 'rule_uid': 'rule1Uid'},
            {'rule_num': 1, 'rule_uid': 'newRuleUid'},
            {'rule_num': 2, 'rule_uid': 'rule2Uid'}
        ]

        # Mock DB insert function to just return True (as if insertion was successful)
        mock_addNewRules.return_value = True

        # mock GetRuleNumMap
        mock_GetRuleNumMap = MagicMock()
        mock_GetRuleNumMap.return_value = rulesMocked

        # mock stm getters
        mock_GetTrackMap = MagicMock()
        mock_GetActionMap = MagicMock()
        mock_GetTrackMap.return_value = ['log', 'none']
        mock_GetActionMap.return_value = ['drop', 'accept']

        # Instantiate your main application logic
        ruleImporter = FwConfigImportRule(importState, mock_fw_config_normalized)

        # Suppose this is the method that combines API fetching and DB insertion
        currentRules = ruleImporter.getCurrentRules(123, 234, 'rulebase1')
        ruleImporter.setNewRulesNumbering(previousRulesRb1)

        success = ruleImporter.addNewRules()

        # Asserts to ensure everything works as expected
        # mock_getCurrentRules.assert_called_once()
        # mock_addNewRules.assert_called_once_with(currentRules)

        # mock_getCurrentRules.assertEqual(currentRules, newRulesRb1)
        # self.assertEqual(currentRules, newRulesRb1)
        self.assertEqual(currentMockedRules, newRulesRb1)
        self.assertTrue(success)

if __name__ == '__main__':
    setGlobalValues (suppress_cert_warnings_in=True, verify_certs_in=False, debug_level_in=0)

    unittest.main()


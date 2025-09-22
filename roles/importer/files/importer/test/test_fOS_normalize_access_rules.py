import unittest
from unittest.mock import MagicMock, patch
import sys
import os

sys.path.append(os.path.join(os.path.dirname(__file__), '../importer'))

# from fortiosmanagementREST.fOS_rule import normalize_access_rules
# import fortiosmanagementREST.fOS_service

class TestNormalizeAccessRules(unittest.TestCase):
    @patch('fortiosmanagementREST.fOS_zone.add_zone_if_missing', return_value='zone_1')
    @patch('fortiosmanagementREST.fOS_rule.resolve_objects', side_effect=lambda name, **kwargs: f'ref_{name}')
    @patch('fortiosmanagementREST.fOS_common.add_users_to_rule')
    @patch('fwo_log.getFwoLogger')
    def test_basic_rule_normalization(self, mock_logger, mock_add_users, mock_resolve, mock_add_zone):
        global list_delimiter
        list_delimiter = ','

        full_config = {
            'rules': {
                'rules': [
                    {
                        'policyid': 1,
                        'uuid': 'abc-123',
                        'action': 'accept',
                        'status': 'enable',
                        'logtraffic': 'utm',
                        '_last_hit': 1722796800,  # 2024-08-05
                        'srcaddr': [{'name': 'src1'}],
                        'dstaddr': [{'name': 'dst1'}],
                        'service': [{'name': 'svc1'}],
                        'srcaddr6': [],
                        'dstaddr6': [],
                        'srcintf': [{'name': 'if1'}],
                        'dstintf': [{'name': 'if2'}],
                        'srcaddr-negate': 'disable',
                        'dstaddr-negate': 'disable',
                        'service-negate': 'disable',
                        'comments': 'some comment'
                    }
                ]
            },
            'nw_obj_lookup_dict': {}
        }
        config2import = {}
        mgm_details = MagicMock()
        mgm_details.Devices = [{'name': 'firewall1'}]

        # normalize_access_rules(full_config, config2import, import_id=42, mgm_details=mgm_details)

        rules = config2import.get('rules')
        self.assertEqual(len(rules), 1)
        rule = rules[0]

        self.assertEqual(rule['rule_ruleid'], 1)
        self.assertEqual(rule['rule_uid'], 'abc-123')
        self.assertEqual(rule['rule_action'], 'Accept')
        self.assertEqual(rule['rule_disabled'], False)
        self.assertEqual(rule['rule_track'], 'Log')
        self.assertEqual(rule['last_hit'], '2024-08-05')
        self.assertEqual(rule['rule_src_refs'], 'ref_src1')
        self.assertEqual(rule['rule_dst_refs'], 'ref_dst1')
        self.assertEqual(rule['rule_svc_refs'], 'svc1')
        self.assertEqual(rule['rule_from_zone'], 'zone_1')
        self.assertEqual(rule['rule_to_zone'], 'zone_1')
        self.assertEqual(rule['rule_comment'], 'some comment')

if __name__ == '__main__':
    unittest.main()

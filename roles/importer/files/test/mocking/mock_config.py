import json
import uuid
from typing import Union
from test.mocking.uid_manager import UidManager
from test.mocking.mock_rulebase import MockRulebase

""" 

DISCLAIMER

This script does create a config similar to the normalized config we get when we import and transform a config from sting. However, it is not very sophisticated at this point.
It simply creates an empty config with the bare minimum to import n rules. To make it work you have to deactivate the consistency checks and create only one rulebase. 

"""

class ConfigMocker:
    config_type = "normalized"
    file_name_prefix = ''
    tmp_dir = "/usr/local/fworch/tmp/import/"
    mgm_id = 3
    manager_uid = "6ae3760206b9bfbd2282b5964f6ea07869374f427533c72faa7418c28f7a77f2"
    manager_name = 'sting-mgmt'
    file_name = f'{file_name_prefix}mgm_id_{mgm_id}_config_{config_type}.json'
    default_file_path = tmp_dir + file_name
    default_number_config = [10000]
    uid_manager = UidManager()

    config = {}
    rules_uids = []
    rulebases = []

    def create_config(self, as_dict: bool = False, file_path: str = "", number_config: list = []) -> Union[dict, None]: 
        if number_config == []:
            number_config = self.default_number_config

        self.generate_rulebases(number_config)
        self.build_config()
        
        if as_dict:
            return self, self.rules_uids
        else:
            if file_path == "":
                file_path = self.default_file_path

            with open(file_path, "w") as file:
                json.dump(self.config, file, indent=4)
            print(f"Config file saved to {file_path}")

            return None

    def build_config(self):
        self.config = {
            "ConfigFormat": self.config_type.upper(),
            "ManagerSet": [
                {
                    "ManagerUid": self.manager_uid,
                    "ManagerName": self.manager_name,
                    "IsGlobal": False,
                    "DependantManagerUids": [],
                    "Configs": [
                        {
                            "ConfigFormat": "NORMALIZED_LEGACY",
                            "action": "INSERT",
                            "network_objects":{

                            },
                            "users":{

                            },
                            "zone_objects":{

                            },
                            "rulebases": [rulebase.to_dict() for rulebase in self.rulebases] or [],
                            "gateways":[
                                
                            ]
                        }  
                    ]
                }
            ]
        }

    def generate_rules(self, rules_number=0):
        rules = {}
        counter = 0

        while counter < rules_number:
            rule_uid = self.uid_manager.create_uid()
            rules[rule_uid]={
                "rule_num": 0,
                "rule_disabled": False,
                "rule_src_neg": False,
                "rule_src": "None",
                "rule_src_refs": "",
                "rule_dst_neg": False,
                "rule_dst": "None",
                "rule_dst_refs": "",
                "rule_svc_neg": False,
                "rule_svc": "None",
                "rule_svc_refs": "",
                "rule_action": "accept",
                "rule_track": "log",
                "rule_installon": "Policy Targets",
                "rule_time": "Any",
                "rule_name": "Rule",
                "rule_uid": rule_uid,
                "rule_custom_fields": "{'field-1': '', 'field-2': '', 'field-3': ''}",
                "rule_implied": False,
                "rule_type": "access",
                "rule_last_change_admin": "admin",
                "parent_rule_uid": None,
                "last_hit": None,
                "rule_comment": None,
                "rule_src_zone": None,
                "rule_dst_zone": None,
                "rule_head_text": None
            }
            self.rules_uids.append(rule_uid)
            counter +=1

        return rules

    def generate_rulebase(self, rules_number=0):
        rulebase = MockRulebase()
        rulebase.uid = self.uid_manager.create_uid()
        rulebase.mgm_uid = self.uid_manager.create_uid()
        rulebase.Rules = self.generate_rules(rules_number) or {}
        return rulebase

    def generate_rulebases(self, rulebase_config):
        for number_of_rules in rulebase_config:
            new_rulebase = self.generate_rulebase(number_of_rules)
            self.rulebases.append(new_rulebase)

if __name__ == '__main__':
    config_mocker = ConfigMocker()
    config_mocker.create_config()


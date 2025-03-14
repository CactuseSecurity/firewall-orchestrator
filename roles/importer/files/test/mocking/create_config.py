""" 

DISCLAIMER

This script does create a config similar to the normalized config we get when we import and transform a config from sting. However, it is not very sophisticated at this point.
It simply creates an empty config with the bare minimum to import n rules. To make it work you have to deactivate the consistency checks. 

"""

import json
import uuid
from typing import Union

as_dict = False
is_test = True

config_type = 'normalized'
file_name_prefix = ''
tmp_dir = '/usr/local/fworch/tmp/import/'
mgm_id = 3
manager_uid = '6ae3760206b9bfbd2282b5964f6ea07869374f427533c72faa7418c28f7a77f2'
manager_name = 'sting-mgmt'
n = 10000

file_name = f'{file_name_prefix}mgm_id_{mgm_id}_config_{config_type}.json'
file_path = tmp_dir + file_name

config = {}
used_uids = []

def create_config(as_dict: bool, file_path: str) -> Union[dict, None]: 
    if as_dict:
        return config
    else:
        with open(file_path, "w") as file:
            json.dump(config, file, indent=4)
        print(f"Config file saved to {file_path}")
        return None

def generate_rules(rules_number=0):
    rules = {}
    counter = 0

    while counter < rules_number:
        rule_uid = create_uid()
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
        counter +=1

    return rules

def generate_rulebase(rules_number=0):
    return {
        "id": None,
        "uid": create_uid(),
        "name": "Generated Rulebase",
        "mgm_uid": create_uid(),
        "is_global": False,
        "Rules": generate_rules(rules_number) or {}
    }

def generate_rulebases():
    return [
        generate_rulebase(n)
    ]
    
def build_config():
    return {
        "ConfigFormat": config_type.upper(),
        "ManagerSet": [
            {
                "ManagerUid": manager_uid,
                "ManagerName": manager_name,
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
                        "rulebases": generate_rulebases() or [],
                        "gateways":[
                            
                        ]
                    }  
                ]
            }
        ]
    }

def create_uid():
    need_new_uid = True
    new_uid = ""

    while need_new_uid:
        new_uid = str(uuid.uuid4())
        if not new_uid in used_uids:
            used_uids.append(new_uid)
            need_new_uid = False

    return new_uid

    



if __name__ == '__main__':
    config = build_config()
    create_config(as_dict, file_path)


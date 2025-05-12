import json
import uuid
from typing import Union, Dict

if __name__ == '__main__': # for usage as executable script
    from uid_manager import UidManager
    from mock_rulebase import MockRulebase
else: # for usage in unit tests
    from . uid_manager import UidManager
    from . mock_rulebase import MockRulebase
    from importer.models.fwconfig_normalized import FwConfigNormalized
    from importer.models.rulebase import Rulebase
    from importer.models.rule import Rule
    from pydantic import PrivateAttr
    from importer.fwo_const import rule_num_numeric_steps


class MockFwConfigNormalized(FwConfigNormalized):
    """
    A mock subclass of FwConfigNormalized for testing purposes.

    This class creates a fake firewall configuration including rulebases
    and rules, using a UID manager to generate unique identifiers.
    """

    _uid_manager: UidManager = PrivateAttr()

    def __init__(self, **kwargs):
        """
        Initializes the mock configuration with default action and an empty gateway list.
        """
        super().__init__(action="INSERT", gateways=[], **kwargs)
        self._uid_manager = UidManager()

    @property
    def uid_manager(self) -> UidManager:
        """
        Returns the internal UID manager.
        """
        return self._uid_manager

    def initialize_config(self, mock_config: Dict):
        """
        Initializes the mock configuration with rulebases and rules.

        Args:
            mock_config (dict): A dictionary with configuration values. Expected keys:
                - "rule_config": List of integers, each representing the number of rules per rulebase.
                - "initialize_rule_num_numeric": Optional boolean, if True assigns incremental numeric rule numbers.
        """
        mock_mgm_uid = self.uid_manager.create_uid()
        created_rule_counter = 1

        for number_of_rules in mock_config["rule_config"]:
            # Create a new rulebase
            new_rulebase_uid = self.uid_manager.create_uid()
            new_rulebase = Rulebase(
                uid=new_rulebase_uid,
                name=f"Rulebase {new_rulebase_uid}",
                mgm_uid=mock_mgm_uid
            )
            self.rulebases.append(new_rulebase)

            for i in range(number_of_rules):
                # Add a new rule to the rulebase
                new_rule = self.add_rule_to_rulebase(new_rulebase_uid)

                if mock_config.get("initialize_rule_num_numeric"):
                    new_rule.rule_num_numeric = created_rule_counter * rule_num_numeric_steps
                    created_rule_counter += 1

    def add_rule_to_rulebase(self, rulebase_uid: str) -> Rule:
        """
        Adds a new rule to the rulebase identified by the given UID.

        Args:
            rulebase_uid (str): The UID of the rulebase to which the rule should be added.

        Returns:
            Rule: The newly created rule.
        """
        rulebase = next(rb for rb in self.rulebases if rb.uid == rulebase_uid)

        new_rule = Rule(
            action_id=0,
            mgm_id=0,
            rule_action="accept",
            rule_create=0,
            rule_disabled=False,
            rule_dst="",
            rule_dst_neg=False,
            rule_dst_refs="",
            rule_last_seen=0,
            rule_num=0,
            rule_num_numeric=0,
            rule_src="",
            rule_src_neg=False,
            rule_src_refs="",
            rule_svc="",
            rule_svc_neg=False,
            rule_svc_refs="",
            rule_time="",
            track_id=0,
            rule_track="none",
            rule_uid=self.uid_manager.create_uid(),
            rule_installon=""
        )
        rulebase.Rules[new_rule.rule_uid] = new_rule

        return new_rule
        

class ConfigMocker:
    config_type = "normalized"
    file_name_prefix = ''
    tmp_dir = "/usr/local/fworch/tmp/import/"
    mgm_id = 3
    manager_uid = "6ae3760206b9bfbd2282b5964f6ea07869374f427533c72faa7418c28f7a77f2"
    manager_name = 'sting-mgmt'
    file_name = f'{file_name_prefix}mgm_id_{mgm_id}_config_{config_type}.json'
    default_file_path = tmp_dir + file_name
    default_number_config = [100000]
    uid_manager = UidManager()
    checkpoint_object_types = ["hosts", 
                                "networks", 
                                "groups", 
                                "address-ranges", 
                                "groups-with-exclusion",
                                "gateways-and-servers",
                                "dns-domains",
                                "updatable-objects-repository-content",
                                "interoperable-devices",
                                "security-zones",
                                "Global",
                                "access-roles",
                                "updatable-objects",
                                "service-groups",
                                "application-site-categories",
                                "application-sites",
                                "services-tcp",
                                "services-udp",
                                "services-dce-rpc",
                                "services-rpc",
                                "services-other",
                                "services-icmp",
                                "services-icmp6",
                                "services-sctp",
                                "services-gtp",
                                "CpmiAnyObject",
                                "user-groups",
                                "users",
                                "access-roles",
                                "user-templates"]

    config = {}
    rules_uids = []
    rulebases = []
    checkpoint_config_objects = []

    checkpoint_objects_config = {}


    def create_checkpoint_config(self):
        self.config["object_tables"] = []
        for object_type in self.checkpoint_object_types:
            self.config["object_tables"].append(
                {
                    "object_type": object_type,
                    "object_chunks": [{"objects":[]}]
                }
            )


    def create_checkpoint_config_object(self, uid, name, type, domain_uid = "", domain_name = "", domain_type = "", icon = "", color = ""):
        return {
            "uid": uid,
            "name": name,
            "type": type,
            "domain": {
            "uid": domain_uid,
            "name": domain_name,
            "domain-type": domain_type
            },
            "icon": icon,
            "color": color
        }


    def create_checkpoint_config_objects(self):
        for objects_config in self.checkpoint_objects_config.keys():
            object_tables = self.config["object_tables"]
            table = next((object_table for object_table in object_tables if object_table["object_type"] == objects_config), None)
            counter = 1
            while counter <= self.checkpoint_objects_config[objects_config]:
                new_uid = self.uid_manager.create_uid()
                new_object = self.create_checkpoint_config_object(new_uid, f"{table["object_type"]}_object {counter}", table["object_type"])
                self.checkpoint_config_objects.append(new_object)
                table["object_chunks"][0]["objects"].append(new_object) # TODO: Support more than one chunk
                counter += 1


    def create_config(self, as_dict: bool = False, file_path: str = "", number_config: list = []) -> Union[dict, None]: 
        if number_config == []:
            number_config = self.default_number_config

        self.generate_rulebases(number_config)

        match self.config_type:
            case "normalized":
                self.build_normalized_config()
            case "checkpoint":
                self.create_checkpoint_config()
                self.create_checkpoint_config_objects()
                
        
        if as_dict:
            if self.config_type == "normalized":
                return self, self.rules_uids
            else:
                return self.checkpoint_config_objects, self.config
        else:
            if file_path == "":
                file_path = self.default_file_path

            with open(file_path, "w") as file:
                json.dump(self.config, file, indent=4)
            print(f"Config file saved to {file_path}")

            return None


    def build_normalized_config(self):
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
        rulebase.name = f"Rulebase {rulebase.uid}" # to ensure unique key 'unique_rulebase_mgm_id_name'
        rulebase.mgm_uid = self.uid_manager.create_uid()
        rulebase.Rules = self.generate_rules(rules_number) or {}
        return rulebase


    def generate_rulebases(self, rulebase_config):
        for number_of_rules in rulebase_config:
            new_rulebase = self.generate_rulebase(number_of_rules)
            self.rulebases.append(new_rulebase)


if __name__ == '__main__':
    """ 

    DISCLAIMER

    This script does create a config similar to the normalized config we get when we import and transform a config from sting. However, it is not very sophisticated at this point.
    It simply creates an empty config with the bare minimum to import n rules. To make it work you have to deactivate the consistency checks and create only one rulebase. 

    """

    config_mocker = ConfigMocker()
    config_mocker.create_config()


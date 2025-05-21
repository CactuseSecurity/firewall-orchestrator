import json
import uuid
from typing import List, Union, Dict
import random



if __name__ == '__main__': # for usage as executable script
    import sys
    import os
    sys.path.append(
        os.path.abspath(
            os.path.join(os.path.dirname(__file__), '../../importer')
        )
    )
    sys.path.append(
        os.path.abspath(
            os.path.join(os.path.dirname(__file__), '../../')
        )
    )
    from uid_manager import UidManager
    from mock_rulebase import MockRulebase
    from models.fwconfig_normalized import FwConfigNormalized
    from pydantic import PrivateAttr
    from models.rule import Rule
    from models.networkobject import NetworkObject
    from models.serviceobject import ServiceObject
    from models.rulebase import Rulebase
    from fwo_const import rule_num_numeric_steps, dummy_ip
    from fwo_globals import setGlobalValues
    from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
    from mock_import_state import MockImportStateController
    from model_controllers.fwconfigmanager_controller import FwConfigManager
else: # for usage in unit tests
    from . uid_manager import UidManager
    from . mock_rulebase import MockRulebase
    from importer.models.fwconfig_normalized import FwConfigNormalized
    from importer.models.rulebase import Rulebase
    from importer.models.rule import Rule
    from pydantic import PrivateAttr
    from importer.fwo_const import rule_num_numeric_steps
    from importer.models.networkobject import NetworkObject
    from importer.models.serviceobject import ServiceObject


class MockFwConfigNormalized(FwConfigNormalized):
    """
        A mock subclass of FwConfigNormalized for testing purposes.

        This class creates a fake firewall configuration including rulebases
        and rules, using a UID manager to generate unique identifiers.
    """

    _uid_manager: UidManager = PrivateAttr()

    @property
    def uid_manager(self) -> UidManager:
        """Returns the internal UID manager."""
        return self._uid_manager
    

    def __init__(self, **kwargs):
        """
            Initializes the mock configuration with default action and an empty gateway list.
        """

        super().__init__(action="INSERT", gateways=[], **kwargs)
        self._uid_manager = UidManager()


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

        # Add network objects.

        for index in range(mock_config["network_object_config"]):

            new_network_object_uid = self.uid_manager.create_uid()

            new_network_object = NetworkObject(
                obj_uid = new_network_object_uid,
                obj_ip = dummy_ip,
                obj_ip_end = dummy_ip,
                obj_name = f"Network Object {index}",
                obj_color = "black",
                obj_typ = "group"
            )
            
            self.network_objects[new_network_object_uid] = new_network_object

        # Add users.

        for index in range(mock_config["user_config"]):

            new_user_uid = self.uid_manager.create_uid()

            new_user = NetworkObject(
                obj_uid = new_user_uid,
                obj_ip = dummy_ip,
                obj_ip_end = dummy_ip,
                obj_name = f"IA_{index}",
                obj_color = "black",
                obj_typ = "access-role"
            )
            
            self.network_objects[new_user_uid] = new_user

        # Add services.

        for index in range(mock_config["service_config"]):

            new_service_uid = self.uid_manager.create_uid()

            new_service = ServiceObject(
                svc_uid = new_service_uid,
                svc_name = f"Service {index}",
                svc_color = "black",
                svc_typ = "group"
            )
            
            self.service_objects[new_service_uid] = new_service

        # Add rules and rulebases.
            
        for number_of_rules in mock_config["rule_config"]:

            # Create a new rulebase.

            new_rulebase_uid = self.uid_manager.create_uid()
            new_rulebase = Rulebase(
                uid = new_rulebase_uid,
                name = f"Rulebase {new_rulebase_uid}",
                mgm_uid = mock_mgm_uid
            )
            self.rulebases.append(new_rulebase)

            for i in range(number_of_rules):

                # Add a new rule to the rulebase.

                new_rule = self.add_rule_to_rulebase(new_rulebase_uid)
                self.add_references_to_rule(new_rule)

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
        

    def add_references_to_rule(self, rule: Rule):
        network_objects = [
            network_object for network_object in self.network_objects.values()
            if network_object.obj_typ == "group"
        ]
        src_network_object = random.choice(network_objects)
        dst_network_object = random.choice(network_objects)

        service_objects = list(self.service_objects.values())
        service = random.choice(service_objects)

        users = [
            user for user in self.network_objects.values()
            if user.obj_typ == "access-role"
        ]
        src_user = random.choice(users)
        dst_user = random.choice(users)

        rule.rule_dst = f"{dst_network_object.obj_name}|{dst_user.obj_name}"
        rule.rule_dst_refs = f"{dst_network_object.obj_uid}|{dst_user.obj_uid}"
        rule.rule_src = f"{src_network_object.obj_name}|{src_user.obj_name}"
        rule.rule_src_refs = f"{src_network_object.obj_uid}|{src_user.obj_uid}"
        rule.rule_svc = service.svc_name
        rule.rule_svc_refs = service.svc_uid


if __name__ == '__main__':
    mock_config = MockFwConfigNormalized()
    mock_config.initialize_config(
        {
            "rule_config": [10,10,10],
            "network_object_config": 10,
            "service_config": 10,
            "user_config": 10
        }
    )

    fw_mock_import_state = MockImportStateController()
    setGlobalValues(debug_level_in = 8)
    fw_config_manager_list_controller = FwConfigManagerListController()
    fw_config_manager = FwConfigManager(
        ManagerUid = "6ae3760206b9bfbd2282b5964f6ea07869374f427533c72faa7418c28f7a77f2",
        ManagerName= "sting-mgmt"
    )
    fw_config_manager.Configs.append(mock_config)
    fw_config_manager_list_controller.ManagerSet.append(fw_config_manager)
    fw_config_manager_list_controller.storeFullNormalizedConfigToFile(fw_mock_import_state)

    print(f"MockConfig: File saved on disk.")


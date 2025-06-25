from typing import List, Dict, Optional
import secrets

from netaddr import IPNetwork

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
    from models.fwconfig_normalized import FwConfigNormalized
    from pydantic import PrivateAttr
    from models.rule import RuleNormalized, RuleAction, RuleTrack, RuleType
    from models.networkobject import NetworkObject
    from models.serviceobject import ServiceObject
    from models.rulebase import Rulebase
    from fwo_const import rule_num_numeric_steps, dummy_ip, list_delimiter, user_delimiter
    from fwo_globals import setGlobalValues
    from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
    from mock_import_state import MockImportStateController
    from model_controllers.fwconfigmanager_controller import FwConfigManager
    from models.gateway import Gateway
    from models.rulebase_link import RulebaseLinkUidBased
else: # for usage in unit tests
    from . uid_manager import UidManager
    from importer.models.fwconfig_normalized import FwConfigNormalized
    from importer.models.rulebase import Rulebase
    from importer.models.rule import RuleNormalized, RuleAction, RuleTrack, RuleType
    from pydantic import PrivateAttr
    from importer.fwo_const import rule_num_numeric_steps, dummy_ip, list_delimiter, user_delimiter
    from importer.models.networkobject import NetworkObject
    from importer.models.serviceobject import ServiceObject
    from importer.models.gateway import Gateway
    from importer.models.rulebase_link import RulebaseLinkUidBased


DUMMY_IP = IPNetwork(dummy_ip)

class MockFwConfigNormalizedBuilder():
    """
        A mock subclass of FwConfigNormalized for testing purposes.

        This class creates a fake firewall configuration including rulebases
        and rules, using a UID manager to generate unique identifiers.
    """

    _uid_manager: UidManager = PrivateAttr()
    _seed: int = 42  # Seed for reproducibility in tests

    def __init__(self):
        """
            Initializes the mock configuration builder with an internal UID manager.
        """
        self._uid_manager = UidManager()

    @property
    def uid_manager(self) -> UidManager:
        """Returns the internal UID manager."""
        return self._uid_manager

    @staticmethod
    def empty_config() -> FwConfigNormalized:
        """
            Returns an empty FwConfigNormalized object with no rulebases or rules.
        """
        return FwConfigNormalized(
            action='INSERT', # type: ignore
            gateways=[],
            network_objects={},
            service_objects={},
            users={},
            rulebases=[]
        )


    def build_config(self, mock_config: Dict):
        """
            Initializes the mock configuration with rulebases and rules.

            Args:
                mock_config (dict): A dictionary with configuration values. Expected keys:
                    - "rule_config": List of integers, each representing the number of rules per rulebase.
                    - "initialize_rule_num_numeric": Optional boolean, if True assigns incremental numeric rule numbers.
        """
        config = self.empty_config()

        mock_mgm_uid = self.uid_manager.create_uid()
        created_rule_counter = 1

        # Add network objects.

        # TODO: test various object types
        # obj_types = [entry["obj_typ_name"] for entry in mock_fwo_api_oo.STM_TABLES["stm_obj_typ"]]

        for index in range(mock_config["network_object_config"]):

            new_network_object_uid = self.uid_manager.create_uid()

            new_network_object = NetworkObject(
                obj_uid = new_network_object_uid,
                obj_ip = DUMMY_IP,
                obj_ip_end = DUMMY_IP,
                obj_name = f"Network Object {index}",
                obj_color = "black",
                obj_typ = "group"
            )
            
            config.network_objects[new_network_object_uid] = new_network_object

        # Add users.

        for index in range(mock_config["user_config"]):

            new_user_uid = self.uid_manager.create_uid()

            new_user = NetworkObject(
                obj_uid = new_user_uid,
                obj_ip = DUMMY_IP,
                obj_ip_end = DUMMY_IP,
                obj_name = f"IA_{index}",
                obj_color = "black",
                obj_typ = "access-role"
            )
            
            config.network_objects[new_user_uid] = new_user

        # Add services.

        for index in range(mock_config["service_config"]):

            new_service_uid = self.uid_manager.create_uid()

            new_service = ServiceObject(
                svc_uid = new_service_uid,
                svc_name = f"Service {index}",
                svc_color = "black",
                svc_typ = "group"
            )
            
            config.service_objects[new_service_uid] = new_service

        # Add rules and rulebases.
            
        for number_of_rules in mock_config["rule_config"]:

            # Create a new rulebase.

            new_rulebase_uid = self.uid_manager.create_uid()
            new_rulebase = Rulebase(
                uid = new_rulebase_uid,
                name = f"Rulebase {new_rulebase_uid}",
                mgm_uid = mock_mgm_uid,
                id=None
            )
            config.rulebases.append(new_rulebase)

            for i in range(number_of_rules):

                # Add a new rule to the rulebase.

                new_rule = self.add_rule_to_rulebase(config, new_rulebase_uid)
                self.add_references_to_rule(config, new_rule)

                if mock_config.get("initialize_rule_num_numeric"):
                    new_rule.rule_num_numeric = created_rule_counter * rule_num_numeric_steps
                    created_rule_counter += 1

        # Add Gateway.

        gateway_uid = self.uid_manager.create_uid() if not mock_config.get("gateway_uid") else mock_config["gateway_uid"]
        gateway_name = f"Gateway {gateway_uid}" if not mock_config.get("gateway_name") else mock_config["gateway_name"]
        config.gateways.append(
            Gateway(
                Uid = gateway_uid,
                Name = gateway_name,
                RulebaseLinks = self.create_rulebase_links(config)
            )
        )

        return config
    
    def add_rule_with_nested_groups(self, config: FwConfigNormalized):
        """
        Adds a rule with highest possible reference complexity for testing correctness
        of reference imports
        """
        # create basic objects
        host_objects = []
        for i in range(5):
            new_network_object = NetworkObject(
                obj_uid = self.uid_manager.create_uid(),
                obj_ip = DUMMY_IP,
                obj_ip_end = DUMMY_IP,
                obj_name = f"Network Object {i}",
                obj_color = "black",
                obj_typ = "host"
            )
            host_objects.append(new_network_object)
            config.network_objects[new_network_object.obj_uid] = new_network_object
        
        inner_group_objects = []
        for i in range(2):
            inner_group = NetworkObject(
                obj_uid = self.uid_manager.create_uid(),
                obj_ip = DUMMY_IP,
                obj_ip_end = DUMMY_IP,
                obj_name = f"Inner Group {i}",
                obj_color = "black",
                obj_typ = "group",
                obj_member_names = list_delimiter.join([host_objects[i].obj_name for i in range(i, i + 2)]),
                obj_member_refs = list_delimiter.join([host_objects[i].obj_uid for i in range(i, i + 2)])
            )
            inner_group_objects.append(inner_group)
            config.network_objects[inner_group.obj_uid] = inner_group
        
        outer_group = NetworkObject(
            obj_uid = self.uid_manager.create_uid(),
            obj_ip = DUMMY_IP,
            obj_ip_end = DUMMY_IP,
            obj_name = "Outer Group",
            obj_color = "black",
            obj_typ = "group",
            obj_member_names = list_delimiter.join([inner_group.obj_name for inner_group in inner_group_objects]),
            obj_member_refs = list_delimiter.join([inner_group.obj_uid for inner_group in inner_group_objects])
        )
        config.network_objects[outer_group.obj_uid] = outer_group
        
        rulebase = config.rulebases[0]
        self.add_rule_to_rulebase(
            config,
            rulebase.uid,
            src_objs=[outer_group],
            dst_objs=[outer_group]
        )

        
    def create_rulebase_links(self, config: FwConfigNormalized) -> List[RulebaseLinkUidBased]:
        rulebase_links = []

        # Add initial link.

        initial_rulebase_uid = config.rulebases[0].uid
        rulebase_links.append(
            RulebaseLinkUidBased(
                from_rulebase_uid = "",
                from_rule_uid = "",
                to_rulebase_uid = initial_rulebase_uid,
                link_type = "ordered",
                is_initial = True,
                is_global = False,
                is_section = False
            )
        )

        # Add remaining links.
        
        for index, rulebase in enumerate(config.rulebases):

            # Skip first rulebase.

            if index == 0:
                continue

            # Create ordered rulebase link.
            
            current_from_rulebase_uid = config.rulebases[index - 1].uid
            current_from_rule_uid = list(config.rulebases[index - 1].Rules.values())[-1].rule_uid
            rulebase_links.append(
                RulebaseLinkUidBased(
                    from_rulebase_uid = current_from_rulebase_uid,
                    from_rule_uid = current_from_rule_uid,
                    to_rulebase_uid = config.rulebases[index].uid,
                    link_type = "ordered",
                    is_initial = False,
                    is_global = False,
                    is_section = False
                )
            )

        return rulebase_links
    

    def add_rule_to_rulebase(self, config: FwConfigNormalized, rulebase_uid: str, src_objs: Optional[List[NetworkObject]] = None,
                             dst_objs: Optional[List[NetworkObject]] = None, svc_objs: Optional[List[ServiceObject]] = None) -> RuleNormalized:
        """
        Adds a new rule to the rulebase identified by the given UID.

        Args:
            rulebase_uid (str): The UID of the rulebase to which the rule should be added.

        Returns:
            Rule: The newly created rule.
        """
        rulebase = next(rb for rb in config.rulebases if rb.uid == rulebase_uid)

        if not src_objs:
            src_objs = secrets.SystemRandom(self._seed).sample(
                list(config.network_objects.values()), 
                k=secrets.SystemRandom(self._seed).randint(1, 5)
            )
        if not dst_objs:
            dst_objs = secrets.SystemRandom(self._seed).sample(
                list(config.network_objects.values()), 
                k=secrets.SystemRandom(self._seed).randint(1, 5)
            )
        if not svc_objs:
            svc_objs = secrets.SystemRandom(self._seed).sample(
                list(config.service_objects.values()), 
                k=secrets.SystemRandom(self._seed).randint(1, 5)
            )

        new_rule = RuleNormalized(
            rule_disabled = False,
            rule_src_neg = False,
            rule_src = list_delimiter.join(obj.obj_name for obj in src_objs),
            rule_src_refs = list_delimiter.join(obj.obj_uid for obj in src_objs),
            rule_dst_neg = False,
            rule_dst = list_delimiter.join(obj.obj_name for obj in dst_objs),
            rule_dst_refs = list_delimiter.join(obj.obj_uid for obj in dst_objs),
            rule_svc_neg = False,
            rule_svc = list_delimiter.join(svc.svc_name for svc in svc_objs),
            rule_svc_refs = list_delimiter.join(svc.svc_uid for svc in svc_objs),
            rule_action = RuleAction.ACCEPT,
            rule_track = RuleTrack.NONE,
            rule_installon = "",
            rule_time = "always",
            rule_name = f"Rule {self.uid_manager.create_uid()}",
            rule_uid = self.uid_manager.create_uid(),
            rule_custom_fields = None,
            rule_implied = False,
            rule_type = RuleType.SECTIONHEADER,
            rule_last_change_admin = None,
            parent_rule_uid = None,
            last_hit = None,
            rule_comment = None,
            rule_src_zone = None,
            rule_dst_zone = None,
            rule_head_text = None,
            rule_num = 0,
            rule_num_numeric = 0.0
        )
        rulebase.Rules[new_rule.rule_uid] = new_rule

        return new_rule
        

    def add_references_to_rule(self, config: FwConfigNormalized, rule: RuleNormalized, num_src: int = 1, num_dst: int = 1, num_svc: int = 1):
        network_objects = [
            network_object for network_object in config.network_objects.values()
            if network_object.obj_typ == "group"
        ]
        src_network_objects = secrets.SystemRandom(self._seed).sample(network_objects, k=min(num_src, len(network_objects)))
        dst_network_objects = secrets.SystemRandom(self._seed).sample(network_objects, k=min(num_dst, len(network_objects)))

        service_objects = list(config.service_objects.values())
        
        svcs = secrets.SystemRandom(self._seed).sample(service_objects, k=min(num_svc, len(service_objects)))

        users = [
            user for user in config.network_objects.values()
            if user.obj_typ == "access-role"
        ]
        src_user = secrets.choice(users)
        dst_user = secrets.choice(users)

        rule.rule_dst = list_delimiter.join([f"{obj.obj_name}{user_delimiter}{src_user.obj_name}" for obj in dst_network_objects])
        rule.rule_dst_refs = list_delimiter.join([f"{obj.obj_uid}{user_delimiter}{src_user.obj_uid}" for obj in dst_network_objects])
        rule.rule_src = list_delimiter.join([f"{obj.obj_name}{user_delimiter}{dst_user.obj_name}" for obj in src_network_objects])
        rule.rule_src_refs = list_delimiter.join([f"{obj.obj_uid}{user_delimiter}{dst_user.obj_uid}" for obj in src_network_objects])
        rule.rule_svc = list_delimiter.join([svc.svc_name for svc in svcs])
        rule.rule_svc_refs = list_delimiter.join([svc.svc_uid for svc in svcs])


if __name__ == '__main__':
    mock_config_builder = MockFwConfigNormalizedBuilder()
    mock_config = mock_config_builder.build_config(
        {
            "rule_config": [10,10,10],
            "network_object_config": 10,
            "service_config": 10,
            "user_config": 10,
            "gateway_uid": "cbdd1e35-b6e9-4ead-b13f-fd6389e34987",
            "gateway_name": "sting-gw"
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

    print("MockConfig: File saved on disk.")


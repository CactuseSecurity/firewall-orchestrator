import secrets

from netaddr import IPNetwork
from models.rule import RuleNormalized, RuleAction, RuleTrack, RuleType
from .uid_manager import UidManager
from models.fwconfig_normalized import FwConfigNormalized
from pydantic import PrivateAttr
from models.networkobject import NetworkObject
from models.serviceobject import ServiceObject
from models.rulebase import Rulebase
from fwo_const import rule_num_numeric_steps, dummy_ip, list_delimiter, user_delimiter
from models.gateway import Gateway
from models.rulebase_link import RulebaseLinkUidBased
from fwo_const import list_delimiter, user_delimiter


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
        self.set_up()


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


    def set_up(self):
        """
            Sets up config builder to an initial state.
        """

        self._uid_manager = UidManager()        


    def build_config(self, mock_config: dict):
        """
            Initializes the mock configuration with rulebases and rules.

            Args:
                mock_config (dict): A dictionary with configuration values. Expected keys:
                    - "rule_config": list of integers, each representing the number of rules per rulebase.
                    - "initialize_rule_num_numeric": optional boolean, if True assigns incremental numeric rule numbers.
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

            new_user_obj = NetworkObject(
                obj_uid = new_user_uid,
                obj_ip = DUMMY_IP,
                obj_ip_end = DUMMY_IP,
                obj_name = f"IA_{index}",
                obj_color = "black",
                obj_typ = "access-role"
            )

            new_user = {
                "user_uid": new_user_uid,
                "user_name": f"IA_{index}",
                "user_typ": "simple"
            }
            
            config.network_objects[new_user_uid] = new_user_obj
            config.users[new_user_uid] = new_user

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

            _, new_rulebase_uid = self.add_rulebase(config, mock_mgm_uid)

            for i in range(number_of_rules):

                # Add a new rule to the rulebase.

                new_rule = self.add_rule(config, new_rulebase_uid)
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

        return config, mock_mgm_uid
    
    def add_rule_with_nested_groups(self, config: FwConfigNormalized):
        """
        Adds a rule with highest possible reference complexity for testing correctness
        of reference imports
        """
        rulebase = config.rulebases[0]
        config_objs = {
            "rules": [
                {
                    "rule_uid": "cpr",
                    "rule_src": f"cpr-from1{list_delimiter}cpr-from2",
                    "rule_src_refs": f"cpr-from1{list_delimiter}cpr-from2",
                    "rule_dst": "cpr-to1",
                    "rule_dst_refs": "cpr-to1",
                    "rule_svc": "cpr-svc1",
                    "rule_svc_refs": "cpr-svc1",
                }
            ],
            "network_objects": [
                {
                    "obj_uid": "cpr-from1",
                    "obj_name": "cpr-from1",
                    "obj_member_names": f"cpr-from1-member1{list_delimiter}cpr-from1-member2",
                    "obj_member_refs": f"cpr-from1-member1{list_delimiter}cpr-from1-member2",
                },
                {
                    "obj_uid": "cpr-from1-member1",
                    "obj_name": "cpr-from1-member1",
                    "obj_member_names": f"cpr-from1-member1-member1{list_delimiter}cpr-from1-member1-member2",
                    "obj_member_refs": f"cpr-from1-member1-member1{list_delimiter}cpr-from1-member1-member2",
                },
                {
                    "obj_uid": "cpr-from1-member2",
                    "obj_name": "cpr-from1-member2",
                    "obj_member_names": "cpr-from1-member1-member2",
                    "obj_member_refs": "cpr-from1-member1-member2",
                },
                {
                    "obj_uid": "cpr-from1-member1-member1",
                    "obj_name": "cpr-from1-member1-member1",
                    "obj_typ": "host",
                },
                {
                    "obj_uid": "cpr-from1-member1-member2",
                    "obj_name": "cpr-from1-member1-member2",
                    "obj_typ": "host",
                },
                {
                    "obj_uid": "cpr-from2",
                    "obj_name": "cpr-from2",
                    "obj_member_names": "cpr-from1-member2",
                    "obj_member_refs": "cpr-from1-member2",
                },
                {
                    "obj_uid": "cpr-to1",
                    "obj_name": "cpr-to1",
                    "obj_typ": "host"
                }
            ],
            "service_objects": [
                {
                    "svc_uid": "cpr-svc1",
                    "svc_name": "cpr-svc1",
                    "svc_member_names": "cpr-svc1-member1",
                    "svc_member_refs": "cpr-svc1-member1",
                },
                {
                    "svc_uid": "cpr-svc1-member1",
                    "svc_name": "cpr-svc1-member1",
                }
            ]
        }

        for rule in config_objs["rules"]:
            self.add_rule(config, rulebase.uid, rule)
        for obj in config_objs["network_objects"]:
            self.add_network_object(config, obj)
        for svc in config_objs["service_objects"]:
            self.add_service_object(config, svc)
        
    
    def change_rule_with_nested_groups(self, config: FwConfigNormalized, change_type: str = "change",
                                       change_obj: str = "from"):
        """
        Changes a rule with nested groups in a subtle way to test if the import process detects changes correctly.
        Args:
            config (FwConfigNormalized): The configuration to change.
            change_type (str): The type of change to apply. Can be "change", "add", or "remove".
            change_obj (str): The object to change. Can be "from", "svc", "member", "member_svc", "nested_member", or "nested_member_svc".
        """
        rulebase = config.rulebases[0]
        rule = rulebase.Rules.get("cpr")
        if not rule:
            raise ValueError("Rule 'cpr' not found in the rulebase.")
        if change_type == "change":
            if change_obj == "from":
                self.change_network_object_subtle(config, "cpr-from1")
            elif change_obj == "svc":
                self.change_service_object_subtle(config, "cpr-svc1")
            elif change_obj == "member":
                self.change_network_object_subtle(config, "cpr-from1-member1")
            elif change_obj == "member_svc":
                self.change_service_object_subtle(config, "cpr-svc1-member1")
            elif change_obj == "nested_member":
                self.change_network_object_subtle(config, "cpr-from1-member1-member1")
        elif change_type == "add":
            self._add_rule_with_nested_groups(config, rule, change_obj)

        elif change_type == "remove":
            self._remove_rule_with_nested_groups(config, rule, rulebase, change_obj)


    def _add_rule_with_nested_groups(self, config: FwConfigNormalized, rule, change_obj):

        if change_obj == "from":
            self.add_network_object(config, {
                "obj_uid": "cpr-from-new",
                "obj_name": "cpr-from-new",
                "obj_typ": "host"
            })
            rule.rule_src += list_delimiter + "cpr-from-new"
            rule.rule_src_refs += list_delimiter + "cpr-from-new"
        elif change_obj == "svc":
            self.add_service_object(config, {
                "svc_uid": "cpr-svc-new",
                "svc_name": "cpr-svc-new",
                "svc_typ": "simple"
            })
            rule.rule_svc += list_delimiter + "cpr-svc-new"
            rule.rule_svc_refs += list_delimiter + "cpr-svc-new"
        elif change_obj == "member":
            from_obj = config.network_objects.get("cpr-from1")
            self.add_network_object(config, {
                "obj_uid": "cpr-from-member-new",
                "obj_name": "cpr-from-member-new",
                "obj_typ": "host"
            })
            from_obj.obj_member_names += list_delimiter + "cpr-from-member-new"
            from_obj.obj_member_refs += list_delimiter + "cpr-from-member-new"
        elif change_obj == "member_svc":
            svc_obj = config.service_objects.get("cpr-svc1")
            self.add_service_object(config, {
                "svc_uid": "cpr-svc-member-new",
                "svc_name": "cpr-svc-member-new",
                "svc_typ": "simple"
            })
            svc_obj.svc_member_names += list_delimiter + "cpr-svc-member-new"
            svc_obj.svc_member_refs += list_delimiter + "cpr-svc-member-new"
        elif change_obj == "nested_member":
            member = config.network_objects.get("cpr-from1-member1")
            self.add_network_object(config, {
                "obj_uid": "cpr-from-member1-member-new",
                "obj_name": "cpr-from-member1-member-new",
                "obj_typ": "host"
            })
            member.obj_member_names += list_delimiter + "cpr-from-member1-member-new"
            member.obj_member_refs += list_delimiter + "cpr-from-member1-member-new"
        elif change_obj == "nested_member_svc":
            member_svc = config.service_objects.get("cpr-svc1-member1")
            self.add_service_object(config, {
                "svc_uid": "cpr-svc-member1-member-new",
                "svc_name": "cpr-svc-member1-member-new",
                "svc_typ": "simple"
            })
            member_svc.svc_member_names = "cpr-svc-member1-member-new"
            member_svc.svc_member_refs = "cpr-svc-member1-member-new"


    def _remove_rule_with_nested_groups(self, config: FwConfigNormalized, rule, rulebase, change_obj):
        """
        Removes a rule with nested groups from the configuration.
        Args:
            config (FwConfigNormalized): The configuration to modify.
            rule (RuleNormalized): The rule to remove.
            rulebase (Rulebase): The rulebase containing the rule.
        """
        if change_obj == "from":
            rule.rule_src = rule.rule_src.replace("cpr-from1", "").strip(list_delimiter)
            rule.rule_src_refs = rule.rule_src_refs.replace("cpr-from1", "").strip(list_delimiter)
        elif change_obj == "svc":
            rule.rule_svc = rule.rule_svc.replace("cpr-svc1", "").strip(list_delimiter)
            rule.rule_svc_refs = rule.rule_svc_refs.replace("cpr-svc1", "").strip(list_delimiter)
        elif change_obj == "member":
            from_obj = config.network_objects.get("cpr-from1")
            from_obj.obj_member_names = from_obj.obj_member_names.replace("cpr-from1-member1", "").strip(list_delimiter)
            from_obj.obj_member_refs = from_obj.obj_member_refs.replace("cpr-from1-member1", "").strip(list_delimiter)
        elif change_obj == "member_svc":
            svc_obj = config.service_objects.get("cpr-svc1")
            svc_obj.svc_member_names = svc_obj.svc_member_names.replace("cpr-svc1-member1", "").strip(list_delimiter)
            svc_obj.svc_member_refs = svc_obj.svc_member_refs.replace("cpr-svc1-member1", "").strip(list_delimiter)
        elif change_obj == "nested_member":
            from_obj = config.network_objects.get("cpr-from1-member1")
            from_obj.obj_member_names = from_obj.obj_member_names.replace("cpr-from1-member1-member2", "").strip(list_delimiter)
            from_obj.obj_member_refs = from_obj.obj_member_refs.replace("cpr-from1-member1-member2", "").strip(list_delimiter)
        elif change_obj == "nested_member_svc":
            svc_obj = config.service_objects.get("cpr-svc1-member1")
            svc_obj.svc_member_names = svc_obj.svc_member_names.replace("cpr-svc1-member1-member1", "").strip(list_delimiter)
            svc_obj.svc_member_refs = svc_obj.svc_member_refs.replace("cpr-svc1-member1-member1", "").strip(list_delimiter)


    def create_rulebase_links(self, config: FwConfigNormalized) -> list[RulebaseLinkUidBased]:
        rulebase_links = []

        # Add initial link.

        initial_rulebase_uid = config.rulebases[0].uid
        rulebase_links.append(
            RulebaseLinkUidBased(
                from_rulebase_uid = None,
                from_rule_uid = None,
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
    
    def add_network_object(self, config: FwConfigNormalized, obj_dict: dict):
        uid = obj_dict.get("obj_uid", self.uid_manager.create_uid())
        dummy_ip = DUMMY_IP if obj_dict.get("obj_typ", "group") != "group" else None
        new_network_object = NetworkObject(
            obj_uid = uid,
            obj_ip = obj_dict.get("obj_ip", dummy_ip),
            obj_ip_end = obj_dict.get("obj_ip_end", dummy_ip),
            obj_name = obj_dict.get("obj_name", f"Network Object {uid}"),
            obj_color = obj_dict.get("obj_color", "black"),
            obj_typ = obj_dict.get("obj_typ", "group"),
            obj_member_names = obj_dict.get("obj_member_names", ""),
            obj_member_refs = obj_dict.get("obj_member_refs", "")
        )
        config.network_objects[new_network_object.obj_uid] = new_network_object
        return new_network_object
    
    def add_service_object(self, config: FwConfigNormalized, svc_dict: dict):
        uid = svc_dict.get("svc_uid", self.uid_manager.create_uid())
        default_port = 80 if svc_dict.get("svc_typ", "group") != "group" else None
        default_proto = 6 if svc_dict.get("svc_typ", "group") != "group" else None
        new_service_object = ServiceObject(
            svc_uid = uid,
            svc_name = svc_dict.get("svc_name", f"Service {uid}"),
            svc_color = svc_dict.get("svc_color", "black"),
            svc_typ = svc_dict.get("svc_typ", "group"),
            svc_port = svc_dict.get("svc_port", default_port),
            svc_port_end = svc_dict.get("svc_port_end", default_port),
            ip_proto = svc_dict.get("ip_proto", default_proto),
            svc_member_names = svc_dict.get("svc_member_names", ""),
            svc_member_refs = svc_dict.get("svc_member_refs", "")
        )
        config.service_objects[new_service_object.svc_uid] = new_service_object
        return new_service_object
    

    def add_rule(self, config: FwConfigNormalized, rulebase_uid: str, rule_dict: dict = {}) -> RuleNormalized:
        """
        Adds a new rule to the rulebase identified by the given UID.

        Args:
            rulebase_uid (str): The UID of the rulebase to which the rule should be added.

        Returns:
            Rule: The newly created rule.
        """
        rulebase = next(rb for rb in config.rulebases if rb.uid == rulebase_uid)

        src_objs, dst_objs, svc_objs = [], [], []
        if "rule_src" not in rule_dict:
            src_objs = secrets.SystemRandom(self._seed).sample(
                list(config.network_objects.values()), 
                k=secrets.SystemRandom(self._seed).randint(1, 5)
            )
        if "rule_dst" not in rule_dict:
            dst_objs = secrets.SystemRandom(self._seed).sample(
                list(config.network_objects.values()), 
                k=secrets.SystemRandom(self._seed).randint(1, 5)
            )
        if "rule_svc" not in rule_dict:
            svc_objs = secrets.SystemRandom(self._seed).sample(
                list(config.service_objects.values()), 
                k=secrets.SystemRandom(self._seed).randint(1, 5)
            )
        uid = rule_dict.get("rule_uid", self.uid_manager.create_uid())
        new_rule = RuleNormalized(
            rule_disabled = rule_dict.get("rule_disabled", False),
            rule_src_neg = rule_dict.get("rule_src_neg", False),
            rule_src = rule_dict.get("rule_src", list_delimiter.join(obj.obj_name for obj in src_objs)),
            rule_src_refs = rule_dict.get("rule_src_refs", list_delimiter.join(obj.obj_uid for obj in src_objs)),
            rule_dst_neg = rule_dict.get("rule_dst_neg", False),
            rule_dst = rule_dict.get("rule_dst", list_delimiter.join(obj.obj_name for obj in dst_objs)),  
            rule_dst_refs = rule_dict.get("rule_dst_refs", list_delimiter.join(obj.obj_uid for obj in dst_objs)),
            rule_svc_neg = rule_dict.get("rule_svc_neg", False),
            rule_svc = rule_dict.get("rule_svc", list_delimiter.join(svc.svc_name for svc in svc_objs)),
            rule_svc_refs = rule_dict.get("rule_svc_refs", list_delimiter.join(svc.svc_uid for svc in svc_objs)),
            rule_action = rule_dict.get("rule_action", RuleAction.ACCEPT),
            rule_track = rule_dict.get("rule_track", RuleTrack.NONE),
            rule_installon = rule_dict.get("rule_installon", ""),
            rule_time = rule_dict.get("rule_time", "always"),
            rule_name = rule_dict.get("rule_name", f"Rule {uid}"),
            rule_uid = uid,
            rule_custom_fields = rule_dict.get("rule_custom_fields", None),
            rule_implied = rule_dict.get("rule_implied", False),
            rule_type = rule_dict.get("rule_type", RuleType.SECTIONHEADER),
            last_change_admin = rule_dict.get("last_change_admin", None),
            parent_rule_uid = rule_dict.get("parent_rule_uid", None),
            last_hit = rule_dict.get("last_hit", None),
            rule_comment = rule_dict.get("rule_comment", None),
            rule_src_zone = rule_dict.get("rule_src_zone", None),
            rule_dst_zone = rule_dict.get("rule_dst_zone", None),
            rule_head_text = rule_dict.get("rule_head_text", None),
            rule_num = rule_dict.get("rule_num", 0),
            rule_num_numeric = rule_dict.get("rule_num_numeric", 0.0),
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

    def change_network_object_subtle(self, config, obj_uid):
        """
        Changes a network object in a subtle way (excluding name and UID) to test
        if the import process detects changes correctly, esp. in nested groups.
        """
        if obj_uid not in config.network_objects:
            raise ValueError(f"Object with UID {obj_uid} not found in config.")

        obj = config.network_objects[obj_uid]
        # # Change the IP address slightly
        # new_ip = IPNetwork(str(IPAddress(DUMMY_IP.first + 1)) + '/' + str(DUMMY_IP.prefixlen))
        # obj.obj_ip = new_ip
        # obj.obj_ip_end = new_ip
        obj.obj_color = "blue" if obj.obj_color == "black" else "black"  # Toggle color for subtle change
    
    def change_service_object_subtle(self, config, svc_uid):
        """
        Changes a service object in a subtle way (excluding name and UID) to test
        if the import process detects changes correctly.
        """
        if svc_uid not in config.service_objects:
            raise ValueError(f"Service with UID {svc_uid} not found in config.")

        svc = config.service_objects[svc_uid]
        # # Change the service protocol slightly
        # svc.svc_port = 6 if svc.svc_port == 17 else 17  # Toggle between TCP (6) and UDP (17)
        svc.svc_color = "blue" if svc.svc_color == "black" else "black"  # Toggle color for subtle change

    def add_cp_section_header(self, gateway: Gateway, from_rulebase_uid: str, to_rulebase_uid: str, from_rule_uid: str) -> None:

        gateway.RulebaseLinks.append(
            RulebaseLinkUidBased(
                from_rulebase_uid = from_rulebase_uid,
                from_rule_uid = from_rule_uid,
                to_rulebase_uid = to_rulebase_uid,
                link_type = "ordered",
                is_initial = False,
                is_global = False,
                is_section = True
            )
        )
    

    def add_inline_layer(self, gateway: Gateway, index: int, from_rulebase_uid: str, to_rulebase_uid: str, from_rule_uid: str) -> None:

        gateway.RulebaseLinks.insert(
            index,
            RulebaseLinkUidBased(
                from_rulebase_uid = from_rulebase_uid,
                from_rule_uid = from_rule_uid,
                to_rulebase_uid = to_rulebase_uid,
                link_type = "inline",
                is_initial = False,
                is_global = False,
                is_section = False
            )
        )


    def add_rulebase(self, config: FwConfigNormalized, mgm_uid: str) -> tuple[Rulebase, str]:

        new_rulebase_uid = self.uid_manager.create_uid()
        new_rulebase = Rulebase(
            uid = new_rulebase_uid,
            name = f"Rulebase {new_rulebase_uid}",
            mgm_uid = mgm_uid,
            id=None
        )
        config.rulebases.append(new_rulebase)

        return new_rulebase, new_rulebase_uid


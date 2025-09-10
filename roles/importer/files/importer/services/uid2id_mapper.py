from logging import Logger
from fwo_log import getFwoLogger
from model_controllers.import_state_controller import ImportStateController
from services.service_provider import ServiceProvider
from services.enums import Services
import fwo_const
from fwo_api import FwoApi

class Uid2IdMapper:
    """
    A class to map unique identifiers (UIDs) to IDs.
    This class is used to maintain a mapping between UID and relevant ID in the database.
    """

    import_state: ImportStateController
    logger: Logger
    nwobj_uid2id: dict[str, int]
    svc_uid2id: dict[str, int]
    user_uid2id: dict[str, int]
    rule_uid2id: dict[str, int]
    outdated_nwobj_uid2id: dict[str, int]
    outdated_svc_uid2id: dict[str, int]
    outdated_user_uid2id: dict[str, int]
    outdated_zone_name2id: dict[str, int]
    outdated_rule_uid2id: dict[str, int]

    @property
    def api_connection(self):
        if self.import_state is None:
            return None
        else:
            return self.import_state.api_connection

    def __init__(self):
        """
        Initialize the Uid2IdMapper.
        """
        global_state = ServiceProvider().get_service(Services.GLOBAL_STATE)
        self.import_state = global_state.import_state
        self.logger = getFwoLogger()
        self.nwobj_uid2id = {}
        self.svc_uid2id = {}
        self.user_uid2id = {}
        self.rule_uid2id = {}
        self.zone_name2id = {}
        self.outdated_nwobj_uid2id = {}
        self.outdated_svc_uid2id = {}
        self.outdated_user_uid2id = {}
        self.outdated_zone_name2id = {}
        self.outdated_rule_uid2id = {}
        self.global_nwobj_uid2id = {}
        self.global_svc_uid2id = {}
        self.global_user_uid2id = {}
        self.global_rule_uid2id = {}
        self.global_zone_name2id = {}

    def log_error(self, message: str):
        """
        Log an error message.
        
        Args:
            message (str): The error message to log.
        """
        self.logger.error(message)
        self.import_state.appendErrorString(message)
        self.import_state.increaseErrorCounterByOne()
    
    def log_debug(self, message: str):
        """
        Log a debug message.
        
        Args:
            message (str): The debug message to log.
        """
        self.logger.debug(message)

    def get_network_object_id(self, uid: str, before_update: bool = False, local_only: bool = False) -> int:
        """
        Get the ID for a given network object UID.
        
        Args:
            uid (str): The UID of the network object.
            before_update (bool): If True, use the outdated mapping if available.
        
        Returns:
            int: The ID of the network object.
        """
        if before_update:
            nwobj_id = self.outdated_nwobj_uid2id.get(uid)
            if nwobj_id is not None:
                return nwobj_id
        nwobj_id = self.nwobj_uid2id.get(uid)
        if not local_only and nwobj_id is None:
            nwobj_id = self.global_nwobj_uid2id.get(uid)
        if nwobj_id is None:
            self.log_error(f"Network object UID '{uid}' not found in mapping.")
        return nwobj_id
    

    def get_service_object_id(self, uid: str, before_update: bool = False, local_only: bool = False) -> int:
        """
        Get the ID for a given service object UID.
        
        Args:
            uid (str): The UID of the service object.
            before_update (bool): If True, use the outdated mapping if available.
        
        Returns:
            int: The ID of the service object.
        """
        if before_update:
            svc_id = self.outdated_svc_uid2id.get(uid)
            if svc_id is not None:
                return svc_id

        svc_id = self.svc_uid2id.get(uid)
        if not local_only and svc_id is None:
            svc_id = self.global_svc_uid2id.get(uid)
        if svc_id is None:
            self.log_error(f"Service object UID '{uid}' not found in mapping.")
        return svc_id
    

    def get_user_id(self, uid: str, before_update: bool = False, local_only: bool = False) -> int:
        """
        Get the ID for a given user UID.
        
        Args:
            uid (str): The UID of the user.
            before_update (bool): If True, use the outdated mapping if available.
        
        Returns:
            int: The ID of the user.
        """
        if before_update:
            usr_id = self.outdated_user_uid2id.get(uid)
            if usr_id is not None:
                return usr_id
        usr_id = self.user_uid2id.get(uid)
        if not local_only and usr_id is None:
            usr_id = self.global_user_uid2id.get(uid)
        if usr_id is None:
            self.log_error(f"User UID '{uid}' not found in mapping.")
        return usr_id
    

    def get_zone_object_id(self, uid: str, before_update: bool = False, local_only: bool = False) -> int:
        """
        Get the ID for a given zone UID.
        
        Args:
            uid (str): The UID of the zone.
            before_update (bool): If True, use the outdated mapping if available.
        
        Returns:
            int: The ID of the zone
        """
        if before_update:
            zone_id = self.outdated_zone_name2id.get(uid)
            if zone_id is not None:
                return zone_id

        zone_id = self.zone_name2id.get(uid)
        if not local_only and zone_id is None:
            zone_id = self.global_zone_name2id.get(uid)
        if zone_id is None:
            self.log_error(f"Zone UID '{uid}' not found in mapping.")
        return zone_id


    def get_rule_id(self, uid: str, before_update: bool = False) -> int:
        """
        Get the ID for a given rule UID.
        
        Args:
            uid (str): The UID of the rule.
            before_update (bool): If True, use the outdated mapping if available.
        
        Returns:
            int: The ID of the rule.
        """
        if before_update:
            rule_id = self.outdated_rule_uid2id.get(uid)
            if rule_id is not None:
                return rule_id
        rule_id = self.rule_uid2id.get(uid)
        if rule_id is None:
            self.log_error(f"Rule UID '{uid}' not found in mapping.")
        return rule_id
    

    def add_network_object_mappings(self, mappings: list[dict], is_global=False) -> bool:
        """
        Add network object mappings to the internal mapping dictionary.

        Args:
            mappings (list[dict]): A list of dictionaries containing UID and ID mappings.
                    Each dictionary should have 'obj_uid' and 'obj_id' keys.

        Returns:
            bool: True if the mappings were added successfully, False otherwise.
        """
        main_map = self.global_nwobj_uid2id if is_global else self.nwobj_uid2id
        outdated_map = self.outdated_nwobj_uid2id

        for mapping in mappings:
            if 'obj_uid' not in mapping or 'obj_id' not in mapping:
                self.log_error("Invalid mapping format. Each mapping must contain 'obj_uid' and 'obj_id'.")
                return False
            if mapping['obj_uid'] in main_map:
                outdated_map[mapping['obj_uid']] = main_map[mapping['obj_uid']]
            main_map[mapping['obj_uid']] = mapping['obj_id']

        msg = f"Added {len(mappings)} {'global ' if is_global else ''}network object mappings."
        self.log_debug(msg)
        return True


    def add_service_object_mappings(self, mappings: list[dict], is_global=False) -> bool:
        """
        Add service object mappings to the internal mapping dictionary.

        Args:
            mappings (list[dict]): A list of dictionaries containing UID and ID mappings.
                    Each dictionary should have 'svc_uid' and 'svc_id' keys.

        Returns:
            bool: True if the mappings were added successfully, False otherwise.
        """
        main_map = self.global_svc_uid2id if is_global else self.svc_uid2id
        outdated_map = self.outdated_svc_uid2id

        for mapping in mappings:
            if 'svc_uid' not in mapping or 'svc_id' not in mapping:
                self.log_error("Invalid mapping format. Each mapping must contain 'svc_uid' and 'svc_id'.")
                return False
            if mapping['svc_uid'] in main_map:
                outdated_map[mapping['svc_uid']] = main_map[mapping['svc_uid']]
            main_map[mapping['svc_uid']] = mapping['svc_id']

        self.log_debug(f"Added {len(mappings)} service object mappings.")
        return True


    def add_zone_mappings(self, mappings: list[dict], is_global=False) -> bool:
        """
        Add zone object mappings to the internal mapping dictionary.

        Args:
            mappings (list[dict]): A list of dictionaries containing Name and ID mappings.
                    Each dictionary should have 'zone_name' and 'zone_id' keys.

        Returns:
            bool: True if the mappings were added successfully, False otherwise.
        """
        main_map = self.global_zone_name2id if is_global else self.zone_name2id
        outdated_map = self.outdated_zone_name2id

        for mapping in mappings:
            if 'zone_name' not in mapping or 'zone_id' not in mapping:
                self.log_error("Invalid mapping format. Each mapping must contain 'zone_name' and 'zone_id'.")
                return False
            if mapping['zone_name'] in main_map:
                outdated_map[mapping['zone_name']] = main_map[mapping['zone_name']]
            main_map[mapping['zone_name']] = mapping['zone_id']

        self.log_debug(f"Added {len(mappings)} zone mappings.")
        return True


    def add_user_mappings(self, mappings: list[dict], is_global=False) -> bool:
        """
        Add user object mappings to the internal mapping dictionary.

        Args:
            mappings (list[dict]): A list of dictionaries containing UID and ID mappings.
                    Each dictionary should have 'user_uid' and 'user_id' keys.

        Returns:
            bool: True if the mappings were added successfully, False otherwise.
        """
        main_map = self.global_user_uid2id if is_global else self.user_uid2id
        outdated_map = self.outdated_user_uid2id

        for mapping in mappings:
            if 'user_uid' not in mapping or 'user_id' not in mapping:
                self.log_error("Invalid mapping format. Each mapping must contain 'user_uid' and 'user_id'.")
                return False
            if mapping['user_uid'] in main_map:
                outdated_map[mapping['user_uid']] = main_map[mapping['user_uid']]
            main_map[mapping['user_uid']] = mapping['user_id']

        self.log_debug(f"Added {len(mappings)} service object mappings.")
        return True
    

    def add_rule_mappings(self, mappings: list[dict]) -> bool:
        """
        Add rule mappings to the internal mapping dictionary.

        Args:
            mappings (list[dict]): A list of dictionaries containing UID and ID mappings.
                    Each dictionary should have 'rule_uid' and 'rule_id' keys.

        Returns:
            bool: True if the mappings were added successfully, False otherwise.
        """
        for mapping in mappings:
            if 'rule_uid' not in mapping or 'rule_id' not in mapping:
                self.log_error("Invalid mapping format. Each mapping must contain 'rule_uid' and 'rule_id'.")
                return False
            if mapping['rule_uid'] in self.rule_uid2id:
                self.outdated_rule_uid2id[mapping['rule_uid']] = self.rule_uid2id[mapping['rule_uid']]
            self.rule_uid2id[mapping['rule_uid']] = mapping['rule_id']
        self.log_debug(f"Added {len(mappings)} rule mappings.")
        return True


    def update_network_object_mapping(self, uids: list[str]|None = None) -> bool:
        """
        Update the mapping for network objects based on the provided UIDs.
        
        Args:
            uids (list[str]): A list of UIDs to update the mapping for. If None, all UIDs for the Management will be fetched.
        
        Returns:
            bool: True if the mapping was updated successfully, False otherwise.
        """
        query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "networkObject/getmapOfUid2Id.graphql"])

        if uids is not None:
            if len(uids) == 0:
                self.log_debug("Network object mapping updated for 0 objects")
                return True
            variables = {'uids': uids}
        else:
            # If no UIDs are provided, fetch all UIDs for the Management
            variables = {'mgmId': self.import_state.MgmDetails.CurrentMgmId}
        try:
            response = self.import_state.api_connection.call(query, variables)
            if response is None:
                self.log_error("Error updating network object mapping: No response from API")
                return False
            if 'errors' in response:
                self.log_error(f"Error updating network object mapping: {response['errors']}")
                return False
            self.nwobj_uid2id.update({
                obj['obj_uid']: obj['obj_id']
                for obj in response['data']['object']
            })
            self.log_debug(f"Network object mapping updated for {len(response['data']['object'])} objects")
            return True
        except Exception as e:
            self.log_error(f"Error updating network object mapping: {e}")
            return False # raise
    
    def update_service_object_mapping(self, uids: list[str]|None = None) -> bool:
        """
        Update the mapping for service objects based on the provided UIDs.
        
        Args:
            uids (list[str]): A list of UIDs to update the mapping for. If None, all UIDs for the Management will be fetched.
        
        Returns:
            bool: True if the mapping was updated successfully, False otherwise.
        """
        query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "networkService/getmapOfUid2Id.graphql"])
        if uids is not None:
            if len(uids) == 0:
                self.log_debug("Service object mapping updated for 0 objects")
                return True
            variables = {'uids': uids}
        else:
            # If no UIDs are provided, fetch all UIDs for the Management
            variables = {'mgmId': self.import_state.MgmDetails.CurrentMgmId}
        try:
            response = self.import_state.api_connection.call(query, variables)
            if response is None:
                self.log_error("Error updating service object mapping: No response from API")
                return False
            if 'errors' in response:
                self.log_error(f"Error updating service object mapping: {response['errors']}")
                return False
            for obj in response['data']['service']:
                self.svc_uid2id[obj['svc_uid']] = obj['svc_id']
            self.log_debug(f"Service object mapping updated for {len(response['data']['service'])} objects")
            return True
        except Exception as e:
            self.log_error(f"Error updating service object mapping: {e}")
            return False # raise
        
    def update_user_mapping(self, uids: list[str]|None = None) -> bool:
        """
        Update the mapping for users based on the provided UIDs.
        
        Args:
            uids (list[str]): A list of UIDs to update the mapping for. If None, all UIDs for the Management will be fetched.
        
        Returns:
            bool: True if the mapping was updated successfully, False otherwise.
        """
        query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "user/getmapOfUid2Id.graphql"])
        if uids is not None:
            if len(uids) == 0:
                self.log_debug("User mapping updated for 0 objects")
                return True
            variables = {'uids': uids}
        else:
            # If no UIDs are provided, fetch all UIDs for the Management
            variables = {'mgmId': self.import_state.MgmDetails.CurrentMgmId}
        try:
            response = self.import_state.api_connection.call(query, variables)
            if response is None:
                self.log_error("Error updating user mapping: No response from API")
                return False
            if 'errors' in response:
                self.log_error(f"Error updating user mapping: {response['errors']}")
                return False
            for obj in response['data']['usr']:
                self.user_uid2id[obj['user_uid']] = obj['user_id']
            self.log_debug(f"User mapping updated for {len(response['data']['usr'])} objects")
            return True
        except Exception as e:
            self.log_error(f"Error updating user mapping: {e}")
            return False # raise

    def update_rule_mapping(self, uids: list[str]|None = None) -> bool:
        """
        Update the mapping for rules based on the provided UIDs.
        
        Args:
            uids (list[str]): A list of UIDs to update the mapping for. If None, all UIDs for the Management will be fetched.
        
        Returns:
            bool: True if the mapping was updated successfully, False otherwise.
        """
        query = FwoApi.get_graphql_code([fwo_const.graphql_query_path + "rule/getmapOfUid2Id.graphql"])
        if uids is not None:
            if len(uids) == 0:
                self.log_debug("Rule mapping updated for 0 objects")
                return True
            variables = {'uids': uids}
        else:
            # If no UIDs are provided, fetch all UIDs for the Management
            variables = {'mgmId': self.import_state.MgmDetails.CurrentMgmId}
        try:
            response = self.import_state.api_connection.call(query, variables)
            if response is None:
                self.log_error("Error updating rule mapping: No response from API")
                return False
            if 'errors' in response:
                self.log_error(f"Error updating rule mapping: {response['errors']}")
                return False
            for obj in response['data']['rule']:
                self.rule_uid2id[obj['rule_uid']] = obj['rule_id']
            self.log_debug(f"Rule mapping updated for {len(response['data']['rule'])} objects")
            return True
        except Exception as e:
            self.log_error(f"Error updating rule mapping: {e}")
            return False # raise

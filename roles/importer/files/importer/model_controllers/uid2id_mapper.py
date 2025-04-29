from typing import List, Optional
from fwo_log import getFwoLogger
from model_controllers.import_state_controller import ImportStateController


class Uid2IdMapper:
    """
    A class to map unique identifiers (UIDs) to IDs.
    This class is used to maintain a mapping between UID and relevant ID in the database.
    """
      
    def __init__(self, import_state_controller: ImportStateController):
        """
        Initialize the Uid2IdMapper with an API connection.
        
        Args:
            import_state_controller (ImportStateController): The import state controller instance.
        """
        self.import_state_controller = import_state_controller
        self.api_connection = import_state_controller.api_connection
        self.logger = getFwoLogger()
        self.nwobj_uid2id = {}
        self.svc_uid2id = {}
        self.user_uid2id = {}
        self.outdated_nwobj_uid2id = {}
        self.outdated_svc_uid2id = {}
        self.outdated_user_uid2id = {}

    def log_error(self, message: str):
        """
        Log an error message.
        
        Args:
            message (str): The error message to log.
        """
        self.logger.error(message)
        self.import_state_controller.appendErrorString(message)
        self.import_state_controller.increaseErrorCounterByOne()
    
    def log_debug(self, message: str):
        """
        Log a debug message.
        
        Args:
            message (str): The debug message to log.
        """
        self.logger.debug(message)

    def get_network_object_id(self, uid: str, before_update: bool = False) -> int:
        """
        Get the ID for a given network object UID.
        
        Args:
            uid (str): The UID of the network object.
            before_update (bool): If True, use the outdated mapping if available.
        
        Returns:
            int: The ID of the network object.
        """
        if before_update:
            id = self.outdated_nwobj_uid2id.get(uid)
            if id is not None:
                return id
        id = self.nwobj_uid2id.get(uid)
        if id is None:
            self.log_error(f"Network object UID '{uid}' not found in mapping.")
        return id
    
    def get_service_object_id(self, uid: str, before_update: bool = False) -> int:
        """
        Get the ID for a given service object UID.
        
        Args:
            uid (str): The UID of the service object.
            before_update (bool): If True, use the outdated mapping if available.
        
        Returns:
            int: The ID of the service object.
        """
        if before_update:
            id = self.outdated_svc_uid2id.get(uid)
            if id is not None:
                return id
        id = self.svc_uid2id.get(uid)
        if id is None:
            self.log_error(f"Service object UID '{uid}' not found in mapping.")
        return id
    
    def get_user_id(self, uid: str, before_update: bool = False) -> int:
        """
        Get the ID for a given user UID.
        
        Args:
            uid (str): The UID of the user.
            before_update (bool): If True, use the outdated mapping if available.
        
        Returns:
            int: The ID of the user.
        """
        if before_update:
            id = self.outdated_user_uid2id.get(uid)
            if id is not None:
                return id
        id = self.user_uid2id.get(uid)
        if id is None:
            self.log_error(f"User UID '{uid}' not found in mapping.")
        return id
    
    def add_network_object_mappings(self, mappings: List[dict]) -> bool:
        """
        Add network object mappings to the internal mapping dictionary.

        Args:
            mappings (List[dict]): A list of dictionaries containing UID and ID mappings.
                    Each dictionary should have 'obj_uid' and 'obj_id' keys.

        Returns:
            bool: True if the mappings were added successfully, False otherwise.
        """
        for mapping in mappings:
            if 'obj_uid' not in mapping or 'obj_id' not in mapping:
                self.log_error("Invalid mapping format. Each mapping must contain 'obj_uid' and 'obj_id'.")
                return False
            self.nwobj_uid2id[mapping['obj_uid']] = mapping['obj_id']
        self.log_debug(f"Added {len(mappings)} network object mappings.")
        return True

    def add_service_object_mappings(self, mappings: List[dict]) -> bool:
        """
        Add service object mappings to the internal mapping dictionary.

        Args:
            mappings (List[dict]): A list of dictionaries containing UID and ID mappings.
                    Each dictionary should have 'svc_uid' and 'svc_id' keys.

        Returns:
            bool: True if the mappings were added successfully, False otherwise.
        """
        for mapping in mappings:
            if 'svc_uid' not in mapping or 'svc_id' not in mapping:
                self.log_error("Invalid mapping format. Each mapping must contain 'svc_uid' and 'svc_id'.")
                return False
            self.svc_uid2id[mapping['svc_uid']] = mapping['svc_id']
        self.log_debug(f"Added {len(mappings)} service object mappings.")
        return True

    def add_user_mappings(self, mappings: List[dict]) -> bool:
        """
        Add user mappings to the internal mapping dictionary.

        Args:
            mappings (List[dict]): A list of dictionaries containing UID and ID mappings.
                    Each dictionary should have 'user_uid' and 'user_id' keys.

        Returns:
            bool: True if the mappings were added successfully, False otherwise.
        """
        for mapping in mappings:
            if 'user_uid' not in mapping or 'user_id' not in mapping:
                self.log_error("Invalid mapping format. Each mapping must contain 'user_uid' and 'user_id'.")
                return False
            self.user_uid2id[mapping['user_uid']] = mapping['user_id']
        self.log_debug(f"Added {len(mappings)} user mappings.")
        return True
    
    def update_network_object_mapping(self, uids: Optional[List[str]] = None) -> bool:
        """
        Update the mapping for network objects based on the provided UIDs.
        
        Args:
            uids (List[str]): A list of UIDs to update the mapping for. If None, all UIDs for the Management will be fetched.
        
        Returns:
            bool: True if the mapping was updated successfully, False otherwise.
        """
        # TODO: remove active filter later
        query = """
            query getMapOfUid2Id($uids: [String!], $mgmId: Int!) {
                object(where: {obj_uid: {_in: $uids}, mgm_id: {_eq: $mgmId}, removed: {_is_null: true}, active: {_eq: true}}) {
                    obj_id
                    obj_uid
                }
            }
            """
        if uids is not None:
            if len(uids) == 0:
                self.log_debug("Network object mapping updated for 0 objects")
                return True
            variables = {'uids': uids}
        else:
            # If no UIDs are provided, fetch all UIDs for the Management
            variables = {'mgmId': self.import_state_controller.MgmDetails.Id}
        try:
            response = self.api_connection.call(query, variables)
            if response is None:
                self.log_error("Error updating network object mapping: No response from API")
                return False
            if 'errors' in response:
                self.log_error(f"Error updating network object mapping: {response['errors']}")
                return False
            for obj in response['data']['object']:
                self.nwobj_uid2id[obj['obj_uid']] = obj['obj_id']
            self.log_debug(f"Network object mapping updated for {len(response['data']['object'])} objects")
            return True
        except Exception as e:
            self.log_error(f"Error updating network object mapping: {e}")
            return False
    
    def update_service_object_mapping(self, uids: Optional[List[str]] = None) -> bool:
        """
        Update the mapping for service objects based on the provided UIDs.
        
        Args:
            uids (List[str]): A list of UIDs to update the mapping for. If None, all UIDs for the Management will be fetched.
        
        Returns:
            bool: True if the mapping was updated successfully, False otherwise.
        """
        # TODO: remove active filter later
        query = """
            query getMapOfUid2Id($uids: [String!], $mgmId: Int!) {
                service(where: {svc_uid: {_in: $uids}, mgm_id: {_eq: $mgmId}, removed: {_is_null: true}, active: {_eq: true}}) {
                    svc_id
                    svc_uid
                }
            }
            """
        if uids is not None:
            if len(uids) == 0:
                self.log_debug("Service object mapping updated for 0 objects")
                return True
            variables = {'uids': uids}
        else:
            # If no UIDs are provided, fetch all UIDs for the Management
            variables = {'mgmId': self.import_state_controller.MgmDetails.Id}
        try:
            response = self.api_connection.call(query, variables)
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
            return False
        
    def update_user_mapping(self, uids: Optional[List[str]] = None) -> bool:
        """
        Update the mapping for users based on the provided UIDs.
        
        Args:
            uids (List[str]): A list of UIDs to update the mapping for. If None, all UIDs for the Management will be fetched.
        
        Returns:
            bool: True if the mapping was updated successfully, False otherwise.
        """
        # TODO: remove active filter later
        query = """
            query getMapOfUid2Id($uids: [String!], $mgmId: Int!) {
                usr(where: {user_uid: {_in: $uids}, mgm_id: {_eq: $mgmId}, removed: {_is_null: true}, active: {_eq: true}}) {
                    user_id
                    user_uid
                }
            }
            """
        if uids is not None:
            if len(uids) == 0:
                self.log_debug("User mapping updated for 0 objects")
                return True
            variables = {'uids': uids}
        else:
            # If no UIDs are provided, fetch all UIDs for the Management
            variables = {'mgmId': self.import_state_controller.MgmDetails.Id}
        try:
            response = self.api_connection.call(query, variables)
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
            return False

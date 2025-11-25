from logging import Logger
from typing import TYPE_CHECKING, Any
from fwo_log import FWOLogger
if TYPE_CHECKING:
    from model_controllers.import_state_controller import ImportStateController
from fwo_exceptions import FwoImporterError
from services.service_provider import ServiceProvider
from services.enums import Services
import fwo_const
from fwo_api import FwoApi

class Uid2IdMap:
    """
    A simple data structure to hold UID to ID mappings.
    Includes current and outdated, local and global mappings.
    """
    def __init__(self):
        self.local: dict[str, int] = {}
        self.outdated_local: dict[str, int] = {}
        self.global_map: dict[str, int] = {}
        self.outdated_global: dict[str, int] = {}

    def get(self, uid: str, before_update: bool = False, local_only: bool = False) -> int | None:
        if before_update:
            outdated_id = self.outdated_local.get(uid) or self.outdated_global.get(uid)
            if outdated_id is not None:
                return outdated_id
            return self.local.get(uid) or self.global_map.get(uid) # was not updated, use current
        if local_only:
            return self.local.get(uid)
        return self.local.get(uid) or self.global_map.get(uid)

    def set(self, uid: str, db_id: int, is_global: bool = False):
        target_map = self.global_map if is_global else self.local
        outdated_map = self.outdated_global if is_global else self.outdated_local
        if uid in target_map:
            outdated_map[uid] = target_map[uid]
        target_map[uid] = db_id
    
    def update(self, new_mappings: dict[str, int], is_global: bool = False):
        target_map = self.global_map if is_global else self.local
        outdated_map = self.outdated_global if is_global else self.outdated_local
        for uid, db_id in new_mappings.items():
            if uid in target_map:
                outdated_map[uid] = target_map[uid]
            target_map[uid] = db_id

class Uid2IdMapper:
    """
    A class to map unique identifiers (UIDs) to IDs.
    This class is used to maintain a mapping between UID and relevant ID in the database.
    """

    import_state: 'ImportStateController'
    logger: Logger

    nwobj_uid2id: Uid2IdMap
    svc_uid2id: Uid2IdMap
    user_uid2id: Uid2IdMap
    zone_name2id: Uid2IdMap
    rule_uid2id: Uid2IdMap

    @property
    def api_connection(self) -> FwoApi:
        return self.import_state.api_connection

    def __init__(self):
        """
        Initialize the Uid2IdMapper.
        """
        global_state = ServiceProvider().get_service(Services.GLOBAL_STATE)
        self.import_state = global_state.import_state
        self.nwobj_uid2id = Uid2IdMap()
        self.svc_uid2id = Uid2IdMap()
        self.user_uid2id = Uid2IdMap()
        self.zone_name2id = Uid2IdMap()
        self.rule_uid2id = Uid2IdMap()

    def log_error(self, message: str):
        """
        Log an error message.
        
        Args:
            message (str): The error message to log.
        """
        self.logger.error(message)

    def log_debug(self, message: str):
        """
        Log a debug message.
        
        Args:
            message (str): The debug message to log.
        """
        FWOLogger.debug(message)

    def get_network_object_id(self, uid: str, before_update: bool = False, local_only: bool = False) -> int:
        """
        Get the ID for a given network object UID.
        
        Args:
            uid (str): The UID of the network object.
            before_update (bool): If True, use the outdated mapping if available.
        
        Returns:
            int: The ID of the network object.
        """
        nwobj_id = self.nwobj_uid2id.get(uid, before_update, local_only)
        if nwobj_id is None:
            raise KeyError(f"Network object UID '{uid}' not found in mapping.")
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
        svc_id = self.svc_uid2id.get(uid, before_update, local_only)
        if svc_id is None:
            raise KeyError(f"Service object UID '{uid}' not found in mapping.")
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
        user_id = self.user_uid2id.get(uid, before_update, local_only)
        if user_id is None:
            raise KeyError(f"User UID '{uid}' not found in mapping.")
        return user_id

    def get_zone_object_id(self, name: str, before_update: bool = False, local_only: bool = False) -> int:
        """
        Get the ID for a given zone UID.
        
        Args:
            name (str): The name of the zone.
            before_update (bool): If True, use the outdated mapping if available.
        
        Returns:
            int: The ID of the zone
        """
        zone_id = self.zone_name2id.get(name, before_update, local_only)
        if zone_id is None:
            raise KeyError(f"Zone Name '{name}' not found in mapping.")
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
        rule_id = self.rule_uid2id.get(uid, before_update)
        if rule_id is None:
            raise KeyError(f"Rule UID '{uid}' not found in mapping.")
        return rule_id

    def add_network_object_mappings(self, mappings: list[dict[str, Any]], is_global: bool = False):
        """
        Add network object mappings to the internal mapping dictionary.

        Args:
            mappings (list[dict]): A list of dictionaries containing UID and ID mappings.
                    Each dictionary should have 'obj_uid' and 'obj_id' keys.
        """
        for mapping in mappings:
            if 'obj_uid' not in mapping or 'obj_id' not in mapping:
                raise ValueError("Invalid mapping format. Each mapping must contain 'obj_uid' and 'obj_id'.")
            self.nwobj_uid2id.set(mapping['obj_uid'], mapping['obj_id'], is_global)

        msg = f"Added {len(mappings)} {'global ' if is_global else ''}network object mappings."
        self.log_debug(msg)

    def add_service_object_mappings(self, mappings: list[dict[str, Any]], is_global: bool = False):
        """
        Add service object mappings to the internal mapping dictionary.

        Args:
            mappings (list[dict]): A list of dictionaries containing UID and ID mappings.
                    Each dictionary should have 'svc_uid' and 'svc_id' keys.
        """
        for mapping in mappings:
            if 'svc_uid' not in mapping or 'svc_id' not in mapping:
                raise ValueError("Invalid mapping format. Each mapping must contain 'svc_uid' and 'svc_id'.")
            self.svc_uid2id.set(mapping['svc_uid'], mapping['svc_id'], is_global)

        self.log_debug(f"Added {len(mappings)} {'global ' if is_global else ''}service object mappings.")

    def add_user_mappings(self, mappings: list[dict[str, Any]], is_global: bool = False):
        """
        Add user object mappings to the internal mapping dictionary.

        Args:
            mappings (list[dict]): A list of dictionaries containing UID and ID mappings.
                    Each dictionary should have 'user_uid' and 'user_id' keys.
        """
        for mapping in mappings:
            if 'user_uid' not in mapping or 'user_id' not in mapping:
                raise ValueError("Invalid mapping format. Each mapping must contain 'user_uid' and 'user_id'.")
            self.user_uid2id.set(mapping['user_uid'], mapping['user_id'], is_global)

        self.log_debug(f"Added {len(mappings)} {'global ' if is_global else ''}user mappings.")

    def add_zone_mappings(self, mappings: list[dict[str, Any]], is_global: bool = False):
        """
        Add zone object mappings to the internal mapping dictionary.

        Args:
            mappings (list[dict]): A list of dictionaries containing Name and ID mappings.
                    Each dictionary should have 'zone_name' and 'zone_id' keys.
        """
        for mapping in mappings:
            if 'zone_name' not in mapping or 'zone_id' not in mapping:
                raise ValueError("Invalid mapping format. Each mapping must contain 'zone_name' and 'zone_id'.")
            self.zone_name2id.set(mapping['zone_name'], mapping['zone_id'], is_global)

        self.log_debug(f"Added {len(mappings)} {'global ' if is_global else ''}zone mappings.")

    def add_rule_mappings(self, mappings: list[dict[str, Any]]):
        """
        Add rule mappings to the internal mapping dictionary.

        Args:
            mappings (list[dict]): A list of dictionaries containing UID and ID mappings.
                    Each dictionary should have 'rule_uid' and 'rule_id' keys.
        """
        for mapping in mappings:
            if 'rule_uid' not in mapping or 'rule_id' not in mapping:
                raise ValueError("Invalid mapping format. Each mapping must contain 'rule_uid' and 'rule_id'.")
            self.rule_uid2id.set(mapping['rule_uid'], mapping['rule_id'])

        self.log_debug(f"Added {len(mappings)} rule mappings.")

    def update_network_object_mapping(self, uids: list[str]|None = None, is_global: bool = False):
        """
        Update the mapping for network objects based on the provided UIDs.
        
        Args:
            uids (list[str]): A list of UIDs to update the mapping for. If None, all UIDs for the Management will be fetched.
        """
        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "networkObject/getMapOfUid2Id.graphql"])

        if uids is not None:
            if len(uids) == 0:
                self.log_debug("Network object mapping updated for 0 objects")
                return
            variables = {'uids': uids}
        else:
            # If no UIDs are provided, fetch all UIDs for the Management
            variables = {'mgmId': self.import_state.mgm_details.CurrentMgmId}
        try:
            response = self.import_state.api_connection.call(query, variables)
            if 'errors' in response:
                raise FwoImporterError(f"Error updating network object mapping: {response['errors']}")
            self.nwobj_uid2id.update({
                obj['obj_uid']: obj['obj_id']
                for obj in response['data']['object']
            }, is_global)
            self.log_debug(f"Network object mapping updated for {len(response['data']['object'])} objects")
        except Exception as e:
            raise FwoImporterError(f"Error updating network object mapping: {e}")

    def update_service_object_mapping(self, uids: list[str]|None = None, is_global: bool = False):
        """
        Update the mapping for service objects based on the provided UIDs.
        
        Args:
            uids (list[str]): A list of UIDs to update the mapping for. If None, all UIDs for the Management will be fetched.
        """
        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "networkService/getMapOfUid2Id.graphql"])
        if uids is not None:
            if len(uids) == 0:
                self.log_debug("Service object mapping updated for 0 objects")
                return
            variables = {'uids': uids}
        else:
            # If no UIDs are provided, fetch all UIDs for the Management
            variables = {'mgmId': self.import_state.mgm_details.CurrentMgmId}
        try:
            response = self.import_state.api_connection.call(query, variables)
            if 'errors' in response:
                raise FwoImporterError(f"Error updating service object mapping: {response['errors']}")
            self.svc_uid2id.update({
                obj['svc_uid']: obj['svc_id']
                for obj in response['data']['service']
            }, is_global)
            self.log_debug(f"Service object mapping updated for {len(response['data']['service'])} objects")
        except Exception as e:
            raise FwoImporterError(f"Error updating service object mapping: {e}")

    def update_user_mapping(self, uids: list[str]|None = None, is_global: bool = False):
        """
        Update the mapping for users based on the provided UIDs.
        
        Args:
            uids (list[str]): A list of UIDs to update the mapping for. If None, all UIDs for the Management will be fetched.
        """
        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "user/getMapOfUid2Id.graphql"])
        if uids is not None:
            if len(uids) == 0:
                self.log_debug("User mapping updated for 0 objects")
                return
            variables = {'uids': uids}
        else:
            # If no UIDs are provided, fetch all UIDs for the Management
            variables = {'mgmId': self.import_state.mgm_details.CurrentMgmId}
        try:
            response = self.import_state.api_connection.call(query, variables)
            if 'errors' in response:
                raise FwoImporterError(f"Error updating user mapping: {response['errors']}")
            self.user_uid2id.update({
                obj['user_uid']: obj['user_id']
                for obj in response['data']['usr']
            }, is_global)
            self.log_debug(f"User mapping updated for {len(response['data']['usr'])} objects")
        except Exception as e:
            raise FwoImporterError(f"Error updating user mapping: {e}")

    def update_zone_mapping(self, names: list[str]|None = None, is_global: bool = False):
        """
        Update the mapping for zones based on the provided names.

        Args:
            names (list[str]): A list of zone names to update the mapping for. If None, all zones for the Management will be fetched.
        """
        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "zone/getMapOfName2Id.graphql"])
        if names is not None:
            if len(names) == 0:
                self.log_debug("Zone mapping updated for 0 objects")
                return
            variables = {'names': names}
        else:
            # If no names are provided, fetch all zones for the Management
            variables = {'mgmId': self.import_state.mgm_details.CurrentMgmId}
        try:
            response = self.import_state.api_connection.call(query, variables)
            if 'errors' in response:
                raise FwoImporterError(f"Error updating zone mapping: {response['errors']}")
            self.zone_name2id.update({
                obj['zone_name']: obj['zone_id']
                for obj in response['data']['zone']
            }, is_global)
            self.log_debug(f"Zone mapping updated for {len(response['data']['zone'])} objects")
        except Exception as e:
            raise FwoImporterError(f"Error updating zone mapping: {e}")

    def update_rule_mapping(self, uids: list[str]|None = None):
        """
        Update the mapping for rules based on the provided UIDs.

        Args:
            uids (list[str]): A list of UIDs to update the mapping for. If None, all UIDs for the Management will be fetched.
        """
        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "rule/getMapOfUid2Id.graphql"])
        if uids is not None:
            if len(uids) == 0:
                self.log_debug("Rule mapping updated for 0 objects")
                return
            variables = {'uids': uids}
        else:
            # If no UIDs are provided, fetch all UIDs for the Management
            variables = {'mgmId': self.import_state.mgm_details.CurrentMgmId}
        try:
            response = self.import_state.api_connection.call(query, variables)
            if 'errors' in response:
                raise FwoImporterError(f"Error updating rule mapping: {response['errors']}")
            self.rule_uid2id.update({
                obj['rule_uid']: obj['rule_id']
                for obj in response['data']['rule']
            })
            self.log_debug(f"Rule mapping updated for {len(response['data']['rule'])} objects")
        except Exception as e:
            raise FwoImporterError(f"Error updating rule mapping: {e}")

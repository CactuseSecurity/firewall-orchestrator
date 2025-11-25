from __future__ import annotations
from typing import TYPE_CHECKING, Any

from models.networkobject import NetworkObject
from roles.importer.files.importer.models.serviceobject import ServiceObject

if TYPE_CHECKING:
    from models.fwconfig_normalized import FwConfigNormalized
    from model_controllers.import_state_controller import ImportStateController
import fwo_const
from fwo_log import FWOLogger
from services.service_provider import ServiceProvider
from services.enums import Services


MAX_RECURSION_LEVEL = 20
CONFIG_NOT_SET_MESSAGE = "normalized config is not set"

class GroupFlatsMapper:
    """
    This class is responsible for mapping group objects to their fully resolved members.
    """

    import_state: 'ImportStateController'
    normalized_config: FwConfigNormalized|None = None
    global_normalized_config: FwConfigNormalized|None = None

    def __init__(self):
        global_state = ServiceProvider().get_global_state()
        self.import_state = global_state.import_state
        self.network_object_flats: dict[str, set[str]] = {}
        self.service_object_flats: dict[str, set[str]] = {}
        self.user_flats: dict[str, set[str]] = {}


    def log_error(self, message: str) -> None:
        """
        Log an error message.
        
        Args:
            message (str): The error message to log.
        """
        FWOLogger.error(message)
    
    def init_config(self, normalized_config: FwConfigNormalized, global_normalized_config: FwConfigNormalized|None = None) -> None:
        self.normalized_config = normalized_config
        self.global_normalized_config = global_normalized_config
        self.network_object_flats = {}
        self.service_object_flats = {}
        self.user_flats = {}

    def get_network_object_flats(self, uids: list[str]) -> list[str]:
        """
        Flatten the network object UIDs to all members, including group objects, and the top-level group object itself.
        Does not check if the given objects are group objects or not.
        Args:
            uids (list[str]): The list of network object UIDs to flatten.
        
        Returns:
            list[str]: The flattened network object UIDs.
        """
        if self.normalized_config is None:
            self.log_error(f"{CONFIG_NOT_SET_MESSAGE} - networks")
            return []
        all_members: set[str] = set()
        for uid in uids:
            members = self.flat_nwobj_members_recursive(uid)
            if members is not None:
                all_members.update(members)
        return list(all_members)
    
    def flat_nwobj_members_recursive(self, group_uid: str, recursion_level: int = 0) -> set[str] | None:
        if recursion_level > MAX_RECURSION_LEVEL:
            FWOLogger.warning(f"recursion level exceeded for group {group_uid}")
            return None
        if group_uid in self.network_object_flats:
            return self.network_object_flats[group_uid]
        nwobj = self.get_nwobj(group_uid)
        if nwobj is None:
            self.log_error(f"object with uid {group_uid} not found in network objects of config")
            return None
        members: set[str] = {group_uid}
        if nwobj.obj_member_refs is None or nwobj.obj_member_refs == '':
            return members
        for member_uid in nwobj.obj_member_refs.split(fwo_const.LIST_DELIMITER):
            if fwo_const.USER_DELIMITER in member_uid:
                member_uid = member_uid.split(fwo_const.USER_DELIMITER)[0]  # remove user delimiter if present
            flat_members = self.flat_nwobj_members_recursive(member_uid, recursion_level + 1)
            if flat_members is None:
                continue
            members.update(flat_members)
        self.network_object_flats[group_uid] = members
        return members


    def get_nwobj(self, group_uid: str) -> NetworkObject | None:
        if not self.normalized_config:
            return None
        nwobj = self.normalized_config.network_objects.get(group_uid, None)
        if nwobj is None and self.global_normalized_config is not None:
            nwobj = self.global_normalized_config.network_objects.get(group_uid, None)
        return nwobj


    def get_service_object_flats(self, uids: list[str]) -> list[str]:
        """
        Flatten the service object UIDs to all members, including group objects, and the top-level group object itself.
        Does not check if the given objects are group objects or not.
        Args:
            uids (list[str]): The list of service object UIDs to flatten.
        Returns:
            list[str]: The flattened service object UIDs.
        """
        if self.normalized_config is None:
            self.log_error(f"{CONFIG_NOT_SET_MESSAGE} - services")
            return []
        all_members: set[str] = set()
        for uid in uids:
            members = self.flat_svcobj_members_recursive(uid)
            if members is not None:
                all_members.update(members)
        return list(all_members)

    def flat_svcobj_members_recursive(self, group_uid: str, recursion_level: int = 0) -> set[str] | None:
        if recursion_level > MAX_RECURSION_LEVEL:
            FWOLogger.warning(f"recursion level exceeded for group {group_uid}")
            return None
        if group_uid in self.service_object_flats:
            return self.service_object_flats[group_uid]
        svcobj = self.get_svcobj(group_uid)
        if svcobj is None:
            self.log_error(f"object with uid {group_uid} not found in service objects of config")
            return None
        members: set[str] = {group_uid}
        if svcobj.svc_member_refs is None or svcobj.svc_member_refs == '':
            return members
        for member_uid in svcobj.svc_member_refs.split(fwo_const.LIST_DELIMITER):
            flat_members = self.flat_svcobj_members_recursive(member_uid, recursion_level + 1)
            if flat_members is None:
                continue
            members.update(flat_members)
        self.service_object_flats[group_uid] = members
        return members
    

    def get_svcobj(self, group_uid: str) -> ServiceObject | None:
        if not self.normalized_config:
            return None
        svcobj = self.normalized_config.service_objects.get(group_uid, None)
        if svcobj is None and self.global_normalized_config is not None:
            # try to get from global normalized config if not found in current normalized config
            svcobj = self.global_normalized_config.service_objects.get(group_uid, None)
        return svcobj


    def get_user_flats(self, uids: list[str]) -> list[str]:
        """
        Flatten the user UIDs to all members, including groups, and the top-level group itself.
        Does not check if the given users are groups or not.
        Args:
            uids (list[str]): The list of user UIDs to flatten.
        Returns:
            list[str]: The flattened user UIDs.
        """
        if self.normalized_config is None:
            self.log_error(f"{CONFIG_NOT_SET_MESSAGE} - users")
            return []
        all_members: set[str] = set()
        for uid in uids:
            members = self.flat_user_members_recursive(uid)
            if members is not None:
                all_members.update(members)
        return list(all_members)
    
    def flat_user_members_recursive(self, group_uid: str, recursion_level: int = 0) -> set[str] | None:
        if recursion_level > MAX_RECURSION_LEVEL:
            FWOLogger.warning(f"recursion level exceeded for group {group_uid}")
            return None
        if group_uid in self.user_flats:
            return self.user_flats[group_uid]

        user = self.get_user(group_uid)
        if user is None:
            self.log_error(f"object with uid {group_uid} not found in users of config")
            return None
        members: set[str] = {group_uid}
        if "user_member_refs" not in user or user['user_member_refs'] is None or user['user_member_refs'] == '':
            return members
        for member_uid in user['user_member_refs'].split(fwo_const.LIST_DELIMITER): #TODO: adjust when/if users are refactored into objects
            flat_members = self.flat_user_members_recursive(member_uid, recursion_level + 1)
            if flat_members is None:
                continue
            members.update(flat_members)
        self.user_flats[group_uid] = members
        return members


    def get_user(self, group_uid: str) -> Any | None:
        if not self.normalized_config:
            return None
        user = self.normalized_config.users.get(group_uid, None)
        if user is None and self.global_normalized_config is not None:
            # try to get from global normalized config if not found in current normalized config
            user = self.global_normalized_config.users.get(group_uid, None)
        return user

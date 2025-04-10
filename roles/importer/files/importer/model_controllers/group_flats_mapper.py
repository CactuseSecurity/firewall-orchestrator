from typing import List
import fwo_const
from fwo_log import getFwoLogger
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized


MAX_RECURSION_LEVEL = 20

class GroupFlatsMapper:
    """
    This class is responsible for mapping group objects to their fully resolved members.
    """

    def __init__(self, import_state_controller: ImportStateController, normalized_config: FwConfigNormalized):
        """
        Initialize the GroupFlatsMapper with the import state controller and normalized configuration.

        Args:
            import_state_controller (ImportStateController): The import state controller instance.
            normalized_config (FwConfigNormalized): The normalized configuration instance.
        """
        self.import_state_controller = import_state_controller
        self.normalized_config = normalized_config
        self.logger = getFwoLogger()
        self.network_object_flats = {}
        self.service_object_flats = {}
        self.user_flats = {}

    def log_error(self, message: str):
        """
        Log an error message.
        
        Args:
            message (str): The error message to log.
        """
        self.logger.error(message)
        self.import_state_controller.appendErrorString(message)
        self.import_state_controller.increaseErrorCounterByOne()

    def get_network_object_flats(self, uids: List[str]) -> List[str]:
        """
        Args:
            uids (List[str]): The list of network object UIDs to flatten.
        
        Returns:
            List[str]: The flattened network object UIDs.
        """
        all_members = set()
        for uid in uids:
            members = self.network_object_flats.get(uid)
            if members is None:
                members = self.flat_nwobj_members_recursive(uid)
                if members is None:
                    continue
                self.network_object_flats[uid] = members
                self.logger.debug(f"Added {len(members)} members to network object flats for group {uid}")
            all_members.update(members)
        return list(all_members)
    
    def flat_nwobj_members_recursive(self, groupUid: str, flatMembers: set = None, recursionLevel: int = 0):
        if flatMembers is None:
            flatMembers = set()
        if recursionLevel > MAX_RECURSION_LEVEL:
            self.logger.warning(f"recursion level exceeded for group {groupUid}")
            return flatMembers
        obj = self.normalized_config.network_objects.get(groupUid, None)
        if obj is None:
            self.logger.error(f"object with uid {groupUid} not found in network objects of config")
            return
        if obj.obj_member_refs is not None and obj.obj_member_refs != '':
            for memberUid in obj.obj_member_refs.split(fwo_const.list_delimiter):
                flatMembers.add(memberUid)
                self.flat_nwobj_members_recursive(memberUid, flatMembers, recursionLevel + 1)
        return flatMembers

    def get_service_object_flats(self, uids: List[str]) -> List[str]:
        """
        Args:
            uids (List[str]): The list of service object UIDs to flatten.
        Returns:
            List[str]: The flattened service object UIDs.
        """
        all_members = set()
        for uid in uids:
            members = self.service_object_flats.get(uid)
            if members is None:
                members = self.flat_svcobj_members_recursive(uid)
                if members is None:
                    continue
                self.service_object_flats[uid] = members
                self.logger.debug(f"Added {len(members)} members to service object flats for group {uid}")
            all_members.update(members)
        return list(all_members)

    def flat_svcobj_members_recursive(self, groupUid: str, flatMembers: set = None, recursionLevel: int = 0):
        if flatMembers is None:
            flatMembers = set()
        if recursionLevel > MAX_RECURSION_LEVEL:
            self.logger.warning(f"recursion level exceeded for group {groupUid}")
            return flatMembers
        svc = self.normalized_config.service_objects.get(groupUid, None)
        if svc is None:
            self.logger.error(f"object with uid {groupUid} not found in service objects of config")
            return
        if svc.svc_member_refs is not None and svc.svc_member_refs != '':
            for memberUid in svc.svc_member_refs.split(fwo_const.list_delimiter):
                flatMembers.add(memberUid)
                self.flat_svcobj_members_recursive(memberUid, flatMembers, recursionLevel + 1)
        return flatMembers
    
    def get_user_flats(self, uids: List[str]) -> List[str]:
        """
        Args:
            uids (List[str]): The list of user UIDs to flatten.
        Returns:
            List[str]: The flattened user UIDs.
        """
        all_members = set()
        for uid in uids:
            members = self.user_flats.get(uid)
            if members is None:
                members = self.flat_user_members_recursive(uid)
                if members is None:
                    continue
                self.user_flats[uid] = members
                self.logger.debug(f"Added {len(members)} members to user flats for group {uid}")
            all_members.update(members)
        return list(all_members)
    
    def flat_user_members_recursive(self, groupUid: str, flatMembers: set = None, recursionLevel: int = 0):
        if flatMembers is None:
            flatMembers = set()
        if recursionLevel > MAX_RECURSION_LEVEL:
            self.logger.warning(f"recursion level exceeded for group {groupUid}")
            return flatMembers
        user = self.normalized_config.users.get(groupUid, None)
        if user is None:
            self.logger.error(f"object with uid {groupUid} not found in users of config")
            return
        if user['user_member_refs'] is not None and user['user_member_refs'] != '': #TODO: adjust when/if users are refactored into objects
            for memberUid in user['user_member_refs'].split(fwo_const.list_delimiter):
                flatMembers.add(memberUid)
                self.flat_user_members_recursive(memberUid, flatMembers, recursionLevel + 1)
        return flatMembers
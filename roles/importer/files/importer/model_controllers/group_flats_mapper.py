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
            members = self.flat_nwobj_members_recursive(uid)
            if members is not None:
                all_members.update(members)
        return list(all_members)
    
    def flat_nwobj_members_recursive(self, groupUid: str, recursionLevel: int = 0):
        if recursionLevel > MAX_RECURSION_LEVEL:
            self.logger.warning(f"recursion level exceeded for group {groupUid}")
            return None
        if groupUid in self.network_object_flats:
            return self.network_object_flats[groupUid]
        nwobj = self.normalized_config.network_objects.get(groupUid, None)
        if nwobj is None:
            self.logger.error(f"object with uid {groupUid} not found in network objects of config")
            return None
        if nwobj.obj_member_refs is None or nwobj.obj_member_refs == '':
            if recursionLevel == 0:
                return None # group has no members / not a group
            return [groupUid] # leaf node, return itself
        members = set()
        for memberUid in nwobj.obj_member_refs.split(fwo_const.list_delimiter):
            flatMembers = self.flat_nwobj_members_recursive(memberUid, recursionLevel + 1)
            if flatMembers is None:
                continue
            members.update(flatMembers)
        self.network_object_flats[groupUid] = members
        return members

    def get_service_object_flats(self, uids: List[str]) -> List[str]:
        """
        Args:
            uids (List[str]): The list of service object UIDs to flatten.
        Returns:
            List[str]: The flattened service object UIDs.
        """
        all_members = set()
        for uid in uids:
            members = self.flat_svcobj_members_recursive(uid)
            if members is not None:
                all_members.update(members)
        return list(all_members)

    def flat_svcobj_members_recursive(self, groupUid: str, recursionLevel: int = 0):
        if recursionLevel > MAX_RECURSION_LEVEL:
            self.logger.warning(f"recursion level exceeded for group {groupUid}")
            return None
        if groupUid in self.service_object_flats:
            return self.service_object_flats[groupUid]
        svcobj = self.normalized_config.service_objects.get(groupUid, None)
        if svcobj is None:
            self.logger.error(f"object with uid {groupUid} not found in service objects of config")
            return None
        if svcobj.svc_member_refs is None or svcobj.svc_member_refs == '':
            if recursionLevel == 0:
                return None # group has no members / not a group
            return [groupUid] # leaf node, return itself
        members = set()
        for memberUid in svcobj.svc_member_refs.split(fwo_const.list_delimiter):
            flatMembers = self.flat_svcobj_members_recursive(memberUid, recursionLevel + 1)
            if flatMembers is None:
                continue
            members.update(flatMembers)
        self.service_object_flats[groupUid] = members
        return members
    
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
    
    def flat_user_members_recursive(self, groupUid: str, recursionLevel: int = 0):
        if recursionLevel > MAX_RECURSION_LEVEL:
            self.logger.warning(f"recursion level exceeded for group {groupUid}")
            return None
        if groupUid in self.user_flats:
            return self.user_flats[groupUid]
        user = self.normalized_config.users.get(groupUid, None)
        if user is None:
            self.logger.error(f"object with uid {groupUid} not found in users of config")
            return None
        if user['user_member_refs'] is None or user['user_member_refs'] == '':
            if recursionLevel == 0:
                return None # group has no members / not a group
            return [groupUid] # leaf node, return itself
        members = set()
        for memberUid in user['user_member_refs'].split(fwo_const.list_delimiter): #TODO: adjust when/if users are refactored into objects
            flatMembers = self.flat_user_members_recursive(memberUid, recursionLevel + 1)
            if flatMembers is None:
                continue
            members.update(flatMembers)
        self.user_flats[groupUid] = members
        return members
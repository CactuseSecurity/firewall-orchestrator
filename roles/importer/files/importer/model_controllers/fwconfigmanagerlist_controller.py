import json
import time
import traceback
from copy import deepcopy

from fwo_base import ConfFormat
from fwo_const import IMPORT_TMP_PATH
from fwo_log import FWOLogger
from models.fwconfigmanager import FwConfigManager
from models.fwconfigmanagerlist import FwConfigManagerList
from models.import_state import ImportState
from utils.fwconfig_json_encoder import FwConfigJsonEncoder

"""
    a list of normalized configuratons of a firewall management to import
    FwConfigManagerList: [ FwConfigManager ]
"""


class FwConfigManagerListController(FwConfigManagerList):
    def __str__(self):
        return f"{self.ManagerSet!s})"

    def to_json_string(self, pretty_print: bool = False):
        json_dict = self.model_dump(by_alias=True)
        if pretty_print:
            return json.dumps(json_dict, indent=2, cls=FwConfigJsonEncoder)
        return json.dumps(json_dict)

    def mergeConfigs(self, conf2: "FwConfigManagerListController"):
        if self.ConfigFormat == conf2.ConfigFormat:
            self.ManagerSet.extend(conf2.ManagerSet)

    @staticmethod
    def generate_empty_config(is_super_manager: bool = False) -> "FwConfigManagerListController":
        """
        Generates an empty FwConfigManagerListController with a single empty FwConfigManager.
        """
        empty_config = FwConfigManagerListController()
        empty_config.ConfigFormat = ConfFormat.NORMALIZED
        empty_manager = FwConfigManager(
            manager_uid="",
            is_super_manager=is_super_manager,
            sub_manager_ids=[],
            configs=[],
            domain_name="",
            domain_uid="",
            manager_name="",
        )
        empty_config.add_manager(empty_manager)
        empty_config.native_config = {}
        return empty_config

    def get_all_zone_names(self, mgr_uid: str) -> set[str]:
        """
        Returns a list of all zone UIDs in the configuration.
        """
        all_zone_names: list[str] = []
        for mgr in self.ManagerSet:
            if mgr.is_super_manager or mgr.manager_uid == mgr_uid:
                for single_config in mgr.configs:
                    all_zone_names.extend(single_config.zone_objects.keys())
        return set(all_zone_names)

    def get_all_network_object_uids(self, mgr_uid: str) -> set[str]:
        """
        Returns a list of all network objects in the configuration.
        """
        all_network_objects: list[str] = []
        for mgr in self.ManagerSet:
            if mgr.is_super_manager or mgr.manager_uid == mgr_uid:
                for single_config in mgr.configs:
                    all_network_objects.extend(single_config.network_objects.keys())
        return set(all_network_objects)

    def get_all_service_object_uids(self, mgr_uid: str) -> set[str]:
        """
        Returns a list of all service objects in the configuration.
        """
        all_service_objects: list[str] = []
        for mgr in self.ManagerSet:
            if mgr.is_super_manager or mgr.manager_uid == mgr_uid:
                for single_config in mgr.configs:
                    all_service_objects.extend(single_config.service_objects.keys())
        return set(all_service_objects)

    def get_all_user_object_uids(self, mgr_uid: str) -> set[str]:
        """
        Returns a list of all user objects in the configuration.
        """
        all_user_objects: list[str] = []
        for mgr in self.ManagerSet:
            if mgr.is_super_manager or mgr.manager_uid == mgr_uid:
                for single_config in mgr.configs:
                    all_user_objects.extend(single_config.users.keys())
        return set(all_user_objects)

    def add_manager(self, manager: FwConfigManager):
        self.ManagerSet.append(manager)

    def get_first_manager(self):
        if len(self.ManagerSet) > 0:
            return self.ManagerSet[0]
        return None

    @staticmethod
    def get_device_uid_from_rulebase_name(rb_name: str) -> str:
        return rb_name

    @staticmethod
    def get_policy_uid_from_rulebase_name(rb_name: str) -> str:
        return rb_name

    def store_full_normalized_config_to_file(self, import_state: ImportState):
        if FWOLogger.is_debug_level(6):
            debug_start_time = int(time.time())
            try:
                normalized_config_filename = (
                    f"{IMPORT_TMP_PATH}/mgm_id_{import_state.mgm_details.mgm_id!s}_config_normalized.json"
                )

                config_copy_without_native = deepcopy(self)
                config_copy_without_native.native_config = {}

                with open(normalized_config_filename, "w") as json_data:
                    json_data.write(config_copy_without_native.to_json_string(pretty_print=True))
                time_write_debug_json = int(time.time()) - debug_start_time
                FWOLogger.debug(
                    f"storeFullNormalizedConfigToFile - writing normalized config json files duration {time_write_debug_json!s}s"
                )

                return normalized_config_filename

            except Exception:
                FWOLogger.error(
                    f"import_management - unspecified error while dumping normalized config to json file: {traceback.format_exc()!s}"
                )
                raise

    def is_native(self) -> bool:
        return self.native_config is not None

    def is_native_non_empty(self) -> bool:
        return self.native_config is not None and self.native_config != {}

    def contains_only_native(self) -> bool:
        return self.is_native() and (
            len(self.ManagerSet) == 0 or (len(self.ManagerSet) == 1 and len(self.ManagerSet[0].configs) == 0)
        )

    def native_config_is_empty(self) -> bool:
        return self.native_config is None or self.native_config == {}

    def normalized_config_is_empty(self) -> bool:
        return len(self.ManagerSet) == 1 and len(self.ManagerSet[0].configs) == 0

    def is_normalized(self) -> bool:
        return not self.is_native()

    def is_legacy(self) -> bool:
        return self.ConfigFormat == ConfFormat.IsLegacyConfigFormat

    def has_empty_config(self) -> bool:
        return self.native_config_is_empty() and self.normalized_config_is_empty()

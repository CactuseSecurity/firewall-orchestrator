from enum import Enum
from typing import Any

from fwo_const import LIST_DELIMITER
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager
from models.networkobject import NetworkObject
from models.serviceobject import ServiceObject
from netaddr import IPNetwork
from test.utils.partial_dict import PartialDict


class ChangelogObjectType(Enum):
    NETWORK_OBJECT = "obj"
    SERVICE_OBJECT = "svc"


class ChangelogChangeAction(Enum):
    INSERT = "I"
    DELETE = "D"
    CHANGE = "C"


class MockObjectsFactory:
    @staticmethod
    def get_mock_mgm_details() -> dict[str, Any]:
        return {
            "data": {
                "management": [
                    {
                        "id": 12,
                        "name": "Checkpoint_MDS",
                        "hostname": "192.168.10.5",  # Needs to NOT have "://" to trigger decryption logic
                        "ssh_port": 22,
                        # Required for: api_call_result["data"]["management"][0]["import_credential"]["secret"]
                        "import_credential": {"id": 45, "username": "api_admin", "secret": ""},
                        # Optional: The code iterates this if present
                        "subManagers": [
                            {
                                "id": 101,
                                "name": "CMA_London",
                                "hostname": "192.168.10.6",
                                "import_credential": {"id": 46, "secret": ""},
                            }
                        ],
                    }
                ]
            },
            "deviceType": {"id": 12, "name": "Checkpoint MDS"},
        }

    @staticmethod
    def add_standard_network_host_object(config: FwConfigNormalized, index: int | None = None) -> NetworkObject:
        network_object = NetworkObject(
            obj_uid="NetworkObject" + (str(index) if index is not None else ""),
            obj_name="NetworkObject" + (str(index) if index is not None else ""),
            obj_ip=IPNetwork("192.168.1.1/32"),
            obj_ip_end=IPNetwork("192.168.1.1/32"),
            obj_typ="host",
            obj_color="black",
        )
        config.network_objects[network_object.obj_uid] = network_object
        return network_object

    @staticmethod
    def add_standard_network_group_object(
        config: FwConfigNormalized, obj_members: list[NetworkObject] | None = None, index: int | None = None
    ) -> NetworkObject:
        network_object = NetworkObject(
            obj_uid="NetworkGroupObject" + (str(index) if index is not None else ""),
            obj_name="NetworkGroupObject" + (str(index) if index is not None else ""),
            obj_typ="group",
            obj_color="black",
            obj_member_names=LIST_DELIMITER.join([member.obj_name for member in obj_members])
            if obj_members is not None
            else None,
            obj_member_refs=LIST_DELIMITER.join([member.obj_uid for member in obj_members])
            if obj_members is not None
            else None,
        )
        config.network_objects[network_object.obj_uid] = network_object
        return network_object

    @staticmethod
    def add_standard_service_object(config: FwConfigNormalized, index: int | None = None) -> ServiceObject:
        service_object = ServiceObject(
            svc_uid="ServiceObject" + (str(index) if index is not None else ""),
            svc_name="ServiceObject" + (str(index) if index is not None else ""),
            svc_port=80,
            svc_port_end=80,
            svc_color="blue",
            svc_typ="simple",
        )
        config.service_objects[service_object.svc_uid] = service_object
        return service_object

    @staticmethod
    def add_standard_service_group_object(
        config: FwConfigNormalized, svc_members: list[ServiceObject] | None = None, index: int | None = None
    ) -> ServiceObject:
        service_object = ServiceObject(
            svc_uid="ServiceGroupObject" + (str(index) if index is not None else ""),
            svc_name="ServiceGroupObject" + (str(index) if index is not None else ""),
            svc_typ="group",
            svc_color="blue",
            svc_member_names=LIST_DELIMITER.join([member.svc_name for member in svc_members])
            if svc_members is not None
            else None,
            svc_member_refs=LIST_DELIMITER.join([member.svc_uid for member in svc_members])
            if svc_members is not None
            else None,
        )
        config.service_objects[service_object.svc_uid] = service_object
        return service_object

    @staticmethod
    def get_standard_fwconfig_manager(config: FwConfigNormalized | None = None) -> FwConfigManager:
        return FwConfigManager(
            manager_uid="mgr1",
            is_super_manager=True,
            sub_manager_ids=[],
            configs=[config] if config else [],
            domain_name="",
            domain_uid="",
            manager_name="mgr1",
        )

    @staticmethod
    def get_standard_changelog_return_value() -> dict[str, Any]:
        return {
            "data": {
                "add_nwobj_changelog": {
                    "nwobj_changelog": {
                        "id": 1,
                    }
                },
                "add_svc_changelog": {
                    "svc_changelog": {
                        "id": 2,
                    }
                },
            }
        }

    @staticmethod
    def build_changelog_object(
        change_action: ChangelogChangeAction,
        object_type: ChangelogObjectType,
        object_id: int,
        change_type_id: int = 3,
    ) -> PartialDict:
        return PartialDict(
            {
                "change_action": change_action.value,
                "change_type_id": change_type_id,
                f"new_{object_type.value}_id": object_id
                if change_action == ChangelogChangeAction.INSERT
                else int(f"{object_id}0")
                if change_action == ChangelogChangeAction.CHANGE
                else None,
                f"old_{object_type.value}_id": None if change_action == ChangelogChangeAction.INSERT else object_id,
                "unique_name": str(object_id)
                if change_action != ChangelogChangeAction.CHANGE
                else str(int(f"{object_id}0")),
            }
        )

    @staticmethod
    def get_changelog_object_insert_delete(change_type_id: int = 3) -> list[PartialDict]:
        return [
            MockObjectsFactory.build_changelog_object(
                change_action=ChangelogChangeAction.INSERT,
                object_type=ChangelogObjectType.NETWORK_OBJECT,
                object_id=1,
                change_type_id=change_type_id,
            ),
            MockObjectsFactory.build_changelog_object(
                change_action=ChangelogChangeAction.DELETE,
                object_type=ChangelogObjectType.NETWORK_OBJECT,
                object_id=2,
                change_type_id=change_type_id,
            ),
        ]

    @staticmethod
    def get_changelog_object_insert_delete_change(change_type_id: int = 3) -> list[PartialDict]:
        return [
            MockObjectsFactory.build_changelog_object(
                change_action=ChangelogChangeAction.INSERT,
                object_type=ChangelogObjectType.NETWORK_OBJECT,
                object_id=1,
                change_type_id=change_type_id,
            ),
            MockObjectsFactory.build_changelog_object(
                change_action=ChangelogChangeAction.DELETE,
                object_type=ChangelogObjectType.NETWORK_OBJECT,
                object_id=2,
                change_type_id=change_type_id,
            ),
            MockObjectsFactory.build_changelog_object(
                change_action=ChangelogChangeAction.CHANGE,
                object_type=ChangelogObjectType.NETWORK_OBJECT,
                object_id=1,
                change_type_id=change_type_id,
            ),
        ]

    @staticmethod
    def get_changelog_svc_objects_insert_delete(change_type_id: int = 3) -> list[PartialDict]:
        return [
            MockObjectsFactory.build_changelog_object(
                change_action=ChangelogChangeAction.INSERT,
                object_type=ChangelogObjectType.SERVICE_OBJECT,
                object_id=3,
                change_type_id=change_type_id,
            ),
            MockObjectsFactory.build_changelog_object(
                change_action=ChangelogChangeAction.DELETE,
                object_type=ChangelogObjectType.SERVICE_OBJECT,
                object_id=4,
                change_type_id=change_type_id,
            ),
        ]

    @staticmethod
    def get_changelog_svc_objects_insert_delete_change(change_type_id: int = 3) -> list[PartialDict]:
        return [
            MockObjectsFactory.build_changelog_object(
                change_action=ChangelogChangeAction.INSERT,
                object_type=ChangelogObjectType.SERVICE_OBJECT,
                object_id=3,
                change_type_id=change_type_id,
            ),
            MockObjectsFactory.build_changelog_object(
                change_action=ChangelogChangeAction.DELETE,
                object_type=ChangelogObjectType.SERVICE_OBJECT,
                object_id=4,
                change_type_id=change_type_id,
            ),
            MockObjectsFactory.build_changelog_object(
                change_action=ChangelogChangeAction.CHANGE,
                object_type=ChangelogObjectType.SERVICE_OBJECT,
                object_id=3,
                change_type_id=change_type_id,
            ),
        ]

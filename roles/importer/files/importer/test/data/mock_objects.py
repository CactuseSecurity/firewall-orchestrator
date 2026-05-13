from enum import Enum
from typing import Any

from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager
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
            "id": 12,
        }

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
        def get_new_id() -> int | None:
            if change_action == ChangelogChangeAction.INSERT:
                return object_id
            if change_action == ChangelogChangeAction.CHANGE:
                return int(f"{object_id}0")
            return None

        return PartialDict(
            {
                "change_action": change_action.value,
                "change_type_id": change_type_id,
                f"new_{object_type.value}_id": get_new_id(),
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

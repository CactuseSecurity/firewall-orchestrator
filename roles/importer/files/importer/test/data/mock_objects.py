from typing import Any

from fwo_const import LIST_DELIMITER
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager
from models.networkobject import NetworkObject
from models.serviceobject import ServiceObject
from netaddr import IPNetwork


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

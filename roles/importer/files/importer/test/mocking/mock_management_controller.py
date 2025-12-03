
from model_controllers.management_controller import (
    ManagementController, 
    DeviceInfo, 
    ConnectionInfo, 
    CredentialInfo, 
    ManagerInfo, 
    DomainInfo
)


class MockManagementController(ManagementController):
    def __init__(self, is_super_manager: bool = False):
        """
            Initializes with minimal required data for testing.
        """
        # Call parent constructor with required parameters
        super().__init__(
            mgm_id=3,
            uid="mock-uid",
            devices=[],
            device_info=DeviceInfo(
                name="Mock Management",
                type_name="MockDevice",
                type_version="1.0"
            ),
            connection_info=ConnectionInfo(
                hostname="mock.example.com",
                port=443
            ),
            importer_hostname="mock-importer",
            credential_info=CredentialInfo(
                secret="mock-secret",
                import_user="mock-user",
                cloud_client_id="",
                cloud_client_secret=""
            ),
            manager_info=ManagerInfo(
                is_super_manager=is_super_manager,
                sub_manager_ids=[],
                sub_managers=[]
            ),
            domain_info=DomainInfo(
                domain_name="mock-domain",
                domain_uid="mock-domain-uid"
            ),
            import_disabled=False
        )
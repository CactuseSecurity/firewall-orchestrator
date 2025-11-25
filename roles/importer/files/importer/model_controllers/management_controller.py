import hashlib
from dataclasses import dataclass
from typing import Any

from models.management import Management
from fwo_exceptions import FwLoginFailed
from models.gateway import Gateway
from fwconfig_base import replace_none_with_empty
from fwo_const import GRAPHQL_QUERY_PATH
from fwo_api import FwoApi
from fwo_encrypt import decrypt, read_main_key
from fwo_exceptions import SecretDecryptionFailed, FwoApiFailure

@dataclass
class DeviceInfo:
    name: str = ''
    type_name: str = ''
    type_version: str = ''

@dataclass
class ConnectionInfo:
    hostname: str = ''
    port: int = 443

@dataclass
class CredentialInfo:
    secret: str = ''
    import_user: str = ''
    cloud_client_id: str = ''
    cloud_client_secret: str = ''

@dataclass
class ManagerInfo:
    is_super_manager: bool = False
    sub_manager_ids: list[int]|None = None
    sub_managers: list['Management']|None = None

@dataclass
class DomainInfo:
    domain_name: str = ''
    domain_uid: str = ''

class ManagementController(Management):
    def __init__(self, mgm_id: int, uid: str, devices: list[dict[str, Any]], device_info: DeviceInfo,
                 connection_info: ConnectionInfo, importer_hostname: str, credential_info: CredentialInfo,
                 manager_info: ManagerInfo, domain_info: DomainInfo, 
                 import_disabled: bool = False):
        
        self.id = mgm_id
        self.uid = uid
        self.devices = devices
        self.import_disabled = import_disabled
        
        # Device info
        self.name = device_info.name
        self.device_type_name = device_info.type_name
        self.device_type_version = device_info.type_version
        
        # Connection info
        self.hostname = connection_info.hostname
        self.port = connection_info.port

        # Importer Host info
        self.importer_hostname = importer_hostname

        # Credential info
        self.import_user = credential_info.import_user
        self.secret = credential_info.secret
        self.cloud_client_id = credential_info.cloud_client_id
        self.cloud_client_secret = credential_info.cloud_client_secret

        # Manager info
        self.is_super_manager = manager_info.is_super_manager
        self.sub_manager_ids = manager_info.sub_manager_ids or []
        self.sub_managers = manager_info.sub_managers or []

        # Current Sub-Manager info for multi-management imports
        self.current_mgm_id = mgm_id
        self.current_mgm_is_super_manager = manager_info.is_super_manager
        
        # Domain info
        self.domain_name = domain_info.domain_name
        self.domain_uid = domain_info.domain_uid

    @classmethod
    def from_json(cls, json_dict: dict[str, Any]) -> "ManagementController":
        device_info = DeviceInfo(
            name=json_dict['name'],
            type_name=json_dict['deviceType']['name'],
            type_version=json_dict['deviceType']['version']
        )
        
        connection_info = ConnectionInfo(
            hostname=json_dict['hostname'],
            port=json_dict['port'],
        )
        
        credential_info = CredentialInfo(
            import_user=json_dict['import_credential']['user'],
            secret=json_dict['import_credential']['secret'],
            cloud_client_id=json_dict['import_credential']['cloud_client_id'],
            cloud_client_secret=json_dict['import_credential']['cloud_client_secret']
        )
        
        manager_info = ManagerInfo(
            is_super_manager=json_dict["isSuperManager"],
            sub_manager_ids=[subManager["id"] for subManager in json_dict["subManagers"]],
            sub_managers=[cls.from_json(subManager) for subManager in json_dict["subManagers"]]
        )
        
        domain_info = DomainInfo(
            domain_name=json_dict['configPath'],
            domain_uid=json_dict['domainUid']
        )

        return cls(
            mgm_id=json_dict['id'],
            uid=json_dict['uid'],
            devices=json_dict['devices'],
            device_info=device_info,
            connection_info=connection_info,
            importer_hostname=json_dict['importerHostname'],
            credential_info=credential_info,
            manager_info=manager_info,
            domain_info=domain_info,
            import_disabled=json_dict['importDisabled']
        )


    def __str__(self):
        return f"{self.hostname}({self.id})"
    

    # TODO: fix device type URIs
    def buildFwApiString(self):
        if self.device_type_name == 'Check Point':
            return f"https://{self.hostname}:{str(self.port)}/web_api/"
        elif self.device_type_name == 'CiscoFMC':
            return f"https://{self.hostname}:{str(self.port)}/api/fmc_platform/v1/"
        elif self.device_type_name == 'Fortinet':
            return f"https://{self.hostname}:{str(self.port)}/api/v2/"
        elif self.device_type_name == 'FortiAdom':
            return f"https://{self.hostname}:{str(self.port)}/jsonrpc"
        elif self.device_type_name == 'FortiManager':
            return f"https://{self.hostname}:{str(self.port)}/jsonrpc"
        elif self.device_type_name == 'PaloAlto':
            return f"https://{self.hostname}:{str(self.port)}/restapi/v10.0/"
        elif self.device_type_name == 'PaloAltoLegacy':
            return f"https://{self.hostname}:{str(self.port)}/restapi/v10.0/"
        else:
            raise FwLoginFailed(f"Unsupported device type: {self.device_type_name}")


    def getDomainString(self) -> str:
        return self.domain_uid if self.domain_uid != None else self.domain_name # type: ignore #TODO: check if None check is needed if yes, change type


    @classmethod
    def build_gateway_list(cls, mgmDetails: "ManagementController") -> list['Gateway']:
        devs: list['Gateway'] = []
        for dev in mgmDetails.devices:
            # check if gateway import is enabled
            if 'do_not_import' in dev and dev['do_not_import']:
                continue
            devs.append(Gateway(Name = dev['name'], Uid = f"{dev['name']}/{mgmDetails.calc_manager_uid_hash()}"))
        return devs


    def calc_manager_uid_hash(self):
        combination = f"""
            {replace_none_with_empty(self.hostname)}
            {replace_none_with_empty(str(self.port))}
            {replace_none_with_empty(self.domain_uid)}
            {replace_none_with_empty(self.domain_name)}
        """
        return hashlib.sha256(combination.encode()).hexdigest()


    def get_mgm_details(self, api_conn: FwoApi, mgm_id: int) -> dict[str, Any]:
        get_mgm_details_query = FwoApi.get_graphql_code([
                    GRAPHQL_QUERY_PATH + "device/getSingleManagementDetails.graphql",
                    GRAPHQL_QUERY_PATH + "device/fragments/managementDetails.graphql",
                    GRAPHQL_QUERY_PATH + "device/fragments/subManagements.graphql",
                    GRAPHQL_QUERY_PATH + "device/fragments/deviceTypeDetails.graphql",
                    GRAPHQL_QUERY_PATH + "device/fragments/importCredentials.graphql"])

        api_call_result = api_conn.call(get_mgm_details_query, query_variables={'mgmId': mgm_id })
        if api_call_result is None or 'data' not in api_call_result or 'management' not in api_call_result['data'] or len(api_call_result['data']['management'])<1: #type: ignore #TODO: check if api_call_result can be None
            raise FwoApiFailure('did not succeed in getting management details from FWO API')

        if not '://' in api_call_result['data']['management'][0]['hostname']:
            # only decrypt if we have a real management and are not fetching the config from an URL
            # decrypt secret read from API
            try:
                secret = api_call_result['data']['management'][0]['import_credential']['secret']
                decrypted_secret = decrypt(secret, read_main_key())
            except ():
                raise SecretDecryptionFailed
            api_call_result['data']['management'][0]['import_credential']['secret'] = decrypted_secret
            if 'subManagers' in api_call_result['data']['management'][0]:
                for sub_mgm in api_call_result['data']['management'][0]['subManagers']:
                    try:
                        secret = sub_mgm['import_credential']['secret']
                        decrypted_secret = decrypt(secret, read_main_key())
                    except ():
                        raise SecretDecryptionFailed
                    sub_mgm['import_credential']['secret'] = decrypted_secret
        return api_call_result['data']['management'][0]


import hashlib
from dataclasses import dataclass

from models.management import Management
from fwo_exceptions import FwLoginFailed
from models.gateway import Gateway
from fwconfig_base import replaceNoneWithEmpty
from fwo_const import graphql_query_path
from fwo_api import FwoApi
from services.service_provider import ServiceProvider, Services
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
    def __init__(self, mgm_id: int, uid: str, devices: dict, device_info: DeviceInfo,
                 connection_info: ConnectionInfo, importer_hostname: str, credential_info: CredentialInfo,
                 manager_info: ManagerInfo, domain_info: DomainInfo, 
                 import_disabled: bool = False):
        
        self.Id = mgm_id
        self.Uid = uid
        self.Devices = devices
        self.ImportDisabled = import_disabled
        
        # Device info
        self.Name = device_info.name
        self.DeviceTypeName = device_info.type_name
        self.DeviceTypeVersion = device_info.type_version
        
        # Connection info
        self.Hostname = connection_info.hostname
        self.Port = connection_info.port

        # Importer Host info
        self.ImporterHostname = importer_hostname

        # Credential info
        self.ImportUser = credential_info.import_user
        self.Secret = credential_info.secret
        
        # Manager info
        self.IsSuperManager = manager_info.is_super_manager
        self.SubManagerIds = manager_info.sub_manager_ids or []
        self.SubManagers = manager_info.sub_managers or []

        # Current Sub-Manager info for multi-management imports
        self.CurrentMgmId = mgm_id
        self.CurrentMgmIsSuperManager = manager_info.is_super_manager
        
        # Domain info
        self.DomainName = domain_info.domain_name
        self.DomainUid = domain_info.domain_uid

    @classmethod
    def fromJson(cls, json_dict: dict):
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
            secret=json_dict['import_credential']['secret']
        )
        
        manager_info = ManagerInfo(
            is_super_manager=json_dict["isSuperManager"],
            sub_manager_ids=[subManager["id"] for subManager in json_dict["subManagers"]],
            sub_managers=[cls.fromJson(subManager) for subManager in json_dict["subManagers"]]
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

    # ...existing code...

    def __str__(self):
        return f"{self.Hostname}({self.Id})"
    

    # TODO: fix device type URIs
    def buildFwApiString(self):
        if self.DeviceTypeName == 'Check Point':
            return f"https://{self.Hostname}:{str(self.Port)}/web_api/"
        elif self.DeviceTypeName == 'CiscoFMC':
            return f"https://{self.Hostname}:{str(self.Port)}/api/fmc_platform/v1/"
        elif self.DeviceTypeName == 'Fortinet':
            return f"https://{self.Hostname}:{str(self.Port)}/api/v2/"
        elif self.DeviceTypeName == 'FortiAdom':
            return f"https://{self.Hostname}:{str(self.Port)}/jsonrpc"
        elif self.DeviceTypeName == 'FortiManager':
            return f"https://{self.Hostname}:{str(self.Port)}/jsonrpc"
        elif self.DeviceTypeName == 'PaloAlto':
            return f"https://{self.Hostname}:{str(self.Port)}/restapi/v10.0/"
        elif self.DeviceTypeName == 'PaloAltoLegacy':
            return f"https://{self.Hostname}:{str(self.Port)}/restapi/v10.0/"
        else:
            raise FwLoginFailed(f"Unsupported device type: {self.DeviceTypeName}")


    def getDomainString(self):
        return self.DomainUid if self.DomainUid != None else self.DomainName


    @classmethod
    def buildGatewayList(cls, mgmDetails: "ManagementController") -> list['Gateway']:
        devs = []
        for dev in mgmDetails.Devices:
            # check if gateway import is enabled
            if 'do_not_import' in dev and dev['do_not_import']: # TODO: get this key from the device
                continue
            devs.append(Gateway(Name = dev['name'], Uid = f"{dev['name']}/{mgmDetails.calcManagerUidHash()}"))
        return devs


    def calcManagerUidHash(self):
        combination = f"""
            {replaceNoneWithEmpty(self.Hostname)}
            {replaceNoneWithEmpty(self.Port)}
            {replaceNoneWithEmpty(self.DomainUid)}
            {replaceNoneWithEmpty(self.DomainName)}
        """
        return hashlib.sha256(combination.encode()).hexdigest()


    def get_mgm_details(self, api_conn, mgm_id, debug_level=0):

        service_provider = ServiceProvider()
        _global_state = service_provider.get_service(Services.GLOBAL_STATE)

        getMgmDetailsQuery = FwoApi.get_graphql_code([
                    graphql_query_path + "device/getSingleManagementDetails.graphql",
                    graphql_query_path + "device/fragments/managementDetails.graphql",
                    graphql_query_path + "device/fragments/subManagements.graphql",
                    graphql_query_path + "device/fragments/deviceTypeDetails.graphql",
                    graphql_query_path + "device/fragments/importCredentials.graphql"])

        api_call_result = api_conn.call(getMgmDetailsQuery, query_variables={'mgmId': mgm_id })
        if api_call_result is None or 'data' not in api_call_result or 'management' not in api_call_result['data'] or len(api_call_result['data']['management'])<1:
            raise FwoApiFailure('did not succeed in getting management details from FWO API')

        if not '://' in api_call_result['data']['management'][0]['hostname']:
            # only decrypt if we have a real management and are not fetching the config from an URL
            # decrypt secret read from API
            try:
                secret = api_call_result['data']['management'][0]['import_credential']['secret']
                decryptedSecret = decrypt(secret, read_main_key())
            except ():
                raise SecretDecryptionFailed
            api_call_result['data']['management'][0]['import_credential']['secret'] = decryptedSecret
            if 'subManagers' in api_call_result['data']['management'][0]:
                for subMgm in api_call_result['data']['management'][0]['subManagers']:
                    try:
                        secret = subMgm['import_credential']['secret']
                        decryptedSecret = decrypt(secret, read_main_key())
                    except ():
                        raise SecretDecryptionFailed
                    subMgm['import_credential']['secret'] = decryptedSecret
        return api_call_result['data']['management'][0]


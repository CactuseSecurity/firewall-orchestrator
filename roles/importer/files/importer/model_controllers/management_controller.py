import hashlib

from models.management import Management
from fwo_exceptions import FwLoginFailed
from models.gateway import Gateway
from fwconfig_base import replaceNoneWithEmpty
from fwo_const import graphql_query_path
from fwo_api import FwoApi
from services.service_provider import ServiceProvider, Services
from fwo_encrypt import decrypt, read_main_key
from fwo_exceptions import SecretDecryptionFailed, FwoApiFailure

class ManagementController(Management):

    def __init__(self, hostname: str, id: int, uid: str, devices: dict,
                 name: str, deviceTypeName: str, deviceTypeVersion: str,
                 importDisabled: bool = False, importerHostname: str = '',  
                 port: int = 443, secret: str = '', importUser: str = '', isSuperManager: bool = False, 
                 subManagerIds: list[int] = [], subManagers: list['Management'] = [],
                 domainName: str = '', domainUid: str = ''):
        
        subManagers: list['Management'] = []
        self.Hostname = hostname
        self.Id = id
        self.Uid = uid
        self.ImportDisabled = importDisabled
        self.Devices = devices
        self.ImporterHostname = importerHostname
        self.Name = name
        self.DeviceTypeName = deviceTypeName
        self.DeviceTypeVersion = deviceTypeVersion
        self.Port = port
        self.ImportUser = importUser
        self.Secret = secret
        self.IsSuperManager = isSuperManager
        self.SubManagerIds = subManagerIds
        self.SubManagers = subManagers
        self.DomainName = domainName
        self.DomainUid = domainUid

    @classmethod
    def fromJson(cls, json_dict: dict):
        Hostname = json_dict['hostname']
        Id = json_dict['id']
        Uid = json_dict['uid']
        ImportDisabled = json_dict['importDisabled']
        Devices = json_dict['devices']
        ImporterHostname = json_dict['importerHostname']
        Name = json_dict['name']
        DeviceTypeName = json_dict['deviceType']['name']
        DeviceTypeVersion = json_dict['deviceType']['version']
        Port = json_dict['port']
        ImportUser = json_dict['import_credential']['user']
        Secret = json_dict['import_credential']['secret']
        IsSuperManager = json_dict["isSuperManager"]
        SubManagerIds = [subManager["id"] for subManager in json_dict["subManagers"]]
        SubManagers = [cls.fromJson(subManager) for subManager in json_dict["subManagers"]]
        domainName = json_dict['configPath']
        domainUid = json_dict['domainUid']

        return cls(hostname=Hostname, id=Id, uid=Uid, importDisabled=ImportDisabled, devices=Devices, 
                   importerHostname=ImporterHostname, name=Name, deviceTypeName=DeviceTypeName, 
                   deviceTypeVersion=DeviceTypeVersion, port=Port, importUser=ImportUser, secret=Secret, 
                   isSuperManager = IsSuperManager, subManagerIds = SubManagerIds, 
                   subManagers = SubManagers, domainName = domainName, domainUid = domainUid)

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
    def buildGatewayList(cls, mgmDetails: Management) -> list['Gateway']:
        devs = []
        for dev in mgmDetails.Devices:
            # check if gateway import is enabled
            if 'do_not_import' in dev and dev['do_not_import']: # TODO: get this key from the device
                continue
            devs.append(Gateway(Name = dev['name'], Uid = f"{dev['name']}/{cls.calcManagerUidHash(mgmDetails)}"))
        return devs


    @classmethod
    def calcManagerUidHash(cls, mgm_details):
        combination = f"""
            {replaceNoneWithEmpty(mgm_details.Hostname)}
            {replaceNoneWithEmpty(mgm_details.Port)}
            {replaceNoneWithEmpty(mgm_details.DomainUid)}
            {replaceNoneWithEmpty(mgm_details.DomainName)}
        """
        return hashlib.sha256(combination.encode()).hexdigest()


    def get_mgm_details(self, api_conn, mgm_id, debug_level=0):

        service_provider = ServiceProvider()
        _global_state = service_provider.get_service(Services.GLOBAL_STATE)

        getMgmDetailsQuery = FwoApi.get_graphql_code([
                    graphql_query_path + "device/getSingleManagementDetails.graphql",
                    graphql_query_path + "device/fragments/managementDetails.graphql",
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


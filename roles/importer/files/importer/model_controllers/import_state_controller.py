import time
from datetime import datetime
import urllib3
import traceback

import fwo_globals

from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_log import getFwoLogger
from fwo_config import readConfig
from fwo_const import fwo_config_filename, graphql_query_path
from fwo_exceptions import FwoImporterError
from models.import_state import ImportState
from model_controllers.fworch_config_controller import FworchConfigController
from model_controllers.management_controller import ManagementController, DeviceInfo, ConnectionInfo, CredentialInfo, ManagerInfo, DomainInfo
from model_controllers.import_statistics_controller import ImportStatisticsController


"""Used for storing state during import process per management"""
class ImportStateController(ImportState):

    api_connection:FwoApi
    api_call: FwoApiCall
    management_map: dict[str, int]  # maps management uid to management id

    def __init__(self, debugLevel, configChangedSinceLastImport, fwoConfig, mgmDetails, jwt, force, 
                 version=8, isFullImport=False, isInitialImport=False, isClearingImport=False, verifyCerts=False, LastSuccessfulImport=None):
        self.Stats = ImportStatisticsController()
        self.StartTime = int(time.time())
        self.DebugLevel = debugLevel
        self.VerifyCerts = verifyCerts
        self.ConfigChangedSinceLastImport = configChangedSinceLastImport
        self.FwoConfig = fwoConfig
        self.MgmDetails = ManagementController.fromJson(mgmDetails)
        self.ImportId = -1
        self.Jwt = jwt
        self.ImportFileName = ""
        self.ForceImport = force
        self.ImportVersion = int(version)
        self.IsFullImport = isFullImport
        self.IsInitialImport = isInitialImport
        self.IsClearingImport = isClearingImport
        self.RulbaseToGatewayMap = {}
        self.LastSuccessfulImport = LastSuccessfulImport
        self.api_connection = FwoApi(fwoConfig.FwoApiUri, jwt)
        self.api_call = FwoApiCall(self.api_connection)

    def __str__(self):
        return f"{str(self.MgmDetails)}(import_id={self.ImportId})"
    
    def setImportFileName(self, importFileName):
        self.ImportFileName = importFileName

    def setImportId(self, importId):
        self.ImportId = importId

    def increaseErrorCounter(self, errorNo):
        self.Stats.ErrorCount = self.Stats.ErrorCount + errorNo

    def increaseErrorCounterByOne(self):
        self.increaseErrorCounter(1)

    def appendErrorString(self, errorStr):
        self.Stats.ErrorDetails.append(errorStr)

    def getErrors(self):
        return self.Stats.ErrorDetails

    def getErrorString(self):
        return str(self.Stats.ErrorDetails)
    
    def addError(self, error, log=False):
        self.increaseErrorCounterByOne()
        self.appendErrorString(str(error))
        if log and not self.Stats.ErrorAlreadyLogged:
            logger = getFwoLogger()
            logger.error(str(error))
            # self.Stats.ErrorAlreadyLogged = True


    @classmethod
    def initializeImport(cls, mgmId, fwo_api_uri, jwt,
                         debugLevel=0, suppressCertWarnings=False, 
                         sslVerification=False, force=False, version=8,
                         isClearingImport=False, isFullImport=False, isInitialImport=False,
                         ):

        def _check_input_parameters(mgmId):
            if mgmId is None:
                raise ValueError("parameter mgm_id is mandatory")

        logger = getFwoLogger()
        _check_input_parameters(mgmId)

        fwoConfig = FworchConfigController.fromJson(readConfig(fwo_config_filename))

        api_conn = FwoApi(ApiUri=fwoConfig.FwoApiUri, Jwt=jwt)
        api_call = FwoApiCall(api_conn)
        # set global https connection values
        fwo_globals.set_global_values (suppress_cert_warnings_in=suppressCertWarnings, verify_certs_in=sslVerification, debug_level_in=debugLevel)
        if fwo_globals.suppress_cert_warnings:
            urllib3.disable_warnings()  # suppress ssl warnings only    

        try: # get mgm_details (fw-type, port, ip, user credentials):
            mgm_controller = ManagementController(
                mgm_id=int(mgmId), uid='', devices={},
                device_info=DeviceInfo(),
                connection_info=ConnectionInfo(),
                importer_hostname='',
                credential_info=CredentialInfo(),
                manager_info=ManagerInfo(),
                domain_info=DomainInfo()
            )
            mgmDetails = mgm_controller.get_mgm_details(api_conn, mgmId, debugLevel) 
        except Exception as e:
            logger.error(f"import_management - error while getting fw management details for mgm={str(mgmId)}: {str(traceback.format_exc())}")
            raise

        try: # get last import data
            last_import_id, last_import_date = api_call.get_last_complete_import({"mgmId": int(mgmId)}, debug_level=0)
        except Exception:
            logger.error("import_management - error while getting last import data for mgm=" + str(mgmId) )
            raise

        result = cls (
            debugLevel = int(debugLevel),
            configChangedSinceLastImport = True,
            fwoConfig = fwoConfig,
            mgmDetails = mgmDetails,
            jwt = jwt,
            force = force,
            version = version,
            isClearingImport=isClearingImport,
            isFullImport=isFullImport,
            isInitialImport=(last_import_date == ""),
            verifyCerts=sslVerification,
            LastSuccessfulImport=last_import_date,
        )

        result.getPastImportInfos()
        result.setCoreData()

        if type(result) is str:
            logger.error("error while getting import state")
            raise FwoImporterError("error while getting import state")
        
        return result 


    def getPastImportInfos(self):        
        logger = getFwoLogger()
        api_call = FwoApiCall(FwoApi(ApiUri=self.FwoConfig.FwoApiUri, Jwt=self.Jwt))
        try: # get past import details (LastFullImport, ...):
            day_string = api_call.get_config_value(key='dataRetentionTime')
            if day_string:
                self.DataRetentionDays = int(day_string)
            self.LastFullImportId, self.lastFullImportDate = \
                api_call.get_last_complete_import({"mgmId": int(self.MgmDetails.Id)}, self.DebugLevel) 
        except Exception:
            logger.error(f"import_management - error while getting past import details for mgm={str(self.MgmDetails.Id)}: {str(traceback.format_exc())}")
            raise

        if self.lastFullImportDate != "":
            self.LastSuccessfulImport = self.lastFullImportDate

            # Convert the string to a datetime object
            pastDate = datetime.strptime(self.lastFullImportDate, "%Y-%m-%dT%H:%M:%S.%f")
            now = datetime.now()
            difference = now - pastDate
            self.DaysSinceLastFullImport = difference.days
        else:
            self.DaysSinceLastFullImport = 0
            # self.IsInitialImport = True


    def setCoreData(self):
        api_call = FwoApiCall(FwoApi(ApiUri=self.FwoConfig.FwoApiUri, Jwt=self.Jwt))
        self.SetTrackMap(api_call)
        self.SetActionMap(api_call)
        self.SetLinkTypeMap(api_call)
        self.SetGatewayMap(api_call)
        self.SetManagementMap(api_call)
        self.SetColorRefMap(api_call)

        # the following maps will be empty when starting first import of a management
        self.SetRulebaseMap(api_call)
        self.SetRuleMap(api_call)

    def SetActionMap(self, api_call):
        query = "query getActionMap { stm_action { action_name action_id allowed } }"
        try:
            result = api_call.call(query=query, query_variables={})
        except Exception:
            logger = getFwoLogger()
            logger.error('Error while getting stm_action')
            raise
        
        map: dict[str, int] = {}
        for action in result['data']['stm_action']:
            map.update({action['action_name']: action['action_id']})
        self.Actions = map

    def SetTrackMap(self, api_call):
        query = "query getTrackMap { stm_track { track_name track_id } }"
        try:
            result = api_call.call(query=query, query_variables={})
        except Exception:
            logger = getFwoLogger()
            logger.error('Error while getting stm_track')
            raise

        track_map: dict[str, int] = {}
        for track in result['data']['stm_track']:
            track_map.update({track['track_name']: track['track_id']})
        self.Tracks = track_map

    def SetLinkTypeMap(self, api_call):
        query = "query getLinkType { stm_link_type { id name } }"
        try:
            result = api_call.call(query=query, query_variables={})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting stm_link_type")
            raise
        
        link_map: dict[str, int] = {}
        for track in result['data']['stm_link_type']:
            link_map.update({track['name']: track['id']})
        self.LinkTypes = link_map

    def SetColorRefMap(self, api_call):
        get_colors_query = FwoApi.get_graphql_code([graphql_query_path + "stmTables/getColors.graphql"])

        try:
            result = api_call.call(query=get_colors_query, query_variables={})
        except Exception:
            logger = getFwoLogger()
            logger.error('Error while getting stm_color')
            raise
        
        color_map: dict[str, int] = {}
        for color in result['data']['stm_color']:
            color_map.update({color['color_name']: color['color_id']})
        self.ColorMap = color_map


    # limited to the current mgm_id
    # creates a dict with key = rulebase.name and value = rulebase.id
    def SetRulebaseMap(self, api_call):

        # TODO: maps need to be updated directly after data changes
        query = """query getRulebaseMap($mgmId: Int) { rulebase(where:{mgm_id: {_eq: $mgmId}, removed:{_is_null:true }}) { id name uid } }"""
        try:
            result = api_call.call(query=query, query_variables= {"mgmId": self.MgmDetails.CurrentMgmId})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting rulebases")
            self.RulebaseMap = {}
            raise
        
        m = {}
        for rulebase in result['data']['rulebase']:
            rbid = rulebase['id']
            m.update({rulebase['name']: rbid})
            m.update({rulebase['uid']: rbid})
        self.RulebaseMap = m

    # limited to the current mgm_id
    # creats a dict with key = rule.uid and value = rule.id 
    # should be called sparsely, as there might be a lot of rules for a mgmt
    def SetRuleMap(self, api_call):
        query = """query getRuleMap($mgmId: Int) { rule(where:{mgm_id: {_eq: $mgmId}, removed:{_is_null:true }}) { rule_id rule_uid } }"""
        try:
            result = api_call.call(query=query, query_variables= {"mgmId": self.MgmDetails.Id})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting rules")
            self.RuleMap = {}
            raise
        
        m = {}
        for rule in result['data']['rule']:
            m.update({rule['rule_uid']: rule['rule_id']})
        self.RuleMap = m

    # getting all gateways (not limitited to the current mgm_id) to support super managements
    # creates a dict with key = gateway.uid  and value = gateway.id
    # and also            key = gateway.name and value = gateway.id
    def SetGatewayMap(self, api_call):
        query = """
            query getGatewayMap($mgmId: Int) {
                device {
                    dev_id
                    dev_name
                    dev_uid
                }
            }
    """
        try:
            result = api_call.call(query=query, query_variables={})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting gateways")
            self.GatewayMap = {}
            raise
        
        m = {}
        for gw in result['data']['device']:
            m.update({gw['dev_name']: gw['dev_id']})
            m.update({gw['dev_uid']: gw['dev_id']})
        self.GatewayMap = m

    # getting all managements (not limitited to the current mgm_id) to support super managements
    # creates a dict with key = management.uid  and value = management.id
    def SetManagementMap(self, api_call):
        query = """
            query getManagementMap($mgmId: Int!) {
                management(where: {mgm_id: {_eq: $mgmId}}) {
                    mgm_id
                    mgm_uid
                    sub_managers: managementByMultiDeviceManagerId {
                        mgm_id
                        mgm_uid
                    }
                }
            }
        """
        try:
            result = api_call.call(query=query, query_variables= {"mgmId": self.MgmDetails.Id})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting managements")
            self.ManagementMap: dict[str, int] = {}
            raise
        
        m: dict[str, int] = {}
        mgm = result['data']['management'][0]
        m.update({mgm['mgm_uid']: mgm['mgm_id']})
        for sub_mgr in mgm['sub_managers']:
            m.update({sub_mgr['mgm_uid']: sub_mgr['mgm_id']})

        self.ManagementMap = m

    def lookupRule(self, ruleUid):
        return self.RuleMap.get(ruleUid, None)

    def lookupAction(self, actionStr):
        return self.Actions.get(actionStr.lower(), None)

    def lookupTrack(self, trackStr):
        return self.Tracks.get(trackStr.lower(), None)

    def lookupRulebaseId(self, rulebaseUid):
        rulebaseId = self.RulebaseMap.get(rulebaseUid, None)
        if rulebaseId is None:
            logger = getFwoLogger()
            logger.error(f"Rulebase {rulebaseUid} not found")
        return rulebaseId

    def lookupLinkType(self, linkUid):
        return self.LinkTypes.get(linkUid, -1)

    def lookupGatewayId(self, gwUid):
        return self.GatewayMap.get(gwUid, None)

    def lookupManagementId(self, mgmUid):
        if not self.ManagementMap.get(mgmUid, None):
            logger = getFwoLogger()
            logger.error(f"fwo_api:import_latest_config - no mgm id found for current manager uid '{mgmUid}'")
        return self.ManagementMap.get(mgmUid, None)


    def lookupColorId(self, color_str):
        return self.ColorMap.get(color_str, 1)  # 1 = forground color black
    

    def delete_import(self):
        logger = getFwoLogger()

        delete_import_mutation = """
            mutation deleteImport($importId: bigint!) {
                delete_import_control(where: {control_id: {_eq: $importId}}) { affected_rows }
            }"""

        try:
            result = self.api_connection.call(delete_import_mutation, query_variables={"importId": self.ImportId})
            api_changes = result['data']['delete_import_control']['affected_rows']
        except Exception:
            logger.exception(
                "fwo_api: failed to unlock import for import id " + str(self.ImportId))
            return 1  # signaling an error
        logger.info(f"removed import with id {str(self.ImportId)} completely")
        if api_changes == 1:
            return 0        # return code 0 is ok
        else:
            return 1
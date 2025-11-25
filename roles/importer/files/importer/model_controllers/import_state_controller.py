import time
from datetime import datetime, timezone
from typing import Any
from dateutil import parser

import urllib3
import traceback

import fwo_globals

from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_log import FWOLogger
from fwo_config import read_config
from fwo_const import FWO_CONFIG_FILENAME, GRAPHQL_QUERY_PATH
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

    def __init__(self, configChangedSinceLastImport: bool, fwoConfig: FworchConfigController, mgmDetails: dict[str, Any], jwt: str, force: bool, 
                 version: int, isFullImport: bool = False, isInitialImport: bool = False, isClearingImport: bool = False, verifyCerts: bool = False, LastSuccessfulImport: str | None = None):
        self.Stats = ImportStatisticsController()
        self.StartTime = int(time.time())
        self.VerifyCerts = verifyCerts
        self.ConfigChangedSinceLastImport = configChangedSinceLastImport
        self.FwoConfig = fwoConfig
        self.MgmDetails = ManagementController.from_json(mgmDetails)
        self.ImportId = -1
        self.Jwt = jwt
        self.import_file_name = ""
        self.ForceImport = force
        self.ImportVersion = int(version)
        self.IsFullImport = isFullImport
        self.IsInitialImport = isInitialImport
        self.IsClearingImport = isClearingImport
        self.RulbaseToGatewayMap: dict[int, list[int]] = {}
        self.LastSuccessfulImport = LastSuccessfulImport
        self.api_connection = FwoApi(fwoConfig.FwoApiUri, jwt)
        self.api_call = FwoApiCall(self.api_connection)
        self.removed_rules_map: dict[str, int] = {}

    def __str__(self):
        return f"{str(self.MgmDetails)}(import_id={self.ImportId})"
    
    def setImportFileName(self, importFileName: str):
        self.import_file_name = importFileName

    def setImportId(self, importId: int):
        self.ImportId = importId

    def increaseErrorCounter(self, errorNo: int):
        self.Stats.ErrorCount = self.Stats.ErrorCount + errorNo

    def increaseErrorCounterByOne(self):
        self.increaseErrorCounter(1)

    def appendErrorString(self, errorStr: str):
        self.Stats.ErrorDetails.append(errorStr)

    def getErrors(self):
        return self.Stats.ErrorDetails

    def get_error_string(self):
        return str(self.Stats.ErrorDetails)

    def addError(self, error: str, log: bool = False):
        self.increaseErrorCounterByOne()
        self.appendErrorString(str(error))
        if log and not self.Stats.ErrorAlreadyLogged:
            FWOLogger.error(str(error))
            # self.Stats.ErrorAlreadyLogged = True


    @classmethod
    def initializeImport(cls, mgmId: int, jwt: str,
                         suppressCertWarnings: bool, 
                         sslVerification: bool, force: bool, version: int,
                         isClearingImport: bool, isFullImport: bool,
                         ):

        fwoConfig = FworchConfigController.fromJson(read_config(FWO_CONFIG_FILENAME))

        api_conn = FwoApi(ApiUri=fwoConfig.FwoApiUri, Jwt=jwt)
        api_call = FwoApiCall(api_conn)
        # set global https connection values
        fwo_globals.set_global_values(suppress_cert_warnings_in=suppressCertWarnings, verify_certs_in=sslVerification)
        if fwo_globals.suppress_cert_warnings:
            urllib3.disable_warnings()  # suppress ssl warnings only    

        try: # get mgm_details (fw-type, port, ip, user credentials):
            mgm_controller = ManagementController(
                mgmId, '', [], DeviceInfo(), ConnectionInfo(), '', CredentialInfo(), ManagerInfo(), DomainInfo()
            )
            mgmDetails = mgm_controller.get_mgm_details(api_conn, mgmId) 
        except Exception as _:
            FWOLogger.error(f"import_management - error while getting fw management details for mgm={mgmId}: {str(traceback.format_exc())}")
            raise

        try: # get last import data
            _, last_import_date = api_call.get_last_complete_import({"mgmId": mgmId})
        except Exception:
            FWOLogger.error(f"import_management - error while getting last import data for mgm={mgmId}")
            raise

        result = cls(
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

        if type(result) is str: # type: ignore # TODO: This should never happen
            FWOLogger.error("error while getting import state")
            raise FwoImporterError("error while getting import state")
        
        return result 


    def getPastImportInfos(self):        
        api_call = FwoApiCall(FwoApi(ApiUri=self.FwoConfig.FwoApiUri, Jwt=self.Jwt))
        try: # get past import details (LastFullImport, ...):
            day_string = api_call.get_config_value(key='dataRetentionTime')
            if day_string:
                self.DataRetentionDays = int(day_string)
            self.LastFullImportId, self.lastFullImportDate = \
                api_call.get_last_complete_import({"mgmId": int(self.MgmDetails.Id)}) 
        except Exception:
            FWOLogger.error(f"import_management - error while getting past import details for mgm={str(self.MgmDetails.Id)}: {str(traceback.format_exc())}")
            raise

        if self.lastFullImportDate != "":
            self.LastSuccessfulImport = self.lastFullImportDate

            # Convert the string to a datetime object
            pastDate = parser.parse(self.lastFullImportDate)

            # Ensure "now" is timezone-aware (UTC here)
            now = datetime.now(timezone.utc)

            # Normalize pastDate too (convert to UTC if it had a tz)
            if pastDate.tzinfo is None:
                pastDate = pastDate.replace(tzinfo=timezone.utc)
            else:
                pastDate = pastDate.astimezone(timezone.utc)

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

    def SetActionMap(self, api_call: FwoApiCall):
        query = "query getActionMap { stm_action { action_name action_id allowed } }"
        try:
            result = api_call.call(query=query, query_variables={})
        except Exception:
            FWOLogger.error('Error while getting stm_action')
            raise
        
        map: dict[str, int] = {}
        for action in result['data']['stm_action']:
            map.update({action['action_name']: action['action_id']})
        self.Actions = map

    def SetTrackMap(self, api_call: FwoApiCall):
        query = "query getTrackMap { stm_track { track_name track_id } }"
        try:
            result = api_call.call(query=query, query_variables={})
        except Exception:
            FWOLogger.error('Error while getting stm_track')
            raise

        track_map: dict[str, int] = {}
        for track in result['data']['stm_track']:
            track_map.update({track['track_name']: track['track_id']})
        self.Tracks = track_map

    def SetLinkTypeMap(self, api_call: FwoApiCall):
        query = "query getLinkType { stm_link_type { id name } }"
        try:
            result = api_call.call(query=query, query_variables={})
        except Exception:
            FWOLogger.error("Error while getting stm_link_type")
            raise
        
        link_map: dict[str, int] = {}
        for track in result['data']['stm_link_type']:
            link_map.update({track['name']: track['id']})
        self.LinkTypes = link_map

    def SetColorRefMap(self, api_call: FwoApiCall):
        get_colors_query = FwoApi.get_graphql_code([GRAPHQL_QUERY_PATH + "stmTables/getColors.graphql"])

        try:
            result = api_call.call(query=get_colors_query, query_variables={})
        except Exception:
            FWOLogger.error('Error while getting stm_color')
            raise
        
        color_map: dict[str, int] = {}
        for color in result['data']['stm_color']:
            color_map.update({color['color_name']: color['color_id']})
        self.ColorMap = color_map


    # limited to the current mgm_id
    # creates a dict with key = rulebase.uid and value = rulebase.id
    # TODO: map update inconsistencies: import_state is global over all sub managers, so map needs to be updated for each sub manager
    #   currently, this is done in fwconfig_import_rule. But what about other maps? - see #3646
    # TODO: global rulebases not yet included
    def SetRulebaseMap(self, api_call: FwoApiCall) -> None:
        # TODO: maps need to be updated directly after data changes
        query = """query getRulebaseMap($mgmId: Int) { rulebase(where:{mgm_id: {_eq: $mgmId}, removed:{_is_null:true }}) { id uid } }"""
        try:
            result = api_call.call(query=query, query_variables= {"mgmId": self.MgmDetails.CurrentMgmId})
        except Exception:
            FWOLogger.error("Error while getting rulebases")
            self.RulebaseMap = {}
            raise
        
        m: dict[str, int] = {}
        for rulebase in result['data']['rulebase']:
            rbid = rulebase['id']
            m.update({rulebase['uid']: rbid})
        self.RulebaseMap = m

        FWOLogger.debug(f"updated rulebase map for mgm_id {self.MgmDetails.CurrentMgmId} with {len(self.RulebaseMap)} entries")

    # limited to the current mgm_id
    # creats a dict with key = rule.uid and value = rule.id 
    # should be called sparsely, as there might be a lot of rules for a mgmt
    def SetRuleMap(self, api_call: FwoApi) -> None:
        query = """query getRuleMap($mgmId: Int) { rule(where:{mgm_id: {_eq: $mgmId}, removed:{_is_null:true }}) { rule_id rule_uid } }"""
        try:
            result = api_call.call(query=query, query_variables= {"mgmId": self.MgmDetails.Id})
        except Exception:
            FWOLogger.error("Error while getting rules")
            self.RuleMap = {}
            raise
        
        m: dict[str, int] = {}
        for rule in result['data']['rule']:
            m.update({rule['rule_uid']: rule['rule_id']})
        self.RuleMap = m

    # getting all gateways (not limitited to the current mgm_id) to support super managements
    # creates a dict with key = gateway.uid  and value = gateway.id
    # and also            key = gateway.name and value = gateway.id
    def SetGatewayMap(self, api_call: FwoApiCall):
        query = """
            query getGatewayMap {
                device {
                    mgm_id
                    dev_id
                    dev_uid
                }
            }
    """
        try:
            result = api_call.call(query=query, query_variables={})
        except Exception:
            FWOLogger.error("Error while getting gateways")
            self.GatewayMap = {}
            raise
        
        m = {}
        for gw in result['data']['device']:
            if gw['mgm_id'] not in m:
                m[gw['mgm_id']] = {}
            m[gw['mgm_id']][gw['dev_uid']] = gw['dev_id']
        self.GatewayMap = m

    # getting all managements (not limitited to the current mgm_id) to support super managements
    # creates a dict with key = management.uid  and value = management.id
    def SetManagementMap(self, api_call: FwoApiCall):
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
            FWOLogger.error("Error while getting managements")
            self.ManagementMap: dict[str, int] = {}
            raise
        
        m: dict[str, int] = {}
        mgm = result['data']['management'][0]
        m.update({mgm['mgm_uid']: mgm['mgm_id']})
        for sub_mgr in mgm['sub_managers']:
            m.update({sub_mgr['mgm_uid']: sub_mgr['mgm_id']})

        self.ManagementMap = m

    def lookupRule(self, ruleUid: str) -> int | None:
        return self.RuleMap.get(ruleUid, None)

    def lookupAction(self, actionStr: str) -> int:
        action_id = self.Actions.get(actionStr.lower(), None)
        if action_id is None:
            FWOLogger.error(f"Action {actionStr} not found")
            raise FwoImporterError(f"Action {actionStr} not found")
        return action_id

    def lookupTrack(self, trackStr: str) -> int:
        track_id = self.Tracks.get(trackStr.lower(), None)
        if track_id is None:
            FWOLogger.error(f"Track {trackStr} not found")
            raise FwoImporterError(f"Track {trackStr} not found")
        return track_id

    def lookupRulebaseId(self, rulebaseUid: str) -> int:
        rulebaseId = self.RulebaseMap.get(rulebaseUid, None)
        if rulebaseId is None:
            FWOLogger.error(f"Rulebase {rulebaseUid} not found in {len(self.RulebaseMap)} known rulebases")
            raise FwoImporterError(f"Rulebase {rulebaseUid} not found in {len(self.RulebaseMap)} known rulebases")
        return rulebaseId

    def lookupLinkType(self, linkUid: str) -> int:
        return self.LinkTypes.get(linkUid, -1)

    def lookupGatewayId(self, gwUid: str) -> int | None:
        mgm_id = self.MgmDetails.CurrentMgmId
        gws_for_mgm = self.GatewayMap.get(mgm_id, {})
        gw_id = gws_for_mgm.get(gwUid, None)
        if gw_id is None:
            FWOLogger.error(f"fwo_api:import_latest_config - no gateway id found for current mgm id '{mgm_id}' and gateway uid '{gwUid}' in {len(gws_for_mgm)} known gateways for this mgm")
            raise FwoImporterError(f"fwo_api:import_latest_config - no gateway id found for current mgm id '{mgm_id}' and gateway uid '{gwUid}' in {len(gws_for_mgm)} known gateways for this mgm")
        return gw_id
    
    def lookup_all_gateway_ids(self) -> list[int]:
        mgm_id = self.MgmDetails.CurrentMgmId
        gws_for_mgm = self.GatewayMap.get(mgm_id, {})
        gw_ids = list(gws_for_mgm.values())
        return gw_ids

    def lookupManagementId(self, mgmUid: str) -> int | None:
        if not self.ManagementMap.get(mgmUid, None):
            FWOLogger.error(f"fwo_api:import_latest_config - no mgm id found for current manager uid '{mgmUid}'")
        return self.ManagementMap.get(mgmUid, None)


    def lookupColorId(self, color_str: str) -> int:
        return self.ColorMap.get(color_str, 1)  # 1 = forground color black
    

    def delete_import(self):
        delete_import_mutation = """
            mutation deleteImport($importId: bigint!) {
                delete_import_control(where: {control_id: {_eq: $importId}}) { affected_rows }
            }"""

        try:
            result = self.api_connection.call(delete_import_mutation, query_variables={"importId": self.ImportId})
            api_changes = result['data']['delete_import_control']['affected_rows']
        except Exception:
            FWOLogger.exception(
                "fwo_api: failed to unlock import for import id " + str(self.ImportId))
            return 1  # signaling an error
        FWOLogger.info(f"removed import with id {str(self.ImportId)} completely")
        if api_changes == 1:
            return 0        # return code 0 is ok
        else:
            return 1
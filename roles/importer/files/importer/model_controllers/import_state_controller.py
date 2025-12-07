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

    def __init__(self, config_changed_since_last_import: bool, fwo_config: FworchConfigController, mgm_details: dict[str, Any], jwt: str, force: bool, 
                 version: int, is_full_import: bool = False, is_initial_import: bool = False, is_clearing_import: bool = False, verify_certs: bool = False, last_successful_import: str | None = None):
        self.stats = ImportStatisticsController()
        self.start_time = int(time.time())
        self.verify_certs = verify_certs
        self.config_changed_since_last_import = config_changed_since_last_import
        self.fwo_config = fwo_config
        self.mgm_details = ManagementController.from_json(mgm_details)
        self.import_id = -1
        self.Jwt = jwt
        self.import_file_name = ""
        self.force_import = force
        self.import_version = int(version)
        self.is_full_import = is_full_import
        self.is_initial_import = is_initial_import
        self.IsClearingImport = is_clearing_import
        self.RulbaseToGatewayMap: dict[int, list[int]] = {}
        self.last_successful_import = last_successful_import
        self.api_connection = FwoApi(fwo_config.fwo_api_url, jwt)
        self.api_call = FwoApiCall(self.api_connection)
        self.removed_rules_map: dict[str, int] = {}

    def __str__(self):
        return f"{str(self.mgm_details)}(import_id={self.import_id})"
    
    def set_import_file_name(self, import_file_name: str):
        self.import_file_name = import_file_name

    def set_import_id(self, import_id: int):
        self.import_id = import_id

    @classmethod
    def initialize_import(cls, mgm_id: int, jwt: str,
                         suppress_cert_warnings: bool, 
                         ssl_verification: bool, force: bool, version: int,
                         is_clearing_import: bool, is_full_import: bool,
                         ):

        fwo_config = FworchConfigController.from_json(read_config(FWO_CONFIG_FILENAME))

        api_conn = FwoApi(api_uri=fwo_config.fwo_api_url, jwt=jwt)
        api_call = FwoApiCall(api_conn)
        # set global https connection values
        fwo_globals.set_global_values(suppress_cert_warnings_in=suppress_cert_warnings, verify_certs_in=ssl_verification)
        if fwo_globals.suppress_cert_warnings:
            urllib3.disable_warnings()  # suppress ssl warnings only    

        try: # get mgm_details (fw-type, port, ip, user credentials):
            mgm_controller = ManagementController(
                mgm_id, '', [], DeviceInfo(), ConnectionInfo(), '', CredentialInfo(), ManagerInfo(), DomainInfo()
            )
            mgm_details = mgm_controller.get_mgm_details(api_conn, mgm_id) 
        except Exception as _:
            FWOLogger.error(f"import_management - error while getting fw management details for mgm={mgm_id}: {str(traceback.format_exc())}")
            raise

        try: # get last import data
            _, last_import_date = api_call.get_last_complete_import({"mgmId": mgm_id})
        except Exception:
            FWOLogger.error(f"import_management - error while getting last import data for mgm={mgm_id}")
            raise

        result = cls(
            config_changed_since_last_import = True,
            fwo_config = fwo_config,
            mgm_details = mgm_details,
            jwt = jwt,
            force = force,
            version = version,
            is_clearing_import=is_clearing_import,
            is_full_import=is_full_import,
            is_initial_import=(last_import_date == ""),
            verify_certs=ssl_verification,
            last_successful_import=last_import_date,
        )

        result.getPastImportInfos()
        result.set_core_data()

        if type(result) is str: # type: ignore # TODO: This should never happen
            FWOLogger.error("error while getting import state")
            raise FwoImporterError("error while getting import state")
        
        return result 


    def getPastImportInfos(self):        
        api_call = FwoApiCall(FwoApi(api_uri=self.fwo_config.fwo_api_url, jwt=self.Jwt))
        try: # get past import details (LastFullImport, ...):
            day_string = api_call.get_config_value(key='dataRetentionTime')
            if day_string:
                self.data_retention_days = int(day_string)
            self.last_full_import_id, self.last_full_import_date = \
                api_call.get_last_complete_import({"mgmId": int(self.mgm_details.mgm_id)}) 
        except Exception:
            FWOLogger.error(f"import_management - error while getting past import details for mgm={str(self.mgm_details.mgm_id)}: {str(traceback.format_exc())}")
            raise

        if self.last_full_import_date != "":
            self.last_successful_import = self.last_full_import_date

            # Convert the string to a datetime object
            past_date = parser.parse(self.last_full_import_date)

            # Ensure "now" is timezone-aware (UTC here)
            now = datetime.now(timezone.utc)

            # Normalize pastDate too (convert to UTC if it had a tz)
            if past_date.tzinfo is None:
                past_date = past_date.replace(tzinfo=timezone.utc)
            else:
                past_date = past_date.astimezone(timezone.utc)

            difference = now - past_date

            self.days_since_last_full_import = difference.days
        else:
            self.days_since_last_full_import = 0


    def set_core_data(self):
        api_call = FwoApiCall(FwoApi(api_uri=self.fwo_config.fwo_api_url, jwt=self.Jwt))
        self.set_track_map(api_call)
        self.set_action_map(api_call)
        self.set_link_type_map(api_call)
        self.set_gateway_map(api_call)
        self.set_management_map(api_call)
        self.set_color_ref_map(api_call)

        # the following maps will be empty when starting first import of a management
        self.set_rulebase_map(api_call)
        self.set_rule_map(api_call)

    def set_action_map(self, api_call: FwoApiCall):
        query = "query getActionMap { stm_action { action_name action_id allowed } }"
        try:
            result = api_call.call(query=query, query_variables={})
        except Exception:
            FWOLogger.error('Error while getting stm_action')
            raise
        
        action_map: dict[str, int] = {}
        for action in result['data']['stm_action']:
            action_map.update({action['action_name']: action['action_id']})
        self.actions = action_map

    def set_track_map(self, api_call: FwoApiCall):
        query = "query getTrackMap { stm_track { track_name track_id } }"
        try:
            result = api_call.call(query=query, query_variables={})
        except Exception:
            FWOLogger.error('Error while getting stm_track')
            raise

        track_map: dict[str, int] = {}
        for track in result['data']['stm_track']:
            track_map.update({track['track_name']: track['track_id']})
        self.tracks = track_map

    def set_link_type_map(self, api_call: FwoApiCall):
        query = "query getLinkType { stm_link_type { id name } }"
        try:
            result = api_call.call(query=query, query_variables={})
        except Exception:
            FWOLogger.error("Error while getting stm_link_type")
            raise
        
        link_map: dict[str, int] = {}
        for track in result['data']['stm_link_type']:
            link_map.update({track['name']: track['id']})
        self.link_types = link_map

    def set_color_ref_map(self, api_call: FwoApiCall):
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
    def set_rulebase_map(self, api_call: FwoApiCall) -> None:
        # TODO: maps need to be updated directly after data changes
        query = """query getRulebaseMap($mgmId: Int) { rulebase(where:{mgm_id: {_eq: $mgmId}, removed:{_is_null:true }}) { id uid } }"""
        try:
            result = api_call.call(query=query, query_variables= {"mgmId": self.mgm_details.current_mgm_id})
        except Exception:
            FWOLogger.error("Error while getting rulebases")
            self.rulebase_map = {}
            raise
        
        m: dict[str, int] = {}
        for rulebase in result['data']['rulebase']:
            rbid = rulebase['id']
            m.update({rulebase['uid']: rbid})
        self.rulebase_map = m

        FWOLogger.debug(f"updated rulebase map for mgm_id {self.mgm_details.current_mgm_id} with {len(self.rulebase_map)} entries")

    # limited to the current mgm_id
    # creats a dict with key = rule.uid and value = rule.id 
    # should be called sparsely, as there might be a lot of rules for a mgmt
    def set_rule_map(self, api_call: FwoApi) -> None:
        query = """query getRuleMap($mgmId: Int) { rule(where:{mgm_id: {_eq: $mgmId}, removed:{_is_null:true }}) { rule_id rule_uid } }"""
        try:
            result = api_call.call(query=query, query_variables= {"mgmId": self.mgm_details.mgm_id})
        except Exception:
            FWOLogger.error("Error while getting rules")
            self.rule_map = {}
            raise
        
        m: dict[str, int] = {}
        for rule in result['data']['rule']:
            m.update({rule['rule_uid']: rule['rule_id']})
        self.rule_map = m

    # getting all gateways (not limitited to the current mgm_id) to support super managements
    # creates a dict with key = gateway.uid  and value = gateway.id
    # and also            key = gateway.name and value = gateway.id
    def set_gateway_map(self, api_call: FwoApiCall):
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
            self.gateway_map = {}
            raise
        
        m = {}
        for gw in result['data']['device']:
            if gw['mgm_id'] not in m:
                m[gw['mgm_id']] = {}
            m[gw['mgm_id']][gw['dev_uid']] = gw['dev_id']
        self.gateway_map = m

    # getting all managements (not limitited to the current mgm_id) to support super managements
    # creates a dict with key = management.uid  and value = management.id
    def set_management_map(self, api_call: FwoApiCall):
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
            result = api_call.call(query=query, query_variables= {"mgmId": self.mgm_details.mgm_id})
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

    def lookup_rule(self, rule_uid: str) -> int | None:
        return self.rule_map.get(rule_uid, None)

    def lookup_action(self, action_str: str) -> int:
        action_id = self.actions.get(action_str.lower(), None)
        if action_id is None:
            FWOLogger.error(f"Action {action_str} not found")
            raise FwoImporterError(f"Action {action_str} not found")
        return action_id

    def lookup_track(self, track_str: str) -> int:
        track_id = self.tracks.get(track_str.lower(), None)
        if track_id is None:
            FWOLogger.error(f"Track {track_str} not found")
            raise FwoImporterError(f"Track {track_str} not found")
        return track_id

    def lookup_rulebase_id(self, rulebase_uid: str) -> int:
        rulebase_id = self.rulebase_map.get(rulebase_uid, None)
        if rulebase_id is None:
            FWOLogger.error(f"Rulebase {rulebase_uid} not found in {len(self.rulebase_map)} known rulebases")
            raise FwoImporterError(f"Rulebase {rulebase_uid} not found in {len(self.rulebase_map)} known rulebases")
        return rulebase_id

    def lookup_link_type(self, link_uid: str) -> int:
        return self.link_types.get(link_uid, -1)

    def lookup_gateway_id(self, gw_uid: str) -> int | None:
        mgm_id = self.mgm_details.current_mgm_id
        gws_for_mgm = self.gateway_map.get(mgm_id, {})
        gw_id = gws_for_mgm.get(gw_uid, None)
        if gw_id is None:
            FWOLogger.error(f"fwo_api:import_latest_config - no gateway id found for current mgm id '{mgm_id}' and gateway uid '{gw_uid}' in {len(gws_for_mgm)} known gateways for this mgm")
            raise FwoImporterError(f"fwo_api:import_latest_config - no gateway id found for current mgm id '{mgm_id}' and gateway uid '{gw_uid}' in {len(gws_for_mgm)} known gateways for this mgm")
        return gw_id
    
    def lookup_all_gateway_ids(self) -> list[int]:
        mgm_id = self.mgm_details.current_mgm_id
        gws_for_mgm = self.gateway_map.get(mgm_id, {})
        gw_ids = list(gws_for_mgm.values())
        return gw_ids

    def lookup_management_id(self, mgm_uid: str) -> int | None:
        if not self.ManagementMap.get(mgm_uid, None):
            FWOLogger.error(f"fwo_api:import_latest_config - no mgm id found for current manager uid '{mgm_uid}'")
        return self.ManagementMap.get(mgm_uid, None)


    def lookupColorId(self, color_str: str) -> int:
        return self.ColorMap.get(color_str, 1)  # 1 = forground color black
    

    def delete_import(self):
        delete_import_mutation = """
            mutation deleteImport($importId: bigint!) {
                delete_import_control(where: {control_id: {_eq: $importId}}) { affected_rows }
            }"""

        try:
            result = self.api_connection.call(delete_import_mutation, query_variables={"importId": self.import_id})
            api_changes = result['data']['delete_import_control']['affected_rows']
        except Exception:
            FWOLogger.exception(
                "fwo_api: failed to unlock import for import id " + str(self.import_id))
            return 1  # signaling an error
        FWOLogger.info(f"removed import with id {str(self.import_id)} completely")
        if api_changes == 1:
            return 0        # return code 0 is ok
        else:
            return 1
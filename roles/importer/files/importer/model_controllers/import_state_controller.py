import traceback
from datetime import UTC, datetime

import fwo_globals
import urllib3
from dateutil import parser
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_config import read_config
from fwo_const import FWO_CONFIG_FILENAME, GRAPHQL_QUERY_PATH
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger
from model_controllers.fworch_config_controller import FworchConfigController
from model_controllers.management_controller import (
    ConnectionInfo,
    CredentialInfo,
    DeviceInfo,
    DomainInfo,
    ManagementController,
    ManagerInfo,
)
from models.import_state import ImportState

"""Used for storing state during import process per management"""


class ImportStateController:
    state: ImportState
    api_connection: FwoApi
    api_call: FwoApiCall

    def __init__(self, state: ImportState, fwo_api: FwoApi):
        self.state = state
        self.api_connection = fwo_api
        self.api_call = FwoApiCall(self.api_connection)

    def __str__(self):
        return f"{self.state.mgm_details!s}(import_id={self.state.import_id})"

    def set_import_file_name(self, import_file_name: str):
        self.state.import_file_name = import_file_name

    def set_import_id(self, import_id: int):
        self.state.import_id = import_id

    @classmethod
    def initialize_import(
        cls,
        mgm_id: int,
        jwt: str,
        suppress_cert_warnings: bool,
        ssl_verification: bool,
        force: bool,
        version: int,
        is_clearing_import: bool,
        is_full_import: bool,
    ):
        fwo_config = FworchConfigController.from_json(read_config(FWO_CONFIG_FILENAME))

        api_conn = FwoApi(api_uri=fwo_config.fwo_api_url, jwt=jwt)
        api_call = FwoApiCall(api_conn)
        # set global https connection values
        fwo_globals.set_global_values(
            suppress_cert_warnings_in=suppress_cert_warnings, verify_certs_in=ssl_verification
        )
        if fwo_globals.suppress_cert_warnings:
            urllib3.disable_warnings()  # suppress ssl warnings only

        try:  # get mgm_details (fw-type, port, ip, user credentials):
            mgm_controller = ManagementController(
                mgm_id, "", [], DeviceInfo(), ConnectionInfo(), "", CredentialInfo(), ManagerInfo(), DomainInfo()
            )
            mgm_details = mgm_controller.get_mgm_details(api_conn, mgm_id)
        except Exception as _:
            FWOLogger.error(
                f"import_management - error while getting fw management details for mgm={mgm_id}: {traceback.format_exc()!s}"
            )
            raise

        try:  # get last import data
            _, last_import_date = api_call.get_last_complete_import({"mgmId": mgm_id})
        except Exception:
            FWOLogger.error(f"import_management - error while getting last import data for mgm={mgm_id}")
            raise

        state = ImportState()
        state.config_changed_since_last_import = True
        state.fwo_config = fwo_config
        state.mgm_details = ManagementController.from_json(mgm_details)
        state.force_import = force
        state.import_version = version
        state.is_clearing_import = is_clearing_import
        state.is_full_import = is_full_import
        state.is_initial_import = last_import_date == ""
        state.verify_certs = ssl_verification
        state.last_successful_import = last_import_date

        fwo_api = FwoApi(fwo_config.fwo_api_url, jwt)

        result = cls(state, fwo_api)
        result.get_past_import_infos()
        result.set_core_data()

        if type(result) is str:  # type: ignore # TODO: This should never happen  # noqa: PGH003
            FWOLogger.error("error while getting import state")
            raise FwoImporterError("error while getting import state")

        return result

    def get_past_import_infos(self):
        try:  # get past import details (LastFullImport, ...):
            day_string = self.api_call.get_config_value(key="dataRetentionTime")
            if day_string:
                self.state.data_retention_days = int(day_string)
            self.state.last_full_import_id, self.state.last_full_import_date = self.api_call.get_last_complete_import(
                {"mgmId": int(self.state.mgm_details.mgm_id)}
            )
        except Exception:
            FWOLogger.error(
                f"import_management - error while getting past import details for mgm={self.state.mgm_details.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise

        if self.state.last_full_import_date != "":
            self.state.last_successful_import = self.state.last_full_import_date

            # Convert the string to a datetime object
            past_date = parser.parse(self.state.last_full_import_date)

            # Ensure "now" is timezone-aware (UTC here)
            now = datetime.now(UTC)

            # Normalize pastDate too (convert to UTC if it had a tz)
            past_date = past_date.replace(tzinfo=UTC) if past_date.tzinfo is None else past_date.astimezone(UTC)

            difference = now - past_date

            self.state.days_since_last_full_import = difference.days
        else:
            self.state.days_since_last_full_import = 0

    def set_core_data(self):
        self.set_track_map()
        self.set_action_map()
        self.set_link_type_map()
        self.set_gateway_map()
        self.set_management_map()
        self.set_color_ref_map()

        # the following maps will be empty when starting first import of a management
        self.set_rulebase_map()
        self.set_rule_map()

    def set_action_map(self):
        query = "query getActionMap { stm_action { action_name action_id allowed } }"
        try:
            result = self.api_call.call(query=query, query_variables={})
        except Exception:
            FWOLogger.error("Error while getting stm_action")
            raise

        action_map: dict[str, int] = {}
        for action in result["data"]["stm_action"]:
            action_map.update({action["action_name"]: action["action_id"]})
        self.state.actions = action_map

    def set_track_map(self):
        query = "query getTrackMap { stm_track { track_name track_id } }"
        try:
            result = self.api_call.call(query=query, query_variables={})
        except Exception:
            FWOLogger.error("Error while getting stm_track")
            raise

        track_map: dict[str, int] = {}
        for track in result["data"]["stm_track"]:
            track_map.update({track["track_name"]: track["track_id"]})
        self.state.tracks = track_map

    def set_link_type_map(self):
        query = "query getLinkType { stm_link_type { id name } }"
        try:
            result = self.api_call.call(query=query, query_variables={})
        except Exception:
            FWOLogger.error("Error while getting stm_link_type")
            raise

        link_map: dict[str, int] = {}
        for track in result["data"]["stm_link_type"]:
            link_map.update({track["name"]: track["id"]})
        self.state.link_types = link_map

    def set_color_ref_map(self):
        get_colors_query = FwoApi.get_graphql_code([GRAPHQL_QUERY_PATH + "stmTables/getColors.graphql"])

        try:
            result = self.api_call.call(query=get_colors_query, query_variables={})
        except Exception:
            FWOLogger.error("Error while getting stm_color")
            raise

        color_map: dict[str, int] = {}
        for color in result["data"]["stm_color"]:
            color_map.update({color["color_name"]: color["color_id"]})
        self.state.color_map = color_map

    # limited to the current mgm_id
    # creates a dict with key = rulebase.uid and value = rulebase.id
    # TODO: map update inconsistencies: import_state is global over all sub managers, so map needs to be updated for each sub manager
    #   currently, this is done in fwconfig_import_rule. But what about other maps? - see #3646
    # TODO: global rulebases not yet included
    def set_rulebase_map(self) -> None:
        # TODO: maps need to be updated directly after data changes
        query = """query getRulebaseMap($mgmId: Int) { rulebase(where:{mgm_id: {_eq: $mgmId}, removed:{_is_null:true }}) { id uid } }"""
        try:
            result = self.api_call.call(query=query, query_variables={"mgmId": self.state.mgm_details.current_mgm_id})
        except Exception:
            FWOLogger.error("Error while getting rulebases")
            self.state.rulebase_map = {}
            raise

        m: dict[str, int] = {}
        for rulebase in result["data"]["rulebase"]:
            rbid = rulebase["id"]
            m.update({rulebase["uid"]: rbid})
        self.state.rulebase_map = m

        FWOLogger.debug(
            f"updated rulebase map for mgm_id {self.state.mgm_details.current_mgm_id} with {len(self.state.rulebase_map)} entries"
        )

    # limited to the current mgm_id
    # creats a dict with key = rule.uid and value = rule.id
    # should be called sparsely, as there might be a lot of rules for a mgmt
    def set_rule_map(self) -> None:
        query = """query getRuleMap($mgmId: Int) { rule(where:{mgm_id: {_eq: $mgmId}, removed:{_is_null:true }}) { rule_id rule_uid } }"""
        try:
            result = self.api_call.call(query=query, query_variables={"mgmId": self.state.mgm_details.mgm_id})
        except Exception:
            FWOLogger.error("Error while getting rules")
            self.state.rule_map = {}
            raise

        m: dict[str, int] = {}
        for rule in result["data"]["rule"]:
            m.update({rule["rule_uid"]: rule["rule_id"]})
        self.state.rule_map = m

    # getting all gateways (not limitited to the current mgm_id) to support super managements
    # creates a dict with key = gateway.uid  and value = gateway.id
    # and also            key = gateway.name and value = gateway.id
    def set_gateway_map(self):
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
            result = self.api_call.call(query=query, query_variables={})
        except Exception:
            FWOLogger.error("Error while getting gateways")
            self.state.gateway_map = {}
            raise

        m = {}
        for gw in result["data"]["device"]:
            if gw["mgm_id"] not in m:
                m[gw["mgm_id"]] = {}
            m[gw["mgm_id"]][gw["dev_uid"]] = gw["dev_id"]
        self.state.gateway_map = m

    # getting all managements (not limitited to the current mgm_id) to support super managements
    # creates a dict with key = management.uid  and value = management.id
    def set_management_map(self):
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
            result = self.api_call.call(query=query, query_variables={"mgmId": self.state.mgm_details.mgm_id})
        except Exception:
            FWOLogger.error("Error while getting managements")
            self.state.management_map = {}
            raise

        m: dict[str, int] = {}
        mgm = result["data"]["management"][0]
        m.update({mgm["mgm_uid"]: mgm["mgm_id"]})
        for sub_mgr in mgm["sub_managers"]:
            m.update({sub_mgr["mgm_uid"]: sub_mgr["mgm_id"]})

        self.state.management_map = m

    def delete_import(self):
        delete_import_mutation = """
            mutation deleteImport($importId: bigint!) {
                delete_import_control(where: {control_id: {_eq: $importId}}) { affected_rows }
            }"""

        try:
            result = self.api_connection.call(
                delete_import_mutation, query_variables={"importId": self.state.import_id}
            )
            _ = result["data"]["delete_import_control"]["affected_rows"]
            FWOLogger.info(f"removed import with id {self.state.import_id!s} completely")
        except Exception:
            FWOLogger.exception("fwo_api: failed to unlock import for import id " + str(self.state.import_id))

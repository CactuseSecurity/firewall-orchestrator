import time
import traceback

from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger
from model_controllers.import_statistics_controller import ImportStatisticsController
from model_controllers.management_controller import ManagementController
from models.fwconfig_normalized import FwConfigNormalized
from states.management_state import ManagementState


class ImportState:
    import_id: int = -1
    mgm_id: int = -1

    fwo_api: FwoApi
    fwo_api_call: FwoApiCall

    super_config: FwConfigNormalized | None
    previous_global_config: FwConfigNormalized | None
    management_state: ManagementState
    mgm_map: dict[int, dict[str, int]]
    gateway_map: dict[int, dict[str, int]]
    rulebase_map: dict[str, int]

    mgm_details: ManagementController

    import_version: int
    data_retention_days: int
    days_since_last_full_import: int
    last_full_import_id: int
    last_full_import_date: str | None = None
    last_successful_import: str | None = None
    is_full_import: bool = False
    is_initial_import: bool = False
    responsible_for_importing: bool = True
    is_clearing_import: bool = False

    def __init__(self, fwo_api: FwoApi, fwo_api_call: FwoApiCall, mgm_id: int):
        self.fwo_api = fwo_api
        self.fwo_api_call = fwo_api_call
        self.mgm_id = mgm_id

        self.stats: ImportStatisticsController = ImportStatisticsController()
        self.start_time: int = int(time.time())
        self.actions: dict[str, int] = {}
        self.tracks: dict[str, int] = {}
        self.link_types: dict[str, int] = {}
        self.gateway_map: dict[int, dict[str, int]] = {}
        self.rulebase_map: dict[str, int] = {}
        self.management_map: dict[str, int] = {}
        self.color_map: dict[str, int] = {}
        self.network_obj_type_map: dict[str, int] = {}
        self.service_obj_type_map: dict[str, int] = {}
        self.user_obj_type_map: dict[str, int] = {}
        self.protocol_map: dict[str, int] = {}
        self.rulebase_to_gateway_map: dict[int, list[int]] = {}
        self.data_retention_days: int = 30

        try:  # get mgm_details (fw-type, port, ip, user credentials):
            mgm_details = ManagementController.get_mgm_details(fwo_api, mgm_id)
        except Exception as _:
            FWOLogger.error(
                f"import_management - error while getting fw management details for mgm={mgm_id}: {traceback.format_exc()!s}"
            )
            raise

        try:  # get last import data
            _, last_import_date = self.fwo_api_call.get_last_complete_import({"mgmId": mgm_id})
        except Exception:
            FWOLogger.error(f"import_management - error while getting last import data for mgm={mgm_id}")
            raise

        self.mgm_details = ManagementController.from_json(mgm_details)
        self.last_full_import_date = last_import_date
        self.is_initial_import = last_import_date == ""

        self.get_past_import_infos()
        self.set_core_data()

    def set_import_file_name(self, import_file_name: str):
        self.import_file_name = import_file_name

    def set_import_id(self, import_id: int):
        self.import_id = import_id

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

    def lookup_link_type(self, link_uid: str) -> int:
        link_type_id = self.link_types.get(link_uid, None)
        if not link_type_id:
            FWOLogger.error(f"Link type {link_uid} not found")
            raise FwoImporterError(f"Link type {link_uid} not found")
        return link_type_id

    def lookup_network_obj_type_id(self, obj_type_str: str) -> int:
        obj_type_id = self.network_obj_type_map.get(obj_type_str, None)
        if obj_type_id is None:
            FWOLogger.error(f"Network object type {obj_type_str} not found")
            raise FwoImporterError(f"Network object type {obj_type_str} not found")
        return obj_type_id

    def lookup_service_obj_type_id(self, svc_type_str: str) -> int:
        obj_type_id = self.service_obj_type_map.get(svc_type_str, None)
        if obj_type_id is None:
            FWOLogger.error(f"Service object type {svc_type_str} not found")
            raise FwoImporterError(f"Service object type {svc_type_str} not found")
        return obj_type_id

    def lookup_user_obj_type_id(self, usr_type_str: str) -> int:
        obj_type_id = self.user_obj_type_map.get(usr_type_str, None)
        if obj_type_id is None:
            FWOLogger.error(f"User object type {usr_type_str} not found")
            raise FwoImporterError(f"User object type {usr_type_str} not found")
        return obj_type_id

    def lookup_protocol_id(self, proto_str: str) -> int:
        proto_id = self.protocol_map.get(proto_str.lower(), None)
        if proto_id is None:
            FWOLogger.error(f"Protocol {proto_str} not found")
            raise FwoImporterError(f"Protocol {proto_str} not found")
        return proto_id

    def lookup_gateway_id(self, gw_uid: str, mgm_id: int) -> int:
        gws_for_mgm = self.gateway_map.get(mgm_id, {})
        gw_id = gws_for_mgm.get(gw_uid, None)
        if gw_id is None:
            FWOLogger.error(
                f"fwo_api:import_latest_config - no gateway id found for current mgm id '{mgm_id}' and gateway uid '{gw_uid}' in {len(gws_for_mgm)} known gateways for this mgm"
            )
            raise FwoImporterError(
                f"fwo_api:import_latest_config - no gateway id found for current mgm id '{mgm_id}' and gateway uid '{gw_uid}' in {len(gws_for_mgm)} known gateways for this mgm"
            )
        return gw_id

    def lookup_all_gateway_ids(self, mgm_id: int) -> list[int]:
        gws_for_mgm = self.gateway_map.get(mgm_id, {})
        return list(gws_for_mgm.values())

    def lookup_management_id(self, mgm_uid: str) -> int | None:
        if not self.management_map.get(mgm_uid, None):
            FWOLogger.error(f"fwo_api:import_latest_config - no mgm id found for current manager uid '{mgm_uid}'")
        return self.management_map.get(mgm_uid, None)

    def lookup_color_id_unresolved(self, color_str: str) -> int | None:
        return self.color_map.get(color_str, None)

    def lookup_color_id(self, color_str: str) -> int:
        return self.color_map.get(color_str, 1)  # 1 = forground color black

    def get_past_import_infos(self):
        try:  # get past import details (LastFullImport, ...):
            day_string = self.fwo_api_call.get_config_value(key="dataRetentionTime")
            if day_string:
                self.data_retention_days = int(day_string)
            self.last_full_import_id, self.last_full_import_date = self.fwo_api_call.get_last_complete_import(
                {"mgmId": int(self.mgm_details.mgm_id)}
            )
        except Exception:
            FWOLogger.error(
                f"import_management - error while getting past import details for mgm={self.state.mgm_details.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError(f"Error while getting past import details: {traceback.format_exc()!s}")

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
        self.set_color_ref_map()
        self.set_network_obj_type_map()
        self.set_service_obj_type_map()
        self.set_user_obj_type_map()
        self.set_protocol_map()
        self.set_gateway_map()
        self.set_management_map()

    def set_action_map(self):
        query = "query getActionMap { stm_action { action_name action_id allowed } }"
        try:
            result = self.api_call.call(query=query, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_action: {e!s}")
            raise FwoImporterError(f"Error while getting stm_action: {e!s}")

        action_map: dict[str, int] = {}
        for action in result["data"]["stm_action"]:
            action_map.update({action["action_name"]: action["action_id"]})
        self.state.actions = action_map

    def set_track_map(self):
        query = "query getTrackMap { stm_track { track_name track_id } }"
        try:
            result = self.api_call.call(query=query, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_track: {e!s}")
            raise FwoImporterError(f"Error while getting stm_track: {e!s}")

        track_map: dict[str, int] = {}
        for track in result["data"]["stm_track"]:
            track_map.update({track["track_name"]: track["track_id"]})
        self.state.tracks = track_map

    def set_link_type_map(self):
        query = "query getLinkType { stm_link_type { id name } }"
        try:
            result = self.api_call.call(query=query, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_link_type: {e!s}")
            raise FwoImporterError(f"Error while getting stm_link_type: {e!s}")

        link_map: dict[str, int] = {}
        for track in result["data"]["stm_link_type"]:
            link_map.update({track["name"]: track["id"]})
        self.state.link_types = link_map

    def set_color_ref_map(self):
        get_colors_query = FwoApi.get_graphql_code([GRAPHQL_QUERY_PATH + "stmTables/getColors.graphql"])

        try:
            result = self.api_call.call(query=get_colors_query, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_color: {e!s}")
            raise FwoImporterError(f"Error while getting stm_color: {e!s}")

        color_map: dict[str, int] = {}
        for color in result["data"]["stm_color"]:
            color_map.update({color["color_name"]: color["color_id"]})
        self.state.color_map = color_map

    def set_network_obj_type_map(self):
        query = "query getNetworkObjTypeMap { stm_obj_typ { obj_typ_name obj_typ_id } }"
        try:
            result = self.api_call.call(query=query, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_obj_typ: {e!s}")
            raise FwoImporterError(f"Error while getting stm_obj_typ: {e!s}")

        nwobj_type_map: dict[str, int] = {}
        for nw_type in result["data"]["stm_obj_typ"]:
            nwobj_type_map.update({nw_type["obj_typ_name"]: nw_type["obj_typ_id"]})
        self.state.network_obj_type_map = nwobj_type_map

    def set_service_obj_type_map(self):
        query = "query getServiceObjTypeMap { stm_svc_typ { svc_typ_name svc_typ_id } }"
        try:
            result = self.api_call.call(query=query, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_svc_typ: {e!s}")
            raise FwoImporterError(f"Error while getting stm_svc_typ: {e!s}")

        svc_type_map: dict[str, int] = {}
        for svc_type in result["data"]["stm_svc_typ"]:
            svc_type_map.update({svc_type["svc_typ_name"]: svc_type["svc_typ_id"]})
        self.state.service_obj_type_map = svc_type_map

    def set_user_obj_type_map(self):
        query = "query getUserObjTypeMap { stm_usr_typ { usr_typ_name usr_typ_id } }"
        try:
            result = self.api_call.call(query=query, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_usr_typ: {e!s}")
            raise FwoImporterError(f"Error while getting stm_usr_typ: {e!s}")

        user_type_map: dict[str, int] = {}
        for usr_type in result["data"]["stm_usr_typ"]:
            user_type_map.update({usr_type["usr_typ_name"]: usr_type["usr_typ_id"]})
        self.state.user_obj_type_map = user_type_map

    def set_protocol_map(self):
        query = "query getIpProtocols { stm_ip_proto { ip_proto_id ip_proto_name } }"
        try:
            result = self.api_call.call(query=query, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_ip_proto: {e!s}")
            raise FwoImporterError(f"Error while getting stm_ip_proto: {e!s}")

        protocol_map: dict[str, int] = {}
        for proto in result["data"]["stm_ip_proto"]:
            protocol_map.update({proto["ip_proto_name"].lower(): proto["ip_proto_id"]})
        self.state.protocol_map = protocol_map

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
            raise FwoImporterError("Error while getting gateways")

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
            raise FwoImporterError("Error while getting managements")

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
                delete_import_mutation,
                query_variables={"importId": self.state.import_id},
            )
            _ = result["data"]["delete_import_control"]["affected_rows"]
            FWOLogger.info(f"removed import with id {self.state.import_id!s} completely")
        except Exception:
            FWOLogger.exception("fwo_api: failed to unlock import for import id " + str(self.state.import_id))

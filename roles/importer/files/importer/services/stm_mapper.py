import networking.graphql.import_state_queries as queries
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger


class StmMapper:
    tracks: dict[str, int]
    actions: dict[str, int]
    link_types: dict[str, int]
    color_map: dict[str, int]
    network_obj_type_map: dict[str, int]
    service_obj_type_map: dict[str, int]
    user_obj_type_map: dict[str, int]
    gateway_map: dict[int, dict[str, int]]
    protocol_map: dict[str, int]

    def __init__(self, fwo_api_call: FwoApiCall):
        self.set_track_map(fwo_api_call)
        self.set_action_map(fwo_api_call)
        self.set_link_type_map(fwo_api_call)
        self.set_color_ref_map(fwo_api_call)
        self.set_network_obj_type_map(fwo_api_call)
        self.set_service_obj_type_map(fwo_api_call)
        self.set_user_obj_type_map(fwo_api_call)
        self.set_protocol_map(fwo_api_call)
        self.set_gateway_map(fwo_api_call)

    def set_action_map(self, fwo_api_call: FwoApiCall):
        try:
            result = fwo_api_call.call(query=queries.GET_ACTION_MAP, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_action: {e!s}")
            raise FwoImporterError(f"Error while getting stm_action: {e!s}")

        action_map: dict[str, int] = {}
        for action in result["data"]["stm_action"]:
            action_map.update({action["action_name"]: action["action_id"]})
        self.actions = action_map

    def lookup_action(self, action_str: str) -> int:
        action_id = self.actions.get(action_str.lower(), None)
        if action_id is None:
            FWOLogger.error(f"Action {action_str} not found")
            raise FwoImporterError(f"Action {action_str} not found")
        return action_id

    def set_track_map(self, fwo_api_call: FwoApiCall):
        try:
            result = fwo_api_call.call(query=queries.GET_TRACK_MAP, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_track: {e!s}")
            raise FwoImporterError(f"Error while getting stm_track: {e!s}")

        track_map: dict[str, int] = {}
        for track in result["data"]["stm_track"]:
            track_map.update({track["track_name"]: track["track_id"]})
        self.tracks = track_map

    def lookup_track(self, track_str: str) -> int:
        track_id = self.tracks.get(track_str.lower(), None)
        if track_id is None:
            FWOLogger.error(f"Track {track_str} not found")
            raise FwoImporterError(f"Track {track_str} not found")
        return track_id

    def set_link_type_map(self, fwo_api_call: FwoApiCall):
        try:
            result = fwo_api_call.call(query=queries.GET_LINK_TYPE_MAP, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_link_type: {e!s}")
            raise FwoImporterError(f"Error while getting stm_link_type: {e!s}")

        link_map: dict[str, int] = {}
        for track in result["data"]["stm_link_type"]:
            link_map.update({track["name"]: track["id"]})
        self.link_types = link_map

    def lookup_link_type(self, link_uid: str) -> int:
        link_type_id = self.link_types.get(link_uid, None)
        if not link_type_id:
            FWOLogger.error(f"Link type {link_uid} not found")
            raise FwoImporterError(f"Link type {link_uid} not found")
        return link_type_id

    def set_color_ref_map(self, fwo_api_call: FwoApiCall):
        try:
            result = fwo_api_call.call(query=queries.GET_COLOR_MAP, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_color: {e!s}")
            raise FwoImporterError(f"Error while getting stm_color: {e!s}")

        color_map: dict[str, int] = {}
        for color in result["data"]["stm_color"]:
            color_map.update({color["color_name"]: color["color_id"]})
        self.color_map = color_map

    def lookup_color_id_unresolved(self, color_str: str) -> int | None:
        return self.color_map.get(color_str, None)

    def lookup_color_id(self, color_str: str) -> int:
        return self.color_map.get(color_str, 1)  # 1 = forground color black

    def set_network_obj_type_map(self, fwo_api_call: FwoApiCall):
        try:
            result = fwo_api_call.call(query=queries.GET_NETWORK_OBJ_TYPE_MAP, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_obj_typ: {e!s}")
            raise FwoImporterError(f"Error while getting stm_obj_typ: {e!s}")

        nwobj_type_map: dict[str, int] = {}
        for nw_type in result["data"]["stm_obj_typ"]:
            nwobj_type_map.update({nw_type["obj_typ_name"]: nw_type["obj_typ_id"]})
        self.network_obj_type_map = nwobj_type_map

    def lookup_network_obj_type_id(self, obj_type_str: str) -> int:
        obj_type_id = self.network_obj_type_map.get(obj_type_str, None)
        if obj_type_id is None:
            FWOLogger.error(f"Network object type {obj_type_str} not found")
            raise FwoImporterError(f"Network object type {obj_type_str} not found")
        return obj_type_id

    def set_service_obj_type_map(self, fwo_api_call: FwoApiCall):
        try:
            result = fwo_api_call.call(query=queries.GET_SERVICE_OBJ_TYPE_MAP, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_svc_typ: {e!s}")
            raise FwoImporterError(f"Error while getting stm_svc_typ: {e!s}")

        svc_type_map: dict[str, int] = {}
        for svc_type in result["data"]["stm_svc_typ"]:
            svc_type_map.update({svc_type["svc_typ_name"]: svc_type["svc_typ_id"]})
        self.service_obj_type_map = svc_type_map

    def lookup_service_obj_type_id(self, svc_type_str: str) -> int:
        obj_type_id = self.service_obj_type_map.get(svc_type_str, None)
        if obj_type_id is None:
            FWOLogger.error(f"Service object type {svc_type_str} not found")
            raise FwoImporterError(f"Service object type {svc_type_str} not found")
        return obj_type_id

    def set_user_obj_type_map(self, fwo_api_call: FwoApiCall):
        try:
            result = fwo_api_call.call(query=queries.GET_USER_OBJ_TYPE_MAP, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_usr_typ: {e!s}")
            raise FwoImporterError(f"Error while getting stm_usr_typ: {e!s}")

        user_type_map: dict[str, int] = {}
        for usr_type in result["data"]["stm_usr_typ"]:
            user_type_map.update({usr_type["usr_typ_name"]: usr_type["usr_typ_id"]})
        self.user_obj_type_map = user_type_map

    def lookup_user_obj_type_id(self, usr_type_str: str) -> int:
        obj_type_id = self.user_obj_type_map.get(usr_type_str, None)
        if obj_type_id is None:
            FWOLogger.error(f"User object type {usr_type_str} not found")
            raise FwoImporterError(f"User object type {usr_type_str} not found")
        return obj_type_id

    def set_protocol_map(self, fwo_api_call: FwoApiCall):
        try:
            result = fwo_api_call.call(query=queries.GET_PROTOCOL_MAP, query_variables={})
        except Exception as e:
            FWOLogger.error(f"Error while getting stm_ip_proto: {e!s}")
            raise FwoImporterError(f"Error while getting stm_ip_proto: {e!s}")

        protocol_map: dict[str, int] = {}
        for proto in result["data"]["stm_ip_proto"]:
            protocol_map.update({proto["ip_proto_name"].lower(): proto["ip_proto_id"]})
        self.protocol_map = protocol_map

    def lookup_protocol_id(self, proto_str: str) -> int:
        proto_id = self.protocol_map.get(proto_str.lower(), None)
        if proto_id is None:
            FWOLogger.error(f"Protocol {proto_str} not found")
            raise FwoImporterError(f"Protocol {proto_str} not found")
        return proto_id

    # getting all gateways (not limitited to the current mgm_id) to support super managements
    # creates a dict with key = gateway.uid  and value = gateway.id
    # and also            key = gateway.name and value = gateway.id
    def set_gateway_map(self, fwo_api_call: FwoApiCall):
        try:
            result = fwo_api_call.call(query=queries.GET_GATEWAY_MAP, query_variables={})
        except Exception:
            FWOLogger.error("Error while getting gateways")
            self.gateway_map = {}
            raise FwoImporterError("Error while getting gateways")

        m = {}
        for gw in result["data"]["device"]:
            if gw["mgm_id"] not in m:
                m[gw["mgm_id"]] = {}
            m[gw["mgm_id"]][gw["dev_uid"]] = gw["dev_id"]
        self.gateway_map = m

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

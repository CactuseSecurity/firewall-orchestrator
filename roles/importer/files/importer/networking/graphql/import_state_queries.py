GET_ACTION_MAP: str = """
query getActionMap {
    stm_action {
        action_name
        action_id
        allowed
    }
}
"""

GET_TRACK_MAP: str = """
query getTrackMap {
    stm_track {
        track_name
        track_id
    }
}
"""

GET_LINK_TYPE_MAP: str = """
query getLinkType {
    stm_link_type {
        id
        name
    }
}
"""

GET_COLOR_MAP: str = """
query getColors {
    stm_color {
        color_id
        color_name
    }
}
"""

GET_NETWORK_OBJ_TYPE_MAP: str = """
query getNetworkObjTypeMap {
    stm_obj_typ {
        obj_typ_name
        obj_typ_id
    }
}
"""

GET_SERVICE_OBJ_TYPE_MAP: str = """
query getServiceObjTypeMap {
    stm_svc_typ {
        svc_typ_name
        svc_typ_id
    }
}
"""

GET_USER_OBJ_TYPE_MAP: str = """
query getUserObjTypeMap {
    stm_usr_typ {
        usr_typ_name
        usr_typ_id
    }
}
"""

GET_PROTOCOL_MAP: str = """
query getIpProtocols {
    stm_ip_proto {
        ip_proto_id
        ip_proto_name
    }
}
"""

GET_GATEWAY_MAP: str = """
query getGatewayMap {
    device {
        mgm_id
        dev_id
        dev_uid
    }
}
"""

GET_MANAGEMENT_MAP: str = """
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

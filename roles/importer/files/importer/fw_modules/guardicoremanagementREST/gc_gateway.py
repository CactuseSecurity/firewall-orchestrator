from __future__ import annotations

from typing import TYPE_CHECKING

import fwo_const
from fw_modules.guardicoremanagementREST.gc_rule import get_rule_installon
from fwo_api import FwoApi
from fwo_exceptions import FwoImporterError

if TYPE_CHECKING:
    from model_controllers.import_state_controller import ImportStateController


def ensure_policy_gateway(import_state: ImportStateController) -> None:
    """Register a dummy FWO gateway to group policies enforced on Guardicore hosts."""
    state = import_state.state
    mgm_id = state.mgm_details.current_mgm_id
    if state.gateway_map.get(mgm_id):
        return

    gateway_uid = get_rule_installon(state.mgm_details)
    type_result = import_state.api_call.call(
        query=FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "device/getGuardicoreGatewayType.graphql"]),
        query_variables={},
    )
    device_types = type_result.get("data", {}).get("stm_dev_typ", [])
    if "errors" in type_result or len(device_types) != 1:
        raise FwoImporterError("Cannot register Guardicore policy gateway: Guardicore Gateway REST type not found")

    result = import_state.api_call.call(
        query=FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "device/newGuardicoreGateway.graphql"]),
        query_variables={
            "name": gateway_uid,
            "uid": gateway_uid,
            "devTypeId": device_types[0]["dev_typ_id"],
            "managementId": mgm_id,
        },
    )
    if "errors" in result:
        raise FwoImporterError("Cannot register Guardicore policy gateway: device creation failed")
    gateways = result.get("data", {}).get("insert_device", {}).get("returning", [])
    if len(gateways) != 1 or not isinstance(gateways[0].get("newId"), int):
        raise FwoImporterError("Cannot register Guardicore policy gateway: device ID missing")
    state.gateway_map[mgm_id] = {gateway_uid: gateways[0]["newId"]}

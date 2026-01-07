from collections.abc import Generator
from typing import Any

import fwo_const
from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig
from fw_modules.fortiosmanagementREST.fos_network import normalize_network_objects
from fw_modules.fortiosmanagementREST.fos_service import normalize_service_objects
from fwo_log import FWOLogger
from models.fwconfig_normalized import FwConfigNormalized


def normalize_config(native_config: FortiOSConfig) -> FwConfigNormalized:
    """
    Normalize FortiOS Management REST native configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.

    Returns:
        FwConfigNormalized: The normalized configuration.

    """
    normalized_config = FwConfigNormalized()

    for nw_obj in normalize_network_objects(native_config):
        normalized_config.network_objects[nw_obj.obj_uid] = nw_obj
    FWOLogger.debug(f"Normalized {len(normalized_config.network_objects)} network objects.")

    for svc_obj in normalize_service_objects(native_config):
        normalized_config.service_objects[svc_obj.svc_uid] = svc_obj
    FWOLogger.debug(f"Normalized {len(normalized_config.service_objects)} service objects.")

    for user in normalize_users(native_config):
        normalized_config.users[user["user_uid"]] = user
    FWOLogger.debug(f"Normalized {len(normalized_config.users)} user objects.")

    # TODO: rules

    # TODO: gateway

    return normalized_config


def normalize_users(native_config: FortiOSConfig) -> Generator[dict[str, Any]]:
    """
    Normalize a user object.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.

    Yields:
        dict[str, Any]: The normalized user object.

    """
    # user/local
    for user_obj in native_config.user_obj_local:
        yield {
            "user_name": user_obj.name,
            "user_uid": user_obj.name,
            "user_typ": "simple",
            "user_color": fwo_const.DEFAULT_COLOR,
            "user_comment": None,
            "user_member_refs": None,
            "user_member_names": None,
        }

    # user/group
    for user_obj in native_config.user_obj_group:
        yield {
            "user_name": user_obj.name,
            "user_uid": user_obj.name,
            "user_typ": "group",
            "user_color": fwo_const.DEFAULT_COLOR,
            "user_comment": None,
            "user_member_refs": (
                fwo_const.LIST_DELIMITER.join([member.name for member in user_obj.member]) if user_obj.member else None
            ),
            "user_member_names": (
                fwo_const.LIST_DELIMITER.join([member.name for member in user_obj.member]) if user_obj.member else None
            ),
        }

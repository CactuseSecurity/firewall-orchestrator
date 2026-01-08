from collections.abc import Generator
from typing import TYPE_CHECKING, Any

import fwo_const
from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig
from fw_modules.fortiosmanagementREST.fos_network import normalize_network_objects
from fw_modules.fortiosmanagementREST.fos_rule import normalize_access_rules
from fw_modules.fortiosmanagementREST.fos_service import normalize_service_objects
from fwo_log import FWOLogger
from model_controllers.management_controller import ManagementController
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from models.rulebase import Rulebase
from models.rulebase_link import RulebaseLinkUidBased

if TYPE_CHECKING:
    from models.rule import RuleNormalized


def normalize_config(native_config: FortiOSConfig, mgm_details: ManagementController) -> FwConfigNormalized:
    """
    Normalize FortiOS Management REST native configuration.

    Args:
        native_config (FortiOSConfig): The native FortiOS configuration.
        mgm_details (ManagementController): The management details object.

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

    nw_obj_lookup_dict = {obj.obj_name: obj.obj_uid for obj in normalized_config.network_objects.values()}

    rulebase_name = "access_rules"
    rules: dict[str, RuleNormalized] = {}
    for rule in normalize_access_rules(native_config, mgm_details, nw_obj_lookup_dict):
        if not rule.rule_uid:
            FWOLogger.warning(f"Skipping rule '{rule.rule_name}' without UID.")
            continue
        rules[rule.rule_uid] = rule
    rulebase = Rulebase(uid=rulebase_name, name=rulebase_name, mgm_uid=mgm_details.uid, is_global=False, rules=rules)

    rulebase_links = [
        RulebaseLinkUidBased(
            to_rulebase_uid=rulebase.uid,
            link_type="ordered",
            is_initial=True,
            is_global=False,
            is_section=False,
        )
    ]

    gateway = Gateway(
        Uid=mgm_details.devices[0]["name"],
        Name=mgm_details.devices[0]["name"],
        Routing=[],
        RulebaseLinks=rulebase_links,
        GlobalPolicyUid=None,
        EnforcedPolicyUids=[],
        EnforcedNatPolicyUids=[],
        ImportDisabled=False,
        ShowInUI=True,
    )

    normalized_config.gateways = [gateway]
    normalized_config.rulebases = [rulebase]

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

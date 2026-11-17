from __future__ import annotations

from typing import TYPE_CHECKING

from fwo_log import FWOLogger
from models.rule_hit import RuleHit

if TYPE_CHECKING:
    from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
    from models.gateway import Gateway
    from models.rulebase import Rulebase


def extract_rule_hits(config: FwConfigManagerListController, management_map: dict[str, int]) -> list[RuleHit]:
    """
    Derive the rule hits per gateway from a normalized configuration whose rules carry their last hit.

    Used for vendors reporting a rule's last hit per rulebase instead of per gateway: the hit of a rule is
    stored for every gateway linked to the rule's rulebase.

    Args:
        config: Normalized configuration of the management (incl. sub-managers).
        management_map: Management uid to management id of the management and its sub-managers.

    Returns:
        One rule hit per gateway and rule with hit information.

    """
    rulebases_by_uid: dict[str, Rulebase] = {}
    for manager in config.ManagerSet:
        for normalized_config in manager.configs:
            for rulebase in normalized_config.rulebases:
                rulebases_by_uid[rulebase.uid] = rulebase

    rule_hits: list[RuleHit] = []
    for manager in config.ManagerSet:
        gateway_mgm_id = management_map.get(manager.manager_uid)
        if gateway_mgm_id is None:
            FWOLogger.warning(f"rule hit import - no management found for manager uid {manager.manager_uid}, skipping")
            continue
        for normalized_config in manager.configs:
            for gateway in normalized_config.gateways:
                rule_hits.extend(_extract_gateway_rule_hits(gateway, gateway_mgm_id, rulebases_by_uid, management_map))
    return rule_hits


def _extract_gateway_rule_hits(
    gateway: Gateway,
    gateway_mgm_id: int,
    rulebases_by_uid: dict[str, Rulebase],
    management_map: dict[str, int],
) -> list[RuleHit]:
    """Return the rule hits of all rulebases linked to the gateway."""
    rule_hits: list[RuleHit] = []
    if gateway.Uid is None:
        return rule_hits

    for rulebase_uid in get_linked_rulebase_uids(gateway):
        rulebase = rulebases_by_uid.get(rulebase_uid)
        if rulebase is None:
            continue
        rule_mgm_id = management_map.get(rulebase.mgm_uid)
        if rule_mgm_id is None:
            FWOLogger.warning(f"rule hit import - no management found for rulebase {rulebase.uid}, skipping")
            continue
        for rule in rulebase.rules.values():
            if rule.last_hit is None or not rule.rule_uid:
                continue
            rule_hits.append(
                RuleHit(
                    rule_mgm_id=rule_mgm_id,
                    rule_uid=rule.rule_uid,
                    gateway_mgm_id=gateway_mgm_id,
                    gateway_uid=gateway.Uid,
                    last_hit=rule.last_hit,
                )
            )
    return rule_hits


def get_linked_rulebase_uids(gateway: Gateway) -> list[str]:
    """Return the uids of all rulebases the gateway's rulebase links point from or to, in link order."""
    linked_uids: dict[str, None] = {}
    for link in gateway.RulebaseLinks:
        if link.from_rulebase_uid is not None:
            linked_uids[link.from_rulebase_uid] = None
        linked_uids[link.to_rulebase_uid] = None
    return list(linked_uids)

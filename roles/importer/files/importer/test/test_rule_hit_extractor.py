from __future__ import annotations

from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.rule_hit_extractor import extract_rule_hits, get_linked_rulebase_uids
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager
from models.gateway import Gateway
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType
from models.rulebase import Rulebase
from models.rulebase_link import RulebaseLinkUidBased

GLOBAL_MGM_UID = "global-uid"
ADOM_MGM_UID = "adom-uid"
GLOBAL_MGM_ID = 10
ADOM_MGM_ID = 11
MANAGEMENT_MAP = {GLOBAL_MGM_UID: GLOBAL_MGM_ID, ADOM_MGM_UID: ADOM_MGM_ID}
HIT = "2026-11-06T08:15:00+00:00"


def _rule(rule_uid: str, last_hit: str | None) -> RuleNormalized:
    return RuleNormalized(
        rule_num_numeric=1.0,
        rule_disabled=False,
        rule_src_neg=False,
        rule_src="any",
        rule_src_refs="any",
        rule_dst_neg=False,
        rule_dst="any",
        rule_dst_refs="any",
        rule_svc_neg=False,
        rule_svc="any",
        rule_svc_refs="any",
        rule_action=RuleAction.ACCEPT,
        rule_track=RuleTrack.NONE,
        rule_installon=None,
        rule_time="any",
        rule_name=rule_uid,
        rule_uid=rule_uid,
        rule_custom_fields=None,
        rule_implied=False,
        rule_type=RuleType.ACCESS,
        last_change_admin=None,
        parent_rule_uid=None,
        last_hit=last_hit,
    )


def _link(from_uid: str | None, to_uid: str) -> RulebaseLinkUidBased:
    return RulebaseLinkUidBased(
        from_rulebase_uid=from_uid,
        to_rulebase_uid=to_uid,
        is_initial=from_uid is None,
        is_global=False,
        is_section=False,
    )


def _manager(uid: str, rulebases: list[Rulebase], gateways: list[Gateway]) -> FwConfigManager:
    return FwConfigManager(
        manager_uid=uid,
        manager_name=uid,
        configs=[FwConfigNormalized(rulebases=rulebases, gateways=gateways)],
    )


def _config() -> FwConfigManagerListController:
    global_rulebase = Rulebase(
        uid="rb-global", name="global header", mgm_uid=GLOBAL_MGM_UID, rules={"g1": _rule("g1", HIT)}
    )
    package_rulebase = Rulebase(
        uid="rb-pkg",
        name="package",
        mgm_uid=ADOM_MGM_UID,
        rules={"r1": _rule("r1", HIT), "r2": _rule("r2", None)},
    )
    gateway = Gateway(Uid="gw-1", Name="gw-1", RulebaseLinks=[_link(None, "rb-global"), _link("rb-global", "rb-pkg")])
    gateway_without_uid = Gateway(Uid=None, RulebaseLinks=[_link(None, "rb-pkg")])
    config = FwConfigManagerListController()
    config.ManagerSet = [
        _manager(GLOBAL_MGM_UID, [global_rulebase], []),
        _manager(ADOM_MGM_UID, [package_rulebase], [gateway, gateway_without_uid]),
    ]
    return config


def test_hits_of_all_linked_rulebases_are_stored_for_the_gateway() -> None:
    rule_hits = extract_rule_hits(_config(), MANAGEMENT_MAP)

    found = {(hit.rule_mgm_id, hit.rule_uid, hit.gateway_mgm_id, hit.gateway_uid) for hit in rule_hits}
    assert found == {
        (GLOBAL_MGM_ID, "g1", ADOM_MGM_ID, "gw-1"),
        (ADOM_MGM_ID, "r1", ADOM_MGM_ID, "gw-1"),
    }
    assert all(hit.last_hit == HIT for hit in rule_hits)


def test_managers_and_rulebases_without_management_id_are_skipped() -> None:
    assert extract_rule_hits(_config(), {ADOM_MGM_UID: ADOM_MGM_ID}) == [
        hit for hit in extract_rule_hits(_config(), MANAGEMENT_MAP) if hit.rule_uid == "r1"
    ]
    assert extract_rule_hits(_config(), {GLOBAL_MGM_UID: GLOBAL_MGM_ID}) == []


def test_linked_rulebase_uids_keep_link_order_without_duplicates() -> None:
    gateway = Gateway(Uid="gw", RulebaseLinks=[_link(None, "a"), _link("a", "b"), _link("b", "a")])

    assert get_linked_rulebase_uids(gateway) == ["a", "b"]

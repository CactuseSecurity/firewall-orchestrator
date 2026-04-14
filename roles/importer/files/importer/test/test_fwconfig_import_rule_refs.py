import unittest.mock

from model_controllers.fwconfig_import_rule import FwConfigImportRule, RefType
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType
from states.management_state import ManagementState


def build_rule(rule_uid: str) -> RuleNormalized:
    return RuleNormalized(
        rule_num=1,
        rule_num_numeric=1.0,
        rule_disabled=False,
        rule_src_neg=False,
        rule_src="src",
        rule_src_refs="src",
        rule_dst_neg=False,
        rule_dst="dst",
        rule_dst_refs="dst",
        rule_svc_neg=False,
        rule_svc="svc",
        rule_svc_refs="svc",
        rule_src_zone="src_zone",
        rule_dst_zone="dst_zone",
        rule_time="time",
        rule_action=RuleAction.ACCEPT,
        rule_track=RuleTrack.NONE,
        rule_implied=False,
        rule_type=RuleType.ACCESS,
        rule_uid=rule_uid,
    )


class TestFwconfigImportRuleRefs:
    def test_get_outdated_refs_to_remove_removes_all_on_missing_rule(
        self,
        fwconfig_import_rule: FwConfigImportRule,
        management_state: ManagementState,
    ):
        prev_rule = build_rule("rule-1")

        fake_refs = {
            RefType.SRC: [("src", None)],
            RefType.DST: [("dst", "user")],
            RefType.SVC: ["svc"],
            RefType.NWOBJ_RESOLVED: ["src", "dst"],
            RefType.SVC_RESOLVED: ["svc"],
            RefType.USER_RESOLVED: ["user"],
            RefType.SRC_ZONE: ["src_zone"],
            RefType.DST_ZONE: ["dst_zone"],
            RefType.TIME: ["time"],
        }

        management_state.uid2id_mapper.get_rule_id = unittest.mock.MagicMock(return_value=100)

        def _get_network_object_id(uid: str, before_update: bool = True) -> int:  # noqa: ARG001
            return {"src": 10, "dst": 11}[uid]

        def _get_zone_object_id(uid: str, before_update: bool = True) -> int:  # noqa: ARG001
            return {"src_zone": 40, "dst_zone": 41}[uid]

        management_state.uid2id_mapper.get_network_object_id = unittest.mock.MagicMock(
            side_effect=_get_network_object_id
        )
        management_state.uid2id_mapper.get_user_id = unittest.mock.MagicMock(return_value=30)
        management_state.uid2id_mapper.get_service_object_id = unittest.mock.MagicMock(return_value=20)
        management_state.uid2id_mapper.get_zone_object_id = unittest.mock.MagicMock(side_effect=_get_zone_object_id)
        management_state.uid2id_mapper.get_time_object_id = unittest.mock.MagicMock(return_value=50)

        fwconfig_import_rule.get_rule_refs = unittest.mock.MagicMock(
            return_value=fake_refs,
        )
        refs_to_remove = fwconfig_import_rule.get_outdated_refs_to_remove(prev_rule, None, remove_all=True)

        assert {
            "_and": [{"rule_id": {"_eq": 100}}, {"obj_id": {"_eq": 10}}, {"user_id": {"_is_null": True}}]
        } in refs_to_remove[RefType.SRC]
        assert {
            "_and": [{"rule_id": {"_eq": 100}}, {"obj_id": {"_eq": 11}}, {"user_id": {"_eq": 30}}]
        } in refs_to_remove[RefType.DST]
        assert {"_and": [{"rule_id": {"_eq": 100}}, {"svc_id": {"_eq": 20}}]} in refs_to_remove[RefType.SVC]
        assert {"_and": [{"rule_id": {"_eq": 100}}, {"obj_id": {"_eq": 10}}]} in refs_to_remove[RefType.NWOBJ_RESOLVED]
        assert {"_and": [{"rule_id": {"_eq": 100}}, {"obj_id": {"_eq": 11}}]} in refs_to_remove[RefType.NWOBJ_RESOLVED]
        assert {"_and": [{"rule_id": {"_eq": 100}}, {"svc_id": {"_eq": 20}}]} in refs_to_remove[RefType.SVC_RESOLVED]
        assert {"_and": [{"rule_id": {"_eq": 100}}, {"user_id": {"_eq": 30}}]} in refs_to_remove[RefType.USER_RESOLVED]
        assert {"_and": [{"rule_id": {"_eq": 100}}, {"zone_id": {"_eq": 40}}]} in refs_to_remove[RefType.SRC_ZONE]
        assert {"_and": [{"rule_id": {"_eq": 100}}, {"zone_id": {"_eq": 41}}]} in refs_to_remove[RefType.DST_ZONE]
        assert {"_and": [{"rule_id": {"_eq": 100}}, {"time_obj_id": {"_eq": 50}}]} in refs_to_remove[RefType.TIME]

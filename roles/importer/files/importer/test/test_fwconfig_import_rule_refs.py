from model_controllers.fwconfig_import_rule import RefType
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType
from test.mocking.mock_fwconfig_import_rule import MockFwConfigImportRule


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
        rule_action=RuleAction.ACCEPT,
        rule_track=RuleTrack.NONE,
        rule_implied=False,
        rule_type=RuleType.ACCESS,
        rule_uid=rule_uid,
    )


def test_get_outdated_refs_to_remove_removes_all_on_missing_rule():
    importer = MockFwConfigImportRule()
    prev_rule = build_rule("rule-1")

    fake_refs = {
        RefType.SRC: [("src1", None)],
        RefType.DST: [("dst1", "user1")],
        RefType.SVC: ["svc1"],
        RefType.NWOBJ_RESOLVED: ["src1", "dst1"],
        RefType.SVC_RESOLVED: ["svc1"],
        RefType.USER_RESOLVED: ["user1"],
        RefType.SRC_ZONE: ["zone1"],
        RefType.DST_ZONE: ["zone2"],
    }

    def fake_get_rule_refs(_rule, is_prev: bool = False):
        return fake_refs

    class DummyMapper:
        def get_rule_id(self, _uid, before_update: bool = True):
            return 100

        def get_network_object_id(self, uid, before_update: bool = True):
            return {"src1": 10, "dst1": 11}[uid]

        def get_user_id(self, _uid, before_update: bool = True):
            return 30

        def get_service_object_id(self, _uid, before_update: bool = True):
            return 20

        def get_zone_object_id(self, uid, before_update: bool = True):
            return {"zone1": 40, "zone2": 41}[uid]

    importer.get_rule_refs = fake_get_rule_refs
    importer.uid2id_mapper = DummyMapper()

    refs_to_remove = importer.get_outdated_refs_to_remove(prev_rule, None, remove_all=True)

    assert {"_and": [{"rule_id": {"_eq": 100}}, {"obj_id": {"_eq": 10}}, {"user_id": {"_is_null": True}}]} in refs_to_remove[
        RefType.SRC
    ]
    assert {"_and": [{"rule_id": {"_eq": 100}}, {"obj_id": {"_eq": 11}}, {"user_id": {"_eq": 30}}]} in refs_to_remove[
        RefType.DST
    ]
    assert {"_and": [{"rule_id": {"_eq": 100}}, {"svc_id": {"_eq": 20}}]} in refs_to_remove[RefType.SVC]
    assert {"_and": [{"rule_id": {"_eq": 100}}, {"obj_id": {"_eq": 10}}]} in refs_to_remove[
        RefType.NWOBJ_RESOLVED
    ]
    assert {"_and": [{"rule_id": {"_eq": 100}}, {"obj_id": {"_eq": 11}}]} in refs_to_remove[
        RefType.NWOBJ_RESOLVED
    ]
    assert {"_and": [{"rule_id": {"_eq": 100}}, {"svc_id": {"_eq": 20}}]} in refs_to_remove[RefType.SVC_RESOLVED]
    assert {"_and": [{"rule_id": {"_eq": 100}}, {"user_id": {"_eq": 30}}]} in refs_to_remove[
        RefType.USER_RESOLVED
    ]
    assert {"_and": [{"rule_id": {"_eq": 100}}, {"zone_id": {"_eq": 40}}]} in refs_to_remove[RefType.SRC_ZONE]
    assert {"_and": [{"rule_id": {"_eq": 100}}, {"zone_id": {"_eq": 41}}]} in refs_to_remove[RefType.DST_ZONE]

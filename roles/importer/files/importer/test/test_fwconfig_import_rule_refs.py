import unittest.mock

import pytest
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from model_controllers.fwconfig_import_rule import FwConfigImportRule, RefType
from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import ManagementController
from models.fwconfig_normalized import FwConfigNormalized
from models.import_state import ImportState
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType
from models.rulebase import Rulebase
from pytest_mock import MockerFixture
from services.uid2id_mapper import Uid2IdMapper


def build_rule(rule_uid: str) -> RuleNormalized:
    return RuleNormalized(
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


@pytest.fixture
def import_state_controller(
    management_controller: ManagementController,
    api_call: FwoApiCall,
    api_connection: FwoApi,
) -> ImportStateController:
    import_state = ImportState()
    import_state.mgm_details = management_controller
    controller = ImportStateController(state=import_state, api_call=api_call)
    controller.state = import_state
    controller.api_call = api_call
    controller.api_connection = api_connection
    return controller


class TestFwconfigImportRuleRefs:
    def test_get_outdated_refs_to_remove_removes_all_on_missing_rule(
        self,
        uid2id_mapper: Uid2IdMapper,
        fwconfig_import_rule: FwConfigImportRule,
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

        uid2id_mapper.get_rule_id = unittest.mock.MagicMock(return_value=100)

        def _get_network_object_id(uid: str, before_update: bool = True) -> int:  # noqa: ARG001
            return {"src": 10, "dst": 11}[uid]

        def _get_zone_object_id(uid: str, before_update: bool = True) -> int:  # noqa: ARG001
            return {"src_zone": 40, "dst_zone": 41}[uid]

        uid2id_mapper.get_network_object_id = unittest.mock.MagicMock(side_effect=_get_network_object_id)
        uid2id_mapper.get_user_id = unittest.mock.MagicMock(return_value=30)
        uid2id_mapper.get_service_object_id = unittest.mock.MagicMock(return_value=20)
        uid2id_mapper.get_zone_object_id = unittest.mock.MagicMock(side_effect=_get_zone_object_id)
        uid2id_mapper.get_time_object_id = unittest.mock.MagicMock(return_value=50)

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

    def test_add_new_refs_forces_all_refs_for_xlate_fk_repoint_uid(
        self,
        fwconfig_import_rule: FwConfigImportRule,
        mocker: MockerFixture,
    ):
        # F52: a NAT "original" rule repointed to a new rule_id (F45) keeps identical content
        # to its previous version, so without force_all_refs_rule_uids the content-equality
        # check in add_new_refs would treat its refs as unchanged and never insert them for
        # the new row, leaving it with no src/dst/svc/zone refs.
        rule = build_rule("orig-uid")
        rulebase = Rulebase(uid="rb-uid", name="Rulebase", mgm_uid="mgm-uid", rules={"orig-uid": rule})
        prev_rulebase = Rulebase(uid="rb-uid", name="Rulebase", mgm_uid="mgm-uid", rules={"orig-uid": rule})
        fwconfig_import_rule.normalized_config = FwConfigNormalized(rulebases=[rulebase])
        prev_config = FwConfigNormalized(rulebases=[prev_rulebase])

        fake_refs = {
            RefType.SRC: [("src", None)],
            RefType.DST: [],
            RefType.SVC: [],
            RefType.NWOBJ_RESOLVED: [],
            RefType.SVC_RESOLVED: [],
            RefType.USER_RESOLVED: [],
            RefType.SRC_ZONE: [],
            RefType.DST_ZONE: [],
            RefType.TIME: [],
        }
        fwconfig_import_rule.get_rule_refs = unittest.mock.Mock(return_value=fake_refs)
        fwconfig_import_rule.uid2id_mapper.get_rule_id = unittest.mock.Mock(return_value=100)
        fwconfig_import_rule.uid2id_mapper.get_network_object_id = unittest.mock.Mock(return_value=10)
        fwconfig_import_rule.import_details.api_call.call = mocker.Mock(
            return_value={"data": {f"insert_{ref_type.value}": {"affected_rows": 0} for ref_type in RefType}}
        )

        fwconfig_import_rule.add_new_refs(prev_config, {"orig-uid"})

        query_variables = fwconfig_import_rule.import_details.api_call.call.call_args.kwargs["query_variables"]
        rule_froms = query_variables["ruleFroms"]

        assert len(rule_froms) == 1
        assert rule_froms[0]["rule_id"] == 100
        assert rule_froms[0]["obj_id"] == 10

    def test_remove_outdated_refs_forces_all_refs_for_xlate_fk_repoint_uid(
        self,
        fwconfig_import_rule: FwConfigImportRule,
        mocker: MockerFixture,
    ):
        # F52: without force_all_refs_rule_uids, remove_outdated_refs would treat the unchanged
        # rule content as "not removed or changed" and leave the old row's refs (now pointing at
        # a superseded rule_id) active in the database.
        rule = build_rule("orig-uid")
        rulebase = Rulebase(uid="rb-uid", name="Rulebase", mgm_uid="mgm-uid", rules={"orig-uid": rule})
        prev_rulebase = Rulebase(uid="rb-uid", name="Rulebase", mgm_uid="mgm-uid", rules={"orig-uid": rule})
        fwconfig_import_rule.normalized_config = FwConfigNormalized(rulebases=[rulebase])
        prev_config = FwConfigNormalized(rulebases=[prev_rulebase])

        fake_refs = {
            RefType.SRC: [("src", None)],
            RefType.DST: [],
            RefType.SVC: [],
            RefType.NWOBJ_RESOLVED: [],
            RefType.SVC_RESOLVED: [],
            RefType.USER_RESOLVED: [],
            RefType.SRC_ZONE: [],
            RefType.DST_ZONE: [],
            RefType.TIME: [],
        }
        fwconfig_import_rule.get_rule_refs = unittest.mock.Mock(return_value=fake_refs)
        fwconfig_import_rule.uid2id_mapper.get_rule_id = unittest.mock.Mock(return_value=100)
        fwconfig_import_rule.uid2id_mapper.get_network_object_id = unittest.mock.Mock(return_value=10)
        fwconfig_import_rule.import_details.api_call.call = mocker.Mock(
            return_value={"data": {f"update_{ref_type.value}": {"affected_rows": 0} for ref_type in RefType}}
        )

        fwconfig_import_rule.remove_outdated_refs(prev_config, {"orig-uid"})

        query_variables = fwconfig_import_rule.import_details.api_call.call.call_args.kwargs["query_variables"]
        rule_froms = query_variables["ruleFroms"]

        assert len(rule_froms) == 1
        assert rule_froms[0]["_and"][0]["rule_id"]["_eq"] == 100
        assert rule_froms[0]["_and"][1]["obj_id"]["_eq"] == 10

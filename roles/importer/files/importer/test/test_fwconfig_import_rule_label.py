import unittest.mock
from typing import Any

import pytest
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoApiWriteError, FwoImporterError
from model_controllers.fwconfig_import_rule_label import FwConfigImportRuleLabel
from models.rule import RuleAction, RuleLabels, RuleNormalized, RuleTrack

IMPORT_ID = 42
EVENT_ID = 7
LABEL_IDS: dict[tuple[str, str], int] = {
    ("AppRole", "AR1"): 1,
    ("AppRole", "AR2"): 2,
    ("Stage", "Prod"): 3,
    ("Stage", "Test"): 4,
}


def build_rule(src_labels: RuleLabels | None = None, dst_labels: RuleLabels | None = None) -> RuleNormalized:
    return RuleNormalized(
        rule_num_numeric=1,
        rule_disabled=False,
        rule_src_neg=False,
        rule_src="Any",
        rule_src_refs="any",
        rule_dst_neg=False,
        rule_dst="Any",
        rule_dst_refs="any",
        rule_svc_neg=False,
        rule_svc="Any",
        rule_svc_refs="any",
        rule_action=RuleAction.ACCEPT,
        rule_track=RuleTrack.LOG,
        rule_implied=False,
        rule_src_labels=src_labels,
        rule_dst_labels=dst_labels,
    )


def answer_api(query: str, query_variables: dict[str, Any], analyze_payload: bool = False) -> dict[str, Any]:  # noqa: ARG001
    if "addLabelChangeEvent" in query:
        return {"data": {"insert_labelling_label_change_event_one": {"id": EVENT_ID}}}
    if "getLabelIds" in query:
        labels = [
            {"label_id": label_id, "value": value, "label_key": {"key_name": key_name, "allow_multiple_values": False}}
            for (key_name, value), label_id in LABEL_IDS.items()
        ]
        return {"data": {"labelling_label_value": labels}}
    if "removeRuleLabels" in query:
        return {
            "data": {
                "update_labelling_rule_source_label": {"affected_rows": 2},
                "update_labelling_rule_destination_label": {"affected_rows": 1},
            }
        }
    return {
        "data": {
            "insert_labelling_rule_source_label": {"affected_rows": len(query_variables["sourceLabels"])},
            "insert_labelling_rule_destination_label": {"affected_rows": len(query_variables["destinationLabels"])},
        }
    }


def calls_of(api_call: FwoApiCall, operation: str) -> list[dict[str, Any]]:
    mock: unittest.mock.MagicMock = api_call.call  # type: ignore[assignment]
    return [call.kwargs["query_variables"] for call in mock.call_args_list if operation in call.args[0]]


@pytest.fixture
def label_api_call(api_call: FwoApiCall) -> FwoApiCall:
    api_call.call = unittest.mock.MagicMock(side_effect=answer_api)
    return api_call


class TestFwConfigImportRuleLabel:
    def test_rules_without_labels_touch_nothing(self, label_api_call: FwoApiCall) -> None:
        importer = FwConfigImportRuleLabel(label_api_call, IMPORT_ID)

        assert importer.write_rule_label_assignments({10: build_rule()}, []) == (0, 0)
        label_api_call.call.assert_not_called()  # type: ignore[attr-defined]

    def test_inserted_rule_versions_get_their_assignments_per_and_group(self, label_api_call: FwoApiCall) -> None:
        rule = build_rule(
            src_labels=[{"AppRole": "AR1"}],
            dst_labels=[{"AppRole": "AR2", "Stage": "Prod"}, {"AppRole": "AR2", "Stage": ["Prod", "Test"]}],
        )

        added, closed = FwConfigImportRuleLabel(label_api_call, IMPORT_ID).write_rule_label_assignments({10: rule}, [])

        assert (added, closed) == (6, 0)
        assert calls_of(label_api_call, "addLabelChangeEvent") == [{"importControlId": IMPORT_ID, "originIsUi": False}]
        assert calls_of(label_api_call, "removeRuleLabels") == []
        rows = calls_of(label_api_call, "addRuleLabels")[0]
        assert rows["sourceLabels"] == [{"rule_id": 10, "label_id": 1, "and_group": 0, "created_event_id": EVENT_ID}]
        assert [(row["and_group"], row["label_id"]) for row in rows["destinationLabels"]] == [
            (0, 2),
            (0, 3),
            (1, 2),
            (1, 3),
            (1, 4),
        ]

    def test_removed_rule_versions_get_their_assignments_closed(self, label_api_call: FwoApiCall) -> None:
        added, closed = FwConfigImportRuleLabel(label_api_call, IMPORT_ID).write_rule_label_assignments({}, [5, 6])

        assert (added, closed) == (0, 3)
        assert calls_of(label_api_call, "removeRuleLabels") == [{"ruleIds": [5, 6], "removedEventId": EVENT_ID}]
        assert calls_of(label_api_call, "addRuleLabels") == []
        assert calls_of(label_api_call, "getLabelIds") == []

    def test_label_ids_are_read_for_all_used_keys_and_values(self, label_api_call: FwoApiCall) -> None:
        rules = {10: build_rule(src_labels=[{"Stage": "Prod"}]), 11: build_rule(dst_labels=[{"AppRole": "AR1"}])}

        FwConfigImportRuleLabel(label_api_call, IMPORT_ID).write_rule_label_assignments(rules, [])

        assert calls_of(label_api_call, "getLabelIds") == [
            {"keyNames": ["AppRole", "Stage"], "values": ["AR1", "Prod"]}
        ]

    def test_duplicate_values_within_a_group_are_written_once(self) -> None:
        rows = FwConfigImportRuleLabel.build_assignments(10, [{"AppRole": ["AR1", "AR1"]}], LABEL_IDS, EVENT_ID)

        assert rows == [{"rule_id": 10, "label_id": 1, "and_group": 0, "created_event_id": EVENT_ID}]

    def test_unknown_labels_raise(self) -> None:
        with pytest.raises(FwoImporterError):
            FwConfigImportRuleLabel.build_assignments(10, [{"AppRole": "AR9"}], LABEL_IDS, EVENT_ID)

    def test_api_errors_raise(self, api_call: FwoApiCall) -> None:
        api_call.call = unittest.mock.MagicMock(return_value={"errors": [{"message": "denied"}]})

        with pytest.raises(FwoApiWriteError):
            FwConfigImportRuleLabel(api_call, IMPORT_ID).write_rule_label_assignments({}, [5])

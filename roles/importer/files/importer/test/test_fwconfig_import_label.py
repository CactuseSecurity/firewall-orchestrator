import unittest.mock

import pytest
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoApiWriteError
from model_controllers.fwconfig_import_label import FwConfigImportLabel, get_side_label_values
from models.fwconfig_normalized import FwConfigNormalized
from models.label import LabelNormalized
from models.rulebase import Rulebase
from test.test_fwconfig_import_rule_label import build_rule

LABELS = [
    LabelNormalized(key_name="Stage", value="Prod"),
    LabelNormalized(key_name="AppRole", value="AR1"),
    LabelNormalized(key_name="AppRole", value="AR2"),
    LabelNormalized(key_name="AppRole", value="AR1"),
]


class TestFwConfigImportLabel:
    def test_build_label_key_objects_groups_and_deduplicates(self) -> None:
        objects = FwConfigImportLabel.build_label_key_objects(LABELS)

        assert [obj["key_name"] for obj in objects] == ["AppRole", "Stage"]
        assert objects[0]["key_source_internal"] is False
        assert objects[0]["label_values"]["data"] == [
            {"value": "AR1", "is_imported": True},
            {"value": "AR2", "is_imported": True},
        ]
        assert objects[0]["label_values"]["on_conflict"]["constraint"] == "labelling_label_value_key_id_value_unique"

    def test_upsert_labels_calls_api(self, api_call: FwoApiCall) -> None:
        api_call.call = unittest.mock.MagicMock(
            return_value={"data": {"insert_labelling_label_key": {"affected_rows": 4}}}
        )

        assert FwConfigImportLabel(api_call).upsert_labels(LABELS) == 4

        mutation = api_call.call.call_args.args[0]
        assert "insert_labelling_label_key" in mutation
        assert "labelling_label_key_key_name_unique" in mutation
        assert len(api_call.call.call_args.kwargs["query_variables"]["labelKeys"]) == 2

    def test_upsert_without_labels_does_nothing(self, api_call: FwoApiCall) -> None:
        api_call.call = unittest.mock.MagicMock()

        assert FwConfigImportLabel(api_call).upsert_labels([]) == 0

        api_call.call.assert_not_called()

    def test_upsert_errors_raise(self, api_call: FwoApiCall) -> None:
        api_call.call = unittest.mock.MagicMock(return_value={"errors": [{"message": "denied"}]})

        with pytest.raises(FwoApiWriteError):
            FwConfigImportLabel(api_call).upsert_labels(LABELS)


class TestRuleSideLabels:
    def test_side_label_values_are_numbered_by_and_group(self) -> None:
        labels: list[dict[str, str | list[str]]] = [{"AppRole": ["AR1", "AR2"], "Stage": "Prod"}, {"AppRole": "AR3"}]

        assert get_side_label_values(labels) == [
            (0, "AppRole", "AR1"),
            (0, "AppRole", "AR2"),
            (0, "Stage", "Prod"),
            (1, "AppRole", "AR3"),
        ]
        assert get_side_label_values(None) == []

    def test_collect_labels_adds_the_labels_of_rule_sides(self) -> None:
        rule = build_rule(src_labels=[{"AppRole": "AR9"}], dst_labels=[{"Stage": ["Prod", "Test"]}])
        config = FwConfigNormalized(
            labels=[LabelNormalized(key_name="Stage", value="Prod")],
            rulebases=[Rulebase(uid="rb", name="rb", mgm_uid="m", rules={"rule-uid": rule})],
        )

        labels = FwConfigImportLabel.collect_labels(config)

        assert [(label.key_name, label.value) for label in labels] == [
            ("Stage", "Prod"),
            ("AppRole", "AR9"),
            ("Stage", "Prod"),
            ("Stage", "Test"),
        ]

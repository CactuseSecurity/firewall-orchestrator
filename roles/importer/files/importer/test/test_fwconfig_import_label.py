import unittest.mock

import pytest
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoApiWriteError
from model_controllers.fwconfig_import_label import FwConfigImportLabel
from models.label import LabelNormalized

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

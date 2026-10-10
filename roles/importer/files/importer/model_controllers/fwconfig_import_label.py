from __future__ import annotations

from typing import TYPE_CHECKING, Any

import fwo_const
from fwo_api import FwoApi
from fwo_exceptions import FwoApiWriteError
from fwo_log import FWOLogger

if TYPE_CHECKING:
    from fwo_api_call import FwoApiCall
    from models.label import LabelNormalized


class FwConfigImportLabel:
    """
    Writes imported key/value labels into labelling.label_key and labelling.label_value.

    Labels are global (not bound to a management), so they are upserted on every import instead of being diffed:
    existing keys and values are kept untouched, missing ones are added as imported. Labels that disappeared from
    the source are not removed, as they might still be referenced by label assignments.
    """

    def __init__(self, api_call: FwoApiCall) -> None:
        self.api_call = api_call

    @staticmethod
    def build_label_key_objects(labels: list[LabelNormalized]) -> list[dict[str, Any]]:
        """
        Group the labels by key into nested insert objects, dropping duplicates.

        Duplicates have to go, as an upsert must not touch the same row twice within one statement.
        """
        values_by_key: dict[str, list[str]] = {}
        for label in labels:
            values = values_by_key.setdefault(label.key_name, [])
            if label.value not in values:
                values.append(label.value)
        return [
            {
                "key_name": key_name,
                "key_source_internal": False,
                "label_values": {
                    "data": [{"value": value, "is_imported": True} for value in values],
                    "on_conflict": {
                        "constraint": "labelling_label_value_key_id_value_unique",
                        "update_columns": ["value"],
                    },
                },
            }
            for key_name, values in sorted(values_by_key.items())
        ]

    def upsert_labels(self, labels: list[LabelNormalized]) -> int:
        """
        Add all labels that are not yet known.

        Args:
            labels (list[LabelNormalized]): The imported labels.

        Returns:
            int: The number of label keys written.

        """
        if len(labels) == 0:
            return 0
        mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "labelling/upsertImportedLabels.graphql"])
        query_variables: dict[str, Any] = {"labelKeys": self.build_label_key_objects(labels)}
        result = self.api_call.call(mutation, query_variables=query_variables, analyze_payload=True)
        if "errors" in result:
            raise FwoApiWriteError(f"failed to write imported labels: {result['errors']!s}")
        changes = int(result["data"]["insert_labelling_label_key"]["affected_rows"])
        FWOLogger.debug(f"upserted {len(labels)} imported labels, {changes} rows affected")
        return changes

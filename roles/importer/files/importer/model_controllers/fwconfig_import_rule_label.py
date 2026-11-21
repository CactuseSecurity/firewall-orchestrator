from __future__ import annotations

from typing import TYPE_CHECKING, Any

import fwo_const
from fwo_api import FwoApi
from fwo_exceptions import FwoApiWriteError, FwoImporterError
from fwo_log import FWOLogger
from model_controllers.fwconfig_import_label import get_side_label_values

if TYPE_CHECKING:
    from fwo_api_call import FwoApiCall
    from models.rule import RuleLabels, RuleNormalized

LABELLING_QUERY_PATH = "labelling/"
SOURCE_LABELS_VARIABLE = "sourceLabels"
DESTINATION_LABELS_VARIABLE = "destinationLabels"


class FwConfigImportRuleLabel:
    """
    Writes the source and destination labels of rule versions into labelling.rule_source_label and
    labelling.rule_destination_label, the authoritative source of rule labels (rule_src_labels / rule_dst_labels of
    firewall.rule are a copy).

    Labels are part of the rule comparison, so a label change always creates a new rule version. The assignments are
    therefore not diffed: every inserted rule version gets its assignments, and the assignments of every removed rule
    version are closed. Both happen with one label change event per import, which is only created if a rule with labels
    is involved.
    """

    def __init__(self, api_call: FwoApiCall, import_id: int) -> None:
        self.api_call = api_call
        self.import_id = import_id

    @staticmethod
    def has_labels(rule: RuleNormalized) -> bool:
        return bool(rule.rule_src_labels) or bool(rule.rule_dst_labels)

    def write_rule_label_assignments(
        self, inserted_rules: dict[int, RuleNormalized], removed_rule_ids: list[int]
    ) -> tuple[int, int]:
        """
        Add the label assignments of inserted rule versions and close those of removed rule versions.

        Args:
            inserted_rules (dict[int, RuleNormalized]): The inserted rule versions by their new rule_id.
            removed_rule_ids (list[int]): The rule_ids of the removed rule versions that carried labels.

        Returns:
            tuple[int, int]: The number of added and of closed assignments.

        """
        rules_with_labels = {rule_id: rule for rule_id, rule in inserted_rules.items() if self.has_labels(rule)}
        if len(rules_with_labels) == 0 and len(removed_rule_ids) == 0:
            return 0, 0
        event_id = self.add_change_event()
        closed = self.close_assignments(removed_rule_ids, event_id)
        added = self.add_assignments(rules_with_labels, event_id)
        FWOLogger.debug(f"rule labels: {added} assignments added, {closed} closed (label change event {event_id})")
        return added, closed

    def add_change_event(self) -> int:
        result = self.call_api(
            "addLabelChangeEvent", {"importControlId": self.import_id, "originIsUi": False}, "label change event"
        )
        return int(result["data"]["insert_labelling_label_change_event_one"]["id"])

    def close_assignments(self, rule_ids: list[int], event_id: int) -> int:
        if len(rule_ids) == 0:
            return 0
        result = self.call_api(
            "removeRuleLabels", {"ruleIds": rule_ids, "removedEventId": event_id}, "closing of rule labels"
        )
        data = result["data"]
        return int(data["update_labelling_rule_source_label"]["affected_rows"]) + int(
            data["update_labelling_rule_destination_label"]["affected_rows"]
        )

    def add_assignments(self, rules: dict[int, RuleNormalized], event_id: int) -> int:
        if len(rules) == 0:
            return 0
        label_ids = self.get_label_ids(rules)
        query_variables: dict[str, Any] = {SOURCE_LABELS_VARIABLE: [], DESTINATION_LABELS_VARIABLE: []}
        for rule_id, rule in rules.items():
            query_variables[SOURCE_LABELS_VARIABLE].extend(
                self.build_assignments(rule_id, rule.rule_src_labels, label_ids, event_id)
            )
            query_variables[DESTINATION_LABELS_VARIABLE].extend(
                self.build_assignments(rule_id, rule.rule_dst_labels, label_ids, event_id)
            )
        result = self.call_api("addRuleLabels", query_variables, "rule labels")
        data = result["data"]
        return int(data["insert_labelling_rule_source_label"]["affected_rows"]) + int(
            data["insert_labelling_rule_destination_label"]["affected_rows"]
        )

    @staticmethod
    def build_assignments(
        rule_id: int, labels: RuleLabels | None, label_ids: dict[tuple[str, str], int], event_id: int
    ) -> list[dict[str, int]]:
        """
        Build the assignment rows of one rule side, one per label value and AND-group, without duplicates.

        Raises:
            FwoImporterError: If a label is unknown, which means it was not upserted before.

        """
        assignments: list[dict[str, int]] = []
        seen: set[tuple[int, int]] = set()
        for and_group, key_name, value in get_side_label_values(labels):
            label_id = label_ids.get((key_name, value))
            if label_id is None:
                raise FwoImporterError(f"label {key_name}: {value} of rule {rule_id} is unknown")
            if (and_group, label_id) in seen:
                continue
            seen.add((and_group, label_id))
            assignments.append(
                {"rule_id": rule_id, "label_id": label_id, "and_group": and_group, "created_event_id": event_id}
            )
        return assignments

    def get_label_ids(self, rules: dict[int, RuleNormalized]) -> dict[tuple[str, str], int]:
        """Return the label_id of every label value used by the given rules by (key name, value)."""
        key_names: set[str] = set()
        values: set[str] = set()
        for rule in rules.values():
            for side_labels in (rule.rule_src_labels, rule.rule_dst_labels):
                for _, key_name, value in get_side_label_values(side_labels):
                    key_names.add(key_name)
                    values.add(value)
        result = self.call_api(
            "getLabelIds", {"keyNames": sorted(key_names), "values": sorted(values)}, "reading of label ids"
        )
        return {
            (label["label_key"]["key_name"], label["value"]): int(label["label_id"])
            for label in result["data"]["labelling_label_value"]
        }

    def call_api(self, operation: str, query_variables: dict[str, Any], description: str) -> dict[str, Any]:
        query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + LABELLING_QUERY_PATH + operation + ".graphql"])
        result = self.api_call.call(query, query_variables=query_variables, analyze_payload=True)
        if "errors" in result:
            raise FwoApiWriteError(f"{description} failed: {result['errors']!s}")
        return result

from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from models.fwconfig_normalized import FwConfigNormalized

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "importer"))

from fwo_base import init_service_provider
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from models.rulebase import Rulebase
from test.mocking.mock_import_state import MockImportStateController


class MockFwConfigImportRule(FwConfigImportRule):
    """
    A mock subclass of FwConfigImportRule for testing.

    This class allows selective stubbing of internal methods to control behavior during tests.
    """

    def __init__(self):
        """
        Initializes the mock import rule controller with stubs enabled by default.
        """
        service_provider = init_service_provider()
        self._import_details = MockImportStateController(stub_setCoreData=True)
        service_provider.get_global_state().import_state = self._import_details

        self._stub_markRulesRemoved = True
        self._stub_getRules = False
        self._stub_addNewRuleMetadata = True
        self._stub_addNewRules = True
        self._stub_moveRules = True
        self._stub_create_new_rule_version = True
        self._stub_add_new_refs = True
        self._stub_remove_outdated_refs = True
        self._stub_write_changelog_rules = True

        super().__init__()

    # Properties to control stubbing behavior

    @property
    def import_details(self) -> MockImportStateController:
        """
        Returns the mock import state controller.
        """
        return self._import_details

    @import_details.setter
    def import_details(self, value: MockImportStateController):
        self._import_details = value

    @property
    def stub_markRulesRemoved(self) -> bool:
        """
        Indicates whether to stub markRulesRemoved.
        """
        return self._stub_markRulesRemoved

    @stub_markRulesRemoved.setter
    def stub_markRulesRemoved(self, value: bool):
        self._stub_markRulesRemoved = value

    @property
    def stub_getRules(self) -> bool:
        """
        Indicates whether to stub getRules.
        """
        return self._stub_getRules

    @stub_getRules.setter
    def stub_getRules(self, value: bool):
        self._stub_getRules = value

    @property
    def stub_addNewRuleMetadata(self) -> bool:
        """
        Indicates whether to stub addNewRuleMetadata.
        """
        return self._stub_addNewRuleMetadata

    @stub_addNewRuleMetadata.setter
    def stub_addNewRuleMetadata(self, value: bool):
        self._stub_addNewRuleMetadata = value

    @property
    def stub_addNewRules(self) -> bool:
        """
        Indicates whether to stub addNewRules.
        """
        return self._stub_addNewRules

    @stub_addNewRules.setter
    def stub_addNewRules(self, value: bool):
        self._stub_addNewRules = value

    @property
    def stub_moveRules(self) -> bool:
        """
        Indicates whether to stub moveRules.
        """
        return self._stub_moveRules

    @stub_moveRules.setter
    def stub_moveRules(self, value: bool):
        self._stub_moveRules = value

    @property
    def stub_create_new_rule_version(self) -> bool:
        """
        Indicates whether to stub create_new_rule_version.
        """
        return self._stub_create_new_rule_version

    @stub_create_new_rule_version.setter
    def stub_create_new_rule_version(self, value: bool):
        self._stub_create_new_rule_version = value

    @property
    def stub_add_new_refs(self) -> bool:
        """
        Indicates whether to stub add_new_refs.
        """
        return self._stub_add_new_refs

    @stub_add_new_refs.setter
    def stub_add_new_refs(self, value: bool):
        self._stub_add_new_refs = value

    @property
    def stub_remove_outdated_refs(self) -> bool:
        """
        Indicates whether to stub remove_outdated_refs.
        """
        return self._stub_remove_outdated_refs

    @stub_remove_outdated_refs.setter
    def stub_remove_outdated_refs(self, value: bool):
        self._stub_remove_outdated_refs = value

    @property
    def stub_write_changelog_rules(self) -> bool:
        """
        Indicates whether to stub write_changelog_rules.
        """
        return self._stub_write_changelog_rules

    @stub_write_changelog_rules.setter
    def stub_write_changelog_rules(self, value: bool):
        self._stub_write_changelog_rules = value

    # Overridden methods with stubbing behavior

    def mark_rules_removed(self, removedRuleUids: dict[str, list[str]]) -> tuple[int, list[int]]:
        """
        Simulates marking rules as removed. Can delegate to the base class if not stubbed.

        Args:
            removedRuleUids (dict): A dict mapping rulebase identifiers to lists of rule UIDs.
            changedRuleUids (dict): A dict mapping rulebase identifiers to lists of rule UIDs.

        Returns:
            tuple: (changes, collectedRemovedRuleIds)

        """
        changes = 0
        collectedRemovedRuleIds = []
        for rulebase in removedRuleUids:
            changes += len(removedRuleUids[rulebase])
            collectedRemovedRuleIds.extend(removedRuleUids[rulebase])

        if not self.stub_markRulesRemoved:
            changes, collectedRemovedRuleIds = super().mark_rules_removed(removedRuleUids)

        return changes, collectedRemovedRuleIds

    def get_rules(self, ruleUids: list[str]) -> list[Rulebase]:
        """
        Simulates returning rules by UID. Delegates to base if not stubbed.

        Args:
            ruleUids (list): list of rule UIDs to fetch.

        Returns:
            list: list of Rulebase instances containing the rules.

        """
        rulebases: list[Rulebase] = []

        if not self.stub_getRules:
            rulebases = super().get_rules(ruleUids)

        return rulebases

    def add_new_rule_metadata(self, newRules: list[Rulebase]) -> tuple[int, list[int]]:
        """
        Simulates adding metadata for new rules. Delegates to base if not stubbed.

        Args:
            newRules (list): list of Rulebase objects with new rules.

        Returns:
            tuple: (changes, newRuleIds)

        """
        changes = 0
        newRuleIds: list[int] = []

        if not self.stub_addNewRuleMetadata:
            changes, newRuleIds = super().add_new_rule_metadata(newRules)

        return changes, newRuleIds

    def add_new_rules(self, rulebases: list[Rulebase]) -> tuple[int, list[dict]]:
        """
        Simulates adding new rules to db and returning their ids. Delegates to base if not stubbed.

        Args:
            newRules (list): list of Rulebase objects with new rules.

        Returns:
            tuple: (changes, newRuleIds)

        """
        changes = 0
        newRuleIds = []

        for rulebase in rulebases:
            for rule in list(rulebase.rules.values()):
                changes += 1
                newRuleIds.append({"rule_uid": rule.rule_uid, "rule_id": changes})

        if not self.stub_addNewRuleMetadata:
            changes, newRuleIds = super().add_new_rules(rulebases)

        return changes, newRuleIds

    def moveRules(self, moved_rule_uids: dict[str, list[str]]) -> tuple[int, list[int]]:
        """
        Simulates moving rules to a new location.  Delegates to base if not stubbed.

        Args:
            moved_rule_uids (dict): Mapping from rulebase to list of rule UIDs to move.

        Returns:
            tuple: (changes, moved_rule_ids)

        """
        changes = 0
        moved_rule_ids: list[int] = []

        for rulebase in moved_rule_uids:
            for _ in moved_rule_uids[rulebase]:
                changes += 1
                moved_rule_ids.append(changes)

        if not self.stub_moveRules:
            changes, moved_rule_ids = super().moveRules(moved_rule_uids)

        return changes, moved_rule_ids

    def create_new_rule_version(self, rule_uids):
        """
        Simulates creating a new version of a rule. Delegates to base if not stubbed.

        Args:
            rule_uid (str): The UID of the rule to version.

        """
        changes = 0
        collected_rule_ids: list[int] = []
        insert_rules_return: list[dict] = []

        if not self.stub_create_new_rule_version:
            changes, collected_rule_ids, insert_rules_return = super().create_new_rule_version(rule_uids)
        else:
            for rulebase_rule_uids in rule_uids.values():
                changes += len(rulebase_rule_uids)
                collected_rule_ids = list(range(1, len(rulebase_rule_uids) + 1))
                for counter in range(len(rulebase_rule_uids)):
                    insert_rule_return = dict()
                    insert_rule_return["rule_uid"] = rulebase_rule_uids[counter]
                    insert_rule_return["rule_id"] = changes + counter + 1
                    insert_rules_return.append(insert_rule_return)

        return changes, collected_rule_ids, insert_rules_return

    def add_new_refs(self, prev_config: FwConfigNormalized) -> int:
        """
        Simulates adding new references for rules. Delegates to base if not stubbed.

        Args:
            rule_uids (list): List of rule UIDs to add references for.

        Returns:
            int: (changes)

        """
        changes = 0

        if not self.stub_add_new_refs:
            changes = super().add_new_refs(prev_config)

        return changes

    def remove_outdated_refs(self, prev_config: FwConfigNormalized) -> int:
        """
        Simulates removing outdated references for rules. Delegates to base if not stubbed.

        Args:
            prev_config (FwConfigNormalized): The previous configuration.

        Returns:
            int: (changes)

        """
        changes = 0

        if not self.stub_remove_outdated_refs:
            changes = super().remove_outdated_refs(prev_config)

        return changes

    def write_changelog_rules(self, added_rules_ids, removed_rules_ids):
        """
        Simulates writing a changelog entry for rules. Delegates to base if not stubbed.

        Args:
            added_rules_ids (list): List of added rule IDs.
            removed_rules_ids (list): List of removed rule IDs.

        """
        errors = 0

        if not self.stub_write_changelog_rules:
            errors = super().write_changelog_rules(added_rules_ids, removed_rules_ids)

        return errors

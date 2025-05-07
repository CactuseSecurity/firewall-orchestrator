from typing import List, Dict, Tuple, Any

from importer.model_controllers.fwconfig_import_rule import FwConfigImportRule
from importer.models.rulebase import Rulebase
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

        self._import_details = MockImportStateController()
        self._stub_markRulesRemoved = True
        self._stub_getRules = False
        self._stub_addNewRuleMetadata = True
        self._stub_addNewRules = True
        self._stub_moveRules = True


    @property
    def ImportDetails(self) -> MockImportStateController:
        """
            Returns the mock import state controller.
        """

        return self._import_details

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


    def markRulesRemoved(self, removedRuleUids: Dict[str, List[str]]) -> Tuple[int, int, Dict[str, List[str]]]:
        """
            Simulates marking rules as removed. Can delegate to the base class if not stubbed.

            Args:
                removedRuleUids (dict): A dict mapping rulebase identifiers to lists of rule UIDs.

            Returns:
                tuple: (errors, changes, collectedRemovedRuleIds)
        """
        
        errors = 0
        changes = 0
        for rulebase in removedRuleUids.keys():
            changes += len(removedRuleUids[rulebase])
        collectedRemovedRuleIds = removedRuleUids

        if not self.stub_markRulesRemoved:
            errors, changes, collectedRemovedRuleIds = super().markRulesRemoved(removedRuleUids)

        return errors, changes, collectedRemovedRuleIds


    def getRules(self, ruleUids: List[str]) -> List[Rulebase]:
        """
            Simulates returning rules by UID. Delegates to base if not stubbed.

            Args:
                ruleUids (list): List of rule UIDs to fetch.

            Returns:
                list: List of Rulebase instances containing the rules.
        """

        rulebases: List[Rulebase] = []

        if not self.stub_getRules:
            rulebases = super().getRules(ruleUids)

        return rulebases


    def addNewRuleMetadata(self, newRules: List[Rulebase]) -> Tuple[int, int, List[int]]:
        """
            Simulates adding metadata for new rules. Delegates to base if not stubbed.

            Args:
                newRules (list): List of Rulebase objects with new rules.

            Returns:
                tuple: (errors, changes, newRuleIds)
        """

        errors = 0
        changes = 0
        newRuleIds: List[int] = []

        if not self.stub_addNewRuleMetadata:
            errors, changes, newRuleIds = super().addNewRuleMetadata(newRules)

        return errors, changes, newRuleIds


    def addNewRules(self, newRules: List[Rulebase]) -> Tuple[int, int, List[int]]:
        """
            Simulates adding new rules to db and returning their ids. Delegates to base if not stubbed.

            Args:
                newRules (list): List of Rulebase objects with new rules.

            Returns:
                tuple: (errors, changes, newRuleIds)
        """

        errors = 0
        changes = 0
        newRuleIds: List[int] = []

        for rulebase in newRules:
            for rule in rulebase.Rules:
                changes += 1
                newRuleIds.append(changes)

        if not self.stub_addNewRuleMetadata:
            errors, changes, newRuleIds = super().addNewRules(newRules)

        return errors, changes, newRuleIds


    def moveRules(self, moved_rule_uids: Dict[str, List[str]]) -> Tuple[int, int, List[int]]:
        """
            Simulates moving rules to a new location.  Delegates to base if not stubbed.

            Args:
                moved_rule_uids (dict): Mapping from rulebase to list of rule UIDs to move.
            Returns:
                tuple: (errors, changes, moved_rule_ids)
        """

        errors = 0
        changes = 0
        moved_rule_ids: List[int] = []

        for rulebase in moved_rule_uids.keys():
            for _ in moved_rule_uids[rulebase]:
                changes += 1
                moved_rule_ids.append(changes)

        if not self.stub_moveRules:
            errors, changes, moved_rule_ids = super().moveRules(moved_rule_uids)

        return errors, changes, moved_rule_ids


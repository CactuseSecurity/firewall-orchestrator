from importer.model_controllers.fwconfig_import_rule import FwConfigImportRule
from test.mocking.mock_import_state import MockImportStateController

class MockFwConfigImportRule(FwConfigImportRule):
        """
            A class for testing FwConfigImportRule, while stubbing internal methods as configured.
        """
        def __init__(self):
            self._import_details = MockImportStateController()
            self._stub_markRulesRemoved = True

        @property
        def ImportDetails(self) -> MockImportStateController:
              return self._import_details
        
        @property
        def stub_markRulesRemoved(self) -> bool:
              return self._stub_markRulesRemoved
        
        @stub_markRulesRemoved.setter
        def stub_markRulesRemoved(self, value):
            self._stub_markRulesRemoved = value

        def markRulesRemoved(self, removedRuleUids):
            errors = 0
            changes = 0
            for rulebase in removedRuleUids.keys():
                changes += len(removedRuleUids[rulebase])
            collectedRemovedRuleIds = removedRuleUids

            if not self.stub_markRulesRemoved:
                errors, changes, collectedRemovedRuleIds = super().markRulesRemoved(removedRuleUids)

            return errors, changes, collectedRemovedRuleIds
            


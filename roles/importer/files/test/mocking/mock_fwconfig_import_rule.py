from typing import List

from importer.model_controllers.fwconfig_import_rule import FwConfigImportRule
from importer.models.rulebase import Rulebase
from test.mocking.mock_import_state import MockImportStateController

class MockFwConfigImportRule(FwConfigImportRule):
        """
            A class for testing FwConfigImportRule, while stubbing internal methods as configured.
        """
        
        def __init__(self):
            self._import_details = MockImportStateController()
            self._stub_markRulesRemoved = True
            self._stub_getRules = False
            self._stub_addNewRuleMetadata = True
            self._stub_addNewRules = True
            self._stub_moveRules = True

        @property
        def ImportDetails(self) -> MockImportStateController:
              return self._import_details
        
        @property
        def stub_markRulesRemoved(self) -> bool:
              return self._stub_markRulesRemoved
        
        @stub_markRulesRemoved.setter
        def stub_markRulesRemoved(self, value):
            self._stub_markRulesRemoved = value

        @property
        def stub_getRules(self) -> bool:
              return self._stub_getRules
        
        @stub_getRules.setter
        def stub_getRules(self, value):
            self._stub_getRules = value

        @property
        def stub_addNewRuleMetadata(self) -> bool:
              return self._stub_addNewRuleMetadata
        
        @stub_addNewRuleMetadata.setter
        def stub_addNewRuleMetadata(self, value):
            self._stub_addNewRuleMetadata = value

        @property
        def stub_addNewRules(self) -> bool:
              return self._stub_addNewRules
        
        @stub_addNewRules.setter
        def stub_addNewRules(self, value):
            self._stub_addNewRules = value

        @property
        def stub_moveRules(self) -> bool:
              return self._stub_moveRules
        
        @stub_moveRules.setter
        def stub_moveRules(self, value):
            self._stub_moveRules = value

        def markRulesRemoved(self, removedRuleUids):
            errors = 0
            changes = 0
            for rulebase in removedRuleUids.keys():
                changes += len(removedRuleUids[rulebase])
            collectedRemovedRuleIds = removedRuleUids

            if not self.stub_markRulesRemoved:
                errors, changes, collectedRemovedRuleIds = super().markRulesRemoved(removedRuleUids)

            return errors, changes, collectedRemovedRuleIds
        

        def getRules(self, ruleUids):
            rulebases = []

            if not self.stub_getRules:
                rulebases = super().getRules(ruleUids)

            return rulebases
        

        def addNewRuleMetadata(self, newRules: List[Rulebase]):
            errors = 0
            changes = 0
            newRuleIds = []

            if not self.stub_addNewRuleMetadata:
                errors, changes, newRuleIds = super().addNewRuleMetadata(newRules)

            return errors, changes, newRuleIds
        

        def addNewRules(self, newRules: List[Rulebase]):
            errors = 0
            changes = 0
            newRuleIds = []
            
            for rulebase in newRules:
                for rule in rulebase.Rules:
                    changes += 1
                    newRuleIds.append(changes) # just random incremental id for now
            
            if not self.stub_addNewRuleMetadata:
                errors, changes, newRuleIds = super().addNewRules(newRules)

            return errors, changes, newRuleIds
        
        def moveRules(self, movedRuleUids, target_rule_uids):
            errors = 0
            changes = 0
            movedRuleIds = []

            for rulebase in movedRuleUids.keys():
                for rule in movedRuleUids[rulebase]:
                    changes += 1
                    movedRuleIds.append(changes) # just random incremental id for now

            if not self.stub_moveRules:
                errors, changes, movedRuleIds = super().moveRules(movedRuleUids, target_rule_uids)

            return errors, changes, movedRuleIds


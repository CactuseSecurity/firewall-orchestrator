# from pydantic import BaseModel
from typing import List
from models.import_state import ImportState
# from models.rule_enforced_on_gateway import RuleEnforcedOnGateway

class RuleEnforcedOnGatewayController():
    def __init__(self, importState: ImportState):
        self.ImportDetails: ImportState = importState

    # this should not be confused with the rulebase_link model - it refers to the check point "install on" feature
    def insertRulesEnforcedOnGateway(self, enforcements: List[dict]):

        if len(enforcements) > 0:
            # queryVariables = { "rulesEnforcedOnGateway": { "data": enforcements } }
            queryVariables = { "rulesEnforcedOnGateway": enforcements }
            
            mutation = """
                mutation importInsertRulesEnforcedOnGateway($rulesEnforcedOnGateway: [rule_enforced_on_gateway_insert_input!]!) {
                    insert_rule_enforced_on_gateway(objects: $rulesEnforcedOnGateway) {
                        affected_rows
                    }
                }"""
            
            return self.ImportDetails.call(mutation, queryVariables=queryVariables)

    # TODO: also handled changes and deletions

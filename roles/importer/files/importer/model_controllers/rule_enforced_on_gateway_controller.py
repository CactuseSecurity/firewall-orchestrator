# from pydantic import BaseModel
from models.rule_enforced_on_gateway import RuleEnforcedOnGateway

class RuleEnforcedOnGatewayController(RuleEnforcedOnGateway):

    # this should not be confused with the rulebase_link model - it refers to the check point "install on" feature
    def insertRulesEnforcedOnGateway(self, ruleIds, devId):
        rulesEnforcedOnGateway = []
        for ruleId in ruleIds:
            rulesEnforcedOnGateway.append({
                "rule_id": ruleId,
                "dev_id": devId,
                "created": self.ImportDetails.ImportId
            })

        query_variables = { "ruleEnforcedOnGateway": rulesEnforcedOnGateway }
        
        mutation = """
            mutation importInsertRulesEnforcedOnGateway($rulesEnforcedOnGateway: [rule_enforced_on_gateway_insert_input!]!) {
                insert_rule_enforced_on_gateway(objects: $rulesEnforcedOnGateway) {
                    affected_rows
                }
            }"""
        
        return self.ImportDetails.call(mutation, queryVariables=query_variables)


    # TODO: also handled changes and deletions

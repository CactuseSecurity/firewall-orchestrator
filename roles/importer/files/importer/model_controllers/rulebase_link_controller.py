# from pydantic import BaseModel
from models.rulebase_link import RulebaseLink
# from model.Rule import Rule
from fwoBaseImport import ImportState

class RulebaseLinkController(RulebaseLink):

    # def __init__(self):
    #     super().__init__(self)

    def importInsertRulebaseLink(self, importState: ImportState):
        query_variables = { "rule_link": [
            {
                "dev_id": self.gw_id,
                # "from_rulebase_id": rblink.from_rule_id,
                "to_rulebase_id": self.to_rulebase_id,
                "from_rule_id": self.from_rule_id,
                "link_type": self.link_type
            }
        ] }

        mutation = """
            mutation importInsertRulebaseOnGateway($rulebaseLinks: [rulebase_link_insert_input!]!) {
                insert_rulebase_link(objects: $rulebaseLinks) {
                    affected_rows
                }
            }"""
        
        # return self.ImportDetails.call(mutation, queryVariables=query_variables)
        return importState.call(mutation, queryVariables=query_variables)

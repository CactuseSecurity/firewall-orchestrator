# from pydantic import BaseModel
from models.rulebase_link import RulebaseLink
from fwoBaseImport import ImportState
from fwo_log import getFwoLogger

class RulebaseLinkController(RulebaseLink):

    # def __init__(self, importState: ImportState, config: RulebaseLink):
    #     super().__init__(gw_id=gwId, 
    #                                      from_rule_id=fromRuleId,
    #                                      to_rulebase_id=toRulebaseId,
    #                                      link_type=linkTypeId,
    #                                      created=self.ImportDetails.ImportId

    def importInsertRulebaseLink(self, importState: ImportState):
        errors = 0
        changes = 0
        logger = getFwoLogger()
        query_variables = { "rulebaseLinks": [
            {
                "gw_id": self.gw_id,
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
        addResult = importState.call(mutation, queryVariables=query_variables)
        if 'errors' in addResult:
            errors = 1
            logger.exception(f"fwo_api:removeRules - error while removing rules: {str(addResult['errors'])}")
        else:
            changes = addResult['data']['insert_rulebase_link']['affected_rows']
        return errors, changes

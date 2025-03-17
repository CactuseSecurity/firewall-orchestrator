# from pydantic import BaseModel
from models.rulebase_link import RulebaseLink
from model_controllers.import_state_controller import ImportStateController
from fwo_log import getFwoLogger
from models.import_state import ImportState
from model_controllers.import_statistics_controller import ImportStatisticsController

class RulebaseLinkController(RulebaseLink):

    def importInsertRulebaseLink(self, importState: ImportStateController):
        errors = 0
        logger = getFwoLogger()
        query_variables = { "rulebaseLinks": [
            {
                "gw_id": self.gw_id,
                "to_rulebase_id": self.to_rulebase_id,
                "from_rule_id": self.from_rule_id,
                "link_type": self.link_type,
                "created": importState.ImportId
            }
        ] }

        # TODO: add on_conflict do nothing to the mutation
        mutation = """
            mutation importInsertRulebaseOnGateway($rulebaseLinks: [rulebase_link_insert_input!]!) {
                insert_rulebase_link(objects: $rulebaseLinks) {
                    affected_rows
                }
            }"""
        
        # return self.ImportDetails.call(mutation, queryVariables=query_variables)
        addResult = importState.call(mutation, queryVariables=query_variables)
        if 'errors' in addResult:
            importState.Stats.addError(f"fwo_api:removeRules - error while removing rules: {str(addResult['errors'])}")
            logger.exception(f"fwo_api:removeRules - error while removing rules: {str(addResult['errors'])}")
        else:
            changes = addResult['data']['insert_rulebase_link']['affected_rows']
            importState.Stats.RuleChangeCount += changes    # TODO: move this to separate category?!
        return

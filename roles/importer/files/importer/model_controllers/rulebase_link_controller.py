# from pydantic import BaseModel
from typing import List
from models.rulebase_link import RulebaseLink
from model_controllers.import_state_controller import ImportStateController
from fwo_log import getFwoLogger
from models.import_state import ImportState
from model_controllers.import_statistics_controller import ImportStatisticsController

class RulebaseLinkController():

    RulbaseToGatewayMap: dict = {}

    # def importInsertRulebaseLink(self, importState: ImportStateController):
    #     errors = 0
    #     logger = getFwoLogger()
    #     query_variables = { "rulebaseLinks": [
    #         {
    #             "gw_id": self.gw_id,
    #             "to_rulebase_id": self.to_rulebase_id,
    #             "from_rule_id": self.from_rule_id,
    #             "link_type": self.link_type,
    #             "created": importState.ImportId
    #         }
    #     ] }

    #     # TODO: add on_conflict do nothing to the mutation
    #     mutation = """
    #         mutation importInsertRulebaseOnGateway($rulebaseLinks: [rulebase_link_insert_input!]!) {
    #             insert_rulebase_link(objects: $rulebaseLinks) {
    #                 affected_rows
    #             }
    #         }"""
        
    #     # return self.ImportDetails.call(mutation, queryVariables=query_variables)
    #     addResult = importState.call(mutation, queryVariables=query_variables)
    #     if 'errors' in addResult:
    #         importState.Stats.addError(f"fwo_api:insertRulebaseLinks - error while inserting: {str(addResult['errors'])}")
    #         logger.exception(f"fwo_api:insertRulebaseLinks - error while inserting: {str(addResult['errors'])}")
    #     else:
    #         changes = addResult['data']['insert_rulebase_link']['affected_rows']
    #         importState.Stats.RuleChangeCount += changes    # TODO: move this to separate category?!
    #     return

    def importInsertRulebaseLinks(self, importState: ImportStateController, rbLinks: List[RulebaseLink]):
        errors = 0
        logger = getFwoLogger()
        query_variables = { "rulebaseLinks": rbLinks }

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
            importState.Stats.addError(f"fwo_api:insertRulebaseLinks - error while inserting: {str(addResult['errors'])}")
            logger.exception(f"fwo_api:insertRulebaseLinks - error while inserting: {str(addResult['errors'])}")
        else:
            changes = addResult['data']['insert_rulebase_link']['affected_rows']
            importState.Stats.RuleChangeCount += changes    # TODO: move this to separate category?!
        return


    def getRulebaseLinks(self, importState: ImportStateController, gwIds: List[int] = []):
        logger = getFwoLogger()
        if len(gwIds) == 0:
            # if no gwIds are provided, get all rulebase links
            queryVariables = {}
        else:
            # if gwIds are provided, use them to filter the rulebase links  
            queryVariables = { "gwIds": gwIds}

        query = """
            query getRulebaseLinks($gwIds: [Int!]) {
                rulebase_link (where: {removed:{_is_null:true}, gw_id: {_in: $gwIds } }) {
                    gw_id
                    to_rulebase_id
                    from_rule_id
                    link_type
                }
            }"""
        
        links = importState.call(query, queryVariables=queryVariables)
        if 'errors' in links:
            importState.Stats.addError(f"fwo_api:getRulebaseLinks - error while getting rulebaseLinks: {str(links['errors'])}")
            logger.exception(f"fwo_api:getRulebaseLinks - error while getting rulebaseLinks: {str(links['errors'])}")
        else:
            rbLinks = links['data']['rulebase_link']
        return rbLinks

    # add an entry for all rulebase to gateway pairs that are conained in the rulebase_links table
    def SetMapOfAllEnforcingGatewayIdsForRulebaseId(self, importState: ImportStateController):
        rbLinks = self.getRulebaseLinks(importState)

        for link in rbLinks:
            rulebaseId = link['to_rulebase_id']
            gwId = link['gw_id']
            if rulebaseId not in self.RulbaseToGatewayMap:
                self.RulbaseToGatewayMap.update({rulebaseId: []})
            if gwId not in self.RulbaseToGatewayMap[rulebaseId]:
                self.RulbaseToGatewayMap[rulebaseId].append(gwId)
        
    def GetGwIdsForRulebaseId(self, rulebaseId):
        return self.RulbaseToGatewayMap.get(rulebaseId, [])
    
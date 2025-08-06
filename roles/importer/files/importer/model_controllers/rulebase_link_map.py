# from pydantic import BaseModel
from typing import List
from models.rulebase_link import RulebaseLink
from model_controllers.import_state_controller import ImportStateController
from fwo_log import getFwoLogger
from models.import_state import ImportState
from model_controllers.import_statistics_controller import ImportStatisticsController

class RulebaseLinkMap():


    def getRulebaseLinks(self, importState: ImportStateController, gwIds: List[int] = []):
        logger = getFwoLogger()
        query_variables = { "gwIds": gwIds}

        query = """
            query getRulebaseLinks($gwIds: [Int!]) {
                rulebase_link (where: {removed:{_is_null:true}, gw_id: {_in: $gwIds } }) {
                    gw_id
                    to_rulebase_id
                    from_rule_id
                    link_type
                }
            }"""
        
        links = importState.api_call.call(query, query_variables=query_variables)
        if 'errors' in links:
            importState.Stats.addError(f"fwo_api:getRulebaseLinks - error while getting rulebaseLinks: {str(links['errors'])}")
            logger.exception(f"fwo_api:getRulebaseLinks - error while getting rulebaseLinks: {str(links['errors'])}")
        else:
            rbLinks = links['data']['rulebase_link']
        return rbLinks


    def SetMapOfAllEnforcingGatewayIdsForRulebaseId(self):
        rbLinks = self.getRulebaseLinks()

        for link in rbLinks:
            rulebaseId = link['to_rulebase_id']
            gwId = link['gw_id']
            if rulebaseId not in self.RulebaseMap:
                self.RulebaseMap.update({rulebaseId: []})
            if gwId not in self.RulebaseMap[rulebaseId]:
                self.RulebaseMap[rulebaseId].append(gwId)
        
        # for rulebaseId in self.RulebaseMap.values():
        #     self.RulbaseToGatewayMap.update({rulebaseId: []})
        #     # TODO: implement

    def GetGwIdsForRulebaseId(self, rulebaseId):
        return self.RulbaseToGatewayMap.get(rulebaseId, [])
    
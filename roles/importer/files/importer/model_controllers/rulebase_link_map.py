from fwo_log import getFwoLogger
from models.rulebase_link import RulebaseLink
from model_controllers.import_state_controller import ImportStateController
from models.import_state import ImportState
from model_controllers.import_statistics_controller import ImportStatisticsController
from fwo_api_call import FwoApiCall

class RulebaseLinkMap():


    def getRulebaseLinks(self, importState: ImportStateController, gwIds: list[int] = []):
        logger = getFwoLogger()
        query_variables = { "gwIds": gwIds}
        rbLinks = []

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
            return rbLinks

        rbLinks = links['data']['rulebase_link']
        return rbLinks
    
    
    # TODO: implement SetMapOfAllEnforcingGatewayIdsForRulebaseId

    def GetGwIdsForRulebaseId(self, rulebaseId, importState: ImportStateController):
        return importState.RulbaseToGatewayMap.get(rulebaseId, [])
    
from typing import Any
from fwo_log import get_fwo_logger
from model_controllers.import_state_controller import ImportStateController

class RulebaseLinkMap():


    def getRulebaseLinks(self, importState: ImportStateController, gwIds: list[int] = []) -> list[dict[str, Any]]:
        logger = get_fwo_logger()
        query_variables = { "gwIds": gwIds}
        rbLinks: list[dict[str, Any]] = []

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

    def GetGwIdsForRulebaseId(self, rulebaseId: int, importState: ImportStateController) -> list[int]:
        return importState.RulbaseToGatewayMap.get(rulebaseId, [])
    
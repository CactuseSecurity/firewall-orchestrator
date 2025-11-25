from typing import Any
from fwo_log import FWOLogger
from model_controllers.import_state_controller import ImportStateController

class RulebaseLinkMap():


    def get_rulebase_links(self, import_state: ImportStateController, gw_ids: list[int] = []) -> list[dict[str, Any]]:
        query_variables = { "gwIds": gw_ids}
        rb_links: list[dict[str, Any]] = []

        query = """
            query getRulebaseLinks($gwIds: [Int!]) {
                rulebase_link (where: {removed:{_is_null:true}, gw_id: {_in: $gwIds } }) {
                    gw_id
                    to_rulebase_id
                    from_rule_id
                    link_type
                }
            }"""
        
        links = import_state.api_call.call(query, query_variables=query_variables)
        if 'errors' in links:
            FWOLogger.exception(f"fwo_api:getRulebaseLinks - error while getting rulebaseLinks: {str(links['errors'])}")
            return rb_links

        rb_links = links['data']['rulebase_link']
        return rb_links
    
    
    # TODO: implement SetMapOfAllEnforcingGatewayIdsForRulebaseId

    def get_gw_ids_for_rulebase_id(self, rulebase_id: int, import_state: ImportStateController) -> list[int]:
        return import_state.RulbaseToGatewayMap.get(rulebase_id, [])
    
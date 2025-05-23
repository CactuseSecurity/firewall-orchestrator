# from pydantic import BaseModel
from typing import List
from models.rulebase_link import RulebaseLink
from model_controllers.import_state_controller import ImportStateController
from fwo_log import getFwoLogger
import fwo_const
from fwo_api import get_graphql_code

class RulebaseLinkController():

    rulbase_to_gateway_map: dict = {}

    def insert_rulebase_links(self, import_state: ImportStateController, rb_links: List[RulebaseLink]):
        logger = getFwoLogger()
        query_variables = { "rulebaseLinks": rb_links }
        mutation = get_graphql_code([f"{fwo_const.graphqlQueryPath}rule/insertRulebaseLinks.graphql"])      
        add_result = import_state.call(mutation, queryVariables=query_variables)
        if 'errors' in add_result:
            import_state.Stats.addError(f"fwo_api:insertRulebaseLinks - error while inserting: {str(add_result['errors'])}")
            logger.exception(f"fwo_api:insertRulebaseLinks - error while inserting: {str(add_result['errors'])}")
        else:
            changes = add_result['data']['insert_rulebase_link']['affected_rows']
            import_state.Stats.rulebase_add_count += changes

    def get_rulebase_links(self, import_state: ImportStateController, gw_ids: List[int] = None):
        logger = getFwoLogger()
        if gw_ids is None:
            gw_ids = []
        if len(gw_ids) == 0:
            # if no gwIds are provided, get all rulebase links
            query_variables = {}
        else:
            # if gwIds are provided, use them to filter the rulebase links  
            query_variables = { "gwIds": gw_ids}

        query = get_graphql_code([f"{fwo_const.graphqlQueryPath}rule/getRulebaseLinks.graphql"])
        links = import_state.call(query, queryVariables=query_variables)
        if 'errors' in links:
            import_state.Stats.addError(f"fwo_api:getRulebaseLinks - error while getting rulebaseLinks: {str(links['errors'])}")
            logger.exception(f"fwo_api:getRulebaseLinks - error while getting rulebaseLinks: {str(links['errors'])}")
        else:
            rb_links = links['data']['rulebase_link']
        return rb_links

    # add an entry for all rulebase to gateway pairs that are conained in the rulebase_links table
    def set_map_of_all_enforcing_gateway_ids_for_rulebase_id(self, importState: ImportStateController):
        rb_links = self.get_rulebase_links(importState)

        for link in rb_links:
            rulebase_id = link['to_rulebase_id']
            gw_id = link['gw_id']
            if rulebase_id not in self.rulbase_to_gateway_map:
                self.rulbase_to_gateway_map.update({rulebase_id: []})
            if gw_id not in self.rulbase_to_gateway_map[rulebase_id]:
                self.rulbase_to_gateway_map[rulebase_id].append(gw_id)
        
    def get_gw_ids_for_rulebase_id(self, rulebase_id):
        return self.rulbase_to_gateway_map.get(rulebase_id, [])
    
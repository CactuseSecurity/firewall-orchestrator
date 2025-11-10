# from pydantic import BaseModel

from typing import Any
from models.rulebase_link import RulebaseLink, parse_rulebase_links
from model_controllers.import_state_controller import ImportStateController
from fwo_log import getFwoLogger
import fwo_const
from fwo_api import FwoApi

class RulebaseLinkController():

    rulbase_to_gateway_map: dict[int, list[int]] = {}
    rb_links: list[RulebaseLink]

    def insert_rulebase_links(self, import_state: ImportStateController, rb_links: list[dict[str, Any]]) -> None:
        logger = getFwoLogger()
        query_variables = { "rulebaseLinks": rb_links }
        if len(rb_links) == 0:
            return
        mutation = FwoApi.get_graphql_code([f"{fwo_const.graphql_query_path}rule/insertRulebaseLinks.graphql"])      
        add_result = import_state.api_call.call(mutation, query_variables=query_variables)
        if 'errors' in add_result:
            import_state.Stats.addError(f"fwo_api:insertRulebaseLinks - error while inserting: {str(add_result['errors'])}")
            logger.exception(f"fwo_api:insertRulebaseLinks - error while inserting: {str(add_result['errors'])}")
        else:
            changes = add_result['data']['insert_rulebase_link']['affected_rows']
            import_state.Stats.rulebase_link_add_count += changes


    def remove_rulebase_links(self, import_state: ImportStateController, removed_rb_links_ids: list[int | None]) -> None:
        logger = getFwoLogger()
        query_variables: dict[str, Any] = { "removedRulebaseLinks": removed_rb_links_ids, "importId": import_state.ImportId }
        if len(removed_rb_links_ids) == 0:
            return
        mutation = FwoApi.get_graphql_code([f"{fwo_const.graphql_query_path}rule/removeRulebaseLinks.graphql"])      
        add_result = import_state.api_call.call(mutation, query_variables=query_variables)
        if 'errors' in add_result:
            import_state.Stats.addError(f"fwo_api:removeRulebaseLinks - error while removing: {str(add_result['errors'])}")
            logger.exception(f"fwo_api:removeRulebaseLinks - error while removing: {str(add_result['errors'])}")
        else:
            changes = add_result['data']['update_rulebase_link']['affected_rows']
            import_state.Stats.rulebase_link_delete_count += changes 


    def get_rulebase_links(self, import_state: ImportStateController):
        logger = getFwoLogger()
        gw_ids = import_state.lookup_all_gateway_ids()
        if len(gw_ids) == 0:
            logger.warning("RulebaseLinkController:get_rulebase_links - no gateway ids found for current management - skipping getting rulebase links")
            self.rb_links = []
            return
        # we always need to provide gwIds since rulebase_links may be duplicate across different gateways
        query_variables = { "gwIds": gw_ids}

        query = FwoApi.get_graphql_code(file_list=[f"{fwo_const.graphql_query_path}rule/getRulebaseLinks.graphql"])
        links = import_state.api_call.call(query, query_variables=query_variables)
        if 'errors' in links:
            import_state.Stats.addError(f"fwo_api:getRulebaseLinks - error while getting rulebaseLinks: {str(links['errors'])}")
            logger.exception(f"fwo_api:getRulebaseLinks - error while getting rulebaseLinks: {str(links['errors'])}")
        else:
            parsable_rulebase_links = [link for link in links['data']['rulebase_link'] if link.get("created") is not None] # TODO: is this necessary or was the bug some corrupted local db stuff? But why does integration test fail?
            self.rb_links: list[RulebaseLink] = parse_rulebase_links(parsable_rulebase_links)


    # add an entry for all rulebase to gateway pairs that are conained in the rulebase_links table
    def set_map_of_all_enforcing_gateway_ids_for_rulebase_id(self, importState: ImportStateController):
        self.get_rulebase_links(importState)

        for link in self.rb_links:
            rulebase_id = link.to_rulebase_id
            gw_id = link.gw_id
            if rulebase_id not in self.rulbase_to_gateway_map:
                self.rulbase_to_gateway_map.update({rulebase_id: []})
            if gw_id not in self.rulbase_to_gateway_map[rulebase_id]:
                self.rulbase_to_gateway_map[rulebase_id].append(gw_id)
        

    def get_gw_ids_for_rulebase_id(self, rulebase_id: int) -> list[int]:
        return self.rulbase_to_gateway_map.get(rulebase_id, [])
    

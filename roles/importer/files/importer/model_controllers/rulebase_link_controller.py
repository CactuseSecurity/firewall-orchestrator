from typing import Any

import fwo_const
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_log import FWOLogger
from model_controllers.import_statistics_controller import ImportStatisticsController
from models.import_state import ImportState
from models.rulebase_link import RulebaseLink, parse_rulebase_links


class RulebaseLinkController:
    rulbase_to_gateway_map: dict[int, list[int]]
    rb_links: list[RulebaseLink]

    def __init__(self) -> None:
        self.rulbase_to_gateway_map = {}

    def insert_rulebase_links(
        self, fwo_api_call: FwoApiCall, stats: ImportStatisticsController, rb_links: list[dict[str, Any]]
    ) -> None:
        query_variables = {"rulebaseLinks": rb_links}
        if len(rb_links) == 0:
            return
        mutation = FwoApi.get_graphql_code([f"{fwo_const.GRAPHQL_QUERY_PATH}rule/insertRulebaseLinks.graphql"])
        add_result = fwo_api_call.call(mutation, query_variables=query_variables)
        if "errors" in add_result:
            FWOLogger.exception(f"fwo_api:insertRulebaseLinks - error while inserting: {add_result['errors']!s}")
        else:
            changes = add_result["data"]["insert_rulebase_link"]["affected_rows"]
            stats.increment_rulebase_link_add_count(changes)

    def remove_rulebase_links(
        self,
        fwo_api_call: FwoApiCall,
        stats: ImportStatisticsController,
        import_id: int,
        removed_rb_links_ids: list[int | None],
    ) -> None:
        query_variables: dict[str, Any] = {"removedRulebaseLinks": removed_rb_links_ids, "importId": import_id}
        if len(removed_rb_links_ids) == 0:
            return
        mutation = FwoApi.get_graphql_code([f"{fwo_const.GRAPHQL_QUERY_PATH}rule/removeRulebaseLinks.graphql"])
        add_result = fwo_api_call.call(mutation, query_variables=query_variables)
        if "errors" in add_result:
            FWOLogger.exception(f"fwo_api:removeRulebaseLinks - error while removing: {add_result['errors']!s}")
        else:
            changes = add_result["data"]["update_rulebase_link"]["affected_rows"]
            stats.increment_rulebase_link_delete_count(changes)

    def get_rulebase_links(self, import_state: ImportState, fwo_api_call: FwoApiCall) -> None:
        gw_ids = import_state.lookup_all_gateway_ids()
        if len(gw_ids) == 0:
            FWOLogger.warning(
                "RulebaseLinkController:get_rulebase_links - no gateway ids found for current management - skipping getting rulebase links"
            )
            self.rb_links = []
            return
        # we always need to provide gwIds since rulebase_links may be duplicate across different gateways
        query_variables = {"gwIds": gw_ids}

        query = FwoApi.get_graphql_code(file_list=[f"{fwo_const.GRAPHQL_QUERY_PATH}rule/getRulebaseLinks.graphql"])
        links = fwo_api_call.call(query, query_variables=query_variables)
        if "errors" in links:
            FWOLogger.exception(f"fwo_api:getRulebaseLinks - error while getting rulebaseLinks: {links['errors']!s}")
        else:
            parsable_rulebase_links = [
                link for link in links["data"]["rulebase_link"] if link.get("created") is not None
            ]  # TODO: is this necessary or was the bug some corrupted local db stuff? But why does integration test fail?
            self.rb_links: list[RulebaseLink] = parse_rulebase_links(parsable_rulebase_links)

    # add an entry for all rulebase to gateway pairs that are conained in the rulebase_links table
    def set_map_of_all_enforcing_gateway_ids_for_rulebase_id(self, import_state: ImportState, fwo_api_call: FwoApiCall):
        self.get_rulebase_links(import_state, fwo_api_call)

        for link in self.rb_links:
            rulebase_id = link.to_rulebase_id
            gw_id = link.gw_id
            if rulebase_id not in self.rulbase_to_gateway_map:
                self.rulbase_to_gateway_map.update({rulebase_id: []})
            if gw_id not in self.rulbase_to_gateway_map[rulebase_id]:
                self.rulbase_to_gateway_map[rulebase_id].append(gw_id)

    def get_gw_ids_for_rulebase_id(self, rulebase_id: int | None) -> list[int]:
        if rulebase_id is None:
            return []
        return self.rulbase_to_gateway_map.get(rulebase_id, [])

from traceback import format_exc
from typing import Any

import fwo_const
from fwo_api_call import FwoApiCall
from fwo_log import FWOLogger
from model_controllers.import_statistics_controller import ImportStatisticsController
from model_controllers.rulebase_link_controller import RulebaseLinkController
from models.import_state import ImportState


class RuleEnforcedOnGatewayController:
    def add_new_rule_enforced_on_gateway_refs(
        self,
        new_rules: list[dict[str, Any]],
        import_state: ImportState,
        fwo_api_call: FwoApiCall,
        statistics_controller: ImportStatisticsController,
    ):
        """
        Main function to add new rule-to-gateway references.
        """
        # Step 1: Initialize the RulebaseLinkController
        rb_link_controller = self.initialize_rulebase_link_controller(import_state, fwo_api_call)

        # Step 2: Prepare rule-to-gateway references
        rule_to_gw_refs = self.prepare_rule_to_gateway_references(import_state, new_rules, rb_link_controller)

        # Step 3: Check if there are any references to insert
        if not rule_to_gw_refs:
            FWOLogger.info("No rules to be enforced on gateways.")
            return

        # Step 4: Insert the references into the database
        self.insert_rule_to_gateway_references(statistics_controller, fwo_api_call, rule_to_gw_refs)

    def initialize_rulebase_link_controller(
        self, import_state: ImportState, fwo_api_call: FwoApiCall
    ) -> RulebaseLinkController:
        """
        Initialize the RulebaseLinkController and set the map of enforcing gateways.
        """
        rb_link_controller = RulebaseLinkController()
        rb_link_controller.set_map_of_all_enforcing_gateway_ids_for_rulebase_id(import_state, fwo_api_call)
        return rb_link_controller

    def prepare_rule_to_gateway_references(
        self,
        import_state: ImportState,
        new_rules: list[dict[str, Any]],
        rb_link_controller: RulebaseLinkController,
    ) -> list[dict[str, Any]]:
        """
        Prepare the list of rule-to-gateway references based on the rules and their 'install on' settings.
        """
        rule_to_gw_refs: list[dict[str, Any]] = []
        for rule in new_rules:
            if rule["rule_installon"] is None:
                self.handle_rule_without_installon(import_state, rule, rb_link_controller, rule_to_gw_refs)
            else:
                self.handle_rule_with_installon(import_state, rule, rule_to_gw_refs)
        return rule_to_gw_refs

    def handle_rule_without_installon(
        self,
        import_state: ImportState,
        rule: dict[str, Any],
        rb_link_controller: RulebaseLinkController,
        rule_to_gw_refs: list[dict[str, Any]],
    ) -> None:
        """
        Handle rules with no 'install on' setting by linking them to all gateways for the rulebase.
        """
        rule_to_gw_refs.extend(
            [
                self.create_rule_to_gateway_reference(import_state, rule, gw_id)
                for gw_id in rb_link_controller.get_gw_ids_for_rulebase_id(rule["rulebase_id"])
            ]
        )

    def handle_rule_with_installon(
        self,
        import_state: ImportState,
        rule: dict[str, Any],
        rule_to_gw_refs: list[dict[str, Any]],
    ) -> None:
        """
        Handle rules with 'install on' settings by linking them to specific gateways.
        """
        rule_installon: str | None = rule["rule_installon"]
        if rule_installon is None:
            return

        for gw_uid in rule_installon.split(fwo_const.LIST_DELIMITER):
            gw_id = import_state.lookup_gateway_id(gw_uid)
            if gw_id is not None:
                rule_to_gw_refs.append(self.create_rule_to_gateway_reference(import_state, rule, gw_id))
            else:
                FWOLogger.warning(f"Found a broken reference to a non-existing gateway (uid={gw_uid}). Ignoring.")

    def create_rule_to_gateway_reference(
        self, import_state: ImportState, rule: dict[str, Any], gw_id: int
    ) -> dict[str, Any]:
        """
        Create a dictionary representing a rule-to-gateway reference.
        """
        return {
            "rule_id": rule["rule_id"],  # TODO: rule_id does not exist
            "dev_id": gw_id,
            "created": import_state.import_id,
            "removed": None,
        }

    def insert_rule_to_gateway_references(
        self,
        statistics_controller: ImportStatisticsController,
        fwo_api_call: FwoApiCall,
        rule_to_gw_refs: list[dict[str, Any]],
    ) -> None:
        """
        Insert the rule-to-gateway references into the database.
        """
        try:
            import_results: dict[str, Any] = self.insert_rules_enforced_on_gateway(fwo_api_call, rule_to_gw_refs)
            if "errors" in import_results:
                FWOLogger.exception(f"Error in add_new_rule_enforced_on_gateway_refs: {import_results['errors']!s}")
            else:
                changes = import_results["data"]["insert_rule_enforced_on_gateway"].get("affected_rows", 1)
                statistics_controller.increment_rule_enforce_change_count(changes)
        except Exception:
            FWOLogger.exception(f"Failed to write new rules: {format_exc()!s}")
            raise

    def insert_rules_enforced_on_gateway(
        self, fwo_api_call: FwoApiCall, enforcements: list[dict[str, Any]]
    ) -> dict[str, Any]:
        """
        Insert rules enforced on gateways into the database.
        """
        if len(enforcements) > 0:
            query_variables = {"rulesEnforcedOnGateway": enforcements}
            mutation = """
                mutation importInsertRulesEnforcedOnGateway($rulesEnforcedOnGateway: [rule_enforced_on_gateway_insert_input!]!) {
                    insert_rule_enforced_on_gateway(objects: $rulesEnforcedOnGateway) {
                        affected_rows
                    }
                }"""
            return fwo_api_call.call(mutation, query_variables=query_variables)
        return {}

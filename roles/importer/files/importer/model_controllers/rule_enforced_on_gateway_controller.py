# from pydantic import BaseModel
from typing import List
from model_controllers.import_state_controller import ImportStateController
from models.rule_enforced_on_gateway import RuleEnforcedOnGateway
from fwo_log import getFwoLogger
import fwo_const
from traceback import format_exc
from model_controllers.rulebase_link_controller import RulebaseLinkController
from models.rule import Rule


class RuleEnforcedOnGatewayController:
    def __init__(self, import_state: ImportStateController):
        self.import_details: ImportStateController = import_state

    def add_new_rule_enforced_on_gateway_refs(self, new_rules, import_state):
        """
        Main function to add new rule-to-gateway references.
        """
        # Step 1: Initialize the RulebaseLinkController
        rb_link_controller = self.initialize_rulebase_link_controller(import_state)

        # Step 2: Prepare rule-to-gateway references
        rule_to_gw_refs = self.prepare_rule_to_gateway_references(new_rules, rb_link_controller)

        # Step 3: Check if there are any references to insert
        if not rule_to_gw_refs:
            logger = getFwoLogger()
            logger.info("No rules to be enforced on gateways.")
            return

        # Step 4: Insert the references into the database
        self.insert_rule_to_gateway_references(rule_to_gw_refs)

    def initialize_rulebase_link_controller(self, import_state):
        """
        Initialize the RulebaseLinkController and set the map of enforcing gateways.
        """
        rb_link_controller = RulebaseLinkController()
        rb_link_controller.set_map_of_all_enforcing_gateway_ids_for_rulebase_id(import_state)
        return rb_link_controller

    def prepare_rule_to_gateway_references(self, new_rules, rb_link_controller):
        """
        Prepare the list of rule-to-gateway references based on the rules and their 'install on' settings.
        """
        rule_to_gw_refs = []
        for rule in new_rules:
            if 'rule_installon' in rule:
                if rule['rule_installon'] is None:
                    self.handle_rule_with_no_installon(rule, rb_link_controller, rule_to_gw_refs)
                else:
                    self.handle_rule_with_installon(rule, rule_to_gw_refs)
        return rule_to_gw_refs

    def handle_rule_with_no_installon(self, 
                                      rule: Rule, 
                                      rb_link_controller: RulebaseLinkController, 
                                      rule_to_gw_refs: List[RuleEnforcedOnGateway]
                                    ):
        """
        Handle rules with no 'install on' setting by linking them to all gateways for the rulebase.
        """
        for gw_id in rb_link_controller.get_gw_ids_for_rulebase_id(rule['rulebase_id']):
            rule_to_gw_refs.append(self.create_rule_to_gateway_reference(rule, gw_id))

    def handle_rule_with_installon(self, rule, rule_to_gw_refs):
        """
        Handle rules with 'install on' settings by linking them to specific gateways.
        """
        for gw_uid in rule['rule_installon'].split(fwo_const.list_delimiter):
            gw_id = self.import_details.lookupGatewayId(gw_uid)
            if gw_id is not None:
                rule_to_gw_refs.append(self.create_rule_to_gateway_reference(rule, gw_id))
            else:
                logger = getFwoLogger()
                logger.warning(f"Found a broken reference to a non-existing gateway (uid={gw_uid}). Ignoring.")

    def create_rule_to_gateway_reference(self, rule, gw_id):
        """
        Create a dictionary representing a rule-to-gateway reference.
        """
        return RuleEnforcedOnGateway(
            rule_id=rule['rule_id'],
            dev_id=gw_id,
            created=self.import_details.ImportId,
            removed=None,
            self=self.import_details
        ).to_dict()

    def insert_rule_to_gateway_references(self, rule_to_gw_refs):
        """
        Insert the rule-to-gateway references into the database.
        """
        logger = getFwoLogger()
        try:
            import_results = self.insert_rules_enforced_on_gateway(rule_to_gw_refs)
            if 'errors' in import_results:
                logger.exception(f"Error in add_new_rule_enforced_on_gateway_refs: {str(import_results['errors'])}")
                self.import_details.increaseErrorCounter()
                self.import_details.appendErrorString(f"Error in add_new_rule_enforced_on_gateway_refs: {str(import_results['errors'])}")
            else:
                changes = import_results['data']['insert_rule_enforced_on_gateway'].get('affected_rows', 1)
                self.import_details.Stats.rule_enforce_change_count += changes
        except Exception:
            logger.exception(f"Failed to write new rules: {str(format_exc())}")
            self.import_details.increaseErrorCounterByOne()
            self.import_details.appendErrorString(f"Failed to write new rules: {str(format_exc())}")

    def insert_rules_enforced_on_gateway(self, enforcements: List[dict]):
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
            return self.import_details.call(mutation, queryVariables=query_variables)

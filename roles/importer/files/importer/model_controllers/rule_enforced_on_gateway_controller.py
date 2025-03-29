# from pydantic import BaseModel
from typing import List
from models.import_state import ImportState
from models.rule_enforced_on_gateway import RuleEnforcedOnGateway
from fwo_log import getFwoLogger
import fwo_const
from traceback import format_exc
from model_controllers.rulebase_link_controller import RulebaseLinkController

class RuleEnforcedOnGatewayController():
    def __init__(self, importState: ImportState):
        self.ImportDetails: ImportState = importState

    # this should not be confused with the rulebase_link model - it refers to the check point "install on" feature
    def insertRulesEnforcedOnGateway(self, enforcements: List[dict]):

        if len(enforcements) > 0:
            # queryVariables = { "rulesEnforcedOnGateway": { "data": enforcements } }
            queryVariables = { "rulesEnforcedOnGateway": enforcements }
            
            mutation = """
                mutation importInsertRulesEnforcedOnGateway($rulesEnforcedOnGateway: [rule_enforced_on_gateway_insert_input!]!) {
                    insert_rule_enforced_on_gateway(objects: $rulesEnforcedOnGateway) {
                        affected_rows
                    }
                }"""
            
            return self.ImportDetails.call(mutation, queryVariables=queryVariables)

    # TODO: also handled changes and deletions


    def addNewRuleEnforcedOnGatewayRefs(self, newRules, importState):
        # for each new rule: add refs in rule_enforced_on_gateway
        # assuming all gateways and rules are already in the database
        # set map for this management: rulebaseId to list of ids of all gateways this rulebase is installed on
        # (using rulebase_links)]

        rbLinkController = RulebaseLinkController()
        rbLinkController.SetMapOfAllEnforcingGatewayIdsForRulebaseId(importState)

        ruleToGwRefs = []
        # now add the references to the rules
        for rule in newRules:
            if 'rule_installon' in rule:
                if rule['rule_installon'] is None:
                    # add links to all gateways that this rulebase is installed on
                    for gwId in rbLinkController.GetGwIdsForRulebaseId(rule['rulebase_id']):
                        ruleToGwRefs.append(RuleEnforcedOnGateway(
                            rule_id=rule['rule_id'], 
                            dev_id=gwId, 
                            created=self.ImportDetails.ImportId, 
                            removed=None,
                            self=self.ImportDetails).
                            toDict())
                    pass
                else:
                    for gwUid in rule['rule_installon'].split(fwo_const.list_delimiter):
                        gwId = self.ImportDetails.lookupGatewayId(gwUid)
                        if gwId is not None:
                            ruleToGwRefs.append(RuleEnforcedOnGateway(
                                rule_id=rule['rule_id'], 
                                dev_id=gwId, 
                                created=self.ImportDetails.ImportId, 
                                removed=None,
                                self=self.ImportDetails).
                                toDict())
                        else:
                            # TODO: here we got a broken ref to a non-existing gateway (e.g. CpmiOseDevice) - ignoring for now
                            pass

        enforcementController = RuleEnforcedOnGatewayController(self.ImportDetails)

        if ruleToGwRefs is None or len(ruleToGwRefs) == 0:
            logger = getFwoLogger()
            logger.info("no rules to be enforced on gateways")
            return
        try:
            logger = getFwoLogger()
            importResults = enforcementController.insertRulesEnforcedOnGateway(ruleToGwRefs)
            if 'errors' in importResults:
                logger.exception(f"fwo_api:importNwObject - error in addNewRuleEnforcedOnGatewayRefs: {str(importResults['errors'])}")
                self.ImportDetails.increaseErrorCounterByOne()
                self.ImportDetails.appendErrorString(f"error in addNewRuleEnforcedOnGatewayRefs: {str(importResults['errors'])}")
            else:
                if 'affected_rows' in importResults['data']['insert_rule_enforced_on_gateway']:
                    changes = importResults['data']['insert_rule_enforced_on_gateway']['affected_rows']
                else:
                    changes = 1
                self.ImportDetails.Stats.RuleEnforceChangeCount += changes
        except:
            logger.exception(f"failed to write new rules: {str(format_exc())}")
            self.ImportDetails.increaseErrorCounterByOne()
            self.ImportDetails.appendErrorString(f"failed to write new rules: {str(format_exc())}")

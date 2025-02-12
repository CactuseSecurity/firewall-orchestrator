
from typing import List

from fwoBaseImport import ImportState
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from model_controllers.fwconfig_import_base import FwConfigImportBase
from fwo_log import getFwoLogger
from model_controllers.rulebase_link_controller import RulebaseLinkController
# from model_controllers.rulebase_link_uid_based_controller import RulebaseLink, RulebaseLinkUidBasedController

# this class is used for importing a config into the FWO API
class FwConfigImportGateway(FwConfigImportBase):

    ImportDetails: ImportState

    def __init__(self, importState: ImportState, config: FwConfigNormalized):
      # ImportDetails = importState
      super().__init__(importState, config)

    def updateGatewayDiffs(self, prevConfig: FwConfigNormalized):
        logger = getFwoLogger()
        errors = 0
        changes = 0
        totalChanges = 0
        # changedRuleUids = {}
        # deletedRuleUids = {}
        # newRuleUids = {}
        # ruleUidsInBoth = {}
        # previousRulebaseUids = []
        # currentRulebaseUids = []


        for gw in self.NormalizedConfig.gateways:
            # check interface changes
            # check routing changes
            # check rulebase link changes
            if gw in prevConfig.gateways:
                logger.debug(f"gateway {str(gw)} found in previous config")
                # check if rulebase links have changed
                # check if interfaces have changed
                # check if routing has changed
                pass
            else:
                logger.debug(f"gateway {str(gw)} NOT found in previous config")
                # add gateway details:
                # TODO: add interfaces
                # TODO: add routing
                gwId = self.ImportDetails.lookupGatewayId(gw.Uid)
                for link in gw.RulebaseLinks:
                    fromRuleId = self.ImportDetails.lookupRule(link.from_rule_uid)
                    toRulebaseId = self.ImportDetails.lookupRulebaseId(link.to_rulebase_uid)
                    if toRulebaseId is None:
                        logger.error(f"toRulebaseId is None for link {link}")
                        errors += 1
                        continue
                    linkTypeId = self.ImportDetails.lookupLinkType(link.link_type)
                    rbLink = RulebaseLinkController(gw_id=gwId, 
                                         from_rule_id=fromRuleId,
                                         to_rulebase_id=toRulebaseId,
                                         link_type=linkTypeId,
                                         created=self.ImportDetails.ImportId)
                    (errors, changes) = rbLink.importInsertRulebaseLink(self.ImportDetails)
                    totalChanges += changes

        return errors, totalChanges

    # this should not be confused with the rulebase_link model - it refers to the check point "install on" feature
    def insertRulesEnforcedOnGateway(self, ruleIds, devId):
        rulesEnforcedOnGateway = []
        for ruleId in ruleIds:
            rulesEnforcedOnGateway.append({
                "rule_id": ruleId,
                "dev_id": devId,
                "created": self.ImportDetails.ImportId
            })

        query_variables = {
            "ruleEnforcedOnGateway": rulesEnforcedOnGateway
        }
        mutation = """
            mutation importInsertRulesEnforcedOnGateway($rulesEnforcedOnGateway: [rule_enforced_on_gateway_insert_input!]!) {
                insert_rule_enforced_on_gateway(objects: $rulesEnforcedOnGateway) {
                    affected_rows
                }
            }"""
        
        return self.ImportDetails.call(mutation, queryVariables=query_variables)

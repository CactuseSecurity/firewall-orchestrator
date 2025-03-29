
from typing import List

from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from model_controllers.fwconfig_import_base import FwConfigImportBase
from fwo_log import getFwoLogger
from model_controllers.rulebase_link_controller import RulebaseLinkController
# from model_controllers.rulebase_link_uid_based_controller import RulebaseLink, RulebaseLinkUidBasedController

# this class is used for importing a config into the FWO API
class FwConfigImportGateway(FwConfigImportBase):

    ImportDetails: ImportStateController

    def __init__(self, importState: ImportStateController, config: FwConfigNormalized):
      super().__init__(importState, config)

    def updateGatewayDiffs(self, prevConfig: FwConfigNormalized):
        # add gateway details:
        self.updateRulebaseLinkDiffs(prevConfig)
        self.updateRuleEnforcedOnGatewayDiffs(prevConfig)
        self.updateInterfaceDiffs(prevConfig)
        self.updateRoutingDiffs(prevConfig)
        # self.ImportDetails.Stats.addError('simulate error')


    def updateRulebaseLinkDiffs(self, prevConfig: FwConfigNormalized):
        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
        for gw in self.NormalizedConfig.gateways:
            if gw in prevConfig.gateways:   # this check finds all changes in gateway (including rulebase link changes)
                if self.ImportDetails.DebugLevel>3:
                    logger.debug(f"gateway {str(gw)} found in previous config")
            else:
                if self.ImportDetails.DebugLevel>3:
                    logger.debug(f"gateway {str(gw)} NOT found in previous config")
                gwId = self.ImportDetails.lookupGatewayId(gw.Uid)
                for link in gw.RulebaseLinks:
                    fromRuleId = self.ImportDetails.lookupRule(link.from_rule_uid)
                    toRulebaseId = self.ImportDetails.lookupRulebaseId(link.to_rulebase_uid)
                    if toRulebaseId is None:
                        self.ImportDetails.Stats.addError(f"toRulebaseId is None for link {link}")
                        continue
                    linkTypeId = self.ImportDetails.lookupLinkType(link.link_type)
                    rbLink = RulebaseLinkController(gw_id=gwId, 
                                         from_rule_id=fromRuleId,
                                         to_rulebase_id=toRulebaseId,
                                         link_type=linkTypeId,
                                         created=self.ImportDetails.ImportId)

                    # Handle new links
                    logger.debug(f"link {link} was added")
                    rbLink.importInsertRulebaseLink(self.ImportDetails)

                    # TODO: check for changed rbLink
                    # for prev_gw in prevConfig.gateways:
                    #     if prev_gw.Uid == gw.Uid:
                    #         for prev_link in prev_gw.RulebaseLinks:
                    #             if prev_link not in gw.RulebaseLinks:
                    #                 # TODO: Handle deleted links
                    #                 logger.debug(f"link {prev_link} was deleted")
                    #             if link not in prev_gw.RulebaseLinks:
                    #                 # Handle new links
                    #                 logger.debug(f"link {link} was added")
                    #                 rbLink.importInsertRulebaseLink(self.ImportDetails)
                    #                 # TODO: check for changed rbLink
        return

    def updateRuleEnforcedOnGatewayDiffs(self, prevConfig: FwConfigNormalized):
        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
        # TODO: needs to be implemented
        return
    

    def updateInterfaceDiffs(self, prevConfig: FwConfigNormalized):
        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
        # TODO: needs to be implemented
        return

    def updateRoutingDiffs(self, prevConfig: FwConfigNormalized):
        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
        # TODO: needs to be implemented
        return
    
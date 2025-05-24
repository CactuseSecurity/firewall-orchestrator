
from typing import List

from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from model_controllers.fwconfig_import_base import FwConfigImportBase
from fwo_log import getFwoLogger
from model_controllers.rulebase_link_controller import RulebaseLinkController
from models.rulebase_link import RulebaseLink
# from model_controllers.rulebase_link_uid_based_controller import RulebaseLink, RulebaseLinkUidBasedController

# this class is used for importing a config into the FWO API
class FwConfigImportGateway(FwConfigImportBase):

    ImportDetails: ImportStateController

    def __init__(self, importState: ImportStateController, config: FwConfigNormalized):
      super().__init__(importState, config)

    def update_gateway_diffs(self, prev_config: FwConfigNormalized):
        # add gateway details:
        self.update_rulebase_link_diffs(prev_config)
        # self.updateRuleEnforcedOnGatewayDiffs(prevConfig)
        self.update_interface_diffs(prev_config)
        self.update_routing_diffs(prev_config)
        # self.ImportDetails.Stats.addError('simulate error')


    def update_rulebase_link_diffs(self, prev_config: FwConfigNormalized):
        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
        rb_link_list = []
        for gw in self.NormalizedConfig.gateways:
            if gw not in prev_config.gateways:   # this check finds all changes in gateway (including rulebase link changes)
                if self.ImportDetails.DebugLevel>3:
                    logger.debug(f"gateway {str(gw)} NOT found in previous config")
                gwId = self.ImportDetails.lookupGatewayId(gw.Uid)
                if gwId is None or gwId == '' or gwId == 'none':
                    logger.warning(f"did not find a gwId for UID {gw.Uid}")
                for link in gw.RulebaseLinks:
                    self.add_single_link(rb_link_list, link, gwId, logger)
        rb_link_controller = RulebaseLinkController()
        rb_link_controller.insert_rulebase_links(self.ImportDetails, rb_link_list) 
        

    def add_single_link(self, rb_link_list, link, gw_id, logger):
        from_rule_id = self.ImportDetails.lookupRule(link.from_rule_uid)
        if link.from_rulebase_uid is None or link.from_rulebase_uid == '':
            from_rulebase_id = None
        else:
            from_rulebase_id = self.ImportDetails.lookupRulebaseId(link.from_rulebase_uid)
        to_rulebase_id = self.ImportDetails.lookupRulebaseId(link.to_rulebase_uid)
        if to_rulebase_id is None:
            self.ImportDetails.Stats.addError(f"toRulebaseId is None for link {link}")
            return
        link_type_id = self.ImportDetails.lookupLinkType(link.link_type)
        if link_type_id is None or type(link_type_id) is not int:
            logger = getFwoLogger()
            logger.warning(f"did not find a link_type_id for link_type {link.link_type}")
        rb_link_list.append(RulebaseLink(gw_id=gw_id, 
                                from_rule_id=from_rule_id,
                                to_rulebase_id=to_rulebase_id,
                                link_type=link_type_id,
                                is_initial=link.is_initial,
                                is_global=link.is_global,
                                from_rulebase_id=from_rulebase_id,
                                created=self.ImportDetails.ImportId).toDict())
        logger.debug(f"link {link} was added")

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


    def update_interface_diffs(self, prevConfig: FwConfigNormalized):
        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
        # TODO: needs to be implemented

    def update_routing_diffs(self, prevConfig: FwConfigNormalized):
        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
        # TODO: needs to be implemented
    
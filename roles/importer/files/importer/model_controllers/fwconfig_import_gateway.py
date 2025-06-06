
from typing import List

from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from fwo_log import getFwoLogger
from model_controllers.rulebase_link_controller import RulebaseLinkController
from models.rulebase_link import RulebaseLink
# from model_controllers.rulebase_link_uid_based_controller import RulebaseLink, RulebaseLinkUidBasedController

# this class is used for importing a config into the FWO API


def update_gateway_diffs(prev_config: FwConfigNormalized, ImportDetails: ImportStateController, NormalizedConfig: FwConfigNormalized):
    # add gateway details:
    update_rulebase_link_diffs(prev_config, ImportDetails, NormalizedConfig)
    # self.updateRuleEnforcedOnGatewayDiffs(prevConfig)
    update_interface_diffs(prev_config, ImportDetails, NormalizedConfig)
    update_routing_diffs(prev_config, ImportDetails, NormalizedConfig)
    # self.ImportDetails.Stats.addError('simulate error')


def update_rulebase_link_diffs(prev_config: FwConfigNormalized, ImportDetails: ImportStateController, NormalizedConfig: FwConfigNormalized):
    logger = getFwoLogger(debug_level=ImportDetails.DebugLevel)
    rb_link_list = []
    for gw in NormalizedConfig.gateways:
        if gw not in prev_config.gateways:   # this check finds all changes in gateway (including rulebase link changes)
            if ImportDetails.DebugLevel>3:
                logger.debug(f"gateway {str(gw)} NOT found in previous config")
            gwId = ImportDetails.lookupGatewayId(gw.Uid)
            if gwId is None or gwId == '' or gwId == 'none':
                logger.warning(f"did not find a gwId for UID {gw.Uid}")
            for link in gw.RulebaseLinks:
                add_single_link(rb_link_list, link, gwId, logger, ImportDetails, NormalizedConfig)
    rb_link_controller = RulebaseLinkController()
    rb_link_controller.insert_rulebase_links(ImportDetails, rb_link_list) 
    

def add_single_link(rb_link_list, link, gw_id, logger, ImportDetails: ImportStateController, NormalizedConfig: FwConfigNormalized):
    from_rule_id = ImportDetails.lookupRule(link.from_rule_uid)
    if link.from_rulebase_uid is None or link.from_rulebase_uid == '':
        from_rulebase_id = None
    else:
        from_rulebase_id = ImportDetails.lookupRulebaseId(link.from_rulebase_uid)
    to_rulebase_id = ImportDetails.lookupRulebaseId(link.to_rulebase_uid)
    if to_rulebase_id is None:
        ImportDetails.Stats.addError(f"toRulebaseId is None for link {link}")
        return
    link_type_id = ImportDetails.lookupLinkType(link.link_type)
    if link_type_id is None or type(link_type_id) is not int:
        logger = getFwoLogger()
        logger.warning(f"did not find a link_type_id for link_type {link.link_type}")
    rb_link_list.append(RulebaseLink(gw_id=gw_id, 
                            from_rule_id=from_rule_id,
                            to_rulebase_id=to_rulebase_id,
                            link_type=link_type_id,
                            is_initial=link.is_initial,
                            is_global=link.is_global,
                            is_section = link.is_section,
                            from_rulebase_id=from_rulebase_id,
                            created=ImportDetails.ImportId).toDict())
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


def update_interface_diffs(prevConfig: FwConfigNormalized, ImportDetails: ImportStateController, NormalizedConfig: FwConfigNormalized):
    logger = getFwoLogger(debug_level=ImportDetails.DebugLevel)
    # TODO: needs to be implemented

def update_routing_diffs(prevConfig: FwConfigNormalized, ImportDetails: ImportStateController, NormalizedConfig: FwConfigNormalized):
    logger = getFwoLogger(debug_level=ImportDetails.DebugLevel)
    # TODO: needs to be implemented

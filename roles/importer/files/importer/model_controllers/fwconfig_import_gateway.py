
from typing import List

from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from fwo_log import getFwoLogger
from model_controllers.rulebase_link_controller import RulebaseLinkController
from models.rulebase_link import RulebaseLink
from services.service_provider import ServiceProvider
from services.global_state import GlobalState
from services.enums import Services
# from model_controllers.rulebase_link_uid_based_controller import RulebaseLink, RulebaseLinkUidBasedController

# this class is used for importing a config into the FWO API

class FwConfigImportGateway:

    _global_state: GlobalState


    def __init__(self):
        service_provider = ServiceProvider()
        self._global_state = service_provider.get_service(Services.GLOBAL_STATE)


    def update_gateway_diffs(self):
        # add gateway details:
        self.update_rulebase_link_diffs()
        # self.updateRuleEnforcedOnGatewayDiffs(prevConfig)
        self.update_interface_diffs()
        self.update_routing_diffs()
        # self.ImportDetails.Stats.addError('simulate error')


    def update_rulebase_link_diffs(self):
        logger = getFwoLogger(debug_level=self._global_state.import_state.DebugLevel)
        rb_link_list = []
        for gw in self._global_state.normalized_config.gateways:
            if gw not in self._global_state.previous_config.gateways:   # this check finds all changes in gateway (including rulebase link changes)
                if self._global_state.import_state.DebugLevel>3:
                    logger.debug(f"gateway {str(gw)} NOT found in previous config")
                gwId = self._global_state.import_state.lookupGatewayId(gw.Uid)
                if gwId is None or gwId == '' or gwId == 'none':
                    logger.warning(f"did not find a gwId for UID {gw.Uid}")
                for link in gw.RulebaseLinks:
                    self.add_single_link(rb_link_list, link, gwId, logger)
        rb_link_controller = RulebaseLinkController()
        rb_link_controller.insert_rulebase_links(self._global_state.import_state, rb_link_list) 
        

    def add_single_link(self, rb_link_list, link, gw_id, logger):
        from_rule_id = self._global_state.import_state.lookupRule(link.from_rule_uid)
        if link.from_rulebase_uid is None or link.from_rulebase_uid == '':
            from_rulebase_id = None
        else:
            from_rulebase_id = self._global_state.import_state.lookupRulebaseId(link.from_rulebase_uid)
        to_rulebase_id = self._global_state.import_state.lookupRulebaseId(link.to_rulebase_uid)
        if to_rulebase_id is None:
            self._global_state.import_state.Stats.addError(f"toRulebaseId is None for link {link}")
            return
        link_type_id = self._global_state.import_state.lookupLinkType(link.link_type)
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
                                created=self._global_state.import_state.ImportId).toDict())
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


    def update_interface_diffs(self):
        logger = getFwoLogger(debug_level=self._global_state.import_state.DebugLevel)
        # TODO: needs to be implemented

    def update_routing_diffs(self):
        logger = getFwoLogger(debug_level=self._global_state.import_state.DebugLevel)
        # TODO: needs to be implemented

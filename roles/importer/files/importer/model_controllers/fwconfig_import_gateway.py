import copy
from fwo_log import getFwoLogger

from model_controllers.rulebase_link_controller import RulebaseLinkController

from models.gateway import Gateway
from models.rulebase_link import RulebaseLink, RulebaseLinkUidBased

from services.enums import Services
from services.global_state import GlobalState
from services.service_provider import ServiceProvider




class FwConfigImportGateway:
    """
        Provides methods import gateway information into the FWO API.
    """

    _global_state: GlobalState
    _rb_link_controller: RulebaseLinkController


    def __init__(self):
        service_provider = ServiceProvider()
        self._global_state = service_provider.get_service(Services.GLOBAL_STATE)
        self._rb_link_controller = RulebaseLinkController()


    def update_gateway_diffs(self):

        # add gateway details:
        self._rb_link_controller.get_rulebase_links(self._global_state.import_state)
        required_inserts, required_removes = self.update_rulebase_link_diffs()
        self._rb_link_controller.insert_rulebase_links(self._global_state.import_state, required_inserts)
        self._rb_link_controller.remove_rulebase_links(self._global_state.import_state, required_removes)       
        # self.updateRuleEnforcedOnGatewayDiffs(prevConfig)
        self.update_interface_diffs()
        self.update_routing_diffs()
        # self.ImportDetails.Stats.addError('simulate error')


    def update_rulebase_link_diffs(self):

        required_inserts: list[RulebaseLinkUidBased] = []
        required_removes: list[int] = []

        logger = getFwoLogger(debug_level=self._global_state.import_state.DebugLevel)

        for gw in self._global_state.normalized_config.gateways:

            previous_config_gw = next((p_gw for p_gw in self._global_state.previous_config.gateways if gw.Uid == p_gw.Uid), None)

            if gw not in self._global_state.previous_config.gateways:   # this check finds all changes in gateway (including rulebase link changes)
                if self._global_state.import_state.DebugLevel>8:
                    logger.debug(f"gateway {str(gw)} NOT found in previous config")
                gw_id = self._global_state.import_state.lookupGatewayId(gw.Uid)
                if gw_id is None or gw_id == '' or gw_id == 'none':
                    logger.warning(f"did not find a gwId for UID {gw.Uid}")

                self._create_insert_args(gw, previous_config_gw, gw_id, logger, required_inserts)

                if previous_config_gw:
                    self._create_remove_args(gw, previous_config_gw, gw_id, logger, required_removes)

        return required_inserts, required_removes
    

    def _create_insert_args(self, normalized_gateway: Gateway, previous_gateway: Gateway, gw_id, logger, arg_list):
        
        rulebase_links = []

        for link in normalized_gateway.RulebaseLinks:
            if previous_gateway:
                rulebase_links = previous_gateway.RulebaseLinks
            self._try_add_single_link(arg_list, link, rulebase_links, gw_id, True, logger)


    def _create_remove_args(self, normalized_gateway: Gateway, previous_gateway: Gateway, gw_id, logger, arg_list):
            
            removed_rulebase_links = []

            for link in previous_gateway.RulebaseLinks:
                self._try_add_single_link(removed_rulebase_links, link, normalized_gateway.RulebaseLinks, gw_id, False, logger)
            for link in removed_rulebase_links:
                link_in_db = self._try_get_id_based_link(link, self._rb_link_controller.rb_links)
                if link_in_db:
                    arg_list.append(link_in_db.id)


    def _try_add_single_link(self, rb_link_list, link, link_list, gw_id, is_insert, logger):
        
        # If rule changed we need the id of the old version, since the rulebase links still have the old fks (for updates)

        from_rule_id = self._global_state.import_state.removed_rules_map.get(link.from_rule_uid, None)

        # If rule is unchanged or new id can be fetched from RuleMap, because it has been updated already
        if not from_rule_id or is_insert:
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
            logger.warning(f"did not find a link_type_id for link_type {link.link_type}")

        if not self._link_is_in_link_list(link, link_list):
            rb_link_list.append(RulebaseLink(gw_id=gw_id, 
                                    from_rule_id=from_rule_id,
                                    to_rulebase_id=to_rulebase_id,
                                    link_type=link_type_id,
                                    is_initial=link.is_initial,
                                    is_global=link.is_global,
                                    is_section = link.is_section,
                                    from_rulebase_id=from_rulebase_id,
                                    created=self._global_state.import_state.ImportId).toDict())
            
            if self._global_state.import_state.DebugLevel > 8:
                logger.debug(f"link {link} was added")


    def _link_is_in_link_list(self, link: RulebaseLinkUidBased, link_list: list[RulebaseLinkUidBased]):

        if link_list:
            existing_link = next((
                existing_link 
                for existing_link in link_list
                if existing_link.toDict() == link.toDict() 
            ), None)

            if existing_link:
                return True
            
        return False
    

    def _try_get_id_based_link(self, link: RulebaseLinkUidBased, link_list: list[RulebaseLink]):
            
        return next((
            existing_link 
            for existing_link in link_list
            if {**existing_link.toDict(), "created": 0} == {**link, "created": 0}
        ), None)


    def update_interface_diffs(self):
        logger = getFwoLogger(debug_level=self._global_state.import_state.DebugLevel)
        # TODO: needs to be implemented


    def update_routing_diffs(self):
        logger = getFwoLogger(debug_level=self._global_state.import_state.DebugLevel)
        # TODO: needs to be implemented


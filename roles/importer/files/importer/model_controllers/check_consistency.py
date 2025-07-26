
import fwo_const
from fwo_log import getFwoLogger
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from models.rulebase_link import RulebaseLink
from model_controllers.rulebase_link_controller import RulebaseLinkController
from model_controllers.fwconfig_import_object import FwConfigImportObject
from models.fwconfig_normalized import FwConfigNormalized
from fwo_base import ConfFormat
from services.service_provider import ServiceProvider
from services.enums import Services
from fwo_exceptions import FwoImporterErrorInconsistencies


# this class is used for importing a config into the FWO API
class FwConfigImportCheckConsistency(FwConfigImport):
    issues: dict = {}
    maps: FwConfigImportObject # = FwConfigImportObject()
    config: FwConfigNormalizedController = FwConfigNormalizedController(ConfFormat.NORMALIZED, FwConfigNormalized())

    # merges all configs in the set together to prepare for consistency checks
    def __init__(self, import_details: ImportStateController, config_list: FwConfigManagerListController):
        service_provider = ServiceProvider()
        self._global_state = service_provider.get_service(Services.GLOBAL_STATE)
        self.import_state = import_details
        self.maps = FwConfigImportObject()
        
        for mgr in config_list.ManagerSet:
            for cfg in mgr.Configs:
                import_worker = FwConfigImport()
                self.config.merge(cfg)
                self.maps.NetworkObjectTypeMap.update(import_worker.fwconfig_import_object.NetworkObjectTypeMap)
                self.maps.ServiceObjectTypeMap.update(import_worker.fwconfig_import_object.ServiceObjectTypeMap)
                self.maps.UserObjectTypeMap.update(import_worker.fwconfig_import_object.UserObjectTypeMap)


    # pre-flight checks
    def checkConfigConsistency(self, config: FwConfigNormalized = None):
        self.checkColorConsistency(config, fix=True)
        self.checkNetworkObjectConsistency(config)
        self.checkServiceObjectConsistency(config)
        self.checkUserObjectConsistency(config)
        self.checkZoneObjectConsistency(config)
        self.check_rulebase_consistency(config)
        self.check_gateway_consistency(config)
        self.check_rulebase_link_consistency(config)

        if len(self.issues)>0:
            self.import_state.addError("Inconsistencies found in the configuration: " + str(self.issues))
            raise FwoImporterErrorInconsistencies("Inconsistencies found in the configuration.")

        if self.import_state.DebugLevel >= 1:
            getFwoLogger().info("Consistency check completed without issues.")


    def checkNetworkObjectConsistency(self, config: FwConfigManagerListController = None):
        # check if all uid refs are valid
        global_objects = set()

        # add all new obj refs from all rules
        for mgr in sorted(config.ManagerSet, key=lambda m: not getattr(m, 'IsSuperManager', False)):
            if mgr.IsSuperManager:
                global_objects = config.get_all_network_object_uids(mgr.ManagerUid)
            all_used_obj_refs = []
            for single_config in mgr.Configs:
                for rb in single_config.rulebases:
                    all_used_obj_refs += self._collect_all_used_objects_from_rules(rb)
                
                all_used_obj_refs += self._collect_all_used_objects_from_groups(single_config)

            # now make list unique and get all refs not contained in network_objects
            all_used_obj_refs = set(all_used_obj_refs)

            unresolvable_nw_obj_refs = all_used_obj_refs - config.get_all_network_object_uids(mgr.ManagerUid) - global_objects
            if len(unresolvable_nw_obj_refs)>0:
                self.issues.update({'unresolvableNwObRefs': list(unresolvable_nw_obj_refs)})

            self._check_network_object_types_exist(single_config)
            self._check_objects_with_missing_ips(single_config)


    def _check_network_object_types_exist(self, single_config):
        allUsedObjTypes = set()
        for objId in single_config.network_objects:
            allUsedObjTypes.add(single_config.network_objects[objId].obj_typ)
        allUsedObjTypes = list(set(allUsedObjTypes))
        missingNwObjTypes = allUsedObjTypes - self.maps.NetworkObjectTypeMap.keys()
        if len(missingNwObjTypes)>0:
            self.issues.update({'unresolvableNwObjTypes': list(missingNwObjTypes)})


    @staticmethod
    def _collect_all_used_objects_from_groups(single_config):
        all_used_obj_refs = []
        # add all nw obj refs from groups
        for obj_id in single_config.network_objects:
            if single_config.network_objects[obj_id].obj_typ=='group':
                if single_config.network_objects[obj_id].obj_member_refs is not None:
                    all_used_obj_refs += single_config.network_objects[obj_id].obj_member_refs.split(fwo_const.list_delimiter)
        return all_used_obj_refs
    

    def _collect_all_used_objects_from_rules(self, rb):
        all_used_obj_refs = []
        for rule_id in rb.Rules:
            all_used_obj_refs += rb.Rules[rule_id].rule_src_refs.split(fwo_const.list_delimiter)
            all_used_obj_refs += rb.Rules[rule_id].rule_dst_refs.split(fwo_const.list_delimiter)
        
        return all_used_obj_refs


    def _check_objects_with_missing_ips(self, single_config):
        # check if there are any objects with obj_typ<>group and empty ip addresses (breaking constraint)
        nonGroupNwObjWithMissingIps = []
        for objId in single_config.network_objects:
            if single_config.network_objects[objId].obj_typ!='group':
                ip1 = single_config.network_objects[objId].obj_ip
                ip2 = single_config.network_objects[objId].obj_ip_end
                if ip1==None or ip2==None:
                    nonGroupNwObjWithMissingIps.append(single_config.network_objects[objId])
        if len(nonGroupNwObjWithMissingIps)>0:
            self.issues.update({'non-group network object with undefined IP addresse(s)': list(nonGroupNwObjWithMissingIps)})


    def checkServiceObjectConsistency(self, config: FwConfigManagerListController = None):
        # check if all uid refs are valid
        global_objects = set()

        for mgr in sorted(config.ManagerSet, key=lambda m: not getattr(m, 'IsSuperManager', False)):
            if mgr.IsSuperManager:
                global_objects = config.get_all_service_object_uids(mgr.ManagerUid)
            all_used_obj_refs = []
            for single_config in mgr.Configs:
                all_used_obj_refs += self._collect_service_object_refs_from_rules(single_config)
                all_used_obj_refs += self._collect_all_service_object_refs_from_groups(single_config)
                self._check_service_object_types_exist(single_config)
                # now make list unique 
                all_used_obj_refs = set(all_used_obj_refs)
                # and get all refs not contained in serivce_objects

            unresolvableObRefs = all_used_obj_refs - config.get_all_service_object_uids(mgr.ManagerUid) - global_objects
            if len(unresolvableObRefs)>0:
                self.issues.update({'unresolvableSvcObRefs': list(unresolvableObRefs)})



    def _check_service_object_types_exist(self, single_config):
        # check that all obj_typ exist 
        all_used_obj_types = set()
        for obj_id in single_config.service_objects:
            all_used_obj_types.add(single_config.service_objects[obj_id].svc_typ)
        all_used_obj_types = list(set(all_used_obj_types))
        missing_obj_types = all_used_obj_types - self.maps.ServiceObjectTypeMap.keys()
        if len(missing_obj_types)>0:
            self.issues.update({'unresolvableSvcObjTypes': list(missing_obj_types)})

    @staticmethod
    def _collect_all_service_object_refs_from_groups(single_config):
        all_used_obj_refs = []
        for objId in single_config.service_objects:
            if single_config.service_objects[objId].svc_typ=='group':
                if single_config.service_objects[objId].svc_member_refs is not None:
                    all_used_obj_refs += single_config.service_objects[objId].svc_member_refs.split(fwo_const.list_delimiter)
        return all_used_obj_refs

    @staticmethod
    def _collect_service_object_refs_from_rules(single_config):
        all_used_obj_refs = []
        for rb in single_config.rulebases:
            for ruleId in rb.Rules:
                all_used_obj_refs += rb.Rules[ruleId].rule_svc_refs.split(fwo_const.list_delimiter)
        return all_used_obj_refs


    def checkUserObjectConsistency(self, config: FwConfigManagerListController = None):
        global_objects = set()
        # add all user refs from all rules
        for mgr in sorted(config.ManagerSet, key=lambda m: not getattr(m, 'IsSuperManager', False)):
            all_used_obj_refs = []
            if mgr.IsSuperManager:
                global_objects = config.get_all_user_object_uids(mgr.ManagerUid)
            for single_config in mgr.Configs:
                all_used_obj_refs += self._collect_users_from_rules(single_config)
                self._collect_users_from_groups(single_config, all_used_obj_refs)
                self._check_user_types_exist(single_config)

            # now make list unique and get all refs not contained in users
            all_used_obj_refs = set(all_used_obj_refs)
            unresolvable_obj_refs = all_used_obj_refs - config.get_all_user_object_uids(mgr.ManagerUid) - global_objects
            if len(unresolvable_obj_refs)>0:
                self.issues.update({'unresolvableUserObjRefs': list(unresolvable_obj_refs)})


    def _collect_users_from_rules(self, single_config):
        all_used_obj_refs = []
        for rb in single_config.rulebases:
            for ruleId in rb.Rules:
                if fwo_const.user_delimiter in rb.Rules[ruleId].rule_src_refs:
                    all_used_obj_refs += self._collectUsersFromRule(rb.Rules[ruleId].rule_src_refs.split(fwo_const.list_delimiter))
                    all_used_obj_refs += self._collectUsersFromRule(rb.Rules[ruleId].rule_dst_refs.split(fwo_const.list_delimiter))
        return all_used_obj_refs


    def _collect_users_from_groups(self, single_config, all_used_obj_refs):
        for objId in single_config.users:
            if self.users[objId].user_typ=='group':
                if self.users[objId].user_member_refs is not None:
                    all_used_obj_refs += self.users[objId].user_member_refs.split(fwo_const.list_delimiter)


    def _check_user_types_exist(self, single_config):
        # check that all obj_typ exist 
        allUsedObjTypes = set()
        for objId in single_config.users:
            allUsedObjTypes.add(single_config.users[objId].user_typ)
        allUsedObjTypes = list(set(allUsedObjTypes))    # make list unique
        missingObjTypes = allUsedObjTypes - self.maps.UserObjectTypeMap.keys()
        if len(missingObjTypes)>0:
            self.issues.update({'unresolvableUserObjTypes': list(missingObjTypes)})


    @staticmethod
    def _collectUsersFromRule(listOfElements):
        userRefs = []
        for el in listOfElements:
            splitResult = el.split(fwo_const.user_delimiter)
            if len(splitResult)==2:
                userRefs.append(splitResult[0])
        return userRefs

    
    def checkZoneObjectConsistency(self, config: FwConfigManagerListController = None):
        all_used_zone_refs = set()
        for mgr in config.ManagerSet:
            for single_config in mgr.Configs:
                all_used_zone_refs = all_used_zone_refs.union(self._collect_zone_refs_from_rules(single_config))

                # we currently do not have zone groups - skipping group ref handling
                # we currently do not have zone types - skipping type handling

            self._check_zone_refs(single_config, all_used_zone_refs, config.get_all_zone_uids(mgr.ManagerUid))


    def _check_zone_refs(self, single_config, all_used_zone_refs, all_zone_uids):
        # now make list unique and get all refs not contained in zone_objects
        all_used_zone_refs = set(all_used_zone_refs)
        unresolvable_zone_refs = all_used_zone_refs - all_zone_uids
        if len(unresolvable_zone_refs)>0:
            self.issues.update({'unresolvableZoneRefs': list(unresolvable_zone_refs)})
    
    @staticmethod
    def _collect_zone_refs_from_rules(single_config):
        all_used_zones_refs = []
        for rb in single_config.rulebases:
            for ruleId in rb.Rules:
                if rb.Rules[ruleId].rule_src_zone is not None:
                    all_used_zones_refs += rb.Rules[ruleId].rule_src_zone
                if rb.Rules[ruleId].rule_dst_zone is not None:
                    all_used_zones_refs += rb.Rules[ruleId].rule_dst_zone
        return set(all_used_zones_refs)


    # check if all color refs are valid (in the DB)
    # fix=True means that missing color refs will be replaced by the default color (black)
    def checkColorConsistency(self, config: FwConfigNormalized, fix=True):
        self.import_state.SetColorRefMap()
        
        # collect all colors

        for mgr in config.ManagerSet:
            for single_config in mgr.Configs:

                allUsedNwObjColorRefSet, allUsedSvcColorRefSet, allUsedUserColorRefSet = \
                    self._collect_all_used_colors(single_config)
 
                unresolvableNwObjColors, unresolvableSvcColors, unresolvableUserColors = \
                    self._check_resolvability_of_used_colors(allUsedNwObjColorRefSet, allUsedSvcColorRefSet, allUsedUserColorRefSet)

                if fix:
                    self._fix_colors(single_config, unresolvableNwObjColors, unresolvableSvcColors, unresolvableUserColors)
                elif len(unresolvableNwObjColors)>0 or len(unresolvableSvcColors)>0 or len(unresolvableUserColors)>0:
                    self.issues.update({ 'unresolvableColorRefs': 
                        {'nwObjColors': unresolvableNwObjColors, 'svcColors': unresolvableSvcColors, 'userColors': unresolvableUserColors}})


    @staticmethod
    def _collect_all_used_colors(single_config):
        allUsedNwObjColorRefSet = set()
        allUsedSvcColorRefSet = set()
        allUsedUserColorRefSet = set()

        for uid in single_config.network_objects:
            if single_config.network_objects[uid].obj_color is not None:
                allUsedNwObjColorRefSet.add(single_config.network_objects[uid].obj_color)
        for uid in single_config.service_objects:
            if single_config.service_objects[uid].svc_color is not None:
                allUsedSvcColorRefSet.add(single_config.service_objects[uid].svc_color)
        for uid in single_config.users:
            if single_config.users[uid].user_color is not None:
                allUsedUserColorRefSet.add(single_config.users[uid].user_color)

        return allUsedNwObjColorRefSet, allUsedSvcColorRefSet, allUsedUserColorRefSet


    def _check_resolvability_of_used_colors(self, allUsedNwObjColorRefSet, allUsedSvcColorRefSet, allUsedUserColorRefSet):
        unresolvableNwObjColors = []
        unresolvableSvcColors = []
        unresolvableUserColors = []
        # check all nwobj color refs
        for color_string in allUsedNwObjColorRefSet:
            color_id = self.import_state.lookupColorId(color_string)
            if color_id is None:
                unresolvableNwObjColors.append(color_string)

        # check all nwobj color refs
        for color_string in allUsedSvcColorRefSet:
            color_id = self.import_state.lookupColorId(color_string)
            if color_id is None:
                unresolvableSvcColors.append(color_string)

        # check all user color refs
        for color_string in allUsedUserColorRefSet:
            color_id = self.import_state.lookupColorId(color_string)
            if color_id is None:
                unresolvableUserColors.append(color_string)
        
        return unresolvableNwObjColors, unresolvableSvcColors, unresolvableUserColors


    @staticmethod
    def _fix_colors(config, unresolvable_nw_obj_colors, unresolvable_svc_colors, unresolvable_user_colors):
        # Replace unresolvable network object colors
        for obj in config.network_objects.values():
            if obj.obj_color in unresolvable_nw_obj_colors:
                obj.obj_color = fwo_const.defaultColor
        # Replace unresolvable service object colors
        for obj in config.service_objects.values():
            if obj.svc_color in unresolvable_svc_colors:
                obj.svc_color = fwo_const.defaultColor
        # Replace unresolvable user object colors
        for obj in config.users.values():
            if obj.user_color in unresolvable_user_colors:
                obj.user_color = fwo_const.defaultColor


    @staticmethod
    def _extract_rule_track_n_action_refs(rulebases):
        track_refs = []
        action_refs = []
        for rb in rulebases:
            track_refs.extend(rule.rule_track for rule in rb.Rules.values())
            action_refs.extend(rule.rule_action for rule in rb.Rules.values())
        return track_refs, action_refs


    def check_rulebase_consistency(self, config: FwConfigNormalized = None):
        all_used_track_refs = []
        all_used_action_refs = []

        for mgr in config.ManagerSet:
            for single_config in mgr.Configs:
                track_refs, action_refs = self._extract_rule_track_n_action_refs(single_config.rulebases)
                all_used_track_refs.extend(track_refs)
                all_used_action_refs.extend(action_refs)

                all_used_track_refs = list(set(all_used_track_refs))
                all_used_action_refs = list(set(all_used_action_refs))

                unresolvable_tracks = all_used_track_refs - self.import_state.Tracks.keys()
                if unresolvable_tracks:
                    self.issues.update({'unresolvableRuleTracks': list(unresolvable_tracks)})

                unresolvable_actions = all_used_action_refs - self.import_state.Actions.keys()
                if unresolvable_actions:
                    self.issues.update({'unresolvableRuleActions': list(unresolvable_actions)})


    # e.g. check routing, interfaces refs
    def check_gateway_consistency(self, config: FwConfigNormalized = None):
        # TODO: implement
        pass


    # e.g. check rule to rule refs
    # TODO: check if the rule & rulebases referenced belong to either 
    #       - the same submanger or 
    #       - the super manager but not another sub manager
    def check_rulebase_link_consistency(self, config: FwConfigNormalized = None):
        broken_rulebase_links = []

        all_rulebase_uids, all_rule_uids = self._collect_uids(config)

        # check consistency of links
        for mgr in config.ManagerSet:
            if self.import_state.MgmDetails.ImportDisabled:
                continue
            for single_config in mgr.Configs:        
                # now check rblinks for all gateways
                for gw in single_config.gateways:
                    self._check_rulebase_links_for_gateway(gw, broken_rulebase_links, all_rule_uids, all_rulebase_uids)

        if len(broken_rulebase_links)>0:
            self.issues.update({'brokenRulebaseLinks': broken_rulebase_links})


    def _check_rulebase_links_for_gateway(self, gw, broken_rulebase_links, all_rule_uids, all_rulebase_uids):
        if not gw.ImportDisabled:
            for rbl in gw.RulebaseLinks:
                self._check_rulebase_link(gw, rbl, broken_rulebase_links, all_rule_uids, all_rulebase_uids)


    def _collect_uids(self, config):
        all_rulebase_uids = set()
        all_rule_uids = set()
        for mgr in config.ManagerSet:
            if self.import_state.MgmDetails.ImportDisabled:
                continue
            for single_config in mgr.Configs:        
                # collect rulebase UIDs
                for rb in single_config.rulebases:
                    all_rulebase_uids.add(rb.uid)
                    # collect rule UIDs
                    for rule_uid in rb.Rules:
                        all_rule_uids.add(rule_uid)
        return all_rulebase_uids, all_rule_uids


    def _check_rulebase_link(self, gw, rbl, broken_rulebase_links, all_rule_uids, all_rulebase_uids):
        if rbl.from_rulebase_uid is not None and rbl.from_rulebase_uid != '' and rbl.from_rulebase_uid not in all_rulebase_uids:
            self._add_issue(broken_rulebase_links, rbl, gw, 'from_rulebase_uid broken')
        if rbl.to_rulebase_uid is not None and rbl.to_rulebase_uid != '' and rbl.to_rulebase_uid not in all_rulebase_uids:
            self._add_issue(broken_rulebase_links, rbl, gw, 'to_rulebase_uid broken')
        if rbl.from_rule_uid is not None and rbl.from_rule_uid != '' and rbl.from_rule_uid not in all_rule_uids:
            self._add_issue(broken_rulebase_links, rbl, gw, 'from_rule_uid broken')

    @staticmethod
    def _add_issue(broken_rulebase_links, rbl, gw, error_txt):
            rbl_dict = rbl.toDict()
            rbl_dict.update({'error': error_txt})
            rbl_dict.update({'gw': f'{gw.Name} ({gw.Uid})'})
            if rbl_dict not in broken_rulebase_links:
                broken_rulebase_links.append(rbl_dict)

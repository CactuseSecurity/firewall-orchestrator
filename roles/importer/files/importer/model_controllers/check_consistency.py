
from typing import Any
import fwo_const
from fwo_log import get_fwo_logger
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from model_controllers.fwconfigmanager_controller import FwConfigManager
from model_controllers.fwconfig_import_object import FwConfigImportObject
from models.fwconfig_normalized import FwConfigNormalized
from fwo_base import ConfFormat
from models.rulebase import Rulebase
from models.networkobject import NetworkObject
from models.gateway import Gateway
from models.rulebase_link import RulebaseLinkUidBased
from services.service_provider import ServiceProvider
from services.enums import Services
from fwo_exceptions import FwoImporterErrorInconsistencies


# this class is used for importing a config into the FWO API
class FwConfigImportCheckConsistency(FwConfigImport):
    issues: dict[str, Any] = {}
    maps: FwConfigImportObject # = FwConfigImportObject()
    config: FwConfigNormalizedController = FwConfigNormalizedController(ConfFormat.NORMALIZED, FwConfigNormalized())

    # merges all configs in the set together to prepare for consistency checks
    def __init__(self, import_details: ImportStateController, config_list: FwConfigManagerListController):
        service_provider = ServiceProvider()
        self._global_state = service_provider.get_service(Services.GLOBAL_STATE)
        self.import_state = import_details

        self.maps = FwConfigImportObject() # TODO don't use like this (separation of concerns) - see #3154
        for mgr in config_list.ManagerSet:
            for cfg in mgr.Configs:
                import_worker = FwConfigImport()
                self.config.merge(cfg)
                self.maps.NetworkObjectTypeMap.update(import_worker.fwconfig_import_object.NetworkObjectTypeMap)
                self.maps.ServiceObjectTypeMap.update(import_worker.fwconfig_import_object.ServiceObjectTypeMap)
                self.maps.UserObjectTypeMap.update(import_worker.fwconfig_import_object.UserObjectTypeMap)


    # pre-flight checks
    def checkConfigConsistency(self, config: FwConfigManagerListController):
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
            get_fwo_logger().info("Consistency check completed without issues.")


    def checkNetworkObjectConsistency(self, config: FwConfigManagerListController):
        # check if all uid refs are valid
        global_objects: set[str] = set()
        single_config: FwConfigNormalized

        # add all new obj refs from all rules
        for mgr in sorted(config.ManagerSet, key=lambda m: not getattr(m, 'IsSuperManager', False)):
            if mgr.IsSuperManager:
                global_objects = config.get_all_network_object_uids(mgr.ManagerUid)
            all_used_obj_refs: list[str] = []
            for single_config in mgr.Configs:
                for rb in single_config.rulebases:
                    all_used_obj_refs += self._collect_all_used_objects_from_rules(rb)
                
                all_used_obj_refs += self._collect_all_used_objects_from_groups(single_config)

            # now make list unique and get all refs not contained in network_objects
            unresolvable_nw_obj_refs = set(all_used_obj_refs) - config.get_all_network_object_uids(mgr.ManagerUid) - global_objects
            if len(unresolvable_nw_obj_refs)>0:
                self.issues.update({'unresolvableNwObRefs': list(unresolvable_nw_obj_refs)})

            self._check_network_object_types_exist(mgr)
            self._check_objects_with_missing_ips(mgr)


    def _check_network_object_types_exist(self, mgr: FwConfigManager):
        allUsedObjTypes: set[str] = set()

        for single_config in mgr.Configs:            
            for objId in single_config.network_objects:
                allUsedObjTypes.add(single_config.network_objects[objId].obj_typ)
            missingNwObjTypes = allUsedObjTypes - self.maps.NetworkObjectTypeMap.keys()
            if len(missingNwObjTypes)>0:
                self.issues.update({'unresolvableNwObjTypes': list(missingNwObjTypes)})


    @staticmethod
    def _collect_all_used_objects_from_groups(single_config: FwConfigNormalized) -> list[str]:
        all_used_obj_refs: list[str] = []
        # add all nw obj refs from groups
        for obj_id in single_config.network_objects:
            if single_config.network_objects[obj_id].obj_typ=='group':
                obj_member_refs = single_config.network_objects[obj_id].obj_member_refs
                if obj_member_refs is not None and len(obj_member_refs)>0:
                    all_used_obj_refs += obj_member_refs.split(fwo_const.list_delimiter)
        return all_used_obj_refs
    

    def _collect_all_used_objects_from_rules(self, rb: Rulebase) -> list[str]:
        all_used_obj_refs: list[str] = []
        for rule_id in rb.rules:
            all_used_obj_refs += rb.rules[rule_id].rule_src_refs.split(fwo_const.list_delimiter)
            all_used_obj_refs += rb.rules[rule_id].rule_dst_refs.split(fwo_const.list_delimiter)
        
        return all_used_obj_refs


    def _check_objects_with_missing_ips(self, single_config: FwConfigManager):
        # check if there are any objects with obj_typ<>group and empty ip addresses (breaking constraint)
        nonGroupNwObjWithMissingIps: list[NetworkObject] = []
        for conf in single_config.Configs:
            for objId in conf.network_objects:
                if conf.network_objects[objId].obj_typ!='group':
                    ip1 = conf.network_objects[objId].obj_ip
                    ip2 = conf.network_objects[objId].obj_ip_end
                    if ip1==None or ip2==None:
                        nonGroupNwObjWithMissingIps.append(conf.network_objects[objId])
        if len(nonGroupNwObjWithMissingIps)>0:
            self.issues.update({'non-group network object with undefined IP addresse(s)': list(nonGroupNwObjWithMissingIps)})


    def checkServiceObjectConsistency(self, config: FwConfigManagerListController):
        # check if all uid refs are valid
        global_objects: set[str] = set()

        for mgr in sorted(config.ManagerSet, key=lambda m: not getattr(m, 'IsSuperManager', False)):
            if len(mgr.Configs)==0:
                continue
            if mgr.IsSuperManager:
                global_objects = config.get_all_service_object_uids(mgr.ManagerUid)
            all_used_obj_refs: set[str] = set()
            for single_config in mgr.Configs:
                all_used_obj_refs |= self._collect_service_object_refs_from_rules(single_config)
                all_used_obj_refs |= self._collect_all_service_object_refs_from_groups(single_config)
                self._check_service_object_types_exist(single_config)
                # now make list unique 
                all_used_obj_refs = set(all_used_obj_refs)
                # and get all refs not contained in serivce_objects

            unresolvableObRefs = all_used_obj_refs - config.get_all_service_object_uids(mgr.ManagerUid) - global_objects
            if len(unresolvableObRefs)>0:
                self.issues.update({'unresolvableSvcObRefs': list(unresolvableObRefs)})



    def _check_service_object_types_exist(self, single_config: FwConfigNormalized):
        # check that all obj_typ exist 
        all_used_obj_types: set[str] = set()
        for obj_id in single_config.service_objects:
            all_used_obj_types.add(single_config.service_objects[obj_id].svc_typ)
        missing_obj_types = list(all_used_obj_types) - self.maps.ServiceObjectTypeMap.keys()
        if len(missing_obj_types)>0:
            self.issues.update({'unresolvableSvcObjTypes': list(missing_obj_types)})


    @staticmethod
    def _collect_all_service_object_refs_from_groups(single_config: FwConfigNormalized) -> set[str]:
        all_used_obj_refs: set[str] = set()
        for objId in single_config.service_objects:
            if single_config.service_objects[objId].svc_typ=='group':
                if single_config.service_objects[objId].svc_member_refs is not None:
                    member_refs = single_config.service_objects[objId].svc_member_refs
                    if member_refs is None or len(member_refs) == 0:
                        continue
                    all_used_obj_refs |= set(member_refs.split(fwo_const.list_delimiter))
        return all_used_obj_refs


    @staticmethod
    def _collect_service_object_refs_from_rules(single_config: FwConfigNormalized) -> set[str]:
        all_used_obj_refs: set[str] = set()
        for rb in single_config.rulebases:
            for ruleId in rb.rules:
                all_used_obj_refs |= set(rb.rules[ruleId].rule_svc_refs.split(fwo_const.list_delimiter))
        return all_used_obj_refs


    def checkUserObjectConsistency(self, config: FwConfigManagerListController):
        global_objects: set[str] = set()
        # add all user refs from all rules
        for mgr in sorted(config.ManagerSet, key=lambda m: not getattr(m, 'IsSuperManager', False)):
            all_used_obj_refs: list[str] = []
            if mgr.IsSuperManager:
                global_objects = config.get_all_user_object_uids(mgr.ManagerUid)
            for single_config in mgr.Configs:
                all_used_obj_refs += self._collect_users_from_rules(single_config)
                self._collect_users_from_groups(single_config, all_used_obj_refs)
                self._check_user_types_exist(single_config)

            # now make list unique and get all refs not contained in users
            unresolvable_obj_refs = set(all_used_obj_refs) - config.get_all_user_object_uids(mgr.ManagerUid) - global_objects
            if len(unresolvable_obj_refs)>0:
                self.issues.update({'unresolvableUserObjRefs': list(unresolvable_obj_refs)})


    def _collect_users_from_rules(self, single_config: FwConfigNormalized) -> list[str]:
        all_used_obj_refs: list[str] = []
        for rb in single_config.rulebases:
            for ruleId in rb.rules:
                if fwo_const.user_delimiter in rb.rules[ruleId].rule_src_refs:
                    all_used_obj_refs += self._collectUsersFromRule(rb.rules[ruleId].rule_src_refs.split(fwo_const.list_delimiter))
                    all_used_obj_refs += self._collectUsersFromRule(rb.rules[ruleId].rule_dst_refs.split(fwo_const.list_delimiter))
        return all_used_obj_refs


    def _collect_users_from_groups(self, single_config: FwConfigNormalized, all_used_obj_refs: list[str]):
        return


    def _check_user_types_exist(self, single_config: FwConfigNormalized):
        # check that all obj_typ exist 
        allUsedObjTypes: set[str] = set()
        for objId in single_config.users:
            allUsedObjTypes.add(single_config.users[objId].user_typ)  # make list unique
        missingObjTypes = list(set(allUsedObjTypes)) - self.maps.UserObjectTypeMap.keys() #TODO: why list(set())?
        if len(missingObjTypes)>0:
            self.issues.update({'unresolvableUserObjTypes': list(missingObjTypes)})


    @staticmethod
    def _collectUsersFromRule(listOfElements: list[str]) -> list[str]:
        userRefs: list[str] = []
        for el in listOfElements:
            splitResult = el.split(fwo_const.user_delimiter)
            if len(splitResult)==2:
                userRefs.append(splitResult[0])
        return userRefs

    
    def checkZoneObjectConsistency(self, config: FwConfigManagerListController):

        global_objects: set[str] = set()
        for mgr in sorted(config.ManagerSet, key=lambda m: not getattr(m, 'IsSuperManager', False)):
            if len(mgr.Configs)==0:
                continue
            if mgr.IsSuperManager:
                global_objects = config.get_all_zone_names(mgr.ManagerUid)

            all_used_obj_refs: set[str] = set()
            for single_config in mgr.Configs:
                all_used_obj_refs |= self._collect_zone_refs_from_rules(single_config)
                # now make list unique 
                all_used_obj_refs = set(all_used_obj_refs)

            # and get all refs not contained in zone_objects
            unresolvable_object_refs = all_used_obj_refs - config.get_all_zone_names(mgr.ManagerUid) - global_objects
            if len(unresolvable_object_refs)>0:
                self.issues.update({'unresolvableZoneObRefs': list(unresolvable_object_refs)})

   
    @staticmethod
    def _collect_zone_refs_from_rules(single_config: FwConfigNormalized) -> set[str]:
        all_used_zones_refs: set[str] = set()
        for rb in single_config.rulebases:
            for rule_id in rb.rules:
                rule = rb.rules[rule_id]
                if rule.rule_src_zone is not None:
                    all_used_zones_refs.update(rule.rule_src_zone.split(fwo_const.list_delimiter))
                if rule.rule_dst_zone is not None:
                    all_used_zones_refs.update(rule.rule_dst_zone.split(fwo_const.list_delimiter))
        return all_used_zones_refs


    # check if all color refs are valid (in the DB)
    # fix=True means that missing color refs will be replaced by the default color (black)
    def checkColorConsistency(self, config: FwConfigManagerListController, fix: bool = True):
        self.import_state.SetColorRefMap(self.import_state.api_call)
        
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
    def _collect_all_used_colors(single_config: FwConfigNormalized):
        allUsedNwObjColorRefSet: set[str] = set()
        allUsedSvcColorRefSet: set[str] = set()
        allUsedUserColorRefSet: set[str] = set()

        for uid in single_config.network_objects:
            if single_config.network_objects[uid].obj_color is not None: # type: ignore #TODO: obj_color cant be None
                allUsedNwObjColorRefSet.add(single_config.network_objects[uid].obj_color)
        for uid in single_config.service_objects:
            if single_config.service_objects[uid].svc_color is not None: # type: ignore #TODO: svc_color cant be None
                allUsedSvcColorRefSet.add(single_config.service_objects[uid].svc_color)
        for uid in single_config.users:
            if single_config.users[uid].user_color is not None:
                allUsedUserColorRefSet.add(single_config.users[uid].user_color)

        return allUsedNwObjColorRefSet, allUsedSvcColorRefSet, allUsedUserColorRefSet


    def _check_resolvability_of_used_colors(self, allUsedNwObjColorRefSet: set[str], allUsedSvcColorRefSet: set[str], allUsedUserColorRefSet: set[str]):
        unresolvableNwObjColors: list[str] = []
        unresolvableSvcColors: list[str] = []
        unresolvableUserColors: list[str] = []
        # check all nwobj color refs
        for color_string in allUsedNwObjColorRefSet:
            color_id = self.import_state.lookupColorId(color_string)
            if color_id is None: # type: ignore # TODO: lookupColorId cant return None
                unresolvableNwObjColors.append(color_string)

        # check all nwobj color refs
        for color_string in allUsedSvcColorRefSet:
            color_id = self.import_state.lookupColorId(color_string)
            if color_id is None:    # type: ignore # TODO: lookupColorId cant return None
                unresolvableSvcColors.append(color_string)

        # check all user color refs
        for color_string in allUsedUserColorRefSet:
            color_id = self.import_state.lookupColorId(color_string)
            if color_id is None:   # type: ignore # TODO: lookupColorId cant return None
                unresolvableUserColors.append(color_string)
        
        return unresolvableNwObjColors, unresolvableSvcColors, unresolvableUserColors


    @staticmethod
    def _fix_colors(config: FwConfigNormalized, unresolvable_nw_obj_colors: list[str], unresolvable_svc_colors: list[str], unresolvable_user_colors: list[str]):
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
    def _extract_rule_track_n_action_refs(rulebases: list[Rulebase]) -> tuple[list[str], list[str]]:
        track_refs: list[str] = []
        action_refs: list[str] = []
        for rb in rulebases:
            track_refs.extend(rule.rule_track for rule in rb.rules.values())
            action_refs.extend(rule.rule_action for rule in rb.rules.values())
        return track_refs, action_refs


    def check_rulebase_consistency(self, config: FwConfigManagerListController):
        all_used_track_refs: list[str] = []
        all_used_action_refs: list[str] = []

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
    def check_gateway_consistency(self, config: FwConfigManagerListController):
        # TODO: implement
        pass


    # e.g. check rule to rule refs
    # TODO: check if the rule & rulebases referenced belong to either 
    #       - the same submanager or 
    #       - the super manager but not another sub manager
    def check_rulebase_link_consistency(self, config: FwConfigManagerListController):
        broken_rulebase_links: list[dict[str, Any]] = []

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


    def _check_rulebase_links_for_gateway(self, gw: Gateway, broken_rulebase_links: list[dict[str, Any]], all_rule_uids: set[str], all_rulebase_uids: set[str]):
        if not gw.ImportDisabled:
            for rbl in gw.RulebaseLinks:
                self._check_rulebase_link(gw, rbl, broken_rulebase_links, all_rule_uids, all_rulebase_uids)


    def _collect_uids(self, config: FwConfigManagerListController):
        all_rulebase_uids: set[str] = set()
        all_rule_uids: set[str] = set()
        for mgr in config.ManagerSet:
            if self.import_state.MgmDetails.ImportDisabled:
                continue
            for single_config in mgr.Configs:        
                # collect rulebase UIDs
                for rb in single_config.rulebases:
                    all_rulebase_uids.add(rb.uid)
                    # collect rule UIDs
                    for rule_uid in rb.rules:
                        all_rule_uids.add(rule_uid)
        return all_rulebase_uids, all_rule_uids


    def _check_rulebase_link(self, gw: Gateway, rbl: RulebaseLinkUidBased, broken_rulebase_links: list[dict[str, Any]], all_rule_uids: set[str], all_rulebase_uids: set[str]):
        if rbl.from_rulebase_uid is not None and rbl.from_rulebase_uid != '' and rbl.from_rulebase_uid not in all_rulebase_uids:
            self._add_issue(broken_rulebase_links, rbl, gw, 'from_rulebase_uid broken')
        if rbl.to_rulebase_uid is not None and rbl.to_rulebase_uid != '' and rbl.to_rulebase_uid not in all_rulebase_uids: # type: ignore # TODO: to_rulebase_uid cant be None 
            self._add_issue(broken_rulebase_links, rbl, gw, 'to_rulebase_uid broken')
        if rbl.from_rule_uid is not None and rbl.from_rule_uid != '' and rbl.from_rule_uid not in all_rule_uids:
            self._add_issue(broken_rulebase_links, rbl, gw, 'from_rule_uid broken')

    @staticmethod
    def _add_issue(broken_rulebase_links: list[dict[str, Any]], rbl: RulebaseLinkUidBased, gw: Gateway, error_txt: str):
            rbl_dict = rbl.toDict()
            rbl_dict.update({'error': error_txt})
            rbl_dict.update({'gw': f'{gw.Name} ({gw.Uid})'})
            if rbl_dict not in broken_rulebase_links:
                broken_rulebase_links.append(rbl_dict)

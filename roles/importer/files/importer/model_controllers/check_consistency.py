import json

import fwo_const
from fwo_log import getFwoLogger
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from model_controllers.fwconfig_import_object import FwConfigImportObject
from models.fwconfig_normalized import FwConfigNormalized
from fwo_base import ConfFormat
from services.service_provider import ServiceProvider
from services.enums import Services
import fwo_exceptions


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
            raise fwo_exceptions.FwoImporterErrorInconsistencies("Inconsistencies found in the configuration.")

        if self.import_state.DebugLevel >= 1:
            getFwoLogger().info("Consistency check completed without issues.")


    def checkNetworkObjectConsistency(self, config: FwConfigNormalized = None):
        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all new obj refs from all rules

        for mgr in config.ManagerSet:
            for single_config in mgr.Configs:

                for rb in single_config.rulebases:
                    allUsedObjRefs = self._collect_all_used_objects_from_rules(rb)
                
                allUsedObjRefs += self._collect_all_used_objects_from_groups(single_config)

                # now make list unique and get all refs not contained in network_objects
                allUsedObjRefsUnique = list(set(allUsedObjRefs))

                unresolvableNwObRefs = allUsedObjRefsUnique - single_config.network_objects.keys()
                if len(unresolvableNwObRefs)>0:
                    self.issues.update({'unresolvableNwObRefs': list(unresolvableNwObRefs)})

                # check that all obj_typ exist 
                allUsedObjTypes = set()
                for objId in single_config.network_objects:
                    allUsedObjTypes.add(single_config.network_objects[objId].obj_typ)
                allUsedObjTypes = list(set(allUsedObjTypes))
                missingNwObjTypes = allUsedObjTypes - self.maps.NetworkObjectTypeMap.keys()
                if len(missingNwObjTypes)>0:
                    self.issues.update({'unresolvableNwObjTypes': list(missingNwObjTypes)})

                self._check_objects_with_missing_ips(single_config)

    @staticmethod
    def _collect_all_used_objects_from_groups(single_config):
        allUsedObjRefs = []
        # add all nw obj refs from groups
        for objId in single_config.network_objects:
            if single_config.network_objects[objId].obj_typ=='group':
                if single_config.network_objects[objId].obj_member_refs is not None:
                    allUsedObjRefs += single_config.network_objects[objId].obj_member_refs.split(fwo_const.list_delimiter)
        return allUsedObjRefs
    

    def _collect_all_used_objects_from_rules(self, rb):
        allUsedObjRefs = []
        for ruleId in rb.Rules:
            allUsedObjRefs += rb.Rules[ruleId].rule_src_refs.split(fwo_const.list_delimiter)
            allUsedObjRefs += rb.Rules[ruleId].rule_dst_refs.split(fwo_const.list_delimiter)
        
        return allUsedObjRefs


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


    def checkServiceObjectConsistency(self, config: FwConfigNormalized = None):
        # check if all uid refs are valid
        allUsedObjRefs = []

        for mgr in config.ManagerSet:
            for single_config in mgr.Configs:
                allUsedObjRefs += self._collect_service_object_refs_from_rules(single_config)

                # add all svc obj refs from groups
                allUsedObjRefs += self._collect_all_service_object_refs_from_groups(single_config)

                # now make list unique and get all refs not contained in service_objects
                allUsedObjRefsUnique = list(set(allUsedObjRefs))
                unresolvableObRefs = allUsedObjRefsUnique - single_config.service_objects.keys()
                if len(unresolvableObRefs)>0:
                    self.issues.update({'unresolvableSvcObRefs': list(unresolvableObRefs)})

                # check that all obj_typ exist 
                allUsedObjTypes = set()
                for objId in single_config.service_objects:
                    allUsedObjTypes.add(single_config.service_objects[objId].svc_typ)
                allUsedObjTypes = list(set(allUsedObjTypes))
                missingObjTypes = allUsedObjTypes - self.maps.ServiceObjectTypeMap.keys()
                if len(missingObjTypes)>0:
                    self.issues.update({'unresolvableSvcObjTypes': list(missingObjTypes)})

    @staticmethod
    def _collect_all_service_object_refs_from_groups(single_config):
        allUsedObjRefs = []
        for objId in single_config.service_objects:
            if single_config.service_objects[objId].svc_typ=='group':
                if single_config.service_objects[objId].svc_member_refs is not None:
                    allUsedObjRefs += single_config.service_objects[objId].svc_member_refs.split(fwo_const.list_delimiter)
        return allUsedObjRefs

    @staticmethod
    def _collect_service_object_refs_from_rules(single_config):
        allUsedObjRefs = []
        for rb in single_config.rulebases:
            for ruleId in rb.Rules:
                allUsedObjRefs += rb.Rules[ruleId].rule_svc_refs.split(fwo_const.list_delimiter)
        return allUsedObjRefs


    def checkUserObjectConsistency(self, config: FwConfigNormalized = None):
        allUsedObjRefs = []
        # add all user refs from all rules
        for mgr in config.ManagerSet:
            for single_config in mgr.Configs:
                for rb in single_config.rulebases:
                    for ruleId in rb.Rules:
                        if fwo_const.user_delimiter in rb.Rules[ruleId].rule_src_refs:
                            allUsedObjRefs += self._collectUsersFromRule(rb.Rules[ruleId].rule_src_refs.split(fwo_const.list_delimiter))
                            allUsedObjRefs += self._collectUsersFromRule(rb.Rules[ruleId].rule_dst_refs.split(fwo_const.list_delimiter))

                self._collect_users_from_groups(single_config, allUsedObjRefs)

                # now make list unique and get all refs not contained in users
                allUsedObjRefsUnique = list(set(allUsedObjRefs))
                unresolvableObRefs = allUsedObjRefsUnique - single_config.users.keys()
                if len(unresolvableObRefs)>0:
                    self.issues.update({'unresolvableUserObRefs': list(unresolvableObRefs)})

                self._check_user_types_exist(single_config)

    def _collect_users_from_groups(self, single_config, allUsedObjRefs):
        for objId in single_config.users:
            if self.users[objId].user_typ=='group':
                if self.users[objId].user_member_refs is not None:
                    allUsedObjRefs += self.users[objId].user_member_refs.split(fwo_const.list_delimiter)


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

    
    def checkZoneObjectConsistency(self, config: FwConfigNormalized = None):
        # check if all uid refs are valid
        allUsedObjRefs = []

        for mgr in config.ManagerSet:
            for single_config in mgr.Configs:
                # add all zone refs from all rules
                for rb in single_config.rulebases:
                    for ruleId in rb.Rules:
                        if rb.Rules[ruleId].rule_src_zone is not None:
                            allUsedObjRefs += rb.Rules[ruleId].rule_src_zone
                        if rb.Rules[ruleId].rule_dst_zone is not None:
                            allUsedObjRefs += rb.Rules[ruleId].rule_dst_zone

                # we currently do not have zone groups - skipping group ref handling

                # now make list unique and get all refs not contained in zone_objects
                allUsedObjRefsUnique = list(set(allUsedObjRefs))
                unresolvableObRefs = allUsedObjRefsUnique - single_config.zone_objects.keys()
                if len(unresolvableObRefs)>0:
                    self.issues.update({'unresolvableZoneObRefs': list(unresolvableObRefs)})

                # we currently do not have zone types - skipping type handling
    
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


    def _collect_all_used_colors(self, single_config):
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


    def _fix_colors(self, config, unresolvable_nw_obj_colors, unresolvable_svc_colors, unresolvable_user_colors):
        for color_string in unresolvable_nw_obj_colors:
            # replace with default color
            for uid in config.network_objects:
                if config.network_objects[uid].obj_color==color_string:
                    config.network_objects[uid].obj_color = fwo_const.defaultColor
        for color_string in unresolvable_svc_colors:
            # replace with default color
            for uid in config.service_objects:
                if config.service_objects[uid].svc_color==color_string:
                    config.service_objects[uid].svc_color = fwo_const.defaultColor
        for color_string in unresolvable_user_colors:
            # replace with default color
            for uid in config.users:
                if config.users[uid].user_color==color_string:
                    config.users[uid].user_color = fwo_const.defaultColor


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
    def check_rulebase_link_consistency(self, config: FwConfigNormalized = None):
        # TODO: implement
        pass
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
from services.global_state import GlobalState
from services.enums import Services


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
        self.checkRuleConsistency(config)
        self.checkGatewayConsistency(config)
        if len(self.issues)>0:
            logger = getFwoLogger()
            logger.warning(f'config not imported due to the following inconsistencies: {json.dumps(self.issues, indent=3)}')
            self.import_state.increaseErrorCounterByOne()

        return self.issues

    def checkNetworkObjectConsistency(self, config: FwConfigNormalized = None):
        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all new obj refs from all rules

        for mgr in config.ManagerSet:
            for single_config in mgr.Configs:
                for rb in single_config.rulebases:
                    for ruleId in rb.Rules:
                        allUsedObjRefs += rb.Rules[ruleId].rule_src_refs.split(fwo_const.list_delimiter)
                        allUsedObjRefs += rb.Rules[ruleId].rule_dst_refs.split(fwo_const.list_delimiter)

                # add all nw obj refs from groups
                for objId in single_config.network_objects:
                    if single_config.network_objects[objId].obj_typ=='group':
                        if single_config.network_objects[objId].obj_member_refs is not None:
                            allUsedObjRefs += single_config.network_objects[objId].obj_member_refs.split(fwo_const.list_delimiter)

                    # TODO: also check color

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
                # add all new obj refs from all rules
                for rb in single_config.rulebases:
                    for ruleId in rb.Rules:
                        allUsedObjRefs += rb.Rules[ruleId].rule_svc_refs.split(fwo_const.list_delimiter)

                # add all svc obj refs from groups
                for objId in single_config.service_objects:
                    if single_config.service_objects[objId].svc_typ=='group':
                        if single_config.service_objects[objId].svc_member_refs is not None:
                            allUsedObjRefs += single_config.service_objects[objId].svc_member_refs.split(fwo_const.list_delimiter)

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

    
    def checkUserObjectConsistency(self, config: FwConfigNormalized = None):

        def collectUsersFromRule(listOfElements):
            userRefs = []
            for el in listOfElements:
                splitResult = el.split(fwo_const.user_delimiter)
                if len(splitResult)==2:
                    allUsedObjRefs.append(splitResult[0])
            return userRefs

        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all user refs from all rules
        for mgr in config.ManagerSet:
            for single_config in mgr.Configs:
                for rb in single_config.rulebases:
                    for ruleId in rb.Rules:
                        if fwo_const.user_delimiter in rb.Rules[ruleId].rule_src_refs:
                            allUsedObjRefs += collectUsersFromRule(rb.Rules[ruleId].rule_src_refs.split(fwo_const.list_delimiter))
                            allUsedObjRefs += collectUsersFromRule(rb.Rules[ruleId].rule_dst_refs.split(fwo_const.list_delimiter))

                # add all user obj refs from groups
                for objId in single_config.users:
                    if self.users[objId].user_typ=='group':
                        if self.users[objId].user_member_refs is not None:
                            allUsedObjRefs += self.users[objId].user_member_refs.split(fwo_const.list_delimiter)

                # now make list unique and get all refs not contained in users
                allUsedObjRefsUnique = list(set(allUsedObjRefs))
                unresolvableObRefs = allUsedObjRefsUnique - single_config.users.keys()
                if len(unresolvableObRefs)>0:
                    self.issues.update({'unresolvableUserObRefs': list(unresolvableObRefs)})

                # check that all obj_typ exist 
                allUsedObjTypes = set()
                for objId in single_config.users:
                    allUsedObjTypes.add(single_config.users[objId].user_typ)
                allUsedObjTypes = list(set(allUsedObjTypes))    # make list unique
                missingObjTypes = allUsedObjTypes - self.maps.UserObjectTypeMap.keys()
                if len(missingObjTypes)>0:
                    self.issues.update({'unresolvableUserObjTypes': list(missingObjTypes)})

    
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
        allUsedNwObjColorRefSet = set()
        allUsedSvcColorRefSet = set()
        allUsedUserColorRefSet = set()
        unresolvableNwObjColors = []
        unresolvableSvcColors = []
        unresolvableUserColors = []

        self.import_state.SetColorRefMap()
        
        # collect all colors

        for mgr in config.ManagerSet:
            for single_config in mgr.Configs:
                for uid in single_config.network_objects:
                    if single_config.network_objects[uid].obj_color is not None:
                        allUsedNwObjColorRefSet.add(single_config.network_objects[uid].obj_color)
                for uid in single_config.service_objects:
                    if single_config.service_objects[uid].svc_color is not None:
                        allUsedSvcColorRefSet.add(single_config.service_objects[uid].svc_color)
                for uid in single_config.users:
                    if single_config.users[uid].user_color is not None:
                        allUsedUserColorRefSet.add(single_config.users[uid].user_color)

                # check all nwobj color refs
                for colorString in allUsedNwObjColorRefSet:
                    colorId = self.import_state.lookupColorId(colorString)
                    if colorId is None:
                        unresolvableNwObjColors.append(colorString)

                # check all nwobj color refs
                for colorString in allUsedSvcColorRefSet:
                    colorId = self.import_state.lookupColorId(colorString)
                    if colorId is None:
                        unresolvableSvcColors.append(colorString)

                # check all user color refs
                for colorString in allUsedUserColorRefSet:
                    colorId = self.import_state.lookupColorId(colorString)
                    if colorId is None:
                        unresolvableUserColors.append(colorString)

                if fix:
                    for colorString in unresolvableNwObjColors:
                        # replace with default color
                        for uid in single_config.network_objects:
                            if single_config.network_objects[uid].obj_color==colorString:
                                single_config.network_objects[uid].obj_color = fwo_const.defaultColor
                    for colorString in unresolvableSvcColors:
                        # replace with default color
                        for uid in single_config.service_objects:
                            if single_config.service_objects[uid].svc_color==colorString:
                                single_config.service_objects[uid].svc_color = fwo_const.defaultColor
                    for colorString in unresolvableUserColors:
                        # replace with default color
                        for uid in single_config.users:
                            if single_config.users[uid].user_color==colorString:
                                single_config.users[uid].user_color = fwo_const.defaultColor

                elif len(unresolvableNwObjColors)>0 or len(unresolvableSvcColors)>0 or len(unresolvableUserColors)>0:
                    self.issues.update({ 'unresolvableColorRefs': 
                        {'nwObjColors': unresolvableNwObjColors, 'svcColors': unresolvableSvcColors, 'userColors': unresolvableUserColors}})

    # e.g. check rule to rule refs
    def checkRuleConsistency(self, config: FwConfigNormalized = None):
        # TODO: implement
        pass

    # e.g. check rule to rule refs
    def checkGatewayConsistency(self, config: FwConfigNormalized = None):
        # TODO: implement
        pass
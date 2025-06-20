import json

import fwo_const
from fwo_log import getFwoLogger
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.fwconfig_import_object import FwConfigImportObject


# this class is used for importing a config into the FWO API
class FwConfigImportCheckConsistency(FwConfigImport):
    

    def __init__(self, config: FwConfigImport):
        self.ImportDetails = config.ImportDetails
        self.NormalizedConfig = config.NormalizedConfig
        self.NetworkObjectTypeMap = config.fwconfig_import_object.NetworkObjectTypeMap
        self.ServiceObjectTypeMap = config.fwconfig_import_object.ServiceObjectTypeMap
        self.UserObjectTypeMap = config.fwconfig_import_object.UserObjectTypeMap

        
    # pre-flight checks
    def checkConfigConsistency(self):
        issues = {}
        issues.update(self.checkColorConsistency(fix=True))
        issues.update(self.checkNetworkObjectConsistency())
        issues.update(self.checkServiceObjectConsistency())
        issues.update(self.checkUserObjectConsistency())
        issues.update(self.checkZoneObjectConsistency())
        issues.update(self.checkRuleConsistency())
        issues.update(self.checkGatewayConsistency())
        if len(issues)>0:
            logger = getFwoLogger()
            logger.warning(f'config not imported due to the following inconsistencies: {json.dumps(issues, indent=3)}')
            self.ImportDetails.increaseErrorCounterByOne()

        return issues

    def checkNetworkObjectConsistency(self):
        issues = {}
        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all new obj refs from all rules
        for rb in self.NormalizedConfig.rulebases:
            for ruleId in rb.Rules:
                allUsedObjRefs += rb.Rules[ruleId].rule_src_refs.split(fwo_const.list_delimiter)
                allUsedObjRefs += rb.Rules[ruleId].rule_dst_refs.split(fwo_const.list_delimiter)

        # add all nw obj refs from groups
        for objId in self.NormalizedConfig.network_objects:
            if self.NormalizedConfig.network_objects[objId].obj_typ=='group':
                if self.NormalizedConfig.network_objects[objId].obj_member_refs is not None:
                    allUsedObjRefs += self.NormalizedConfig.network_objects[objId].obj_member_refs.split(fwo_const.list_delimiter)

            # TODO: also check color

        # now make list unique and get all refs not contained in network_objects
        allUsedObjRefsUnique = list(set(allUsedObjRefs))
        unresolvableNwObRefs = allUsedObjRefsUnique - self.NormalizedConfig.network_objects.keys()
        if len(unresolvableNwObRefs)>0:
            issues.update({'unresolvableNwObRefs': list(unresolvableNwObRefs)})

        # check that all obj_typ exist 
        allUsedObjTypes = set()
        for objId in self.NormalizedConfig.network_objects:
            allUsedObjTypes.add(self.NormalizedConfig.network_objects[objId].obj_typ)
        allUsedObjTypes = list(set(allUsedObjTypes))
        missingNwObjTypes = allUsedObjTypes - self.NetworkObjectTypeMap.keys()
        if len(missingNwObjTypes)>0:
            issues.update({'unresolvableNwObjTypes': list(missingNwObjTypes)})


        # check if there are any objects with obj_typ<>group and empty ip addresses (breaking constraint)
        nonGroupNwObjWithMissingIps = []
        for objId in self.NormalizedConfig.network_objects:
            if self.NormalizedConfig.network_objects[objId].obj_typ!='group':
                ip1 = self.NormalizedConfig.network_objects[objId].obj_ip
                ip2 = self.NormalizedConfig.network_objects[objId].obj_ip_end
                if ip1==None or ip2==None:
                    nonGroupNwObjWithMissingIps.append(self.NormalizedConfig.network_objects[objId])
        if len(nonGroupNwObjWithMissingIps)>0:
            issues.update({'non-group network object with undefined IP addresse(s)': list(nonGroupNwObjWithMissingIps)})

        return issues
    
    def checkServiceObjectConsistency(self):
        issues = {}
        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all new obj refs from all rules
        for rb in self.NormalizedConfig.rulebases:
            for ruleId in rb.Rules:
                allUsedObjRefs += rb.Rules[ruleId].rule_svc_refs.split(fwo_const.list_delimiter)

        # add all svc obj refs from groups
        for objId in self.NormalizedConfig.service_objects:
            if self.NormalizedConfig.service_objects[objId].svc_typ=='group':
                if self.NormalizedConfig.service_objects[objId].svc_member_refs is not None:
                    allUsedObjRefs += self.NormalizedConfig.service_objects[objId].svc_member_refs.split(fwo_const.list_delimiter)

        # now make list unique and get all refs not contained in service_objects
        allUsedObjRefsUnique = list(set(allUsedObjRefs))
        unresolvableObRefs = allUsedObjRefsUnique - self.NormalizedConfig.service_objects.keys()
        if len(unresolvableObRefs)>0:
            issues.update({'unresolvableSvcObRefs': list(unresolvableObRefs)})

        # check that all obj_typ exist 
        allUsedObjTypes = set()
        for objId in self.NormalizedConfig.service_objects:
            allUsedObjTypes.add(self.NormalizedConfig.service_objects[objId].svc_typ)
        allUsedObjTypes = list(set(allUsedObjTypes))
        missingObjTypes = allUsedObjTypes - self.ServiceObjectTypeMap.keys()
        if len(missingObjTypes)>0:
            issues.update({'unresolvableSvcObjTypes': list(missingObjTypes)})

        return issues
    
    def checkUserObjectConsistency(self):

        def collectUsersFromRule(listOfElements):
            userRefs = []
            for el in listOfElements:
                splitResult = el.split(fwo_const.user_delimiter)
                if len(splitResult)==2:
                    allUsedObjRefs.append(splitResult[0])
            return userRefs

        issues = {}
        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all user refs from all rules
        for rb in self.NormalizedConfig.rulebases:
            for ruleId in rb.Rules:
                if fwo_const.user_delimiter in rb.Rules[ruleId].rule_src_refs:
                    allUsedObjRefs += collectUsersFromRule(rb.Rules[ruleId].rule_src_refs.split(fwo_const.list_delimiter))
                    allUsedObjRefs += collectUsersFromRule(rb.Rules[ruleId].rule_dst_refs.split(fwo_const.list_delimiter))

        # add all user obj refs from groups
        for objId in self.NormalizedConfig.users:
            if self.users[objId].user_typ=='group':
                if self.users[objId].user_member_refs is not None:
                    allUsedObjRefs += self.users[objId].user_member_refs.split(fwo_const.list_delimiter)

        # now make list unique and get all refs not contained in users
        allUsedObjRefsUnique = list(set(allUsedObjRefs))
        unresolvableObRefs = allUsedObjRefsUnique - self.NormalizedConfig.users.keys()
        if len(unresolvableObRefs)>0:
            issues.update({'unresolvableUserObRefs': list(unresolvableObRefs)})

        # check that all obj_typ exist 
        allUsedObjTypes = set()
        for objId in self.NormalizedConfig.users:
            allUsedObjTypes.add(self.NormalizedConfig.users[objId].user_typ)
        allUsedObjTypes = list(set(allUsedObjTypes))    # make list unique
        missingObjTypes = allUsedObjTypes - self.UserObjectTypeMap.keys()
        if len(missingObjTypes)>0:
            issues.update({'unresolvableUserObjTypes': list(missingObjTypes)})

        return issues
    
    def checkZoneObjectConsistency(self):
        issues = {}
        # check if all uid refs are valid
        allUsedObjRefs = []
        # add all zone refs from all rules
        for rb in self.NormalizedConfig.rulebases:
            for ruleId in rb.Rules:
                if rb.Rules[ruleId].rule_src_zone is not None:
                    allUsedObjRefs += rb.Rules[ruleId].rule_src_zone
                if rb.Rules[ruleId].rule_dst_zone is not None:
                    allUsedObjRefs += rb.Rules[ruleId].rule_dst_zone

        # we currently do not have zone groups - skipping group ref handling

        # now make list unique and get all refs not contained in zone_objects
        allUsedObjRefsUnique = list(set(allUsedObjRefs))
        unresolvableObRefs = allUsedObjRefsUnique - self.NormalizedConfig.zone_objects.keys()
        if len(unresolvableObRefs)>0:
            issues.update({'unresolvableZoneObRefs': list(unresolvableObRefs)})

        # we currently do not have zone types - skipping type handling
        return issues
    
    # check if all color refs are valid (in the DB)
    # fix=True means that missing color refs will be replaced by the default color (black)
    def checkColorConsistency(self, fix=True):
        issues = {}
        allUsedNwObjColorRefSet = set()
        allUsedSvcColorRefSet = set()
        allUsedUserColorRefSet = set()
        unresolvableNwObjColors = []
        unresolvableSvcColors = []
        unresolvableUserColors = []

        self.ImportDetails.SetColorRefMap()
        
        # collect all colors
        for uid in self.NormalizedConfig.network_objects:
            if self.NormalizedConfig.network_objects[uid].obj_color is not None:
                allUsedNwObjColorRefSet.add(self.NormalizedConfig.network_objects[uid].obj_color)
        for uid in self.NormalizedConfig.service_objects:
            if self.NormalizedConfig.service_objects[uid].svc_color is not None:
                allUsedSvcColorRefSet.add(self.NormalizedConfig.service_objects[uid].svc_color)
        for uid in self.NormalizedConfig.users:
            if self.NormalizedConfig.users[uid].user_color is not None:
                allUsedUserColorRefSet.add(self.NormalizedConfig.users[uid].user_color)

        # check all nwobj color refs
        for colorString in allUsedNwObjColorRefSet:
            colorId = self.ImportDetails.lookupColorId(colorString)
            if colorId is None:
                unresolvableNwObjColors.append(colorString)

        # check all nwobj color refs
        for colorString in allUsedSvcColorRefSet:
            colorId = self.ImportDetails.lookupColorId(colorString)
            if colorId is None:
                unresolvableSvcColors.append(colorString)

        # check all user color refs
        for colorString in allUsedUserColorRefSet:
            colorId = self.ImportDetails.lookupColorId(colorString)
            if colorId is None:
                unresolvableUserColors.append(colorString)

        if fix:
            for colorString in unresolvableNwObjColors:
                # replace with default color
                for uid in self.NormalizedConfig.network_objects:
                    if self.NormalizedConfig.network_objects[uid].obj_color==colorString:
                        self.NormalizedConfig.network_objects[uid].obj_color = fwo_const.defaultColor
            for colorString in unresolvableSvcColors:
                # replace with default color
                for uid in self.NormalizedConfig.service_objects:
                    if self.NormalizedConfig.service_objects[uid].svc_color==colorString:
                        self.NormalizedConfig.service_objects[uid].svc_color = fwo_const.defaultColor
            for colorString in unresolvableUserColors:
                # replace with default color
                for uid in self.NormalizedConfig.users:
                    if self.NormalizedConfig.users[uid].user_color==colorString:
                        self.NormalizedConfig.users[uid].user_color = fwo_const.defaultColor

        elif len(unresolvableNwObjColors)>0 or len(unresolvableSvcColors)>0 or len(unresolvableUserColors)>0:
            issues.update({ 'unresolvableColorRefs': 
                {'nwObjColors': unresolvableNwObjColors, 'svcColors': unresolvableSvcColors, 'userColors': unresolvableUserColors}})
        return issues

    # e.g. check rule to rule refs
    def checkRuleConsistency(self):
        issues = {}
        # TODO: implement
        return issues

    # e.g. check rule to rule refs
    def checkGatewayConsistency(self):
        issues = {}
        # TODO: implement
        return issues

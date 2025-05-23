from typing import Dict, List
import traceback
import time, datetime
import json

from fwo_log import ChangeLogger, getFwoLogger
from model_controllers.import_state_controller import ImportStateController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalized
from model_controllers.fwconfig_import_base import FwConfigImportBase
from models.networkobject import NetworkObjectForImport
from models.serviceobject import ServiceObjectForImport
import fwo_const


# this class is used for importing a config into the FWO API
class FwConfigImportObject(FwConfigImportBase):

    # @root_validator(pre=True)
    # def custom_initialization(cls, values):
    #     values['NetworkObjectTypeMap'] = cls.GetNetworkObjTypeMap()
    #     values['ServiceObjectTypeMap'] = cls.GetServiceObjTypeMap()
    #     values['UserObjectTypeMap'] = cls.GetUserObjTypeMap()
    #     values['ProtocolMap'] = cls.GetProtocolMap()
    #     values['ColorMap'] = cls.GetColorMap()
    #     return values
    
    def __init__(self, importState: ImportStateController, config: FwConfigNormalized):
        super().__init__(importState, config)

        self.NetworkObjectTypeMap = self.GetNetworkObjTypeMap()
        self.ServiceObjectTypeMap = self.GetServiceObjTypeMap()
        self.UserObjectTypeMap = self.GetUserObjTypeMap()
        self.ProtocolMap = self.GetProtocolMap()
        self.ColorMap = self.GetColorMap()

    def updateObjectDiffs(self, prevConfig: FwConfigNormalized):

        # calculate network object diffs
        # here we are handling the previous config as a dict for a while
        # previousNwObjects = prevConfig.network_objects
        deletedNwobjUids = list(prevConfig.network_objects.keys() - self.NormalizedConfig.network_objects.keys())
        newNwobjUids = list(self.NormalizedConfig.network_objects.keys() - prevConfig.network_objects.keys())
        nwobjUidsInBoth = list(self.NormalizedConfig.network_objects.keys() & prevConfig.network_objects.keys())
        change_logger = ChangeLogger()

        # For correct changelog and stats.
        changed_nw_objs = []
        changed_svcs = []

        # decide if it is prudent to mix changed, deleted and added rules here:
        for nwObjUid in nwobjUidsInBoth:
            if self.NormalizedConfig.network_objects[nwObjUid].did_change(prevConfig.network_objects[nwObjUid]):
                newNwobjUids.append(nwObjUid)
                deletedNwobjUids.append(nwObjUid)
                changed_nw_objs.append(nwObjUid)

        # calculate service object diffs
        deletedSvcObjUids = list(prevConfig.service_objects.keys() - self.NormalizedConfig.service_objects.keys())
        newSvcObjUids = list(self.NormalizedConfig.service_objects.keys() - prevConfig.service_objects.keys())
        svcObjUidsInBoth = list(self.NormalizedConfig.service_objects.keys() & prevConfig.service_objects.keys())

        for nwSvcUid in svcObjUidsInBoth:
            if self.NormalizedConfig.service_objects[nwSvcUid].did_change(prevConfig.service_objects[nwSvcUid]):
                newSvcObjUids.append(nwSvcUid)
                deletedSvcObjUids.append(nwSvcUid)
                changed_svcs.append(nwSvcUid)
        
        #TODO: calculate user diffs
        # deletedUserUids = list(prevConfig.users.keys() - self.NormalizedConfig.users.keys())

        # initial mapping of object uids to ids. needs to be updated, if more objects are created in the db after this point
        #TODO: only fetch objects needed later. Esp for !isFullImport. but: newNwObjIds not enough!
        # -> newObjs + extract all objects from new/changed rules and groups, flatten them. Complete?
        self.uid2id_mapper.update_network_object_mapping()
        self.uid2id_mapper.update_service_object_mapping()
        self.uid2id_mapper.update_user_mapping()

        self.group_flats_mapper.init_config(self.NormalizedConfig)
        self.prev_group_flats_mapper.init_config(prevConfig)

        # need to do this first, since we need the old object IDs for the group memberships
        #TODO: computationally expensive? Even without changes, all group objects and their members are compared to the previous config.
        errors, changes, changedNwObjMembers, changedNwObjFlats = self.removeOutdatedNwObjectMemberships(prevConfig)
        errors, changes, changedSvcObjMembers, changedSvcObjFlats = self.removeOutdatedSvcObjectMemberships(prevConfig)

        # add newly created objects
        errorCountUpdate, numberOfModifiedObjects, newNwObjIds, newNwSvcIds, removedNwObjIds, removedNwSvcIds = \
            self.updateObjectsViaApi(newNwobjUids, newSvcObjUids, deletedNwobjUids, deletedSvcObjUids)
        
        self.uid2id_mapper.add_network_object_mappings(newNwObjIds)
        self.uid2id_mapper.add_service_object_mappings(newNwSvcIds)

        # insert new and updated group memberships
        errors, changes = self.addNwObjGroupMemberships(prevConfig, changedNwObjMembers, changedNwObjFlats)
        errors, changes = self.addNwSvcGroupMemberships(prevConfig, changedSvcObjMembers, changedSvcObjFlats)
        #TODO: self.addUserObjGroupMemberships(newUserIds)

        # these objects have really been deleted so there should be no refs to them anywhere! verify this

        # update all references to objects marked as removed
        self.markObjectRefsRemoved(removedNwObjIds, removedNwSvcIds)

        # TODO: calculate user diffs
        # TODO: calculate zone diffs


        # Get Changed Ids.

        change_logger.create_change_id_maps(self.uid2id_mapper, changed_nw_objs, changed_svcs, removedNwObjIds, removedNwSvcIds)

        # Seperate changes from adds and removes for changelog and stats.

        newNwObjIds = [newNwObjId
            for newNwObjId in newNwObjIds
            if newNwObjId['obj_id'] not in list(change_logger.changed_object_id_map.values())       
        ]
        removedNwObjIds = [removedNwObjId
            for removedNwObjId in removedNwObjIds
            if removedNwObjId['obj_id'] not in list(change_logger.changed_object_id_map.keys())       
        ]
        newNwSvcIds = [newNwSvcId
            for newNwSvcId in newNwSvcIds
            if newNwSvcId['svc_id'] not in list(change_logger.changed_service_id_map.values())       
        ]
        removedNwSvcIds = [removedNwSvcId
            for removedNwSvcId in removedNwSvcIds
            if removedNwSvcId['svc_id'] not in list(change_logger.changed_service_id_map.keys())       
        ]

        # Write change logs to tables.
        
        self.addChangelogObjects(newNwObjIds, newNwSvcIds, removedNwObjIds, removedNwSvcIds)

        # note changes:
        self.ImportDetails.Stats.NetworkObjectAddCount = len(newNwObjIds)
        self.ImportDetails.Stats.NetworkObjectDeleteCount = len(removedNwObjIds)
        self.ImportDetails.Stats.NetworkObjectChangeCount = len(change_logger.changed_object_id_map.items())
        self.ImportDetails.Stats.ServiceObjectAddCount = len(newNwSvcIds)
        self.ImportDetails.Stats.ServiceObjectDeleteCount = len(removedNwSvcIds)
        self.ImportDetails.Stats.ServiceObjectChangeCount = len(change_logger.changed_service_id_map.items())

    def GetNetworkObjTypeMap(self):
        query = "query getNetworkObjTypeMap { stm_obj_typ { obj_typ_name obj_typ_id } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting stm_obj_typ")
            return {}
        
        map = {}
        for nwType in result['data']['stm_obj_typ']:
            map.update({nwType['obj_typ_name']: nwType['obj_typ_id']})
        return map

    def GetServiceObjTypeMap(self):
        query = "query getServiceObjTypeMap { stm_svc_typ { svc_typ_name svc_typ_id } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting stm_svc_typ")
            return {}
        
        map = {}
        for svcType in result['data']['stm_svc_typ']:
            map.update({svcType['svc_typ_name']: svcType['svc_typ_id']})
        return map

    def GetUserObjTypeMap(self):
        query = "query getUserObjTypeMap { stm_usr_typ { usr_typ_name usr_typ_id } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting stm_usr_typ")
            return {}
        
        map = {}
        for usrType in result['data']['stm_usr_typ']:
            map.update({usrType['usr_typ_name']: usrType['usr_typ_id']})
        return map

    def GetProtocolMap(self):
        query = "query getIpProtocols { stm_ip_proto { ip_proto_id ip_proto_name } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting stm_ip_proto")
            return {}
        
        map = {}
        for proto in result['data']['stm_ip_proto']:
            map.update({proto['ip_proto_name'].lower(): proto['ip_proto_id']})
        return map

    def GetColorMap(self):
        query = "query getColorMap { stm_color { color_name color_id } }"
        try:
            result = self.ImportDetails.call(query=query, queryVariables={})
        except Exception:
            logger = getFwoLogger()
            logger.error("Error while getting stm_color")
            return {}
        

        map = {}

        for color in result['data']['stm_color']:
            map.update({color['color_name']: color['color_id']})
        return map


    def updateObjectsViaApi(self, newNwObjectUids, newSvcObjectUids, removedNwObjectUids, removedSvcObjectUids):
        # here we also mark old objects removed before adding the new versions
        logger = getFwoLogger(debug_level=self.ImportDetails.DebugLevel)
        errors = 0
        changes = 0
        newNwObjIds = []
        newNwSvcIds = []
        removedNwObjIds = []
        removedNwSvcIds = []
        import_mutation = """mutation updateObjects($mgmId: Int!, $importId: bigint!, $removedNwObjectUids: [String!]!, $removedSvcObjectUids: [String!]!, $newNwObjects: [object_insert_input!]!, $newSvcObjects: [service_insert_input!]!) {
                update_removed_obj: update_object(where: {mgm_id: {_eq: $mgmId}, obj_uid: {_in: $removedNwObjectUids}, removed: {_is_null: true}},
                    _set: {
                        removed: $importId,
                        active: false
                    }
                ) {
                    affected_rows
                    returning {
                        obj_id
                        obj_uid
                        obj_typ_id
                    }
                }
                update_removed_svc: update_service(where: {mgm_id: {_eq: $mgmId}, svc_uid: {_in: $removedSvcObjectUids}, removed: {_is_null: true}},
                    _set: {
                        removed: $importId,
                        active: false
                    }
                ) {
                    affected_rows
                    returning {
                        svc_id
                        svc_uid
                    }
                }
                insert_object(objects: $newNwObjects) {
                    affected_rows
                    returning {
                        obj_id
                        obj_uid
                        obj_typ_id
                    }
                }
                insert_service(objects: $newSvcObjects) {
                    affected_rows
                    returning {
                        svc_id
                        svc_uid
                    }
                }
            }
        """
        queryVariables = {
            'mgmId': self.ImportDetails.MgmDetails.Id,
            'importId': self.ImportDetails.ImportId,
            'newNwObjects': self.prepareNewNwObjects(newNwObjectUids),
            'newSvcObjects': self.prepareNewSvcObjects(newSvcObjectUids),
            'removedNwObjectUids': removedNwObjectUids,
            'removedSvcObjectUids': removedSvcObjectUids,
        }
        
        try:
            import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables, debug_level=self.ImportDetails.DebugLevel, analyze_payload=True)
            if 'errors' in import_result:
                logger.exception(f"fwo_api:importNwObject - error in updateObjectsViaApi: {str(import_result['errors'])}")
                errors = 1
            else:
                changes = int(import_result['data']['insert_object']['affected_rows']) + \
                    int(import_result['data']['insert_service']['affected_rows']) + \
                    int(import_result['data']['update_removed_obj']['affected_rows']) + \
                    int(import_result['data']['update_removed_svc']['affected_rows'])
                newNwObjIds = import_result['data']['insert_object']['returning']
                newNwSvcIds = import_result['data']['insert_service']['returning']
                removedNwObjIds = import_result['data']['update_removed_obj']['returning']
                removedNwSvcIds = import_result['data']['update_removed_svc']['returning']
        except Exception:
            logger.exception(f"failed to update objects: {str(traceback.format_exc())}")
            errors = 1
        return errors, changes, newNwObjIds, newNwSvcIds, removedNwObjIds, removedNwSvcIds
    

    def prepareNewNwObjects(self, newNwobjUids):
        newNwObjs = []
        for nwobjUid in newNwobjUids:
            newNwObj = NetworkObjectForImport(nwObject=self.NormalizedConfig.network_objects[nwobjUid],
                                                    mgmId=self.ImportDetails.MgmDetails.Id, 
                                                    importId=self.ImportDetails.ImportId, 
                                                    colorId=self.lookupColor(self.NormalizedConfig.network_objects[nwobjUid].obj_color), 
                                                    typId=self.lookupObjType(self.NormalizedConfig.network_objects[nwobjUid].obj_typ))
            newNwObjDict = newNwObj.toDict()
            newNwObjs.append(newNwObjDict)
            # newNwObjs.append(NetworkObjectForImport(nwObject=self.NormalizedConfig.network_objects[nwobjUid],
            #                                         mgmId=self.ImportDetails.MgmDetails.Id, 
            #                                         importId=self.ImportDetails.ImportId, 
            #                                         colorId=self.lookupColor(self.NormalizedConfig.network_objects[nwobjUid].obj_color), 
            #                                         typId=self.lookupObjType(self.NormalizedConfig.network_objects[nwobjUid].obj_typ)).toJson())
        return newNwObjs


    def prepareNewSvcObjects(self, newSvcobjUids):
        newObjs = []
        for uid in newSvcobjUids:
            newObjs.append(ServiceObjectForImport(svcObject=self.NormalizedConfig.service_objects[uid],
                                        mgmId=self.ImportDetails.MgmDetails.Id, 
                                        importId=self.ImportDetails.ImportId, 
                                        colorId=self.lookupColor(self.NormalizedConfig.service_objects[uid].svc_color), 
                                        typId=self.lookupSvcType(self.NormalizedConfig.service_objects[uid].svc_typ),
                                        ).toDict())

            # newEnrichedSvcObj = self.NormalizedConfig.service_objects[uid].copy() # leave the original dict as is

            # color = newEnrichedSvcObj.pop('svc_color', None)     # get and remove
            # if color != None:
            #     color = self.lookupColor(color)
            # objtype = newEnrichedSvcObj.pop('svc_typ', None)     # get and remove
            # if objtype != None:
            #     objtype = self.lookupSvcType(objtype)
            # protoId = newEnrichedSvcObj.pop('ip_proto', None)     # get and remove
            # if protoId != None:
            #     protoId = self.lookupProtoNameToId(protoId)

            # rpcNr = newEnrichedSvcObj.pop('rpc_nr', None)     # get and remove

            # newEnrichedSvcObj.update({
            #         'mgm_id': self.ImportDetails.MgmDetails.Id,
            #         'svc_create': self.ImportDetails.ImportId,
            #         'svc_last_seen': self.ImportDetails.ImportId,   # could be left out
            #         'svc_color_id': color,
            #         'svc_typ_id': objtype,
            #         'ip_proto_id': protoId,
            #         'svc_rpcnr': rpcNr
            #     })
            # newObjs.append(newEnrichedSvcObj)
        return newObjs


    def removeOutdatedNwObjectMemberships(self, prevConfig: FwConfigNormalized):
        errors = 0
        changes = 0
        removedNwObjMembers = []
        removedNwObjFlats = []
        changedNwObjMembers = []
        changedNwObjFlats = []

        for nwObjUid in prevConfig.network_objects.keys():
            if prevConfig.network_objects[nwObjUid].obj_member_refs is not None:
                objgrpId = self.uid2id_mapper.get_network_object_id(nwObjUid)
                prevMemberUIds = prevConfig.network_objects[nwObjUid].obj_member_refs.split(fwo_const.list_delimiter)
                prevFlatMemberUIds = self.prev_group_flats_mapper.get_network_object_flats([nwObjUid])
                memberUIds = [] # all members need to be removed if group deleted or changed
                flatMemberUIds = []
                if nwObjUid in self.NormalizedConfig.network_objects: # group not removed
                    if not self.NormalizedConfig.network_objects[nwObjUid].did_change(prevConfig.network_objects[nwObjUid]):
                        # group not changed -> check for changes in members
                        memberUIds = self.NormalizedConfig.network_objects[nwObjUid].obj_member_refs.split(fwo_const.list_delimiter)
                        flatMemberUIds = self.group_flats_mapper.get_network_object_flats([nwObjUid])
                for prevMemberUId in prevMemberUIds:
                    removed = prevMemberUId not in memberUIds
                    if not removed: # check for change
                        if self.NormalizedConfig.network_objects[prevMemberUId].did_change(prevConfig.network_objects[prevMemberUId]):
                            removed = True
                            changedNwObjMembers.append([nwObjUid, prevMemberUId])
                    if removed:
                        prevMemberId = self.uid2id_mapper.get_network_object_id(prevMemberUId)
                        removedNwObjMembers.append({
                            "_and": [
                                { "objgrp_id": {"_eq": objgrpId} },
                                { "objgrp_member_id": {"_eq": prevMemberId} },
                            ]
                        })
                for prevFlatMemberUId in prevFlatMemberUIds:
                    removed = prevFlatMemberUId not in flatMemberUIds
                    if not removed:
                        if self.NormalizedConfig.network_objects[prevFlatMemberUId].did_change(prevConfig.network_objects[prevFlatMemberUId]):
                            removed = True
                            changedNwObjFlats.append([nwObjUid, prevFlatMemberUId])
                    if removed:
                        prevFlatMemberId = self.uid2id_mapper.get_network_object_id(prevFlatMemberUId)
                        removedNwObjFlats.append({
                            "_and": [
                                { "objgrp_flat_id": {"_eq": objgrpId} },
                                { "objgrp_flat_member_id": {"_eq": prevFlatMemberId} },
                            ]
                        })

        # remove outdated group memberships
        if len(removedNwObjMembers) > 0:
            import_mutation = """
                mutation removeOutdatedNwObjMemberships($importId: bigint!, $removedNwObjMembers: [objgrp_bool_exp!]!, $removedNwObjFlats: [objgrp_flat_bool_exp!]!) {
                    update_objgrp(where: {_and: [{_or: $removedNwObjMembers}, {removed: {_is_null: true}}]},
                        _set: {
                            removed: $importId,
                            active: false
                        }
                    ) {
                        affected_rows
                    }
                    update_objgrp_flat(where: {_and: [{_or: $removedNwObjFlats}, {removed: {_is_null: true}}]},
                        _set: {
                            removed: $importId,
                            active: false
                        }
                    ) {
                        affected_rows
                    }
                }
            """
            queryVariables = {
                'importId': self.ImportDetails.ImportId,
                'removedNwObjMembers': removedNwObjMembers,
                'removedNwObjFlats': removedNwObjFlats
            }
            try:
                import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables, analyze_payload=True)
                if 'errors' in import_result:
                    logger = getFwoLogger()
                    logger.exception(f"fwo_api:importNwObject - error in removeOutdatedNwObjMemberships: {str(import_result['errors'])}")
                else:
                    changes = int(import_result['data']['update_objgrp']['affected_rows']) + \
                        int(import_result['data']['update_objgrp_flat']['affected_rows'])
            except Exception:
                logger = getFwoLogger()
                logger.exception(f"failed to remove outdated group memberships: {str(traceback.format_exc())}")
                errors = 1
        return errors, changes, changedNwObjMembers, changedNwObjFlats


    def removeOutdatedSvcObjectMemberships(self, prevConfig: FwConfigNormalized):
        errors = 0
        changes = 0
        removedSvcObjMembers = []
        removedSvcObjFlats = []
        changedSvcObjMembers = []
        changedSvcObjFlats = []

        for svcObjUid in prevConfig.service_objects.keys():
            if prevConfig.service_objects[svcObjUid].svc_member_refs is not None:
                svcgrpId = self.uid2id_mapper.get_service_object_id(svcObjUid)
                prevMemberUIds = prevConfig.service_objects[svcObjUid].svc_member_refs.split(fwo_const.list_delimiter)
                prevFlatMemberUIds = self.prev_group_flats_mapper.get_service_object_flats([svcObjUid])
                memberUIds = [] # all members need to be removed if group deleted or changed
                flatMemberUIds = []
                if svcObjUid in self.NormalizedConfig.service_objects: # group not removed
                    if not self.NormalizedConfig.service_objects[svcObjUid].did_change(prevConfig.service_objects[svcObjUid]):
                        # group not changed -> check for changes in members
                        memberUIds = self.NormalizedConfig.service_objects[svcObjUid].svc_member_refs.split(fwo_const.list_delimiter)
                        flatMemberUIds = self.group_flats_mapper.get_service_object_flats([svcObjUid])
                for prevMemberUId in prevMemberUIds:
                    removed = prevMemberUId not in memberUIds
                    if not removed: # check for change
                        if self.NormalizedConfig.service_objects[prevMemberUId].did_change(prevConfig.service_objects[prevMemberUId]):
                            removed = True
                            changedSvcObjMembers.append([svcObjUid, prevMemberUId])
                    if removed:
                        prevMemberId = self.uid2id_mapper.get_service_object_id(prevMemberUId)
                        removedSvcObjMembers.append({
                            "_and": [
                                { "svcgrp_id": {"_eq": svcgrpId} },
                                { "svcgrp_member_id": {"_eq": prevMemberId} },
                            ]
                        })
                for prevFlatMemberUId in prevFlatMemberUIds:
                    removed = prevFlatMemberUId not in flatMemberUIds
                    if not removed:
                        if self.NormalizedConfig.service_objects[prevFlatMemberUId].did_change(prevConfig.service_objects[prevFlatMemberUId]):
                            removed = True
                            changedSvcObjFlats.append([svcObjUid, prevFlatMemberUId])
                    if removed:
                        prevFlatMemberId = self.uid2id_mapper.get_service_object_id(prevFlatMemberUId)
                        removedSvcObjFlats.append({
                            "_and": [
                                { "svcgrp_flat_id": {"_eq": svcgrpId} },
                                { "svcgrp_flat_member_id": {"_eq": prevFlatMemberId} },
                            ]
                        })

        # remove outdated group memberships
        if len(removedSvcObjMembers) > 0:
            import_mutation = """
                mutation removeOutdatedSvcObjMemberships($importId: bigint!, $removedSvcObjMembers: [svcgrp_bool_exp!]!, $removedSvcObjFlats: [svcgrp_flat_bool_exp!]!) {
                    update_svcgrp(where: {_and: [{_or: $removedSvcObjMembers}, {removed: {_is_null: true}}]},
                        _set: {
                            removed: $importId,
                            active: false
                        }
                    ) {
                        affected_rows
                    }
                    update_svcgrp_flat(where: {_and: [{_or: $removedSvcObjFlats}, {removed: {_is_null: true}}]},
                        _set: {
                            removed: $importId,
                            active: false
                        }
                    ) {
                        affected_rows
                    }
                }
            """
            queryVariables = {
                'importId': self.ImportDetails.ImportId,
                'removedSvcObjMembers': removedSvcObjMembers,
                'removedSvcObjFlats': removedSvcObjFlats
            }
            try:
                import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables, analyze_payload=True)
                if 'errors' in import_result:
                    logger = getFwoLogger()
                    logger.exception(f"fwo_api:importNwObject - error in removeOutdatedSvcObjMemberships: {str(import_result['errors'])}")
                else:
                    changes = int(import_result['data']['update_svcgrp']['affected_rows']) + \
                        int(import_result['data']['update_svcgrp_flat']['affected_rows'])
            except Exception:
                logger = getFwoLogger()
                logger.exception(f"failed to remove outdated group memberships: {str(traceback.format_exc())}")
                errors = 1
        return errors, changes, changedSvcObjMembers, changedSvcObjFlats


    def addNwObjGroupMemberships(self, prevConfig, outdatedMembers, outdatedFlats):
        """
        This function is used to update group memberships for network objects in the database.
        It adds group memberships and flats for new and updated members.

        Args:
            prevConfig (FwConfigNormalized): The previous configuration.
            outdatedMembers (List[Tuple[string, string]]): List of tuples containing the group UIDs and member UIDs of outdated members.
            outdatedFlats (List[Tuple[string, string]]): List of tuples containing the group UIDs and flat member UIDs of outdated flats.
        """
        newGroupMembers = []
        newGroupMemberFlats = []
        logger = getFwoLogger()
        errors = 0
        changes = 0

        for nwObjUid in self.NormalizedConfig.network_objects.keys():
            if self.NormalizedConfig.network_objects[nwObjUid].obj_member_refs is not None:
                memberUids = self.NormalizedConfig.network_objects[nwObjUid].obj_member_refs.split(fwo_const.list_delimiter)
                if nwObjUid in prevConfig.network_objects: # group not added
                    if not self.NormalizedConfig.network_objects[nwObjUid].did_change(prevConfig.network_objects[nwObjUid]):
                        # group not changed -> if exist, changed members are handled below
                        continue
                objgrpId = self.uid2id_mapper.get_network_object_id(nwObjUid)
                for memberUId in memberUids:
                    memberId = self.uid2id_mapper.get_network_object_id(memberUId)
                    newGroupMembers.append({
                        "objgrp_id": objgrpId,
                        "objgrp_member_id": memberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId # to be removed in the future
                    })
                flatMemberUids = self.group_flats_mapper.get_network_object_flats([nwObjUid])
                for flatMemberUid in flatMemberUids:
                    flatMemberId = self.uid2id_mapper.get_network_object_id(flatMemberUid)
                    newGroupMemberFlats.append({
                        "objgrp_flat_id": objgrpId,
                        "objgrp_flat_member_id": flatMemberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId # to be removed in the future
                    })
        
        for changedObj in outdatedMembers: # readd changed members
            objgrpId = self.uid2id_mapper.get_network_object_id(changedObj[0])
            memberId = self.uid2id_mapper.get_network_object_id(changedObj[1])
            newGroupMembers.append({
                "objgrp_id": objgrpId,
                "objgrp_member_id": memberId,
                "import_created": self.ImportDetails.ImportId,
                "import_last_seen": self.ImportDetails.ImportId # to be removed in the future
            })
        
        for changedObj in outdatedFlats: # readd changed flats
            objgrpId = self.uid2id_mapper.get_network_object_id(changedObj[0])
            memberId = self.uid2id_mapper.get_network_object_id(changedObj[1])
            newGroupMemberFlats.append({
                "objgrp_flat_id": objgrpId,
                "objgrp_flat_member_id": memberId,
                "import_created": self.ImportDetails.ImportId,
                "import_last_seen": self.ImportDetails.ImportId # to be removed in the future
            })


        if len(newGroupMembers)>0:

            import_mutation = """
                mutation updateNwGroups($nwGroups: [objgrp_insert_input!]!, $nwGroupFlats: [objgrp_flat_insert_input!]!) {
                    insert_objgrp(objects: $nwGroups) {
                        affected_rows
                    }
                    insert_objgrp_flat(objects: $nwGroupFlats) {
                        affected_rows
                    }
                }
            """

            queryVariables = { 
                'nwGroups': newGroupMembers,
                'nwGroupFlats': newGroupMemberFlats
            }
            try:
                import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables, analyze_payload=True)
                if 'errors' in import_result:
                    logger.exception(f"fwo_api:importNwObject - error in addNwObjGroupMemberships: {str(import_result['errors'])}")
                    errors = 1
                else:
                    changes = int(import_result['data']['insert_objgrp']['affected_rows']) + \
                        int(import_result['data']['insert_objgrp_flat']['affected_rows'])
            except Exception:
                logger.exception(f"failed to write new objects: {str(traceback.format_exc())}")
                errors = 1
            
        return errors, changes
    

    def addNwSvcGroupMemberships(self, prevConfig, outdatedMembers, outdatedFlats):
        """
        This function is used to update group memberships for service objects in the database.
        It adds group memberships and flats for new and updated members.

        Args:
            prevConfig (FwConfigNormalized): The previous configuration.
            outdatedMembers (List[Tuple[string, string]]): List of tuples containing the group UIDs and member UIDs of outdated members.
            outdatedFlats (List[Tuple[string, string]]): List of tuples containing the group UIDs and flat member UIDs of outdated flats.
        """
        newGroupMembers = []
        newGroupMemberFlats = []
        logger = getFwoLogger()
        errors = 0
        changes = 0

        for svcObjUid in self.NormalizedConfig.service_objects.keys():
            if self.NormalizedConfig.service_objects[svcObjUid].svc_member_refs is not None:
                memberUids = self.NormalizedConfig.service_objects[svcObjUid].svc_member_refs.split(fwo_const.list_delimiter)
                if svcObjUid in prevConfig.service_objects: # group not added
                    if not self.NormalizedConfig.service_objects[svcObjUid].did_change(prevConfig.service_objects[svcObjUid]):
                        # group not changed -> if exist, changed members are handled below
                        continue
                svcgrpId = self.uid2id_mapper.get_service_object_id(svcObjUid)
                for memberUId in memberUids:
                    memberId = self.uid2id_mapper.get_service_object_id(memberUId)
                    newGroupMembers.append({
                        "svcgrp_id": svcgrpId,
                        "svcgrp_member_id": memberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId # to be removed in the future
                    })
                flatMemberUids = self.group_flats_mapper.get_service_object_flats([svcObjUid])
                for flatMemberUid in flatMemberUids:
                    flatMemberId = self.uid2id_mapper.get_service_object_id(flatMemberUid)
                    newGroupMemberFlats.append({
                        "svcgrp_flat_id": svcgrpId,
                        "svcgrp_flat_member_id": flatMemberId,
                        "import_created": self.ImportDetails.ImportId,
                        "import_last_seen": self.ImportDetails.ImportId # to be removed in the future
                    })

        for changedObj in outdatedMembers: # readd changed members
            svcgrpId = self.uid2id_mapper.get_service_object_id(changedObj[0])
            memberId = self.uid2id_mapper.get_service_object_id(changedObj[1])
            newGroupMembers.append({
                "svcgrp_id": svcgrpId,
                "svcgrp_member_id": memberId,
                "import_created": self.ImportDetails.ImportId,
                "import_last_seen": self.ImportDetails.ImportId # to be removed in the future
            })

        for changedObj in outdatedFlats: # readd changed flats
            svcgrpId = self.uid2id_mapper.get_service_object_id(changedObj[0])
            memberId = self.uid2id_mapper.get_service_object_id(changedObj[1])
            newGroupMemberFlats.append({
                "svcgrp_flat_id": svcgrpId,
                "svcgrp_flat_member_id": memberId,
                "import_created": self.ImportDetails.ImportId,
                "import_last_seen": self.ImportDetails.ImportId # to be removed in the future
            })

        if len(newGroupMembers)>0:
            import_mutation = """
                mutation updateSvcGroups($svcGroups: [svcgrp_insert_input!]!, $svcGroupFlats: [svcgrp_flat_insert_input!]!) {
                    insert_svcgrp(objects: $svcGroups) {
                        affected_rows
                    }
                    insert_svcgrp_flat(objects: $svcGroupFlats) {
                        affected_rows
                    }
                }
            """
            queryVariables = {
                'svcGroups': newGroupMembers,
                'svcGroupFlats': newGroupMemberFlats
            }
            try:
                import_result = self.ImportDetails.call(import_mutation, queryVariables=queryVariables, analyze_payload=True)
                if 'errors' in import_result:
                    logger.exception(f"fwo_api:importNwObject - error in addNwSvcGroupMemberships: {str(import_result['errors'])}")
                    errors = 1
                else:
                    changes = int(import_result['data']['insert_svcgrp']['affected_rows']) + \
                        int(import_result['data']['insert_svcgrp_flat']['affected_rows'])
            except Exception:
                logger.exception(f"failed to write new objects: {str(traceback.format_exc())}")
                errors = 1
        return errors, changes

    # object refs are not deleted but marked as removed
    def markObjectRefsRemoved(self, removedNwObjectIds, removedSvcObjectIds):
        logger = getFwoLogger()
        errors = 0
        changes = 0
        removedNwObjIds = []
        removedNwSvcIds = []
        removeMutation = """
            mutation updateObjects($importId: bigint!, $removedNwObjectIds: [bigint!]!, $removedSvcObjectIds: [bigint!]!) {
                update_rule_from(where: {obj_id: {_in: $removedNwObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
                    affected_rows
                }
                update_rule_to(where: {obj_id: {_in: $removedNwObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
                    affected_rows
                }
                update_rule_service(where: {svc_id: {_in: $removedSvcObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId, active: false}) {
                    affected_rows
                }
                update_rule_nwobj_resolved(where: {obj_id: {_in: $removedNwObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId}) {
                    affected_rows
                }
                update_rule_svc_resolved(where: {svc_id: {_in: $removedSvcObjectIds}, removed: {_is_null: true}}, _set: {removed: $importId}) {
                    affected_rows
                }
            }
        """

        plainRemovedNwObjIds = []
        for obj in removedNwObjectIds:
            plainRemovedNwObjIds.append(obj['obj_id'])
        plainRemovedSvcObjIds = []
        for obj in removedSvcObjectIds:
            plainRemovedSvcObjIds.append(obj['svc_id'])

        queryVariables = {
            'importId': self.ImportDetails.ImportId,
            'removedNwObjectIds': plainRemovedNwObjIds,
            'removedSvcObjectIds': plainRemovedSvcObjIds
        }
        
        try:
            removeResult = self.ImportDetails.call(removeMutation, queryVariables=queryVariables, analyze_payload=True)
            if 'errors' in removeResult:
                logger.exception(f"error while marking objects as removed: {str(removeResult['errors'])}")
                errors = 1
            else:
                changes = int(removeResult['data']['update_rule_from']['affected_rows']) + \
                    int(removeResult['data']['update_rule_to']['affected_rows']) + \
                    int(removeResult['data']['update_rule_service']['affected_rows']) + \
                    int(removeResult['data']['update_rule_nwobj_resolved']['affected_rows']) + \
                    int(removeResult['data']['update_rule_svc_resolved']['affected_rows'])
                    
        except Exception:
            logger.exception(f"fatal error while marking objects as removed: {str(traceback.format_exc())}")
            errors = 1
        
        return errors, changes, removedNwObjIds, removedNwSvcIds

    def lookupObjType(self, objTypeString):
        # TODO: might check for miss here as this is a mandatory field!
        return self.NetworkObjectTypeMap.get(objTypeString, None)

    def lookupSvcType(self, svcTypeString):
        # TODO: might check for miss here as this is a mandatory field!
        return self.ServiceObjectTypeMap.get(svcTypeString, None)

    def lookupObjIdToUidAndPolicyName(self, objId: int):
        return str(objId) # mock
        # CAST((COALESCE (rule.rule_ruleid, rule.rule_uid) || ', Rulebase: ' || device.local_rulebase_name) AS VARCHAR) AS unique_name,
        # return self.NetworkObjectIdMap.get(objId, None)

    def lookupSvcIdToUidAndPolicyName(self, svcId: int):
        return str(svcId) # mock

    def lookupColor(self, colorString):
        return self.ColorMap.get(colorString, None)

    def lookupProtoNameToId(self, protoString):
        if isinstance(protoString, int):
            # logger = getFwoLogger()
            # logger.warning(f"found protocol with an id as name: {str(protoString)}")
            return protoString  # already an int, do nothing
        else:
            if protoString == None:
                return None
            else:
                return self.ProtocolMap.get(protoString.lower(), None)

    def prepareChangelogObjects(self, nwObjIdsAdded, svcObjIdsAdded, nwObjIdsRemoved, svcObjIdsRemoved):
        """
            insert into stm_change_type (change_type_id,change_type_name) VALUES (1,'factory settings');
            insert into stm_change_type (change_type_id,change_type_name) VALUES (2,'initial import');
            insert into stm_change_type (change_type_id,change_type_name) VALUES (3,'in operation');
        """
        # TODO: deal with object changes where we need old and new obj id

        nwObjs = []
        svcObjs = []
        importTime = datetime.datetime.now().isoformat()
        changeTyp = 3  # standard
        change_logger = ChangeLogger()

        if self.ImportDetails.IsFullImport or self.ImportDetails.IsClearingImport:
            changeTyp = 2   # to be ignored in change reports
        
        # Write changelog for network objects.

        for nw_obj_id in [nw_obj_ids_added_item["obj_id"] for nw_obj_ids_added_item in nwObjIdsAdded]:
            nwObjs.append(change_logger.create_changelog_import_object("obj", self.ImportDetails, 'I', changeTyp, importTime, nw_obj_id))

        for nw_obj_id in [nw_obj_ids_removed_item["obj_id"] for nw_obj_ids_removed_item in nwObjIdsRemoved]:
            nwObjs.append(change_logger.create_changelog_import_object("obj", self.ImportDetails, 'D', changeTyp, importTime, nw_obj_id))

        for old_nw_obj_id, new_nw_obj_id in change_logger.changed_object_id_map.items():
            nwObjs.append(change_logger.create_changelog_import_object("obj", self.ImportDetails, 'C', changeTyp, importTime, new_nw_obj_id, old_nw_obj_id))

        # Write changelog for Services.

        for svc_id in [svc_ids_added_item["svc_id"] for svc_ids_added_item in svcObjIdsAdded]:
            svcObjs.append(change_logger.create_changelog_import_object("svc", self.ImportDetails, 'I', changeTyp, importTime, svc_id))

        for svc_id in [svc_ids_removed_item["svc_id"] for svc_ids_removed_item in svcObjIdsRemoved]:
            svcObjs.append(change_logger.create_changelog_import_object("svc", self.ImportDetails, 'D', changeTyp, importTime, svc_id))

        for old_svc_id, new_svc_id in change_logger.changed_service_id_map.items():
            svcObjs.append(change_logger.create_changelog_import_object("svc", self.ImportDetails, 'C', changeTyp, importTime, new_svc_id, old_svc_id))

        return nwObjs, svcObjs


    def addChangelogObjects(self, nwObjIdsAdded, svcObjIdsAdded, nwObjIdsRemoved, svcObjIdsRemoved):
        logger = getFwoLogger()
        errors = 0

        nwObjsChanged, svcObjsChanged = self.prepareChangelogObjects(nwObjIdsAdded, svcObjIdsAdded, nwObjIdsRemoved, svcObjIdsRemoved)

        changelogMutation = """
            mutation updateObjChangelogs($nwObjChanges: [changelog_object_insert_input!]!, $svcObjChanges: [changelog_service_insert_input!]!) {
                insert_changelog_object(objects: $nwObjChanges) {
                    affected_rows
                }
                insert_changelog_service(objects: $svcObjChanges) {
                    affected_rows
                }
            }
        """

        queryVariables = {
            'nwObjChanges': nwObjsChanged, 
            'svcObjChanges': svcObjsChanged
        }

        if len(nwObjsChanged) + len(svcObjsChanged)>0:
            try:
                changelogResult = self.ImportDetails.call(changelogMutation, queryVariables=queryVariables, analyze_payload=True)
                if 'errors' in changelogResult:
                    logger.exception(f"error while adding changelog entries for objects: {str(changelogResult['errors'])}")
                    errors = 1
            except Exception:
                logger.exception(f"fatal error while adding changelog entries for objects: {str(traceback.format_exc())}")
                errors = 1
        
        return errors

from typing import List
import requests.packages
import requests
import json
import traceback
import time

import fwo_globals
from fwo_log import getFwoLogger
from fwoBaseImport import ImportState
from fwconfig_normalized import FwConfigNormalized
from fwo_const import fwo_api_http_import_timeout, import_tmp_path
from fwo_exception import FwoApiTServiceUnavailable, FwoApiTimeout
from fwo_base import ConfigAction


# this class is used for importing a config into the FWO API
class FwConfigImport(FwConfigNormalized):
    ImportDetails: ImportState
    
    def __init__(self, importState: ImportState, config: FwConfigNormalized):
        self.FwoApiUrl = importState.FwoConfig.FwoApiUri
        self.FwoJwt = importState.Jwt
        self.ImportDetails = importState
        super().__init__(action=config.action,
                         network_objects=config.network_objects,
                         service_objects=config.service_objects,
                         users=config.users,
                         zone_objects=config.zone_objects,
                         rules=config.rules,
                         gateways=config.gateways,
                         ConfigFormat=config.ConfigFormat)
        
        self.NetworkObjectTypeMap = self.GetNetworkObjTypeMap()
        self.ServiceObjectTypeMap = self.GetServiceObjTypeMap()
        self.ProtocolMap = self.GetProtocolMap()
        self.ColorMap = self.GetColorMap()

    def GetNetworkObjTypeMap(self):
        query = "query getNetworkObjTypeMap { stm_obj_typ { obj_typ_name obj_typ_id } }"
        try:
            result = self.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_obj_typ')
            return {}
        
        map = {}
        for nwType in result['data']['stm_obj_typ']:
            map.update({nwType['obj_typ_name']: nwType['obj_typ_id']})
        return map

    def GetServiceObjTypeMap(self):
        query = "query getServiceObjTypeMap { stm_svc_typ { svc_typ_name svc_typ_id } }"
        try:
            result = self.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_svc_typ')
            return {}
        
        map = {}
        for svcType in result['data']['stm_svc_typ']:
            map.update({svcType['svc_typ_name']: svcType['svc_typ_id']})
        return map

    def GetProtocolMap(self):
        query = "query getIpProtocols { stm_ip_proto { ip_proto_id ip_proto_name } }"
        try:
            result = self.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_ip_proto')
            return {}
        
        map = {}
        for proto in result['data']['stm_ip_proto']:
            map.update({proto['ip_proto_name'].lower(): proto['ip_proto_id']})
        return map

    def GetColorMap(self):
        query = "query getColorMap { stm_color { color_name color_id } }"
        try:
            result = self.call(query=query, queryVariables={})
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting stm_color')
            return {}
        
        map = {}
        for color in result['data']['stm_color']:
            map.update({color['color_name']: color['color_id']})
        return map

    # standard FWO API call
    def call(self, query, queryVariables=""):
        role = 'importer'
        request_headers = { 
            'Content-Type': 'application/json', 
            'Authorization': f'Bearer {self.FwoJwt}', 
            'x-hasura-role': role 
        }
        full_query = {"query": query, "variables": queryVariables}
        logger = getFwoLogger()

        with requests.Session() as session:
            if fwo_globals.verify_certs is None:    # only for first FWO API call (getting info on cert verification)
                session.verify = False
            else: 
                session.verify = fwo_globals.verify_certs
            session.headers = request_headers

            try:
                r = session.post(self.FwoApiUrl, data=json.dumps(full_query), timeout=int(fwo_api_http_import_timeout))
                r.raise_for_status()
            except requests.exceptions.RequestException:
                logger.error(self.showApiCallInfo(self.FwoApiUrl, full_query, request_headers, type='error') + ":\n" + str(traceback.format_exc()))
                if r != None:
                    if r.status_code == 503:
                        raise FwoApiTServiceUnavailable("FWO API HTTP error 503 (FWO API died?)" )
                    if r.status_code == 502:
                        raise FwoApiTimeout("FWO API HTTP error 502 (might have reached timeout of " + str(int(fwo_api_http_import_timeout)/60) + " minutes)" )
                else:
                    raise
            if int(fwo_globals.debug_level) > 8:
                logger.debug (self.showApiCallInfo(self.FwoApiUrl, full_query, request_headers, type='debug'))
            if r != None:
                return r.json()
            else:
                return None

    def showApiCallInfo(self, query, headers, type='debug'):
        max_query_size_to_display = 1000
        query_string = json.dumps(query, indent=2)
        header_string = json.dumps(headers, indent=2)
        query_size = len(query_string)

        if type=='error':
            result = "error while sending api_call to url "
        else:
            result = "successful FWO API call to url "        
        result += str(self.FwoApiUrl) + " with payload \n"
        if query_size < max_query_size_to_display:
            result += query_string 
        else:
            result += str(query)[:round(max_query_size_to_display/2)] +   "\n ... [snip] ... \n" + \
                query_string[query_size-round(max_query_size_to_display/2):] + " (total query size=" + str(query_size) + " bytes)"
        result += "\n and  headers: \n" + header_string
        return result

    # returns error count
    def importConfig(self):
        self.fillGateways(self.ImportDetails)

        # assuming we always get the full config (only inserts) from API
        # 
        if self.isInitialImport():
            self.addObjects()
            self.addRules()
        else:
            previousConfig = self.getPreviousConfig()
            errorCount, totalChanges = self.updateDiffs(previousConfig)
            
            # build references from gateways to rulebases 
            # deal with networking later

        return errorCount, totalChanges
    
    def isInitialImport(self):
        query = """
            query isInitialImport($mgmId: Int!) {
              import_control_aggregate(where:{mgm_id:{_eq: $mgmId}})
                {
                    imports: aggregate {
                        count
                    }
                }
            }
        """
        queryVariables = { 'mgmId': self.ImportDetails.MgmDetails.Id }

        try:
            result = self.call(query=query, queryVariables=queryVariables)
            if result['data']['import_control_aggregate']['imports']['count']==0:
                return True
            else:
                return False
        except:
            logger = getFwoLogger()
            logger.error(f'Error while getting imports count')

    def addObjects(self):
        pass

    def updateObjects(self):
        pass

    def deleteObjects(self):
        pass

    def addRules(self):
        pass

    def updateRules(self):
        pass

    def deleteRules(self):
        pass

    def getPreviousConfig(self):
        logger = getFwoLogger()
        query = """
          query getLatestConfig($mgmId: Int!) { latest_config(where: {mgm_id: {_eq: $mgmId}}) { config } }
        """
        queryVariables = { 'mgmId': self.ImportDetails.MgmDetails.Id }
        try:
            import_result = self.call(query, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception("fwo_api:import_latest_config - error while deleting last config for mgm id " +
                                str(self.ImportDetails.MgmDetails.Id) + ": " + str(import_result['errors']))
                return 1 # error
            else:
                if len(import_result['data']['latest_config'])>0:
                    return import_result['data']['latest_config'][0]['config']
                else:
                    return FwConfigNormalized(action=ConfigAction.INSERT)
        except:
            logger.exception(f"failed to get latest normalized config for mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
        
        return FwConfigNormalized(action=ConfigAction.INSERT)
    
    def updateDiffs(self, previousConfig: FwConfigNormalized) -> int:

        prevConfig = json.loads(previousConfig)

        # option: import all inserts (simulating initial import)

        # calculate network object diffs
        previousNwObjects = prevConfig['network_objects']
        deletedNwobjUids = previousNwObjects.keys() - self.network_objects.keys()
        newNwobjUids = self.network_objects.keys() - previousNwObjects.keys()
        nwobjUidsInBoth = self.network_objects.keys() & previousNwObjects.keys()
        changedNwobjUids = []
        for nwObjUid in nwobjUidsInBoth:
            if previousNwObjects[nwObjUid] != self.network_objects[nwObjUid]:
                changedNwobjUids.append(nwObjUid)

        # calculate service object diffs
        previousSvcObjects = prevConfig['service_objects']
        deletedSvcObjUids = previousSvcObjects.keys() - self.service_objects.keys()
        newSvcObjUids = self.service_objects.keys() - previousSvcObjects.keys()
        svcObjUidsInBoth = self.service_objects.keys() & previousSvcObjects.keys()
        changedSvcObjUids = []
        for uid in svcObjUidsInBoth:
            if previousSvcObjects[uid] != self.service_objects[uid]:
                changedSvcObjUids.append(uid)

        # nwObjectsToDelete = changedNwobjUids + deletedNwobjUids
        # get obj_id for all nwObjectsToDelete
        # set deleted to current import id
        # update all references to these object ids


        errorCount, numberOfAddedObjects = self.addNewObjects(newNwobjUids, newSvcObjUids)

        # for nwobjUid in nwobjUidsInBoth:
        #     if self.network_objects[nwobjUid] != prevConfig[nwobjUid]:
        #         self.updateNetworkObject(nwobjUid)
        # for nwobjUid in deletedNwobjUids:
        #     self.deleteNetworkObject(nwobjUid)
        
        # calculate service object diffs

        # calculate user diffs

        # calculate rule diffs

        # for now always returning no diffs: 
        # return  FwConfigImport(self.ImportDetails, FwConfigNormalized(action=ConfigAction.INSERT)), \
        #         FwConfigImport(self.ImportDetails, FwConfigNormalized(action=ConfigAction.UPDATE)), \
        #         FwConfigImport(self.ImportDetails, FwConfigNormalized(action=ConfigAction.DELETE))
        return errorCount, numberOfAddedObjects

    def lookupObjType(self, objTypeString):
        return self.NetworkObjectTypeMap.get(objTypeString, None)

    def lookupColor(self, colorString):
        return self.ColorMap.get(colorString, None)

    def lookupProtoId(self, protoString):
        return self.ProtocolMap.get(protoString.lower(), None)

    def addNetworkObject(nwObjUid):
        # add object to object table
        # for groups: 
        #    add results to objgrp 
        #    resolve groups and add results to objgrp_flat
        pass

    def prepareNewNwObjects(self, newNwobjUids):
        newNwObjs = []
        for nwobjUid in newNwobjUids:
            newEnrichedNwObj = self.network_objects[nwobjUid].copy() # leave the original dict as is

            obj_color = newEnrichedNwObj.pop('obj_color', None)     # get and remove
            if obj_color != None:
                obj_color = self.lookupColor(obj_color)
            obj_type = newEnrichedNwObj.pop('obj_typ', None)     # get and remove
            if obj_type != None:
                obj_type = self.lookupObjType(obj_type)

            newEnrichedNwObj.update({
                    'mgm_id': self.ImportDetails.MgmDetails.Id,
                    'obj_create': self.ImportDetails.ImportId,
                    'obj_last_seen': self.ImportDetails.ImportId,    # could be left out
                    'obj_color_id': obj_color,
                    'obj_typ_id': obj_type
                })
            newNwObjs.append(newEnrichedNwObj)
        return newNwObjs

    def prepareNewSvcObjects(self, newSvcobjUids):
        newObjs = []
        for uid in newSvcobjUids:
            newEnrichedSvcObj = self.service_objects[uid].copy() # leave the original dict as is

            color = newEnrichedSvcObj.pop('svc_color', None)     # get and remove
            if color != None:
                color = self.lookupColor(color)
            objtype = newEnrichedSvcObj.pop('svc_typ', None)     # get and remove
            if objtype != None:
                objtype = self.lookupObjType(objtype)
            protoId = newEnrichedSvcObj.pop('ip_proto', None)     # get and remove
            if protoId != None:
                protoId = self.lookupProtoId(protoId)

            rpcNr = newEnrichedSvcObj.pop('rpc_nr', None)     # get and remove

            newEnrichedSvcObj.update({
                    'mgm_id': self.ImportDetails.MgmDetails.Id,
                    'svc_create': self.ImportDetails.ImportId,
                    'svc_last_seen': self.ImportDetails.ImportId,   # could be left out
                    'svc_color_id': color,
                    'svc_typ_id': objtype,
                    'ip_proto_id': protoId,
                    'svc_rpcnr': rpcNr
                })
            newObjs.append(newEnrichedSvcObj)
        return newObjs

    def addNewObjects(self, newNwObjectUids, newSvcObjectUids):
        logger = getFwoLogger()
        errors = 0
        changes = 0
        import_mutation = """
            mutation insertObjects($newNwObjects: [object_insert_input!]!, $newSvcObjects: [service_insert_input!]!) {
                insert_object(objects: $newNwObjects) {
                    affected_rows
                }
                insert_service(objects: $newSvcObjects) {
                    affected_rows
                }
            }
        """
        queryVariables = { 
            'newNwObjects': self.prepareNewNwObjects(newNwObjectUids),
            'newSvcObjects': self.prepareNewSvcObjects(newSvcObjectUids),
        }
        
        # examples: 
        # {'obj_uid': 'd379bf1c-7e14-46f4-8a8c-9c19020c8ff2', 'obj_name': 'testHost_7.7.7.7', 'obj_comment': None, 'obj_ip': '7.7.7.7/32', 'obj_ip_end': '7.7.7.7/32', 'obj_member_refs': None, 'obj_member_names': None, 'mgm_id': 26, 'obj_created': 33690, 'obj_last_seen': 33690, 'obj_color_id': None, 'obj_typ_id': 1}
        # {"svc_uid": "d379bf1c-7e14-46f4-8a8c-9c19020c8ff2", "svc_name": "ntp-dienst", "svc_comment": null, "ip_proto_id": "17", "svc_port": 123, "svc_member_refs": null, "svc_member_names": null, "mgm_id": 26, "svc_create": 33690, "svc_last_seen": 33690, "svc_color_id": null, "svc_typ_id": 1 }

        try:
            import_result = self.call(import_mutation, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception(f"fwo_api:importNwObject - error while adding new nw objects: {str(import_result['errors'])}")
                errors = 1
            else:
                changes = int(import_result['data']['insert_object']['affected_rows']) + \
                    int(import_result['data']['insert_service']['affected_rows'])
        except:
            logger.exception(f"failed to write new objects: {str(traceback.format_exc())}")
            errors = 1
        
        return errors, changes


    def updateNetworkObject(nwObjUid):
        pass

    def deleteNetworkObject(nwObjUid):
        pass


    def insertRulebase(self, ruleBaseName, isGlobal=False):
        # call for each rulebase to add
        query_variables = {
            "rulebase": {
                "is_global": isGlobal,
                "mgm_id": self.ImportDetails.MgmDetails.Id,
                "name": ruleBaseName,
                "created": self.ImportDetails.ImportId
            }
        }
        mutation = """
            mutation insertRulebase($rulebase: [rulebase_insert_input!]!) {
                insert_rulebase(objects: $rulebase) {
                    returning {id}
                }
            }"""
        return self.call(mutation, queryVariables=query_variables)


    def insertRulesEnforcedOnGateway(self, ruleIds, devId):
        rulesEnforcedOnGateway = []
        for ruleId in ruleIds:
            rulesEnforcedOnGateway.append({
                "rule_id": ruleId,
                "dev_id": devId,
                "created": self.ImportDetails.ImportId
            })

        query_variables = {
            "ruleEnforcedOnGateway": rulesEnforcedOnGateway
        }
        mutation = """
            mutation importInsertRulesEnforcedOnGateway($rulesEnforcedOnGateway: [rule_enforced_on_gateway_insert_input!]!) {
                insert_rule_enforced_on_gateway(objects: $rulesEnforcedOnGateway) {
                    affected_rows
                }
            }"""
        
        return self.call(mutation, queryVariables=query_variables)


    def importInsertRulebaseOnGateway(self, rulebaseId, devId, orderNo=0):
        query_variables = {
            "rulebase2gateway": [
                {
                    "dev_id": devId,
                    "rulebase_id": rulebaseId,
                    "order_no": orderNo
                }
            ]
        }
        mutation = """
            mutation importInsertRulebaseOnGateway($rulebase2gateway: [rulebase_on_gateway_insert_input!]!) {
                insert_rulebase_on_gateway(objects: $rulebase2gateway) {
                    affected_rows
                }
            }"""
        
        return self.call(mutation, queryVariables=query_variables)

    # def resolveRuleRefs(self, rule2Import, refLists):
    #     actionId = refLists['action'][rule2Import['action']]
    #     # ...
    #     rule = Rule(actionId=actionId)

    def importLatestConfig(self, config):
        logger = getFwoLogger()
        import_mutation = """
            mutation importLatestConfig($importId: bigint!, $mgmId: Int!, $config: jsonb!) {
                insert_latest_config(objects: {import_id: $importId, mgm_id: $mgmId, config: $config}) {
                    affected_rows
                }
            }
        """
        try:
            queryVariables = {
                'mgmId': self.ImportDetails.MgmDetails.Id,
                'importId': self.ImportDetails.ImportId,
                'config': config
            }
            import_result = self.call(import_mutation, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception("fwo_api:import_latest_config - error while writing importable config for mgm id " +
                                str(self.ImportDetails.MgmDetails.Id) + ": " + str(import_result['errors']))
                return 1 # error
            else:
                changes = import_result['data']['insert_latest_config']['affected_rows']
        except:
            logger.exception(f"failed to write latest normalized config for mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
            return 1 # error
        
        if changes==1:
            return 0
        else:
            return 1
        

    def deleteLatestConfig(self):
        logger = getFwoLogger()
        import_mutation = """
            mutation deleteLatestConfig($mgmId: Int!) {
                delete_latest_config(where: { mgm_id: {_eq: $mgmId} }) {
                    affected_rows
                }
            }
        """
        try:
            queryVariables = { 'mgmId': self.ImportDetails.MgmDetails.Id }
            import_result = self.call(import_mutation, queryVariables=queryVariables)
            if 'errors' in import_result:
                logger.exception("fwo_api:import_latest_config - error while deleting last config for mgm id " +
                                str(self.ImportDetails.MgmDetails.Id) + ": " + str(import_result['errors']))
                return 1 # error
            else:
                changes = import_result['data']['delete_latest_config']['affected_rows']
        except:
            logger.exception(f"failed to delete latest normalized config for mgm id {str(self.ImportDetails.MgmDetails.Id)}: {str(traceback.format_exc())}")
            return 1 # error
        
        if changes<=1:  # if nothing was changed, we are also happy (assuming this to be the first config of the current management)
            return 0
        else:
            return 1

    def convertToBase(self):
        return FwConfigNormalized(action=self.action, 
                                  network_objects=self.network_objects, 
                                  service_objects=self.service_objects, 
                                  users=self.users,
                                  zone_objects=self.zone_objects,
                                  rules=self.rules,
                                  gateways=self.gateways,
                                  ConfigFormat=self.ConfigFormat)

    def storeConfigToApi(self):

        conf = self.convertToBase() # convert FwConfigImport to FwConfigNormalized
        # conf.writeNormalizedConfigToFile(self.ImportDetails)

        if self.ImportDetails.ImportVersion>8:
            errorsFound = self.deleteLatestConfig()
            if errorsFound:
                getFwoLogger().warning(f"error while trying to delete latest config for mgm_id: {self.ImportDetails.ImportId}")
            errorsFound = self.importLatestConfig(conf.toJsonString(prettyPrint=False))
            if errorsFound:
                getFwoLogger().warning(f"error while writing latest config for mgm_id: {self.ImportDetails.ImportId}")


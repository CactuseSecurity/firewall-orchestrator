import json
from fwo_log import getFwoLogger
import time
from copy import deepcopy

import cp_rule
import cp_const, cp_network, cp_service
import cp_getter
import cp_gateway
import fwo_exceptions
from fwconfig_base import calcManagerUidHash
from models.fwconfigmanagerlist import FwConfigManagerList, FwConfigManager
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.fwconfig_normalized import FwConfigNormalized
from model_controllers.import_state_controller import ImportStateController
from fwo_base import ConfigAction
import fwo_const
import fwo_globals
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from fwo_exceptions import ImportInterruption
from models.management_details import ManagementDetails
from models.import_state import ImportState


def has_config_changed (full_config, importState: ImportState, force=False):

    if full_config != {}:   # a config was passed in (read from file), so we assume that an import has to be done (simulating changes here)
        return 1

    session_id = loginCp(importState.MgmDetails)

    if importState.LastSuccessfulImport==None or importState.LastSuccessfulImport=='' or force:
        # if no last import time found or given or if force flag is set, do full import
        result = 1
    else: # otherwise search for any changes since last import
        result = (cp_getter.get_changes(session_id, importState.MgmDetails.Hostname, str(importState.MgmDetails.Port),importState.LastSuccessfulImport) != 0)

    logout_cp(importState.MgmDetails.buildFwApiString(), session_id)

    return result


def get_config(nativeConfig: json, importState: ImportStateController) -> tuple[int, FwConfigManagerList]:

    logger = getFwoLogger()
    normalizedConfigDict = fwo_const.emptyNormalizedFwConfigJsonDict
    logger.debug ( "starting checkpointR8x/get_config" )

    if nativeConfig == {}:   # no native config was passed in, so getting it from FW-Manager
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # get config from cp fw mgr
        starttime = int(time.time())

        if 'users' not in nativeConfig:
            nativeConfig.update({'users': {}})

        cpManagerApiBaseUrl = importState.MgmDetails.buildFwApiString()

        sid = loginCp(importState.MgmDetails)

        starttimeTemp = int(time.time())
        logger.debug ( "checkpointR8x/get_config/getting objects ...")

        #result_get_objects = get_objects (nativeConfig, importState.MgmDetails, cpManagerApiBaseUrl, sid, force=importState.ForceImport, limit=str(importState.FwoConfig.ApiFetchSize), details_level=cp_const.details_level_objects, test_version='off')
        result_get_objects = getObjects (nativeConfig, importState)
        if result_get_objects>0:
            logger.warning ( "checkpointR8x/get_config/error while gettings objects")
            return result_get_objects
        logger.debug ( "checkpointR8x/get_config/fetched objects in " + str(int(time.time()) - starttimeTemp) + "s")


        # delete_v prüfe nativeConfig, kann später weg
        # inDom1ButNotInDom2List = {}
        # for dom1Types in nativeConfig['object_tables']['c0f60e6c-23af-4bcb-be0b-26f79d734995']:
        #     currentType = dom1Types['object_type']
        #     inDom1ButNotInDom2List.update({currentType: []})
        #     for dom1Chunk in dom1Types['chunks']:
        #         if 'objects' in dom1Chunk:
        #             for dom1Object in dom1Chunk['objects']:
        #                 if 'uid' in dom1Object:
        #                     found = False

        #                     for dom2Types in nativeConfig['object_tables']['a0bbbc99-adef-4ef8-bb6d-defdefdefdef']:
        #                         if currentType == dom2Types['object_type']:
        #                             for dom2Chunk in dom2Types['chunks']:
        #                                 if 'objects' in dom2Chunk:
        #                                     for dom2Object in dom2Chunk['objects']:
        #                                         if 'uid' in dom2Object:
        #                                             if dom1Object['uid'] == dom2Object['uid']:
        #                                                 found = True

        #                     if not found:
        #                         inDom1ButNotInDom2List[currentType].append(dom1Object['uid'])



        starttimeTemp = int(time.time())
        logger.debug ( "checkpointR8x/get_config/getting rules ...")
        result_get_rules = get_rules (nativeConfig, importState)
        if result_get_rules>0:
            logger.warning ( "checkpointR8x/get_config/error while gettings rules")
            return result_get_rules
        logger.debug ( "checkpointR8x/get_config/fetched rules in " + str(int(time.time()) - starttimeTemp) + "s")

        duration = int(time.time()) - starttime
        logger.debug ( "checkpointR8x/get_config - fetch duration: " + str(duration) + "s" )

    normalizedConfig = normalizeConfig(nativeConfig, normalizedConfigDict, importState, parsing_config_only, sid)

    manager = FwConfigManager(ManagerUid=calcManagerUidHash(importState.MgmDetails),
                              ManagerName=importState.MgmDetails.Name,
                              IsGlobal=False, 
                              DependantManagerUids=[], 
                              Configs=[normalizedConfig])
    
    listOfManagers = FwConfigManagerListController()
    listOfManagers.addManager(manager)
    logger.info("completed getting config")
    
    return 0, listOfManagers

def normalizeConfig(nativeConfig: json, normalizedConfigDict, importState: ImportStateController, parsing_config_only: bool, sid: str) -> tuple[int, FwConfigManagerList]:
    logger = getFwoLogger()
    cp_network.normalize_network_objects(nativeConfig, normalizedConfigDict, importState.ImportId, mgm_id=importState.MgmDetails.Id)
    logger.info("completed normalizing network objects")
    cp_service.normalize_service_objects(nativeConfig, normalizedConfigDict, importState.ImportId)
    logger.info("completed normalizing service objects")
    cp_rule.normalizeRulebases(nativeConfig, importState, normalizedConfigDict)
    cp_gateway.normalizeGateways(nativeConfig, importState, normalizedConfigDict)
    if not parsing_config_only: # get config from cp fw mgr
        logout_cp(importState.MgmDetails.buildFwApiString(), sid)
    logger.info("completed normalizing rulebases")
    
    # put dicts into object of class FwConfigManager
    return FwConfigNormalized(
        action=ConfigAction.INSERT, 
        network_objects=FwConfigNormalizedController.convertListToDict(normalizedConfigDict['network_objects'], 'obj_uid'),
        service_objects=FwConfigNormalizedController.convertListToDict(normalizedConfigDict['service_objects'], 'svc_uid'),
        zone_objects=normalizedConfigDict['zone_objects'],
        rulebases=normalizedConfigDict['policies'],
        gateways=normalizedConfigDict['gateways']
    )

def get_rules(nativeConfig: dict, importState: ImportStateController) -> int:
    """
    Main function to get rules. Divided into smaller sub-tasks for better readability and maintainability.
    """
    initialize_native_config(nativeConfig)
    show_params_policy_structure = {
        'limit': importState.FwoConfig.ApiFetchSize,
        'details-level': 'full'
    }

    globalAssignments, globalPolicyStructure, globalDomain, globalSid = None, None, None, None
    manager_details_list = enrich_submanager_details(importState)
    for managerDetails in manager_details_list:
        cpManagerApiBaseUrl = importState.MgmDetails.buildFwApiString()

        if managerDetails.IsSuperManager:
            globalAssignments, globalPolicyStructure, globalDomain, globalSid = handle_super_manager(
                managerDetails, cpManagerApiBaseUrl, show_params_policy_structure
            )

        sid = loginCp(managerDetails)
        policyStructure = get_policy_structure(cpManagerApiBaseUrl, sid, show_params_policy_structure)

        process_devices(
            managerDetails, policyStructure, globalAssignments, globalPolicyStructure,
            globalDomain, globalSid, cpManagerApiBaseUrl, sid, nativeConfig, importState
        )

    return 0


def initialize_native_config(nativeConfig):
    nativeConfig.update({'rulebases': [], 'nat_rulebases': [], 'gateways': []})


def enrich_submanager_details(importState):
    managerDetailsList = [deepcopy(importState.MgmDetails)]
    if importState.MgmDetails.IsSuperManager:
        for subManager in importState.MgmDetails.SubManagers:
            managerDetailsList.append(deepcopy(subManager))
    return managerDetailsList


def handle_super_manager(managerDetails, cpManagerApiBaseUrl, show_params_policy_structure):

    logger = getFwoLogger()

    # global assignments are fetched from mds domain
    mdsSid = loginCp(managerDetails)
    globalAssignments = []
    cp_getter.getGlobalAssignments(
        cpManagerApiBaseUrl, mdsSid, show_params_policy_structure, globalAssignments=globalAssignments
    )

    # import global policies if at least one global assignment exists
    if len(globalAssignments) > 0:

        if 'global-domain' in globalAssignments[0] and 'uid' in globalAssignments[0]['global-domain']:
            global_domain = globalAssignments[0]['global-domain']['uid']

            # policy structure is fetched from global domain
            globalPolicyStructure = []
            managerDetails.DomainUid = global_domain
            global_sid = loginCp(managerDetails)
            cp_getter.getPolicyStructure(
                cpManagerApiBaseUrl, global_sid, show_params_policy_structure, policyStructure=globalPolicyStructure
            )
        else:
            logger.warning(f"Unexpected globalAssignments: {str(globalAssignments)}")
    else:
        globalPolicyStructure, global_domain, global_sid = None, None, None

    return globalAssignments, globalPolicyStructure, global_domain, global_sid


def get_policy_structure(cpManagerApiBaseUrl, sid, show_params_policy_structure):
    pol_structure = []
    cp_getter.getPolicyStructure(
        cpManagerApiBaseUrl, sid, show_params_policy_structure, policyStructure=pol_structure
    )
    return pol_structure


def process_devices(
    managerDetails, policyStructure, globalAssignments, globalPolicyStructure,
    globalDomain, globalSid, cpManagerApiBaseUrl, sid, nativeConfig, importState
):
    logger = getFwoLogger()
    for device in managerDetails.Devices:
        deviceConfig = initialize_device_config(device)
        if not deviceConfig:
            continue

        orderedLayerUids = getOrderedLayerUids(policyStructure, deviceConfig, managerDetails.getDomainString())
        if not orderedLayerUids:
            logger.warning(f"No ordered layers found for device: {deviceConfig['name']}")
            continue

        if importState.MgmDetails.IsSuperManager:
            handle_global_rulebase_links(
                managerDetails, importState, deviceConfig, globalAssignments, globalPolicyStructure, globalDomain,
                globalSid, orderedLayerUids, nativeConfig, cpManagerApiBaseUrl
            )
        else:
            define_initial_rulebase(deviceConfig, orderedLayerUids)

        add_ordered_layers_to_native_config(orderedLayerUids, get_rules_params(importState), cpManagerApiBaseUrl, sid, nativeConfig, deviceConfig)
        handle_nat_rules(device, nativeConfig, sid, importState)

        nativeConfig['gateways'].append(deviceConfig)


def initialize_device_config(device):
    if 'name' in device and 'uid' in device:
        return {'name': device['name'], 'uid': device['uid'], 'rulebase_links': []}
    logger = getFwoLogger()
    logger.error(f"Device missing name or uid: {device}")
    return None


def handle_global_rulebase_links(
    managerDetails, import_state, deviceConfig, globalAssignments, globalPolicyStructure, globalDomain,
    globalSid, orderedLayerUids, nativeConfig, cpManagerApiBaseUrl):

    logger = getFwoLogger()
    for globalAssignment in globalAssignments:
        if globalAssignment['dependent-domain']['uid'] == managerDetails.getDomainString():
            for globalPolicy in globalPolicyStructure:
                if globalPolicy['name'] == globalAssignment['global-access-policy']:
                    global_ordered_layer_uids = getOrderedLayerUids([globalPolicy], deviceConfig, globalDomain)
                    if not global_ordered_layer_uids:
                        logger.warning(f"No access layer for global policy: {globalPolicy['name']}")
                        break

                    add_ordered_layers_to_native_config(global_ordered_layer_uids, get_rules_params(import_state), cpManagerApiBaseUrl, globalSid, nativeConfig, deviceConfig)
                    define_global_rulebase_link(deviceConfig, global_ordered_layer_uids, orderedLayerUids, nativeConfig)


def define_global_rulebase_link(deviceConfig, globalOrderedLayerUids, orderedLayerUids, nativeConfig):
    # define initial rulebase for device in case of mds
    define_initial_rulebase(deviceConfig, globalOrderedLayerUids)

    # parse global rulebase, find place-holder and link local rulebase (first ordered layer)
    for globalOrderedLayerUid in globalOrderedLayerUids:
        placeholderRuleUid = ''
        for rulebase in nativeConfig['rulebases']:
            if rulebase['uid'] == globalOrderedLayerUid:
                placeholderRuleUid = cp_getter.getRuleUid(rulebase, 'place-holder')
                if placeholderRuleUid:
                    break

        if placeholderRuleUid:
            deviceConfig['rulebase_links'].append({
                'from_rulebase_uid': globalOrderedLayerUid,
                'from_rule_uid': placeholderRuleUid,
                'to_rulebase_uid': orderedLayerUids[0],
                'type': 'ordered',
                'is_global': True,
                'is_initial': False,
            })


def define_initial_rulebase(deviceConfig, orderedLayerUids):
    deviceConfig['rulebase_links'].append({
        'from_rulebase_uid': '',
        'from_rule_uid': '',
        'to_rulebase_uid': orderedLayerUids[0],
        'type': 'ordered',
        'is_global': False,
        'is_initial': True
    })


def get_rules_params(importState):
    return {
        'limit': importState.FwoConfig.ApiFetchSize,
        'use-object-dictionary': cp_const.use_object_dictionary,
        'details-level': 'standard',
        'show-hits': cp_const.with_hits
    }


def handle_nat_rules(device, nativeConfig, sid, importState):
    logger = getFwoLogger()
    if 'package_name' in device and device['package_name']:
        show_params_rules = {
            'limit': importState.FwoConfig.ApiFetchSize,
            'use-object-dictionary': cp_const.use_object_dictionary,
            'details-level': 'standard',
            'package': device['package_name']
        }
        if importState.DebugLevel > 3:
            logger.debug(f"Getting NAT rules for package: {device['package_name']}")
        nat_rules = cp_getter.get_nat_rules_from_api_as_dict(
            importState.MgmDetails.buildFwApiString(), sid, show_params_rules, nativeConfig=nativeConfig
        )
        if nat_rules:
            nativeConfig['nat_rulebases'].append(nat_rules)
        else:
            nativeConfig['nat_rulebases'].append({"nat_rule_chunks": []})
    else:
        nativeConfig['nat_rulebases'].append({"nat_rule_chunks": []})


###### helper functions ######
def add_ordered_layers_to_native_config(orderedLayerUids, show_params_rules, cpManagerApiBaseUrl, sid, nativeConfig, deviceConfig):

    orderedLayerIndex = 0
    for orderedLayerUid in orderedLayerUids:

        show_params_rules.update({'uid': orderedLayerUid})

        cp_getter.get_rulebases (cpManagerApiBaseUrl, 
                                sid, 
                                show_params_rules, 
                                rulebaseUid=orderedLayerUid,
                                access_type='access',
                                nativeConfig=nativeConfig,
                                deviceConfig=deviceConfig)
        if fwo_globals.shutdown_requested:
            raise ImportInterruption("Shutdown requested during rulebase retrieval.")
                    
        lastRuleUid = None
        # parse ordered layer and get last rule uid
        for rulebase in nativeConfig['rulebases']:
            if rulebase['uid'] == orderedLayerUid:
                lastRuleUid = cp_getter.getRuleUid(rulebase, 'last')
                break
        
        # link to next ordered layer
        if orderedLayerIndex < len(orderedLayerUids) - 1:
            deviceConfig['rulebase_links'].append({
                'from_rulebase_uid': orderedLayerUid,
                'from_rule_uid': lastRuleUid,
                'to_rulebase_uid': orderedLayerUids[orderedLayerIndex + 1],
                'type': 'ordered',
                'is_global': False,
                'is_initial': False
            })
        
        orderedLayerIndex += 1

    return 0


def getOrderedLayerUids(policyStructure, deviceConfig, domain):

    orderedLayerUids = []
    for policy in policyStructure:
        foundTargetInPolciy = False
        for target in policy['targets']:
            if target['uid'] == deviceConfig['uid'] or target['uid'] == 'all':
                foundTargetInPolciy = True
        if foundTargetInPolciy:
            for accessLayer in policy['access-layers']:
                if accessLayer['domain'] == domain or domain == '':
                    orderedLayerUids.append(accessLayer['uid'])

    return orderedLayerUids


def loginCp(mgm_details, ssl_verification=True):
    try: # top level dict start, sid contains the domain information, so only sending domain during login
        login_result = cp_getter.login(mgm_details)
        return login_result
    except Exception:
        raise fwo_exceptions.FwLoginFailed
    

def logout_cp(url, sid):
    try:
        logout_result = cp_getter.logout(url, sid)
        return logout_result
    except Exception:
        logger = getFwoLogger()
        logger.warning("logout from CP management failed")


def getObjects (nativeConfig: dict, importState: ImportStateController) -> int:

    logger = getFwoLogger()
    nativeConfig.update({'object_domains': []})
    show_params_objs = {'limit': importState.FwoConfig.ApiFetchSize}

    # control standalone vs mds
    managerDetailsList = []
    managerDetailsList = [deepcopy(importState.MgmDetails)]
    if importState.MgmDetails.IsSuperManager:
        for subManager in importState.MgmDetails.SubManagers:
            managerDetailsList.append(deepcopy(subManager))
    else:
        managerDetailsList.append(deepcopy(importState.MgmDetails))
            
    # loop over sub-managers in case of mds
    manager_index = 0
    for managerDetails in managerDetailsList:
        cpManagerApiBaseUrl = importState.MgmDetails.buildFwApiString()

        sid = loginCp(managerDetails)
        nativeConfig['object_domains'].append({
            'domain_name': managerDetails.DomainName,
            'domain_uid': managerDetails.DomainUid,
            'object_types': []})
        
        # getting Original (NAT) object (both for networks and services)
        if manager_index == 0:
            origObj = cp_getter.getObjectDetailsFromApi(cp_const.original_obj_uid, sid=sid, apiurl=cpManagerApiBaseUrl)['chunks'][0]
            anyObj = cp_getter.getObjectDetailsFromApi(cp_const.any_obj_uid, sid=sid, apiurl=cpManagerApiBaseUrl)['chunks'][0]
            noneObj = cp_getter.getObjectDetailsFromApi(cp_const.none_obj_uid, sid=sid, apiurl=cpManagerApiBaseUrl)['chunks'][0]
            internetObj = cp_getter.getObjectDetailsFromApi(cp_const.internet_obj_uid, sid=sid, apiurl=cpManagerApiBaseUrl)['chunks'][0]

        # get all objects
        for obj_type in cp_const.api_obj_types:
            if fwo_globals.shutdown_requested:
                raise ImportInterruption("Shutdown requested during object retrieval.")
            if obj_type in cp_const.obj_types_full_fetch_needed:
                show_params_objs.update({'details-level': cp_const.details_level_group_objects})
            else:
                show_params_objs.update({'details-level': cp_const.details_level_objects})
            object_table = { "type": obj_type, "chunks": [] }
            current=0
            total=current+1
            show_cmd = 'show-' + obj_type    
            # if debug_level>5:
            #     logger.debug ( "obj_type: "+ obj_type )

            while (current<total) :
                show_params_objs['offset']=current
                objects = cp_getter.cp_api_call(cpManagerApiBaseUrl, show_cmd, show_params_objs, sid)
                if fwo_globals.shutdown_requested:
                    raise ImportInterruption("Shutdown requested during object retrieval.")

                object_table["chunks"].append(objects)
                if 'total' in objects  and 'to' in objects:
                    total=objects['total']
                    current=objects['to']
                    # if debug_level>5:
                    #     logger.debug ( obj_type +" current:"+ str(current) + " of a total " + str(total) )
                else :
                    current = total
                    # if debug_level>5:
                    #     logger.debug ( obj_type +" total:"+ str(total) )

            # adding the uid of the Original, Any and None objects (as separate chunks):
            if manager_index == 0:
                if obj_type == 'networks':
                    object_table['chunks'].append(origObj)
                    object_table['chunks'].append(anyObj)
                    object_table['chunks'].append(noneObj)
                    object_table['chunks'].append(internetObj)
                if obj_type == 'services-other':
                    object_table['chunks'].append(origObj)
                    object_table['chunks'].append(anyObj)
                    object_table['chunks'].append(noneObj)

            nativeConfig['object_domains'][manager_index]['object_types'].append(object_table)
        manager_index += 1

    return 0


# delete_v komplett löschen wenn getObjects fertig
# def get_objects(config_json, mgm_details, v_url, sid, force=False, config_filename=None,
#     limit=150, details_level=cp_const.details_level_objects, test_version='off', debug_level=0, ssl_verification=True):

#     logger = getFwoLogger()

#     config_json["object_tables"] = []
#     show_params_objs = {'limit':limit,'details-level': details_level }

#     # getting Original (NAT) object (both for networks and services)
#     origObj = cp_getter.getObjectDetailsFromApi(cp_const.original_obj_uid, sid=sid, apiurl=v_url, debug_level=debug_level)['chunks'][0]
#     anyObj = cp_getter.getObjectDetailsFromApi(cp_const.any_obj_uid, sid=sid, apiurl=v_url, debug_level=debug_level)['chunks'][0]
#     noneObj = cp_getter.getObjectDetailsFromApi(cp_const.none_obj_uid, sid=sid, apiurl=v_url, debug_level=debug_level)['chunks'][0]
#     internetObj = cp_getter.getObjectDetailsFromApi(cp_const.internet_obj_uid, sid=sid, apiurl=v_url, debug_level=debug_level)['chunks'][0]

#     for obj_type in cp_const.api_obj_types:
#         if fwo_globals.shutdown_requested:
#             raise ImportInterruption("Shutdown requested during object retrieval.")
#         if obj_type in cp_const.obj_types_full_fetch_needed:
#             show_params_objs.update({'details-level': cp_const.details_level_group_objects})
#         else:
#             show_params_objs.update({'details-level': cp_const.details_level_objects})
#         object_table = { "object_type": obj_type, "chunks": [] }
#         current=0
#         total=current+1
#         show_cmd = 'show-' + obj_type    
#         if debug_level>5:
#             logger.debug ( "obj_type: "+ obj_type )
#         while (current<total) :
#             show_params_objs['offset']=current
#             objects = cp_getter.cp_api_call(v_url, show_cmd, show_params_objs, sid)
#             if fwo_globals.shutdown_requested:
#                 raise ImportInterruption("Shutdown requested during object retrieval.")

#             object_table["chunks"].append(objects)
#             if 'total' in objects  and 'to' in objects:
#                 total=objects['total']
#                 current=objects['to']
#                 if debug_level>5:
#                     logger.debug ( obj_type +" current:"+ str(current) + " of a total " + str(total) )
#             else :
#                 current = total
#                 if debug_level>5:
#                     logger.debug ( obj_type +" total:"+ str(total) )

#         # adding the uid of the Original, Any and None objects (as separate chunks):
#         if obj_type == 'networks':
#             object_table['chunks'].append(origObj)
#             object_table['chunks'].append(anyObj)
#             object_table['chunks'].append(noneObj)
#             object_table['chunks'].append(internetObj)
#         if obj_type == 'services-other':
#             object_table['chunks'].append(origObj)
#             object_table['chunks'].append(anyObj)
#             object_table['chunks'].append(noneObj)

#         config_json["object_tables"].append(object_table)

#     # only write config to file if config_filename is given
#     if config_filename != None and len(config_filename)>1:
#         with open(config_filename, "w") as configfile_json:
#             configfile_json.write(json.dumps(config_json))
#     return 0

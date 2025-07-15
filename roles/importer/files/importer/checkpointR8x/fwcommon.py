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
    logger.debug ( "starting checkpointR8x/get_config" )

    if nativeConfig == {}:   # no native config was passed in, so getting it from FW-Manager
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # get config from cp fw mgr
        starttime = int(time.time())

        initialize_native_config(nativeConfig, importState)

        # delete_v: brauchen wir users?
        # if 'users' not in nativeConfig:
        #     nativeConfig.update({'users': {}})

        start_time_temp = int(time.time())
        logger.debug ( "checkpointR8x/get_config/getting objects ...")

        result_get_objects = get_objects(nativeConfig, importState)
        if result_get_objects>0:
            logger.warning ( "checkpointR8x/get_config/error while gettings objects")
            return result_get_objects
        logger.debug ( "checkpointR8x/get_config/fetched objects in " + str(int(time.time()) - start_time_temp) + "s")

        start_time_temp = int(time.time())
        logger.debug ( "checkpointR8x/get_config/getting rules ...")
        result_get_rules = get_rules (nativeConfig, importState)
        if result_get_rules>0:
            logger.warning ( "checkpointR8x/get_config/error while gettings rules")
            return result_get_rules
        logger.debug ( "checkpointR8x/get_config/fetched rules in " + str(int(time.time()) - start_time_temp) + "s")

        duration = int(time.time()) - starttime
        logger.debug ( "checkpointR8x/get_config - fetch duration: " + str(duration) + "s" )

    sid = loginCp(importState.MgmDetails)

    normalizedConfig = normalize_config(importState, nativeConfig, parsing_config_only, sid)
    logger.info("completed getting config")
    return 0, normalizedConfig


def initialize_native_config(nativeConfig, importState):
    """
    create domain structure in nativeConfig
    """

    manager_details_list = enrich_submanager_details(importState)
    nativeConfig.update({'domains': []})
    for managerDetails in manager_details_list:

        nativeConfig['domains'].append({
            'domain_name': managerDetails.DomainName,
            'domain_uid': managerDetails.DomainUid,
            'is-super-manger': managerDetails.IsSuperManager,
            'management_name': managerDetails.Name,
            'management_uid': managerDetails.Uid,
            'objects': [],
            'rulebases': [],
            'nat_rulebases': [],
            'gateways': []})


def normalize_config(import_state, native_config: json, parsing_config_only: bool, sid: str) -> FwConfigManagerListController:

    manager_list = FwConfigManagerListController()

    if 'domains' not in native_config:
        getFwoLogger().error("No domains found in native config. Cannot normalize config.")
        raise ImportInterruption("No domains found in native config. Cannot normalize config.")
    
    for native_conf in native_config['domains']:
        normalizedConfigDict = fwo_const.emptyNormalizedFwConfigJsonDict
        normalized_config = normalize_single_manager_config(native_conf, normalizedConfigDict, import_state, parsing_config_only, sid)
        manager = FwConfigManager(  ManagerName=native_conf['management_name'],
                                    ManagerUid=native_conf['management_uid'],
                                    IsGlobal=native_conf['is-super-manger'],
                                    IsSuperManager=native_conf['is-super-manger'],
                                    DependantManagerUids=[], 
                                    DomainName=native_conf['domain_name'],
                                    DomainUid=native_conf['domain_uid'],
                                    Configs=[normalized_config])
        manager_list.addManager(manager)

    return manager_list


def normalize_single_manager_config(nativeConfig: json, normalizedConfigDict, importState: ImportStateController, parsing_config_only: bool, sid: str) -> tuple[int, FwConfigManagerList]:
    logger = getFwoLogger()
    cp_network.normalize_network_objects(nativeConfig, normalizedConfigDict, importState.ImportId, mgm_id=importState.MgmDetails.Id)
    logger.info("completed normalizing network objects")
    cp_service.normalize_service_objects(nativeConfig, normalizedConfigDict, importState.ImportId)
    logger.info("completed normalizing service objects")
    cp_gateway.normalizeGateways(nativeConfig, importState, normalizedConfigDict)
    cp_rule.normalizeRulebases(nativeConfig, importState, normalizedConfigDict)
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
    show_params_policy_structure = {
        'limit': importState.FwoConfig.ApiFetchSize,
        'details-level': 'full'
    }

    globalAssignments, globalPolicyStructure, globalDomain, globalSid = None, None, None, None
    manager_details_list = enrich_submanager_details(importState)
    manager_index = 0
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
            globalDomain, globalSid, cpManagerApiBaseUrl, sid, nativeConfig['domains'][manager_index],
            nativeConfig['domains'][0], importState
        )
        manager_index += 1


    return 0    


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
    globalDomain, globalSid, cpManagerApiBaseUrl, sid, nativeConfigDomain,
    nativeConfigGlobalDomain, importState
):
    logger = getFwoLogger()
    for device in managerDetails.Devices:
        deviceConfig = initialize_device_config(device)
        if not deviceConfig:
            continue

        orderedLayerUids = get_ordered_layer_uids(policyStructure, deviceConfig, managerDetails.getDomainString())
        if not orderedLayerUids:
            logger.warning(f"No ordered layers found for device: {deviceConfig['name']}")
            continue

        global_ordered_layer_count = 0
        if importState.MgmDetails.IsSuperManager:
            global_ordered_layer_count = handle_global_rulebase_links(
                managerDetails, importState, deviceConfig, globalAssignments, globalPolicyStructure, globalDomain,
                globalSid, orderedLayerUids, nativeConfigGlobalDomain, cpManagerApiBaseUrl
            )
        else:
            define_initial_rulebase(deviceConfig, orderedLayerUids, False)

        add_ordered_layers_to_native_config(orderedLayerUids,
            get_rules_params(importState), cpManagerApiBaseUrl, sid,
            nativeConfigDomain, deviceConfig, False, global_ordered_layer_count)
        
        handle_nat_rules(device, nativeConfigDomain, sid, importState)

        nativeConfigDomain['gateways'].append(deviceConfig)


def initialize_device_config(device):
    if 'name' in device and 'uid' in device:
        return {'name': device['name'], 'uid': device['uid'], 'rulebase_links': []}
    logger = getFwoLogger()
    logger.error(f"Device missing name or uid: {device}")
    return None


def handle_global_rulebase_links(
    managerDetails, import_state, deviceConfig, globalAssignments, globalPolicyStructure, globalDomain,
    globalSid, orderedLayerUids, nativeConfigGlobalDomain, cpManagerApiBaseUrl):
    """Searches for global access policy for current device policy,
    adds global ordered layers and defines global rulebase link
    """

    logger = getFwoLogger()
    for globalAssignment in globalAssignments:
        if globalAssignment['dependent-domain']['uid'] == managerDetails.getDomainString():
            for globalPolicy in globalPolicyStructure:
                if globalPolicy['name'] == globalAssignment['global-access-policy']:
                    global_ordered_layer_uids = get_ordered_layer_uids([globalPolicy], deviceConfig, globalDomain)
                    if not global_ordered_layer_uids:
                        logger.warning(f"No access layer for global policy: {globalPolicy['name']}")
                        break

                    global_ordered_layer_count = len(global_ordered_layer_uids)
                    global_policy_rulebases_uid_list = add_ordered_layers_to_native_config(global_ordered_layer_uids, get_rules_params(import_state),
                                                                                    cpManagerApiBaseUrl, globalSid, nativeConfigGlobalDomain, deviceConfig,
                                                                                    True, global_ordered_layer_count)
                    define_global_rulebase_link(deviceConfig, global_ordered_layer_uids, orderedLayerUids, nativeConfigGlobalDomain, global_policy_rulebases_uid_list)
                    
                    return global_ordered_layer_count


def define_global_rulebase_link(deviceConfig, globalOrderedLayerUids, orderedLayerUids, nativeConfigGlobalDomain, global_policy_rulebases_uid_list):
    """Links initial and placeholder rule for global rulebases
    """

    define_initial_rulebase(deviceConfig, globalOrderedLayerUids, True)

    # parse global rulebases, find place-holders and link local rulebases
    placeholder_link_index = 0
    for global_rulebase_uid in global_policy_rulebases_uid_list:
        placeholder_rule_uid = ''
        for rulebase in nativeConfigGlobalDomain['rulebases']:
            if rulebase['uid'] == global_rulebase_uid:
                placeholder_rule_uid, placeholder_rulebase_uid = cp_getter.get_placeholder_in_rulebase(rulebase)

                if placeholder_rule_uid:
                    orderedLayerUid =  ''
                    # we might find more than one placeholder, may be unequal to number of domain ordered layers
                    if len(orderedLayerUids) > placeholder_link_index:
                        orderedLayerUid = orderedLayerUids[placeholder_link_index]

                    deviceConfig['rulebase_links'].append({
                        'from_rulebase_uid': placeholder_rulebase_uid,
                        'from_rule_uid': placeholder_rule_uid,
                        'to_rulebase_uid': orderedLayerUid,
                        'type': 'domain',
                        'is_global': False,
                        'is_initial': False,
                        'is_section': False
                    })

                    placeholder_link_index += 1


def define_initial_rulebase(deviceConfig, orderedLayerUids, is_global):
    deviceConfig['rulebase_links'].append({
        'from_rulebase_uid': '',
        'from_rule_uid': '',
        'to_rulebase_uid': orderedLayerUids[0],
        'type': 'ordered',
        'is_global': is_global,
        'is_initial': True,
        'is_section': False
    })


def get_rules_params(importState):
    return {
        'limit': importState.FwoConfig.ApiFetchSize,
        'use-object-dictionary': cp_const.use_object_dictionary,
        'details-level': 'standard',
        'show-hits': cp_const.with_hits
    }


def handle_nat_rules(device, nativeConfigDomain, sid, importState):
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
            importState.MgmDetails.buildFwApiString(), sid, show_params_rules,
            nativeConfigDomain=nativeConfigDomain
        )
        if nat_rules:
            nativeConfigDomain['nat_rulebases'].append(nat_rules)
        else:
            nativeConfigDomain['nat_rulebases'].append({"nat_rule_chunks": []})
    else:
        nativeConfigDomain['nat_rulebases'].append({"nat_rule_chunks": []})


def add_ordered_layers_to_native_config(orderedLayerUids, show_params_rules,
                                        cpManagerApiBaseUrl, sid, nativeConfigDomain,
                                        deviceConfig, is_global, global_ordered_layer_count):
    """Fetches ordered layers and links them
    """
    orderedLayerIndex = 0
    policy_rulebases_uid_list = []
    for orderedLayerUid in orderedLayerUids:

        show_params_rules.update({'uid': orderedLayerUid})

        policy_rulebases_uid_list = cp_getter.get_rulebases(
            cpManagerApiBaseUrl, sid, show_params_rules, nativeConfigDomain,
            deviceConfig, policy_rulebases_uid_list,
            is_global=is_global, access_type='access',
            rulebaseUid=orderedLayerUid)
        
        if fwo_globals.shutdown_requested:
            raise ImportInterruption("Shutdown requested during rulebase retrieval.")
                    
        # link to next ordered layer
        # in case of mds: domain ordered layers are linked once there is no global ordered layer counterpart
        if is_global or orderedLayerIndex > global_ordered_layer_count - 1:
            if orderedLayerIndex < len(orderedLayerUids) - 1:
                deviceConfig['rulebase_links'].append({
                    'from_rulebase_uid': orderedLayerUid,
                    'from_rule_uid': '',
                    'to_rulebase_uid': orderedLayerUids[orderedLayerIndex + 1],
                    'type': 'ordered',
                    'is_global': is_global,
                    'is_initial': False,
                    'is_section': False
                })
        
        orderedLayerIndex += 1

    return policy_rulebases_uid_list


def get_ordered_layer_uids(policyStructure, deviceConfig, domain) -> list[str]:
    """Get UIDs of ordered layers for policy of device
    """

    orderedLayerUids = []
    for policy in policyStructure:
        foundTargetInPolciy = False
        for target in policy['targets']:
            if target['uid'] == deviceConfig['uid'] or target['uid'] == 'all':
                foundTargetInPolciy = True
        if foundTargetInPolciy:
            append_access_layer_uid(policy, domain, orderedLayerUids)

    return orderedLayerUids

def append_access_layer_uid(policy, domain, orderedLayerUids):
    for accessLayer in policy['access-layers']:
        if accessLayer['domain'] == domain or domain == '':
            orderedLayerUids.append(accessLayer['uid'])

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


def get_objects(nativeConfig: dict, importState: ImportStateController) -> int:

    show_params_objs = {'limit': importState.FwoConfig.ApiFetchSize}
    manager_details_list = enrich_submanager_details(importState)
            
    # loop over sub-managers in case of mds
    manager_index = 0
    for manager_details in manager_details_list:
        cp_api_url = importState.MgmDetails.buildFwApiString()
        
        # getting Original (NAT) object (both for networks and services)
        sid = loginCp(manager_details)
        if manager_details.IsSuperManager or len(manager_details_list) == 1:
            origObj = cp_getter.getObjectDetailsFromApi(cp_const.original_obj_uid, sid=sid, apiurl=cp_api_url)['chunks'][0]
            anyObj = cp_getter.getObjectDetailsFromApi(cp_const.any_obj_uid, sid=sid, apiurl=cp_api_url)['chunks'][0]
            noneObj = cp_getter.getObjectDetailsFromApi(cp_const.none_obj_uid, sid=sid, apiurl=cp_api_url)['chunks'][0]
            internetObj = cp_getter.getObjectDetailsFromApi(cp_const.internet_obj_uid, sid=sid, apiurl=cp_api_url)['chunks'][0]

        # get all objects
        if manager_index==0:
            obj_type_array = cp_const.api_obj_types
        else:
            obj_type_array = cp_const.local_api_obj_types

        for obj_type in obj_type_array:
            object_table = get_objects_per_type(obj_type, show_params_objs, sid, cp_api_url)
            add_special_objects_to_global_domain(object_table, manager_index, obj_type,
                                                 origObj, anyObj, noneObj, internetObj)
            remove_predefined_objects_for_domains(object_table, manager_index)
            nativeConfig['domains'][manager_index]['objects'].append(object_table)
        manager_index += 1

    return 0


def remove_predefined_objects_for_domains(object_table, manager_index):
    if not (manager_index>0 and 'chunks' in object_table and 'type' in object_table and \
        object_table['type'] in cp_const.types_to_remove_globals_from):
        return
    
    for chunk in object_table['chunks']:
        if 'objects' in chunk:
            for obj in chunk['objects']:
                domain_type = obj.get("domain", {}).get("domain-type", "")
                if domain_type != "domain":
                    chunk['objects'].remove(obj)


def get_objects_per_type(obj_type, show_params_objs, sid, cpManagerApiBaseUrl):
    logger = getFwoLogger()
    
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
    if fwo_globals.debug_level>5:
        logger.debug ( "obj_type: "+ obj_type )

    while (current<total) :
        show_params_objs['offset']=current
        objects = cp_getter.cp_api_call(cpManagerApiBaseUrl, show_cmd, show_params_objs, sid)
        if fwo_globals.shutdown_requested:
            raise ImportInterruption("Shutdown requested during object retrieval.")

        object_table["chunks"].append(objects)
        if 'total' in objects  and 'to' in objects:
            total=objects['total']
            current=objects['to']
            if fwo_globals.debug_level>5:
                logger.debug ( obj_type +" current:"+ str(current) + " of a total " + str(total) )
        else :
            current = total

    return object_table

def add_special_objects_to_global_domain(object_table, manager_index, obj_type,
                                         origObj, anyObj, noneObj, internetObj):
    """Appends special objects Original, Any, None and Internet to global domain
    """
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
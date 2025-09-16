from typing import Any
from fwo_log import getFwoLogger
import time
from copy import deepcopy

import cp_rule
import cp_const, cp_network, cp_service
import cp_getter
import cp_gateway
from fwo_exceptions import FwLoginFailed
from models.fwconfigmanagerlist import FwConfigManagerList, FwConfigManager
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.fwconfig_normalized import FwConfigNormalized
from model_controllers.import_state_controller import ImportStateController
from fwo_base import ConfigAction, ConfFormat
import fwo_const
import fwo_globals
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from fwo_exceptions import ImportInterruption, FwoImporterError
from models.management import Management
from models.import_state import ImportState


def has_config_changed (full_config, importState: ImportState, force=False):

    if full_config != {}:   # a config was passed in (read from file), so we assume that an import has to be done (simulating changes here)
        return 1

    session_id: str = cp_getter.login(importState.MgmDetails)

    if importState.LastSuccessfulImport==None or importState.LastSuccessfulImport=='' or force:
        # if no last import time found or given or if force flag is set, do full import
        result = 1
    else: # otherwise search for any changes since last import
        result = (cp_getter.get_changes(session_id, importState.MgmDetails.Hostname, str(importState.MgmDetails.Port),importState.LastSuccessfulImport) != 0)

    cp_getter.logout(importState.MgmDetails.buildFwApiString(), session_id)

    return result


def get_config(config_in: FwConfigManagerListController, importState: ImportStateController) -> tuple[int, FwConfigManagerList]:

    logger = getFwoLogger()
    logger.debug ( "starting checkpointR8x/get_config" )

    if config_in.has_empty_config():   # no native config was passed in, so getting it from FW-Manager
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # get config from cp fw mgr
        starttime = int(time.time())
        initialize_native_config(config_in, importState)

        start_time_temp = int(time.time())
        logger.debug ( "checkpointR8x/get_config/getting objects ...")

        result_get_objects = get_objects(config_in.native_config, importState)
        if result_get_objects>0:
            raise FwLoginFailed( "checkpointR8x/get_config/error while gettings objects")
        logger.debug ( "checkpointR8x/get_config/fetched objects in " + str(int(time.time()) - start_time_temp) + "s")

        start_time_temp = int(time.time())
        logger.debug ( "checkpointR8x/get_config/getting rules ...")
        result_get_rules = get_rules (config_in.native_config, importState)
        if result_get_rules>0:
            raise FwLoginFailed( "checkpointR8x/get_config/error while gettings rules")
        logger.debug ( "checkpointR8x/get_config/fetched rules in " + str(int(time.time()) - start_time_temp) + "s")

        duration = int(time.time()) - starttime
        logger.debug ( "checkpointR8x/get_config - fetch duration: " + str(duration) + "s" )

    if config_in.contains_only_native():
        sid: str = cp_getter.login(importState.MgmDetails)
        normalizedConfig = normalize_config(importState, config_in, parsing_config_only, sid)
        logger.info("completed getting config")
        return 0, normalizedConfig
    else:
        # we already have a native config (from file import) 
        return 0, config_in


def initialize_native_config(config_in: FwConfigManagerListController, importState: ImportStateController) -> None:
    """
    create domain structure in nativeConfig
    """

    manager_details_list = create_ordered_manager_list(importState)
    config_in.native_config.update({'domains': []})
    for managerDetails in manager_details_list:

        config_in.native_config['domains'].append({
            'domain_name': managerDetails.DomainName,
            'domain_uid': managerDetails.DomainUid,
            'is-super-manager': managerDetails.IsSuperManager,
            'management_name': managerDetails.Name,
            'management_uid': managerDetails.Uid,
            'objects': [],
            'rulebases': [],
            'nat_rulebases': [],
            'gateways': []})


def normalize_config(import_state, config_in: FwConfigManagerListController, parsing_config_only: bool, sid: str) -> FwConfigManagerListController:

    native_and_normalized_config_dict_list = []

    if config_in.native_config is None:
        raise FwoImporterError("Did not get a native config to normalize.")

    if 'domains' not in config_in.native_config:
        getFwoLogger().error("No domains found in native config. Cannot normalize config.")
        raise FwoImporterError("No domains found in native config. Cannot normalize config.")

    # in case of mds, first nativ config domain is global
    is_global_loop_iteration = False
    native_config_global = {}
    normalized_config_global = {}
    if config_in.native_config['domains'][0]['is-super-manager']:
        native_config_global = config_in.native_config['domains'][0]
        is_global_loop_iteration = True
    
    for native_conf in config_in.native_config['domains']:
        normalized_config_dict = deepcopy(fwo_const.emptyNormalizedFwConfigJsonDict)
        normalize_single_manager_config(
            native_conf, native_config_global, normalized_config_dict, normalized_config_global,
            import_state, parsing_config_only, sid, is_global_loop_iteration
        )

        native_and_normalized_config_dict_list.append({'native': native_conf, 'normalized': normalized_config_dict})

        if is_global_loop_iteration:
            normalized_config_global = normalized_config_dict
            is_global_loop_iteration = False

    for native_and_normalized_config_dict in native_and_normalized_config_dict_list:
        normalized_config = FwConfigNormalized(
            action=ConfigAction.INSERT, 
            network_objects=FwConfigNormalizedController.convertListToDict(native_and_normalized_config_dict['normalized']['network_objects'], 'obj_uid'),
            service_objects=FwConfigNormalizedController.convertListToDict(native_and_normalized_config_dict['normalized']['service_objects'], 'svc_uid'),
            zone_objects=FwConfigNormalizedController.convertListToDict(native_and_normalized_config_dict['normalized']['zone_objects'], 'zone_name'),
            rulebases=native_and_normalized_config_dict['normalized']['policies'],
            gateways=native_and_normalized_config_dict['normalized']['gateways']
        )
        manager = FwConfigManager(  ManagerName=native_and_normalized_config_dict['native']['management_name'],
            ManagerUid=native_and_normalized_config_dict['native']['management_uid'],
            IsSuperManager=native_and_normalized_config_dict['native']['is-super-manager'],
            SubManagerIds=[], 
            DomainName=native_and_normalized_config_dict['native']['domain_name'],
            DomainUid=native_and_normalized_config_dict['native']['domain_uid'],
            Configs=[normalized_config]
        )
        config_in.ManagerSet.append(manager)

    return config_in


def normalize_single_manager_config(nativeConfig: dict, native_config_global: dict, normalized_config_dict: dict,
                                    normalized_config_global: dict, importState: ImportStateController,
                                    parsing_config_only: bool, sid: str, is_global_loop_iteration: bool):
    logger = getFwoLogger()
    cp_network.normalize_network_objects(nativeConfig, normalized_config_dict, importState.ImportId, mgm_id=importState.MgmDetails.Id)
    logger.info("completed normalizing network objects")
    cp_service.normalize_service_objects(nativeConfig, normalized_config_dict, importState.ImportId)
    logger.info("completed normalizing service objects")
    cp_gateway.normalize_gateways(nativeConfig, importState, normalized_config_dict)
    cp_rule.normalize_rulebases(nativeConfig, native_config_global, importState, normalized_config_dict, normalized_config_global, is_global_loop_iteration)
    if not parsing_config_only: # get config from cp fw mgr
        cp_getter.logout(importState.MgmDetails.buildFwApiString(), sid)
    logger.info("completed normalizing rulebases")
    

def get_rules(nativeConfig: dict, importState: ImportStateController) -> int:
    """
    Main function to get rules. Divided into smaller sub-tasks for better readability and maintainability.
    """
    show_params_policy_structure = {
        'limit': importState.FwoConfig.ApiFetchSize,
        'details-level': 'full'
    }

    globalAssignments, global_policy_structure, globalDomain, globalSid = None, None, None, None
    manager_details_list = create_ordered_manager_list(importState)
    manager_index = 0
    for managerDetails in manager_details_list:
        cpManagerApiBaseUrl = importState.MgmDetails.buildFwApiString()

        if managerDetails.IsSuperManager:
            globalAssignments, global_policy_structure, globalDomain, globalSid = handle_super_manager(
                managerDetails, cpManagerApiBaseUrl, show_params_policy_structure
            )

        sid: str = cp_getter.login(managerDetails)
        policy_structure = get_policy_structure(cpManagerApiBaseUrl, sid, show_params_policy_structure, managerDetails)

        process_devices(
            managerDetails, policy_structure, globalAssignments, global_policy_structure,
            globalDomain, globalSid, cpManagerApiBaseUrl, sid, nativeConfig['domains'][manager_index],
            nativeConfig['domains'][0], importState
        )
        nativeConfig['domains'][manager_index].update({'policies': policy_structure})
        manager_index += 1

    return 0    


def create_ordered_manager_list(importState):
    """
    creates list of manager details, supermanager is first
    """
    manager_details_list = [deepcopy(importState.MgmDetails)]
    if importState.MgmDetails.IsSuperManager:
        for subManager in importState.MgmDetails.SubManagers:
            manager_details_list.append(deepcopy(subManager))
    return manager_details_list


def handle_super_manager(managerDetails, cpManagerApiBaseUrl, show_params_policy_structure):# -> tuple[list[Any], list[Any] | None, Any | Literal[''] | No...:

    # global assignments are fetched from mds domain
    mdsSid: str = cp_getter.login(managerDetails)
    global_policy_structure = None
    global_domain = None
    global_assignments = cp_getter.get_global_assignments(cpManagerApiBaseUrl, mdsSid, show_params_policy_structure)
    global_sid = ""
    # import global policies if at least one global assignment exists

    
    if len(global_assignments) > 0:
        if 'global-domain' in global_assignments[0] and 'uid' in global_assignments[0]['global-domain']:
            global_domain = global_assignments[0]['global-domain']['uid']

            # policy structure is fetched from global domain
            managerDetails.DomainUid = global_domain
            global_sid: str = cp_getter.login(managerDetails)
            cp_getter.getPolicyStructure(
                cpManagerApiBaseUrl, global_sid, show_params_policy_structure, managerDetails, policy_structure=global_policy_structure
            )
        else:
            raise FwoImporterError(f"Unexpected global assignments: {str(global_assignments)}")

    return global_assignments, global_policy_structure, global_domain, global_sid


def get_policy_structure(cpManagerApiBaseUrl, sid, show_params_policy_structure, managerDetails):
    pol_structure = []
    cp_getter.getPolicyStructure(
        cpManagerApiBaseUrl, sid, show_params_policy_structure, managerDetails, policy_structure=pol_structure
    )
    return pol_structure


def process_devices(
    managerDetails, policy_structure, globalAssignments, global_policy_structure,
    globalDomain, globalSid, cpManagerApiBaseUrl, sid, nativeConfigDomain,
    nativeConfigGlobalDomain, importState
) -> None:
    logger = getFwoLogger()
    for device in managerDetails.Devices:
        deviceConfig: dict[str,Any] = initialize_device_config(device)
        if not deviceConfig:
            continue

        orderedLayerUids: list[str] = get_ordered_layer_uids(policy_structure, deviceConfig, managerDetails.getDomainString())
        if not orderedLayerUids:
            logger.warning(f"No ordered layers found for device: {deviceConfig['name']}")
            continue

        global_ordered_layer_count = 0
        if importState.MgmDetails.IsSuperManager:
            global_ordered_layer_count = handle_global_rulebase_links(
                managerDetails, importState, deviceConfig, globalAssignments, global_policy_structure, globalDomain,
                globalSid, orderedLayerUids, nativeConfigGlobalDomain, cpManagerApiBaseUrl
            )
        else:
            define_initial_rulebase(deviceConfig, orderedLayerUids, False)

        add_ordered_layers_to_native_config(orderedLayerUids,
            get_rules_params(importState), cpManagerApiBaseUrl, sid,
            nativeConfigDomain, deviceConfig, False, global_ordered_layer_count)
        
        handle_nat_rules(device, nativeConfigDomain, sid, importState)

        nativeConfigDomain['gateways'].append(deviceConfig)


def initialize_device_config(device) -> dict[str, Any]:
    if 'name' in device and 'uid' in device:
        return {'name': device['name'], 'uid': device['uid'], 'rulebase_links': []}
    else:
        raise FwoImporterError(f"Device missing name or uid: {device}")


def handle_global_rulebase_links(
    managerDetails, import_state, deviceConfig, globalAssignments, global_policy_structure, globalDomain,
    globalSid, orderedLayerUids, nativeConfigGlobalDomain, cpManagerApiBaseUrl):
    """Searches for global access policy for current device policy,
    adds global ordered layers and defines global rulebase link
    """

    logger = getFwoLogger()
    for globalAssignment in globalAssignments:
        if globalAssignment['dependent-domain']['uid'] == managerDetails.getDomainString():
            for globalPolicy in global_policy_structure:
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
                        'from_rule_uid': '',
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
    
        # link to next ordered layer
        # in case of mds: domain ordered layers are linked once there is no global ordered layer counterpart
        if is_global or orderedLayerIndex >= global_ordered_layer_count - 1:
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


def get_ordered_layer_uids(policy_structure, deviceConfig, domain) -> list[str]:
    """Get UIDs of ordered layers for policy of device
    """

    orderedLayerUids = []
    for policy in policy_structure:
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
    

def get_objects(native_config_dict: dict[str,Any], importState: ImportStateController) -> int:
    show_params_objs = {'limit': importState.FwoConfig.ApiFetchSize}
    manager_details_list = create_ordered_manager_list(importState)
            
    # loop over sub-managers in case of mds
    manager_index = 0
    for manager_details in manager_details_list:
        if manager_details.ImportDisabled and not importState.ForceImport:
            continue

        is_stand_alone_manager = (len(manager_details_list) == 1)
        if manager_details.IsSuperManager or is_stand_alone_manager:
            obj_type_array = cp_const.api_obj_types
        else:
            obj_type_array = cp_const.local_api_obj_types

        if manager_details.IsSuperManager:
            # for super managers we need to get both the global domain data and the Check Point Data (perdefined objects)

            # Check Point Data (perdefined objects)
            manager_details.DomainName = '' 
            manager_details.DomainUid = '' # Check Point Data 
            get_objects_per_domain(manager_details, native_config_dict['domains'][0], obj_type_array, show_params_objs, is_stand_alone_manager=is_stand_alone_manager)
            
            # global domain containing the manually added global objects
            manager_details.DomainName = 'Global' 
            manager_details.DomainUid = 'Global'  
            get_objects_per_domain(manager_details, native_config_dict['domains'][0], obj_type_array, show_params_objs, is_stand_alone_manager=is_stand_alone_manager)
        else:
            get_objects_per_domain(manager_details, native_config_dict['domains'][manager_index], obj_type_array, show_params_objs, is_stand_alone_manager=is_stand_alone_manager)

        manager_index += 1
    return 0


def get_objects_per_domain(manager_details, native_domain, obj_type_array, show_params_objs, is_stand_alone_manager=True):
    sid = cp_getter.login(manager_details)
    cp_url = manager_details.buildFwApiString()
    for obj_type in obj_type_array:
        object_table = get_objects_per_type(obj_type, show_params_objs, sid, cp_url)
        add_special_objects_to_global_domain(object_table, obj_type, sid, cp_api_url=cp_url)
        if not is_stand_alone_manager and not manager_details.IsSuperManager:
            remove_predefined_objects_for_domains(object_table)
        native_domain['objects'].append(object_table)


def remove_predefined_objects_for_domains(object_table):
    if 'chunks' in object_table and 'type' in object_table and \
        object_table['type'] in cp_const.types_to_remove_globals_from:
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


def add_special_objects_to_global_domain(object_table, obj_type, sid, cp_api_url):
    """Appends special objects Original, Any, None and Internet to global domain
    """
    # getting Original (NAT) object (both for networks and services)
    origObj = cp_getter.getObjectDetailsFromApi(cp_const.original_obj_uid, sid=sid, apiurl=cp_api_url)['chunks'][0]
    anyObj = cp_getter.getObjectDetailsFromApi(cp_const.any_obj_uid, sid=sid, apiurl=cp_api_url)['chunks'][0]
    noneObj = cp_getter.getObjectDetailsFromApi(cp_const.none_obj_uid, sid=sid, apiurl=cp_api_url)['chunks'][0]
    internetObj = cp_getter.getObjectDetailsFromApi(cp_const.internet_obj_uid, sid=sid, apiurl=cp_api_url)['chunks'][0]

    if obj_type == 'networks':
        object_table['chunks'].append(origObj)
        object_table['chunks'].append(anyObj)
        object_table['chunks'].append(noneObj)
        object_table['chunks'].append(internetObj)
    if obj_type == 'services-other':
        object_table['chunks'].append(origObj)
        object_table['chunks'].append(anyObj)
        object_table['chunks'].append(noneObj)

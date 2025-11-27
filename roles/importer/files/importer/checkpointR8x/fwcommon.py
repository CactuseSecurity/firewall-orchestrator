from typing import Any
from fwo_log import FWOLogger
import time
from copy import deepcopy

from checkpointR8x import cp_rule, cp_const, cp_network, cp_service, cp_getter, cp_gateway
from fwo_exceptions import FwLoginFailed
from models.fwconfigmanagerlist import FwConfigManagerList, FwConfigManager
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.fwconfig_normalized import FwConfigNormalized
from model_controllers.import_state_controller import ImportStateController
from fwo_base import ConfigAction
import fwo_const
import fwo_globals
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from fwo_exceptions import ImportInterruption, FwoImporterError
from models.import_state import ImportState
from model_controllers.management_controller import ManagementController



def has_config_changed(full_config: dict[str, Any], import_state: ImportState, force: bool = False) -> bool:

    if full_config != {}:   # a config was passed in (read from file), so we assume that an import has to be done (simulating changes here)
        return True

    session_id: str = cp_getter.login(import_state.mgm_details)

    if import_state.last_successful_import is None or import_state.last_successful_import == '' or force:
        # if no last import time found or given or if force flag is set, do full import
        result = True
    else: # otherwise search for any changes since last import
        result = (cp_getter.get_changes(session_id, import_state.mgm_details.hostname, str(import_state.mgm_details.port),import_state.last_successful_import) != 0)

    cp_getter.logout(import_state.mgm_details.buildFwApiString(), session_id)

    return result > 0


def get_config(config_in: FwConfigManagerListController, import_state: ImportStateController) -> tuple[int, FwConfigManagerList]:

    FWOLogger.debug ( "starting checkpointR8x/get_config" )

    if config_in.has_empty_config():   # no native config was passed in, so getting it from FW-Manager
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # get config from cp fw mgr
        starttime = int(time.time())
        initialize_native_config(config_in, import_state)

        start_time_temp = int(time.time())
        FWOLogger.debug ( "checkpointR8x/get_config/getting objects ...")

        if config_in.native_config is None:
            raise FwoImporterError("native_config is None in get_config")

        # IMPORTANT: cp api is expected to preserve order of refs in group objects (unlike refs in rules, which are sorted later)
        result_get_objects = get_objects(config_in.native_config, import_state)
        if result_get_objects>0:
            raise FwLoginFailed( "checkpointR8x/get_config/error while gettings objects")
        FWOLogger.debug ( "checkpointR8x/get_config/fetched objects in " + str(int(time.time()) - start_time_temp) + "s")

        start_time_temp = int(time.time())
        FWOLogger.debug ( "checkpointR8x/get_config/getting rules ...")
        result_get_rules = get_rules (config_in.native_config, import_state)
        if result_get_rules>0:
            raise FwLoginFailed( "checkpointR8x/get_config/error while gettings rules")
        FWOLogger.debug ( "checkpointR8x/get_config/fetched rules in " + str(int(time.time()) - start_time_temp) + "s")

        duration = int(time.time()) - starttime
        FWOLogger.debug ( "checkpointR8x/get_config - fetch duration: " + str(duration) + "s" )

    if config_in.contains_only_native():
        sid: str = cp_getter.login(import_state.mgm_details)
        normalized_config = normalize_config(import_state, config_in, parsing_config_only, sid)
        FWOLogger.info("completed getting config")
        return 0, normalized_config
    else:
        # we already have a native config (from file import) 
        return 0, config_in


def initialize_native_config(config_in: FwConfigManagerListController, import_state: ImportStateController) -> None:
    """
    create domain structure in nativeConfig
    """

    manager_details_list = create_ordered_manager_list(import_state)
    if config_in.native_config is None:
        raise FwoImporterError("native_config is None in initialize_native_config")
    config_in.native_config.update({'domains': []})
    for manager_details in manager_details_list:
        config_in.native_config['domains'].append({
            'domain_name': manager_details.domain_name,
            'domain_uid': manager_details.domain_uid,
            'is-super-manager': manager_details.is_super_manager,
            'management_name': manager_details.name,
            'management_uid': manager_details.uid,
            'objects': [],
            'rulebases': [],
            'nat_rulebases': [],
            'gateways': []})


def normalize_config(import_state: ImportStateController, config_in: FwConfigManagerListController, parsing_config_only: bool, sid: str) -> FwConfigManagerListController:

    native_and_normalized_config_dict_list: list[dict[str, Any]] = []

    if config_in.native_config is None:
        raise FwoImporterError("Did not get a native config to normalize.")

    if 'domains' not in config_in.native_config:
        FWOLogger.error("No domains found in native config. Cannot normalize config.")
        raise FwoImporterError("No domains found in native config. Cannot normalize config.")

    # in case of mds, first nativ config domain is global
    is_global_loop_iteration = False
    native_config_global: dict[str, Any] = {}
    normalized_config_global = {}
    if config_in.native_config['domains'][0]['is-super-manager']:
        native_config_global = config_in.native_config['domains'][0]
        is_global_loop_iteration = True
    
    for native_conf in config_in.native_config['domains']:
        normalized_config_dict = deepcopy(fwo_const.EMPTY_NORMALIZED_FW_CONFIG_JSON_DICT)
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
            network_objects=FwConfigNormalizedController.convert_list_to_dict(native_and_normalized_config_dict['normalized']['network_objects'], 'obj_uid'),
            service_objects=FwConfigNormalizedController.convert_list_to_dict(native_and_normalized_config_dict['normalized']['service_objects'], 'svc_uid'),
            zone_objects=FwConfigNormalizedController.convert_list_to_dict(native_and_normalized_config_dict['normalized']['zone_objects'], 'zone_name'),
            rulebases=native_and_normalized_config_dict['normalized']['policies'],
            gateways=native_and_normalized_config_dict['normalized']['gateways']
        )
        manager = FwConfigManager(
            manager_name=native_and_normalized_config_dict['native']['management_name'],
            manager_uid=native_and_normalized_config_dict['native']['management_uid'],
            is_super_manager=native_and_normalized_config_dict['native']['is-super-manager'],
            sub_manager_ids=[], 
            domain_name=native_and_normalized_config_dict['native']['domain_name'],
            domain_uid=native_and_normalized_config_dict['native']['domain_uid'],
            configs=[normalized_config]
        )
        config_in.ManagerSet.append(manager)

    return config_in


def normalize_single_manager_config(native_config: dict[str, Any], native_config_global: dict[str, Any], normalized_config_dict: dict[str, Any],
                                    normalized_config_global: dict[str, Any], import_state: ImportStateController,
                                    parsing_config_only: bool, sid: str, is_global_loop_iteration: bool):
    cp_network.normalize_network_objects(native_config, normalized_config_dict, import_state.import_id, mgm_id=import_state.mgm_details.id)
    FWOLogger.info("completed normalizing network objects")
    cp_service.normalize_service_objects(native_config, normalized_config_dict, import_state.import_id)
    FWOLogger.info("completed normalizing service objects")
    cp_gateway.normalize_gateways(native_config, import_state, normalized_config_dict)
    cp_rule.normalize_rulebases(native_config, native_config_global, import_state, normalized_config_dict, normalized_config_global, is_global_loop_iteration)
    if not parsing_config_only: # get config from cp fw mgr
        cp_getter.logout(import_state.mgm_details.buildFwApiString(), sid)
    FWOLogger.info("completed normalizing rulebases")
    

def get_rules(native_config: dict[str, Any], import_state: ImportStateController) -> int:
    """
    Main function to get rules. Divided into smaller sub-tasks for better readability and maintainability.
    """
    show_params_policy_structure: dict[str, Any] = {
        'limit': import_state.fwo_config.api_fetch_size,
        'details-level': 'full'
    }

    global_assignments, global_policy_structure, global_domain, global_sid = None, None, None, None
    manager_details_list = create_ordered_manager_list(import_state)
    manager_index = 0
    for manager_details in manager_details_list:
        cp_manager_api_base_url = import_state.mgm_details.buildFwApiString()

        if manager_details.is_super_manager:
            global_assignments, global_policy_structure, global_domain, global_sid = handle_super_manager(
                manager_details, cp_manager_api_base_url, show_params_policy_structure
            )

        sid: str = cp_getter.login(manager_details)
        policy_structure: list[dict[str, Any]] = []
        cp_getter.get_policy_structure(
            cp_manager_api_base_url, sid, show_params_policy_structure, manager_details, policy_structure=policy_structure
        )

        process_devices(
            manager_details, policy_structure, global_assignments, global_policy_structure,
            global_domain, global_sid, cp_manager_api_base_url, sid, native_config['domains'][manager_index], # globalSid should not be None but is when the first manager is not supermanager 
            native_config['domains'][0], import_state
        )
        native_config['domains'][manager_index].update({'policies': policy_structure})
        manager_index += 1

    return 0    


def create_ordered_manager_list(import_state: ImportStateController) -> list[ManagementController]:
    """
    creates list of manager details, supermanager is first
    """
    manager_details_list: list[ManagementController] = [deepcopy(import_state.mgm_details)]
    if import_state.mgm_details.is_super_manager:
        for sub_manager in import_state.mgm_details.sub_managers:
            manager_details_list.append(deepcopy(sub_manager)) # type: ignore TODO: why we are adding submanagers as ManagementController?
    return manager_details_list


def handle_super_manager(manager_details: ManagementController, cp_manager_api_base_url: str, show_params_policy_structure: dict[str, Any]) -> tuple[list[Any], None, Any | None, str]:

    # global assignments are fetched from mds domain
    mds_sid: str = cp_getter.login(manager_details)
    global_policy_structure = None
    global_domain = None
    global_assignments = cp_getter.get_global_assignments(cp_manager_api_base_url, mds_sid, show_params_policy_structure)
    global_sid = ""
    # import global policies if at least one global assignment exists

    
    if len(global_assignments) > 0:
        if 'global-domain' in global_assignments[0] and 'uid' in global_assignments[0]['global-domain']:
            global_domain = global_assignments[0]['global-domain']['uid']

            # policy structure is fetched from global domain
            manager_details.domain_uid = global_domain
            global_sid: str = cp_getter.login(manager_details)
            cp_getter.get_policy_structure(
                cp_manager_api_base_url, global_sid, show_params_policy_structure, manager_details, policy_structure=global_policy_structure
            )
        else:
            raise FwoImporterError(f"Unexpected global assignments: {str(global_assignments)}")

    return global_assignments, global_policy_structure, global_domain, global_sid

def process_devices(
    manager_details: ManagementController, policy_structure: list[dict[str, Any]], global_assignments: list[Any] | None, global_policy_structure: list[dict[str, Any]] | None,
    global_domain: str | None, global_sid: str | None, cp_manager_api_base_url: str, sid: str, native_config_domain: dict[str, Any],
    native_config_global_domain: dict[str, Any], import_state: ImportStateController
) -> None:
    for device in manager_details.devices:
        device_config: dict[str,Any] = initialize_device_config(device)
        if not device_config:
            continue

        ordered_layer_uids: list[str] = get_ordered_layer_uids(policy_structure, device_config, manager_details.getDomainString())
        if not ordered_layer_uids:
            FWOLogger.warning(f"No ordered layers found for device: {device_config['name']}")
            native_config_domain['gateways'].append(device_config)
            continue

        global_ordered_layer_count = 0
        if import_state.mgm_details.is_super_manager:
            global_ordered_layer_count = handle_global_rulebase_links(
                manager_details, import_state, device_config, global_assignments, global_policy_structure, global_domain,
                global_sid, ordered_layer_uids, native_config_global_domain, cp_manager_api_base_url
            )
        else:
            define_initial_rulebase(device_config, ordered_layer_uids, False)

        add_ordered_layers_to_native_config(ordered_layer_uids,
            get_rules_params(import_state), cp_manager_api_base_url, sid,
            native_config_domain, device_config, False, global_ordered_layer_count)
        
        handle_nat_rules(device, native_config_domain, sid, import_state)

        native_config_domain['gateways'].append(device_config)


def initialize_device_config(device: dict[str, Any]) -> dict[str, Any]:
    if 'name' in device and 'uid' in device:
        return {'name': device['name'], 'uid': device['uid'], 'rulebase_links': []}
    else:
        raise FwoImporterError(f"Device missing name or uid: {device}")


def handle_global_rulebase_links(
    manager_details: ManagementController, import_state: ImportStateController, device_config: dict[str, Any], global_assignments: list[Any] | None, global_policy_structure: list[dict[str, Any]] | None, global_domain: str | None,
    global_sid: str | None, ordered_layer_uids: list[str], native_config_global_domain: dict[str, Any], cp_manager_api_base_url: str) -> int:
    """Searches for global access policy for current device policy,
    adds global ordered layers and defines global rulebase link
    """

    if global_assignments is None:
        raise FwoImporterError("Global assignments is None in handle_global_rulebase_links")
    
    if global_policy_structure is None:
        raise FwoImporterError("Global policy structure is None in handle_global_rulebase_links")

    for global_assignment in global_assignments:
        if global_assignment['dependent-domain']['uid'] != manager_details.getDomainString():
            continue
        for global_policy in global_policy_structure:
            if global_policy['name'] == global_assignment['global-access-policy']:
                global_ordered_layer_uids = get_ordered_layer_uids([global_policy], device_config, global_domain)
                if not global_ordered_layer_uids:
                    FWOLogger.warning(f"No access layer for global policy: {global_policy['name']}")
                    break

                global_ordered_layer_count = len(global_ordered_layer_uids)
                global_policy_rulebases_uid_list = add_ordered_layers_to_native_config(global_ordered_layer_uids, get_rules_params(import_state),
                                                                                cp_manager_api_base_url, global_sid, native_config_global_domain, device_config,
                                                                                True, global_ordered_layer_count)
                define_global_rulebase_link(device_config, global_ordered_layer_uids, ordered_layer_uids, native_config_global_domain, global_policy_rulebases_uid_list)
                
                return global_ordered_layer_count
                
    return 0


def define_global_rulebase_link(device_config: dict[str, Any], global_ordered_layer_uids: list[str], ordered_layer_uids: list[str], native_config_global_domain: dict[str, Any], global_policy_rulebases_uid_list: list[str]):
    """Links initial and placeholder rule for global rulebases
    """

    define_initial_rulebase(device_config, global_ordered_layer_uids, True)

    # parse global rulebases, find place-holders and link local rulebases
    placeholder_link_index = 0
    for global_rulebase_uid in global_policy_rulebases_uid_list:
        placeholder_rule_uid = ''
        for rulebase in native_config_global_domain['rulebases']:
            if rulebase['uid'] == global_rulebase_uid:
                placeholder_rule_uid, placeholder_rulebase_uid = cp_getter.get_placeholder_in_rulebase(rulebase)

                if placeholder_rule_uid:
                    ordered_layer_uid =  ''
                    # we might find more than one placeholder, may be unequal to number of domain ordered layers
                    if len(ordered_layer_uids) > placeholder_link_index:
                        ordered_layer_uid = ordered_layer_uids[placeholder_link_index]

                    device_config['rulebase_links'].append({
                        'from_rulebase_uid': placeholder_rulebase_uid,
                        'from_rule_uid': None,
                        'to_rulebase_uid': ordered_layer_uid,
                        'type': 'domain',
                        'is_global': False,
                        'is_initial': False,
                        'is_section': False
                    })

                    placeholder_link_index += 1


def define_initial_rulebase(device_config: dict[str, Any], ordered_layer_uids: list[str], is_global: bool):
    device_config['rulebase_links'].append({
        'from_rulebase_uid': None,
        'from_rule_uid': None,
        'to_rulebase_uid': ordered_layer_uids[0],
        'type': 'ordered',
        'is_global': is_global,
        'is_initial': True,
        'is_section': False
    })


def get_rules_params(import_state: ImportStateController) -> dict[str, Any]:
    return {
        'limit': import_state.fwo_config.api_fetch_size,
        'use-object-dictionary': cp_const.use_object_dictionary,
        'details-level': 'standard',
        'show-hits': cp_const.with_hits
    }


def handle_nat_rules(device: dict[str, Any], native_config_domain: dict[str, Any], sid: str, import_state: ImportStateController):
    if 'package_name' in device and device['package_name']:
        show_params_rules: dict[str, Any] = {
            'limit': import_state.fwo_config.api_fetch_size,
            'use-object-dictionary': cp_const.use_object_dictionary,
            'details-level': 'standard',
            'package': device['package_name']
        }
        FWOLogger.debug(f"Getting NAT rules for package: {device['package_name']}", 4)
        nat_rules = cp_getter.get_nat_rules_from_api_as_dict(
            import_state.mgm_details.buildFwApiString(), sid, show_params_rules,
            native_config_domain=native_config_domain
        )
        if nat_rules:
            native_config_domain['nat_rulebases'].append(nat_rules)
        else:
            native_config_domain['nat_rulebases'].append({"nat_rule_chunks": []})
    else:
        native_config_domain['nat_rulebases'].append({"nat_rule_chunks": []})


def add_ordered_layers_to_native_config(ordered_layer_uids: list[str], show_params_rules: dict[str, Any],
                                        cp_manager_api_base_url: str, sid: str | None, native_config_domain: dict[str, Any],
                                        device_config: dict[str, Any], is_global: bool, global_ordered_layer_count: int) -> list[str]:
    """Fetches ordered layers and links them
    """
    ordered_layer_index = 0
    policy_rulebases_uid_list = []
    for ordered_layer_uid in ordered_layer_uids:

        show_params_rules.update({'uid': ordered_layer_uid})

        policy_rulebases_uid_list = cp_getter.get_rulebases(
            cp_manager_api_base_url, sid, show_params_rules, native_config_domain,
            device_config, policy_rulebases_uid_list,
            is_global=is_global, access_type='access',
            rulebase_uid=ordered_layer_uid)
    
        # link to next ordered layer
        # in case of mds: domain ordered layers are linked once there is no global ordered layer counterpart
        if is_global or ordered_layer_index >= global_ordered_layer_count - 1:
            if ordered_layer_index < len(ordered_layer_uids) - 1:
                device_config['rulebase_links'].append({
                    'from_rulebase_uid': ordered_layer_uid,
                    'from_rule_uid': None,
                    'to_rulebase_uid': ordered_layer_uids[ordered_layer_index + 1],
                    'type': 'ordered',
                    'is_global': is_global,
                    'is_initial': False,
                    'is_section': False
                })
        
        ordered_layer_index += 1

    return policy_rulebases_uid_list


def get_ordered_layer_uids(policy_structure: list[dict[str, Any]], device_config: dict[str, Any], domain: str | None) -> list[str]:
    """Get UIDs of ordered layers for policy of device
    """

    ordered_layer_uids: list[str] = []
    for policy in policy_structure:
        found_target_in_policy = False
        for target in policy['targets']:
            if target['uid'] == device_config['uid'] or target['uid'] == 'all':
                found_target_in_policy = True
        if found_target_in_policy:
            append_access_layer_uid(policy, domain, ordered_layer_uids)

    return ordered_layer_uids


def append_access_layer_uid(policy: dict[str, Any], domain: str | None, ordered_layer_uids: list[str]) -> None:
    for access_layer in policy['access-layers']:
        if access_layer['domain'] == domain or domain == '':
            ordered_layer_uids.append(access_layer['uid'])
    

def get_objects(native_config_dict: dict[str,Any], import_state: ImportStateController) -> int:
    show_params_objs = {'limit': import_state.fwo_config.api_fetch_size}
    manager_details_list = create_ordered_manager_list(import_state)
            
    # loop over sub-managers in case of mds
    manager_index = 0
    for manager_details in manager_details_list:
        if manager_details.import_disabled and not import_state.force_import:
            continue

        is_stand_alone_manager = (len(manager_details_list) == 1)
        if manager_details.is_super_manager or is_stand_alone_manager:
            obj_type_array = cp_const.api_obj_types
        else:
            obj_type_array = cp_const.local_api_obj_types

        if manager_details.is_super_manager:
            # for super managers we need to get both the global domain data and the Check Point Data (perdefined objects)

            # Check Point Data (perdefined objects)
            manager_details.domain_name = '' 
            manager_details.domain_uid = '' # Check Point Data 
            get_objects_per_domain(manager_details, native_config_dict['domains'][0], obj_type_array, show_params_objs, is_stand_alone_manager=is_stand_alone_manager)
            
            # global domain containing the manually added global objects
            manager_details.domain_name = 'Global' 
            manager_details.domain_uid = 'Global'  
            get_objects_per_domain(manager_details, native_config_dict['domains'][0], obj_type_array, show_params_objs, is_stand_alone_manager=is_stand_alone_manager)
        else:
            get_objects_per_domain(manager_details, native_config_dict['domains'][manager_index], obj_type_array, show_params_objs, is_stand_alone_manager=is_stand_alone_manager)

        manager_index += 1
    return 0


def get_objects_per_domain(manager_details: ManagementController, native_domain: dict[str, Any], obj_type_array: list[str], show_params_objs: dict[str, Any], is_stand_alone_manager: bool=True) -> None:
    sid = cp_getter.login(manager_details)
    cp_url = manager_details.buildFwApiString()
    for obj_type in obj_type_array:
        object_table = get_objects_per_type(obj_type, show_params_objs, sid, cp_url)
        add_special_objects_to_global_domain(object_table, obj_type, sid, cp_api_url=cp_url)
        if not is_stand_alone_manager and not manager_details.is_super_manager:
            remove_predefined_objects_for_domains(object_table)
        native_domain['objects'].append(object_table)


def remove_predefined_objects_for_domains(object_table: dict[str, Any]) -> None:
    if 'chunks' in object_table and 'type' in object_table and \
        object_table['type'] in cp_const.types_to_remove_globals_from:
        return
    
    for chunk in object_table['chunks']:
        if 'objects' in chunk:
            for obj in chunk['objects']:
                domain_type = obj.get("domain", {}).get("domain-type", "")
                if domain_type != "domain":
                    chunk['objects'].remove(obj)


def get_objects_per_type(obj_type: str, show_params_objs: dict[str, Any], sid: str, cp_manager_api_base_url: str) -> dict[str, Any]:
    if fwo_globals.shutdown_requested:
        raise ImportInterruption("Shutdown requested during object retrieval.")
    if obj_type in cp_const.obj_types_full_fetch_needed:
        show_params_objs.update({'details-level': cp_const.details_level_group_objects})
    else:
        show_params_objs.update({'details-level': cp_const.details_level_objects})
    object_table: dict[str, Any] = { "type": obj_type, "chunks": [] }
    current=0
    total=current+1
    show_cmd = 'show-' + obj_type    
    FWOLogger.debug ( "obj_type: "+ obj_type, 6)

    while (current<total) :
        show_params_objs['offset']=current
        objects = cp_getter.cp_api_call(cp_manager_api_base_url, show_cmd, show_params_objs, sid)
        if fwo_globals.shutdown_requested:
            raise ImportInterruption("Shutdown requested during object retrieval.")

        object_table["chunks"].append(objects)
        if 'total' in objects  and 'to' in objects:
            total=objects['total']
            current=objects['to']
            FWOLogger.debug ( obj_type +" current:"+ str(current) + " of a total " + str(total), 6)
        else :
            current = total

    return object_table


def add_special_objects_to_global_domain(object_table: dict[str, Any], obj_type: str, sid: str, cp_api_url: str) -> None:
    """Appends special objects Original, Any, None and Internet to global domain
    """
    # getting Original (NAT) object (both for networks and services)
    orig_obj = cp_getter.get_object_details_from_api(cp_const.original_obj_uid, sid=sid, apiurl=cp_api_url)['chunks'][0]
    any_obj = cp_getter.get_object_details_from_api(cp_const.any_obj_uid, sid=sid, apiurl=cp_api_url)['chunks'][0]
    none_obj = cp_getter.get_object_details_from_api(cp_const.none_obj_uid, sid=sid, apiurl=cp_api_url)['chunks'][0]
    internet_obj = cp_getter.get_object_details_from_api(cp_const.internet_obj_uid, sid=sid, apiurl=cp_api_url)['chunks'][0]

    if obj_type == 'networks':
        object_table['chunks'].append(orig_obj)
        object_table['chunks'].append(any_obj)
        object_table['chunks'].append(none_obj)
        object_table['chunks'].append(internet_obj)
    if obj_type == 'services-other':
        object_table['chunks'].append(orig_obj)
        object_table['chunks'].append(any_obj)
        object_table['chunks'].append(none_obj)

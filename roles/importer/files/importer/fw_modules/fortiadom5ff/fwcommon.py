from typing import Any
import fwo_const
from copy import deepcopy
from model_controllers.import_state_controller import ImportStateController
from fwo_exceptions import ImportInterruption, FwLoginFailed, FwLogoutFailed
from fwo_base import write_native_config_to_file
from fw_modules.fortiadom5ff import fmgr_getter
from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from models.fwconfigmanager import FwConfigManager
from fw_modules.fortiadom5ff.fmgr_network import normalize_network_objects
from fw_modules.fortiadom5ff.fmgr_service import normalize_service_objects
from fw_modules.fortiadom5ff.fmgr_rule import normalize_rulebases, get_access_policy, get_nat_policy
from fw_modules.fortiadom5ff.fmgr_consts import nw_obj_types, svc_obj_types, user_obj_types
from fwo_base import ConfigAction
from fw_modules.fortiadom5ff.fmgr_zone import get_zones, normalize_zones
from models.fwconfig_normalized import FwConfigNormalized
from models.management import Management
from models.fw_common import FwCommon


class FortiAdom5ffCommon(FwCommon):
    def get_config(self, config_in: FwConfigManagerListController, import_state: ImportStateController) -> tuple[int, FwConfigManagerListController]:
        return get_config(config_in, import_state)

def get_config(config_in: FwConfigManagerListController, importState: ImportStateController) -> tuple[int, FwConfigManagerListController]:
    if config_in.has_empty_config():   # no native config was passed in, so getting it from FW-Manager 
        config_in.native_config.update({'domains': []}) # type: ignore #TYPING: What is this? None or not None this is the question
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # no native config was passed in, so getting it from FortiManager
        sid = get_sid(importState)
        limit = importState.fwo_config.api_fetch_size
        fm_api_url = importState.mgm_details.buildFwApiString()
        native_config_global = initialize_native_config_domain(importState.mgm_details)
        config_in.native_config['domains'].append(native_config_global) # type: ignore #TYPING: None or not None this is the question
        adom_list = build_adom_list(importState)
        adom_device_vdom_structure = build_adom_device_vdom_structure(adom_list, sid, fm_api_url)
        # delete_v: das geht schief für unschöne adoms
        arbitrary_vdom_for_updateable_objects = get_arbitrary_vdom(adom_device_vdom_structure)
        adom_device_vdom_policy_package_structure = add_policy_package_to_vdoms(adom_device_vdom_structure, sid, fm_api_url)

        # get global
        get_objects(sid, fm_api_url, native_config_global, native_config_global, '', limit, nw_obj_types, svc_obj_types, 'global', arbitrary_vdom_for_updateable_objects)
        get_zones(sid, fm_api_url, native_config_global, '', limit)

        for adom in adom_list:
            adom_name = adom.domain_name
            native_config_adom = initialize_native_config_domain(adom)
            config_in.native_config['domains'].append(native_config_adom) # type: ignore #TYPING: None or not None this is the question

            adom_scope = 'adom/'+adom_name
            get_objects(sid, fm_api_url, native_config_adom, native_config_global, adom_name, limit, nw_obj_types, svc_obj_types, adom_scope, arbitrary_vdom_for_updateable_objects)
            # currently reading zone from objects/rules for backward compat with FortiManager 6.x
            get_zones(sid, fm_api_url, native_config_adom, adom_name, limit)
            
            # todo: bring interfaces and routing in new domain native config format
            #getInterfacesAndRouting(
            #    sid, fm_api_url, nativeConfig, adom_name, adom.Devices, limit)

            for mgm_details_device in adom.devices:
                device_config = initialize_device_config(mgm_details_device)
                native_config_adom['gateways'].append(device_config)
                get_access_policy(
                    sid, fm_api_url, native_config_adom, native_config_global, adom_device_vdom_policy_package_structure, adom_name, mgm_details_device, device_config, limit)
                get_nat_policy(
                    sid, fm_api_url, native_config_adom, adom_device_vdom_policy_package_structure, adom_name, mgm_details_device, limit)
                                
        try:  # logout of fortimanager API
            fmgr_getter.logout(
                fm_api_url, sid)
        except Exception:
            raise FwLogoutFailed("logout exception probably due to timeout - irrelevant, so ignoring it")

        write_native_config_to_file(importState, config_in.native_config)

    if not config_in.native_config:
        raise ImportError("native config missing")

    normalized_managers = normalize_config(config_in.native_config)
    FWOLogger.info("completed getting config")
    return 0, normalized_managers


def initialize_native_config_domain(mgm_details: Management) -> dict[str, Any]:
    return {
        'domain_name': mgm_details.domain_name,
        'domain_uid': mgm_details.domain_uid,
        'is-super-manager': mgm_details.is_super_manager,
        'management_name': mgm_details.name,
        'management_uid': mgm_details.uid,
        'objects': [],
        'rulebases': [],
        'nat_rulebases': [],
        'zones': [],
        'gateways': []}

def get_arbitrary_vdom(adom_device_vdom_structure: dict[str, dict[str, dict[str, Any]]]) -> dict[str, str] | None:
    for adom in adom_device_vdom_structure:
        for device in adom_device_vdom_structure[adom]:
            for vdom in adom_device_vdom_structure[adom][device]:
                return {'adom': adom, 'device': device, 'vdom': vdom}


def normalize_config(native_config: dict[str,Any]) -> FwConfigManagerListController:

    manager_list = FwConfigManagerListController()

    if 'domains' not in native_config:
        raise ImportInterruption("No domains found in native config. Cannot normalize config.")

    rewrite_native_config_obj_type_as_key(native_config) # for easier accessability of objects in normalization process

    native_config_global: dict[str, Any] = {}
    normalized_config_global = {}

    for native_conf in native_config['domains']:
        normalized_config_adom = deepcopy(fwo_const.EMPTY_NORMALIZED_FW_CONFIG_JSON_DICT)
        is_global_loop_iteration = False

        if native_conf['is-super-manager']:
            native_config_global = native_conf
            normalized_config_global = normalized_config_adom
            is_global_loop_iteration = True

        normalize_single_manager_config(native_conf, native_config_global, normalized_config_adom, normalized_config_global, 
                                                            is_global_loop_iteration)

        normalized_config = FwConfigNormalized(
            action=ConfigAction.INSERT, 
            network_objects=FwConfigNormalizedController.convert_list_to_dict(normalized_config_adom.get('network_objects', []), 'obj_uid'),
            service_objects=FwConfigNormalizedController.convert_list_to_dict(normalized_config_adom.get('service_objects', []), 'svc_uid'),
            zone_objects=FwConfigNormalizedController.convert_list_to_dict(normalized_config_adom.get('zone_objects', []), 'zone_name'),
            rulebases=normalized_config_adom.get('policies', []),
            gateways=normalized_config_adom.get('gateways', [])
        )

        # TODO: identify the correct manager

        manager = FwConfigManager(manager_uid=native_conf.get('management_uid',''),
                                    manager_name=native_conf.get('management_name', ''),
                                    is_super_manager=native_conf.get('is-super-manager', False),
                                    domain_name=native_conf.get('domain_name', ''),
                                    domain_uid=native_conf.get('domain_uid', ''),
                                    sub_manager_ids=[], 
                                    configs=[normalized_config])

        manager_list.add_manager(manager)

    return manager_list


def rewrite_native_config_obj_type_as_key(native_config: dict[str, Any]):
    # rewrite native config objects to have the object type as key
    # this is needed for the normalization process

    for domain in native_config['domains']:
        if 'objects' not in domain:
            continue
        obj_dict: dict[str, Any] = {}
        for obj_chunk in domain['objects']:
            if 'type' not in obj_chunk:
                continue
            obj_type = obj_chunk['type']
            obj_dict.update({obj_type: obj_chunk})
        domain['objects'] = obj_dict


def normalize_single_manager_config(native_config: 'dict[str, Any]', native_config_global: 'dict[str, Any]', normalized_config_adom: dict[str, Any],
                                    normalized_config_global: dict[str, Any], is_global_loop_iteration: bool):

    current_nw_obj_types = deepcopy(nw_obj_types)
    current_svc_obj_types = deepcopy(svc_obj_types)
    if native_config['is-super-manager']:
        current_nw_obj_types = ["nw_obj_global_" + t for t in current_nw_obj_types]
        current_nw_obj_types.append('nw_obj_global_firewall/internet-service-basic')
        current_svc_obj_types = ["svc_obj_global_" + t for t in current_svc_obj_types]
    else:
        current_nw_obj_types = [f"nw_obj_adom/{native_config.get('domain_name','')}_{t}" for t in current_nw_obj_types]
        current_svc_obj_types = [f"svc_obj_adom/{native_config.get('domain_name','')}_{t}" for t in current_svc_obj_types]

    normalize_zones(native_config, normalized_config_adom, is_global_loop_iteration)
    FWOLogger.info("completed normalizing zones for manager: " + native_config.get('domain_name',''))
    normalize_network_objects(native_config, normalized_config_adom, normalized_config_global, 
                                           current_nw_obj_types)
    FWOLogger.info("completed normalizing network objects for manager: " + native_config.get('domain_name',''))
    normalize_service_objects(native_config, normalized_config_adom, current_svc_obj_types)
    FWOLogger.info("completed normalizing service objects for manager: " + native_config.get('domain_name',''))
    mgm_uid = native_config["management_uid"]
    normalize_rulebases(mgm_uid, native_config, native_config_global, normalized_config_adom, normalized_config_global, 
                        is_global_loop_iteration)
    FWOLogger.info("completed normalizing rulebases for manager: " + native_config.get('domain_name',''))

    normalize_gateways(native_config, normalized_config_adom)
    

def build_adom_list(importState : ImportStateController) -> list[Management]:
    adom_list: list[Management] = []
    if importState.mgm_details.is_super_manager:
        for subManager in importState.mgm_details.sub_managers:
            adom_list.append(deepcopy(subManager))
    return adom_list

def build_adom_device_vdom_structure(adom_list: list[Management], sid: str, fm_api_url: str) -> dict[str, dict[str, dict[str, Any]]]:
    adom_device_vdom_structure: dict[str, dict[str, dict[str, Any]]] = {}
    for adom in adom_list:
        adom_device_vdom_structure.update({adom.domain_name: {}})
        if len(adom.devices) > 0:
            device_vdom_dict = fmgr_getter.get_devices_from_manager(adom, sid, fm_api_url)
            adom_device_vdom_structure[adom.domain_name].update(device_vdom_dict)
    return adom_device_vdom_structure

def add_policy_package_to_vdoms(adom_device_vdom_structure: dict[str, dict[str, dict[str, str]]], sid: str, fm_api_url: str) -> dict[str, dict[str, dict[str, Any]]]:
    adom_device_vdom_policy_package_structure = deepcopy(adom_device_vdom_structure)
    for adom in adom_device_vdom_policy_package_structure:
        policy_packages_result = fmgr_getter.fortinet_api_call(sid, fm_api_url, '/pm/pkg/adom/' + adom)
        for policy_package in policy_packages_result:
            if 'scope member' in policy_package:
                parse_policy_package(policy_package, adom_device_vdom_policy_package_structure, adom)
        add_global_policy_package_to_vdom(adom_device_vdom_policy_package_structure, sid, fm_api_url, adom)
    return adom_device_vdom_policy_package_structure

def parse_policy_package(policy_package: dict[str, Any], adom_device_vdom_policy_package_structure: dict[str, dict[str, dict[str, Any]]], adom: str):
    for scope_member in policy_package['scope member']:
        for device in adom_device_vdom_policy_package_structure[adom]:
            if device == scope_member['name']:
                for vdom in adom_device_vdom_policy_package_structure[adom][device]:
                    if vdom == scope_member['vdom']:
                        adom_device_vdom_policy_package_structure[adom][device].update({vdom: {'local': policy_package['name'], 'global': ''}})

def add_global_policy_package_to_vdom(adom_device_vdom_policy_package_structure: dict[str, dict[str, dict[str, Any]]], sid: str, fm_api_url: str, adom: str):
    global_assignment_result = fmgr_getter.fortinet_api_call(sid, fm_api_url, '/pm/config/adom/' + adom + '/_adom/options')
    for global_assignment in global_assignment_result:
        if global_assignment['assign_excluded'] == 0 and global_assignment['specify_assign_pkg_list'] == 0:
            assign_case_all(adom_device_vdom_policy_package_structure, adom, global_assignment)
        elif global_assignment['assign_excluded'] == 0 and global_assignment['specify_assign_pkg_list'] == 1:
            assign_case_include(adom_device_vdom_policy_package_structure, adom, global_assignment)
        elif global_assignment['assign_excluded'] == 1 and global_assignment['specify_assign_pkg_list'] == 1:
            assign_case_exclude(adom_device_vdom_policy_package_structure, adom, global_assignment)
        else:
            raise ImportInterruption('Broken global assign format.')
        
def assign_case_all(adom_device_vdom_policy_package_structure: dict[str, dict[str, dict[str, Any]]], adom: str, global_assignment: dict[str, Any]):
    for device in adom_device_vdom_policy_package_structure[adom]:
        for vdom in adom_device_vdom_policy_package_structure[adom][device]:
            adom_device_vdom_policy_package_structure[adom][device][vdom]['global'] = global_assignment['assign_name']

def assign_case_include(adom_device_vdom_policy_package_structure: dict[str, dict[str, dict[str, Any]]], adom: str, global_assignment: dict[str, Any]):
    for device in adom_device_vdom_policy_package_structure[adom]:
        for vdom in adom_device_vdom_policy_package_structure[adom][device]:
            match_assign_and_vdom_policy_package(global_assignment, adom_device_vdom_policy_package_structure[adom][device][vdom], True)

def assign_case_exclude(adom_device_vdom_policy_package_structure: dict[str, dict[str, dict[str, Any]]], adom: str, global_assignment: dict[str, Any]):
    for device in adom_device_vdom_policy_package_structure[adom]:
        for vdom in adom_device_vdom_policy_package_structure[adom][device]:
            match_assign_and_vdom_policy_package(global_assignment, adom_device_vdom_policy_package_structure[adom][device][vdom], False)

def match_assign_and_vdom_policy_package(global_assignment: dict[str, Any], vdom_structure: dict[str, Any], is_include: bool):
    for package in global_assignment['pkg list']:
        if is_include:
            if package['name'] == vdom_structure['local']:
                vdom_structure['global'] = global_assignment['assign_name']
        else:
            if package['name'] != vdom_structure['local']:
                vdom_structure['global'] = global_assignment['assign_name']

def initialize_device_config(mgm_details_device: dict[str, Any]) -> dict[str, Any]:
    device_config: dict[str, Any] = {'name': mgm_details_device['name'],
                     'uid': mgm_details_device['uid'],
                     'rulebase_links': []}
    return device_config

def get_sid(importState: ImportStateController):
    fm_api_url = 'https://' + \
        importState.mgm_details.hostname + ':' + \
        str(importState.mgm_details.port) + '/jsonrpc'
    sid = fmgr_getter.login(importState.mgm_details.import_user, importState.mgm_details.secret, fm_api_url)
    if sid is None:
        raise FwLoginFailed('did not succeed in logging in to FortiManager API, no sid returned')
    return sid


def get_objects(sid: str, fm_api_url: str, native_config_domain: dict[str, Any], native_config_global: dict[str, Any], adom_name: str, limit: int, nw_obj_types: list[str], svc_obj_types: list[str], adom_scope: str, arbitrary_vdom_for_updateable_objects: dict[str, Any] | None):
    # get those objects that exist globally and on adom level
    api_base_path = f"/pm/config/{adom_scope}/obj/"

    # get network objects:
    for object_type in nw_obj_types:
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_domain['objects'], sid, fm_api_url, api_base_path + object_type, "nw_obj_" + adom_scope + "_" + object_type, limit=limit)

    # get service objects:
    # service/custom is an undocumented API call!
    for object_type in svc_obj_types:
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_domain['objects'], sid, fm_api_url, api_base_path + object_type, "svc_obj_" + adom_scope + "_" + object_type, limit=limit)

    # user: /pm/config/global/obj/user/local, /pm/config/global/obj/user/group
    # get user objects:
    for object_type in user_obj_types:
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_domain['objects'], sid, fm_api_url, api_base_path + object_type, "user_obj_" + adom_scope + "_" + object_type, limit=limit)
            
    # get one arbitrary device and vdom to get dynamic objects
    # they are equal across all adoms, vdoms, devices
    if arbitrary_vdom_for_updateable_objects is None:
        FWOLogger.error("arbitrary_vdom_for_updateable_objects is None, cannot get dynamic objects")
        return
    if arbitrary_vdom_for_updateable_objects['adom'] == adom_name:
        # get dynamic objects
        payload: dict[str, Any] = {
            'params': [
                {
                    'data': {
                        'action': 'get',
                        'resource': '/api/v2/monitor/firewall/internet-service-basic?vdom=' + arbitrary_vdom_for_updateable_objects['vdom'],
                        'target': [
                            'adom/' + adom_name + '/device/' + arbitrary_vdom_for_updateable_objects['device']
                        ]
                    }
                }
            ]
        }
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_global['objects'], sid, fm_api_url, "sys/proxy/json", "nw_obj_global_firewall/internet-service-basic", limit=limit, payload=payload, method='exec')


def normalize_gateways(native_config: dict[str, Any], normalized_config_adom: dict[str, Any]):
    for gateway in native_config['gateways']:
        normalized_gateway = {}
        normalized_gateway['Uid'] = gateway['uid']
        normalized_gateway['Name'] = gateway['name']
        normalized_gateway['Interfaces'] = normalize_interfaces()
        normalized_gateway['Routing'] = normalize_routing()
        normalized_gateway['RulebaseLinks'] = normalize_links(gateway['rulebase_links'])
        normalized_config_adom['gateways'].append(normalized_gateway)

def normalize_interfaces() -> list[Any]:
    # TODO
    return []

def normalize_routing() -> list[Any]:
    # TODO
    return []

def normalize_links(rulebase_links : list[dict[str, Any]]) -> list[dict[str, Any]]:
    for link in rulebase_links:
        link['link_type'] = link.pop('type')

        # Remove from_rulebase_uid and from_rule_uid if link_type is initial
        if link['link_type'] == 'initial':
            if link['from_rulebase_uid'] != None:
                link['from_rulebase_uid'] = None
            if link['from_rule_uid'] != None:
                link['from_rule_uid'] = None
    return rulebase_links

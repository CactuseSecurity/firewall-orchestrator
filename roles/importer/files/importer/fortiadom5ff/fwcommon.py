
from curses import raw
from typing import Any

import json
import fwo_const
from copy import deepcopy
from model_controllers.import_state_controller import ImportStateController
from fwo_exceptions import ImportInterruption, FwLoginFailed, FwLogoutFailed
from fwo_base import write_native_config_to_file
import fmgr_getter
from fwo_log import getFwoLogger
from fmgr_gw_networking import getInterfacesAndRouting, normalize_network_data
from model_controllers.route_controller import get_ip_of_interface_obj
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from models.fwconfigmanager import FwConfigManager
from model_controllers.management_controller import ManagementController
from fmgr_network import normalize_network_objects
from fmgr_service import normalize_service_objects
from fmgr_rule import normalize_rulebases
from fmgr_consts import nw_obj_types, svc_obj_types, user_obj_types
from fwo_base import ConfigAction
from models.fwconfig_normalized import FwConfigNormalized


def has_config_changed(full_config, mgm_details, force=False):
    # dummy - may be filled with real check later on
    return True

def get_config(config_in: FwConfigManagerListController, importState: ImportStateController):
    logger = getFwoLogger()
    
    if config_in.has_empty_config():   # no native config was passed in, so getting it from FW-Manager
        config_in.native_config.update({'domains': []})
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only: # no native config was passed in, so getting it from FortiManager
        sid = get_sid(importState)
        limit = importState.FwoConfig.ApiFetchSize
        fm_api_url = importState.MgmDetails.buildFwApiString()
        native_config_global = initialize_native_config_domain(importState.MgmDetails)
        config_in.native_config['domains'].append(native_config_global)
        adom_list = build_adom_list(importState)
        adom_device_vdom_structure = build_adom_device_vdom_structure(adom_list, sid, fm_api_url)
        # delete_v: das geht schief für unschöne adoms
        arbitrary_vdom_for_updateable_objects = get_arbitrary_vdom(adom_device_vdom_structure)
        adom_device_vdom_policy_package_structure = add_policy_package_to_vdoms(adom_device_vdom_structure, sid, fm_api_url)
        # adom_device_vdom_policy_package_structure = {adom: {device: {vdom1: pol_pkg1}, {vdom2: pol_pkg2}}}
        global_packages = get_global_packages(sid, fm_api_url, adom_device_vdom_policy_package_structure)

        # get global
        get_objects(sid, fm_api_url, native_config_global, native_config_global, '', limit, nw_obj_types, svc_obj_types, 'global', arbitrary_vdom_for_updateable_objects)

        for adom in adom_list:
            adom_name = adom.DomainName
            native_config_adom = initialize_native_config_domain(adom)
            config_in.native_config['domains'].append(native_config_adom)

            adom_scope = 'adom/'+adom_name
            get_objects(sid, fm_api_url, native_config_adom, native_config_global, adom_name, limit, nw_obj_types, svc_obj_types, adom_scope, arbitrary_vdom_for_updateable_objects)
            # currently reading zone from objects/rules for backward compat with FortiManager 6.x
            # getZones(sid, fm_api_url, full_config, adom_name, limit, debug_level)
            
            # todo: bring interfaces and routing in new domain native config format
            #getInterfacesAndRouting(
            #    sid, fm_api_url, nativeConfig, adom_name, adom.Devices, limit)

            for mgm_details_device in adom.Devices:
                device_config = initialize_device_config(mgm_details_device)
                native_config_adom['gateways'].append(device_config)
                getAccessPolicy(
                    sid, fm_api_url, native_config_adom, adom_device_vdom_policy_package_structure, adom_name, mgm_details_device, device_config, limit)
                # delete_v: nat später
                #fmgr_rule.getNatPolicy(
                #    sid, fm_api_url, nativeConfig, adom_name, mgm_details_device, limit)
                                
        try:  # logout of fortimanager API
            fmgr_getter.logout(
                fm_api_url, sid)
        except Exception:
            raise FwLogoutFailed("logout exception probably due to timeout - irrelevant, so ignoring it")

        write_native_config_to_file(importState, config_in.native_config)

    normalized_managers = normalize_config(importState, config_in.native_config)
    logger.info("completed getting config")
    return 0, normalized_managers


def initialize_native_config_domain(mgm_details : ManagementController):
    return {
        'domain_name': mgm_details.DomainName,
        'domain_uid': mgm_details.DomainUid,
        'is-super-manager': mgm_details.IsSuperManager,
        'management_name': mgm_details.Name,
        'management_uid': mgm_details.Uid,
        'objects': [],
        'rulebases': [],
        'nat_rulebases': [],
        'rules_hitcount': [],
        'gateways': []}

def get_arbitrary_vdom(adom_device_vdom_structure):
    for adom in adom_device_vdom_structure:
        for device in adom_device_vdom_structure[adom]:
            for vdom in adom_device_vdom_structure[adom][device]:
                return {'adom': adom, 'device': device, 'vdom': vdom}


def normalize_config(import_state, native_config: dict[str,Any]) -> FwConfigManagerListController:

    manager_list = FwConfigManagerListController()

    native_config_global = {} # TODO: implement reading for FortiManager 5ff
    normalized_config_global = {} # TODO: implement reading for FortiManager 5ff

    if 'domains' not in native_config:
        logger = getFwoLogger()
        logger.error("No domains found in native config. Cannot normalize config.")
        raise ImportInterruption("No domains found in native config. Cannot normalize config.")

    rewrite_native_config_obj_type_as_key(native_config) # for easier accessability of objects in normalization process

    for native_conf in native_config['domains']:
        normalized_config_dict = deepcopy(fwo_const.emptyNormalizedFwConfigJsonDict)

        # delete_v: is_global_loop_iteration scheint immer False zu sein, kann dann weg
        normalize_single_manager_config(native_conf, native_config_global, normalized_config_dict, normalized_config_global, 
                                                            import_state, is_global_loop_iteration=False)
        
        if native_conf['is-super-manager']:
            normalized_config_global = deepcopy(normalized_config_dict)

        normalized_config = FwConfigNormalized(
            action=ConfigAction.INSERT, 
            network_objects=FwConfigNormalizedController.convertListToDict(normalized_config_dict.get('network_objects', []), 'obj_uid'),
            service_objects=FwConfigNormalizedController.convertListToDict(normalized_config_dict.get('service_objects', []), 'svc_uid'),
            zone_objects=FwConfigNormalizedController.convertListToDict(normalized_config_dict.get('zone_objects', []), 'zone_name'),
            rulebases=normalized_config_dict.get('policies', []),
            gateways=normalized_config_dict.get('gateways', [])
        )

        # TODO: identify the correct manager

        manager = FwConfigManager(ManagerUid=native_conf.get('management_uid',''),
                                    ManagerName=native_conf.get('management_name', ''),
                                    IsSuperManager=native_conf.get('is-super-manager', False),
                                    DomainName=native_conf.get('domain_name', ''),
                                    DomainUid=native_conf.get('domain_uid', ''),
                                    SubManagerIds=[], 
                                    Configs=[normalized_config])

        manager_list.addManager(manager)

    return manager_list


def rewrite_native_config_obj_type_as_key(native_config):
    # rewrite native config objects to have the object type as key
    # this is needed for the normalization process

    for domain in native_config['domains']:
        if 'objects' not in domain:
            continue
        obj_dict = {}
        for obj_chunk in domain['objects']:
            if 'type' not in obj_chunk:
                continue
            obj_type = obj_chunk['type']
            obj_dict.update({obj_type: obj_chunk})
        domain['objects'] = obj_dict


def normalize_single_manager_config(native_config: dict[str, Any], native_config_global: dict[str, Any], normalized_config_dict: dict,
                                    normalized_config_global: dict, import_state: ImportStateController,
                                    is_global_loop_iteration: bool):

    current_nw_obj_types = deepcopy(nw_obj_types)
    current_svc_obj_types = deepcopy(svc_obj_types)
    if native_config['is-super-manager']:
        current_nw_obj_types = ["nw_obj_global_" + t for t in current_nw_obj_types]
        current_nw_obj_types.append('nw_obj_global_firewall/internet-service-basic')
        current_svc_obj_types = ["svc_obj_global_" + t for t in current_svc_obj_types]
    else:
        current_nw_obj_types = [f"nw_obj_adom/{native_config.get('domain_name','')}_{t}" for t in current_nw_obj_types]
        current_svc_obj_types = [f"svc_obj_adom/{native_config.get('domain_name','')}_{t}" for t in current_svc_obj_types]

    logger = getFwoLogger()
    normalize_network_objects(import_state, native_config, native_config_global, normalized_config_dict, normalized_config_global, 
                                           current_nw_obj_types)
    logger.info("completed normalizing network objects for manager: " + native_config.get('domain_name',''))
    normalize_service_objects(import_state, native_config, native_config_global, normalized_config_dict, normalized_config_global, 
                                           current_svc_obj_types)
    logger.info("completed normalizing service objects for manager: " + native_config.get('domain_name',''))
    #fmgr_gateway.normalizeGateways(native_conf, import_state, normalized_config_dict)

    mgm_uid = native_config["management_uid"]
    normalize_rulebases(import_state, mgm_uid, native_config, native_config_global, normalized_config_dict, normalized_config_global, 
                        is_global_loop_iteration)
    logger.info("completed normalizing rulebases for manager: " + native_config.get('domain_name',''))

    normalize_gateways(native_config, normalized_config_dict)
    

def build_adom_list(importState : ImportStateController):
    adom_list = []
    if importState.MgmDetails.IsSuperManager:
        for subManager in importState.MgmDetails.SubManagers:
            adom_list.append(deepcopy(subManager))
    return adom_list

def build_adom_device_vdom_structure(adom_list, sid, fm_api_url) -> dict:
    adom_device_vdom_structure = {}
    for adom in adom_list:
        adom_device_vdom_structure.update({adom.DomainName: {}})
        if len(adom.Devices) > 0:
            device_vdom_dict = fmgr_getter.get_devices_from_manager(adom, sid, fm_api_url)
            adom_device_vdom_structure[adom.DomainName].update(device_vdom_dict)
    return adom_device_vdom_structure

def add_policy_package_to_vdoms(adom_device_vdom_structure, sid, fm_api_url):
    adom_device_vdom_policy_package_structure = deepcopy(adom_device_vdom_structure)
    for adom in adom_device_vdom_policy_package_structure:
        policy_packages_result = fmgr_getter.get_policy_packages_from_manager(sid, fm_api_url, adom=adom)
        for policy_package in policy_packages_result:
            if 'scope member' in policy_package:
                parse_policy_package(policy_package, adom_device_vdom_policy_package_structure, adom)
    return adom_device_vdom_policy_package_structure

def parse_policy_package(policy_package, adom_device_vdom_policy_package_structure, adom):
    for scope_member in policy_package['scope member']:
        for device in adom_device_vdom_policy_package_structure[adom]:
            if device == scope_member['name']:
                for vdom in adom_device_vdom_policy_package_structure[adom][device]:
                    if vdom == scope_member['vdom']:
                        adom_device_vdom_policy_package_structure[adom][device].update({vdom: policy_package['name']})

def get_global_packages(sid, fm_api_url, adom_device_vdom_policy_package_structure) -> list:
    global_packages = []
    global_packages_results = fmgr_getter.get_policy_packages_from_manager(sid, fm_api_url, adom='')
    for policy_package in global_packages_results:
        if 'scope member' in policy_package:
            for adom in adom_device_vdom_policy_package_structure:
                if policy_package['scope member'] == adom:
                    global_packages.append(policy_package['name'])
    return global_packages

def initialize_device_config(mgm_details_device):
    device_config = {'name': mgm_details_device['name'],
                     'uid': mgm_details_device['uid'],
                     'rulebase_links': []}
    return device_config

def get_sid(importState: ImportStateController):
    fm_api_url = 'https://' + \
        importState.MgmDetails.Hostname + ':' + \
        str(importState.MgmDetails.Port) + '/jsonrpc'
    sid = fmgr_getter.login(importState.MgmDetails.ImportUser, importState.MgmDetails.Secret, fm_api_url)
    if sid is None:
        raise FwLoginFailed('did not succeed in logging in to FortiManager API, no sid returned')
    return sid


def get_objects(sid, fm_api_url, native_config_domain, native_config_global, adom_name, limit, nw_obj_types, svc_obj_types, adom_scope, arbitrary_vdom_for_updateable_objects):
    # get those objects that exist globally and on adom level

    # get network objects:
    for object_type in nw_obj_types:
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_domain['objects'], sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/" + object_type, "nw_obj_" + adom_scope + "_" + object_type, limit=limit)

    # get service objects:
    # service/custom is an undocumented API call!
    for object_type in svc_obj_types:
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_domain['objects'], sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/" + object_type, "svc_obj_" + adom_scope + "_" + object_type, limit=limit)

    # user: /pm/config/global/obj/user/local, /pm/config/global/obj/user/group
    # get user objects:
    for object_type in user_obj_types:
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_domain['objects'], sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/" + object_type, "user_obj_" + adom_scope + "_" + object_type, limit=limit)
            
    # get one arbitrary device and vdom to get dynamic objects
    # they are equal across all adoms, vdoms, devices
    if arbitrary_vdom_for_updateable_objects is None:
        logger = getFwoLogger()
        logger.error("arbitrary_vdom_for_updateable_objects is None, cannot get dynamic objects")
        return
    if arbitrary_vdom_for_updateable_objects['adom'] == adom_name:
        # get dynamic objects
        payload = {
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


def normalize_gateways(native_config, normalized_config_dict):
    for gateway in native_config['gateways']:
        normalized_gateway = {}
        normalized_gateway['Uid'] = gateway['uid']
        normalized_gateway['Name'] = gateway['name']
        normalized_gateway['Interfaces'] = normalize_interfaces()
        normalized_gateway['Routing'] = normalize_routing()
        normalized_gateway['RulebaseLinks'] = normalize_links(gateway['rulebase_links'])
        normalized_config_dict['gateways'].append(normalized_gateway)

def normalize_interfaces():
    # TODO
    return []

def normalize_routing():
    # TODO
    return []

def normalize_links(rulebase_links : list):
    for link in rulebase_links:
        link['link_type'] = link.pop('type')

        # Remove from_rulebase_uid and from_rule_uid if link_type is initial
        if link['link_type'] == 'initial':
            if link['from_rulebase_uid'] != None:
                link['from_rulebase_uid'] = None
            if link['from_rule_uid'] != None:
                link['from_rule_uid'] = None
    return rulebase_links

# def getZones(sid, fm_api_url, nativeConfig, adom_name, limit, debug_level):
#     nativeConfig.update({"zones": {}})

#     # get global zones?

#     # get local zones
#     for device in nativeConfig['devices']:
#         local_pkg_name = device['package']
#         for adom in nativeConfig['adoms']:
#             if adom['name']==adom_name:
#                 if local_pkg_name not in adom['package_names']:
#                     logger.error('local rulebase/package ' + local_pkg_name + ' not found in management ' + adom_name)
#                     return 1
#                 else:
#                     fmgr_getter.update_config_with_fortinet_api_call(
#                         nativeConfig['zones'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/obj/dynamic/interface", device['id'], debug=debug_level, limit=limit)

#     nativeConfig['zones']['zone_list'] = []
#     for device in nativeConfig['zones']:
#         for mapping in nativeConfig['zones'][device]:
#             if not isinstance(mapping, str):
#                 if not mapping['dynamic_mapping'] is None:
#                     for dyn_mapping in mapping['dynamic_mapping']:
#                         if 'name' in dyn_mapping and not dyn_mapping['name'] in nativeConfig['zones']['zone_list']:
#                             nativeConfig['zones']['zone_list'].append(dyn_mapping['name'])
#                         if 'local-intf' in dyn_mapping and not dyn_mapping['local-intf'][0] in nativeConfig['zones']['zone_list']:
#                             nativeConfig['zones']['zone_list'].append(dyn_mapping['local-intf'][0])
#                 if not mapping['platform_mapping'] is None:
#                     for dyn_mapping in mapping['platform_mapping']:
#                         if 'intf-zone' in dyn_mapping and not dyn_mapping['intf-zone'] in nativeConfig['zones']['zone_list']:
#                             nativeConfig['zones']['zone_list'].append(dyn_mapping['intf-zone'])



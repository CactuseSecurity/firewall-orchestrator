# import sys
# from common import importer_base_dir
# sys.path.append(importer_base_dir + '/fortiadom5ff')
from curses import raw

import json
import fwo_const
from copy import deepcopy
from model_controllers.import_state_controller import ImportStateController
from fwo_exceptions import ImportInterruption, FwLoginFailed, FwLogoutFailed
import fmgr_user
import fmgr_service
import fmgr_zone
from fwo_base import write_native_config_to_file
import fmgr_rule
import fmgr_network
import fmgr_getter
from fwo_log import getFwoLogger
from fmgr_gw_networking import getInterfacesAndRouting, normalize_network_data
from model_controllers.route_controller import get_ip_of_interface_obj
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.management_details_controller import ManagementDetailsController

# delete_v: brauchen scope nicht mehr?
#scope = ['global', 'adom']
nw_obj_types = ['firewall/address', 'firewall/address6', 'firewall/addrgrp',
                'firewall/addrgrp6', 'firewall/ippool', 'firewall/vip', 'system/external-resource']
svc_obj_types = ['application/list', 'application/group', 'application/categories',
                 'application/custom', 'firewall/service/custom', 'firewall/service/group']

# build the product of all scope/type combinations
#nw_obj_scope = ['nw_obj_' + s1 + '_' +
#                s2 for s1 in scope for s2 in nw_obj_types]
#svc_obj_scope = ['svc_obj_' + s1 + '_' +
#                 s2 for s1 in scope for s2 in svc_obj_types]

# zone_types = ['zones_global', 'zones_adom']

user_obj_types = ['user/local', 'user/group']
#user_scope = ['user_obj_' + s1 + '_' +
#                s2 for s1 in scope for s2 in user_obj_types]


def has_config_changed(full_config, mgm_details, force=False):
    # dummy - may be filled with real check later on
    return True

def get_config(nativeConfig: json, importState: ImportStateController):
    logger = getFwoLogger()
    if nativeConfig == {}:   # no native config was passed in, so getting it from FW-Manager
        nativeConfig.update({'domains': []})
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only:   # no native config was passed in, so getting it from FortiManager
        sid = get_sid(importState)
        limit = importState.FwoConfig.ApiFetchSize
        fm_api_url = importState.MgmDetails.buildFwApiString()
        native_config_global = initialize_native_config_domain(importState.MgmDetails)
        nativeConfig['domains'].append(native_config_global)
        adom_list = build_adom_list(importState)
        adom_device_vdom_structure = build_adom_device_vdom_structure(adom_list, sid, fm_api_url)
        arbitrary_vdom_for_updateable_objects = get_arbitrary_vdom(adom_device_vdom_structure)
        adom_device_vdom_policy_package_structure = add_policy_package_to_vdoms(adom_device_vdom_structure, sid, fm_api_url)
        # adom_device_vdom_policy_package_structure = {adom: {device: {vdom1: pol_pkg1}, {vdom2: pol_pkg2}}}
        #delete_v: später hier globale pol_pgk holen mit /pm/pkg/global

        # get globals
        get_objects(sid, fm_api_url, native_config_global, native_config_global, '', limit, nw_obj_types, svc_obj_types, 'global', arbitrary_vdom_for_updateable_objects)

        for adom in adom_list:
            adom_name = adom.DomainName
            native_config_adom = initialize_native_config_domain(adom)
            nativeConfig['domains'].append(native_config_adom)

            adom_scope = 'adom/'+adom_name
            # delete_v: objekte werden auch importiert wenn es kein device gibt, ist das gewollt?
            get_objects(sid, fm_api_url, native_config_adom, native_config_global, adom_name, limit, nw_obj_types, svc_obj_types, adom_scope, arbitrary_vdom_for_updateable_objects)
            # currently reading zone from objects/rules for backward compat with FortiManager 6.x
            # getZones(sid, fm_api_url, full_config, adom_name, limit, debug_level)
            
            # todo: bring interfaces and routing in new domain native config format
            #getInterfacesAndRouting(
            #    sid, fm_api_url, nativeConfig, adom_name, adom.Devices, limit)

            # initialize all rule dicts
            # delete_v: wenn initialize_rulebases wirklich überflüssig, dann in fmgr_getter löschen
            #fmgr_rule.initialize_rulebases(native_config_adom, adom_name)
            for mgm_details_device in adom.Devices:
                device_config = initialize_device_config(mgm_details_device)
                native_config_adom['gateways'].append(device_config)
                fmgr_rule.getAccessPolicy(
                    sid, fm_api_url, native_config_adom, adom_device_vdom_policy_package_structure, adom_name, mgm_details_device, device_config, limit)
                # delete_v: nat später
                #fmgr_rule.getNatPolicy(
                #    sid, fm_api_url, nativeConfig, adom_name, mgm_details_device, limit)
                                
        try:  # logout of fortimanager API
            fmgr_getter.logout(
                fm_api_url, sid)
        except Exception:
            raise FwLogoutFailed("logout exception probably due to timeout - irrelevant, so ignoring it")

    write_native_config_to_file(importState, nativeConfig)

    # delete_v: brauchen wir hier wirklich sid, dann muss die auch für parsing_config_only TRUE erzeugt werden
    normalizedConfig = normalize_config(importState, nativeConfig, parsing_config_only, sid)
    logger.info("completed getting config")
    return 0, normalizedConfig
        
    # normalize_network_data(full_config, config2import, mgm_details)

    # fmgr_user.normalize_users(
    #     full_config, config2import, current_import_id, user_scope)
    # fmgr_network.normalize_nwobjects(
    #     full_config, config2import, current_import_id, nw_obj_scope, jwt=jwt, mgm_id=mgm_details['id'])
    # fmgr_service.normalize_svcobjects(
    #     full_config, config2import, current_import_id, svc_obj_scope)
    # fmgr_user.normalize_users(
    #     full_config, config2import, current_import_id, user_scope)
    # fmgr_rule.normalize_access_rules(
    #     full_config, config2import, current_import_id, mgm_details=mgm_details, jwt=jwt)
    # fmgr_rule.normalize_nat_rules(
    #     full_config, config2import, current_import_id, jwt=jwt)
    # fmgr_network.remove_nat_ip_entries(config2import)
    # return 0

def initialize_native_config_domain(mgm_details : ManagementDetailsController):
    return {
        'domain_name': mgm_details.DomainName,
        'domain_uid': mgm_details.DomainUid,
        'is-super-manger': mgm_details.IsSuperManager,
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


def normalize_config(import_state, native_config: json, parsing_config_only: bool, sid: str) -> FwConfigManagerListController:

    raise NotImplementedError("normalize_config not implemented for FortiManager 5.x")
    # delete_v: einfach kopiert von cp
    # manager_list = FwConfigManagerListController()

    # if 'domains' not in native_config:
    #     logger = getFwoLogger()
    #     logger.error("No domains found in native config. Cannot normalize config.")
    #     raise ImportInterruption("No domains found in native config. Cannot normalize config.")
    
    # for native_conf in native_config['domains']:
    #     normalizedConfigDict = fwo_const.emptyNormalizedFwConfigJsonDict
    #     normalized_config = normalize_single_manager_config(native_conf, normalizedConfigDict, import_state, parsing_config_only, sid)
    #     manager = FwConfigManager(ManagerUid=calcManagerUidHash(import_state.MgmDetails),
    #                                 ManagerName=import_state.MgmDetails.Name,
    #                                 IsGlobal=import_state.MgmDetails.IsSuperManager, 
    #                                 DependantManagerUids=[], 
    #                                 Configs=[normalized_config])
    #     manager_list.addManager(manager)

    # return manager_list


def build_adom_list(importState : ImportStateController):
    adom_list = []
    if importState.MgmDetails.IsSuperManager:
        for subManager in importState.MgmDetails.SubManagers:
            adom_list.append(deepcopy(subManager))
    return adom_list

def build_adom_device_vdom_structure(adom_list, sid, fm_api_url):
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
        policy_packages_result = fmgr_getter.get_policy_packages_from_manager(adom, sid, fm_api_url)
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

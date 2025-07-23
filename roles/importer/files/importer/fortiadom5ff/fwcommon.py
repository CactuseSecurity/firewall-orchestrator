# import sys
# from common import importer_base_dir
# sys.path.append(importer_base_dir + '/fortiadom5ff')
from curses import raw

import json
from copy import deepcopy
from model_controllers.import_state_controller import ImportStateController
import fwo_exceptions
import fmgr_user
import fmgr_service
import fmgr_zone
import fmgr_rule
import fmgr_network
import fmgr_getter
from fwo_log import getFwoLogger
from fmgr_gw_networking import getInterfacesAndRouting, normalize_network_data
from model_controllers.interface_controller import get_ip_of_interface_obj
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.management_details_controller import ManagementDetailsController


scope = ['global', 'adom']
nw_obj_types = ['firewall/address', 'firewall/address6', 'firewall/addrgrp',
                'firewall/addrgrp6', 'firewall/ippool', 'firewall/vip', 'system/external-resource']
svc_obj_types = ['application/list', 'application/group', 'application/categories',
                 'application/custom', 'firewall/service/custom', 'firewall/service/group']

# build the product of all scope/type combinations
nw_obj_scope = ['nw_obj_' + s1 + '_' +
                s2 for s1 in scope for s2 in nw_obj_types]
svc_obj_scope = ['svc_obj_' + s1 + '_' +
                 s2 for s1 in scope for s2 in svc_obj_types]

# zone_types = ['zones_global', 'zones_adom']

user_obj_types = ['user/local', 'user/group']
user_scope = ['user_obj_' + s1 + '_' +
                s2 for s1 in scope for s2 in user_obj_types]


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
        nativ_config_global = initialize_nativ_config_domain(importState.MgmDetails)
        adom_list = build_adom_list(importState)
        adom_device_vdom_structure = build_adom_device_vdom_structure(adom_list, sid, fm_api_url)
        arbitrary_vdom_for_updateable_objects = get_arbitrary_vdom(adom_device_vdom_structure)
        adom_device_vdom_policy_package_structure = add_policy_package_to_vdoms(adom_device_vdom_structure, sid, fm_api_url)
        # adom_device_vdom_policy_package_structure = {adom: {device: {vdom1: pol_pkg1}, {vdom2: pol_pkg2}}}
        #delete_v: später hier globale pol_pgk holen mit /pm/pkg/global

        # get globals
        get_objects(sid, fm_api_url, nativ_config_global, nativ_config_global, '', limit, nw_obj_types, svc_obj_types, 'global', arbitrary_vdom_for_updateable_objects)

        for adom in adom_list:
            adom_name = adom.MgmDetails.DomainName
            nativ_config_adom = initialize_nativ_config_domain(adom.MgmDetails)

            adom_scope = 'adom/'+adom_name
            get_objects(sid, fm_api_url, nativ_config_adom, nativ_config_global, adom_name, limit, nw_obj_types, svc_obj_types, adom_scope, arbitrary_vdom_for_updateable_objects)
            # currently reading zone from objects/rules for backward compat with FortiManager 6.x
            # getZones(sid, fm_api_url, full_config, adom_name, limit, debug_level)
            
            # todo: bring interfaces and routing in new domain native config format
            #getInterfacesAndRouting(
            #    sid, fm_api_url, nativeConfig, adom_name, adom.MgmDetails.Devices, limit)

            # initialize all rule dicts
            fmgr_rule.initialize_rulebases(nativ_config_adom, adom_name)
            for dev in adom.MgmDetails.Devices:
                fmgr_rule.getAccessPolicy(
                    sid, fm_api_url, nativeConfig, adom_device_vdom_structure, adom_name, dev, limit)
                fmgr_rule.getNatPolicy(
                    sid, fm_api_url, nativeConfig, adom_name, dev, limit)
                
            fetched_global = True
                
        try:  # logout of fortimanager API
            fmgr_getter.logout(
                fm_api_url, sid)
        except Exception:
            raise fwo_exceptions.FwLogoutFailed("logout exception probably due to timeout - irrelevant, so ignoring it")
        
    # delete_v: brauchen wir hier wirklich sid, dann muss die auch für parsing_config_only TRUE erzeugt werden
    normalizedConfig = normalize_config(importState, nativeConfig, parsing_config_only, sid)
    logger.info("completed getting config")
    return 0, normalizedConfig
        
    normalize_network_data(full_config, config2import, mgm_details)

    fmgr_user.normalize_users(
        full_config, config2import, current_import_id, user_scope)
    fmgr_network.normalize_nwobjects(
        full_config, config2import, current_import_id, nw_obj_scope, jwt=jwt, mgm_id=mgm_details['id'])
    fmgr_service.normalize_svcobjects(
        full_config, config2import, current_import_id, svc_obj_scope)
    fmgr_user.normalize_users(
        full_config, config2import, current_import_id, user_scope)
    fmgr_rule.normalize_access_rules(
        full_config, config2import, current_import_id, mgm_details=mgm_details, jwt=jwt)
    fmgr_rule.normalize_nat_rules(
        full_config, config2import, current_import_id, jwt=jwt)
    fmgr_network.remove_nat_ip_entries(config2import)
    return 0

def initialize_nativ_config_domain(mgm_details : ManagementDetailsController):
    return {
        'domain_name': mgm_details.DomainName,
        'domain_uid': mgm_details.DomainUid,
        'is-super-manger': mgm_details.IsSuperManager,
        'management_name': mgm_details.Name,
        'management_uid': mgm_details.Uid,
        'objects': [],
        'rulebases': [],
        'nat_rulebases': [],
        'gateways': []}

def get_arbitrary_vdom(adom_device_vdom_structure):
    for adom in adom_device_vdom_structure:
        for device in adom_device_vdom_structure[adom]:
            for vdom in adom_device_vdom_structure[adom][device]:
                return {'adom': adom, 'device': device, 'vdom': vdom}


# delete_v: einfach kopiert von cp
def normalize_config(import_state, native_config: json, parsing_config_only: bool, sid: str) -> FwConfigManagerListController:

    manager_list = FwConfigManagerListController()

    if 'domains' not in native_config:
        getFwoLogger().error("No domains found in native config. Cannot normalize config.")
        raise ImportInterruption("No domains found in native config. Cannot normalize config.")
    
    for native_conf in native_config['domains']:
        normalizedConfigDict = fwo_const.emptyNormalizedFwConfigJsonDict
        normalized_config = normalize_single_manager_config(native_conf, normalizedConfigDict, import_state, parsing_config_only, sid)
        manager = FwConfigManager(ManagerUid=calcManagerUidHash(import_state.MgmDetails),
                                    ManagerName=import_state.MgmDetails.Name,
                                    IsGlobal=import_state.MgmDetails.IsSuperManager, 
                                    DependantManagerUids=[], 
                                    Configs=[normalized_config])
        manager_list.addManager(manager)

    return manager_list


def build_adom_list(importState : ImportStateController):
    adom_list = []
    if importState.MgmDetails.IsSuperManager:
        for subManager in importState.MgmDetails.SubManagers:
            adom_list.append(deepcopy(subManager))
    return adom_list

def build_adom_device_vdom_structure(adom_list, sid, fm_api_url):
    adom_device_vdom_structure = {}
    for adom in adom_list:
        adom_device_vdom_structure.update({adom.MgmDetails.DomainName: {}})
        if len(adom.MgmDetails.Devices) > 0:
            fmgr_devices = fmgr_getter.get_devices_from_manager(adom.MgmDetails, sid, fm_api_url)
            for fmgr_device in fmgr_devices:
                device_vdom_dict = parse_device_and_vdom(fmgr_device)
                adom_device_vdom_structure[adom.MgmDetails.DomainName].update(device_vdom_dict)

def parse_device_and_vdom(fmgr_device):
    device_vdom_dict = {fmgr_device: {}}
    if 'vdom' in fmgr_device:
        for vdom in fmgr_device['vdom']:
            device_vdom_dict[fmgr_device].update({vdom['name']: ''})
    return device_vdom_dict

def add_policy_package_to_vdoms(adom_device_vdom_structure, sid, fm_api_url):
    for adom in adom_device_vdom_structure:
        policy_packages_result = fmgr_getter.get_policy_packages_from_manager(adom, sid, fm_api_url)
        for policy_package in policy_packages_result:
            if 'scope member' in policy_package:
                parse_policy_package(policy_package, adom_device_vdom_structure, adom)

def parse_policy_package(policy_package, adom_device_vdom_structure, adom):
    for scope_member in policy_package['scope member']:
        for device in adom_device_vdom_structure[adom]:
            if device == scope_member['name']:
                for vdom in adom_device_vdom_structure[adom][device]:
                    if vdom == scope_member['vdom']:
                        adom_device_vdom_structure[adom][device].update({vdom: policy_package['name']})

def get_sid(importState: ImportStateController):
    fm_api_url = 'https://' + \
        importState.MgmDetails.Hostname + ':' + \
        importState.MgmDetails.Port + '/jsonrpc'
    sid = fmgr_getter.login(importState.MgmDetails.ImportUser, importState.MgmDetails.Secret, fm_api_url)
    if sid is None:
        raise fwo_exceptions.FwLoginFailed('did not succeed in logging in to FortiManager API, no sid returned')
    return sid


def get_objects(sid, fm_api_url, nativ_config_domain, nativ_config_global, adom_name, limit, nw_obj_types, svc_obj_types, adom_scope, arbitrary_vdom_for_updateable_objects):
    # get those objects that exist globally and on adom level

    # get network objects:
    for object_type in nw_obj_types:
        fmgr_getter.update_config_with_fortinet_api_call(
            nativ_config_domain['objects'], sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/" + object_type, "nw_obj_" + adom_scope + "_" + object_type, limit=limit)

    # get service objects:
    # service/custom is an undocumented API call!
    for object_type in svc_obj_types:
        fmgr_getter.update_config_with_fortinet_api_call(
            nativ_config_domain['objects'], sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/" + object_type, "svc_obj_" + adom_scope + "_" + object_type, limit=limit)

    # user: /pm/config/global/obj/user/local, /pm/config/global/obj/user/group
    # get user objects:
    for object_type in user_obj_types:
        fmgr_getter.update_config_with_fortinet_api_call(
            nativ_config_domain['objects'], sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/" + object_type, "user_obj_" + adom_scope + "_" + object_type, limit=limit)
            
    # get one arbitrary device and vdom to get dynamic objects
    # they are equal across all adoms, vdoms, devices
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
            nativ_config_global['objects'], sid, fm_api_url, "sys/proxy/json", "nw_obj_global_firewall/internet-service-basic", limit=limit, payload=payload, method='exec')





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

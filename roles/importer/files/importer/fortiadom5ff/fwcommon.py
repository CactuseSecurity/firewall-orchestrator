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
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only:   # no native config was passed in, so getting it from FortiManager
        sid = get_sid(importState)
        limit = importState.FwoConfig.ApiFetchSize
        fm_api_url = importState.MgmDetails.buildFwApiString()

        # get globals in first adom loop iteration
        fetched_global = False

        for adom in build_adom_list(importState):
            adom_name = adom.MgmDetails.DomainName

            get_objects(sid, fm_api_url, nativeConfig, adom_name, limit, scope, nw_obj_types, svc_obj_types, fetched_global)
            # currently reading zone from objects/rules for backward compat with FortiManager 6.x
            # getZones(sid, fm_api_url, full_config, adom_name, limit, debug_level)
            getInterfacesAndRouting(
                sid, fm_api_url, nativeConfig, adom_name, adom.MgmDetails.Devices, limit)

            # initialize all rule dicts
            fmgr_rule.initializeRulebases(nativeConfig, adom_name)
            for dev in adom.MgmDetails.Devices:
                fmgr_rule.getAccessPolicy(
                    sid, fm_api_url, nativeConfig, adom_name, dev, limit)
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

def get_sid(importState: ImportStateController):
    fm_api_url = 'https://' + \
        importState.MgmDetails.Hostname + ':' + \
        importState.MgmDetails.Port + '/jsonrpc'
    sid = fmgr_getter.login(importState.MgmDetails.ImportUser, importState.MgmDetails.Secret, fm_api_url)
    if sid is None:
        raise fwo_exceptions.FwLoginFailed('did not succeed in logging in to FortiManager API, no sid returned')
    return sid


def get_objects(sid, fm_api_url, nativeConfig, adom_name, limit, scope, nw_obj_types, svc_obj_types, fetched_global):
    logger = getFwoLogger()
    # get those objects that exist globally and on adom level
    for s in scope:

        # get global objects only once
        if s == 'global':
            if fetched_global:
                continue
            else:
                adom_scope = s
        elif s == 'adom':
            adom_scope = 'adom/'+adom_name
        else:
            logger.error('unexpected scope for adom: ' + adom_name)


        # get network objects:
        for object_type in nw_obj_types:
            fmgr_getter.update_config_with_fortinet_api_call(
                nativeConfig, sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/" + object_type, "nw_obj_" + adom_scope + "_" + object_type, limit=limit)

        # get service objects:
        # service/custom is an undocumented API call!
        for object_type in svc_obj_types:
            fmgr_getter.update_config_with_fortinet_api_call(
                nativeConfig, sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/" + object_type, "svc_obj_" + adom_scope + "_" + object_type, limit=limit)

        # user: /pm/config/global/obj/user/local, /pm/config/global/obj/user/group
        # get user objects:
        for object_type in user_obj_types:
            fmgr_getter.update_config_with_fortinet_api_call(
                nativeConfig, sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/" + object_type, "user_obj_" + adom_scope + "_" + object_type, limit=limit)
            
    # get one arbitrary device and vdom to get dynamic objects
    # they are equal across all adoms, vdoms, devices
    if not fetched_global:
        devices = fmgr_getter.fortinet_api_call(sid, fm_api_url, '/dvmdb/adom/' + adom_name + '/device')
        if len(devices)>0 and 'name' in devices[0] and 'vdom' in devices[0] and 'name' in devices[0]['vdom'][0]:
            arbitraryDevice = devices[0]['name']
            arbitraryVdom = devices[0]['vdom'][0]['name']
        else:
            logger.error('no device or vdom info for adom: ' + adom_name)

        # get dynamic objects
        payload = {
            'params': [
                {
                    'data': {
                        'action': 'get',
                        'resource': '/api/v2/monitor/firewall/internet-service-basic?vdom=' + arbitraryVdom,
                        'target': [
                            'adom/' + adom_name + '/device/' + arbitraryDevice
                        ]
                    }
                }
            ]
        }
        fmgr_getter.update_config_with_fortinet_api_call(
            nativeConfig, sid, fm_api_url, "sys/proxy/json", "nw_obj_global_firewall/internet-service-basic", limit=limit, payload=payload, method='exec')





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

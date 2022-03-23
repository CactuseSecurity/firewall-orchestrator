import sys
from common import importer_base_dir
sys.path.append(importer_base_dir + '/fortiadom5ff')
import fmgr_user
import fmgr_service
import fmgr_zone
import traceback
import fmgr_rule
import fmgr_network
import fmgr_getter
from curses import raw
from fwo_log import getFwoLogger
from fmgr_gw_networking import getInterfacesAndRouting, normalize_network_data

scope = ['global', 'adom']
nw_obj_types = ['firewall/address', 'firewall/address6', 'firewall/addrgrp',
                'firewall/addrgrp6', 'firewall/ippool', 'firewall/vip']
svc_obj_types = ['application/list', 'application/group', 'application/categories',
                 'application/custom', 'firewall/service/custom', 'firewall/service/group']

# build the product of all scope/type combinations
nw_obj_scope = ['nw_obj_' + s1 + '_' +
                s2 for s1 in scope for s2 in nw_obj_types]
svc_obj_scope = ['svc_obj_' + s1 + '_' +
                 s2 for s1 in scope for s2 in svc_obj_types]

# zone_types = ['zones_global', 'zones_adom']
user_types = ['users_global', 'users_adom']
user_scope = ['user_objects']


def has_config_changed(full_config, mgm_details, debug_level=0, force=False, proxy=None, ssl_verification=None):
    # dummy - may be filled with real check later on
    return True


def get_config(config2import, full_config, current_import_id, mgm_details, debug_level=0, proxy=None, limit=100, force=False, ssl_verification=None, jwt=''):
    logger = getFwoLogger(debug_level=debug_level)
    if full_config == {}:   # no native config was passed in, so getting it from FortiManager
        parsing_config_only = False
    else:
        parsing_config_only = True

    if not parsing_config_only:   # no native config was passed in, so getting it from FortiManager
        fm_api_url = 'https://' + \
            mgm_details['hostname'] + ':' + \
            str(mgm_details['port']) + '/jsonrpc'
        api_domain = ''
        sid = fmgr_getter.login(mgm_details['user'], mgm_details['secret'], mgm_details['hostname'],
                                mgm_details['port'], api_domain, debug=debug_level, ssl_verification='', proxy_string=proxy)
        if sid is None:
            logger.ERROR('did not succeed in logging in to FortiManager API, no sid returned')
            return 1

    adom_name = mgm_details['configPath']
    if adom_name is None:
        logger.error('no ADOM name set for management ' + mgm_details['id'])
        return 1
    else:
        if not parsing_config_only:   # no native config was passed in, so getting it from FortiManager
            getObjects(sid, fm_api_url, full_config, adom_name, limit,
                       debug_level, scope, nw_obj_types, svc_obj_types)
            # currently reading zone from objects/rules for backward compat with FortiManager 6.x
            # getZones(sid, fm_api_url, full_config, adom_name, limit, debug_level)
            getInterfacesAndRouting(
                sid, fm_api_url, full_config, adom_name, mgm_details['devices'], limit, debug_level=debug_level)

            # initialize all rule dicts
            fmgr_rule.initializeRulebases(full_config)
            for dev in mgm_details['devices']:
                fmgr_rule.getAccessPolicy(
                    sid, fm_api_url, full_config, adom_name, dev, limit, debug_level=debug_level)
                fmgr_rule.getNatPolicy(
                    sid, fm_api_url, full_config, adom_name, dev, limit, debug_level=debug_level)

            try:  # logout of fortimanager API
                fmgr_getter.logout(
                    fm_api_url, sid, ssl_verification='', proxy_string='', debug=debug_level)
            except:
                logger.warning("logout exception probably due to timeout - irrelevant, so ignoring it")

        # now we normalize relevant parts of the raw config and write the results to config2import dict
        # currently reading zone from objects for backward compat with FortiManager 6.x
        # fmgr_zone.normalize_zones(full_config, config2import, current_import_id)

        # write normalized networking data to config2import 
        # this is currently not written to the database but only used for natting decisions
        # later we will probably store the networking info in the database as well as a basis
        # for path analysis

        normalize_network_data(full_config, config2import, mgm_details, debug_level=debug_level)

        fmgr_user.normalize_users(
            full_config, config2import, current_import_id, user_scope)
        fmgr_network.normalize_nwobjects(
            full_config, config2import, current_import_id, nw_obj_scope, jwt=jwt, mgm_id=mgm_details['id'], debug_level=debug_level)
        fmgr_service.normalize_svcobjects(
            full_config, config2import, current_import_id, svc_obj_scope)
        fmgr_rule.normalize_access_rules(
            full_config, config2import, current_import_id, mgm_details=mgm_details, jwt=jwt, debug_level=debug_level)
        fmgr_rule.normalize_nat_rules(
            full_config, config2import, current_import_id, jwt=jwt)
        fmgr_network.remove_nat_ip_entries(config2import)
    return 0


def getObjects(sid, fm_api_url, raw_config, adom_name, limit, debug_level, scope, nw_obj_types, svc_obj_types):
    # get those objects that exist globally and on adom level
    for s in scope:
        # get network objects:
        for object_type in nw_obj_types:
            if s == 'adom':
                adom_scope = 'adom/'+adom_name
            else:
                adom_scope = s
            fmgr_getter.update_config_with_fortinet_api_call(
                raw_config, sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/" + object_type, "nw_obj_" + s + "_" + object_type, debug=debug_level, limit=limit)

        # get service objects:
        # service/custom is an undocumented API call!
        options = []    # options = ['get reserved']
        for object_type in svc_obj_types:
            if s == 'adom':
                adom_scope = 'adom/'+adom_name
            else:
                adom_scope = s
            fmgr_getter.update_config_with_fortinet_api_call(
                raw_config, sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/" + object_type, "svc_obj_" + s + "_" + object_type, debug=debug_level, limit=limit, options=options)

    #    user: /pm/config/global/obj/user/local
    fmgr_getter.update_config_with_fortinet_api_call(
        raw_config, sid, fm_api_url, "/pm/config/global/obj/user/local", "users_local", debug=debug_level, limit=limit)


# def getZones(sid, fm_api_url, raw_config, adom_name, limit, debug_level):
#     raw_config.update({"zones": {}})

#     # get global zones?

#     # get local zones
#     for device in raw_config['devices']:
#         local_pkg_name = device['package']
#         for adom in raw_config['adoms']:
#             if adom['name']==adom_name:
#                 if local_pkg_name not in adom['package_names']:
#                     logger.error('local rulebase/package ' + local_pkg_name + ' not found in management ' + adom_name)
#                     return 1
#                 else:
#                     fmgr_getter.update_config_with_fortinet_api_call(
#                         raw_config['zones'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/obj/dynamic/interface", device['id'], debug=debug_level, limit=limit)

#     raw_config['zones']['zone_list'] = []
#     for device in raw_config['zones']:
#         for mapping in raw_config['zones'][device]:
#             if not isinstance(mapping, str):
#                 if not mapping['dynamic_mapping'] is None:
#                     for dyn_mapping in mapping['dynamic_mapping']:
#                         if 'name' in dyn_mapping and not dyn_mapping['name'] in raw_config['zones']['zone_list']:
#                             raw_config['zones']['zone_list'].append(dyn_mapping['name'])
#                         if 'local-intf' in dyn_mapping and not dyn_mapping['local-intf'][0] in raw_config['zones']['zone_list']:
#                             raw_config['zones']['zone_list'].append(dyn_mapping['local-intf'][0])
#                 if not mapping['platform_mapping'] is None:
#                     for dyn_mapping in mapping['platform_mapping']:
#                         if 'intf-zone' in dyn_mapping and not dyn_mapping['intf-zone'] in raw_config['zones']['zone_list']:
#                             raw_config['zones']['zone_list'].append(dyn_mapping['intf-zone'])

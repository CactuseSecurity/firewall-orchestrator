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
import logging

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
    return True


def get_config(config2import, full_config, current_import_id, mgm_details, debug_level=0, proxy=None, limit=100, force=False, ssl_verification=None, jwt=''):
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
            logging.ERROR(
                'did not succeed in logging in to FortiManager API, so sid returned')
            return 1

    adom_name = mgm_details['configPath']
    if adom_name is None:
        logging.error('no ADOM name set for this management!')
        return 1
    else:
        if not parsing_config_only:   # no native config was passed in, so getting it from FortiManager
            getObjects(sid, fm_api_url, full_config, adom_name, limit,
                       debug_level, scope, nw_obj_types, svc_obj_types)
            # currently reading zone from objects/rules for backward compat with FortiManager 6.x
            # getZones(sid, fm_api_url, full_config, adom_name, limit, debug_level)
            getInterfacesAndRouting(
                sid, fm_api_url, full_config, adom_name, mgm_details['devices'], limit, debug_level)

            # initialize all rule dicts
            fmgr_rule.initializeRulebases(full_config)
            for dev in mgm_details['devices']:
                fmgr_rule.getAccessPolicy(
                    sid, fm_api_url, full_config, adom_name, dev, limit, debug_level)
                fmgr_rule.getNatPolicy(
                    sid, fm_api_url, full_config, adom_name, dev, limit, debug_level)

            try:  # logout of fortimanager API
                fmgr_getter.logout(
                    fm_api_url, sid, ssl_verification='', proxy_string='', debug=debug_level)
            except:
                logging.warning(
                    "fortiadm5ff/get_config - logout exception probably due to timeout - irrelevant, so ignoring it")

        # now we normalize relevant parts of the raw config and write the results to config2import dict
        # currently reading zone from objects for backward compat with FortiManager 6.x
        # fmgr_zone.normalize_zones(full_config, config2import, current_import_id)
        fmgr_user.normalize_users(
            full_config, config2import, current_import_id, user_scope)
        fmgr_network.normalize_nwobjects(
            full_config, config2import, current_import_id, nw_obj_scope, jwt=jwt)
        fmgr_service.normalize_svcobjects(
            full_config, config2import, current_import_id, svc_obj_scope)
        fmgr_rule.normalize_access_rules(
            full_config, config2import, current_import_id, jwt=jwt)
        fmgr_rule.normalize_nat_rules(
            full_config, config2import, current_import_id, jwt=jwt)
        fmgr_network.remove_nat_ip_entries(config2import)
    return 0


def getInterfacesAndRouting(sid, fm_api_url, raw_config, adom_name, devices, limit, debug_level):
    # get network information (also needed for source nat)
    # adom_scope = 'adom/'+adom_name
    # fmgr_getter.update_config_with_fortinet_api_call(
    #     raw_config, sid, fm_api_url, "/pm/config/"+adom_scope+"/obj/dynamic/interface", "interfaces-dynamic", debug=debug_level, limit=limit)

    # get interfaces via encapsulated call to FortiOS on FortiGate
    # (https://fndn.fortinet.net/index.php?/forums/topic/2938-get-interface-status-not-administrative-status-from-api/&tab=comments#comment-11344)
    # obsolete

    # strip off vdom names, just deal with the plain device
    device_array = []
    for dev in devices:
        dev_name = dev["name"]
        vdom_str = ""
        vdom_name = "undefined"
        if "_" in dev_name:  # strip off _vdom_name
            dev_name_ar = dev_name.split("_")
            vdom_name = dev_name_ar.pop()
            vdom_str = "&vdom="+vdom_name
            dev_name = "_".join(dev_name_ar)

        device_array.append(dev_name)

    for dev_name in device_array:

        payload = {
            "id": 1,
            "params": [
                {
                    "fields": [
                        "name",
                        "ip"
                    ],
                    "sub fetch": {
                        "client-options": {
                            "subfetch hidden": 1
                        },
                        "dhcp-snooping-server-list": {
                            "subfetch hidden": 1
                        },
                        "egress-queues": {
                            "subfetch hidden": 1
                        },
                        "ipv6": {
                            "fields": [
                                "ip6-address"
                            ],
                            "sub fetch": {
                                "dhcp6-iapd-list": {
                                    "subfetch hidden": 1
                                },
                                "ip6-delegated-prefix-list": {
                                    "subfetch hidden": 1
                                },
                                "ip6-extra-addr": {
                                    "subfetch hidden": 1
                                },
                                "ip6-prefix-list": {
                                    "subfetch hidden": 1
                                },
                                "vrrp6": {
                                    "subfetch hidden": 1
                                }
                            }
                        },
                        "l2tp-client-settings": {
                            "subfetch hidden": 1
                        },
                        "secondaryip": {
                            "subfetch hidden": 1
                        },
                        "tagging": {
                            "subfetch hidden": 1
                        },
                        "vrrp": {
                            "subfetch hidden": 1
                        },
                        "wifi-networks": {
                            "subfetch hidden": 1
                        }
                    }
                }
            ]
        }
        try:
            fmgr_getter.update_config_with_fortinet_api_call(
                raw_config, sid, fm_api_url, "/pm/config/device/" + dev_name + "/global/system/interface",
                "interfaces_per_device/" + dev_name, payload=payload, debug=debug_level, limit=limit, method="get")
        except:
            logging.warning("import_management - error while getting interfaces of device " + dev_name + ", ignoring, traceback: " + str(traceback.format_exc()))
        # if vdom_name != 'undefined':
        #     try:
        #         fmgr_getter.update_config_with_fortinet_api_call(
        #             raw_config, sid, fm_api_url, "/pm/config/device/" + vdom_name + "/global/system/interface",
        #             "interfaces_per_vdom/dev:" + dev_name + "/vdom:" + vdom_name,
        #             payload=payload, debug=debug_level, limit=limit, method="get")
        #     except:
        #         logging.warning("import_management - error while getting vdom interfaces of device " + vdom_name + ", ignoring, traceback: " + str(traceback.format_exc()))

        for ip_version in ["ipv4", "ipv6"]:
            payload = {
                "params": [
                    {
                        "data": {
                            "target": ["adom/" + adom_name + "/device/" + dev_name],
                            "action": "get",
                            "resource": "/api/v2/monitor/router/" + ip_version + "/select?" + vdom_str
                        }
                    }
                ]
            }

            try:
                fmgr_getter.update_config_with_fortinet_api_call(
                    raw_config, sid, fm_api_url, "/sys/proxy/json",
                    "routing-table-" + ip_version + '/' + dev_name,
                    payload=payload, debug=debug_level, limit=limit, method="exec")
            except:
                logging.warning(
                    "import_management - error while getting routing table of device " + dev["name"] + ", ignoring")


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
#                     logging.error('local rulebase/package ' + local_pkg_name + ' not found in management ' + adom_name)
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

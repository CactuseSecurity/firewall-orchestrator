import sys
from common import importer_base_dir
sys.path.append(importer_base_dir + '/fortiosmanagementREST')
import fOS_user
import fOS_service
import fOS_zone
import fOS_rule
import fOS_network
import fOS_getter
from curses import raw
from fwo_log import getFwoLogger
from fOS_gw_networking import getInterfacesAndRouting, normalize_network_data
from fwo_data_networking import get_ip_of_interface_obj

from fwo_const import list_delimiter, nat_postfix, fwo_config_filename
from fwo_config import readConfig
from fwo_api import setAlert, create_data_issue


nw_obj_types = ['firewall/address', 'firewall/address6', 'firewall/addrgrp',
                'firewall/addrgrp6', 'firewall/ippool', 'firewall/vip']
svc_obj_types = ['application/list', 'application/group',
                 # 'application/categories',
                 #'application/custom', 
                'firewall.service/custom', 
                'firewall.service/group'
                ]

# build the product of all scope/type combinations
nw_obj_scope = ['nw_obj_' + s1 for s1 in nw_obj_types]
svc_obj_scope = ['svc_obj_' + s1 for s1 in svc_obj_types]

# zone_types = ['zones_global', 'zones_adom']

user_obj_types = ['user/local', 'user/group']
user_scope = ['user_obj_' + s1 for s1 in user_obj_types]


def has_config_changed(full_config, mgm_details, force=False):
    # dummy - may be filled with real check later on
    return True


def get_config(config2import, full_config, current_import_id, mgm_details, limit=100, force=False, jwt=''):
    logger = getFwoLogger()
    if full_config == {}:   # no native config was passed in, so getting it from FortiManager
        parsing_config_only = False
    else:
        parsing_config_only = True

    # fmgr API login
    if not parsing_config_only:   # no native config was passed in, so getting it from FortiManager
        fm_api_url = 'https://' +  mgm_details['hostname'] + ':' +  str(mgm_details['port']) + '/api/v2'
        sid = mgm_details['import_credential']['secret']

        if not parsing_config_only:   # no native config was passed in, so getting it from FortiManager
            getObjects(sid, fm_api_url, full_config, limit, nw_obj_types, svc_obj_types)
            # getInterfacesAndRouting(
            #     sid, fm_api_url, full_config, mgm_details['devices'], limit)

            # adding global zone first:
            fOS_zone.add_zone_if_missing (config2import, 'global', current_import_id)

            # initialize all rule dicts
            fOS_rule.initializeRulebases(full_config)
            for dev in mgm_details['devices']:
                fOS_rule.getAccessPolicy(sid, fm_api_url, full_config, limit)
                # fOS_rule.getNatPolicy(sid, fm_api_url, full_config, limit)

        # now we normalize relevant parts of the raw config and write the results to config2import dict
        # currently reading zone from objects for backward compat with FortiManager 6.x
        # fmgr_zone.normalize_zones(full_config, config2import, current_import_id)

        # write normalized networking data to config2import 
        # this is currently not written to the database but only used for natting decisions
        # later we will probably store the networking info in the database as well as a basis
        # for path analysis

        # normalize_network_data(full_config, config2import, mgm_details)

        fOS_user.normalize_users(
            full_config, config2import, current_import_id, user_scope)
        fOS_network.normalize_nwobjects(
            full_config, config2import, current_import_id, nw_obj_scope, jwt=jwt, mgm_id=mgm_details['id'])
        fOS_service.normalize_svcobjects(
            full_config, config2import, current_import_id, svc_obj_scope)
        fOS_user.normalize_users(
            full_config, config2import, current_import_id, user_scope)
        fOS_rule.normalize_access_rules(
            full_config, config2import, current_import_id, mgm_details=mgm_details, jwt=jwt)
        # fOS_rule.normalize_nat_rules(
        #     full_config, config2import, current_import_id, jwt=jwt)
        # fOS_network.remove_nat_ip_entries(config2import)
    return 0


def getObjects(sid, fm_api_url, raw_config, limit, nw_obj_types, svc_obj_types):
    # get network objects:
    for object_type in nw_obj_types:
        fOS_getter.update_config_with_fortiOS_api_call(
            raw_config, fm_api_url + "/cmdb/" + object_type + "?access_token=" + sid, "nw_obj_" + object_type, limit=limit)

    # get service objects:
    for object_type in svc_obj_types:
        fOS_getter.update_config_with_fortiOS_api_call(
            raw_config, fm_api_url + "/cmdb/" + object_type + "?access_token=" + sid, "svc_obj_" + object_type, limit=limit)

    # get user objects:
    for object_type in user_obj_types:
        fOS_getter.update_config_with_fortiOS_api_call(
            raw_config, fm_api_url + "/cmdb/" + object_type + "?access_token=" + sid, "user_obj_" + object_type, limit=limit)


# TODO: deal with objects with identical names (e.g. all ipv4 & all ipv6)
def resolve_objects (obj_name_string_list, lookup_dict={}, delimiter=list_delimiter, jwt=None, import_id=None, mgm_id=None):
    logger = getFwoLogger()
    fwo_config = readConfig(fwo_config_filename)

    ref_list = []
    objects_not_found = []
    for el in obj_name_string_list.split(delimiter):
        found = False
        if el in lookup_dict:
            ref_list.append(lookup_dict[el])
        else:
            objects_not_found.append(el)

    for obj in objects_not_found:
        if obj != 'all' and obj != 'Original':
            if not create_data_issue(fwo_config['fwo_api_base_url'], jwt, import_id=import_id, obj_name=obj, severity=1, mgm_id=mgm_id):
                logger.warning("resolve_raw_objects: encountered error while trying to log an import data issue using create_data_issue")

            desc = "found a broken object reference '" + obj + "' "
            setAlert(fwo_config['fwo_api_base_url'], jwt, import_id=import_id, title="object reference error", mgm_id=mgm_id, severity=1, role='importer', \
                description=desc, source='import', alertCode=16)

    return delimiter.join(ref_list)

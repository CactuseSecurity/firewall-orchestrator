import copy
import jsonpickle
import ipaddress
import time
from fwo_const import list_delimiter, nat_postfix, dummy_ip
from fwo_base import extend_string_list
from fmgr_service import create_svc_object
from fmgr_network import create_network_object, get_first_ip_of_destination
from fmgr_zone import add_zone_if_missing
from fmgr_getter import fortinet_api_call
from fmgr_gw_networking import get_device_from_package
from fwo_log import getFwoLogger
from model_controllers.route_controller import get_matching_route_obj, get_ip_of_interface_obj
from fwo_exceptions import FwoDeviceWithoutLocalPackage
from fmgr_base import resolve_raw_objects, resolve_objects
from models.rule import Rule
from models.rulebase import Rulebase
from fwo_globals import debug_level

NETWORK_OBJECT='network_object'
rule_access_scope_v4 = ['rules_global_header_v4', 'rules_adom_v4', 'rules_global_footer_v4']
rule_access_scope_v6 = ['rules_global_header_v6', 'rules_adom_v6', 'rules_global_footer_v6']
rule_access_scope = rule_access_scope_v6 + rule_access_scope_v4
rule_nat_scope = ['rules_global_nat', 'rules_adom_nat']
rule_scope = rule_access_scope + rule_nat_scope

uid_to_name_map = {}


def normalize_rulebases (import_state, nativeConfig, native_config_global, importState, normalized_config_dict, normalized_config_global, is_global_loop_iteration):
    
    normalized_config_dict['policies'] = []

    # fill uid_to_name_map:
    for nw_obj in normalized_config_dict['network_objects']:
        uid_to_name_map[nw_obj['obj_uid']] = nw_obj['obj_name']

    fetched_rulebase_uids = []
    if normalized_config_global is not None:
        for normalized_rulebase_global in normalized_config_global.get('policies', []):
            fetched_rulebase_uids.append(normalized_rulebase_global.uid)
    for gateway in nativeConfig['gateways']:
        normalize_rulebases_for_each_link_destination(
            gateway, fetched_rulebase_uids, nativeConfig, native_config_global,
            is_global_loop_iteration, importState, normalized_config_dict,
            normalized_config_global)

    # todo: parse nat rulebase here

def normalize_rulebases_for_each_link_destination(gateway, fetched_rulebase_uids, nativeConfig, native_config_global, is_global_loop_iteration, importState, normalized_config_dict, normalized_config_global):
    logger = getFwoLogger()
    for rulebase_link in gateway['rulebase_links']:
        if rulebase_link['to_rulebase_uid'] not in fetched_rulebase_uids and rulebase_link['to_rulebase_uid'] != '':
            rulebase_to_parse, is_section, is_placeholder = find_rulebase_to_parse(
                nativeConfig['rulebases'], rulebase_link['to_rulebase_uid'])
            # search in global rulebase
            found_rulebase_in_global = False
            if rulebase_to_parse == {} and not is_global_loop_iteration and native_config_global is not None:
                rulebase_to_parse, is_section, is_placeholder = find_rulebase_to_parse(
                    native_config_global['rulebases'], rulebase_link['to_rulebase_uid']
                    )
                found_rulebase_in_global = True
            if rulebase_to_parse == {}:
                logger.warning('found to_rulebase link without rulebase in nativeConfig: ' + str(rulebase_link))
                continue
            rulebase_link['is_section'] = is_section
            normalized_rulebase = initialize_normalized_rulebase(rulebase_to_parse, importState.MgmDetails.Uid)
            parse_rulebase(rulebase_to_parse, is_section, is_placeholder, normalized_rulebase)
            fetched_rulebase_uids.append(rulebase_link['to_rulebase_uid'])

            if found_rulebase_in_global:
                normalized_config_global['policies'].append(normalized_rulebase)
            else:
                normalized_config_dict['policies'].append(normalized_rulebase)

def find_rulebase_to_parse(rulebase_list, rulebase_uid):
    """
    decide if input rulebase is true rulebase, section or placeholder
    """
    for rulebase in rulebase_list:
        if rulebase['type'].endswith(rulebase_uid):
            return rulebase, False, False
        rulebase_to_parse, is_section, is_placeholder = find_rulebase_to_parse_in_case_of_chunk(rulebase, rulebase_uid)
        if rulebase_to_parse != {}:
            return rulebase_to_parse, is_section, is_placeholder
    
    # handle case: no rulebase found
    return {}, False, False

def find_rulebase_to_parse_in_case_of_chunk(rulebase, rulebase_uid):
    return {}, False, False
                    
def initialize_normalized_rulebase(rulebase_to_parse, mgm_uid):
    rulebaseName = rulebase_to_parse['type']
    rulebaseUid = rulebase_to_parse['type']
    normalized_rulebase = Rulebase(uid=rulebaseUid, name=rulebaseName, mgm_uid=mgm_uid, Rules=[])
    return normalized_rulebase

def parse_rulebase(rulebase_to_parse, is_section, is_placeholder, normalized_rulebase):
    logger = getFwoLogger()

    rule_num = 1

    if is_section:
        for rule in rulebase_to_parse['rulebase']:
            # delete_v: kann es passieren, dass eine section über mehrere chunks geht?
            # delte_v sind import_id, parent_uid, config2import wirklich egal? Dann können wir diese argumente löschen - NAT ACHTUNG
            rule_num = parse_single_rule(rule, normalized_rulebase, normalized_rulebase.uid, None, rule_num, None, None)

        if debug_level>3:
            logger.debug("parsed rulebase " + normalized_rulebase.uid)
        return rule_num
    elif is_placeholder:
        rule_num = parse_single_rule(rulebase_to_parse, normalized_rulebase, normalized_rulebase.uid, None, rule_num, None, None)
    else:
        rule_num = parse_rulebase_chunk(rulebase_to_parse, normalized_rulebase, rule_num)                    

def parse_rulebase_chunk(rulebase_to_parse, normalized_rulebase, rule_num):
    logger = getFwoLogger()
    for rule in rulebase_to_parse['data']:
        rule_num = parse_single_rule(rule, normalized_rulebase, normalized_rulebase.uid, None, rule_num, None, None)
    return rule_num
 

def parse_single_rule(nativeRule, rulebase, layer_name, import_id, rule_num, parent_uid, config2import, debug_level=0):
    logger = getFwoLogger()
    # TODO: implement
    return 0


def initialize_rulebases(native_config):
#delete_v: hier auf native_config_domain umschreiben, die Dinger bei rulebases, bzw nat_rulebases einsortieren
    # initialize access rules
    if 'rules_global_header_v4' not in native_config:
        native_config.update({'rules_global_header_v4': {}})
    if 'rules_global_header_v6' not in native_config:
        native_config.update({'rules_global_header_v6': {}})
    if 'rules_adom_v4' not in native_config:
        native_config.update({'rules_adom_v4': {}})
    if 'rules_adom_v6' not in native_config:
        native_config.update({'rules_adom_v6': {}})
    if 'rules_global_footer_v4' not in native_config:
        native_config.update({'rules_global_footer_v4': {}})
    if 'rules_global_footer_v6' not in native_config:
        native_config.update({'rules_global_footer_v6': {}})

    # initialize nat rules
    if 'rules_global_nat' not in native_config:
        native_config.update({'rules_global_nat': {}})
    if 'rules_adom_nat' not in native_config:
        native_config.update({'rules_adom_nat': {}})
    
    # new in v8.3.1:
    # initialize hitcounts
    if 'rules_hitcount' not in native_config:
        native_config.update({'rules_hitcount': {}})


def getAccessPolicy(sid, fm_api_url, native_config_domain, adom_device_vdom_policy_package_structure, adom_name, mgm_details_device, device_config, limit):
    consolidated = '' # '/consolidated'
    logger = getFwoLogger()

    local_pkg_name = find_local_pkg(adom_device_vdom_policy_package_structure, adom_name, mgm_details_device)
    # delete_v: hier global_pkg_name später
    #global_pkg_name = device['global_rulebase_name']
    options = ['extra info', 'scope member', 'get meta']
    # pkg_name = device['package_name'] pkg_name is not used at all

    # get hitcount task number
    hitcount_payload = {
        "params": [
            {
                "data": {
                    "adom": adom_name,
                    "pkg": local_pkg_name
                }
            }
        ]
    }
    hitcount_task = fmgr_getter.fortinet_api_call(
        sid, fm_api_url, "/sys/hitcount", payload=hitcount_payload, method="get")
    time.sleep(2)

    # delete_v: hier initial link wenn global header existiert
    # get global header rulebase:
    # if device['global_rulebase_name'] is None or device['global_rulebase_name'] == '':
    #     logger.debug('no global rulebase name defined in fortimanager, ADOM=' + adom_name + ', local_package=' + local_pkg_name)
    # else:
    #     fmgr_getter.update_config_with_fortinet_api_call(
    #         nativeConfig['rules_global_header_v4'], sid, fm_api_url, "/pm/config/global/pkg/" + global_pkg_name + "/global/header" + consolidated + "/policy", local_pkg_name, limit=limit)
    #     fmgr_getter.update_config_with_fortinet_api_call(
    #         nativeConfig['rules_global_header_v6'], sid, fm_api_url, "/pm/config/global/pkg/" + global_pkg_name + "/global/header" + consolidated + "/policy6", local_pkg_name, limit=limit)
    
    # delete_v: hier initial link wenn global header nicht existiert, sonst link von global header
    is_global = False
    link_initial_rulebase(device_config, local_pkg_name, is_global)
    # get local rulebase
    fmgr_getter.update_config_with_fortinet_api_call(
        native_config_domain['rulebases'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/pkg/" + local_pkg_name + "/firewall" + consolidated + "/policy", 'rules_adom_v4_' + local_pkg_name, options=options, limit=limit)
    fmgr_getter.update_config_with_fortinet_api_call(
        native_config_domain['rulebases'], sid, fm_api_url, "/pm/config/adom/" + adom_name + "/pkg/" + local_pkg_name + "/firewall" + consolidated + "/policy6", 'rules_adom_v6_' + local_pkg_name, limit=limit)

    # get global footer rulebase:
    # if device['global_rulebase_name'] != None and device['global_rulebase_name'] != '':
    #     fmgr_getter.update_config_with_fortinet_api_call(
    #         nativeConfig['rules_global_footer_v4'], sid, fm_api_url, "/pm/config/global/pkg/" + global_pkg_name + "/global/footer" + consolidated + "/policy", local_pkg_name, limit=limit)
    #     fmgr_getter.update_config_with_fortinet_api_call(
    #         nativeConfig['rules_global_footer_v6'], sid, fm_api_url, "/pm/config/global/pkg/" + global_pkg_name + "/global/footer" + consolidated + "/policy6", local_pkg_name, limit=limit)

    # execute hitcount task
    hitcount_payload = {
        "params": [
            {
                "data": {
                    "taskid": hitcount_task[0]['task']
                }
            }
        ]
    }
    fmgr_getter.update_config_with_fortinet_api_call(
        native_config_domain['rules_hitcount'], sid, fm_api_url, "/sys/task/result", local_pkg_name, payload=hitcount_payload, limit=limit)

def find_local_pkg(adom_device_vdom_policy_package_structure, adom_name, mgm_details_device):
    for device in adom_device_vdom_policy_package_structure[adom_name]:
        for vdom in adom_device_vdom_policy_package_structure[adom_name][device]:
            if mgm_details_device['name'] == device + '_' + vdom:
                return adom_device_vdom_policy_package_structure[adom_name][device][vdom]
    raise FwoDeviceWithoutLocalPackage('Could not find local package for ' + mgm_details_device['name'] + ' in Fortimanager Config') from None

def link_initial_rulebase(device_config, local_pkg_name, is_global):
    device_config['rulebase_links'].append({
        'from_rulebase_uid': '',
        'from_rule_uid': '',
        'to_rulebase_uid': local_pkg_name,
        'type': 'concatenated',
        'is_global': is_global,
        'is_initial': True,
        'is_section': False
    })

def getNatPolicy(sid, fm_api_url, nativeConfig, adom_name, device, limit):
    scope = 'global'
    pkg = device['global_rulebase_name']
    if pkg is not None and pkg != '':   # only read global rulebase if it exists
        for nat_type in ['central/dnat', 'central/dnat6', 'firewall/central-snat-map']:
            fmgr_getter.update_config_with_fortinet_api_call(
                nativeConfig['rules_global_nat'], sid, fm_api_url, "/pm/config/" + scope + "/pkg/" + pkg + '/' + nat_type, device['local_rulebase_name'], limit=limit)

    scope = 'adom/'+adom_name
    pkg = device['local_rulebase_name']
    for nat_type in ['central/dnat', 'central/dnat6', 'firewall/central-snat-map']:
        fmgr_getter.update_config_with_fortinet_api_call(
            nativeConfig['rules_adom_nat'], sid, fm_api_url, "/pm/config/" + scope + "/pkg/" + pkg + '/' + nat_type, device['local_rulebase_name'], limit=limit)


# delete_v: versuch das von cp_rule zu kopieren
#def normalizeRulebases (nativeConfig, importState, normalizedConfig):
def normalize_access_rules(native_config, native_config_global, import_state, normalized_config_dict, normalized_config_global, is_global_loop_iteration):
    logger = getFwoLogger()
    rules = []
    first_v4 = True
    first_v6 = True
    nat_rule_number = 0
    rule_number = 0
    src_ref_all = ""
    dst_ref_all = ""
    for rule_table in rule_access_scope:
        #src_ref_all = resolve_raw_objects("all", list_delimiter, native_config, 'name', 'uuid', rule_type=rule_table, jwt=jwt, import_id=import_id, mgm_id=mgm_details['id'])
        #dst_ref_all = resolve_raw_objects("all", list_delimiter, native_config, 'name', 'uuid', rule_type=rule_table, jwt=jwt, import_id=import_id, mgm_id=mgm_details['id'])
        for localPkgName in native_config[rule_table]:
                rule_number, first_v4, first_v6 = insert_headers(rule_table, first_v6, first_v4, native_config, rules, import_id, localPkgName,src_ref_all,dst_ref_all,rule_number)

                for rule_orig in native_config[rule_table][localPkgName]:

                    normalize_rule(rule_orig, rules, native_config, rule_table, localPkgName, rule_number, src_ref_all, dst_ref_all, normalized_config_dict)

    normalized_config_dict.update({'rules': rules})

def normalize_rule(rule_orig, rules, native_config, rule_table, localPkgName, rule_number, src_ref_all, dst_ref_all, normalized_config_dict):
    rule = {'rule_src': '', 'rule_dst': '', 'rule_svc': ''}
    xlate_rule = None
    # rule.update({ 'control_id': import_id})
    rule.update({ 'rulebase_name': localPkgName})    # the rulebase_name will be set to the pkg_name as there is no rulebase_name in FortiMangaer
    rule.update({ 'rule_ruleid': rule_orig['policyid']})
    rule.update({ 'rule_uid': rule_orig['uuid']})
    rule.update({ 'rule_num': rule_number})
    rule.update({ 'rule_name': rule_orig.get('name', None)})
    
    parse_target_gateways(rule_orig, rule, localPkgName)

    rule.update({ 'rule_implied': False })
    rule.update({ 'rule_time': None })
    rule.update({ 'rule_type': 'access' })
    rule.update({ 'parent_rule_id': None })

    rule.update({ 'rule_comment': rule_orig.get('comments', None)})
    if rule_orig['action']==0:
        rule.update({ 'rule_action': 'Drop' })
    else:
        rule.update({ 'rule_action': 'Accept' })
    if 'status' in rule_orig and (rule_orig['status']=='enable' or rule_orig['status']==1):
        rule.update({ 'rule_disabled': False })
    else:
        rule.update({ 'rule_disabled': True })
    if rule_orig['logtraffic'] == 'disable':
        rule.update({ 'rule_track': 'None'})
    else:
        rule.update({ 'rule_track': 'Log'})

    rule['rule_src'] = extend_string_list(rule['rule_src'], rule_orig, 'srcaddr', list_delimiter)
    rule['rule_dst'] = extend_string_list(rule['rule_dst'], rule_orig, 'dstaddr', list_delimiter)
    rule['rule_svc'] = extend_string_list(rule['rule_svc'], rule_orig, 'service', list_delimiter)
    rule['rule_src'] = extend_string_list(rule['rule_src'], rule_orig, 'srcaddr6', list_delimiter)
    rule['rule_dst'] = extend_string_list(rule['rule_dst'], rule_orig, 'dstaddr6', list_delimiter)
    rule['rule_src'] = extend_string_list(rule['rule_src'], rule_orig, 'internet-service-src-name', list_delimiter)

    if len(rule_orig['srcintf'])>0:
        src_obj_zone = add_zone_if_missing (normalized_config_dict, rule_orig['srcintf'][0])
        rule.update({ 'rule_from_zone': src_obj_zone }) # todo: currently only using the first zone
    if len(rule_orig['dstintf'])>0:
        dst_obj_zone = add_zone_if_missing (normalized_config_dict, rule_orig['dstintf'][0])
        rule.update({ 'rule_to_zone': dst_obj_zone }) # todo: currently only using the first zone

    if 'srcaddr-negate' in rule_orig:
        rule.update({ 'rule_src_neg': rule_orig['srcaddr-negate']=='disable'})
    elif 'internet-service-src-negate' in rule_orig:
        rule.update({ 'rule_src_neg': rule_orig['internet-service-src-negate']=='disable'})
    if 'dstaddr-negate' in rule_orig:
        rule.update({ 'rule_dst_neg': rule_orig['dstaddr-negate']=='disable'})
    if 'service-negate' in rule_orig:
        rule.update({ 'rule_svc_neg': rule_orig['service-negate']=='disable'})

    rule.update({ 'rule_src_refs': resolve_raw_objects(rule['rule_src'], list_delimiter, native_config, 'name', 'uuid', \
        rule_type=rule_table, jwt=None, import_id=None, rule_uid=rule_orig['uuid'], object_type=NETWORK_OBJECT, mgm_id=mgm_details['id']) })
    rule.update({ 'rule_dst_refs': resolve_raw_objects(rule['rule_dst'], list_delimiter, native_config, 'name', 'uuid', \
        rule_type=rule_table, jwt=None, import_id=None, rule_uid=rule_orig['uuid'], object_type=NETWORK_OBJECT, mgm_id=mgm_details['id']) })
    rule.update({ 'rule_svc_refs': rule['rule_svc'] }) # services do not have uids, so using name instead
    add_users_to_rule(rule_orig, rule)

    # new in v8.0.3:
    rule.update({ 'rule_custom_fields': rule_orig.get('meta fields', None) })
    rule.update({ 'rule_last_change_admin': rule_orig.get('_last-modified-by',None) })

    update_hit_counters(native_config, rule_table, rule_orig, rule, localPkgName, rule_access_scope_v4, rule_access_scope_v6)

    xlate_rule = handle_combined_nat_rule(rule, rule_orig, normalized_config_dict, nat_rule_number, import_id, localPkgName, dev_id)
    rules.append(rule)
    if xlate_rule is not None:
        rules.append(xlate_rule)
    rule_number += 1    # nat rules have their own numbering


def parse_target_gateways(rule_orig, rule, localPkgName):
    if 'scope member' in rule_orig:
        installon_target = []
        for vdom in rule_orig['scope member']:
            installon_target.append(vdom['name'] + '_' + vdom['vdom'])
        rule.update({ 'rule_installon': '|'.join(installon_target)})
    else:
        rule.update({ 'rule_installon': localPkgName })


def update_hit_counters(native_config, rule_table, rule_orig, rule, localPkgName, rule_access_scope_v4, rule_access_scope_v6):
    if rule_table in rule_access_scope_v4 and len(native_config['rules_hitcount'][localPkgName])>0:
        for hitcount_config in native_config['rules_hitcount'][localPkgName][0]['firewall policy']:
            if rule_orig['policyid'] == hitcount_config['last_hit']:
                rule.update({ 'last_hit': time.strftime("%Y-%m-%dT%H:%M:%S%z", time.localtime(hitcount_config['policyid']))})
    elif rule_table in rule_access_scope_v6 and len(native_config['rules_hitcount'][localPkgName])>0:
        for hitcount_config in native_config['rules_hitcount'][localPkgName][0]['firewall policy6']:
            if rule_orig['policyid'] == hitcount_config['last_hit']:
                rule.update({ 'last_hit': time.strftime("%Y-%m-%dT%H:%M:%S%z", time.localtime(hitcount_config['policyid']))})
    else:
        rule.update({ 'last_hit': None})

# pure nat rules 
def normalize_nat_rules(full_config, config2import, import_id, jwt=None):
    nat_rules = []
    rule_number = 0

    for rule_table in rule_nat_scope:
        for localPkgName in full_config['rules_global_nat']:
            for rule_orig in full_config[rule_table][localPkgName]:
                rule = {'rule_src': '', 'rule_dst': '', 'rule_svc': ''}
                if rule_orig['nat'] == 1:   # assuming source nat
                    rule.update({ 'control_id': import_id})
                    rule.update({ 'rulebase_name': localPkgName})    # the rulebase_name just has to be a unique string among devices
                    rule.update({ 'rule_ruleid': rule_orig['policyid']})
                    rule.update({ 'rule_uid': rule_orig['uuid']})
                    # rule.update({ 'rule_num': rule_orig['obj seq']})
                    rule.update({ 'rule_num': rule_number })
                    if 'comments' in rule_orig:
                        rule.update({ 'rule_comment': rule_orig['comments']})
                    rule.update({ 'rule_action': 'Drop' })  # not used for nat rules
                    rule.update({ 'rule_track': 'None'}) # not used for nat rules

                    rule['rule_src'] = extend_string_list(rule['rule_src'], rule_orig, 'orig-addr', list_delimiter, jwt=jwt, import_id=import_id)
                    rule['rule_dst'] = extend_string_list(rule['rule_dst'], rule_orig, 'dst-addr', list_delimiter, jwt=jwt, import_id=import_id)
                    
                    if rule_orig['protocol']==17:
                        svc_name = 'udp_' + str(rule_orig['orig-port'])
                    elif rule_orig['protocol']==6:
                        svc_name = 'tcp_' + str(rule_orig['orig-port'])
                    else:
                        svc_name = 'svc_' + str(rule_orig['orig-port'])
                    # need to create a helper service object and add it to the nat rule, also needs to be added to service list

                    if not 'service_objects' in config2import: # is normally defined
                        config2import['service_objects'] = []
                    config2import['service_objects'].append(create_svc_object( \
                        import_id=import_id, name=svc_name, proto=rule_orig['protocol'], port=rule_orig['orig-port'], comment='service created by FWO importer for NAT purposes'))
                    rule['rule_svc'] = svc_name

                    #rule['rule_src'] = extend_string_list(rule['rule_src'], rule_orig, 'srcaddr6', list_delimiter, jwt=jwt, import_id=import_id)
                    #rule['rule_dst'] = extend_string_list(rule['rule_dst'], rule_orig, 'dstaddr6', list_delimiter, jwt=jwt, import_id=import_id)

                    if len(rule_orig['srcintf'])>0:
                        rule.update({ 'rule_from_zone': rule_orig['srcintf'][0] }) # todo: currently only using the first zone
                    if len(rule_orig['dstintf'])>0:
                        rule.update({ 'rule_to_zone': rule_orig['dstintf'][0] }) # todo: currently only using the first zone

                    rule.update({ 'rule_src_neg': False})
                    rule.update({ 'rule_dst_neg': False})
                    rule.update({ 'rule_svc_neg': False})
                    rule.update({ 'rule_src_refs': resolve_raw_objects(rule['rule_src'], list_delimiter, full_config, 'name', 'uuid', rule_type=rule_table) }, \
                        jwt=jwt, import_id=import_id, rule_uid=rule_orig['uuid'], object_type=NETWORK_OBJECT)
                    rule.update({ 'rule_dst_refs': resolve_raw_objects(rule['rule_dst'], list_delimiter, full_config, 'name', 'uuid', rule_type=rule_table) }, \
                        jwt=jwt, import_id=import_id, rule_uid=rule_orig['uuid'], object_type=NETWORK_OBJECT)
                    # services do not have uids, so using name instead
                    rule.update({ 'rule_svc_refs': rule['rule_svc'] })
                    rule.update({ 'rule_type': 'original' })
                    rule.update({ 'rule_installon': localPkgName })
                    if 'status' in rule_orig and (rule_orig['status']=='enable' or rule_orig['status']==1):
                        rule.update({ 'rule_disabled': False })
                    else:
                        rule.update({ 'rule_disabled': True })
                    rule.update({ 'rule_implied': False })
                    rule.update({ 'rule_time': None })
                    rule.update({ 'parent_rule_id': None })

                    nat_rules.append(rule)
                    add_users_to_rule(rule_orig, rule)

                    ############## now adding the xlate rule part ##########################
                    xlate_rule = dict(rule) # copy the original (match) rule
                    xlate_rule.update({'rule_src': '', 'rule_dst': '', 'rule_svc': ''})
                    xlate_rule['rule_src'] = extend_string_list(xlate_rule['rule_src'], rule_orig, 'orig-addr', list_delimiter, jwt=jwt, import_id=import_id)
                    xlate_rule['rule_dst'] = 'Original'
                    
                    if rule_orig['protocol']==17:
                        svc_name = 'udp_' + str(rule_orig['nat-port'])
                    elif rule_orig['protocol']==6:
                        svc_name = 'tcp_' + str(rule_orig['nat-port'])
                    else:
                        svc_name = 'svc_' + str(rule_orig['nat-port'])
                    # need to create a helper service object and add it to the nat rule, also needs to be added to service list!
                    # fmgr_service.create_svc_object(name=svc_name, proto=rule_orig['protocol'], port=rule_orig['orig-port'], comment='service created by FWO importer for NAT purposes')
                    config2import['service_objects'].append(create_svc_object(import_id=import_id, name=svc_name, proto=rule_orig['protocol'], port=rule_orig['nat-port'], comment='service created by FWO importer for NAT purposes'))
                    xlate_rule['rule_svc'] = svc_name

                    xlate_rule.update({ 'rule_src_refs': resolve_objects(xlate_rule['rule_src'], list_delimiter, full_config, 'name', 'uuid', rule_type=rule_table, jwt=jwt, import_id=import_id ) })
                    xlate_rule.update({ 'rule_dst_refs': resolve_objects(xlate_rule['rule_dst'], list_delimiter, full_config, 'name', 'uuid', rule_type=rule_table, jwt=jwt, import_id=import_id ) })
                    xlate_rule.update({ 'rule_svc_refs': xlate_rule['rule_svc'] })  # services do not have uids, so using name instead

                    xlate_rule.update({ 'rule_type': 'xlate' })

                    nat_rules.append(xlate_rule)
                    rule_number += 1
    config2import['rules'].extend(nat_rules)


def insert_header(rules, import_id, header_text, rulebase_name, rule_uid, rule_number, src_refs, dst_refs):
    rule = {
        "control_id": import_id,
        "rule_head_text": header_text,
        "rulebase_name": rulebase_name,
        "rule_ruleid": None,
        "rule_uid":  rule_uid + rulebase_name,
        "rule_num": rule_number,
        "rule_disabled": False,
        "rule_src": "all",
        "rule_dst": "all", 
        "rule_svc": "ALL",
        "rule_src_neg": False,
        "rule_dst_neg": False,
        "rule_svc_neg": False,
        "rule_src_refs": src_refs,
        "rule_dst_refs": dst_refs,
        "rule_svc_refs": "ALL",
        "rule_action": "Accept",
        "rule_track": "None",
        "rule_installon": None,
        "rule_time": None,
        "rule_type": "access",
        "parent_rule_id": None,
        "rule_implied": False,
        "rule_comment": None
    }
    rules.append(rule)


def create_xlate_rule(rule):
    xlate_rule = copy.deepcopy(rule)
    rule['rule_type'] = 'combined'
    xlate_rule['rule_type'] = 'xlate'
    xlate_rule['rule_comment'] = None
    xlate_rule['rule_disabled'] = False
    xlate_rule['rule_src'] = 'Original'
    xlate_rule['rule_src_refs'] = 'Original'
    xlate_rule['rule_dst'] = 'Original'
    xlate_rule['rule_dst_refs'] = 'Original'
    xlate_rule['rule_svc'] = 'Original'
    xlate_rule['rule_svc_refs'] = 'Original'
    return xlate_rule


def handle_combined_nat_rule(rule, rule_orig, config2import, nat_rule_number, import_id, localPkgName, dev_id):
    # now dealing with VIPs (dst NAT part) of combined rules
    logger = getFwoLogger()
    xlate_rule = None

    # dealing with src NAT part of combined rules
    if "nat" in rule_orig and rule_orig["nat"]==1:
        logger.debug("found mixed Access/NAT rule no. " + str(nat_rule_number))
        nat_rule_number += 1
        xlate_rule = create_xlate_rule(rule)
        if 'ippool' in rule_orig:
            if rule_orig['ippool']==0:  # hiding behind outbound interface
                interface_name = 'unknownIF'
                destination_interface_ip = '0.0.0.0'
                destination_ip = get_first_ip_of_destination(rule['rule_dst_refs'], config2import) # get an ip of destination
                hideInterface = 'undefined_interface'
                if destination_ip is None:
                    logger.warning('src nat behind interface: found no valid destination ip in rule with UID ' + rule['rule_uid'])
                else:
                    # matching_route = get_matching_route_obj(destination_ip, config2import['networking'][device_name]['routingv4'])
                    matching_route = get_matching_route_obj(destination_ip, config2import['routing'], dev_id)
                    if matching_route is None:
                        logger.warning('src nat behind interface: found no matching route in rule with UID '
                            + rule['rule_uid'] + ', dest_ip: ' + destination_ip)
                    else:
                        destination_interface_ip = get_ip_of_interface_obj(matching_route.interface, dev_id, config2import['interfaces'])
                        interface_name = matching_route.interface
                        hideInterface=interface_name
                        if hideInterface is None:
                            logger.warning('src nat behind interface: found route with undefined interface ' + str(jsonpickle.dumps(matching_route, unpicklable=True)))
                        if destination_interface_ip is None:
                            logger.warning('src nat behind interface: found no matching interface IP in rule with UID '
                            + rule['rule_uid'] + ', dest_ip: ' + destination_ip)
        
                # add dummy object "outbound-interface"
                if hideInterface is not None:
                    obj_name = 'hide_IF_ip_' + str(hideInterface) + '_' + str(destination_interface_ip)
                    obj_comment = 'FWO auto-generated dummy object for source nat'
                    if destination_interface_ip is not None and type(ipaddress.ip_address(str(destination_interface_ip))) is ipaddress.IPv6Address:
                        HideNatIp = str(destination_interface_ip) + '/128'
                    elif destination_interface_ip is not None and type(ipaddress.ip_address(str(destination_interface_ip))) is ipaddress.IPv4Address:
                        HideNatIp = str(destination_interface_ip) + '/32'
                    else:
                        HideNatIp = dummy_ip
                        logger.warning('found invalid HideNatIP ' + str(destination_interface_ip))
                    obj = create_network_object(obj_name, 'host', HideNatIp, HideNatIp, obj_name, 'black', obj_comment, 'global')
                    if obj not in config2import['network_objects']:
                        config2import['network_objects'].append(obj)
                    xlate_rule['rule_src'] = obj_name
                    xlate_rule['rule_src_refs'] = obj_name

            elif rule_orig['ippool']==1: # hiding behind one ip of an ip pool
                poolNameArray = rule_orig['poolname']
                if len(poolNameArray)>0:
                    if len(poolNameArray)>1:
                        logger.warning("found more than one ippool - ignoring all but first pool")
                    poolName = poolNameArray[0]
                    xlate_rule['rule_src'] = poolName
                    xlate_rule['rule_src_refs'] =  poolName
                else:
                    logger.warning("found ippool rule without ippool: " + rule['rule_uid'])
            else:
                logger.warning("found ippool rule with unexpected ippool value: " + rule_orig['ippool'])
        
        if 'natip' in rule_orig and rule_orig['natip']!=["0.0.0.0","0.0.0.0"]:
            logger.warning("found explicit natip rule - ignoring for now: " + rule['rule_uid'])
            # need example for interpretation of config

    # todo: find out how match-vip=1 influences natting (only set in a few vip-nat rules)
    # if "match-vip" in rule_orig and rule_orig["match-vip"]==1:
    #     logger.warning("found VIP destination Access/NAT rule (but not parsing yet); no. " + str(vip_nat_rule_number))
    #     vip_nat_rule_number += 1

    # deal with vip natting: check for each (dst) nw obj if it contains "obj_nat_ip"
    rule_dst_list = rule['rule_dst'].split(list_delimiter)
    nat_object_list = extract_nat_objects(rule_dst_list, config2import['network_objects'])

    if len(nat_object_list)>0:
        if xlate_rule is None: # no source nat, so we create the necessary nat rule here
            xlate_rule = create_xlate_rule(rule)
        xlate_dst = []
        xlate_dst_refs = []
        for nat_obj in nat_object_list:
            if 'obj_ip_end' in nat_obj: # this nat obj is a range - include the end ip in name and uid as well to avoid akey conflicts
                xlate_dst.append(nat_obj['obj_nat_ip'] + '-' + nat_obj['obj_ip_end'] + nat_postfix)
                nat_ref = nat_obj['obj_nat_ip']
                if 'obj_nat_ip_end' in nat_obj:
                    nat_ref += '-' + nat_obj['obj_nat_ip_end'] + nat_postfix
                xlate_dst_refs.append(nat_ref)
            else:
                xlate_dst.append(nat_obj['obj_nat_ip'] + nat_postfix)
                xlate_dst_refs.append(nat_obj['obj_nat_ip']  + nat_postfix)
        xlate_rule['rule_dst'] = list_delimiter.join(xlate_dst)
        xlate_rule['rule_dst_refs'] = list_delimiter.join(xlate_dst_refs)
    # else: (no nat object found) no dnatting involved, dst stays "Original"

    return xlate_rule


def insert_headers(rule_table, first_v6, first_v4, full_config, rules, import_id, localPkgName,src_ref_all,dst_ref_all,rule_number):
    if rule_table in rule_access_scope_v6 and first_v6:
        insert_header(rules, import_id, "IPv6 rules", localPkgName, "IPv6HeaderText", rule_number, src_ref_all, dst_ref_all)
        rule_number += 1
        first_v6 = False
    elif rule_table in rule_access_scope_v4 and first_v4:
        insert_header(rules, import_id, "IPv4 rules", localPkgName, "IPv4HeaderText", rule_number, src_ref_all, dst_ref_all)
        rule_number += 1
        first_v4 = False
    if rule_table == 'rules_adom_v4' and len(full_config['rules_adom_v4'][localPkgName])>0:
        insert_header(rules, import_id, "Adom Rules IPv4", localPkgName, "IPv4AdomRules", rule_number, src_ref_all, dst_ref_all)
        rule_number += 1
    elif rule_table == 'rules_adom_v6' and len(full_config['rules_adom_v6'][localPkgName])>0:
        insert_header(rules, import_id, "Adom Rules IPv6", localPkgName, "IPv6AdomRules", rule_number, src_ref_all, dst_ref_all)
        rule_number += 1
    elif rule_table == 'rules_global_header_v4' and len(full_config['rules_global_header_v4'][localPkgName])>0:
        insert_header(rules, import_id, "Global Header Rules IPv4", localPkgName, "IPv4GlobalHeaderRules", rule_number, src_ref_all, dst_ref_all)
        rule_number += 1
    elif rule_table == 'rules_global_header_v6' and len(full_config['rules_global_header_v6'][localPkgName])>0:
        insert_header(rules, import_id, "Global Header Rules IPv6", localPkgName, "IPv6GlobalHeaderRules", rule_number, src_ref_all, dst_ref_all)
        rule_number += 1
    elif rule_table == 'rules_global_footer_v4' and len(full_config['rules_global_footer_v4'][localPkgName])>0:
        insert_header(rules, import_id, "Global Footer Rules IPv4", localPkgName, "IPv4GlobalFooterRules", rule_number, src_ref_all, dst_ref_all)
        rule_number += 1
    elif rule_table == 'rules_global_footer_v6' and len(full_config['rules_global_footer_v6'][localPkgName])>0:
        insert_header(rules, import_id, "Global Footer Rules IPv6", localPkgName, "IPv6GlobalFooterRules", rule_number, src_ref_all, dst_ref_all)
        rule_number += 1
    return rule_number, first_v4, first_v6


def extract_nat_objects(nwobj_list, all_nwobjects):
    nat_obj_list = []
    for obj in nwobj_list:
        for obj2 in all_nwobjects:
            if obj2['obj_name']==obj:
                if 'obj_nat_ip' in obj2:
                    nat_obj_list.append(obj2)
                break
        # if obj in all_nwobjects and 'obj_nat_ip' in all_nwobjects[obj]:
        #     nat_obj_list.append(obj)
    return nat_obj_list


def add_users_to_rule(rule_orig, rule):
    if 'groups' in rule_orig:
        add_users(rule_orig['groups'], rule)
    if 'users' in rule_orig:
        add_users(rule_orig['users'], rule)


def add_users(users, rule):
    for user in users:
        rule_src_with_users = []
        for src in rule['rule_src'].split(list_delimiter):
            rule_src_with_users.append(user + '@' + src)
        rule['rule_src'] = list_delimiter.join(rule_src_with_users)

        # here user ref is the user name itself
        rule_src_refs_with_users = []
        for src in rule['rule_src_refs'].split(list_delimiter):
            rule_src_refs_with_users.append(user + '@' + src)
        rule['rule_src_refs'] = list_delimiter.join(rule_src_refs_with_users)

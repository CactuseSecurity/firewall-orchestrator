import copy
from typing import Any
import jsonpickle # type: ignore
from fwo_const import LIST_DELIMITER, NAT_POSTFIX, DUMMY_IP
from fwo_base import extend_string_list
from fOS_service import create_svc_object
from fOS_network import create_network_object, get_first_ip_of_destination
import fOS_zone, fOS_getter
#from fOS_gw_networking import get_device_from_package
from fwo_log import FWOLogger
from model_controllers.interface_controller import get_ip_of_interface_obj # type: ignore #TYPING: Importing twice???
from model_controllers.route_controller import get_matching_route_obj, get_ip_of_interface_obj
from models.management import Management
import ipaddress
from fOS_common import resolve_objects
import time


rule_access_scope_v4 = ['rules']
rule_access_scope_v6 = []

rule_access_scope = ['rules']
rule_nat_scope = ['rules_nat']
rule_scope = rule_access_scope + rule_nat_scope


def initialize_rulebases(raw_config: dict[str, Any]):
    for scope in rule_scope:
        if scope not in raw_config:
            raw_config.update({scope: {}})    


def get_access_policy(sid: str, fm_api_url: str, raw_config: dict[str, Any], limit: int):
    fOS_getter.update_config_with_fortios_api_call(raw_config['rules'], fm_api_url + "/cmdb/firewall/policy" + "?access_token=" + sid, 'rules', limit=limit)
    if 'rules' not in raw_config or 'rules' not in raw_config['rules']:
        FWOLogger.warning('did not receive any access rules via API')


def get_nat_policy(sid: str, fm_api_url: str, raw_config: dict[str, Any], adom_name: str, device: dict[str, Any], limit: int):
    scope = 'global'
    pkg = device['global_rulebase_name']
    if pkg is not None and pkg != '':   # only read global rulebase if it exists
        for nat_type in ['central/dnat', 'central/dnat6', 'firewall/central-snat-map']:
            fOS_getter.update_config_with_fortinet_api_call( #type: ignore #TYPING: I don`t know how this happens
                raw_config['rules_global_nat'], sid, fm_api_url, "/pm/config/" + scope + "/pkg/" + pkg + '/' + nat_type, device['local_rulebase_name'], limit=limit)

    scope = 'adom/'+adom_name
    pkg = device['local_rulebase_name']
    for nat_type in ['central/dnat', 'central/dnat6', 'firewall/central-snat-map']:
        fOS_getter.update_config_with_fortinet_api_call( #type: ignore #TYPING: I don`t know how this happens
            raw_config['rules_adom_nat'], sid, fm_api_url, "/pm/config/" + scope + "/pkg/" + pkg + '/' + nat_type, device['local_rulebase_name'], limit=limit)


def normalize_access_rules(full_config: dict[str, Any], config2import: dict[str, Any], import_id: int, mgm_details: Management, jwt: str | None = None):
    rules: list[dict[str, Any]] = []
    rule_number = 0

    if 'rules' not in full_config or 'rules' not in full_config['rules']:
        FWOLogger.warning('did not find any access rules')
        config2import.update({'rules': rules})
        return

    for rule_orig in full_config['rules']['rules']:
        rule = build_base_rule(rule_orig, import_id, mgm_details, rule_number)
        enrich_rule_with_action_and_status(rule, rule_orig)
        enrich_rule_with_hitcount(rule, rule_orig)
        enrich_rule_with_addresses(rule, rule_orig, config2import, import_id)
        enrich_rule_with_zones(rule, rule_orig, config2import, import_id)
        enrich_rule_with_negation(rule, rule_orig)
        enrich_rule_with_refs(rule, full_config['nw_obj_lookup_dict'], jwt)
        add_users_to_rule(rule_orig, rule)
        rules.append(rule)
        rule_number += 1

    config2import.update({'rules': rules})

def build_base_rule(rule_orig: dict[str, Any], import_id: int, mgm_details: Management, rule_number: int) -> dict[str, Any]:
    rule: dict[str, Any] = {
        'rule_src': '',
        'rule_dst': '',
        'rule_svc': '',
        'control_id': import_id,
        'rulebase_name': 'access_rules',
        'rule_ruleid': rule_orig['policyid'],
        'rule_uid': rule_orig['uuid'],
        'rule_num': rule_number,
        'rule_name': rule_orig.get('name'),
        'rule_installon': mgm_details.Devices[0]['name'] if mgm_details.Devices else None,
        'rule_implied': False,
        'rule_time': None,
        'rule_type': 'access',
        'parent_rule_id': None,
        'rule_comment': rule_orig.get('comments', None)
    }
    return rule

def enrich_rule_with_action_and_status(rule: dict[str, Any], rule_orig: dict[str, Any]):
    rule['rule_action'] = 'Drop' if rule_orig['action'] == 'deny' else 'Accept'
    rule['rule_disabled'] = not (rule_orig.get('status') == 'enable' or rule_orig.get('status') == 1)
    rule['rule_track'] = 'None' if rule_orig.get('logtraffic') == 'disable' else 'Log'

def enrich_rule_with_hitcount(rule: dict[str, Any], rule_orig: dict[str, Any]):
    hit = rule_orig.get('_last_hit', 0)
    rule['last_hit'] = None if hit == 0 else time.strftime("%Y-%m-%d", time.localtime(hit))

def enrich_rule_with_addresses(rule: dict[str, Any], rule_orig: dict[str, Any], config2import: dict[str, Any], import_id: int):
    rule['rule_src'] = join_names(rule_orig.get('srcaddr', []))
    rule['rule_dst'] = join_names(rule_orig.get('dstaddr', []))
    rule['rule_svc'] = join_names(rule_orig.get('service', []))

    if rule_orig.get('internet-service-src-name'):
        rule['rule_src'] = join_names(rule_orig['internet-service-src-name'])
        set_service_field_internet_service(rule, config2import, import_id)

    if rule_orig.get('internet-service-name'):
        rule['rule_dst'] = join_names(rule_orig['internet-service-name'])
        set_service_field_internet_service(rule, config2import, import_id)

    append_ipv6(rule, rule_orig)

def append_ipv6(rule: dict[str, Any], rule_orig: dict[str, Any]):
    rule_src_v6 = [d['name'] for d in rule_orig.get('srcaddr6', [])]
    rule_dst_v6 = [d['name'] for d in rule_orig.get('dstaddr6', [])]
    if rule_src_v6:
        rule['rule_src'] = LIST_DELIMITER.join(rule['rule_src'].split(LIST_DELIMITER) + rule_src_v6)
    if rule_dst_v6:
        rule['rule_dst'] = LIST_DELIMITER.join(rule['rule_dst'].split(LIST_DELIMITER) + rule_dst_v6)

def enrich_rule_with_zones(rule: dict[str, Any], rule_orig: dict[str, Any], config2import: dict[str, Any], import_id: int):
    if rule_orig.get('srcintf'):
        rule['rule_from_zone'] = fOS_zone.add_zone_if_missing(config2import, rule_orig['srcintf'][0]['name'], import_id)
    if rule_orig.get('dstintf'):
        rule['rule_to_zone'] = fOS_zone.add_zone_if_missing(config2import, rule_orig['dstintf'][0]['name'], import_id)

def enrich_rule_with_negation(rule: dict[str, Any], rule_orig: dict[str, Any]):
    rule['rule_src_neg'] = rule_orig.get('srcaddr-negate') != 'disable'
    rule['rule_dst_neg'] = rule_orig.get('dstaddr-negate') != 'disable'
    rule['rule_svc_neg'] = rule_orig.get('service-negate') != 'disable'

def enrich_rule_with_refs(rule: dict[str, Any], lookup_dict: dict[str, Any], jwt: str | None = None):
    rule['rule_src_refs'] = join_refs(rule['rule_src'], lookup_dict, jwt)
    rule['rule_dst_refs'] = join_refs(rule['rule_dst'], lookup_dict, jwt)
    rule['rule_svc_refs'] = rule['rule_svc']  # For services, name == uid

def join_names(entries: list[dict[str, Any]]) -> str:
    return LIST_DELIMITER.join([d['name'] for d in entries])

def join_refs(entry_str: str, lookup_dict: dict[str, Any], jwt: str | None = None) -> str:
    return LIST_DELIMITER.join(resolve_objects(name, lookup_dict=lookup_dict, jwt=jwt) for name in entry_str.split(LIST_DELIMITER))


def set_service_field_internet_service(rule: dict[str, Any], config2import: dict[str, Any], import_id: int):
    # check if dummy service "Internet Service" already exists and create if not
    found_internet_service_obj = next((item for item in config2import['service_objects'] if item["svc_name"] == "Internet Service"), None)
    if found_internet_service_obj is None:
        config2import['service_objects'].append({
                'svc_name': 'Internet Service', 'svc_typ': 'group', 'svc_uid': 'Internet Service', 'control_id': import_id
            })

    # set service to "Internet Service"
    rule['rule_svc'] = 'Internet Service'
    rule['rule_svc_refs'] = 'Internet Service'


# pure nat rules 
def normalize_nat_rules(full_config: dict[str, Any], config2import: dict[str, list[Any]], import_id: int, jwt: str | None = None):
    nat_rules: list[dict[str, Any]] = []
    rule_number: int = 0

    for rule_table in rule_nat_scope:
        for localPkgName in full_config['rules_global_nat']:
            for rule_orig in full_config[rule_table][localPkgName]:
                rule: dict[str, Any] = {'rule_src': '', 'rule_dst': '', 'rule_svc': ''}
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

                    rule['rule_src'] = extend_string_list(rule['rule_src'], rule_orig, 'orig-addr', LIST_DELIMITER)
                    rule['rule_dst'] = extend_string_list(rule['rule_dst'], rule_orig, 'dst-addr', LIST_DELIMITER)
                    
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
                    rule.update({ 'rule_src_refs': resolve_raw_objects(rule['rule_src'], LIST_DELIMITER, full_config, 'name', 'uuid', rule_type=rule_table) }, #type: ignore #TYPING: ???? 
                        jwt=jwt, import_id=import_id, rule_uid=rule_orig['uuid'], object_type='network object')
                    rule.update({ 'rule_dst_refs': resolve_raw_objects(rule['rule_dst'], LIST_DELIMITER, full_config, 'name', 'uuid', rule_type=rule_table) }, #type: ignore #TYPING: ????
                        jwt=jwt, import_id=import_id, rule_uid=rule_orig['uuid'], object_type='network object')
                    # services do not have uids, so using name instead
                    rule.update({ 'rule_svc_refs': rule['rule_svc'] })
                    rule.update({ 'rule_type': 'original' })
                    rule.update({ 'rule_installon': None })
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
                    xlate_rule['rule_src'] = extend_string_list(xlate_rule['rule_src'], rule_orig, 'orig-addr', LIST_DELIMITER)
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

                    xlate_rule.update({ 'rule_src_refs': resolve_objects(xlate_rule['rule_src'], LIST_DELIMITER, full_config, 'name', 'uuid', rule_type=rule_table, jwt=jwt, import_id=import_id ) })   #type: ignore #TYPING: ????
                    xlate_rule.update({ 'rule_dst_refs': resolve_objects(xlate_rule['rule_dst'], LIST_DELIMITER, full_config, 'name', 'uuid', rule_type=rule_table, jwt=jwt, import_id=import_id ) })   #type: ignore #TYPING: ????
                    xlate_rule.update({ 'rule_svc_refs': xlate_rule['rule_svc'] })  # services do not have uids, so using name instead

                    xlate_rule.update({ 'rule_type': 'xlate' })

                    nat_rules.append(xlate_rule)
                    rule_number += 1
    config2import['rules'].extend(nat_rules)


def insert_header(rules: list[dict[str, Any]], import_id: int, header_text: str, rulebase_name: str, rule_uid: str, rule_number: int, src_refs: str, dst_refs: str):
    rule: dict[str, Any] = {
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


def create_xlate_rule(rule: dict[str, Any]) -> dict[str, Any]:
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


#TODO: unused function
def handle_combined_nat_rule(rule: dict[str, Any], rule_orig: dict[str, Any], config2import: dict[str, Any], nat_rule_number: int, import_id: int, dev_id: int) -> dict[str, Any] | None:
    # now dealing with VIPs (dst NAT part) of combined rules
    xlate_rule = None

    # dealing with src NAT part of combined rules
    if "nat" in rule_orig and rule_orig["nat"]==1:
        FWOLogger.debug("found mixed Access/NAT rule no. " + str(nat_rule_number))
        nat_rule_number += 1
        xlate_rule = create_xlate_rule(rule)
        if 'ippool' in rule_orig:
            if rule_orig['ippool']==0:  # hiding behind outbound interface
                interface_name = 'unknownIF'
                destination_interface_ip = '0.0.0.0'
                destination_ip = get_first_ip_of_destination(rule['rule_dst_refs'], config2import) # get an ip of destination
                hideInterface = 'undefined_interface'
                if destination_ip is None:
                    FWOLogger.warning('src nat behind interface: found no valid destination ip in rule with UID ' + rule['rule_uid'])
                else:
                    # matching_route = get_matching_route_obj(destination_ip, config2import['networking'][device_name]['routingv4'])
                    matching_route = get_matching_route_obj(destination_ip, config2import['routing'], dev_id)
                    if matching_route is None:
                        FWOLogger.warning('src nat behind interface: found no matching route in rule with UID '
                            + rule['rule_uid'] + ', dest_ip: ' + destination_ip)
                    else:
                        destination_interface_ip = get_ip_of_interface_obj(matching_route.interface, dev_id, config2import['interfaces'])
                        interface_name = matching_route.interface
                        hideInterface=interface_name
                        if hideInterface is None:
                            FWOLogger.warning('src nat behind interface: found route with undefined interface ' + str(jsonpickle.dumps(matching_route, unpicklable=True))) #type: ignore #TYPING: Not sure about jsonpickle usage here why not just json
                        if destination_interface_ip is None:
                            FWOLogger.warning('src nat behind interface: found no matching interface IP in rule with UID '
                            + rule['rule_uid'] + ', dest_ip: ' + destination_ip)
        
                # add dummy object "outbound-interface"
                if hideInterface is not None:
                    obj_name = 'hide_IF_ip_' + str(hideInterface) + '_' + str(destination_interface_ip)
                    obj_comment = 'FWO auto-generated dummy object for source nat'
                    if type(ipaddress.ip_address(str(destination_interface_ip))) is ipaddress.IPv6Address:
                        HideNatIp = str(destination_interface_ip) + '/128'
                    elif type(ipaddress.ip_address(str(destination_interface_ip))) is ipaddress.IPv4Address:
                        HideNatIp = str(destination_interface_ip) + '/32'
                    else:
                        HideNatIp = DUMMY_IP
                        FWOLogger.warning('found invalid HideNatIP ' + str(destination_interface_ip))
                    obj = create_network_object(import_id, obj_name, 'host', HideNatIp, obj_name, 'black', obj_comment, 'global')
                    if obj not in config2import['network_objects']:
                        config2import['network_objects'].append(obj)
                    xlate_rule['rule_src'] = obj_name
                    xlate_rule['rule_src_refs'] = obj_name

            elif rule_orig['ippool']==1: # hiding behind one ip of an ip pool
                poolNameArray = rule_orig['poolname']
                if len(poolNameArray)>0:
                    if len(poolNameArray)>1:
                        FWOLogger.warning("found more than one ippool - ignoring all but first pool")
                    poolName = poolNameArray[0]
                    xlate_rule['rule_src'] = poolName
                    xlate_rule['rule_src_refs'] =  poolName
                else:
                    FWOLogger.warning("found ippool rule without ippool: " + rule['rule_uid'])
            else:
                FWOLogger.warning("found ippool rule with unexpected ippool value: " + rule_orig['ippool'])
        
        if 'natip' in rule_orig and rule_orig['natip']!=["0.0.0.0","0.0.0.0"]:
            FWOLogger.warning("found explicit natip rule - ignoring for now: " + rule['rule_uid'])
            # need example for interpretation of config

    # todo: find out how match-vip=1 influences natting (only set in a few vip-nat rules)
    # if "match-vip" in rule_orig and rule_orig["match-vip"]==1:
    #     FWOLogger.warning("found VIP destination Access/NAT rule (but not parsing yet); no. " + str(vip_nat_rule_number))
    #     vip_nat_rule_number += 1

    # deal with vip natting: check for each (dst) nw obj if it contains "obj_nat_ip"
    rule_dst_list = rule['rule_dst'].split(LIST_DELIMITER)
    nat_object_list = extract_nat_objects(rule_dst_list, config2import['network_objects'])

    if len(nat_object_list)>0:
        if xlate_rule is None: # no source nat, so we create the necessary nat rule here
            xlate_rule = create_xlate_rule(rule)
        xlate_dst: list[str] = []
        xlate_dst_refs: list[str] = []
        for nat_obj in nat_object_list:
            if 'obj_ip_end' in nat_obj: # this nat obj is a range - include the end ip in name and uid as well to avoid akey conflicts
                xlate_dst.append(nat_obj['obj_nat_ip'] + '-' + nat_obj['obj_ip_end'] + NAT_POSTFIX)
                nat_ref = nat_obj['obj_nat_ip']
                if 'obj_nat_ip_end' in nat_obj:
                    nat_ref += '-' + nat_obj['obj_nat_ip_end'] + NAT_POSTFIX
                xlate_dst_refs.append(nat_ref)
            else:
                xlate_dst.append(nat_obj['obj_nat_ip'] + NAT_POSTFIX)
                xlate_dst_refs.append(nat_obj['obj_nat_ip']  + NAT_POSTFIX)
        xlate_rule['rule_dst'] = LIST_DELIMITER.join(xlate_dst)
        xlate_rule['rule_dst_refs'] = LIST_DELIMITER.join(xlate_dst_refs)
    # else: (no nat object found) no dnatting involved, dst stays "Original"

    return xlate_rule


def insert_headers(rule_table: str, first_v6: bool, first_v4: bool, full_config: dict[str, Any], rules: list[dict[str, Any]], import_id: int, localPkgName: str,src_ref_all: str,dst_ref_all: str,rule_number: int) -> tuple[int, bool, bool]:
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


def extract_nat_objects(nwobj_list: list[str], all_nwobjects: list[dict[str, Any]]) -> list[dict[str, Any]]:
    nat_obj_list: list[dict[str, Any]] = []
    for obj in nwobj_list:
        for obj2 in all_nwobjects:
            if obj2['obj_name']==obj:
                if 'obj_nat_ip' in obj2:
                    nat_obj_list.append(obj2)
                break
        # if obj in all_nwobjects and 'obj_nat_ip' in all_nwobjects[obj]:
        #     nat_obj_list.append(obj)
    return nat_obj_list


def add_users_to_rule(rule_orig: dict[str, Any], rule: dict[str, Any]) -> None:
    if 'groups' in rule_orig:
        add_users(rule_orig['groups'], rule)
    if 'users' in rule_orig:
        add_users(rule_orig['users'], rule)


def add_users(users: list[str], rule: dict[str, Any]) -> None:
    for user in users:
        rule_src_with_users: list[str] = []
        for src in rule['rule_src'].split(LIST_DELIMITER):
            rule_src_with_users.append(user + '@' + src)
        rule['rule_src'] = LIST_DELIMITER.join(rule_src_with_users)

        # here user ref is the user name itself
        rule_src_refs_with_users: list[str] = []
        for src in rule['rule_src_refs'].split(LIST_DELIMITER):
            rule_src_refs_with_users.append(user + '@' + src)
        rule['rule_src_refs'] = LIST_DELIMITER.join(rule_src_refs_with_users)

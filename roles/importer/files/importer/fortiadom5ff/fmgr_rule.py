import copy
import jsonpickle
import ipaddress
import time
from time import strftime, localtime
from fwo_const import list_delimiter, nat_postfix, dummy_ip
from fwo_base import extend_string_list, sanitize
from fmgr_service import create_svc_object
from fmgr_network import create_network_object, get_first_ip_of_destination
from fmgr_zone import find_zones_in_normalized_config
import fmgr_getter
from fmgr_gw_networking import get_device_from_package
from fwo_log import getFwoLogger
from model_controllers.route_controller import get_matching_route_obj, get_ip_of_interface_obj
from fwo_exceptions import FwoDeviceWithoutLocalPackage, FwoImporterErrorInconsistencies
#from fmgr_base import resolve_raw_objects, resolve_objects
from models.rule import Rule, RuleNormalized, RuleAction, RuleTrack, RuleType
from models.rulebase import Rulebase
from models.import_state import ImportState


NETWORK_OBJECT='network_object'
rule_access_scope_v4 = ['rules_global_header_v4', 'rules_adom_v4', 'rules_global_footer_v4']
rule_access_scope_v6 = ['rules_global_header_v6', 'rules_adom_v6', 'rules_global_footer_v6']
rule_access_scope = rule_access_scope_v6 + rule_access_scope_v4
rule_nat_scope = ['rules_global_nat', 'rules_adom_nat']
rule_scope = rule_access_scope + rule_nat_scope


def normalize_rulebases(
    import_state: ImportState,
    mgm_uid: str,
    native_config: dict,
    native_config_global: dict,
    normalized_config_adom: dict,
    normalized_config_global: dict,
    is_global_loop_iteration: bool
) -> None:
    normalized_config_adom['policies'] = []

    fetched_rulebase_uids: list = []
    if normalized_config_global is not None:
        for normalized_rulebase_global in normalized_config_global.get('policies', []):
            fetched_rulebase_uids.append(normalized_rulebase_global.uid)
    for gateway in native_config['gateways']:
        normalize_rulebases_for_each_link_destination(
            gateway, mgm_uid, fetched_rulebase_uids, native_config, native_config_global,
            is_global_loop_iteration, normalized_config_adom,
            normalized_config_global)

    # todo: parse nat rulebase here


def normalize_rulebases_for_each_link_destination(gateway, mgm_uid, fetched_rulebase_uids, native_config, native_config_global, is_global_loop_iteration, normalized_config_adom, normalized_config_global):
    logger = getFwoLogger()
    for rulebase_link in gateway['rulebase_links']:
        if rulebase_link['to_rulebase_uid'] not in fetched_rulebase_uids and rulebase_link['to_rulebase_uid'] != '':
            rulebase_to_parse = find_rulebase_to_parse(native_config['rulebases'], rulebase_link['to_rulebase_uid'])
            # search in global rulebase
            found_rulebase_in_global = False
            if rulebase_to_parse == {} and not is_global_loop_iteration and native_config_global is not None:
                rulebase_to_parse = find_rulebase_to_parse(
                    native_config_global['rulebases'], rulebase_link['to_rulebase_uid']
                    )
                found_rulebase_in_global = True
            if rulebase_to_parse == {}:
                logger.warning('found to_rulebase link without rulebase in nativeConfig: ' + str(rulebase_link))
                continue

            normalized_rulebase = initialize_normalized_rulebase(rulebase_to_parse, mgm_uid)
            parse_rulebase(normalized_config_adom, normalized_config_global, rulebase_to_parse, normalized_rulebase, found_rulebase_in_global)
            fetched_rulebase_uids.append(rulebase_link['to_rulebase_uid'])

            if found_rulebase_in_global:
                normalized_config_global['policies'].append(normalized_rulebase)
            else:
                normalized_config_adom['policies'].append(normalized_rulebase)

def find_rulebase_to_parse(rulebase_list, rulebase_uid):
    for rulebase in rulebase_list:
        if rulebase['uid'] == rulebase_uid:
            return rulebase
    return {}
                    
def initialize_normalized_rulebase(rulebase_to_parse, mgm_uid):
    """
    we use 'type' as uid/name since a rulebase may have a v4 and a v6 part
    """
    rulebaseName = rulebase_to_parse['type']
    rulebaseUid = rulebase_to_parse['type']
    normalized_rulebase = Rulebase(uid=rulebaseUid, name=rulebaseName, mgm_uid=mgm_uid, Rules={})
    return normalized_rulebase

def parse_rulebase(normalized_config_adom, normalized_config_global, rulebase_to_parse, normalized_rulebase, found_rulebase_in_global):

    rule_num = 1
    for native_rule in rulebase_to_parse['data']:
        rule_num = parse_single_rule(normalized_config_adom, normalized_config_global, native_rule, normalized_rulebase, rule_num)
    if not found_rulebase_in_global:
        add_implicit_deny_rule(rule_num, normalized_config_adom, normalized_config_global, normalized_rulebase)

def add_implicit_deny_rule(rule_num, normalized_config_adom, normalized_config_global, rulebase: Rulebase):
    
    deny_rule = {'srcaddr': ['all'], 'srcaddr6': ['all'],
                 'dstaddr': ['all'], 'dstaddr6': ['all'],
                 'service': ['ALL'],
                 'srcintf': ['any'], 'dstintf': ['any']}

    rule_src_list, rule_src_refs_list = rule_parse_addresses(deny_rule, 'src', normalized_config_adom, normalized_config_global)
    rule_dst_list, rule_dst_refs_list = rule_parse_addresses(deny_rule, 'dst', normalized_config_adom, normalized_config_global)
    rule_svc_list, rule_svc_refs_list = rule_parse_service(deny_rule)
    rule_src_zones = find_zones_in_normalized_config(
        deny_rule.get('srcintf', []), normalized_config_adom, normalized_config_global)
    rule_dst_zones = find_zones_in_normalized_config(
        deny_rule.get('dstintf', []), normalized_config_adom, normalized_config_global)

    rule_normalized = RuleNormalized(
        rule_num=rule_num,
        rule_num_numeric=0,
        rule_disabled=False,
        rule_src_neg=False,
        rule_src=list_delimiter.join(rule_src_list),
        rule_src_refs=list_delimiter.join(rule_src_refs_list),
        rule_dst_neg=False,
        rule_dst=list_delimiter.join(rule_dst_list),
        rule_dst_refs=list_delimiter.join(rule_dst_refs_list),
        rule_svc_neg=False,
        rule_svc=list_delimiter.join(rule_svc_list),
        rule_svc_refs=list_delimiter.join(rule_svc_refs_list),
        rule_action=RuleAction.DROP,
        rule_track=RuleTrack.NONE,
        rule_installon='',
        rule_time='',  # Time-based rules not commonly used in basic Fortinet configs
        rule_name='Implicit Deny',
        rule_uid='',
        rule_custom_fields={},
        rule_implied=False,
        rule_type=RuleType.ACCESS,
        last_change_admin='',
        parent_rule_uid=None,
        last_hit=None,
        rule_comment='',
        rule_src_zone=list_delimiter.join(rule_src_zones),
        rule_dst_zone=list_delimiter.join(rule_dst_zones),
        rule_head_text=None
    )
    rulebase.Rules[rule_normalized.rule_uid] = rule_normalized

def parse_single_rule(normalized_config_adom, normalized_config_global, native_rule, rulebase: Rulebase, rule_num):
    # Extract basic rule information
    rule_disabled = True  # Default to disabled
    if 'status' in native_rule and (native_rule['status'] == 1 or native_rule['status'] == 'enable'):
        rule_disabled = False
    
    rule_action = rule_parse_action(native_rule)

    rule_track = rule_parse_tracking_info(native_rule)

    rule_src_list, rule_src_refs_list = rule_parse_addresses(native_rule, 'src', normalized_config_adom, normalized_config_global)
    rule_dst_list, rule_dst_refs_list = rule_parse_addresses(native_rule, 'dst', normalized_config_adom, normalized_config_global)

    rule_svc_list, rule_svc_refs_list = rule_parse_service(native_rule)

    rule_src_zones = find_zones_in_normalized_config(
        native_rule.get('srcintf', []), normalized_config_adom, normalized_config_global)
    rule_dst_zones = find_zones_in_normalized_config(
        native_rule.get('dstintf', []), normalized_config_adom, normalized_config_global)

    rule_src_neg, rule_dst_neg, rule_svc_neg = rule_parse_negation_flags(native_rule)
    rule_installon = rule_parse_installon(native_rule, rulebase.name)

    last_hit = rule_parse_last_hit(native_rule)

    # Create the normalized rule
    rule_normalized = RuleNormalized(
        rule_num=rule_num,
        rule_num_numeric=0,
        rule_disabled=rule_disabled,
        rule_src_neg=rule_src_neg,
        rule_src=list_delimiter.join(rule_src_list),
        rule_src_refs=list_delimiter.join(rule_src_refs_list),
        rule_dst_neg=rule_dst_neg,
        rule_dst=list_delimiter.join(rule_dst_list),
        rule_dst_refs=list_delimiter.join(rule_dst_refs_list),
        rule_svc_neg=rule_svc_neg,
        rule_svc=list_delimiter.join(rule_svc_list),
        rule_svc_refs=list_delimiter.join(rule_svc_refs_list),
        rule_action=rule_action,
        rule_track=rule_track,
        rule_installon=rule_installon,
        rule_time='',  # Time-based rules not commonly used in basic Fortinet configs
        rule_name=native_rule.get('name'),
        rule_uid=native_rule.get('uuid'),
        rule_custom_fields=str(native_rule.get('meta fields', {})),
        rule_implied=False,
        rule_type=RuleType.ACCESS,
        last_change_admin=native_rule.get('_last-modified-by', ''),
        parent_rule_uid=None,
        last_hit=last_hit,
        rule_comment=native_rule.get('comments'),
        rule_src_zone=list_delimiter.join(rule_src_zones),
        rule_dst_zone=list_delimiter.join(rule_dst_zones),
        rule_head_text=None
    )
    
    # Add the rule to the rulebase
    rulebase.Rules[rule_normalized.rule_uid] = rule_normalized

    # TODO: handle NAT
    
    return rule_num + 1

def rule_parse_action(native_rule):
    # Extract action - Fortinet uses 0 for deny/drop, 1 for accept
    if native_rule.get('action', 0) == 0:
        return RuleAction.DROP
    else:
        return RuleAction.ACCEPT

def rule_parse_tracking_info(native_rule):
    # TODO: Implement more detailed logging level extraction (difference between 1/2/3?)
    logtraffic = native_rule.get('logtraffic', 0)
    if isinstance(logtraffic, int) and logtraffic > 0 or isinstance(logtraffic, str) and logtraffic != 'disable':
        return RuleTrack.LOG
    else:
        return RuleTrack.NONE

def rule_parse_service(native_rule):
    rule_svc_list = []
    rule_svc_refs_list = []
    for svc in native_rule.get('service', []):
        rule_svc_list.append(svc)
        rule_svc_refs_list.append(svc)

    return rule_svc_list, rule_svc_refs_list

def rule_parse_addresses(native_rule, target, normalized_config_adom, normalized_config_global):
    if target not in ['src', 'dst']:
        raise FwoImporterErrorInconsistencies(f"target '{target}' must either be src or dst.")
    addr_list = []
    addr_ref_list = []
    build_addr_list(native_rule, True, target, normalized_config_adom, normalized_config_global, addr_list, addr_ref_list)
    build_addr_list(native_rule, False, target, normalized_config_adom, normalized_config_global, addr_list, addr_ref_list)
    return addr_list, addr_ref_list

def build_addr_list(native_rule, is_v4, target, normalized_config_adom, normalized_config_global, addr_list, addr_ref_list):
    if is_v4 and target == 'src':
        for addr in native_rule.get('srcaddr', []) + native_rule.get('internet-service-src-name', []):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global))
    elif not is_v4 and target == 'src':
        for addr in native_rule.get('srcaddr6', []):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global))
    elif is_v4 and target == 'dst':
        for addr in native_rule.get('dstaddr', []):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global))
    else:
        for addr in native_rule.get('dstaddr6', []):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global))

def find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global):
    for nw_obj in normalized_config_adom['network_objects'] + normalized_config_global.get('network_objects', []):
        if addr == nw_obj['obj_name']:
            if (is_v4 and ip_type(nw_obj) == 4) or (not is_v4 and ip_type(nw_obj) == 6):
                return nw_obj['obj_uid']
    raise FwoImporterErrorInconsistencies(f"No ref found for '{addr}'.")

def ip_type(nw_obj):
    # default to v4
    first_ip = nw_obj.get('obj_ip', '0.0.0.0/32')
    if first_ip == '':
        first_ip = '0.0.0.0/32'
    net=ipaddress.ip_network(str(first_ip))
    return net.version

def rule_parse_negation_flags(native_rule):
    if 'srcaddr-negate' in native_rule:
        rule_src_neg = native_rule['srcaddr-negate'] == 1 or native_rule['srcaddr-negate'] == 'disable'
    elif 'internet-service-src-negate' in native_rule:
        rule_src_neg = native_rule['internet-service-src-negate'] == 1 or native_rule['internet-service-src-negate'] == 'disable'
    else:
        rule_src_neg = False
    rule_dst_neg = 'dstaddr-negate' in native_rule and (native_rule['dstaddr-negate'] == 1 or native_rule['dstaddr-negate'] == 'disable') #TODO: last part does not make sense?
    rule_svc_neg = 'service-negate' in native_rule and (native_rule['service-negate'] == 1 or native_rule['service-negate'] == 'disable')
    return rule_src_neg, rule_dst_neg, rule_svc_neg

def rule_parse_installon(native_rule, rulebase_name):
    if 'scope_member' in native_rule:
        rule_installon = list_delimiter.join(sorted({vdom['name'] + '_' + vdom['vdom'] for vdom in native_rule['scope_member']}))
    else:
        rule_installon = rulebase_name
    return rule_installon

def rule_parse_last_hit(native_rule):
    last_hit = native_rule.get('_last_hit', None)
    if last_hit != None:
        last_hit = strftime("%Y-%m-%d %H:%M:%S", localtime(last_hit))
    return last_hit

def get_access_policy(sid, fm_api_url, native_config_adom, native_config_global, adom_device_vdom_policy_package_structure, adom_name, mgm_details_device, device_config, limit):

    previous_rulebase = None
    link_list = []
    local_pkg_name, global_pkg_name = find_packages(adom_device_vdom_policy_package_structure, adom_name, mgm_details_device)
    options = ['extra info', 'scope member', 'get meta']

    previous_rulebase = get_and_link_global_rulebase(
        'header', previous_rulebase, global_pkg_name, native_config_global, sid, fm_api_url, options, limit, link_list)
    
    previous_rulebase = get_and_link_local_rulebase(
        'rules_adom', previous_rulebase, adom_name, local_pkg_name, native_config_adom, sid, fm_api_url, options, limit, link_list)
    
    previous_rulebase = get_and_link_global_rulebase(
        'footer', previous_rulebase, global_pkg_name, native_config_global, sid, fm_api_url, options, limit, link_list)

    device_config['rulebase_links'].extend(link_list)

def get_and_link_global_rulebase(header_or_footer, previous_rulebase, global_pkg_name, native_config_global, sid, fm_api_url, options, limit, link_list):
    rulebase_type_prefix = 'rules_global_' + header_or_footer
    if global_pkg_name != '':
        if not is_rulebase_already_fetched(native_config_global['rulebases'], rulebase_type_prefix + '_v4_' + global_pkg_name):
            fmgr_getter.update_config_with_fortinet_api_call(
                native_config_global['rulebases'],
                sid, fm_api_url,
                '/pm/config/global/pkg/' + global_pkg_name + '/global/' + header_or_footer + '/policy',
                rulebase_type_prefix + '_v4_' + global_pkg_name,
                options=options, limit=limit)
        if not is_rulebase_already_fetched(native_config_global['rulebases'], rulebase_type_prefix + '_v6_' + global_pkg_name):
            # delete_v: hier auch options=options?
            fmgr_getter.update_config_with_fortinet_api_call(
                native_config_global['rulebases'],
                sid, fm_api_url,
                '/pm/config/global/pkg/' + global_pkg_name + '/global/' + header_or_footer + '/policy6',
                rulebase_type_prefix + '_v6_' + global_pkg_name,
                limit=limit)
        previous_rulebase = link_rulebase(link_list, native_config_global['rulebases'], global_pkg_name, rulebase_type_prefix, previous_rulebase, True)
    return previous_rulebase

def get_and_link_local_rulebase(rulebase_type_prefix, previous_rulebase, adom_name, local_pkg_name, native_config_adom, sid, fm_api_url, options, limit, link_list):
    if not is_rulebase_already_fetched(native_config_adom['rulebases'], rulebase_type_prefix + '_v4_' + local_pkg_name):
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_adom['rulebases'],
            sid, fm_api_url,
            '/pm/config/adom/' + adom_name + '/pkg/' + local_pkg_name + '/firewall/policy',
            rulebase_type_prefix + '_v4_' + local_pkg_name,
            options=options, limit=limit)
    if not is_rulebase_already_fetched(native_config_adom['rulebases'], rulebase_type_prefix + '_v6_' + local_pkg_name):
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_adom['rulebases'],
            sid, fm_api_url,
            '/pm/config/adom/' + adom_name + '/pkg/' + local_pkg_name + '/firewall/policy6',
            rulebase_type_prefix + '_v6_' + local_pkg_name,
            limit=limit)
    previous_rulebase = link_rulebase(link_list, native_config_adom['rulebases'], local_pkg_name, rulebase_type_prefix, previous_rulebase, False)
    return previous_rulebase

def find_packages(adom_device_vdom_policy_package_structure, adom_name, mgm_details_device):
    for device in adom_device_vdom_policy_package_structure[adom_name]:
        for vdom in adom_device_vdom_policy_package_structure[adom_name][device]:
            if mgm_details_device['name'] == device + '_' + vdom:
                return adom_device_vdom_policy_package_structure[adom_name][device][vdom]['local'], adom_device_vdom_policy_package_structure[adom_name][device][vdom]['global']
    raise FwoDeviceWithoutLocalPackage('Could not find local package for ' + mgm_details_device['name'] + ' in Fortimanager Config') from None

def is_rulebase_already_fetched(rulebases, type):
    for rulebase in rulebases:
        if rulebase['type'] == type:
            return True
    return False

def link_rulebase(link_list, rulebases, pkg_name, rulebase_type_prefix, previous_rulebase, is_global):
    for version in ['v4', 'v6']:
        full_pkg_name = rulebase_type_prefix + '_' + version + '_' + pkg_name
        has_data = has_rulebase_data(rulebases, full_pkg_name, is_global)
        if has_data:
            link_list.append(build_link(previous_rulebase, full_pkg_name, is_global))
            previous_rulebase = full_pkg_name
    
    return previous_rulebase

def build_link(previous_rulebase, full_pkg_name, is_global):
    if previous_rulebase is None:
        is_initial = True
    else:
        is_initial = False
    return {
        'from_rulebase_uid': previous_rulebase,
        'from_rule_uid': None,
        'to_rulebase_uid': full_pkg_name,
        'type': 'concatenated',
        'is_global': is_global,
        'is_initial': is_initial,
        'is_section': False
    }

def has_rulebase_data(rulebases, full_pkg_name, is_global):
    """adds name and uid to rulebase and removes empty global rulebases"""
    has_data = False
    for rulebase in rulebases:
        if rulebase['type'] == full_pkg_name:
            rulebase.update({'name': full_pkg_name,
                             'uid': full_pkg_name})
            if len(rulebase['data']) > 0:
                has_data = True
            elif is_global:
                rulebases.remove(rulebase)
    return has_data

def get_nat_policy(sid, fm_api_url, native_config, adom_device_vdom_policy_package_structure, adom_name, mgm_details_device, limit):
    local_pkg_name, global_pkg_name = find_packages(adom_device_vdom_policy_package_structure, adom_name, mgm_details_device)
    if adom_name == '':
        for nat_type in ['central/dnat', 'central/dnat6', 'firewall/central-snat-map']:
            fmgr_getter.update_config_with_fortinet_api_call(
                native_config['nat_rulebases'], sid, fm_api_url,
                '/pm/config/global/pkg/' + global_pkg_name + '/' + nat_type,
                nat_type + '_global_' + global_pkg_name, limit=limit)
    else:
        for nat_type in ['central/dnat', 'central/dnat6', 'firewall/central-snat-map']:
            fmgr_getter.update_config_with_fortinet_api_call(
                native_config['nat_rulebases'], sid, fm_api_url,
                '/pm/config/adom/' + adom_name + '/pkg/' + local_pkg_name + '/' + nat_type,
                nat_type + '_adom_' + adom_name + '_' + local_pkg_name, limit=limit)

    # scope = 'global'
    # pkg = device['global_rulebase_name']
    # if pkg is not None and pkg != '':   # only read global rulebase if it exists
    #     for nat_type in ['central/dnat', 'central/dnat6', 'firewall/central-snat-map']:
    #         fmgr_getter.update_config_with_fortinet_api_call(
    #             nativeConfig['rules_global_nat'], sid, fm_api_url, "/pm/config/" + scope + "/pkg/" + pkg + '/' + nat_type, device['local_rulebase_name'], limit=limit)

    # scope = 'adom/'+adom_name
    # pkg = device['local_rulebase_name']
    # for nat_type in ['central/dnat', 'central/dnat6', 'firewall/central-snat-map']:
    #     fmgr_getter.update_config_with_fortinet_api_call(
    #         nativeConfig['rules_adom_nat'], sid, fm_api_url, "/pm/config/" + scope + "/pkg/" + pkg + '/' + nat_type, device['local_rulebase_name'], limit=limit)


# delete_v: ab hier kann sehr viel weg, ich lasses vorerst zB für die nat
# pure nat rules 
def normalize_nat_rulebases(native_config, normalized_config_adom, import_id, jwt=None):
    nat_rules = []
    rule_number = 0

    for rule_table in rule_nat_scope:
        for localPkgName in native_config['rules_global_nat']:
            for rule_orig in native_config[rule_table][localPkgName]:
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

                    if not 'service_objects' in normalized_config_adom: # is normally defined
                        normalized_config_adom['service_objects'] = []
                    normalized_config_adom['service_objects'].append(create_svc_object( \
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
                    rule.update({ 'rule_src_refs': resolve_raw_objects(rule['rule_src'], list_delimiter, native_config, 'name', 'uuid', rule_type=rule_table) }, \
                        jwt=jwt, import_id=import_id, rule_uid=rule_orig['uuid'], object_type=NETWORK_OBJECT)
                    rule.update({ 'rule_dst_refs': resolve_raw_objects(rule['rule_dst'], list_delimiter, native_config, 'name', 'uuid', rule_type=rule_table) }, \
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
                    normalized_config_adom['service_objects'].append(create_svc_object(import_id=import_id, name=svc_name, proto=rule_orig['protocol'], port=rule_orig['nat-port'], comment='service created by FWO importer for NAT purposes'))
                    xlate_rule['rule_svc'] = svc_name

                    xlate_rule.update({ 'rule_src_refs': resolve_objects(xlate_rule['rule_src'], list_delimiter, native_config, 'name', 'uuid', rule_type=rule_table, jwt=jwt, import_id=import_id ) })
                    xlate_rule.update({ 'rule_dst_refs': resolve_objects(xlate_rule['rule_dst'], list_delimiter, native_config, 'name', 'uuid', rule_type=rule_table, jwt=jwt, import_id=import_id ) })
                    xlate_rule.update({ 'rule_svc_refs': xlate_rule['rule_svc'] })  # services do not have uids, so using name instead

                    xlate_rule.update({ 'rule_type': 'xlate' })

                    nat_rules.append(xlate_rule)
                    rule_number += 1
    normalized_config_adom['rules'].extend(nat_rules)


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

import copy
import ipaddress
from time import strftime, localtime
from typing import Any
from fwo_const import LIST_DELIMITER, NAT_POSTFIX, DUMMY_IP
from fw_modules.fortiadom5ff.fmgr_network import create_network_object, get_first_ip_of_destination
from fw_modules.fortiadom5ff.fmgr_zone import find_zones_in_normalized_config
from fw_modules.fortiadom5ff.fmgr_consts import nat_types
from fw_modules.fortiadom5ff import fmgr_getter
from fwo_log import FWOLogger
from model_controllers.route_controller import get_matching_route_obj, get_ip_of_interface_obj
from fwo_exceptions import FwoDeviceWithoutLocalPackage, FwoImporterErrorInconsistencies
from models.rule import RuleNormalized, RuleAction, RuleTrack, RuleType
from models.rulebase import Rulebase

NETWORK_OBJECT='network_object'
STRING_PKG = '/pkg/'
STRING_PM_CONFIG_GLOBAL_PKG = '/pm/config/global/pkg/'
STRING_PM_CONFIG_ADOM = '/pm/config/adom/'
rule_access_scope_v4 = ['rules_global_header_v4', 'rules_adom_v4', 'rules_global_footer_v4']
rule_access_scope_v6 = ['rules_global_header_v6', 'rules_adom_v6', 'rules_global_footer_v6']
rule_access_scope = rule_access_scope_v6 + rule_access_scope_v4
rule_nat_scope = ['rules_global_nat', 'rules_adom_nat']
rule_scope = rule_access_scope + rule_nat_scope


def normalize_rulebases(
    mgm_uid: str,
    native_config: dict[str, Any],
    native_config_global: dict[str, Any],
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    is_global_loop_iteration: bool
) -> None:
    
    normalized_config_adom['policies'] = []
    fetched_rulebase_uids: list[str] = []
    if normalized_config_global != {}:
        for normalized_rulebase_global in normalized_config_global.get('policies', []):
            fetched_rulebase_uids.append(normalized_rulebase_global.uid)
    for gateway in native_config['gateways']:
        normalize_rulebases_for_each_link_destination(
            gateway, mgm_uid, fetched_rulebase_uids, native_config, native_config_global,
            is_global_loop_iteration, normalized_config_adom,
            normalized_config_global)

def normalize_rulebases_for_each_link_destination(gateway: dict[str, Any], mgm_uid: str, fetched_rulebase_uids: list[str], native_config: dict[str, Any],
        native_config_global: dict[str, Any], is_global_loop_iteration: bool, normalized_config_adom: dict[str, Any], normalized_config_global: dict[str, Any]):
    for rulebase_link in gateway['rulebase_links']:
        if rulebase_link['to_rulebase_uid'] not in fetched_rulebase_uids and rulebase_link['to_rulebase_uid'] != '':
            rulebase_to_parse = find_rulebase_to_parse(native_config['rulebases'], rulebase_link['to_rulebase_uid'])
            # search in global rulebase
            found_rulebase_in_global = False
            if rulebase_to_parse == {} and not is_global_loop_iteration and native_config_global != {}:
                rulebase_to_parse = find_rulebase_to_parse(
                    native_config_global['rulebases'], rulebase_link['to_rulebase_uid']
                    )
                found_rulebase_in_global = True
            if rulebase_to_parse == {}:
                FWOLogger.warning('found to_rulebase link without rulebase in nativeConfig: ' + str(rulebase_link))
                continue

            normalized_rulebase = initialize_normalized_rulebase(rulebase_to_parse, mgm_uid)
            parse_rulebase(normalized_config_adom, normalized_config_global, rulebase_to_parse, normalized_rulebase, found_rulebase_in_global)
            fetched_rulebase_uids.append(rulebase_link['to_rulebase_uid'])

            if found_rulebase_in_global:
                normalized_config_global['policies'].append(normalized_rulebase)
            else:
                normalized_config_adom['policies'].append(normalized_rulebase)

        # normalizing nat rulebases is work in progress
        #normalize_nat_rulebase(rulebase_link, native_config, normalized_config_adom, normalized_config_global)

def normalize_nat_rulebase(rulebase_link: dict[str, Any], native_config: dict[str, Any], normalized_config_adom: dict[str, Any], normalized_config_global: dict[str, Any]):
    if not rulebase_link['is_section']:
        for nat_type in nat_types:
            nat_type_string = nat_type + '_' + rulebase_link['to_rulebase_uid']
            nat_rulebase = get_native_nat_rulebase(native_config, nat_type_string)
            parse_nat_rulebase(nat_rulebase, nat_type_string, normalized_config_adom, normalized_config_global)

def get_native_nat_rulebase(native_config: dict[str, Any], nat_type_string: str) -> list[dict[str, Any]]:
    for nat_rulebase in native_config['nat_rulebases']:
        if nat_type_string == nat_rulebase['type']:
            return nat_rulebase['data']
    FWOLogger.warning('no nat data for '+ nat_type_string)
    return []

def find_rulebase_to_parse(rulebase_list: list[dict[str, Any]], rulebase_uid: str) -> dict[str, Any]:
    for rulebase in rulebase_list:
        if rulebase['uid'] == rulebase_uid:
            return rulebase
    return {}
                    
def initialize_normalized_rulebase(rulebase_to_parse: dict[str, Any], mgm_uid: str) -> Rulebase:
    """
    we use 'type' as uid/name since a rulebase may have a v4 and a v6 part
    """
    rulebaseName = rulebase_to_parse['type']
    rulebaseUid = rulebase_to_parse['type']
    normalized_rulebase = Rulebase(uid=rulebaseUid, name=rulebaseName, mgm_uid=mgm_uid, rules={})
    return normalized_rulebase

def parse_rulebase(normalized_config_adom: dict[str, Any], normalized_config_global: dict[str, Any], rulebase_to_parse: dict[str, Any], normalized_rulebase: Rulebase, found_rulebase_in_global: bool):
    """Parses a native Fortinet rulebase into a normalized rulebase."""
    for native_rule in rulebase_to_parse['data']:
        parse_single_rule(normalized_config_adom, normalized_config_global, native_rule, normalized_rulebase)
    if not found_rulebase_in_global:
        add_implicit_deny_rule(normalized_config_adom, normalized_config_global, normalized_rulebase)

def add_implicit_deny_rule(normalized_config_adom: dict[str, Any], normalized_config_global: dict[str, Any], rulebase: Rulebase):
    
    deny_rule = {'srcaddr': ['all'], 'srcaddr6': ['all'],
                 'dstaddr': ['all'], 'dstaddr6': ['all'],
                 'service': ['ALL'],
                 'srcintf': ['any'], 'dstintf': ['any']}

    rule_src_list, rule_src_refs_list = rule_parse_addresses(deny_rule, 'src', normalized_config_adom, normalized_config_global, False)
    rule_dst_list, rule_dst_refs_list = rule_parse_addresses(deny_rule, 'dst', normalized_config_adom, normalized_config_global, False)
    rule_svc_list, rule_svc_refs_list = rule_parse_service(deny_rule)
    rule_src_zones = find_zones_in_normalized_config(
        deny_rule.get('srcintf', []), normalized_config_adom, normalized_config_global)
    rule_dst_zones = find_zones_in_normalized_config(
        deny_rule.get('dstintf', []), normalized_config_adom, normalized_config_global)

    rule_normalized = RuleNormalized(
        rule_num=0,
        rule_num_numeric=0,
        rule_disabled=False,
        rule_src_neg=False,
        rule_src=LIST_DELIMITER.join(rule_src_list),
        rule_src_refs=LIST_DELIMITER.join(rule_src_refs_list),
        rule_dst_neg=False,
        rule_dst=LIST_DELIMITER.join(rule_dst_list),
        rule_dst_refs=LIST_DELIMITER.join(rule_dst_refs_list),
        rule_svc_neg=False,
        rule_svc=LIST_DELIMITER.join(rule_svc_list),
        rule_svc_refs=LIST_DELIMITER.join(rule_svc_refs_list),
        rule_action=RuleAction.DROP,
        rule_track=RuleTrack.NONE,     # I guess this could also have different values
        rule_installon=None,
        rule_time=None,  # Time-based rules not commonly used in basic Fortinet configs
        rule_name='Implicit Deny',
        rule_uid=f"{rulebase.uid}_implicit_deny",
        rule_custom_fields=str({}),
        rule_implied=True,
        rule_type=RuleType.ACCESS,
        last_change_admin=None,
        parent_rule_uid=None,
        last_hit=None,
        rule_comment=None,
        rule_src_zone=LIST_DELIMITER.join(rule_src_zones),
        rule_dst_zone=LIST_DELIMITER.join(rule_dst_zones),
        rule_head_text=None
    )

    if rule_normalized.rule_uid is None:
        raise FwoImporterErrorInconsistencies("rule_normalized.rule_uid is None when adding implicit deny rule")
    rulebase.rules[rule_normalized.rule_uid] = rule_normalized

def parse_single_rule(normalized_config_adom: dict[str, Any], normalized_config_global: dict[str, Any], native_rule: dict[str, Any], rulebase: Rulebase):
    """Parses a single native Fortinet rule into a normalized rule and adds it to the given rulebase."""
    # Extract basic rule information
    rule_disabled = True  # Default to disabled
    if 'status' in native_rule and (native_rule['status'] == 1 or native_rule['status'] == 'enable'):
        rule_disabled = False
    
    rule_action = rule_parse_action(native_rule)

    rule_track = rule_parse_tracking_info(native_rule)

    rule_src_list, rule_src_refs_list = rule_parse_addresses(native_rule, 'src', normalized_config_adom, normalized_config_global, False)
    rule_dst_list, rule_dst_refs_list = rule_parse_addresses(native_rule, 'dst', normalized_config_adom, normalized_config_global, False)

    rule_svc_list, rule_svc_refs_list = rule_parse_service(native_rule)

    rule_src_zones = find_zones_in_normalized_config(
        native_rule.get('srcintf', []), normalized_config_adom, normalized_config_global)
    rule_dst_zones = find_zones_in_normalized_config(
        native_rule.get('dstintf', []), normalized_config_adom, normalized_config_global)

    rule_src_neg, rule_dst_neg, rule_svc_neg = rule_parse_negation_flags(native_rule)
    rule_installon = rule_parse_installon(native_rule)

    last_hit = rule_parse_last_hit(native_rule)

    # Create the normalized rule
    rule_normalized = RuleNormalized(
        rule_num=0,
        rule_num_numeric=0,
        rule_disabled=rule_disabled,
        rule_src_neg=rule_src_neg,
        rule_src=LIST_DELIMITER.join(rule_src_list),
        rule_src_refs=LIST_DELIMITER.join(rule_src_refs_list),
        rule_dst_neg=rule_dst_neg,
        rule_dst=LIST_DELIMITER.join(rule_dst_list),
        rule_dst_refs=LIST_DELIMITER.join(rule_dst_refs_list),
        rule_svc_neg=rule_svc_neg,
        rule_svc=LIST_DELIMITER.join(rule_svc_list),
        rule_svc_refs=LIST_DELIMITER.join(rule_svc_refs_list),
        rule_action=rule_action,
        rule_track=rule_track,
        rule_installon=rule_installon,
        rule_time=None,  # Time-based rules not commonly used in basic Fortinet configs
        rule_name=native_rule.get('name'),
        rule_uid=native_rule.get('uuid'),
        rule_custom_fields=str(native_rule.get('meta fields', {})),
        rule_implied=False,
        rule_type=RuleType.ACCESS,
        last_change_admin=None, #native_rule.get('_last-modified-by', ''), not handled yet -> leave out to prevent mismatches
        parent_rule_uid=None,
        last_hit=last_hit,
        rule_comment=native_rule.get('comments'),
        rule_src_zone=LIST_DELIMITER.join(rule_src_zones),
        rule_dst_zone=LIST_DELIMITER.join(rule_dst_zones),
        rule_head_text=None
    )
    if rule_normalized.rule_uid is None:
        raise FwoImporterErrorInconsistencies("rule_normalized.rule_uid is None when parsing single rule")
    
    # Add the rule to the rulebase
    rulebase.rules[rule_normalized.rule_uid] = rule_normalized

    # TODO: handle combined NAT, see handle_combined_nat_rule

def rule_parse_action(native_rule: dict[str, Any]) -> RuleAction:
    # Extract action - Fortinet uses 0 for deny/drop, 1 for accept
    if native_rule.get('action', 0) == 0:
        return RuleAction.DROP
    else:
        return RuleAction.ACCEPT

def rule_parse_tracking_info(native_rule: dict[str, Any]) -> RuleTrack:
    # TODO: Implement more detailed logging level extraction (difference between 1/2/3?)
    logtraffic = native_rule.get('logtraffic', 0)
    if isinstance(logtraffic, int) and logtraffic > 0 or isinstance(logtraffic, str) and logtraffic != 'disable':
        return RuleTrack.LOG
    else:
        return RuleTrack.NONE

def rule_parse_service(native_rule: dict[str, Any]) -> tuple[list[str], list[str]]:
    """
    Parses services to ordered (!) name list and reference list.
    """
    rule_svc_list: list[str] = []
    rule_svc_refs_list: list[str] = []
    for svc in sorted(native_rule.get('service', [])):
        rule_svc_list.append(svc)
        rule_svc_refs_list.append(svc)
    if rule_svc_list == [] and 'internet-service-name' in native_rule and len(native_rule['internet-service-name']) > 0:
        rule_svc_list.append('ALL')
        rule_svc_refs_list.append('ALL')
    if rule_svc_list == [] and 'internet-service-src-name' in native_rule and len(native_rule['internet-service-src-name']) > 0:
        rule_svc_list.append('ALL')
        rule_svc_refs_list.append('ALL')

    return rule_svc_list, rule_svc_refs_list

def rule_parse_addresses(native_rule: dict[str, Any], target: str, normalized_config_adom: dict[str, Any], normalized_config_global: dict[str, Any], is_nat: bool) -> tuple[list[str], list[str]]:
    """
    Parses addresses to ordered (!) name list and reference list for source or destination addresses.
    """
    if target not in ['src', 'dst']:
        raise FwoImporterErrorInconsistencies(f"target '{target}' must either be src or dst.")
    addr_list: list[str] = []
    addr_ref_list: list[str] = []
    if not is_nat:
        build_addr_list(native_rule, True, target, normalized_config_adom, normalized_config_global, addr_list, addr_ref_list)
        build_addr_list(native_rule, False, target, normalized_config_adom, normalized_config_global, addr_list, addr_ref_list)
    else:
        build_nat_addr_list(native_rule, target, normalized_config_adom, normalized_config_global, addr_list, addr_ref_list)
    return addr_list, addr_ref_list

def build_addr_list(native_rule: dict[str, Any], is_v4: bool, target: str, normalized_config_adom: dict[str, Any], normalized_config_global: dict[str, Any], addr_list: list[str], addr_ref_list: list[str]) -> None:
    """
    Builds ordered (!) address list and address reference list for source or destination addresses.
    """
    if is_v4 and target == 'src':
        for addr in sorted(native_rule.get('srcaddr', [])) + sorted(native_rule.get('internet-service-src-name', [])):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global))
    elif not is_v4 and target == 'src':
        for addr in sorted(native_rule.get('srcaddr6', [])):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global))
    elif is_v4 and target == 'dst':
        for addr in sorted(native_rule.get('dstaddr', [])) + sorted(native_rule.get('internet-service-name', [])):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global))
    else:
        for addr in sorted(native_rule.get('dstaddr6', [])):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, is_v4, normalized_config_adom, normalized_config_global))

def build_nat_addr_list(native_rule: dict[str, Any], target: str, normalized_config_adom: dict[str, Any], normalized_config_global: dict[str, Any], addr_list: list[str], addr_ref_list: list[str]) -> None:
    # so far only ip v4 expected
    if target == 'src':
        for addr in sorted(native_rule.get('orig-addr', [])):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, True, normalized_config_adom, normalized_config_global))
    if target == 'dst':
        for addr in sorted(native_rule.get('dst-addr', [])):
            addr_list.append(addr)
            addr_ref_list.append(find_addr_ref(addr, True, normalized_config_adom, normalized_config_global))

def find_addr_ref(addr: str, is_v4: bool, normalized_config_adom: dict[str, Any], normalized_config_global: dict[str, Any]) -> str:
    for nw_obj in normalized_config_adom['network_objects'] + normalized_config_global.get('network_objects', []):
        if addr == nw_obj['obj_name']:
            if (is_v4 and ip_type(nw_obj) == 4) or (not is_v4 and ip_type(nw_obj) == 6):
                return nw_obj['obj_uid']
    raise FwoImporterErrorInconsistencies(f"No ref found for '{addr}'.")

def ip_type(nw_obj: dict[str, Any]) -> int:
    # default to v4
    first_ip = nw_obj.get('obj_ip', '0.0.0.0/32')
    if first_ip == '':
        first_ip = '0.0.0.0/32'
    net=ipaddress.ip_network(str(first_ip))
    return net.version

def rule_parse_negation_flags(native_rule: dict[str, Any]) -> tuple[bool, bool, bool]:
    # if customer decides to mix internet-service and "normal" addr obj in src/dst and mix negates this will prob. not work correctly
    if 'srcaddr-negate' in native_rule:
        rule_src_neg = native_rule['srcaddr-negate'] == 1 or native_rule['srcaddr-negate'] == 'disable'
    elif 'internet-service-src-negate' in native_rule:
        rule_src_neg = native_rule['internet-service-src-negate'] == 1 or native_rule['internet-service-src-negate'] == 'disable'
    else:
        rule_src_neg = False
    rule_dst_neg = 'dstaddr-negate' in native_rule and (native_rule['dstaddr-negate'] == 1 or native_rule['dstaddr-negate'] == 'disable') #TODO: last part does not make sense?
    rule_svc_neg = 'service-negate' in native_rule and (native_rule['service-negate'] == 1 or native_rule['service-negate'] == 'disable')
    return rule_src_neg, rule_dst_neg, rule_svc_neg

def rule_parse_installon(native_rule: dict[str, Any]) -> str|None:
    rule_installon = None
    if 'scope_member' in native_rule and native_rule['scope_member']:
        rule_installon = LIST_DELIMITER.join(sorted({vdom['name'] + '_' + vdom['vdom'] for vdom in native_rule['scope_member']}))
    return rule_installon

def rule_parse_last_hit(native_rule: dict[str, Any]) -> str|None:
    last_hit = native_rule.get('_last_hit', None)
    if last_hit != None:
        last_hit = strftime("%Y-%m-%d %H:%M:%S", localtime(last_hit))
    return last_hit

def get_access_policy(sid: str, fm_api_url: str, native_config_adom: dict[str, Any], native_config_global: dict[str, Any], adom_device_vdom_policy_package_structure: dict[str, Any], adom_name: str, mgm_details_device: dict[str, Any], device_config: dict[str, Any], limit: int):

    previous_rulebase = None
    link_list: list[Any] = []
    local_pkg_name, global_pkg_name = find_packages(adom_device_vdom_policy_package_structure, adom_name, mgm_details_device)
    options = ['extra info', 'scope member', 'get meta']

    previous_rulebase = get_and_link_global_rulebase(
        'header', previous_rulebase, global_pkg_name, native_config_global, sid, fm_api_url, options, limit, link_list)
    
    previous_rulebase = get_and_link_local_rulebase(
        'rules_adom', previous_rulebase, adom_name, local_pkg_name, native_config_adom, sid, fm_api_url, options, limit, link_list)
    
    previous_rulebase = get_and_link_global_rulebase(
        'footer', previous_rulebase, global_pkg_name, native_config_global, sid, fm_api_url, options, limit, link_list)

    device_config['rulebase_links'].extend(link_list)

def get_and_link_global_rulebase(header_or_footer: str, previous_rulebase: str | None, global_pkg_name: str, native_config_global: dict[str, Any], sid: str, fm_api_url: str, options: list[str], limit: int, link_list: list[Any]) -> Any:
    rulebase_type_prefix = 'rules_global_' + header_or_footer
    if global_pkg_name != '':
        if not is_rulebase_already_fetched(native_config_global['rulebases'], rulebase_type_prefix + '_v4_' + global_pkg_name):
            fmgr_getter.update_config_with_fortinet_api_call(
                native_config_global['rulebases'],
                sid, fm_api_url,
                STRING_PM_CONFIG_GLOBAL_PKG + global_pkg_name + '/global/' + header_or_footer + '/policy',
                rulebase_type_prefix + '_v4_' + global_pkg_name,
                options=options, limit=limit)
        if not is_rulebase_already_fetched(native_config_global['rulebases'], rulebase_type_prefix + '_v6_' + global_pkg_name):
            # delete_v: hier auch options=options?
            fmgr_getter.update_config_with_fortinet_api_call(
                native_config_global['rulebases'],
                sid, fm_api_url,
                STRING_PM_CONFIG_GLOBAL_PKG + global_pkg_name + '/global/' + header_or_footer + '/policy6',
                rulebase_type_prefix + '_v6_' + global_pkg_name,
                limit=limit)
        previous_rulebase = link_rulebase(link_list, native_config_global['rulebases'], global_pkg_name, rulebase_type_prefix, previous_rulebase, True)
    return previous_rulebase

def get_and_link_local_rulebase(rulebase_type_prefix: str, previous_rulebase: str | None, adom_name: str, local_pkg_name: str, native_config_adom: dict[str, Any], sid: str, fm_api_url: str, options: list[str], limit: int, link_list: list[Any]) -> Any:
    if not is_rulebase_already_fetched(native_config_adom['rulebases'], rulebase_type_prefix + '_v4_' + local_pkg_name):
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_adom['rulebases'],
            sid, fm_api_url,
            STRING_PM_CONFIG_ADOM + adom_name + STRING_PKG + local_pkg_name + '/firewall/policy',
            rulebase_type_prefix + '_v4_' + local_pkg_name,
            options=options, limit=limit)
    if not is_rulebase_already_fetched(native_config_adom['rulebases'], rulebase_type_prefix + '_v6_' + local_pkg_name):
        fmgr_getter.update_config_with_fortinet_api_call(
            native_config_adom['rulebases'],
            sid, fm_api_url,
            STRING_PM_CONFIG_ADOM + adom_name + STRING_PKG + local_pkg_name + '/firewall/policy6',
            rulebase_type_prefix + '_v6_' + local_pkg_name,
            limit=limit)
    previous_rulebase = link_rulebase(link_list, native_config_adom['rulebases'], local_pkg_name, rulebase_type_prefix, previous_rulebase, False)
    return previous_rulebase

def find_packages(adom_device_vdom_policy_package_structure: dict[str, Any], adom_name: str, mgm_details_device: dict[str, Any]) -> tuple[str, str]:
    for device in adom_device_vdom_policy_package_structure[adom_name]:
        for vdom in adom_device_vdom_policy_package_structure[adom_name][device]:
            if mgm_details_device['name'] == device + '_' + vdom:
                device_dict =  adom_device_vdom_policy_package_structure[adom_name][device]
                if 'local' in device_dict[vdom] and 'global' in adom_device_vdom_policy_package_structure[adom_name][device][vdom]:
                    return device_dict[vdom]['local'], adom_device_vdom_policy_package_structure[adom_name][device][vdom]['global']
                else:
                    return '', ''
    raise FwoDeviceWithoutLocalPackage('Could not find local package for ' + mgm_details_device['name'] + ' in Fortimanager Config') from None

def is_rulebase_already_fetched(rulebases: list[dict[str, Any]], type: str) -> bool:
    for rulebase in rulebases:
        if rulebase['type'] == type:
            return True
    return False

def link_rulebase(link_list: list[Any], rulebases: list[dict[str, Any]], pkg_name: str, rulebase_type_prefix: str, previous_rulebase: str | None, is_global: bool) -> str|None:
    for version in ['v4', 'v6']:
        full_pkg_name = rulebase_type_prefix + '_' + version + '_' + pkg_name
        has_data = has_rulebase_data(rulebases, full_pkg_name, is_global, version, pkg_name)
        if has_data:
            link_list.append(build_link(previous_rulebase, full_pkg_name, is_global))
            previous_rulebase = full_pkg_name
    
    return previous_rulebase

def build_link(previous_rulebase: str | None, full_pkg_name: str, is_global: bool) -> dict[str, Any]:
    if previous_rulebase is None:
        is_initial = True
        previous_rulebase = None
    else:
        is_initial = False
    return {
        'from_rulebase_uid': previous_rulebase,
        'from_rule_uid': None,
        'to_rulebase_uid': full_pkg_name,
        # 'type': 'concatenated',
        'type': 'ordered',
        'is_global': is_global,
        'is_initial': is_initial,
        'is_section': False
    }

def has_rulebase_data(rulebases: list[dict[str, Any]], full_pkg_name: str, is_global: bool, version: str, pkg_name: str) -> bool:
    """adds name and uid to rulebase and removes empty global rulebases"""
    has_data = False
    if version == 'v4':
        is_v4 = True
    else:
        is_v4 = False
    for rulebase in rulebases:
        if rulebase['type'] == full_pkg_name:
            rulebase.update({'name': full_pkg_name,
                             'uid': full_pkg_name,
                             'is_global': is_global,
                             'is_v4': is_v4,
                             'package': pkg_name})
            if len(rulebase['data']) > 0:
                has_data = True
            elif is_global:
                rulebases.remove(rulebase)
    return has_data

def get_nat_policy(sid: str, fm_api_url: str, native_config: dict[str, Any], adom_device_vdom_policy_package_structure: dict[str, Any], adom_name: str, mgm_details_device: dict[str, Any], limit: int):
    local_pkg_name, global_pkg_name = find_packages(adom_device_vdom_policy_package_structure, adom_name, mgm_details_device)
    if adom_name == '':
        for nat_type in nat_types:
            fmgr_getter.update_config_with_fortinet_api_call(
                native_config['nat_rulebases'], sid, fm_api_url,
                STRING_PM_CONFIG_GLOBAL_PKG + global_pkg_name + '/' + nat_type,
                nat_type + '_global_' + global_pkg_name, limit=limit)
    else:
        for nat_type in nat_types:
            fmgr_getter.update_config_with_fortinet_api_call(
                native_config['nat_rulebases'], sid, fm_api_url,
                STRING_PM_CONFIG_ADOM + adom_name + STRING_PKG + local_pkg_name + '/' + nat_type,
                nat_type + '_adom_' + adom_name + '_' + local_pkg_name, limit=limit)

# delete_v: ab hier kann sehr viel weg, ich lasses vorerst zB für die nat
# pure nat rules 

def parse_nat_rulebase(nat_rulebase: list[dict[str, Any]], nat_type_string: str, normalized_config_adom: dict[str, Any], normalized_config_global: dict[str, Any]) -> None:
    # this function is not called until it is ready
    return
    # the following is a first draft and is not yet functional
    # nat_rules = []
    # rule_number = 0
    # for rule_orig in nat_rulebase:

    #     rule_src_list, rule_src_refs_list = rule_parse_addresses(rule_orig, 'src', normalized_config_adom, normalized_config_global, True)
    #     rule_dst_list, rule_dst_refs_list = rule_parse_addresses(rule_orig, 'dst', normalized_config_adom, normalized_config_global, True)

    #     rule_normalized = RuleNormalized(
    #         rule_num=rule_number,
    #         rule_num_numeric=0,
    #         rule_disabled=False,
    #         rule_src_neg=False,
    #         rule_src=list_delimiter.join(rule_src_list),
    #         rule_src_refs=list_delimiter.join(rule_src_refs_list),
    #         rule_dst_neg=False,
    #         rule_dst=list_delimiter.join(rule_dst_list),
    #         rule_dst_refs=list_delimiter.join(rule_dst_refs_list),
    #         rule_svc_neg=False,
    #         rule_svc=list_delimiter.join(rule_svc_list),
    #         rule_svc_refs=list_delimiter.join(rule_svc_refs_list),
    #         rule_action=RuleAction.DROP,
    #         rule_track=RuleTrack.NONE,
    #         rule_installon=nat_type_string,
    #         rule_time='',  # Time-based rules not commonly used in basic Fortinet configs
    #         rule_name=rule_orig.get('name', ''),
    #         rule_uid=rule_orig.get('uuid'),
    #         rule_custom_fields=str({}),
    #         rule_implied=False,
    #         rule_type=RuleType.NAT,
    #         last_change_admin=native_rule.get('_last-modified-by', ''),
    #         parent_rule_uid=None,
    #         last_hit=last_hit,
    #         rule_comment=native_rule.get('comments'),
    #         rule_src_zone=list_delimiter.join(rule_src_zones),
    #         rule_dst_zone=list_delimiter.join(rule_dst_zones),
    #         rule_head_text=None
    #     )


    # # for rule_table in rule_nat_scope:
    # #     for localPkgName in native_config['rules_global_nat']:
    # for rule_orig in nat_rulebase:
    #     rule = {'rule_src': '', 'rule_dst': '', 'rule_svc': ''}
    #     if rule_orig['nat'] == 1:   # assuming source nat
    #         rule.update({ 'rule_ruleid': rule_orig['policyid']})
    #         rule.update({ 'rule_uid': rule_orig['uuid']})
    #         # rule.update({ 'rule_num': rule_orig['obj seq']})
    #         if 'comments' in rule_orig:
    #             rule.update({ 'rule_comment': rule_orig['comments']})
    #         rule.update({ 'rule_action': 'Drop' })  # not used for nat rules
    #         rule.update({ 'rule_track': 'None'}) # not used for nat rules

    #         rule['rule_src'] = extend_string_list(rule['rule_src'], rule_orig, 'orig-addr', list_delimiter, jwt=jwt, import_id=import_id)
    #         rule['rule_dst'] = extend_string_list(rule['rule_dst'], rule_orig, 'dst-addr', list_delimiter, jwt=jwt, import_id=import_id)

    #         if rule_orig['protocol']==17:
    #             svc_name = 'udp_' + str(rule_orig['orig-port'])
    #         elif rule_orig['protocol']==6:
    #             svc_name = 'tcp_' + str(rule_orig['orig-port'])
    #         else:
    #             svc_name = 'svc_' + str(rule_orig['orig-port'])
    #         # need to create a helper service object and add it to the nat rule, also needs to be added to service list

    #         if not 'service_objects' in normalized_config_adom: # is normally defined
    #             normalized_config_adom['service_objects'] = []
    #         normalized_config_adom['service_objects'].append(create_svc_object( \
    #             import_id=import_id, name=svc_name, proto=rule_orig['protocol'], port=rule_orig['orig-port'], comment='service created by FWO importer for NAT purposes'))
    #         rule['rule_svc'] = svc_name

    #         #rule['rule_src'] = extend_string_list(rule['rule_src'], rule_orig, 'srcaddr6', list_delimiter, jwt=jwt, import_id=import_id)
    #         #rule['rule_dst'] = extend_string_list(rule['rule_dst'], rule_orig, 'dstaddr6', list_delimiter, jwt=jwt, import_id=import_id)

    #         if len(rule_orig['srcintf'])>0:
    #             rule.update({ 'rule_from_zone': rule_orig['srcintf'][0] }) # todo: currently only using the first zone
    #         if len(rule_orig['dstintf'])>0:
    #             rule.update({ 'rule_to_zone': rule_orig['dstintf'][0] }) # todo: currently only using the first zone

    #         rule.update({ 'rule_src_neg': False})
    #         rule.update({ 'rule_dst_neg': False})
    #         rule.update({ 'rule_svc_neg': False})
    #         rule.update({ 'rule_src_refs': resolve_raw_objects(rule['rule_src'], list_delimiter, native_config, 'name', 'uuid', rule_type=rule_table) }, \
    #             jwt=jwt, import_id=import_id, rule_uid=rule_orig['uuid'], object_type=NETWORK_OBJECT)
    #         rule.update({ 'rule_dst_refs': resolve_raw_objects(rule['rule_dst'], list_delimiter, native_config, 'name', 'uuid', rule_type=rule_table) }, \
    #             jwt=jwt, import_id=import_id, rule_uid=rule_orig['uuid'], object_type=NETWORK_OBJECT)
    #         # services do not have uids, so using name instead
    #         rule.update({ 'rule_svc_refs': rule['rule_svc'] })
    #         rule.update({ 'rule_type': 'original' })
    #         rule.update({ 'rule_installon': localPkgName })
    #         if 'status' in rule_orig and (rule_orig['status']=='enable' or rule_orig['status']==1):
    #             rule.update({ 'rule_disabled': False })
    #         else:
    #             rule.update({ 'rule_disabled': True })
    #         rule.update({ 'rule_implied': False })
    #         rule.update({ 'rule_time': None })
    #         rule.update({ 'parent_rule_id': None })

    #         nat_rules.append(rule)
    #         add_users_to_rule(rule_orig, rule)

    #         ############## now adding the xlate rule part ##########################
    #         xlate_rule = dict(rule) # copy the original (match) rule
    #         xlate_rule.update({'rule_src': '', 'rule_dst': '', 'rule_svc': ''})
    #         xlate_rule['rule_src'] = extend_string_list(xlate_rule['rule_src'], rule_orig, 'orig-addr', list_delimiter, jwt=jwt, import_id=import_id)
    #         xlate_rule['rule_dst'] = 'Original'

    #         if rule_orig['protocol']==17:
    #             svc_name = 'udp_' + str(rule_orig['nat-port'])
    #         elif rule_orig['protocol']==6:
    #             svc_name = 'tcp_' + str(rule_orig['nat-port'])
    #         else:
    #             svc_name = 'svc_' + str(rule_orig['nat-port'])
    #         # need to create a helper service object and add it to the nat rule, also needs to be added to service list!
    #         # fmgr_service.create_svc_object(name=svc_name, proto=rule_orig['protocol'], port=rule_orig['orig-port'], comment='service created by FWO importer for NAT purposes')
    #         normalized_config_adom['service_objects'].append(create_svc_object(import_id=import_id, name=svc_name, proto=rule_orig['protocol'], port=rule_orig['nat-port'], comment='service created by FWO importer for NAT purposes'))
    #         xlate_rule['rule_svc'] = svc_name

    #         xlate_rule.update({ 'rule_src_refs': resolve_objects(xlate_rule['rule_src'], list_delimiter, native_config, 'name', 'uuid', rule_type=rule_table, jwt=jwt, import_id=import_id ) })
    #         xlate_rule.update({ 'rule_dst_refs': resolve_objects(xlate_rule['rule_dst'], list_delimiter, native_config, 'name', 'uuid', rule_type=rule_table, jwt=jwt, import_id=import_id ) })
    #         xlate_rule.update({ 'rule_svc_refs': xlate_rule['rule_svc'] })  # services do not have uids, so using name instead

    #         xlate_rule.update({ 'rule_type': 'xlate' })

    #         nat_rules.append(xlate_rule)
    # normalized_config_adom['rules'].extend(nat_rules)

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


def handle_combined_nat_rule(rule: dict[str, Any], rule_orig: dict[str, Any], config2import: dict[str, Any], nat_rule_number: int, dev_id: int) -> dict[str, Any] | None:
    #TODO see fOS_rule for reference implementation
    raise NotImplementedError("handle_combined_nat_rule is not implemented yet")


def extract_nat_objects(nwobj_list: list[str], all_nwobjects: list[dict[str, str]]) -> list[dict[str, str]]:
    nat_obj_list: list[dict[str, str]] = []
    for obj in nwobj_list:
        for obj2 in all_nwobjects:
            if obj2['obj_name']==obj:
                if 'obj_nat_ip' in obj2:
                    nat_obj_list.append(obj2)
                break
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

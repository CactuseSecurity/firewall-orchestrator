from asyncio.log import logger
import json
from typing import Any
import ast

from fwo_log import getFwoLogger
import fwo_const
import fwo_globals
from fwo_const import list_delimiter, default_section_header_text
from fwo_base import sanitize, sort_and_join_refs
from fwo_exceptions import ImportRecursionLimitReached, FwoImporterErrorInconsistencies
from models.rulebase import Rulebase
from models.rule import RuleNormalized
from models.rule_enforced_on_gateway import RuleEnforcedOnGatewayNormalized

uid_to_name_map = {}

"""
    new import format which takes the following cases into account without duplicating any rules in the DB:
    - single rulebase used on more than one gw
    - global policies enforced on more than one gws
    - inline layers (CP)
    - migrate section headers from rule to ordering element 
    ...
"""
def normalize_rulebases (nativeConfig, native_config_global, importState, normalized_config_dict, 
                         normalized_config_global, is_global_loop_iteration):
    
    normalized_config_dict['policies'] = []

    # fill uid_to_name_map:
    for nw_obj in normalized_config_dict['network_objects']:
        uid_to_name_map[nw_obj['obj_uid']] = nw_obj['obj_name']

    fetched_rulebase_uids = []
    if normalized_config_global is not None and normalized_config_global != {}:
        for normalized_rulebase_global in normalized_config_global['policies']:
            fetched_rulebase_uids.append(normalized_rulebase_global.uid)
    for gateway in nativeConfig['gateways']:
        normalize_rulebases_for_each_link_destination(
            gateway, fetched_rulebase_uids, nativeConfig, native_config_global,
            is_global_loop_iteration, importState, normalized_config_dict,
            normalized_config_global)

    # todo: parse nat rulebase here

def normalize_rulebases_for_each_link_destination(
        gateway, fetched_rulebase_uids, nativeConfig, 
        native_config_global, is_global_loop_iteration, importState, normalized_config_dict, normalized_config_global):
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
            normalized_rulebase = initialize_normalized_rulebase(rulebase_to_parse, importState.MgmDetails.Uid)
            parse_rulebase(rulebase_to_parse, is_section, is_placeholder, normalized_rulebase, gateway, nativeConfig['policies'])
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
        if rulebase['uid'] == rulebase_uid:
            return rulebase, False, False
        rulebase_to_parse, is_section, is_placeholder = find_rulebase_to_parse_in_case_of_chunk(rulebase, rulebase_uid)
        if rulebase_to_parse != {}:
            return rulebase_to_parse, is_section, is_placeholder
    
    # handle case: no rulebase found
    return {}, False, False

def find_rulebase_to_parse_in_case_of_chunk(rulebase, rulebase_uid):
    is_section = False
    rulebase_to_parse = {}
    for chunk in rulebase['chunks']:
        for section in chunk['rulebase']:
            if section['uid'] == rulebase_uid:
                if section['type'] == 'place-holder':
                    return section, False, True
                else:
                    rulebase_to_parse, is_section = find_rulebase_to_parse_in_case_of_section(is_section, rulebase_to_parse, section)
    return rulebase_to_parse, is_section, False

def find_rulebase_to_parse_in_case_of_section(is_section, rulebase_to_parse, section):
    if is_section:
        rulebase_to_parse = concatenat_sections_across_chunks(rulebase_to_parse, section)
    else:
        is_section = True
        rulebase_to_parse = section
    return rulebase_to_parse, is_section

def concatenat_sections_across_chunks(rulebase_to_parse, section):
    if 'to' in rulebase_to_parse and 'from' in section:
        if rulebase_to_parse['to'] + 1 == section['from']:
            if rulebase_to_parse['name'] == section['name']:
                for rule in section['rulebase']:
                    rulebase_to_parse['rulebase'].append(rule)
                rulebase_to_parse['to'] = section['to']
            else:
                raise FwoImporterErrorInconsistencies("Inconsistent naming in Checkpoint Chunks.")
        else:
            raise FwoImporterErrorInconsistencies("Inconsistent numbering in Checkpoint Chunks.")
    else:
        raise FwoImporterErrorInconsistencies("Broken format in Checkpoint Chunks.")
    return rulebase_to_parse

                    
def initialize_normalized_rulebase(rulebase_to_parse, mgm_uid):
    rulebaseName = rulebase_to_parse.get('name', default_section_header_text)
    rulebaseUid = rulebase_to_parse['uid']
    normalized_rulebase = Rulebase(uid=rulebaseUid, name=rulebaseName, mgm_uid=mgm_uid, rules={})
    return normalized_rulebase

def parse_rulebase(rulebase_to_parse, is_section, is_placeholder, normalized_rulebase, gateway, policy_structure):
    logger = getFwoLogger()

    if is_section:
        for rule in rulebase_to_parse['rulebase']:
            # delte_v sind import_id, parent_uid, config2import wirklich egal? Dann können wir diese argumente löschen - NAT ACHTUNG
            parse_single_rule(rule, normalized_rulebase, normalized_rulebase.uid, None, gateway, policy_structure)

        if fwo_globals.debug_level>3:
            logger.debug("parsed rulebase " + normalized_rulebase.uid)
        return
    elif is_placeholder:
        parse_single_rule(rulebase_to_parse, normalized_rulebase, normalized_rulebase.uid, None, gateway, policy_structure)
    else:
        parse_rulebase_chunk(rulebase_to_parse, normalized_rulebase, gateway, policy_structure)                    

def parse_rulebase_chunk(rulebase_to_parse, normalized_rulebase, gateway, policy_structure):
    logger = getFwoLogger()
    for chunk in rulebase_to_parse['chunks']:
        for rule in chunk['rulebase']:
            if 'rule-number' in rule:
                parse_single_rule(rule, normalized_rulebase, normalized_rulebase.uid, None, gateway, policy_structure)
            else:
                logger.debug("found unparsable rulebase: " + str(rulebase_to_parse))
    return
 

def acceptMalformedParts(objects: dict, part: str ='') -> dict[str, Any]:
    if fwo_globals.debug_level>9:
        logger.debug(f'about to accept malformed rule part ({part}): {str(objects)}')

    # if we are dealing with a list with one element, resolve the list
    if isinstance(objects, list) and len(objects)==1:
        objects = objects[0]

    if isinstance(objects, dict):
        if part == 'action':
            return { 'action': objects.get('name', None) }
        elif part == 'install-on':
            return { 'install-on': objects.get('name', None) }
        elif part == 'time':
            return { 'time': objects.get('name', None) }
        elif part == 'track':
            return { 'track': objects.get('type', {}).get('name', None) }
        else:
            logger.warning(f'found no uid or name in rule part ({part}): {str(objects)}')
            return {}
    else:
        logger.warning(f'objects is not a dictionary: {str(objects)}')
        return {}


def parseRulePart (objects: dict, part: str = 'source') -> dict[str, Any]:
    addressObjects: dict[str, Any] = {}

    if objects is None:
        logger.debug(f"rule part {part} is None: {str(objects)}, which is normal for track field in inline layer guards")
        return None

    if 'chunks' in objects:  # for chunks of actions?!
        addressObjects.update(parseRulePart(objects['chunks'], part=part)) # need to parse chunk first
        return addressObjects

    if isinstance(objects, dict):
        return _parse_single_address_object(addressObjects, objects, part)
    # assuming list of objects
    if objects is None:
        logger.error(f'rule part {part} is None: {str(objects)}')
        return None
    for obj in objects:
        if obj is None:
            logger.warning(f'found list with a single None obj: {str(objects)}')
            continue

        if 'chunks' in obj:
            addressObjects.update(parseRulePart(obj['chunks'], part=part)) # need to parse chunk first
        elif 'objects' in obj:
            for o in obj['objects']:
                addressObjects.update(parseRulePart(o, part=part)) # need to parse chunk first
            return addressObjects
        else:
            if 'type' in obj: # found checkpoint object
                _parse_obj_with_type(obj, addressObjects)
            else:
                return acceptMalformedParts(objects, part=part)

    if '' in addressObjects.values():
        logger.warning('found empty name in one rule part (' + part + '): ' + str(addressObjects))

    return addressObjects


def _parse_single_address_object(addressObjects: dict[str,Any], objects: dict[str,Any], part: str):
    if 'uid' in objects and 'name' in objects:
        addressObjects[objects['uid']] = objects['name']
        return addressObjects
    else:
        return acceptMalformedParts(objects, part=part)


def _parse_obj_with_type(obj: dict[str,Any], addressObjects: dict[str,Any]) -> None:
    
    if obj['type'] == 'LegacyUserAtLocation':
        addressObjects[obj['uid']] = obj['name']

    elif obj['type'] == 'access-role':
        _parse_obj_with_access_role(obj, addressObjects)
    else:  # standard object
        addressObjects[obj['uid']] = obj['name']


def _parse_obj_with_access_role(obj: dict[str,Any], addressObjects: dict[str,Any]) -> None:
    if 'networks' not in obj:
        addressObjects[obj['uid']] = obj['name'] # adding IA without IP info, TODO: get full networks details here!
        return
    if isinstance(obj['networks'], str):  # just a single source
        if obj['networks'] == 'any':
            addressObjects[obj['uid']] = obj['name'] + '@' + 'Any'
        else:
            addressObjects[obj['uid']] = obj['name'] + '@' + obj['networks']
    else:  # more than one source
        for nw in obj['networks']:
            nw_resolved = resolveNwObjUidToName(nw)
            if nw_resolved == "":
                addressObjects[obj['uid']] = obj['name']
            else:
                addressObjects[obj['uid']] = obj['name'] + '@' + nw_resolved


def parse_single_rule(nativeRule, rulebase, layer_name, parent_uid, gateway, policy_structure):
    logger = getFwoLogger()

    # reference to domain rule layer, filling up basic fields
    if not('type' in nativeRule and nativeRule['type'] != 'place-holder' and 'rule-number' in nativeRule):  # standard rule, no section header
        return
    # the following objects might come in chunks:
    sourceObjects: dict[str, str] = parseRulePart (nativeRule['source'], 'source')
    rule_src_ref, rule_src_name = sort_and_join_refs(list(sourceObjects.items()))
    
    destObjects: dict[str, str] = parseRulePart (nativeRule['destination'], 'destination')
    rule_dst_ref, rule_dst_name = sort_and_join_refs(list(destObjects.items()))
    svcObjects: dict[str, str] = parseRulePart (nativeRule['service'], 'service')
    rule_svc_ref, rule_svc_name = sort_and_join_refs(list(svcObjects.items()))

    ruleEnforcedOnGateways = parse_rule_enforced_on_gateway(gateway, policy_structure, native_rule=nativeRule)
    listOfGwUids = sorted({enforceEntry.dev_uid for enforceEntry in ruleEnforcedOnGateways})
    strListOfGwUids = list_delimiter.join(listOfGwUids) if listOfGwUids else None

    rule_track = _parse_track(native_rule=nativeRule)

    actionObjects = parseRulePart (nativeRule['action'], 'action')
    if actionObjects is not None:
        rule_action = list_delimiter.join(actionObjects.values()) # expecting only a single action
    else:
        rule_action = None
        logger.warning('found rule without action: ' + str(nativeRule))

    timeObjects = parseRulePart (nativeRule['time'], 'time')
    rule_time = list_delimiter.join(timeObjects.values()) if timeObjects else None

    # starting with the non-chunk objects
    rule_name = nativeRule.get('name', None)

    # new in v8.0.3:
    rule_custom_fields = nativeRule.get('custom-fields', None)

    # we leave out all last_admin info for now
    # if 'meta-info' in nativeRule and 'last-modifier' in nativeRule['meta-info']:
    #     last_change_admin = nativeRule['meta-info']['last-modifier']
    # else:
    last_change_admin = None

    parent_rule_uid = _parse_parent_rule_uid(parent_uid, native_rule=nativeRule)


    # new in v5.5.1:
    rule_type = nativeRule.get('rule_type', 'access')

    comments = nativeRule.get('comments', None)
    if comments == '':
        comments = None

    if 'hits' in nativeRule and 'last-date' in nativeRule['hits'] and 'iso-8601' in nativeRule['hits']['last-date']:
        last_hit = nativeRule['hits']['last-date']['iso-8601']
    else:
        last_hit = None

    rule = {
        "rule_num":         0,
        "rule_num_numeric": 0,
        "rulebase_name":    sanitize(layer_name),
        "rule_disabled": not bool(nativeRule['enabled']),
        "rule_src_neg":     bool(nativeRule['source-negate']),
        "rule_src":         sanitize(rule_src_name),
        "rule_src_refs":    sanitize(rule_src_ref),
        "rule_dst_neg":     bool(nativeRule['destination-negate']),
        "rule_dst":         sanitize(rule_dst_name),
        "rule_dst_refs":    sanitize(rule_dst_ref),
        "rule_svc_neg":     bool(nativeRule['service-negate']),
        "rule_svc":         sanitize(rule_svc_name),
        "rule_svc_refs":    sanitize(rule_svc_ref),
        "rule_action":      sanitize(rule_action, lower=True),
        "rule_track":       sanitize(rule_track, lower=True),
        "rule_installon":   sanitize(strListOfGwUids),
        "rule_time":        sanitize(rule_time),
        "rule_name":        sanitize(rule_name),
        "rule_uid":         sanitize(nativeRule['uid']),
        "rule_custom_fields": sanitize(rule_custom_fields),
        "rule_implied":     False,
        "rule_type":        sanitize(rule_type),
        "last_change_admin": sanitize(last_change_admin),
        "parent_rule_uid":  sanitize(parent_rule_uid),
        "last_hit":         sanitize(last_hit)
    }
    if comments is not None:
        rule['rule_comment'] = sanitize(comments)
    rulebase.rules.update({ rule['rule_uid']: RuleNormalized(**rule)})

    return


def _parse_parent_rule_uid(parent_uid: str, native_rule: dict[str,Any]) -> str|None:

    # new in v5.1.17:
    if 'parent_rule_uid' in native_rule:
        logger.debug(
            'found rule (uid=' + native_rule['uid'] + ') with parent_rule_uid set: ' + native_rule['parent_rule_uid'])
        parent_rule_uid = native_rule['parent_rule_uid']
    else:
        parent_rule_uid = parent_uid

    if parent_rule_uid == '':
        parent_rule_uid = None
    
    return parent_rule_uid


def _parse_track(native_rule: dict[str, Any]) -> str:
    if isinstance(native_rule['track'],str):
        rule_track = native_rule['track']
    else:
        trackObjects = parseRulePart (native_rule['track'], 'track')
        if trackObjects is None:
            rule_track = 'none'
        else:
            rule_track = list_delimiter.join(trackObjects.values())
    return rule_track


def parse_rule_enforced_on_gateway(gateway, policy_structure, native_rule: dict) -> list[RuleEnforcedOnGatewayNormalized]:
    """Parse rule enforcement information from native rule.
    
    Args:
        native_rule: The native rule dictionary containing install-on information
        
    Returns:
        list of RuleEnforcedOnGatewayNormalized objects
    
    Raises:
        ValueError: If nativeRule is None or empty
    """
    if not native_rule:
        raise ValueError('Native rule cannot be empty')

    enforce_entries = []
    all_target_gw_names_dict = parseRulePart(native_rule['install-on'], 'install-on')

    for targetUid in all_target_gw_names_dict:
        targetName = all_target_gw_names_dict[targetUid]
        if targetName == 'Policy Targets': # or target == 'Any'
            device_uid_list = find_devices_for_current_policy(gateway, policy_structure)
            for device_uid in device_uid_list:
                enforceEntry = RuleEnforcedOnGatewayNormalized(rule_uid=native_rule['uid'], dev_uid=device_uid)
                enforce_entries.append(enforceEntry)
        else:
            enforceEntry = RuleEnforcedOnGatewayNormalized(rule_uid=native_rule['uid'], dev_uid=targetUid)
            enforce_entries.append(enforceEntry)
    return enforce_entries

def find_devices_for_current_policy(gateway, policy_structure):
    device_uid_list = []
    for policy in policy_structure:
        for target in policy['targets']:
            if target['uid'] == gateway['uid']:
                for device in policy['targets']:
                    device_uid_list.append(device['uid'])
    return device_uid_list


def resolveNwObjUidToName(nw_obj_uid):
    if nw_obj_uid in uid_to_name_map:
        return uid_to_name_map[nw_obj_uid]
    else:
        logger = getFwoLogger()
        logger.warning("could not resolve network object with uid " + nw_obj_uid)
        return ""
    

# delete_v: left here only for nat case
def check_and_add_section_header(src_rulebase, target_rulebase, layer_name, import_id, section_header_uids, parent_uid):
    # if current rulebase starts a new section, add section header, but only if it does not exist yet (can happen by chunking a section)
    if 'type' in src_rulebase and src_rulebase['type'] == 'access-section' and 'uid' in src_rulebase: # and not src_rulebase['uid'] in section_header_uids:
        section_name = default_section_header_text
        if 'name' in src_rulebase:
            section_name = src_rulebase['name']
        if 'parent_rule_uid' in src_rulebase:
            parent_uid = src_rulebase['parent_rule_uid']
        else:
            parent_uid = ""
        insert_section_header_rule(target_rulebase, section_name, layer_name, import_id, src_rulebase['uid'], section_header_uids, parent_uid)
        parent_uid = src_rulebase['uid']
    return


def insert_section_header_rule(target_rulebase, section_name, layer_name, import_id, src_rulebase_uid, section_header_uids, parent_uid):
    # TODO: re-implement
    return


# def parse_nat_rulebase(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=0, recursion_level=1):

#     if (recursion_level > fwo_const.max_recursion_level):
#         raise ImportRecursionLimitReached(
#             "parse_nat_rulebase_json") from None

#     logger = getFwoLogger()
#     if 'nat_rule_chunks' in src_rulebase:
#         for chunk in src_rulebase['nat_rule_chunks']:
#             if 'rulebase' in chunk:
#                 for rules_chunk in chunk['rulebase']:
#                     rule_num = parse_nat_rulebase(rules_chunk, target_rulebase, layer_name, import_id, rule_num,
#                                                        section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)
#             else:
#                 logger.warning(
#                     "parse_rule: found no rulebase in chunk:\n" + json.dumps(chunk, indent=2))
#     else:
#         if 'rulebase' in src_rulebase:
#             check_and_add_section_header(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)

#             for rule in src_rulebase['rulebase']:
#                 (rule_match, rule_xlate) = parse_nat_rule_transform(rule, rule_num)
#                 rule_num = parse_single_rule(
#                     rule_match, target_rulebase, layer_name, rule_num, parent_uid)
#                 parse_single_rule( # do not increase rule_num here
#                     rule_xlate, target_rulebase, layer_name, rule_num, parent_uid)

#         if 'rule-number' in src_rulebase:   # rulebase is just a single rule (xlate rules do not count)
#             (rule_match, rule_xlate) = parse_nat_rule_transform(
#                 src_rulebase, rule_num)
#             rule_num = parse_single_rule(
#                 rule_match, target_rulebase, layer_name, rule_num, parent_uid)
#             parse_single_rule(  # do not increase rule_num here (xlate rules do not count)
#                 rule_xlate, target_rulebase, layer_name, rule_num, parent_uid)
#     return rule_num


# def parseNatRulebase(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=0, recursion_level=1):

#     if (recursion_level > fwo_const.max_recursion_level):
#         raise ImportRecursionLimitReached(
#             "parseNatRulebase") from None

#     logger = getFwoLogger()
#     if 'nat_rule_chunks' in src_rulebase:
#         for chunk in src_rulebase['nat_rule_chunks']:
#             if 'rulebase' in chunk:
#                 for rules_chunk in chunk['rulebase']:
#                     rule_num = parseNatRulebase(rules_chunk, target_rulebase, layer_name, import_id, rule_num,
#                                                        section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)
#             else:
#                 logger.warning(
#                     "parse_rule: found no rulebase in chunk:\n" + json.dumps(chunk, indent=2))
#     else:
#         if 'rulebase' in src_rulebase:
#             check_and_add_section_header(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)

#             for rule in src_rulebase['rulebase']:
#                 (rule_match, rule_xlate) = parse_nat_rule_transform(rule, rule_num)
#                 rule_num = parse_single_rule(
#                     rule_match, target_rulebase, layer_name, rule_num, parent_uid)
#                 parse_single_rule( # do not increase rule_num here
#                     rule_xlate, target_rulebase, layer_name, rule_num, parent_uid)

#         if 'rule-number' in src_rulebase:   # rulebase is just a single rule (xlate rules do not count)
#             (rule_match, rule_xlate) = parse_nat_rule_transform(
#                 src_rulebase, rule_num)
#             rule_num = parse_single_rule(
#                 rule_match, target_rulebase, layer_name, rule_num, parent_uid)
#             parse_single_rule(  # do not increase rule_num here (xlate rules do not count)
#                 rule_xlate, target_rulebase, layer_name, rule_num, parent_uid)
#     return rule_num


# def parse_nat_rule_transform(xlate_rule_in, rule_num):
#     # todo: cleanup certain fields (install-on, ....)
#     rule_match = {
#         'uid': xlate_rule_in['uid'],
#         'source': [xlate_rule_in['original-source']],
#         'destination': [xlate_rule_in['original-destination']],
#         'service': [xlate_rule_in['original-service']],
#         'action': {'name': 'Drop'},
#         'track': {'type': {'name': 'None'}},
#         'type': 'nat',
#         'rule-number': rule_num,
#         'source-negate': False,
#         'destination-negate': False,
#         'service-negate': False,
#         'install-on': [{'name': 'Policy Targets'}],
#         'time': [{'name': 'Any'}],
#         'enabled': xlate_rule_in['enabled'],
#         'comments': xlate_rule_in['comments'],
#         'rule_type': 'access'
#     }
#     rule_xlate = {
#         'uid': xlate_rule_in['uid'],
#         'source': [xlate_rule_in['translated-source']],
#         'destination': [xlate_rule_in['translated-destination']],
#         'service': [xlate_rule_in['translated-service']],
#         'action': {'name': 'Drop'},
#         'track': {'type': {'name': 'None'}},
#         'type': 'nat',
#         'rule-number': rule_num,
#         'enabled': True,
#         'source-negate': False,
#         'destination-negate': False,
#         'service-negate': False,
#         'install-on': [{'name': 'Policy Targets'}],
#         'time': [{'name': 'Any'}],
#         'rule_type': 'nat'
#     }
#     return (rule_match, rule_xlate)


def ensure_json(raw: str) -> Any:
    """
    Tries to parse the given string as valid JSON.
    Falls back to ast.literal_eval() if the JSON is using single quotes
    or is otherwise not strictly compliant.
    
    Args:
        raw: The input string containing JSON-like data.
    
    Returns:
        The parsed Python object (e.g., dict, list, str, int, etc.).
    
    Raises:
        ValueError: If neither JSON parsing nor literal_eval() succeed.
    """
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        try:
            return ast.literal_eval(raw)
        except (ValueError, SyntaxError) as e:
            raise ValueError(f"Invalid JSON or literal: {e}") from e
        
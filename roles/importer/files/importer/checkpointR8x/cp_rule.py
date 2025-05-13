from asyncio.log import logger
from typing import List

from fwo_log import getFwoLogger
import json
import cp_const
import fwo_const
import fwo_globals
from fwo_const import list_delimiter, default_section_header_text
from fwo_base import sanitize
from fwo_exceptions import ImportRecursionLimitReached
from models.rulebase import Rulebase
from models.rule import RuleNormalized
from models.rule_enforced_on_gateway import RuleEnforcedOnGatewayNormalized
from model_controllers.fwconfig_import_ruleorder import RuleOrderService

uid_to_name_map = {}

"""
    new import format which takes the following cases into account without duplicating any rules in the DB:
    - single rulebase used on more than one gw
    - global policies enforced on more than one gws
    - inline layers (CP)
    - migrate section headers from rule to ordering element 
    ...
"""
def normalizeRulebases (nativeConfig, importState, normalizedConfig):
    if fwo_globals.debug_level>0:
        logger = getFwoLogger()
    rule_num = 0
    parent_uid=None
    section_header_uids=[]
    normalizedConfig['policies'] = []

    # fill uid_to_name_map:
    for nw_obj in normalizedConfig['network_objects']:
        uid_to_name_map[nw_obj['obj_uid']] = nw_obj['obj_name']

    rb_range = range(len(nativeConfig['rulebases']))
    for rb_id in rb_range:
        rulebaseName = nativeConfig['rulebases'][rb_id]['name']
        rulebaseUid = nativeConfig['rulebases'][rb_id]['uid']
        accessPolicy = Rulebase(uid=rulebaseUid, name=rulebaseName, mgm_uid=importState.MgmDetails.Uid, Rules=[])

        if fwo_globals.debug_level>3:
            logger.debug("parsing layer " + rulebaseUid)

        # parse access rules
        rule_num = parseAccessRulebase(
            nativeConfig['rulebases'][rb_id], accessPolicy, rulebaseUid,
            importState.ImportId, section_header_uids, parent_uid, normalizedConfig, 0)
        
        if fwo_globals.debug_level>3:
            logger.debug("parsed " + str(rule_num) + " rules for layer " + rulebaseUid)
        
        normalizedConfig['policies'].append(accessPolicy)

        # now parse the nat rulebase

        # parse nat rules
        # natPolicy = Rulebase(uid=rulebaseUid, name=rulebaseUid, mgm_uid=importState.MgmDetails.Name, Rules=[])
        # if len(nativeConfig['nat_rulebases'])>0:
        #     if len(nativeConfig['nat_rulebases']) != len(rb_range):
        #         pass
        #         # logger.warning('get_config - found ' + str(len(nativeConfig['nat_rulebases'])) +
        #         #     ' nat rulebases and ' +  str(len(rb_range)) + ' access rulebases')
        #     else:
        #         rule_num = parseNatRulebase(
        #             nativeConfig['nat_rulebases'][rb_id], natPolicy, nativeConfig['rulebases'][rb_id]['layername'], 
        #             importState.ImportId, rule_num, section_header_uids, parent_uid, normalizedConfig)
        #         # TODO: do we have to add the nat rulebase here?!



def acceptMalformedParts(objects, part=''):
    # logger.debug('about to accept malformed rule part (' + part + '): ' + str(objects))

    # if we are dealing with a list with one element, resolve the list
    if isinstance(objects, list) and len(objects)==1:
        objects = objects[0]

    if part == 'action' and 'name' in objects:
        return { 'action': objects['name'] }
    elif part == 'install-on' and 'name' in objects:
        return { 'install-on': objects['name'] }
    elif part == 'time' and 'name' in objects:
        return { 'time': objects['name'] }
    elif part == 'track' and 'type' in objects and 'name' in objects['type']:
        return { 'track': objects['type']['name'] }
    else:
        logger.warning('found no uid or name in rule part (' + part + '): ' + str(objects))


def parseRulePart (objects, part='source'):
    addressObjects = {}

    if 'object_chunks' in objects:  # for chunks of actions?!
        return addressObjects.update(parseRulePart(objects['object_chunks'], part=part)) # need to parse chunk first

    if isinstance(objects, dict): # a single address object
        if 'uid' in objects and 'name' in objects:
            addressObjects[objects['uid']] = objects['name']
            return addressObjects
        else:
            return acceptMalformedParts(objects, part=part)

    else:   # assuming list of objects
        if objects is None:
            logger.error("rule part " + part + " is None: " + str(objects))
            return None
        else:
            for obj in objects:
                if obj is not None:
                    if 'object_chunks' in obj:
                        addressObjects.update(parseRulePart(obj['object_chunks'], part=part)) # need to parse chunk first
                    elif 'objects' in obj:
                        for o in obj['objects']:
                            addressObjects.update(parseRulePart(o, part=part)) # need to parse chunk first
                        return addressObjects
                    else:
                        if 'type' in obj: # found checkpoint object

                            if obj['type'] == 'LegacyUserAtLocation':
                                addressObjects[obj['uid']] = obj['name']

                            elif obj['type'] == 'access-role':
                                if 'networks' in obj:
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
                                else:
                                    addressObjects[obj['uid']] = obj['name'] # adding IA without IP info, TODO: get full networks details here!
                            else:  # standard object
                                addressObjects[obj['uid']] = obj['name']
                        else:
                            return acceptMalformedParts(objects, part=part)
                else:
                    logger.warning(f"found list with a single None obj")


    if '' in addressObjects.values():
        logger.warning('found empty name in one rule part (' + part + '): ' + str(addressObjects))

    return addressObjects


def parse_single_rule(nativeRule, rulebase, layer_name, import_id, rule_num, parent_uid, config2import, debug_level=0):
    logger = getFwoLogger()
    rule_order_service = RuleOrderService()

    # reference to domain rule layer, filling up basic fields
    if 'type' in nativeRule and nativeRule['type'] != 'place-holder':
        if 'rule-number' in nativeRule:  # standard rule, no section header

            # the following objects might come in chunks:
            sourceObjects = parseRulePart (nativeRule['source'], 'source')
            rule_src_ref = list_delimiter.join(sourceObjects.keys())
            rule_src_name = list_delimiter.join(sourceObjects.values())

            destObjects = parseRulePart (nativeRule['destination'], 'destination')
            rule_dst_ref = list_delimiter.join(destObjects.keys())
            rule_dst_name = list_delimiter.join(destObjects.values())

            svcObjects = parseRulePart (nativeRule['service'], 'service')
            rule_svc_ref = list_delimiter.join(svcObjects.keys())
            rule_svc_name = list_delimiter.join(svcObjects.values())

            # targetObjects = parseRulePart (nativeRule['install-on'], 'install-on')
            # rule_installon = list_delimiter.join(targetObjects.values())
            ruleEnforcedOnGateways = parse_rule_enforced_on_gateway(native_rule=nativeRule)
            listOfGwUids = []
            for enforceEntry in ruleEnforcedOnGateways:
                listOfGwUids.append(enforceEntry.dev_uid)
            strListOfGwUids = list_delimiter.join(listOfGwUids)

            if isinstance(nativeRule['track'],str):
                rule_track = nativeRule['track']
            else:
                trackObjects = parseRulePart (nativeRule['track'], 'track')
                if trackObjects is None:
                    rule_track = 'none'
                else:
                    rule_track = list_delimiter.join(trackObjects.values())

            actionObjects = parseRulePart (nativeRule['action'], 'action')
            if actionObjects is not None:
                rule_action = list_delimiter.join(actionObjects.values()) # expecting only a single action
            else:
                rule_action = None
                logger.warning('found rule without action: ' + str(nativeRule))

            timeObjects = parseRulePart (nativeRule['time'], 'time')
            rule_time = list_delimiter.join(timeObjects.values())   # only considering the first time object

            # starting with the non-chunk objects
            rule_name = nativeRule.get('name', None)

            # new in v8.0.3:
            rule_custom_fields = nativeRule.get('custom-fields', None)

            if 'meta-info' in nativeRule and 'last-modifier' in nativeRule['meta-info']:
                rule_last_change_admin = nativeRule['meta-info']['last-modifier']
            else:
                rule_last_change_admin = None

            # new in v5.1.17:
            if 'parent_rule_uid' in nativeRule:
                logger.debug(
                    'found rule (uid=' + nativeRule['uid'] + ') with parent_rule_uid set: ' + nativeRule['parent_rule_uid'])
                parent_rule_uid = nativeRule['parent_rule_uid']
            else:
                parent_rule_uid = parent_uid
            if parent_rule_uid == '':
                parent_rule_uid = None

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
                # "control_id":       int(import_id),
                "rule_num":         int(rule_num),
                "rule_num_numeric": 0,
                "rulebase_name":    sanitize(layer_name),
                # rule_ruleid
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
                "rule_action":      sanitize(rule_action).lower(),
                # "rule_track":       sanitize(nativeRule['track']['type']),
                "rule_track":       sanitize(rule_track).lower(),
                # "rule_installon":   sanitize(rule_installon),
                "rule_installon":   sanitize(strListOfGwUids),
                "rule_time":        sanitize(rule_time),
                "rule_name":        sanitize(rule_name),
                "rule_uid":         sanitize(nativeRule['uid']),
                "rule_custom_fields": sanitize(rule_custom_fields),
                "rule_implied":     False,
                "rule_type":        sanitize(rule_type),
                # "rule_head_text": sanitize(section_name),
                # rule_from_zone
                # rule_to_zone
                "rule_last_change_admin": sanitize(rule_last_change_admin),
                "parent_rule_uid":  sanitize(parent_rule_uid),
                "last_hit":         sanitize(last_hit)
            }
            if comments is not None:
                rule['rule_comment'] = sanitize(comments)
            rulebase.Rules.update({ rule['rule_uid']: RuleNormalized(**rule)})

            return rule_num + 1
    return rule_num

def parse_rule_enforced_on_gateway(native_rule: dict) -> List[RuleEnforcedOnGatewayNormalized]:
    """Parse rule enforcement information from native rule.
    
    Args:
        nativeRule: The native rule dictionary containing install-on information
        
    Returns:
        List of RuleEnforcedOnGatewayNormalized objects
    
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
            # TODO: implement the following
            # assuming that the rule is enforced on all gateways of the current management
            # listofEnforcingGwNames = [] # TODO: getAllGatewayNamesForManagement(mgmId)

            # workaround: simply add the uid of "Policy Targets" here
            enforceEntry = RuleEnforcedOnGatewayNormalized(rule_uid=native_rule['uid'], dev_uid=targetUid)
            enforce_entries.append(enforceEntry)
        else:
            enforceEntry = RuleEnforcedOnGatewayNormalized(rule_uid=native_rule['uid'], dev_uid=targetUid)
            enforce_entries.append(enforceEntry)
    return enforce_entries

def resolveNwObjUidToName(nw_obj_uid):
    if nw_obj_uid in uid_to_name_map:
        return uid_to_name_map[nw_obj_uid]
    else:
        logger = getFwoLogger()
        logger.warning("could not resolve network object with uid " + nw_obj_uid)
        return ""


def insert_section_header_rule(rulebase, section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid):
    section_header_uids.append(sanitize(rule_uid))
    rule = {
        "control_id":       int(import_id),
        "rule_num":         int(rule_num),
        "rulebase_name":    sanitize(layer_name),
        "rule_disabled":    False,
        "rule_src_neg":     False,
        "rule_src":         "Any",
        "rule_src_refs":    sanitize(cp_const.any_obj_uid),
        "rule_dst_neg":     False,
        "rule_dst":         "Any",
        "rule_dst_refs":    sanitize(cp_const.any_obj_uid),
        "rule_svc_neg":     False,
        "rule_svc":         "Any",
        "rule_svc_refs":    sanitize(cp_const.any_obj_uid),
        "rule_action":      "Accept",
        "rule_track":       "Log",
        "rule_installon":   "Policy Targets",
        "rule_time":        "Any",
        "rule_implied":      False,
        "rule_uid":         sanitize(rule_uid),
        "rule_head_text":   sanitize(section_name),
        "parent_rule_uid":  sanitize(parent_uid)
    }
    rulebase.append(rule)
    return rule_num + 1


def insertSectionHeaderRule(rulebase, section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid):
    section_header_uids.append(sanitize(rule_uid))
    rule = {
        # "control_id":       int(import_id),
        "rule_num":         int(rule_num),
        "rulebase_name":    sanitize(layer_name),
        # rule_ruleid
        "rule_disabled":    False,
        "rule_src_neg":     False,
        "rule_src":         "Any",
        "rule_src_refs":    sanitize(cp_const.any_obj_uid),
        "rule_dst_neg":     False,
        "rule_dst":         "Any",
        "rule_dst_refs":    sanitize(cp_const.any_obj_uid),
        "rule_svc_neg":     False,
        "rule_svc":         "Any",
        "rule_svc_refs":    sanitize(cp_const.any_obj_uid),
        "rule_action":      "Accept",
        "rule_track":       "Log",
        "rule_installon":   "Policy Targets",
        "rule_time":        "Any",
        "rule_implied":      False,
        # "rule_comment":     None,
        # rule_name
        "rule_uid":         sanitize(rule_uid),
        "rule_head_text":   sanitize(section_name),
        # rule_from_zone
        # rule_to_zone
        # rule_last_change_admin
        "parent_rule_uid":  sanitize(parent_uid)
    }
    # rulebase.Rules.append(rule)
    rulebase.Rules.update({ rule['rule_uid']: RuleNormalized(**rule)})
    return rule_num + 1

def add_domain_rule_header_rule(rulebase, section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid):
    return insert_section_header_rule(rulebase, section_name, layer_name,
                                    import_id, rule_uid, rule_num, section_header_uids, parent_uid)


def addDomainRuleHeaderRule(rulebase, section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid):
    return insertSectionHeaderRule(rulebase, section_name, layer_name,
                                    import_id, rule_uid, rule_num, section_header_uids, parent_uid)


def checkAndAddSectionHeader(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=0, recursion_level=1):
    # if current rulebase starts a new section, add section header, but only if it does not exist yet (can happen by chunking a section)
    if 'type' in src_rulebase and src_rulebase['type'] == 'access-section' and 'uid' in src_rulebase: # and not src_rulebase['uid'] in section_header_uids:
        section_name = default_section_header_text
        if 'name' in src_rulebase:
            section_name = src_rulebase['name']
        if 'parent_rule_uid' in src_rulebase:
            parent_uid = src_rulebase['parent_rule_uid']
        else:
            parent_uid = ""
        rule_num = insertSectionHeaderRule(target_rulebase, section_name, layer_name, import_id, src_rulebase['uid'], rule_num, section_header_uids, parent_uid)
        parent_uid = src_rulebase['uid']
    return rule_num


def parseAccessRulebase(src_rulebase, target_rulebase, layer_name, import_id, section_header_uids, parent_uid, config2import, 
                    rule_num=0, debug_level=0, recursion_level=1, layer_disabled=False):
    logger = getFwoLogger()
    if (recursion_level > fwo_const.max_recursion_level):
        raise ImportRecursionLimitReached("parse_rulebase") from None

    if 'rule-number' in src_rulebase:   # rulebase is just a single rule
        rule_num = parse_single_rule(src_rulebase, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import)
        logger.warning('found single rule in rulebase: ' + str(src_rulebase))
        return rule_num

    # parse chunks
    if 'chunks' in src_rulebase:   # found chunks of layers which need to be parsed separately
        for chunk in src_rulebase['chunks']:
            rule_num = parseAccessRulebase(chunk['rulebase'], target_rulebase, layer_name, import_id,
                                            section_header_uids, parent_uid, config2import, rule_num, debug_level=debug_level, recursion_level=recursion_level+1)
        return rule_num
      
    checkAndAddSectionHeader(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)

    # parse layered rulebase
    if 'rulebase' in src_rulebase:
        logger.warning('found rulebase in rulebase: ' + str(src_rulebase))
        # layer_disabled = not src_rulebase['enabled']
        for element in src_rulebase['rulebase']:
            if 'type' in element:
                if element['type'] == 'place-holder':  # add domain rules
                    section_name = ""
                    if 'name' in src_rulebase:
                        section_name = element['name']
                    rule_num = add_domain_rule_header_rule(
                        target_rulebase, section_name, layer_name, import_id, element['uid'], rule_num, section_header_uids, parent_uid)
                else:  # parse standard sections
                    rule_num = parse_single_rule(
                        element, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import, debug_level=debug_level)
            if 'rulebase' in element:  # always check if a rule contains another layer
                rule_num = parseAccessRulebase(element, target_rulebase, layer_name, import_id, section_header_uids, parent_uid, config2import, rule_num, debug_level=debug_level, recursion_level=recursion_level+1)

    for element in src_rulebase:   # rulebase is a list
        if element['type'] == 'access-section' and 'rulebase' in element:
            if 'rulebase' in element:
                rule_num = parseAccessRulebase(element['rulebase'], target_rulebase, layer_name, import_id, section_header_uids, parent_uid, config2import, rule_num, debug_level=debug_level, recursion_level=recursion_level+1)
        else:
            parse_single_rule(element, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import, debug_level=debug_level)
            rule_num += 1

    return rule_num


def parse_nat_rulebase(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=0, recursion_level=1):

    if (recursion_level > fwo_const.max_recursion_level):
        raise ImportRecursionLimitReached(
            "parse_nat_rulebase_json") from None

    logger = getFwoLogger()
    if 'nat_rule_chunks' in src_rulebase:
        for chunk in src_rulebase['nat_rule_chunks']:
            if 'rulebase' in chunk:
                for rules_chunk in chunk['rulebase']:
                    rule_num = parse_nat_rulebase(rules_chunk, target_rulebase, layer_name, import_id, rule_num,
                                                       section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)
            else:
                logger.warning(
                    "parse_rule: found no rulebase in chunk:\n" + json.dumps(chunk, indent=2))
    else:
        if 'rulebase' in src_rulebase:
            check_and_add_section_header(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)

            for rule in src_rulebase['rulebase']:
                (rule_match, rule_xlate) = parse_nat_rule_transform(rule, rule_num)
                rule_num = parse_single_rule(
                    rule_match, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import)
                parse_single_rule( # do not increase rule_num here
                    rule_xlate, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import)

        if 'rule-number' in src_rulebase:   # rulebase is just a single rule (xlate rules do not count)
            (rule_match, rule_xlate) = parse_nat_rule_transform(
                src_rulebase, rule_num)
            rule_num = parse_single_rule(
                rule_match, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import)
            parse_single_rule(  # do not increase rule_num here (xlate rules do not count)
                rule_xlate, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import)
    return rule_num


def parseNatRulebase(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=0, recursion_level=1):

    if (recursion_level > fwo_const.max_recursion_level):
        raise ImportRecursionLimitReached(
            "parseNatRulebase") from None

    logger = getFwoLogger()
    if 'nat_rule_chunks' in src_rulebase:
        for chunk in src_rulebase['nat_rule_chunks']:
            if 'rulebase' in chunk:
                for rules_chunk in chunk['rulebase']:
                    rule_num = parseNatRulebase(rules_chunk, target_rulebase, layer_name, import_id, rule_num,
                                                       section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)
            else:
                logger.warning(
                    "parse_rule: found no rulebase in chunk:\n" + json.dumps(chunk, indent=2))
    else:
        if 'rulebase' in src_rulebase:
            checkAndAddSectionHeader(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)

            for rule in src_rulebase['rulebase']:
                (rule_match, rule_xlate) = parse_nat_rule_transform(rule, rule_num)
                rule_num = parse_single_rule(
                    rule_match, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import)
                parse_single_rule( # do not increase rule_num here
                    rule_xlate, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import)

        if 'rule-number' in src_rulebase:   # rulebase is just a single rule (xlate rules do not count)
            (rule_match, rule_xlate) = parse_nat_rule_transform(
                src_rulebase, rule_num)
            rule_num = parse_single_rule(
                rule_match, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import)
            parse_single_rule(  # do not increase rule_num here (xlate rules do not count)
                rule_xlate, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import)
    return rule_num


def parse_nat_rule_transform(xlate_rule_in, rule_num):
    # todo: cleanup certain fields (install-on, ....)
    rule_match = {
        'uid': xlate_rule_in['uid'],
        'source': [xlate_rule_in['original-source']],
        'destination': [xlate_rule_in['original-destination']],
        'service': [xlate_rule_in['original-service']],
        'action': {'name': 'Drop'},
        'track': {'type': {'name': 'None'}},
        'type': 'nat',
        'rule-number': rule_num,
        'source-negate': False,
        'destination-negate': False,
        'service-negate': False,
        'install-on': [{'name': 'Policy Targets'}],
        'time': [{'name': 'Any'}],
        'enabled': xlate_rule_in['enabled'],
        'comments': xlate_rule_in['comments'],
        'rule_type': 'access'
    }
    rule_xlate = {
        'uid': xlate_rule_in['uid'],
        'source': [xlate_rule_in['translated-source']],
        'destination': [xlate_rule_in['translated-destination']],
        'service': [xlate_rule_in['translated-service']],
        'action': {'name': 'Drop'},
        'track': {'type': {'name': 'None'}},
        'type': 'nat',
        'rule-number': rule_num,
        'enabled': True,
        'source-negate': False,
        'destination-negate': False,
        'service-negate': False,
        'install-on': [{'name': 'Policy Targets'}],
        'time': [{'name': 'Any'}],
        'rule_type': 'nat'
    }
    return (rule_match, rule_xlate)

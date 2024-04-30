from asyncio.log import logger
from fwo_log import getFwoLogger
import json
import cp_const
import fwo_const
import fwo_globals
from fwo_const import list_delimiter, default_section_header_text
from fwo_base import sanitize
from fwo_exception import ImportRecursionLimitReached

uid_to_name_map = {}


def normalize_rulebases_top_level (full_config, current_import_id, config2import):
    logger = getFwoLogger()
    target_rulebase = []
    rule_num = 0
    parent_uid=""
    section_header_uids=[]

    # fill uid_to_name_map:
    for nw_obj in config2import['network_objects']:
        uid_to_name_map[nw_obj['obj_uid']] = nw_obj['obj_name']

    rb_range = range(len(full_config['rulebases']))
    for rb_id in rb_range:
        # if current_layer_name == args.rulebase:
        if fwo_globals.debug_level>3:
            logger.debug("parsing layer " + full_config['rulebases'][rb_id]['layername'])

        # parse access rules
        rule_num = parse_rulebase(
            full_config['rulebases'][rb_id], target_rulebase, full_config['rulebases'][rb_id]['layername'], 
            current_import_id, rule_num, section_header_uids, parent_uid, config2import)
        # now parse the nat rulebase

        # parse nat rules
        if len(full_config['nat_rulebases'])>0:
            if len(full_config['nat_rulebases']) != len(rb_range):
                logger.warning('get_config - found ' + str(len(full_config['nat_rulebases'])) +
                    ' nat rulebases and ' +  str(len(rb_range)) + ' access rulebases')
            else:
                rule_num = parse_nat_rulebase(
                    full_config['nat_rulebases'][rb_id], target_rulebase, full_config['rulebases'][rb_id]['layername'], 
                    current_import_id, rule_num, section_header_uids, parent_uid, config2import)
    return target_rulebase


def parse_single_rule(src_rule, rulebase, layer_name, import_id, rule_num, parent_uid, config2import, debug_level=0):
    logger = getFwoLogger()
    # reference to domain rule layer, filling up basic fields
    if 'type' in src_rule and src_rule['type'] != 'place-holder':
        if 'rule-number' in src_rule:  # standard rule, no section header
            # SOURCE names
            rule_src_name = ''
            for src in src_rule["source"]:
                if 'type' in src:
                    if src['type'] == 'LegacyUserAtLocation':
                        rule_src_name += src['name'] + list_delimiter
                    elif src['type'] == 'access-role':
                        if isinstance(src['networks'], str):  # just a single source
                            if src['networks'] == 'any':
                                rule_src_name += src["name"] + \
                                    '@' + 'Any' + list_delimiter
                            else:
                                rule_src_name += src["name"] + '@' + \
                                    src['networks'] + list_delimiter
                        else:  # more than one source
                            for nw in src['networks']:
                                nw_resolved = resolve_uid_to_name(nw)
                                if nw_resolved == "":
                                    rule_src_name += src["name"] + list_delimiter
                                else:
                                    rule_src_name += src["name"] + '@' + nw_resolved + list_delimiter
                    else:  # standard network objects as source
                        rule_src_name += src["name"] + list_delimiter
                else:
                    # assuming standard network object as source (interface) with missing type
                    rule_src_name += src["name"] + list_delimiter

            rule_src_name = rule_src_name[:-1]  # removing last list_delimiter

            # SOURCE refs
            rule_src_ref = ''
            for src in src_rule["source"]:
                if 'type' in src:
                    if src['type'] == 'LegacyUserAtLocation':
                        rule_src_ref += src["userGroup"] + '@' + \
                            src["location"] + list_delimiter
                    elif src['type'] == 'access-role':
                        if isinstance(src['networks'], str):  # just a single source
                            if src['networks'] == 'any':
                                rule_src_ref += src['uid'] + '@' + \
                                    cp_const.any_obj_uid + list_delimiter
                            else:
                                rule_src_ref += src['uid'] + '@' + \
                                    src['networks'] + list_delimiter
                        else:  # more than one source
                            for nw in src['networks']:
                                rule_src_ref += src['uid'] + \
                                    '@' + nw + list_delimiter
                    else:  # standard network objects as source
                        rule_src_ref += src["uid"] + list_delimiter
                else:
                    # assuming standard network object as source (interface) with missing type
                    rule_src_ref += src["uid"] + list_delimiter
            rule_src_ref = rule_src_ref[:-1]  # removing last list_delimiter

            # rule_dst...
            rule_dst_name = ''
            for dst in src_rule["destination"]:
                if 'type' in dst:
                    if dst['type'] == 'LegacyUserAtLocation':
                        rule_dst_name += dst['name'] + list_delimiter
                    elif dst['type'] == 'access-role':
                        if isinstance(dst['networks'], str):  # just a single destination
                            if dst['networks'] == 'any':
                                rule_dst_name += dst["name"] + \
                                    '@' + 'Any' + list_delimiter
                            else:
                                rule_dst_name += dst["name"] + '@' + \
                                    dst['networks'] + list_delimiter
                        else:  # more than one source
                            for nw in dst['networks']:
                                rule_dst_name += dst[
                                    # TODO: this is not correct --> need to reverse resolve name from given UID
                                    "name"] + '@' + nw + list_delimiter
                    else:  # standard network objects as destination
                        rule_dst_name += dst["name"] + list_delimiter
                else:
                    # assuming standard network object as destination (interface) with missing type
                        rule_dst_name += dst["name"] + list_delimiter

            rule_dst_name = rule_dst_name[:-1]

            rule_dst_ref = ''
            for dst in src_rule["destination"]:
                if 'type' in dst:
                    if dst['type'] == 'LegacyUserAtLocation':
                        rule_dst_ref += dst["userGroup"] + '@' + \
                            dst["location"] + list_delimiter
                    elif dst['type'] == 'access-role':
                        if isinstance(dst['networks'], str):  # just a single destination
                            if dst['networks'] == 'any':
                                rule_dst_ref += dst['uid'] + '@' + \
                                    cp_const.any_obj_uid + list_delimiter
                            else:
                                rule_dst_ref += dst['uid'] + '@' + \
                                    dst['networks'] + list_delimiter
                        else:  # more than one source
                            for nw in dst['networks']:
                                rule_dst_ref += dst['uid'] + \
                                    '@' + nw + list_delimiter
                    else:  # standard network objects as destination
                        rule_dst_ref += dst["uid"] + list_delimiter

                else:
                    # assuming standard network object as destination (interface) with missing type
                        rule_dst_ref += dst["uid"] + list_delimiter
                    
            rule_dst_ref = rule_dst_ref[:-1]

            # rule_svc...
            rule_svc_name = ''
            for svc in src_rule["service"]:
                rule_svc_name += svc["name"] + list_delimiter
            rule_svc_name = rule_svc_name[:-1]

            rule_svc_ref = ''
            for svc in src_rule["service"]:
                rule_svc_ref += svc["uid"] + list_delimiter
            rule_svc_ref = rule_svc_ref[:-1]

            if 'name' in src_rule and src_rule['name'] != '':
                rule_name = src_rule['name']
            else:
                rule_name = None

            # new in v8.0.3:
            rule_custom_fields = None
            if 'custom-fields' in src_rule:
                rule_custom_fields = src_rule['custom-fields']

            if 'meta-info' in src_rule and 'last-modifier' in src_rule['meta-info']:
                rule_last_change_admin = src_rule['meta-info']['last-modifier']
            else:
                rule_last_change_admin = None

            # new in v5.1.17:
            if 'parent_rule_uid' in src_rule:
                logger.debug(
                    'found rule (uid=' + src_rule['uid'] + ') with parent_rule_uid set: ' + src_rule['parent_rule_uid'])
                parent_rule_uid = src_rule['parent_rule_uid']
            else:
                parent_rule_uid = parent_uid
            if parent_rule_uid == '':
                parent_rule_uid = None

            # new in v5.5.1:
            if 'rule_type' in src_rule:
                rule_type = src_rule['rule_type']
            else:
                rule_type = 'access'

            if 'comments' in src_rule:
                if src_rule['comments'] == '':
                    comments = None
                else:
                    comments = src_rule['comments']
            else:
                comments = None

            if 'hits' in src_rule and 'last-date' in src_rule['hits'] and 'iso-8601' in src_rule['hits']['last-date']:
                last_hit = src_rule['hits']['last-date']['iso-8601']
            else:
                last_hit = None

            rule = {
                "control_id":       int(import_id),
                "rule_num":         int(rule_num),
                "rulebase_name":    sanitize(layer_name),
                # rule_ruleid
                "rule_disabled": not bool(src_rule['enabled']),
                "rule_src_neg":     bool(src_rule['source-negate']),
                "rule_src":         sanitize(rule_src_name),
                "rule_src_refs":    sanitize(rule_src_ref),
                "rule_dst_neg":     bool(src_rule['destination-negate']),
                "rule_dst":         sanitize(rule_dst_name),
                "rule_dst_refs":    sanitize(rule_dst_ref),
                "rule_svc_neg":     bool(src_rule['service-negate']),
                "rule_svc":         sanitize(rule_svc_name),
                "rule_svc_refs":    sanitize(rule_svc_ref),
                "rule_action":      sanitize(src_rule['action']['name']),
                "rule_track":       sanitize(src_rule['track']['type']['name']),
                "rule_installon":   sanitize(src_rule['install-on'][0]['name']),
                "rule_time":        sanitize(src_rule['time'][0]['name']),
                "rule_name":        sanitize(rule_name),
                "rule_uid":         sanitize(src_rule['uid']),
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
            rulebase.append(rule)

            return rule_num + 1
    return rule_num


def resolve_uid_to_name(nw_obj_uid):
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
    rulebase.append(rule)
    return rule_num + 1


def add_domain_rule_header_rule(rulebase, section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid):
    return insert_section_header_rule(rulebase, section_name, layer_name,
                                    import_id, rule_uid, rule_num, section_header_uids, parent_uid)


def check_and_add_section_header(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=0, recursion_level=1):
    # if current rulebase starts a new section, add section header, but only if it does not exist yet (can happen by chunking a section)
    if 'type' in src_rulebase and src_rulebase['type'] == 'access-section' and 'uid' in src_rulebase: # and not src_rulebase['uid'] in section_header_uids:
        section_name = default_section_header_text
        if 'name' in src_rulebase:
            section_name = src_rulebase['name']
        if 'parent_rule_uid' in src_rulebase:
            parent_uid = src_rulebase['parent_rule_uid']
        else:
            parent_uid = ""
        rule_num = insert_section_header_rule(target_rulebase, section_name, layer_name, import_id, src_rulebase['uid'], rule_num, section_header_uids, parent_uid)
        parent_uid = src_rulebase['uid']
    return rule_num


def parse_rulebase(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, 
                    debug_level=0, recursion_level=1, layer_disabled=False):
    logger = getFwoLogger()
    if (recursion_level > fwo_const.max_recursion_level):
        raise ImportRecursionLimitReached("parse_rulebase") from None

    # parse chunks
    if 'layerchunks' in src_rulebase:   # found chunks of layers which need to be parsed separately
        for chunk in src_rulebase['layerchunks']:
            if 'rulebase' in chunk:
                for rules_chunk in chunk['rulebase']:
                    rule_num = parse_rulebase(rules_chunk, target_rulebase, layer_name, import_id, rule_num,
                                                    section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)
            else:
                rule_num = parse_rulebase(chunk, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)
      
    check_and_add_section_header(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)

    # parse layered rulebase
    if 'rulebase' in src_rulebase:
        # layer_disabled = not src_rulebase['enabled']
        for rule in src_rulebase['rulebase']:
            if 'type' in rule:
                if rule['type'] == 'place-holder':  # add domain rules
                    section_name = ""
                    if 'name' in src_rulebase:
                        section_name = rule['name']
                    rule_num = add_domain_rule_header_rule(
                        target_rulebase, section_name, layer_name, import_id, rule['uid'], rule_num, section_header_uids, parent_uid)
                else:  # parse standard sections
                    rule_num = parse_single_rule(
                        rule, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import, debug_level=debug_level)
            if 'rulebase' in rule:  # alsways check if a rule contains another layer
                rule_num = parse_rulebase(rule, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, config2import, debug_level=debug_level, recursion_level=recursion_level+1)

    if 'type' in src_rulebase and src_rulebase['type'] == 'place-holder':  # add domain rules
        logger.debug('found domain rule ref: ' + src_rulebase['uid'])
        section_name = ""
        if 'name' in src_rulebase:
            section_name = src_rulebase['name']
        rule_num = add_domain_rule_header_rule(
            target_rulebase, section_name, layer_name, import_id, src_rulebase['uid'], rule_num, section_header_uids, parent_uid)

    if 'rule-number' in src_rulebase:   # rulebase is just a single rule
        rule_num = parse_single_rule(src_rulebase, target_rulebase, layer_name, import_id, rule_num, parent_uid, config2import)

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
        'rule_type': 'original'
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
        'rule_type': 'xlate'
    }
    return (rule_match, rule_xlate)


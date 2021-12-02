import logging
import sys, json
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
import common, fwcommon


def add_section_header_rule_in_json (rulebase, section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid):
    section_header_uids.append(common.sanitize(rule_uid))
    rule = {
        "control_id":       int(import_id),
        "rule_num":         int(rule_num),
        "rulebase_name":    common.sanitize(layer_name),
        # rule_ruleid
        "rule_disabled":    False,
        "rule_src_neg":     False,
        "rule_src":         "Any",
        "rule_src_refs":    common.sanitize(fwcommon.any_obj_uid),
        "rule_dst_neg":     False,
        "rule_dst":         "Any",
        "rule_dst_refs":    common.sanitize(fwcommon.any_obj_uid),
        "rule_svc_neg":     False,
        "rule_svc":         "Any",
        "rule_svc_refs":    common.sanitize(fwcommon.any_obj_uid),
        "rule_action":      "Accept",
        "rule_track":       "Log",
        "rule_installon":   "Policy Targets",
        "rule_time":        "Any",
        "rule_implied":      False,
        #"rule_comment":     None,
         # rule_name
        "rule_uid":         common.sanitize(rule_uid),
        "rule_head_text":   common.sanitize(section_name),
        # rule_from_zone
        # rule_to_zone
        # rule_last_change_admin
        "parent_rule_uid":  common.sanitize(parent_uid)
    }
    rulebase.append(rule)


def add_domain_rule_header_rule_in_json(rulebase, section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid):
    add_section_header_rule_in_json(rulebase, section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid)


def parse_single_rule_to_json (src_rule, rulebase, layer_name, import_id, rule_num, parent_uid):
    dst_rule = {}

    # reference to domain rule layer, filling up basic fields
    if 'type' in src_rule and src_rule['type'] != 'place-holder':
        if 'rule-number' in src_rule:  # standard rule, no section header
            # SOURCE names
            rule_src_name = ''
            for src in src_rule["source"]:
                if src['type'] == 'LegacyUserAtLocation':
                    rule_src_name += src['name'] + common.list_delimiter
                elif src['type'] == 'access-role':
                    if isinstance(src['networks'], str):  # just a single source
                        if src['networks'] == 'any':
                            rule_src_name += src["name"] + '@' + 'Any' + common.list_delimiter
                        else:
                            rule_src_name += src["name"] + '@' + src['networks'] + common.list_delimiter
                    else:  # more than one source
                        for nw in src['networks']:
                            rule_src_name += src[
                                                # TODO: this is not correct --> need to reverse resolve name from given UID
                                                "name"] + '@' + nw + common.list_delimiter
                else:  # standard network objects as source
                    rule_src_name += src["name"] + common.list_delimiter
            rule_src_name = rule_src_name[:-1]  # removing last list_delimiter
            #common.csv_add_field(rule_src_name)  # src_names

            # SOURCE refs
            rule_src_ref = ''
            for src in src_rule["source"]:
                if src['type'] == 'LegacyUserAtLocation':
                    rule_src_ref += src["userGroup"] + '@' + src["location"] + common.list_delimiter
                elif src['type'] == 'access-role':
                    if isinstance(src['networks'], str):  # just a single source
                        if src['networks'] == 'any':
                            rule_src_ref += src['uid'] + '@' + fwcommon.any_obj_uid + common.list_delimiter
                        else:
                            rule_src_ref += src['uid'] + '@' + src['networks'] + common.list_delimiter
                    else:  # more than one source
                        for nw in src['networks']:
                            rule_src_ref += src['uid'] + '@' + nw + common.list_delimiter
                else:  # standard network objects as source
                    rule_src_ref += src["uid"] + common.list_delimiter
            rule_src_ref = rule_src_ref[:-1]  # removing last list_delimiter

            # rule_dst...
            rule_dst_name = ''
            for dst in src_rule["destination"]:
                rule_dst_name += dst["name"] + common.list_delimiter
            rule_dst_name = rule_dst_name[:-1]

            rule_dst_ref = ''
            for dst in src_rule["destination"]:
                rule_dst_ref += dst["uid"] + common.list_delimiter
            rule_dst_ref = rule_dst_ref[:-1]

            # rule_svc...
            rule_svc_name = ''
            for svc in src_rule["service"]:
                rule_svc_name += svc["name"] + common.list_delimiter
            rule_svc_name = rule_svc_name[:-1]

            rule_svc_ref = ''
            for svc in src_rule["service"]:
                rule_svc_ref += svc["uid"] + common.list_delimiter
            rule_svc_ref = rule_svc_ref[:-1]

            if 'name' in src_rule and src_rule['name']!='':
                rule_name = src_rule['name']
            else:
                rule_name = None

            if 'meta-info' in src_rule and 'last-modifier' in src_rule['meta-info']:
                rule_last_change_admin = src_rule['meta-info']['last-modifier']
            else:
                rule_last_change_admin = None

            # new in v5.1.17:
            if 'parent_rule_uid' in src_rule:
                logging.debug('csv_dump_rule: found rule (uid=' + src_rule['uid'] + ') with parent_rule_uid set: ' + src_rule['parent_rule_uid'])
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
                if src_rule['comments']=='':
                    comments = None
                else:
                    comments = src_rule['comments']
            else:
                comments = None

            rule = {
                "control_id":       int(import_id),
                "rule_num":         int(rule_num),
                "rulebase_name":    common.sanitize(layer_name),
                # rule_ruleid
                "rule_disabled":    not bool(src_rule['enabled']),
                "rule_src_neg":     bool(src_rule['source-negate']),
                "rule_src":         common.sanitize(rule_src_name),
                "rule_src_refs":    common.sanitize(rule_src_ref),
                "rule_dst_neg":     bool(src_rule['destination-negate']),
                "rule_dst":         common.sanitize(rule_dst_name),
                "rule_dst_refs":    common.sanitize(rule_dst_ref),
                "rule_svc_neg":     bool(src_rule['service-negate']),
                "rule_svc":         common.sanitize(rule_svc_name),
                "rule_svc_refs":    common.sanitize(rule_svc_ref),
                "rule_action":      common.sanitize(src_rule['action']['name']),
                "rule_track":       common.sanitize(src_rule['track']['type']['name']),
                "rule_installon":   common.sanitize(src_rule['install-on'][0]['name']),
                "rule_time":        common.sanitize(src_rule['time'][0]['name']),
                "rule_comment":     common.sanitize(comments),
                "rule_name":        common.sanitize(rule_name),
                "rule_uid":         common.sanitize(src_rule['uid']),
                "rule_implied":     False,
                "rule_type":        common.sanitize(rule_type),
                # "rule_head_text": common.sanitize(section_name),
                # rule_from_zone
                # rule_to_zone
                "rule_last_change_admin": common.sanitize(rule_last_change_admin),
                "parent_rule_uid":  common.sanitize(parent_rule_uid)
            }
            rulebase.append(rule)


def parse_rulebase_json(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid):

    if 'layerchunks' in src_rulebase:
        for chunk in src_rulebase['layerchunks']:
            if 'rulebase' in chunk:
                for rules_chunk in chunk['rulebase']:
                    rule_num  = parse_rulebase_json(rules_chunk, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid)
            else:
                logging.warning("parse_rule: found no rulebase in chunk:\n" + json.dumps(chunk, indent=2))
    else:
        if 'rulebase' in src_rulebase:
            if src_rulebase['type'] == 'access-section' and not src_rulebase['uid'] in section_header_uids: # add section header, but only if it does not exist yet (can happen by chunking a section)
                section_name = ""
                if 'name' in src_rulebase:
                    section_name = src_rulebase['name']
                if 'parent_rule_uid' in src_rulebase:
                    parent_uid = src_rulebase['parent_rule_uid']
                else:
                    parent_uid = ""
                add_section_header_rule_in_json(target_rulebase, section_name, layer_name, import_id, src_rulebase['uid'], rule_num, section_header_uids, parent_uid)
                rule_num += 1
                parent_uid = src_rulebase['uid']
            for rule in src_rulebase['rulebase']:
                if rule['type'] == 'place-holder':  # add domain rules
                    section_name = ""
                    if 'name' in src_rulebase:
                        section_name = rule['name']
                    add_domain_rule_header_rule_in_json(target_rulebase, section_name, layer_name, import_id, rule['uid'], rule_num, section_header_uids, parent_uid)
                else: # parse standard sections
                    parse_single_rule_to_json(rule, target_rulebase, layer_name, import_id, rule_num, parent_uid)
                    rule_num += 1
                   
        if src_rulebase['type'] == 'place-holder':  # add domain rules
            logging.debug('parse_rules_json: found domain rule ref: ' + src_rulebase['uid'])
            section_name = ""
            if 'name' in src_rulebase:
                section_name = src_rulebase['name']
            add_domain_rule_header_rule_in_json(target_rulebase, section_name, layer_name, import_id, src_rulebase['uid'], rule_num, section_header_uids, parent_uid)
            rule_num += 1
        if 'rule-number' in src_rulebase:   # rulebase is just a single rule
            parse_single_rule_to_json(src_rulebase, target_rulebase, layer_name, import_id, rule_num, parent_uid)
            rule_num += 1
    return rule_num


def parse_nat_rulebase_json(src_rulebase, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid):
    if 'nat_rule_chunks' in src_rulebase:
        for chunk in src_rulebase['nat_rule_chunks']:
            if 'rulebase' in chunk:
                for rules_chunk in chunk['rulebase']:
                    rule_num  = parse_nat_rulebase_json(rules_chunk, target_rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid)
            else:
                logging.warning("parse_rule: found no rulebase in chunk:\n" + json.dumps(chunk, indent=2))
    else:
        if 'rulebase' in src_rulebase:
            if src_rulebase['type'] == 'access-section' and not src_rulebase['uid'] in section_header_uids: # add section header, but only if it does not exist yet (can happen by chunking a section)
                section_name = ""
                if 'name' in src_rulebase:
                    section_name = src_rulebase['name']
                parent_uid = ""
                add_section_header_rule_in_json(target_rulebase, section_name, layer_name, import_id, src_rulebase['uid'], rule_num, section_header_uids, parent_uid)
                rule_num += 1
                parent_uid = src_rulebase['uid']
            for rule in src_rulebase['rulebase']:
                (rule_match, rule_xlate) = parse_nat_rule_transform(rule, rule_num)
                parse_single_rule_to_json(rule_match, target_rulebase, layer_name, import_id, rule_num, parent_uid)
                parse_single_rule_to_json(rule_xlate, target_rulebase, layer_name, import_id, rule_num, parent_uid)
                rule_num += 1
                   
        if 'rule-number' in src_rulebase:   # rulebase is just a single rule
            (rule_match, rule_xlate) = parse_nat_rule_transform(src_rulebase, rule_num)
            parse_single_rule_to_json(rule_match, target_rulebase, layer_name, import_id, rule_num, parent_uid)
            parse_single_rule_to_json(rule_xlate, target_rulebase, layer_name, import_id, rule_num, parent_uid)
            rule_num += 1
    return rule_num


def parse_nat_rule_transform(xlate_rule_in, rule_num):
# todo: cleanup certain fields (install-on, ....)
    rule_match = {
        'uid': xlate_rule_in['uid'], # + '_match',
        'source': [xlate_rule_in['original-source']],
        'destination': [xlate_rule_in['original-destination']],
        'service': [xlate_rule_in['original-service']],
        'action': {'name': 'Drop'},
        'track': {'type': {'name': 'None' } },
        'type': 'nat',
        'rule-number': rule_num,
        'enabled': True,
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
        'uid': xlate_rule_in['uid'], # + '_xlate',
        'source': [xlate_rule_in['translated-source']],
        'destination': [xlate_rule_in['translated-destination']],
        'service': [xlate_rule_in['translated-service']],
        'action': {'name': 'Drop'},
        'track': {'type': {'name': 'None' } },
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
 
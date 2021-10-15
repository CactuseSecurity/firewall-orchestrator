import logging
import sys, json
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
import common, fwcommon


def create_section_header(section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid):
    # only do this once! : section_header_uids.append(rule_uid)
    header_rule_csv = common.csv_add_field(import_id)           # control_id
    header_rule_csv += common.csv_add_field(str(rule_num))      # rule_num
    header_rule_csv += common.csv_add_field(layer_name)         # rulebase_name
    header_rule_csv += common.csv_delimiter                     # rule_ruleid
    header_rule_csv += common.csv_add_field('False')            # rule_disabled
    header_rule_csv += common.csv_add_field('False')            # rule_src_neg
    header_rule_csv += common.csv_add_field('Any')              # rule_src
    header_rule_csv += common.csv_add_field(fwcommon.any_obj_uid) # rule_src_refs
    header_rule_csv += common.csv_add_field('False')            # rule_dst_neg
    header_rule_csv += common.csv_add_field('Any')              # rule_dst
    header_rule_csv += common.csv_add_field(fwcommon.any_obj_uid) # rule_dst_refs
    header_rule_csv += common.csv_add_field('False')            # rule_svc_neg
    header_rule_csv += common.csv_add_field('Any')              # rule_svc
    header_rule_csv += common.csv_add_field(fwcommon.any_obj_uid) # rule_svc_refs
    header_rule_csv += common.csv_add_field('Accept')           # rule_action
    header_rule_csv += common.csv_add_field('Log')              # rule_track
    header_rule_csv += common.csv_add_field('Policy Targets')   # rule_installon
    header_rule_csv += common.csv_add_field('Any')              # rule_time
    header_rule_csv += common.csv_delimiter                     # rule_comment
    header_rule_csv += common.csv_delimiter                     # rule_name
    header_rule_csv += common.csv_add_field(rule_uid)           # rule_uid
    header_rule_csv += common.csv_add_field(section_name)       # rule_head_text
    header_rule_csv += common.csv_delimiter                     # rule_from_zone
    header_rule_csv += common.csv_delimiter                     # rule_to_zone
    header_rule_csv += common.csv_delimiter                     # rule_last_change_admin
    if parent_uid != "":
        header_rule_csv += common.csv_add_field(parent_uid, no_csv_delimiter=True) # parent_rule_uid
    return header_rule_csv + common.line_delimiter


def create_domain_rule_header(section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid):
    return create_section_header(section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid)


def csv_dump_rule(rule, layer_name, import_id, rule_num, parent_uid):
    rule_csv = ''

    # reference to domain rule layer, filling up basic fields
    if 'type' in rule and rule['type'] != 'place-holder':
#            add_missing_info_to_domain_ref_rule(rule)
        if 'rule-number' in rule:  # standard rule, no section header
            # print ("rule #" + str(rule['rule-number']) + "\n")
            rule_csv += common.csv_add_field(import_id)  # control_id
            rule_csv += common.csv_add_field(str(rule_num))  # rule_num
            rule_csv += common.csv_add_field(layer_name)  # rulebase_name
            rule_csv += common.csv_add_field('')  # rule_ruleid is empty
            rule_csv += common.csv_add_field(str(not rule['enabled']))  # rule_disabled
            rule_csv += common.csv_add_field(str(rule['source-negate']))  # src_neg

            # SOURCE names
            rule_src_name = ''
            for src in rule["source"]:
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
            rule_csv += common.csv_add_field(rule_src_name)  # src_names

            # SOURCE refs
            rule_src_ref = ''
            for src in rule["source"]:
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
            rule_csv += common.csv_add_field(rule_src_ref)  # src_refs

            rule_csv += common.csv_add_field(str(rule['destination-negate']))  # destination negation

            rule_dst_name = ''
            for dst in rule["destination"]:
                rule_dst_name += dst["name"] + common.list_delimiter
            rule_dst_name = rule_dst_name[:-1]
            rule_csv += common.csv_add_field(rule_dst_name)  # rule dest_name

            rule_dst_ref = ''
            for dst in rule["destination"]:
                rule_dst_ref += dst["uid"] + common.list_delimiter
            rule_dst_ref = rule_dst_ref[:-1]
            rule_csv += common.csv_add_field(rule_dst_ref)  # rule_dest_refs

            # SERVICE negate
            rule_csv += common.csv_add_field(str(rule['service-negate']))  # service negation
            # SERVICE names
            rule_svc_name = ''
            for svc in rule["service"]:
                rule_svc_name += svc["name"] + common.list_delimiter
            rule_svc_name = rule_svc_name[:-1]
            rule_csv += common.csv_add_field(rule_svc_name)  # rule svc name

            # SERVICE refs
            rule_svc_ref = ''
            for svc in rule["service"]:
                rule_svc_ref += svc["uid"] + common.list_delimiter
            rule_svc_ref = rule_svc_ref[:-1]
            rule_csv += common.csv_add_field(rule_svc_ref)  # rule svc ref

            rule_action = rule['action']
            rule_action_name = rule_action['name']
            rule_csv += common.csv_add_field(rule_action_name)  # rule action
            rule_track = rule['track']
            rule_track_type = rule_track['type']
            rule_csv += common.csv_add_field(rule_track_type['name'])  # rule track

            rule_install_on = rule['install-on']
            first_rule_install_target = rule_install_on[0]
            rule_csv += common.csv_add_field(first_rule_install_target['name'])  # install on

            rule_time = rule['time']
            first_rule_time = rule_time[0]
            rule_csv += common.csv_add_field(first_rule_time['name'])  # time
            if (rule['comments']!=None and rule['comments']!=''):
                rule_csv += common.csv_add_field(rule['comments'])  # comments
            else:
                rule_csv += common.csv_delimiter                    # no comments
            if 'name' in rule:
                rule_name = rule['name']
            else:
                rule_name = None
            rule_csv += common.csv_add_field(rule_name)  # rule_name

            rule_csv += common.csv_add_field(rule['uid'])  # rule_uid
            rule_head_text = ''
            rule_csv += common.csv_add_field(rule_head_text)  # rule_head_text
            rule_from_zone = ''
            rule_csv += common.csv_add_field(rule_from_zone)
            rule_to_zone = ''
            rule_csv += common.csv_add_field(rule_to_zone)
            rule_meta_info = rule['meta-info']
            rule_csv += common.csv_add_field(rule_meta_info['last-modifier'])
            # new in v5.1.17:
            if 'parent_rule_uid' in rule:
                logging.debug('csv_dump_rule: found rule (uid=' + rule['uid'] + ') with parent_rule_uid set: ' + rule['parent_rule_uid'])
                parent_rule_uid = rule['parent_rule_uid']
            else:
                parent_rule_uid = parent_uid
            if (parent_rule_uid!=''):
                rule_csv += common.csv_add_field(parent_rule_uid,no_csv_delimiter=True)
            # else:
            #     rule_csv += common.csv_delimiter
            rule_csv += common.line_delimiter
    return rule_csv


def csv_dump_rules(rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid):
    result = ''

    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            if 'rulebase' in chunk:
                for rules_chunk in chunk['rulebase']:
                    rule_num, rules_in_csv = csv_dump_rules(rules_chunk, layer_name, import_id, rule_num, section_header_uids, parent_uid)
                    result += rules_in_csv
            else:
                logging.warning("parse_rule: found no rulebase in chunk:\n" + json.dumps(chunk, indent=2))
    else:
        if 'rulebase' in rulebase:
            if rulebase['type'] == 'access-section' and not rulebase['uid'] in section_header_uids: # add section header, but only if it does not exist yet (can happen by chunking a section)
                section_name = ""
                if 'name' in rulebase:
                    section_name = rulebase['name']
                if 'parent_rule_uid' in rulebase:
                    parent_uid = rulebase['parent_rule_uid']
                else:
                    parent_uid = ""
                section_header = create_section_header(section_name, layer_name, import_id, rulebase['uid'], rule_num, section_header_uids, parent_uid)
                rule_num += 1
                result += section_header
                parent_uid = rulebase['uid']
            for rule in rulebase['rulebase']:
                if rule['type'] == 'place-holder':  # add domain rules
                    section_name = ""
                    if 'name' in rulebase:
                        section_name = rule['name']
                    result += create_domain_rule_header(section_name, layer_name, import_id, rule['uid'], rule_num, section_header_uids, parent_uid)
                else: # parse standard sections
                   rule_num, rules_in_layer = csv_dump_rules(rule, layer_name, import_id, rule_num, section_header_uids, parent_uid)
                   result += rules_in_layer
        if rulebase['type'] == 'place-holder':  # add domain rules
            logging.debug('csv_dump_rules: found domain rule ref: ' + rulebase['uid'])
            section_name = ""
            if 'name' in rulebase:
                section_name = rulebase['name']
            result += create_domain_rule_header(section_name, layer_name, import_id, rulebase['uid'], rule_num, section_header_uids, parent_uid)
            rule_num += 1
        if 'rule-number' in rulebase:
            result += csv_dump_rule(rulebase, layer_name, import_id, rule_num, parent_uid)
            rule_num += 1
    return rule_num, result

###############################################################################################
###############################################################################################
# the following functions are only used within new python-only importer:

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
            common.csv_add_field(rule_src_name)  # src_names

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

            # new in v5.1.17:
            if 'parent_rule_uid' in src_rule:
                logging.debug('csv_dump_rule: found rule (uid=' + src_rule['uid'] + ') with parent_rule_uid set: ' + src_rule['parent_rule_uid'])
                parent_rule_uid = src_rule['parent_rule_uid']
            else:
                parent_rule_uid = parent_uid
            if parent_rule_uid == '':
                parent_rule_uid = None

            if 'comments' in src_rule and src_rule['comments']=='':
                comments = None
            else:
                comments = src_rule['comments']

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
                # "rule_head_text": common.sanitize(section_name),
                # rule_from_zone
                # rule_to_zone
                "rule_last_change_admin": common.sanitize(src_rule['meta-info']['last-modifier']),
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
    if 'layerchunks' in src_rulebase:
        for chunk in src_rulebase['layerchunks']:
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
                parse_single_rule_to_json(rule, target_rulebase, layer_name, import_id, rule_num, parent_uid)
                rule_num += 1
                   
        if 'rule-number' in src_rulebase:   # rulebase is just a single rule
            parse_single_rule_to_json(src_rulebase, target_rulebase, layer_name, import_id, rule_num, parent_uid)
            rule_num += 1
    return rule_num

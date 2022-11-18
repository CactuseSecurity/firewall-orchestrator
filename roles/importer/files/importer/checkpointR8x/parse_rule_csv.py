from fwo_log import getFwoLogger
import json
import common, cpcommon, parse_rule, fwo_const
from fwo_const import list_delimiter, csv_delimiter, line_delimiter
from fwo_base import csv_add_field


def create_section_header(section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid):
    # only do this once! : section_header_uids.append(rule_uid)
    header_rule_csv = csv_add_field(import_id)           # control_id
    header_rule_csv += csv_add_field(str(rule_num))      # rule_num
    header_rule_csv += csv_add_field(layer_name)         # rulebase_name
    header_rule_csv += csv_delimiter                     # rule_ruleid
    header_rule_csv += csv_add_field('False')            # rule_disabled
    header_rule_csv += csv_add_field('False')            # rule_src_neg
    header_rule_csv += csv_add_field('Any')              # rule_src
    header_rule_csv += csv_add_field(cpcommon.any_obj_uid) # rule_src_refs
    header_rule_csv += csv_add_field('False')            # rule_dst_neg
    header_rule_csv += csv_add_field('Any')              # rule_dst
    header_rule_csv += csv_add_field(cpcommon.any_obj_uid) # rule_dst_refs
    header_rule_csv += csv_add_field('False')            # rule_svc_neg
    header_rule_csv += csv_add_field('Any')              # rule_svc
    header_rule_csv += csv_add_field(cpcommon.any_obj_uid) # rule_svc_refs
    header_rule_csv += csv_add_field('Accept')           # rule_action
    header_rule_csv += csv_add_field('Log')              # rule_track
    header_rule_csv += csv_add_field('Policy Targets')   # rule_installon
    header_rule_csv += csv_add_field('Any')              # rule_time
    header_rule_csv += csv_delimiter                     # rule_comment
    header_rule_csv += csv_delimiter                     # rule_name
    header_rule_csv += csv_add_field(rule_uid)           # rule_uid
    header_rule_csv += csv_add_field(section_name)       # rule_head_text
    header_rule_csv += csv_delimiter                     # rule_from_zone
    header_rule_csv += csv_delimiter                     # rule_to_zone
    header_rule_csv += csv_delimiter                     # rule_last_change_admin
    if parent_uid != "":
        header_rule_csv += csv_add_field(parent_uid, no_csv_delimiter=True) # parent_rule_uid
    return header_rule_csv + line_delimiter


def create_domain_rule_header(section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid):
    return create_section_header(section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids, parent_uid)


def csv_dump_rule(rule, layer_name, import_id, rule_num, parent_uid, debug_level=0):
    logger = getFwoLogger()
    rule_csv = ''

    # reference to domain rule layer, filling up basic fields
    if 'type' in rule and rule['type'] != 'place-holder':
#            add_missing_info_to_domain_ref_rule(rule)
        if 'rule-number' in rule:  # standard rule, no section header
            # print ("rule #" + str(rule['rule-number']) + "\n")
            rule_csv += csv_add_field(import_id)  # control_id
            rule_csv += csv_add_field(str(rule_num))  # rule_num
            rule_csv += csv_add_field(layer_name)  # rulebase_name
            rule_csv += csv_add_field('')  # rule_ruleid is empty
            rule_csv += csv_add_field(str(not rule['enabled']))  # rule_disabled
            rule_csv += csv_add_field(str(rule['source-negate']))  # src_neg

            # SOURCE names
            rule_src_name = ''
            for src in rule["source"]:
                if src['type'] == 'LegacyUserAtLocation':
                    rule_src_name += src['name'] + list_delimiter
                elif src['type'] == 'access-role':
                    if isinstance(src['networks'], str):  # just a single source
                        if src['networks'] == 'any':
                            rule_src_name += src["name"] + '@' + 'Any' + list_delimiter
                        else:
                            rule_src_name += src["name"] + '@' + src['networks'] + list_delimiter
                    else:  # more than one source
                        for nw in src['networks']:
                            rule_src_name += src[
                                                # TODO: this is not correct --> need to reverse resolve name from given UID
                                                "name"] + '@' + nw + list_delimiter
                else:  # standard network objects as source
                    rule_src_name += src["name"] + list_delimiter
            rule_src_name = rule_src_name[:-1]  # removing last list_delimiter
            rule_csv += csv_add_field(rule_src_name)  # src_names

            # SOURCE refs
            rule_src_ref = ''
            for src in rule["source"]:
                if src['type'] == 'LegacyUserAtLocation':
                    rule_src_ref += src["userGroup"] + '@' + src["location"] + list_delimiter
                elif src['type'] == 'access-role':
                    if isinstance(src['networks'], str):  # just a single source
                        if src['networks'] == 'any':
                            rule_src_ref += src['uid'] + '@' + cpcommon.any_obj_uid + list_delimiter
                        else:
                            rule_src_ref += src['uid'] + '@' + src['networks'] + list_delimiter
                    else:  # more than one source
                        for nw in src['networks']:
                            rule_src_ref += src['uid'] + '@' + nw + list_delimiter
                else:  # standard network objects as source
                    rule_src_ref += src["uid"] + list_delimiter
            rule_src_ref = rule_src_ref[:-1]  # removing last list_delimiter
            rule_csv += csv_add_field(rule_src_ref)  # src_refs

            rule_csv += csv_add_field(str(rule['destination-negate']))  # destination negation

            rule_dst_name = ''
            for dst in rule["destination"]:
                rule_dst_name += dst["name"] + list_delimiter
            rule_dst_name = rule_dst_name[:-1]
            rule_csv += csv_add_field(rule_dst_name)  # rule dest_name

            rule_dst_ref = ''
            for dst in rule["destination"]:
                rule_dst_ref += dst["uid"] + list_delimiter
            rule_dst_ref = rule_dst_ref[:-1]
            rule_csv += csv_add_field(rule_dst_ref)  # rule_dest_refs

            # SERVICE negate
            rule_csv += csv_add_field(str(rule['service-negate']))  # service negation
            # SERVICE names
            rule_svc_name = ''
            for svc in rule["service"]:
                rule_svc_name += svc["name"] + list_delimiter
            rule_svc_name = rule_svc_name[:-1]
            rule_csv += csv_add_field(rule_svc_name)  # rule svc name

            # SERVICE refs
            rule_svc_ref = ''
            for svc in rule["service"]:
                rule_svc_ref += svc["uid"] + list_delimiter
            rule_svc_ref = rule_svc_ref[:-1]
            rule_csv += csv_add_field(rule_svc_ref)  # rule svc ref

            rule_action = rule['action']
            rule_action_name = rule_action['name']
            rule_csv += csv_add_field(rule_action_name)  # rule action
            rule_track = rule['track']
            rule_track_type = rule_track['type']
            rule_csv += csv_add_field(rule_track_type['name'])  # rule track

            rule_install_on = rule['install-on']
            first_rule_install_target = rule_install_on[0]
            rule_csv += csv_add_field(first_rule_install_target['name'])  # install on

            rule_time = rule['time']
            first_rule_time = rule_time[0]
            rule_csv += csv_add_field(first_rule_time['name'])  # time
            if (rule['comments']!=None and rule['comments']!=''):
                rule_csv += csv_add_field(rule['comments'])  # comments
            else:
                rule_csv += csv_delimiter                    # no comments
            if 'name' in rule:
                rule_name = rule['name']
            else:
                rule_name = None
            rule_csv += csv_add_field(rule_name)  # rule_name

            rule_csv += csv_add_field(rule['uid'])  # rule_uid
            rule_head_text = ''
            rule_csv += csv_add_field(rule_head_text)  # rule_head_text
            rule_from_zone = ''
            rule_csv += csv_add_field(rule_from_zone)
            rule_to_zone = ''
            rule_csv += csv_add_field(rule_to_zone)
            rule_meta_info = rule['meta-info']
            rule_csv += csv_add_field(rule_meta_info['last-modifier'])
            # new in v5.1.17:
            if 'parent_rule_uid' in rule:
                logger.debug('found rule (uid=' + rule['uid'] + ') with parent_rule_uid set: ' + rule['parent_rule_uid'])
                parent_rule_uid = rule['parent_rule_uid']
            else:
                parent_rule_uid = parent_uid
            if (parent_rule_uid!=''):
                rule_csv += csv_add_field(parent_rule_uid,no_csv_delimiter=True)
            rule_csv += line_delimiter
    return rule_csv


def csv_dump_rules(rulebase, layer_name, import_id, rule_num, section_header_uids, parent_uid, debug_level=0, recursion_level=1):
    logger = getFwoLogger()
    result = ''
    
    if recursion_level>fwo_const.max_recursion_level:
        raise common.ImportRecursionLimitReached("csv_dump_rules") from None

    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            if 'rulebase' in chunk:
                for rules_chunk in chunk['rulebase']:
                    rule_num, rules_in_csv = csv_dump_rules(rules_chunk, layer_name, import_id, rule_num, section_header_uids, parent_uid, debug_level=debug_level, recursion_level=recursion_level+1)
                    result += rules_in_csv
            else:
                logger.warning("found no rulebase in chunk:\n" + json.dumps(chunk, indent=2))
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
                    result += parse_rule.create_domain_rule_header(section_name, layer_name, import_id, rule['uid'], rule_num, section_header_uids, parent_uid)
                else: # parse standard sections
                   rule_num, rules_in_layer = csv_dump_rules(rule, layer_name, import_id, rule_num, section_header_uids, parent_uid, debug_level=debug_level)
                   result += rules_in_layer
        if rulebase['type'] == 'place-holder':  # add domain rules
            logger.debug('found domain rule ref: ' + rulebase['uid'])
            section_name = ""
            if 'name' in rulebase:
                section_name = rulebase['name']
            result += parse_rule.create_domain_rule_header(section_name, layer_name, import_id, rulebase['uid'], rule_num, section_header_uids, parent_uid)
            rule_num += 1
        if 'rule-number' in rulebase:
            result += csv_dump_rule(rulebase, layer_name, import_id, rule_num, parent_uid, debug_level=debug_level)
            rule_num += 1
    return rule_num, result

import argparse
import json
import re
import logging

csv_delimiter = '%'
list_delimiter = '|'
line_delimiter = "\n"
found_rulebase = False
section_header_uids=[]

# the following is the static across all installations unique any obj uid 
# cannot fetch the Any object via API (<=1.7) at the moment
# therefore we have a workaround adding the object manually (as svc and nw)
any_obj_uid = "97aeb369-9aea-11d5-bd16-0090272ccb30"
# todo: read this from config (vom API 1.6 on it is fetched)

# ###################### rule handling ###############################################


def create_section_header(section_name, layer_name, rule_uid):
    global rule_num
    global any_obj_uid

    section_header_uids.append(rule_uid)
    header_rule_csv = '"' + args.import_id + '"' + csv_delimiter  # control_id
    header_rule_csv += '"' + str(rule_num) + '"' + csv_delimiter  # rule_num
    header_rule_csv += '"' + layer_name + '"' + csv_delimiter  # rulebase_name
    header_rule_csv += csv_delimiter  # rule_ruleid
    header_rule_csv += '"' + 'false' + '"' + csv_delimiter  # rule_disabled
    header_rule_csv += '"' + 'False' + '"' + csv_delimiter  # rule_src_neg
    header_rule_csv += '"' + 'Any' + '"' + csv_delimiter  # src
    header_rule_csv += '"' + any_obj_uid + '"' + csv_delimiter  # src_refs
    header_rule_csv += '"' + 'False' + '"' + csv_delimiter  # rule_dst_neg
    header_rule_csv += '"' + 'Any' + '"' + csv_delimiter  # dst
    header_rule_csv += '"' + any_obj_uid + '"' + csv_delimiter  # dst_refs
    header_rule_csv += '"' + 'False' + '"' + csv_delimiter  # rule_svc_neg
    header_rule_csv += '"' + 'Any' + '"' + csv_delimiter  # svc
    header_rule_csv += '"' + any_obj_uid + '"' + csv_delimiter  # svc_refs
    header_rule_csv += '"' + 'Accept' + '"' + csv_delimiter  # action
    header_rule_csv += '"' + 'Log' + '"' + csv_delimiter  # track
    header_rule_csv += '"' + 'Policy Targets' + '"' + csv_delimiter  # install-on
    header_rule_csv += '"' + 'Any' + '"' + csv_delimiter  # time
    header_rule_csv += '""' + csv_delimiter  # comments
    header_rule_csv += csv_delimiter  # name
    header_rule_csv += '"' + rule_uid + '"' + csv_delimiter  # uid
    header_rule_csv += '"' + section_name + '"' + csv_delimiter  # head_text
    header_rule_csv += csv_delimiter  # from_zone
    header_rule_csv += csv_delimiter  # to_zone
    # last_change_admin
    return header_rule_csv + line_delimiter


def csv_add_field(content, csv_del, apostrophe):
    if content == '':  # do not add apostrophes for empty fields
        field_result = csv_del
    else:
        field_result = apostrophe + content + apostrophe + csv_del
    return field_result


def csv_dump_rule(rule, layer_name):
    global rule_num
    global number_of_section_headers_so_far
    global any_obj_uid
    apostrophe = '"'
    rule_csv = ''

    if 'rule-number' in rule:  # standard rule, no section header
        rule_csv += csv_add_field(args.import_id, csv_delimiter, apostrophe)  # control_id
        rule_num = rule['rule-number'] + number_of_section_headers_so_far
        rule_csv += csv_add_field(str(rule_num), csv_delimiter, apostrophe)  # rule_num
        rule_csv += csv_add_field(layer_name, csv_delimiter, apostrophe)  # rulebase_name
        rule_csv += csv_add_field('', csv_delimiter, apostrophe)  # rule_ruleid is empty
        if rule['enabled']:
            rule_disabled = 'False'
        else:
            rule_disabled = 'True'
        rule_csv += csv_add_field(rule_disabled, csv_delimiter, apostrophe)  # rule_disabled
        rule_csv += csv_add_field(str(rule['source-negate']), csv_delimiter, apostrophe)  # src_neg

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
        rule_csv += csv_add_field(rule_src_name, csv_delimiter, apostrophe)  # src_names

        # SOURCE refs
        rule_src_ref = ''
        for src in rule["source"]:
            if src['type'] == 'LegacyUserAtLocation':
                rule_src_ref += src["userGroup"] + '@' + src["location"] + list_delimiter
            elif src['type'] == 'access-role':
                if isinstance(src['networks'], str):  # just a single source
                    if src['networks'] == 'any':
                        rule_src_ref += src['uid'] + '@' + any_obj_uid + list_delimiter
                    else:
                        rule_src_ref += src['uid'] + '@' + src['networks'] + list_delimiter
                else:  # more than one source
                    for nw in src['networks']:
                        rule_src_ref += src['uid'] + '@' + nw + list_delimiter
            else:  # standard network objects as source
                rule_src_ref += src["uid"] + list_delimiter
        rule_src_ref = rule_src_ref[:-1]  # removing last list_delimiter
        rule_csv += csv_add_field(rule_src_ref, csv_delimiter, apostrophe)  # src_refs

        rule_csv += csv_add_field(str(rule['destination-negate']), csv_delimiter, apostrophe)  # destination negation

        rule_dst_name = ''
        for dst in rule["destination"]:
            rule_dst_name += dst["name"] + list_delimiter
        rule_dst_name = rule_dst_name[:-1]
        rule_csv += csv_add_field(rule_dst_name, csv_delimiter, apostrophe)  # rule dest_name

        rule_dst_ref = ''
        for dst in rule["destination"]:
            rule_dst_ref += dst["uid"] + list_delimiter
        rule_dst_ref = rule_dst_ref[:-1]
        rule_csv += csv_add_field(rule_dst_ref, csv_delimiter, apostrophe)  # rule_dest_refs

        # SERVICE names
        rule_svc_name = ''
        rule_svc_name += str(rule['service-negate']) + '"' + csv_delimiter + '"'
        for svc in rule["service"]:
            rule_svc_name += svc["name"] + list_delimiter
        rule_svc_name = rule_svc_name[:-1]
        rule_csv += csv_add_field(rule_svc_name, csv_delimiter, apostrophe)  # rule svc name

        # SERVICE refs
        rule_svc_ref = ''
        for svc in rule["service"]:
            rule_svc_ref += svc["uid"] + list_delimiter
        rule_svc_ref = rule_svc_ref[:-1]
        rule_csv += csv_add_field(rule_svc_ref, csv_delimiter, apostrophe)  # rule svc ref

        rule_action = rule['action']
        rule_action_name = rule_action['name']
        rule_csv += csv_add_field(rule_action_name, csv_delimiter, apostrophe)  # rule action
        rule_track = rule['track']
        rule_track_type = rule_track['type']
        rule_csv += csv_add_field(rule_track_type['name'], csv_delimiter, apostrophe)  # rule track

        rule_install_on = rule['install-on']
        first_rule_install_target = rule_install_on[0]
        rule_csv += csv_add_field(first_rule_install_target['name'], csv_delimiter, apostrophe)  # install on

        rule_time = rule['time']
        first_rule_time = rule_time[0]
        rule_csv += csv_add_field(first_rule_time['name'], csv_delimiter, apostrophe)  # time

        rule_csv += csv_add_field(rule['comments'], csv_delimiter, apostrophe)  # time

        if 'name' in rule:
            rule_name = rule['name']
        else:
            rule_name = ''
        rule_csv += csv_add_field(rule_name, csv_delimiter, apostrophe)  # rule_name

        rule_csv += csv_add_field(rule['uid'], csv_delimiter, apostrophe)  # rule_head_text
        rule_head_text = ''
        rule_csv += csv_add_field(rule_head_text, csv_delimiter, apostrophe)  # rule_head_text
        rule_from_zone = ''
        rule_csv += csv_add_field(rule_from_zone, csv_delimiter, apostrophe)
        rule_to_zone = ''
        rule_csv += csv_add_field(rule_to_zone, csv_delimiter, apostrophe)
        rule_meta_info = rule['meta-info']
        rule_csv += csv_add_field(rule_meta_info['last-modifier'], csv_delimiter, apostrophe)

        rule_csv = rule_csv[:-1] + line_delimiter  # remove last csv delimiter and add line delimiter
    return rule_csv


def csv_dump_rules(rulebase, layer_name):
    global rule_num
    global section_header_uids
    result = ''

    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            for rules_chunk in chunk['rulebase']:
                result += csv_dump_rules(rules_chunk, layer_name)
    else:
        if 'rulebase' in rulebase:
            # add section header, but only if it does not exist yet (can happen by chunking a section)
            if rulebase['type'] == 'access-section' and not rulebase['uid'] in section_header_uids:
                section_name = ""
                if 'name' in rulebase:
                    section_name = rulebase['name']
                #else:
                #     print ("warning: found access-section without defined rulebase.name, rulebase uid=" + rulebase['uid'])
                rule_num = rule_num + 1
                section_header = create_section_header(section_name, layer_name, rulebase['uid'])
                result += section_header
            for rule in rulebase['rulebase']:
                result += csv_dump_rule(rule, layer_name)
        if 'rule-number' in rulebase:
            result += csv_dump_rule(rulebase, layer_name)
    return result

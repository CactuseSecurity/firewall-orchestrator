import re
import logging
import common


def create_section_header(section_name, layer_name, import_id, rule_uid, rule_num, section_header_uids):
    section_header_uids.append(rule_uid)
    header_rule_csv = '"' + import_id + '"' + common.csv_delimiter  # control_id
    header_rule_csv += '"' + str(rule_num) + '"' + common.csv_delimiter  # rule_num
    header_rule_csv += '"' + layer_name + '"' + common.csv_delimiter  # rulebase_name
    header_rule_csv += common.csv_delimiter  # rule_ruleid
    header_rule_csv += '"' + 'false' + '"' + common.csv_delimiter  # rule_disabled
    header_rule_csv += '"' + 'False' + '"' + common.csv_delimiter  # rule_src_neg
    header_rule_csv += '"' + 'Any' + '"' + common.csv_delimiter  # src
    header_rule_csv += '"' + common.any_obj_uid + '"' + common.csv_delimiter  # src_refs
    header_rule_csv += '"' + 'False' + '"' + common.csv_delimiter  # rule_dst_neg
    header_rule_csv += '"' + 'Any' + '"' + common.csv_delimiter  # dst
    header_rule_csv += '"' + common.any_obj_uid + '"' + common.csv_delimiter  # dst_refs
    header_rule_csv += '"' + 'False' + '"' + common.csv_delimiter  # rule_svc_neg
    header_rule_csv += '"' + 'Any' + '"' + common.csv_delimiter  # svc
    header_rule_csv += '"' + common.any_obj_uid + '"' + common.csv_delimiter  # svc_refs
    header_rule_csv += '"' + 'Accept' + '"' + common.csv_delimiter  # action
    header_rule_csv += '"' + 'Log' + '"' + common.csv_delimiter  # track
    header_rule_csv += '"' + 'Policy Targets' + '"' + common.csv_delimiter  # install-on
    header_rule_csv += '"' + 'Any' + '"' + common.csv_delimiter  # time
    header_rule_csv += '""' + common.csv_delimiter  # comments
    header_rule_csv += common.csv_delimiter  # name
    header_rule_csv += '"' + rule_uid + '"' + common.csv_delimiter  # uid
    header_rule_csv += '"' + section_name + '"' + common.csv_delimiter  # head_text
    header_rule_csv += common.csv_delimiter  # from_zone
    header_rule_csv += common.csv_delimiter  # to_zone
    # last_change_admin
    return header_rule_csv + common.line_delimiter


def csv_add_field(content, csv_del, apostrophe):
    if content == '':  # do not add apostrophes for empty fields
        field_result = csv_del
    else:
        field_result = apostrophe + content + apostrophe + csv_del
    return field_result


def csv_dump_rule(rule, layer_name, import_id, rule_num):
    apostrophe = '"'
    rule_csv = ''

    if 'type' in rule and rule['type'] != 'place-holder':  # standard rule, no section header
        if 'rule-number' in rule:  # standard rule, no section header
            # print ("rule #" + str(rule['rule-number']) + "\n")
            rule_csv += csv_add_field(import_id, common.csv_delimiter, apostrophe)  # control_id
            rule_csv += csv_add_field(str(rule_num), common.csv_delimiter, apostrophe)  # rule_num
            rule_csv += csv_add_field(layer_name, common.csv_delimiter, apostrophe)  # rulebase_name
            rule_csv += csv_add_field('', common.csv_delimiter, apostrophe)  # rule_ruleid is empty
            rule_disabled = 'False'
            if 'enabled' in rule and rule['enabled']=='false':
                rule_disabled = 'True'
            rule_csv += csv_add_field(rule_disabled, common.csv_delimiter, apostrophe)  # rule_disabled
            rule_csv += csv_add_field(str(rule['source-negate']), common.csv_delimiter, apostrophe)  # src_neg

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
            rule_csv += csv_add_field(rule_src_name, common.csv_delimiter, apostrophe)  # src_names

            # SOURCE refs
            rule_src_ref = ''
            for src in rule["source"]:
                if src['type'] == 'LegacyUserAtLocation':
                    rule_src_ref += src["userGroup"] + '@' + src["location"] + common.list_delimiter
                elif src['type'] == 'access-role':
                    if isinstance(src['networks'], str):  # just a single source
                        if src['networks'] == 'any':
                            rule_src_ref += src['uid'] + '@' + common.any_obj_uid + common.list_delimiter
                        else:
                            rule_src_ref += src['uid'] + '@' + src['networks'] + common.list_delimiter
                    else:  # more than one source
                        for nw in src['networks']:
                            rule_src_ref += src['uid'] + '@' + nw + common.list_delimiter
                else:  # standard network objects as source
                    rule_src_ref += src["uid"] + common.list_delimiter
            rule_src_ref = rule_src_ref[:-1]  # removing last list_delimiter
            rule_csv += csv_add_field(rule_src_ref, common.csv_delimiter, apostrophe)  # src_refs

            rule_csv += csv_add_field(str(rule['destination-negate']), common.csv_delimiter, apostrophe)  # destination negation

            rule_dst_name = ''
            for dst in rule["destination"]:
                rule_dst_name += dst["name"] + common.list_delimiter
            rule_dst_name = rule_dst_name[:-1]
            rule_csv += csv_add_field(rule_dst_name, common.csv_delimiter, apostrophe)  # rule dest_name

            rule_dst_ref = ''
            for dst in rule["destination"]:
                rule_dst_ref += dst["uid"] + common.list_delimiter
            rule_dst_ref = rule_dst_ref[:-1]
            rule_csv += csv_add_field(rule_dst_ref, common.csv_delimiter, apostrophe)  # rule_dest_refs

            # SERVICE names
            rule_svc_name = ''
            rule_svc_name += str(rule['service-negate']) + '"' + common.csv_delimiter + '"'
            for svc in rule["service"]:
                rule_svc_name += svc["name"] + common.list_delimiter
            rule_svc_name = rule_svc_name[:-1]
            rule_csv += csv_add_field(rule_svc_name, common.csv_delimiter, apostrophe)  # rule svc name

            # SERVICE refs
            rule_svc_ref = ''
            for svc in rule["service"]:
                rule_svc_ref += svc["uid"] + common.list_delimiter
            rule_svc_ref = rule_svc_ref[:-1]
            rule_csv += csv_add_field(rule_svc_ref, common.csv_delimiter, apostrophe)  # rule svc ref

            rule_action = rule['action']
            rule_action_name = rule_action['name']
            rule_csv += csv_add_field(rule_action_name, common.csv_delimiter, apostrophe)  # rule action
            rule_track = rule['track']
            rule_track_type = rule_track['type']
            rule_csv += csv_add_field(rule_track_type['name'], common.csv_delimiter, apostrophe)  # rule track

            rule_install_on = rule['install-on']
            first_rule_install_target = rule_install_on[0]
            rule_csv += csv_add_field(first_rule_install_target['name'], common.csv_delimiter, apostrophe)  # install on

            rule_time = rule['time']
            first_rule_time = rule_time[0]
            rule_csv += csv_add_field(first_rule_time['name'], common.csv_delimiter, apostrophe)  # time

            rule_csv += csv_add_field(rule['comments'], common.csv_delimiter, apostrophe)  # time

            if 'name' in rule:
                rule_name = rule['name']
            else:
                rule_name = ''
            rule_csv += csv_add_field(rule_name, common.csv_delimiter, apostrophe)  # rule_name

            rule_csv += csv_add_field(rule['uid'], common.csv_delimiter, apostrophe)  # rule_head_text
            rule_head_text = ''
            rule_csv += csv_add_field(rule_head_text, common.csv_delimiter, apostrophe)  # rule_head_text
            rule_from_zone = ''
            rule_csv += csv_add_field(rule_from_zone, common.csv_delimiter, apostrophe)
            rule_to_zone = ''
            rule_csv += csv_add_field(rule_to_zone, common.csv_delimiter, apostrophe)
            rule_meta_info = rule['meta-info']
            rule_csv += csv_add_field(rule_meta_info['last-modifier'], common.csv_delimiter, apostrophe)

            rule_csv = rule_csv[:-1] + common.line_delimiter  # remove last csv delimiter and add line delimiter
    return rule_csv


def csv_dump_rules(rulebase, layer_name, import_id, rule_num, section_header_uids):
    result = ''

    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            for rules_chunk in chunk['rulebase']:
                rule_num, rules_in_csv = csv_dump_rules(rules_chunk, layer_name, import_id, rule_num, section_header_uids)
                result += rules_in_csv
    else:
        if 'rulebase' in rulebase:
            # add section header, but only if it does not exist yet (can happen by chunking a section)
            if rulebase['type'] == 'access-section' and not rulebase['uid'] in section_header_uids:
                section_name = ""
                if 'name' in rulebase:
                    section_name = rulebase['name']
                #else:
                #     print ("warning: found access-section without defined rulebase.name, rulebase uid=" + rulebase['uid'])
                section_header = create_section_header(section_name, layer_name, import_id, rulebase['uid'], rule_num, section_header_uids)
                rule_num += 1
                result += section_header
            for rule in rulebase['rulebase']:
                result += csv_dump_rule(rule, layer_name, import_id, rule_num)
                rule_num += 1
        if 'rule-number' in rulebase:
            result += csv_dump_rule(rulebase, layer_name, import_id, rule_num)
            rule_num += 1
    return rule_num, result

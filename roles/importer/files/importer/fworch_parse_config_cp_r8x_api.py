import argparse
import json
import re
import logging

parser = argparse.ArgumentParser(description='parse json configuration file from Check Point R8x management')
parser.add_argument('-f', '--config_file', required=True, help='name of config file to parse (json format)')
parser.add_argument('-i', '--import_id', default='0', help='unique import id')
parser.add_argument('-m', '--management_name', default='<mgmt-name>', help='name of management system to import')
parser.add_argument('-r', '--rulebase', default='', help='name of rulebase to import')
parser.add_argument('-n', '--network_objects', action="store_true", help='import network objects')
parser.add_argument('-s', '--service_objects', action="store_true", help='import service objects')
parser.add_argument('-u', '--users', action="store_true", help='import users')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0', help='Debug Level: 0(off) 1(DEBUG Console) 2(DEBUG File)i 2(DEBUG Console&File); default=0')
args = parser.parse_args()

csv_delimiter = '%'
list_delimiter = '|'
line_delimiter = "\n"
found_rulebase = False

# logging config
debug_level = int(args.debug)
# todo: save the initial value, reset initial value at the end
if debug_level == 1:
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
elif debug_level == 2:
    logging.basicConfig(filename='/var/tmp/fworch_get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
elif debug_level == 3:
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    logging.basicConfig(filename='/var/tmp/fworch_get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')


# ###################### rule handling ###############################################


def create_section_header(section_name, layer_name, any_object_uid, rule_uid):
    global rule_num
    global number_of_section_headers_so_far
    header_rule_csv = '"' + args.import_id + '"' + csv_delimiter  # control_id
    header_rule_csv += '"' + str(rule_num) + '"' + csv_delimiter  # rule_num
    header_rule_csv += '"' + layer_name + '"' + csv_delimiter  # rulebase_name
    header_rule_csv += csv_delimiter  # rule_ruleid
    header_rule_csv += '"' + 'false' + '"' + csv_delimiter  # rule_disabled
    header_rule_csv += '"' + 'False' + '"' + csv_delimiter  # rule_src_neg
    header_rule_csv += '"' + 'Any' + '"' + csv_delimiter  # src
    header_rule_csv += '"' + any_object_uid + '"' + csv_delimiter  # src_refs
    header_rule_csv += '"' + 'False' + '"' + csv_delimiter  # rule_dst_neg
    header_rule_csv += '"' + 'Any' + '"' + csv_delimiter  # dst
    header_rule_csv += '"' + any_object_uid + '"' + csv_delimiter  # dst_refs
    header_rule_csv += '"' + 'False' + '"' + csv_delimiter  # rule_svc_neg
    header_rule_csv += '"' + 'Any' + '"' + csv_delimiter  # svc
    header_rule_csv += '"' + any_object_uid + '"' + csv_delimiter  # svc_refs
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
                        # TODO: this is a hack with a hard-coded any obj uid -->
                        any_object_uid = "97aeb369-9aea-11d5-bd16-0090272ccb30"
                        # need to properly fix this as the Any obj uid is probylby differend on every mgmt
                        rule_src_ref += src['uid'] + '@' + any_object_uid + list_delimiter
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


def csv_dump_rules(rulebase, layer_name, any_object_uid):
    global rule_num
    global number_of_section_headers_so_far
    result = ''
    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            for rules_chunk in chunk['rulebase']:
                result += csv_dump_rules(rules_chunk, layer_name, any_obj_uid)
    else:
        if 'rulebase' in rulebase:
            # add section header
            if rulebase['type'] == 'access-section':
                section_name = rulebase['name']
                number_of_section_headers_so_far += 1
                rule_num = rule_num + 1
                section_header_uid = rulebase['uid'] + '-section-header-' + str(number_of_section_headers_so_far)
                section_header = create_section_header(section_name, layer_name, any_obj_uid, section_header_uid)
                result += section_header
            for rule in rulebase['rulebase']:
                result += csv_dump_rule(rule, layer_name)
        if 'rule-number' in rulebase:
            result += csv_dump_rule(rulebase, layer_name)
    return result


####################### user handling ###############################################

def csv_dump_user(user_name):
    user_dict = users[user_name]
    user_line = '"' + args.import_id + '"' + csv_delimiter
    user_line += '"' + user_name + '"' + csv_delimiter
    user_line += '"' + user_dict['user_type'] + '"' + csv_delimiter  # user_typ
    user_line += csv_delimiter  # user_member_names
    user_line += csv_delimiter  # user_member_refs
    user_line += csv_delimiter  # user_color
    user_line += csv_delimiter  # user_comment
    user_line += '"' + user_dict['uid'] + '"'  # user_uid
    user_line += csv_delimiter  # user_valid_until
    user_line += csv_delimiter  # last_change_admin
    user_line += line_delimiter
    return user_line


def collect_users_from_rule(rule):
    if 'rule-number' in rule:  # standard rule
        for src in rule["source"]:
            if src['type'] == 'access-role':
                users[src['name']] = {'uid': src['uid'], 'user_type': 'group', 'comment': src['comments'],
                                      'color': src['color']}
                if 'users' in src:
                    users[src["name"]] = {'uid': src["uid"], 'user_type': 'simple'}
            elif src['type'] == 'LegacyUserAtLocation':
                user_str = src["name"]
                user_ar = user_str.split('@')
                user_name = user_ar[0]
                user_uid = src["userGroup"]
                #                users[user_name] = user_uid
                users[user_name] = {'uid': user_uid, 'user_type': 'group'}
    else:  # section
        collect_users_from_rulebase(rule["rulebase"])


# collect_users writes user info into global users dict
def collect_users_from_rulebase(rulebase):
    result = ''
    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            for rule in chunk['rulebase']:
                collect_users_from_rule(rule)
    else:
        for rule in rulebase:
            collect_users_from_rule(rule)


####################### nw_obj handling ###############################################

def csv_dump_nw_obj(nw_obj):
    result_line = '"' + args.import_id + '"' + csv_delimiter  # control_id
    result_line += '"' + nw_obj['obj_name'] + '"' + csv_delimiter  # obj_name
    result_line += '"' + nw_obj['obj_typ'] + '"' + csv_delimiter  # ob_typ
    result_line += '"' + nw_obj['obj_member_names'] + '"' + csv_delimiter  # obj_member_names
    result_line += '"' + nw_obj['obj_member_refs'] + '"' + csv_delimiter  # obj_member_refs
    result_line += csv_delimiter  # obj_sw
    result_line += '"' + nw_obj['obj_ip'] + '"' + csv_delimiter  # obj_ip
    result_line += csv_delimiter  # result_line += '"' + nw_obj['obj_ip_end'] + '"' + csv_delimiter         # obj_ip_end
    result_line += '"' + nw_obj['obj_color'] + '"' + csv_delimiter  # obj_color
    result_line += '"' + nw_obj['obj_comment'] + '"' + csv_delimiter  # obj_comment
    result_line += csv_delimiter  # result_line += '"' + nw_obj['obj_location'] + '"' + csv_delimiter       # obj_location
    result_line += csv_delimiter  # result_line += '"' + nw_obj['obj_zone'] + '"' + csv_delimiter           # obj_zone
    result_line += '"' + nw_obj['obj_uid'] + '"' + csv_delimiter  # obj_uid
    result_line += csv_delimiter  # last_change_admin
    # add last_change_time
    result_line += line_delimiter
    return result_line


# def nw_objs_add_any_obj(uid_any_obj):
#    nw_objects.extend([{ 'obj_uid': uid_any_obj, 'obj_name': 'Any', 'obj_color': 'black', 
#                            'obj_comment': 'Any obj added by fworch',
#                            'obj_typ': 'network', 'obj_ip': '0.0.0.0/0', 'obj_member_refs': '', 'obj_member_names': '' }])

def get_ip_of_obj(obj):
    if 'ipv4-address' in obj:
        ip_addr = obj['ipv4-address']
    elif 'ipv6-address' in obj:
        ip_addr = obj['ipv6-address']
    elif 'subnet4' in obj:
        ip_addr = obj['subnet4'] + '/' + str(obj['mask-length4'])
    elif 'subnet6' in obj:
        ip_addr = obj['subnet6'] + '/' + str(obj['mask-length6'])
    else:
        ip_addr = '0.0.0.0/0'
    return ip_addr


# collect_nw_objects writes nw objects info into global nw_objects dict
def collect_nw_objects(object_table):
    result = ''
    #    nw_obj_tables = [ 'hosts', 'networks', 'groups', 'address-ranges', 'groups-with-exclusion', 'simple-gateways',
    #                     'security-zones', 'dynamic-objects', 'trusted-clients', 'dns-domains' ]
    nw_obj_tables = ['hosts', 'networks', 'simple-gateways', 'address-ranges', 'groups']
    if object_table['object_type'] in nw_obj_tables:
        for chunk in object_table['object_chunks']:
            for obj in chunk['objects']:
                members = ''
                ip_addr = ''
                member_refs = ''
                member_names = ''
                if 'members' in obj:
                    for member in obj['members']:
                        if 'name' in member:
                            member_names += member['name'] + list_delimiter
                            member_refs += member['uid'] + list_delimiter
                        # else:
                        #    print('warning: no name found for network object group ' + obj['name'])
                    member_names = member_names[:-1]
                    member_refs = member_refs[:-1]
                ip_addr = get_ip_of_obj(obj)
                obj_type = obj['type']
                if obj_type == 'address-range':
                    obj_type = 'ip_range'  # TODO: change later?
                if obj_type == 'simple-gateway':
                    obj_type = 'host'
                nw_objects.extend([{'obj_uid': obj['uid'], 'obj_name': obj['name'], 'obj_color': obj['color'],
                                    'obj_comment': obj['comments'],
                                    'obj_typ': obj_type, 'obj_ip': ip_addr,
                                    'obj_member_refs': member_refs, 'obj_member_names': member_names}])


####################### nw_obj handling: read from rulebase ###############################################

def collect_nw_objs_from_rule(rule):
    global nw_objects
    if 'rule-number' in rule:  # standard rule
        for obj in rule["source"]:
            if (obj['type'] == 'CpmiGatewayPlain' or obj['type'] == 'CpmiHostCkp' or obj['type'] == 'CpmiAnyObject' or obj['type'] == 'checkpoint-host'):
                comment = 'Any network object read from rulebase' if obj['type'] == 'CpmiAnyObject' else obj['comments']
                ip_addr = get_ip_of_obj(obj)
                current_element = {'obj_uid': obj['uid'], 'obj_name': obj['name'], 'obj_color': obj['color'],
                                   'obj_comment': comment,
                                   'obj_typ': 'host', 'obj_ip': ip_addr,
                                   'obj_member_refs': '', 'obj_member_names': ''}
                if nw_objects.count(current_element) == 0:
                    nw_objects.append(current_element)
        for obj in rule["destination"]:
            if (obj['type'] == 'CpmiGatewayPlain' or obj['type'] == 'CpmiHostCkp' or obj['type'] == 'CpmiAnyObject' or obj['type'] == 'checkpoint-host'):
                comment = 'Any network object read from rulebase' if obj['type'] == 'CpmiAnyObject' else obj['comments']
                ip_addr = get_ip_of_obj(obj)
                current_element = {'obj_uid': obj['uid'], 'obj_name': obj['name'], 'obj_color': obj['color'],
                                   'obj_comment': comment,
                                   'obj_typ': 'host', 'obj_ip': ip_addr,
                                   'obj_member_refs': '', 'obj_member_names': ''}
                if nw_objects.count(current_element) == 0:
                    nw_objects.append(current_element)
    else:  # section
        collect_nw_objs_from_rulebase(rule["rulebase"])


# collect_users writes user info into global users dict
def collect_nw_objs_from_rulebase(rulebase):
    result = ''
    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            for rule in chunk['rulebase']:
                collect_nw_objs_from_rule(rule)
    else:
        for rule in rulebase:
            collect_nw_objs_from_rule(rule)


####################### svc obj handling ###############################################

def csv_dump_svc_obj(svc_obj):
    result_line = '"' + args.import_id + '"' + csv_delimiter  # control_id
    result_line += '"' + svc_obj['svc_name'] + '"' + csv_delimiter  # svc_name
    result_line += '"' + svc_obj['svc_typ'] + '"' + csv_delimiter  # svc_typ
    result_line += '"' + svc_obj['svc_typ'] + '"' + csv_delimiter  # svc_prod_specific
    result_line += '"' + svc_obj['svc_member_names'] + '"' + csv_delimiter  # svc_member_names
    result_line += '"' + svc_obj['svc_member_refs'] + '"' + csv_delimiter  # obj_member_refs
    result_line += '"' + svc_obj['svc_color'] + '"' + csv_delimiter  # svc_color
    result_line += '"' + svc_obj['ip_proto'] + '"' + csv_delimiter  # ip_proto
    result_line += str(svc_obj['svc_port']) + csv_delimiter  # svc_port
    result_line += str(svc_obj['svc_port_end']) + csv_delimiter  # svc_port_end
    result_line += csv_delimiter  # result_line += '"' + svc_obj['svc_source_port'] + '"' + csv_delimiter       # svc_source_port
    result_line += csv_delimiter  # result_line += '"' + svc_obj['svc_source_port_end'] + '"' + csv_delimiter   # svc_source_port_end
    result_line += '"' + svc_obj['svc_comment'] + '"' + csv_delimiter  # svc_comment
    result_line += '"' + str(svc_obj['rpc_nr']) + '"' + csv_delimiter  # rpc_nr
    result_line += csv_delimiter  # result_line += '"' + svc_obj['svc_timeout_std'] + '"' + csv_delimiter       # svc_timeout_std
    result_line += str(svc_obj['svc_timeout']) + csv_delimiter  # svc_timeout
    result_line += '"' + svc_obj['svc_uid'] + '"' + csv_delimiter  # svc_uid
    result_line += csv_delimiter  # last_change_admin
    #  last_change_time
    result_line += line_delimiter
    return result_line


# collect_users writes user info into global users dict
def collect_svc_objects(object_table):
    global svc_objects
    result = ''
    #    svc_obj_tables = [ 'services-tcp', 'services-udp', 'services-sctp', 'services-other', 'service-groups', 'services-dce-rpc', 'services-rpc' ]
    svc_obj_tables = ['services-tcp', 'services-udp', 'service-groups', 'services-dce-rpc', 'services-rpc',
                      'services-other']

    if object_table['object_type'] in svc_obj_tables:
        proto = ''
        session_timeout = ''
        typ = 'undef'
        proto = '0'
        if object_table['object_type'] == 'service-groups':
            typ = 'group'
            proto = '0'
        if object_table['object_type'] == 'services-tcp':
            typ = 'simple'
            proto = '6'
        if object_table['object_type'] == 'services-udp':
            typ = 'simple'
            proto = '17'
        if object_table['object_type'] == 'services-dce-rpc' or object_table['object_type'] == 'services-rpc':
            typ = 'simple'
            proto = ''
        if object_table['object_type'] == 'services-other':
            typ = 'simple'
            proto = '0'
        for chunk in object_table['object_chunks']:
            for obj in chunk['objects']:
                ip_addr = ''
                member_refs = ''
                member_names = ''
                port = ''
                port_end = ''
                rpc_nr = ''
                if 'members' in obj:
                    member_refs = ''
                    member_names = ''
                    for member in obj['members']:
                        if 'name' in member:
                            member_names += member['name'] + list_delimiter
                            member_refs += member['uid'] + list_delimiter
                        # else:
                        #    print('warning: no name found for service group ' + obj['name'])
                    member_names = member_names[:-1]
                    member_refs = member_refs[:-1]
                if 'session-timeout' in obj:
                    session_timeout = str(obj['session-timeout'])
                if 'interface-uuid' in obj:
                    rpc_nr = obj['interface-uuid']
                if 'program-number' in obj:
                    rpc_nr = obj['program-number']
                if 'port' in obj:
                    port = str(obj['port'])
                    port_end = port
                    pattern = re.compile('^\>(\d+)$')
                    match = pattern.match(port)
                    if match:
                        port = str(int(match.group()[1:]) + 1)
                        port_end = str(65535)
                    pattern = re.compile('^\<(\d+)$')
                    match = pattern.match(port)
                    if match:
                        port = str(1)
                        port_end = str(int(match.group()[1:]) - 1)
                    pattern = re.compile('^(\d+)\-(\d+)$')
                    match = pattern.match(port)
                    if match:
                        port, port_end = match.group().split('-')
                else:
                    # rpc, group - setting ports to 0
                    port = '0'
                    port_end = '0'
                if not 'color' in obj:
                    # print('warning: no color found for service ' + obj['name'])
                    obj['color'] = 'black'
                svc_objects.extend([{'svc_uid': obj['uid'], 'svc_name': obj['name'], 'svc_color': obj['color'],
                                     'svc_comment': obj['comments'],
                                     'svc_typ': typ, 'svc_port': port, 'svc_port_end': port_end,
                                     'svc_member_refs': member_refs,
                                     'svc_member_names': member_names, 'ip_proto': proto,
                                     'svc_timeout': session_timeout,
                                     'rpc_nr': rpc_nr}])


####################### service handling: read from rulebase ###############################################

def collect_svcs_from_rule(rule):
    global svc_objects
    if 'rule-number' in rule:  # standard rule
        for obj in rule["service"]:
            if obj['type'] == 'service-icmp':
                current_element = {'svc_uid': obj['uid'], 'svc_name': obj['name'], 'svc_color': obj['color'],
                                   'svc_comment': obj['comments'],
                                   'svc_typ': 'simple', 'svc_port': '', 'svc_port_end': '', 'svc_member_refs': '',
                                   'svc_member_names': '', 'ip_proto': '1', 'svc_timeout': '',
                                   'rpc_nr': ''}
                if svc_objects.count(current_element) == 0:
                    svc_objects.append(current_element)
            if obj['type'] == 'CpmiAnyObject':
                current_element = {'svc_uid': obj['uid'], 'svc_name': obj['name'], 'svc_color': obj['color'],
                                   'svc_comment': 'Any service object read from rulebase',
                                   'svc_typ': 'simple', 'svc_port': '1', 'svc_port_end': '65535', 'svc_member_refs': '',
                                   'svc_member_names': '', 'ip_proto': '255', 'svc_timeout': '',
                                   'rpc_nr': ''}
                if svc_objects.count(current_element) == 0:
                    svc_objects.append(current_element)
    else:  # section
        collect_svcs_from_rulebase(rule["rulebase"])


# def svc_objs_add_any_obj(uid_any_obj):
#    # TODO: need to parse the any-uid from rules
#    svc_objects.extend([{ 'svc_uid': uid_any_obj, 'svc_name': 'Any', 'svc_color': 'black', 'svc_comment': 'Svc Any obj by FWORCH',
#                          'svc_typ': 'simple', 'svc_port': '1', 'svc_port_end': '65535', 'svc_member_refs': '', 
#                          'svc_member_names': '', 'ip_proto': '255', 'svc_timeout': '3600', 'rpc_nr': '' }])

# collect_users writes user info into global users dict
def collect_svcs_from_rulebase(rulebase):
    result = ''
    if 'layerchunks' in rulebase:
        for chunk in rulebase['layerchunks']:
            for rule in chunk['rulebase']:
                collect_svcs_from_rule(rule)
    else:
        for rule in rulebase:
            collect_svcs_from_rule(rule)


def get_any_obj_uid(rulebase):
    #    return "97aeb369-9aea-11d5-bd16-0090272ccb30"
    global nw_objects
    global svc_objects

    collect_nw_objs_from_rulebase(rulebase)
    collect_svcs_from_rulebase(rulebase)
    for obj in nw_objects:
        if obj['obj_name'] == 'Any':
            return obj['obj_uid']
    for obj in svc_objects:
        if obj['obj_name'] == 'Any':
            return obj['obj_uid']
    return 'dummy any obj uid (not found in rulebase)'
    # print "ERROR: fond no Any object in rulebase!"


####################### main program ###############################################

# with io.open(args.config_file, "r", encoding="utf8") as json_data:
# with open(args.config_file, "r", encoding="utf8") as json_data:
with open(args.config_file, "r") as json_data:
    config = json.load(json_data)

logging.debug ("fworch_parse_config_cp_r8x_api - args"+ "\nf:" +args.config_file +"\ni: "+ args.import_id +"\nm: "+ args.management_name +"\nr: "+ args.rulebase +"\nn: "+ str(args.network_objects) +"\ns: "+ str(args.service_objects) +"\nu: "+ str(args.users) +"\nd: "+ str(args.debug))

# any_obj_uid = get_any_obj_uid()
# any_obj_uid = 'dummy any obj uid (not found in rulebase)'
any_obj_uid = "97aeb369-9aea-11d5-bd16-0090272ccb30"

number_of_section_headers_so_far = 0
rule_num = 0
nw_objects = []  # only used for storing any obj
svc_objects = []  # only used for storing any obj

# the any objects are needed for almost all cases:
for rulebase in config['rulebases']:
    collect_svcs_from_rulebase(rulebase)
    collect_nw_objs_from_rulebase(rulebase)

for obj in nw_objects:
    if obj['obj_name'] == 'Any':
        any_obj_uid = obj['obj_uid']
        logging.debug ("fworch_parse_config_cp_r8x_api - nw Any uid in rulebase:" + any_obj_uid)
for obj in svc_objects:
    if obj['svc_name'] == 'Any':
        any_obj_uid = obj['svc_uid']
        logging.debug ("fworch_parse_config_cp_r8x_api - svc Any uid in rulebase:" + any_obj_uid)

if args.rulebase != '':
    for rulebase in config['rulebases']:
        current_layer_name = rulebase['layername']
        if current_layer_name == args.rulebase:
            found_rulebase = True
            result = csv_dump_rules(rulebase, args.rulebase, any_obj_uid)

if args.network_objects:
    result = ''
    if args.network_objects != '':
        for obj_table in config['object_tables']:
            collect_nw_objects(obj_table)
    for rulebase in config['rulebases']:
        collect_nw_objs_from_rulebase(rulebase)
    for nw_obj in nw_objects:
        result += csv_dump_nw_obj(nw_obj)

if args.service_objects:
    result = ''
    if args.service_objects != '':
        for obj_table in config['object_tables']:
            collect_svc_objects(obj_table)
    for svc_obj in svc_objects:
        result += csv_dump_svc_obj(svc_obj)

if args.users:
    users = {}
    result = ''
    for rulebase in config['rulebases']:
        collect_users_from_rulebase(rulebase)
    for user in users.keys():
        result += csv_dump_user(user)

if args.rulebase != '' and not found_rulebase:
    print("PARSE ERROR: rulebase '" + args.rulebase + "' not found.")
else:
    result = result[:-1]  # strip off final line break to avoid empty last line
    # print(result.encode('utf-8'))
    print(result)

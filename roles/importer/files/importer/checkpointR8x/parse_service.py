base_dir = "/usr/local/fworch"

import sys
sys.path.append(base_dir + '/importer')
sys.path.append(base_dir + '/importer/checkpointR8x')
import re
import logging
import common, cpcommon


def csv_dump_svc_obj(svc_obj, import_id):
    result_line =  common.csv_add_field(import_id)                          # control_id
    result_line += common.csv_add_field(svc_obj['svc_name'])                # svc_name
    result_line += common.csv_add_field(svc_obj['svc_typ'])                 # svc_typ
    result_line += common.csv_add_field(svc_obj['svc_typ'])                 # svc_prod_specific
    result_line += common.csv_add_field(svc_obj['svc_member_names'])        # svc_member_names
    result_line += common.csv_add_field(svc_obj['svc_member_refs'])         # obj_member_refs
    result_line += common.csv_add_field(svc_obj['svc_color'])               # svc_color
    result_line += common.csv_add_field(svc_obj['ip_proto'])                # ip_proto
    result_line += str(svc_obj['svc_port']) + common.csv_delimiter          # svc_port
    result_line += str(svc_obj['svc_port_end']) + common.csv_delimiter      # svc_port_end
    if 'svc_source_port' in svc_obj:
        result_line += common.csv_add_field(svc_obj['svc_source_port'])     # svc_source_port
    else:
        result_line += common.csv_delimiter                                 # svc_source_port
    if 'svc_source_port_end' in svc_obj:
        result_line += common.csv_add_field(svc_obj['svc_source_port_end']) # svc_source_port_end
    else:
        result_line += common.csv_delimiter                                 # svc_source_port_end
    result_line += common.csv_add_field(svc_obj['svc_comment'])             # svc_comment
    result_line += common.csv_add_field(str(svc_obj['rpc_nr']))             # rpc_nr
    if 'svc_timeout_std' in svc_obj:
        result_line += common.csv_add_field(svc_obj['svc_timeout_std'])     # svc_timeout_std
    else:
        result_line += common.csv_delimiter                                 # svc_timeout_std
    if 'svc_timeout' in svc_obj and svc_obj['svc_timeout']!="":
        result_line += common.csv_add_field(str(svc_obj['svc_timeout']))    # svc_timeout
    else:
        result_line += common.csv_delimiter                                 # svc_timeout null
    result_line += common.csv_add_field(svc_obj['svc_uid'])                 # svc_uid
    result_line += common.csv_delimiter                                     # last_change_admin
    result_line += common.line_delimiter                                    # last_change_time
    return result_line


# collect_svcobjects writes svc info into global users dict
def collect_svc_objects(object_table, svc_objects):
    result = ''

    if object_table['object_type'] in cpcommon.svc_obj_table_names:
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
        if object_table['object_type'] == 'services-icmp' or object_table['object_type'] == 'services-icmp6':
            typ = 'simple'
            proto = '1'
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
                    for member in obj['members']:
                        member_refs += member + common.list_delimiter
                    member_refs = member_refs[:-1]
                if 'session-timeout' in obj:
                    session_timeout = str(obj['session-timeout'])
                else:
                    session_timeout = None
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
                                     'svc_member_names': '',
                                     'ip_proto': proto,
                                     'svc_timeout': session_timeout,
                                     'rpc_nr': rpc_nr}])


# return name of nw_objects element where obj_uid = uid
def resolve_svc_uid_to_name(uid, svc_objects):
    for obj in svc_objects:
        if obj['svc_uid'] == uid:
            return obj['svc_name']
    return 'ERROR: uid ' + uid + ' not found'


def add_member_names_for_svc_group(idx, svc_objects):
    member_names = ''
    group = svc_objects.pop(idx)
    svc_member_refs = group['svc_member_refs'].split(common.list_delimiter)

    for ref in svc_member_refs:
        member_name = resolve_svc_uid_to_name(ref, svc_objects)
        #print ("found member of group " + group['svc_name'] + ": " + member_name)
        member_names += member_name + common.list_delimiter
    group['svc_member_names'] = member_names[:-1]
    svc_objects.insert(idx, group)


def parse_service_objects(full_config, config2import, import_id):
    svc_objects = []
    for svc_table in full_config['object_tables']:
        collect_svc_objects(svc_table, svc_objects)
    for obj in svc_objects:
        obj.update({'control_id': import_id})
    for idx in range(0, len(svc_objects)-1):
        if svc_objects[idx]['svc_typ'] == 'group':
            add_member_names_for_svc_group(idx, svc_objects)
    config2import.update({'service_objects': svc_objects})
    
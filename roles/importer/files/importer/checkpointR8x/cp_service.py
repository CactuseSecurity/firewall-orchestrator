import re
import cp_const
from fwo_const import list_delimiter


# collect_svcobjects writes svc info into global users dict
def collect_svc_objects(object_table, svc_objects):
    proto_map = {
        'service-tcp': 6,
        'service-udp': 17,
        'service-icmp': 1
    }

    if object_table['object_type'] in cp_const.svc_obj_table_names:
        session_timeout = ''
        typ = 'undef'
        if object_table['object_type'] in cp_const.group_svc_obj_types:
            typ = 'group'
        if object_table['object_type'] in cp_const.simple_svc_obj_types:
            typ = 'simple'
        for chunk in object_table['object_chunks']:
            if 'objects' in chunk:
                for obj in chunk['objects']:
                    if 'type' in obj and obj['type'] in proto_map:
                        proto = proto_map[obj['type']]
                    elif 'ip-protocol' in obj:
                        proto = obj['ip-protocol']
                    else:
                        proto = None
                    member_refs = ''
                    port = ''
                    port_end = ''
                    rpc_nr = None
                    member_refs = None
                    if 'members' in obj:
                        member_refs = ''
                        for member in obj['members']:
                            member_refs += member + list_delimiter
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
                        else:
                            pattern = re.compile('^\<(\d+)$')
                            match = pattern.match(port)
                            if match:
                                port = str(1)
                                port_end = str(int(match.group()[1:]) - 1)
                            else:
                                pattern = re.compile('^(\d+)\-(\d+)$')
                                match = pattern.match(port)
                                if match:
                                    port, port_end = match.group().split('-')
                                else: # standard port without "<>-"
                                    pattern = re.compile('^(\d+)$')
                                    match = pattern.match(port)
                                    if match:
                                        # port stays unchanged
                                        port_end = port
                                    else:   # Any
                                        pattern = re.compile('^(Any)$')
                                        match = pattern.match(port)
                                        if match:
                                            port = str(1)
                                            port_end = str(65535)
                                        else:   # e.g. suspicious cases
                                            port = None
                                            port_end = None
                    else:
                        # rpc, group - setting ports to 0
                        port = None
                        port_end = None
                    if not 'color' in obj:
                        # print('warning: no color found for service ' + obj['name'])
                        obj['color'] = 'black'
                    if not 'comments' in obj or obj['comments'] == '':
                        obj['comments'] = None
                    svc_objects.extend([{'svc_uid': obj['uid'], 'svc_name': obj['name'], 'svc_color': obj['color'],
                                        'svc_comment': obj['comments'],
                                        'svc_typ': typ, 'svc_port': port, 'svc_port_end': port_end,
                                        'svc_member_refs': member_refs,
                                        'svc_member_names': None,
                                        'ip_proto': proto,
                                        'svc_timeout': session_timeout,
                                        'rpc_nr': rpc_nr
                                        }])


# return name of nw_objects element where obj_uid = uid
def resolve_svc_uid_to_name(uid, svc_objects):
    for obj in svc_objects:
        if obj['svc_uid'] == uid:
            return obj['svc_name']
    return 'ERROR: uid ' + uid + ' not found'


def add_member_names_for_svc_group(idx, svc_objects):
    member_names = ''
    group = svc_objects.pop(idx)

    if 'svc_member_refs' in group and group['svc_member_refs'] is not None:
        svc_member_refs = group['svc_member_refs'].split(list_delimiter)
        for ref in svc_member_refs:
            member_name = resolve_svc_uid_to_name(ref, svc_objects)
            member_names += member_name + list_delimiter
        group['svc_member_names'] = member_names[:-1]

    svc_objects.insert(idx, group)


def normalize_service_objects(full_config, config2import, import_id, debug_level=0):
    svc_objects = []
    for svc_table in full_config['object_tables']:
        collect_svc_objects(svc_table, svc_objects)
    for obj in svc_objects:
        obj.update({'control_id': import_id})
    for idx in range(0, len(svc_objects)-1):
        if svc_objects[idx]['svc_typ'] == 'group':
            add_member_names_for_svc_group(idx, svc_objects)
    config2import.update({'service_objects': svc_objects})

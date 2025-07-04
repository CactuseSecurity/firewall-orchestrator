import re
import cp_const
from fwo_const import list_delimiter


# collect_svcobjects writes svc info into global users dict
def collect_svc_objects(object_table, svc_objects):
    if object_table['type'] in cp_const.svc_obj_table_names:
        typ = 'undef'
        if object_table['type'] in cp_const.group_svc_obj_types:
            typ = 'group'
        if object_table['type'] in cp_const.simple_svc_obj_types:
            typ = 'simple'
        for chunk in object_table['chunks']:
            if 'objects' in chunk:
                for obj in chunk['objects']:
                    collect_single_svc_object(obj)
                    svc_objects.append({'svc_uid': obj['uid'], 'svc_name': obj['name'], 'svc_color': obj['color'],
                                            'svc_comment': obj['comments'], 'svc_domain': obj['domain_uid'],
                                            'svc_typ': typ, 'svc_port': obj['port'], 'svc_port_end': obj['port_end'],
                                            'svc_member_refs': obj['svc_member_refs'],
                                            'svc_member_names': None,
                                            'ip_proto': obj['proto'],
                                            'svc_timeout': obj['session_timeout'],
                                            'rpc_nr': obj['rpc_nr']
                                            })


def collect_single_svc_object(obj):
    """
    Collects a single service object and appends its details to the svc_objects list.
    Handles different types of service objects and normalizes port information.
    """
    proto_map = {
        'service-tcp': 6,
        'service-udp': 17,
        'service-icmp': 1
    }

    if 'type' in obj and obj['type'] in proto_map:
        proto = proto_map[obj['type']]
    elif 'ip-protocol' in obj:
        proto = obj['ip-protocol']
    else:
        proto = None
    obj['proto'] = proto
    
    rpc_nr = None
    member_refs = None
    if 'members' in obj:
        member_refs = ''
        for member in obj['members']:
            member_refs += member + list_delimiter
        member_refs = member_refs[:-1]
    obj['svc_member_refs'] = member_refs    

    if 'session-timeout' in obj:
        session_timeout = str(obj['session-timeout'])
    else:
        session_timeout = None
    obj['session_timeout'] = session_timeout
    if 'interface-uuid' in obj:
        rpc_nr = obj['interface-uuid']
    if 'program-number' in obj:
        rpc_nr = obj['program-number']
    obj['rpc_nr'] = rpc_nr

    obj['port'], obj['port_end'] = normalize_port(obj)

    if 'color' not in obj:
        obj['color'] = 'black'
    if 'comments' not in obj or obj['comments'] == '':
        obj['comments'] = None
    obj['domain_uid'] = get_obj_domain_uid(obj)    
 

def normalize_port(obj) -> tuple[str, str]:
    """
    Normalizes the port information in the given object.
    If the 'port' key exists, it processes the port value to handle ranges and special cases.
    """
    port = None
    port_end = None
    if 'port' in obj:
        port = str(obj['port'])
        pattern = re.compile(r'^\>(\d+)$')
        match = pattern.match(port)
        if match:
            return str(int(match.group()[1:]) + 1), str(65535) 
        pattern = re.compile(r'^\<(\d+)$')
        match = pattern.match(port)
        if match:
            return str(1), str(int(match.group()[1:]) - 1)
        pattern = re.compile(r'^(\d+)\-(\d+)$')
        match = pattern.match(port)
        if match:
            return match.group().split('-')

        # standard port without "<>-"
        pattern = re.compile(r'^(\d+)$')
        match = pattern.match(port)
        if match:
            # port stays unchanged
            port_end = port
        else:   # Any
            pattern = re.compile(r'^(Any)$')
            match = pattern.match(port)
            if match:
                port = str(1)
                port_end = str(65535)
            else:   # e.g. suspicious cases
                port = None
                port_end = None
    return port, port_end


def get_obj_domain_uid(obj):
    """
    Returns the domain UID for the given object.
    If the object has a 'domain' key with a 'uid', it returns that UID.
    Otherwise, it returns the global domain UID.
    """
    if 'domain' in obj and 'uid' in obj['domain']:
        return obj['domain']['uid']
    else:
        return "DUMMY" # TODO: set domain uid correctly (updatable objects?)
    

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
    for obj_dict in full_config['objects']:
            collect_svc_objects(obj_dict, svc_objects)
    for obj in svc_objects:
        obj.update({'control_id': import_id})
    for idx in range(0, len(svc_objects)-1):
        if svc_objects[idx]['svc_typ'] == 'group':
            add_member_names_for_svc_group(idx, svc_objects)
    config2import.update({'service_objects': svc_objects})

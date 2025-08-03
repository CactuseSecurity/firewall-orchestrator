import re
from fwo_const import list_delimiter
from model_controllers.import_state_controller import ImportStateController
from fwo_log import getFwoLogger

def normalize_service_objects(import_state: ImportStateController, native_config, native_config_global, normalized_config, 
                              normalized_config_global, svc_obj_types):
    svc_objects = []
    logger = getFwoLogger()
    
    if 'objects' not in native_config:
        return # no objects to normalize
    for current_obj_type in native_config['objects']:
        if not(current_obj_type in svc_obj_types and 'data' in native_config['objects'][current_obj_type]):
            continue
        for obj_orig in native_config['objects'][current_obj_type]['data']:
            normalize_service_object(obj_orig, svc_objects)

    if native_config.get('is-super-manager', False):
        # finally add "Original" service object for natting (global domain only)
        original_obj_name = 'Original'
        svc_objects.append(create_svc_object(name=original_obj_name, proto=0, color='1', port=None,\
            comment='"original" service object created by FWO importer for NAT purposes'))

    normalized_config.update({'service_objects': svc_objects})

def normalize_service_object(obj_orig, svc_objects):
    member_names = ''
    if 'member' in obj_orig:
        svc_type = 'group'
        for member in obj_orig['member']:
            member_names += member + list_delimiter
        member_names = member_names[:-1]
    else:
        svc_type = 'simple'

    name = None
    if 'name' in obj_orig:
        name = str(obj_orig['name'])

    color = str(obj_orig.get('color', 1))
    
    session_timeout = None   # todo: find the right timer

    if 'protocol' in obj_orig:
        handle_svc_protocol(obj_orig, svc_objects, svc_type, name, color, session_timeout)
    else:
        if svc_type == 'group':
            add_object(svc_objects, svc_type, name, color, 0, None, member_names, session_timeout)
        else:
            add_object(svc_objects, svc_type, name, color, 0, None, None, session_timeout)


def handle_svc_protocol(obj_orig, svc_objects, svc_type, name, color, session_timeout):
    proto = 0
    range_names = ''
    added_svc_obj = 0

    # forti uses strange protocol numbers, so we need to map them

    match obj_orig['protocol']:
        case 1:
            add_object(svc_objects, svc_type, name, color, 1, None, None, session_timeout)
            added_svc_obj += 1
        case 2:
            if 'protocol-number' in obj_orig:
                proto = obj_orig['protocol-number']
            add_object(svc_objects, svc_type, name, color, proto, None, None, session_timeout)
            added_svc_obj += 1
        case 5 | 11:
            parse_standard_protocols_with_ports(obj_orig, svc_objects, svc_type, name, color, session_timeout, range_names, added_svc_obj)
        case 6:
            add_object(svc_objects, svc_type, name, color, 58, None, None, session_timeout)
        case _:
            pass # not doing anything for other protocols, e.g. GRE, ESP, ...


def parse_standard_protocols_with_ports(obj_orig, svc_objects, svc_type, name, color, session_timeout, range_names, added_svc_obj):
    split = check_split(obj_orig)
    if "tcp-portrange" in obj_orig and len(obj_orig['tcp-portrange']) > 0:
        tcpname = name
        if split:
            tcpname += "_tcp"
            range_names += tcpname + list_delimiter
        add_object(svc_objects, svc_type, tcpname, color, 6, obj_orig['tcp-portrange'], None, session_timeout)
        added_svc_obj += 1
    if "udp-portrange" in obj_orig and len(obj_orig['udp-portrange']) > 0:
        udpname = name
        if split:
            udpname += "_udp"
            range_names += udpname + list_delimiter
        add_object(svc_objects, svc_type, udpname, color, 17, obj_orig['udp-portrange'], None, session_timeout)
        added_svc_obj += 1
    if "sctp-portrange" in obj_orig and len(obj_orig['sctp-portrange']) > 0:
        sctpname = name
        if split:
            sctpname += "_sctp"
            range_names += sctpname + list_delimiter
        add_object(svc_objects, svc_type, sctpname, color, 132, obj_orig['sctp-portrange'], None, session_timeout)
        added_svc_obj += 1
    if split:
        range_names = range_names[:-1]
        add_object(svc_objects, 'group', name, color, 0, None, range_names, session_timeout)
        added_svc_obj += 1
    if added_svc_obj==0: # assuming RPC service which here has no properties at all
        add_object(svc_objects, 'rpc', name, color, 0, None, None, None)
        added_svc_obj += 1


def check_split(obj_orig):
    count = 0
    if "tcp-portrange" in obj_orig and len(obj_orig['tcp-portrange']) > 0:
        count += 1
    if "udp-portrange" in obj_orig and len(obj_orig['udp-portrange']) > 0:
        count += 1
    if "sctp-portrange" in obj_orig and len(obj_orig['sctp-portrange']) > 0:
        count += 1
    return (count > 1)


def extractPorts(port_ranges):
    ports = []
    port_ends = []
    if port_ranges is not None and len(port_ranges) > 0:
        for port_range in port_ranges:
            # remove src-ports
            port = port_range.split(':')[0]
            port_end = port

            # open ranges (not found so far in data)
            pattern = re.compile(r'^\>(\d+)$')
            match = pattern.match(port)
            if match:
                port = str(int(match.group()[1:]) + 1)
                port_end = str(65535)
            pattern = re.compile(r'^\<(\d+)$')
            match = pattern.match(port)
            if match:
                port = str(1)
                port_end = str(int(match.group()[1:]) - 1)

            # split ranges
            pattern = re.compile(r'^(\d+)\-(\d+)$')
            match = pattern.match(port)
            if match:
                port, port_end = match.group().split('-')
            ports.append(port)
            port_ends.append(port_end)
    return ports, port_ends


def create_svc_object(name, proto, color, port, comment):
    return {
        'svc_name': name,
        'svc_typ': 'simple',
        'svc_port': port,
        'ip_proto': proto,
        'svc_color': color,
        'svc_uid': name,    # services have no uid in fortimanager
        'svc_comment': comment
    }


def add_object(svc_objects, type, name, color, proto, port_ranges, member_names, session_timeout):
    if port_ranges is None:
        svc_objects.extend([{'svc_typ': type,
                            'svc_name': name, 
                            'svc_color': color,
                            'svc_uid': name,  # ?
                            'svc_comment': None, # ?
                            'ip_proto': proto,
                            'svc_port': None, 
                            'svc_port_end': None,
                            'svc_member_refs': member_names, # ?
                            'svc_member_names': member_names,
                            'svc_timeout': session_timeout,
                            'rpc_nr': None # ?
                            }])
    else:
        range_names = ''
        ports, port_ends = extractPorts(port_ranges)
        split = (len(ports) > 1)
        for index, port in enumerate(ports):
            port_end = port_ends[index]
            full_name = name
            if split:
                full_name += '_' + str(port)
                range_names += full_name + list_delimiter
            svc_objects.extend([{'svc_typ': type,
                                'svc_name': full_name, 
                                'svc_color': color,
                                'svc_uid': full_name,  # ?
                                'svc_comment': None, # ?
                                'ip_proto': proto,
                                'svc_port': port, 
                                'svc_port_end': port_end,
                                'svc_member_refs': member_names, # ?
                                'svc_member_names': member_names,
                                'svc_timeout': session_timeout,
                                'rpc_nr': None # ?
                                }])
        if split:
            range_names = range_names[:-1]
            svc_objects.extend([{'svc_typ': 'group',
                                'svc_name': name, 
                                'svc_color': color,
                                'svc_uid': name,  # ?
                                'svc_comment': None, # ?
                                'ip_proto': proto,
                                'svc_port': None, 
                                'svc_port_end': None,
                                'svc_member_refs': range_names, # ?
                                'svc_member_names': range_names,
                                'svc_timeout': session_timeout,
                                'rpc_nr': None # ?
                                }])


import re
from fwo_const import list_delimiter

def normalize_svcobjects(full_config, config2import, import_id, scope):
    svc_objects = []
    for s in scope:
        for obj_orig in full_config[s]:
            member_names = ''
            if 'member' in obj_orig:
                type = 'group'
                for member in obj_orig['member']:
                    member_names += member + list_delimiter
                member_names = member_names[:-1]
            else:
                type = 'simple'

            name = None
            if 'name' in obj_orig:
                name = str(obj_orig['name'])

            color = None
            if 'color' in obj_orig and str(obj_orig['color']) != 0:
                color = str(obj_orig['color'])

            session_timeout = None   # todo: find the right timer
    #        if 'udp-idle-timer' in obj_orig and str(obj_orig['udp-idle-timer']) != 0:
    #            session_timeout = str(obj_orig['udp-idle-timer'])

            proto = 0
            range_names = ''
            if 'protocol' in obj_orig:
                added_svc_obj = 0
                if obj_orig['protocol'] == 1:
                    addObject(svc_objects, type, name, color, 1, None, None, session_timeout, import_id)
                    added_svc_obj += 1
                elif obj_orig['protocol'] == 2:
                    if 'protocol-number' in obj_orig:
                        proto = obj_orig['protocol-number']
                    addObject(svc_objects, type, name, color, proto, None, None, session_timeout, import_id)
                    added_svc_obj += 1
                elif  obj_orig['protocol'] == 5 or obj_orig['protocol'] == 11:
                    split = check_split(obj_orig)
                    if "tcp-portrange" in obj_orig and len(obj_orig['tcp-portrange']) > 0:
                        tcpname = name
                        if split:
                            tcpname += "_tcp"
                            range_names += tcpname + list_delimiter
                        addObject(svc_objects, type, tcpname, color, 6, obj_orig['tcp-portrange'], None, session_timeout, import_id)
                        added_svc_obj += 1
                    if "udp-portrange" in obj_orig and len(obj_orig['udp-portrange']) > 0:
                        udpname = name
                        if split:
                            udpname += "_udp"
                            range_names += udpname + list_delimiter
                        addObject(svc_objects, type, udpname, color, 17, obj_orig['udp-portrange'], None, session_timeout, import_id)
                        added_svc_obj += 1
                    if "sctp-portrange" in obj_orig and len(obj_orig['sctp-portrange']) > 0:
                        sctpname = name
                        if split:
                            sctpname += "_sctp"
                            range_names += sctpname + list_delimiter
                        addObject(svc_objects, type, sctpname, color, 132, obj_orig['sctp-portrange'], None, session_timeout, import_id)
                        added_svc_obj += 1
                    if split:
                        range_names = range_names[:-1]
                        addObject(svc_objects, 'group', name, color, 0, None, range_names, session_timeout, import_id)
                        added_svc_obj += 1
                    if added_svc_obj==0: # assuming RPC service which here has no properties at all
                        addObject(svc_objects, 'rpc', name, color, 0, None, None, None, import_id)
                        added_svc_obj += 1
                elif  obj_orig['protocol'] == 6:
                    addObject(svc_objects, type, name, color, 58, None, None, session_timeout, import_id)
            elif type == 'group':
                addObject(svc_objects, type, name, color, 0, None, member_names, session_timeout, import_id)
            else:
                addObject(svc_objects, type, name, color, 0, None, None, session_timeout, import_id)

    # finally add "Original" service object for natting
    original_obj_name = 'Original'
    svc_objects.append(create_svc_object(import_id=import_id, name=original_obj_name, proto=0, port=None,\
        comment='"original" service object created by FWO importer for NAT purposes'))

    config2import.update({'service_objects': svc_objects})


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

            # split ranges
            pattern = re.compile('^(\d+)\-(\d+)$')
            match = pattern.match(port)
            if match:
                port, port_end = match.group().split('-')
            ports.append(port)
            port_ends.append(port_end)
    return ports, port_ends



def create_svc_object(import_id, name, proto, port, comment):
    return {
        'control_id': import_id,
        'svc_name': name,
        'svc_typ': 'simple',
        'svc_port': port,
        'ip_proto': proto,
        'svc_uid': name,    # services have no uid in fortimanager
        'svc_comment': comment
    }



def addObject(svc_objects, type, name, color, proto, port_ranges, member_names, session_timeout, import_id):
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
                            'rpc_nr': None, # ?
                            'control_id': import_id
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
                                'rpc_nr': None, # ?
                                'control_id': import_id
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
                                'rpc_nr': None, # ?
                                'control_id': import_id
                                }])


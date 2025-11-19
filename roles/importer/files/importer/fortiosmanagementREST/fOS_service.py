import re
from typing import Any
from fwo_const import list_delimiter
from fwo_log import get_fwo_logger


def normalize_svcobjects(full_config: dict[str, Any], config2import: dict[str, Any], import_id: int, scope: list[str]):
    logger = get_fwo_logger()
    svc_objects: list[dict[str, Any]] = []
    full_config['svc_obj_lookup_dict'] = {}
    for s in scope:
        if s in full_config:
            for obj_orig in full_config[s]:
                member_names = ''
                if 'member' in obj_orig:
                    type = 'group'
                    for member in obj_orig['member']:
                        member_names += member['name'] + list_delimiter
                    member_names = member_names[:-1]
                else:
                    type = 'simple'

                name = None 
                if 'name' in obj_orig:
                    name = str(obj_orig['name']) #TYPING: Can this be None???

                ## Added by TYPING!
                if name is None:
                    raise ValueError("Service object name cannot be None")
                
                color: str | None = None
                if 'color' in obj_orig and str(obj_orig['color']) != "0":
                    color = str(obj_orig['color'])

                if color is None:
                    raise ValueError("Service object color cannot be None")

                session_timeout = None   # todo: find the right timer
        #        if 'udp-idle-timer' in obj_orig and str(obj_orig['udp-idle-timer']) != 0:
        #            session_timeout = str(obj_orig['udp-idle-timer'])

                range_names = ''
                if 'protocol' in obj_orig:
                    added_svc_obj = 0
                    # if obj_orig['protocol'] == 1:
                    #     addObject(svc_objects, type, name, color, 1, None, None, session_timeout, import_id, full_config=full_config)
                    #     added_svc_obj += 1
                    # if obj_orig['protocol'] == 2:
                    #     if 'protocol-number' in obj_orig:
                    #         proto = obj_orig['protocol-number']
                    #     addObject(svc_objects, type, name, color, proto, None, None, session_timeout, import_id)
                    #     added_svc_obj += 1
                    # if  obj_orig['protocol'] == 5 or obj_orig['protocol'] == 11 or obj_orig['protocol'] == 'TCP/UDP/SCTP':
                    if  obj_orig['protocol'] == 'TCP/UDP/SCTP':
                        split = check_split(obj_orig)
                        if "tcp-portrange" in obj_orig and len(obj_orig['tcp-portrange']) > 0:
                            tcpname = name
                            if split:
                                tcpname += "_tcp"
                                range_names += tcpname + list_delimiter
                            add_object(svc_objects, type, tcpname, color, 6, obj_orig['tcp-portrange'], None, session_timeout, import_id, full_config=full_config)
                            added_svc_obj += 1
                        if "udp-portrange" in obj_orig and len(obj_orig['udp-portrange']) > 0:
                            udpname = name
                            if split:
                                udpname += "_udp"
                                range_names += udpname + list_delimiter
                            add_object(svc_objects, type, udpname, color, 17, obj_orig['udp-portrange'], None, session_timeout, import_id, full_config=full_config)
                            added_svc_obj += 1
                        if "sctp-portrange" in obj_orig and len(obj_orig['sctp-portrange']) > 0:
                            sctpname = name
                            if split:
                                sctpname += "_sctp"
                                range_names += sctpname + list_delimiter
                            add_object(svc_objects, type, sctpname, color, 132, obj_orig['sctp-portrange'], None, session_timeout, import_id, full_config=full_config)
                            added_svc_obj += 1
                        if split:
                            range_names = range_names[:-1]
                            # TODO: collect group members
                            add_object(svc_objects, 'group', name, color, 0, None, range_names, session_timeout, import_id, full_config=full_config)
                            added_svc_obj += 1
                        if added_svc_obj==0: # assuming RPC service which here has no properties at all
                            add_object(svc_objects, 'rpc', name, color, 0, None, None, None, import_id, full_config=full_config)
                            added_svc_obj += 1
                    elif obj_orig['protocol'] == 'IP':
                        add_object(svc_objects, 'simple', name, color, obj_orig['protocol-number'], None, None, None, import_id, full_config=full_config)
                        added_svc_obj += 1
                    elif obj_orig['protocol'] == 'ICMP':
                        add_object(svc_objects, 'simple', name, color, 1, None, None, None, import_id, full_config=full_config)
                        added_svc_obj += 1
                    elif obj_orig['protocol'] == 'ICMP6':
                        add_object(svc_objects, 'simple', name, color, 1, None, None, None, import_id, full_config=full_config)
                        added_svc_obj += 1
                    else:
                        logger.warning("Unknown service protocol found: " + obj_orig['name'] +', proto: ' + obj_orig['protocol'])
                elif type == 'group':
                    add_object(svc_objects, type, name, color, 0, None, member_names, session_timeout, import_id, full_config=full_config)
                else:
                    # application/list
                    add_object(svc_objects, type, name, color, 0, None, None, session_timeout, import_id, full_config=full_config)

    # finally add "Original" service object for natting
    original_obj_name = 'Original'
    svc_objects.append(create_svc_object(import_id=import_id, name=original_obj_name, proto=0, port=None,\
        comment='"original" service object created by FWO importer for NAT purposes'))

    config2import.update({'service_objects': svc_objects})


def check_split(obj_orig: dict[str, Any]) -> bool:
    count = 0
    if "tcp-portrange" in obj_orig and len(obj_orig['tcp-portrange']) > 0:
        count += 1
    if "udp-portrange" in obj_orig and len(obj_orig['udp-portrange']) > 0:
        count += 1
    if "sctp-portrange" in obj_orig and len(obj_orig['sctp-portrange']) > 0:
        count += 1
    return (count > 1)


def extract_single_port_range(port_range: str) -> tuple[str, str]:
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
    return port, port_end


def extract_ports(port_ranges: str | None) -> tuple[list[str], list[str]]:
    ports = []
    port_ends = []
    if port_ranges is not None and len(port_ranges) > 0: # TYPING: ?????? 
        if ' ' in port_ranges:
            # port range of the form "12 13 114"
            port_ranges = port_ranges.split(' ') #type: ignore #TYPING: can be str or list ???
        
        if not isinstance(port_ranges, str):
            for port_range in port_ranges: #type: ignore #TYPING: can be str or list ???
                port1, port2 = extract_single_port_range(port_range) #type: ignore #TYPING: can be str or list ???
                ports.append(port1) #type: ignore #TYPING: can be str or list ???
                port_ends.append(port2) #type: ignore #TYPING: can be str or list ???
        else:
            port1, port2 = extract_single_port_range(port_ranges)
            ports.append(port1) #type: ignore #TYPING: can be str or list ???
            port_ends.append(port2) #type: ignore #TYPING: can be str or list ???
    return ports, port_ends #type: ignore #TYPING: can be str or list ???


def create_svc_object(import_id: int, name: str, proto: int, port: str | None, comment: str | None=None) -> dict[str, Any]:
    return {
        'control_id': import_id,
        'svc_name': name,
        'svc_typ': 'simple',
        'svc_port': port,
        'ip_proto': proto,
        'svc_uid': name,    # services have no uid in fortimanager
        'svc_comment': comment
    }


def add_object(svc_objects: list[dict[str, Any]], type: str, name: str, color: str, proto: int, port_ranges: str | None, member_names: str | None, session_timeout: int | None, import_id: int, full_config: dict[str, Any]={}):

    # add service object in lookup table (currently no UID, name is the UID)
    full_config['svc_obj_lookup_dict'][name] = name

    svc_obj = create_svc_object(import_id, name, proto, None, None)
    svc_obj['svc_color'] = color
    svc_obj['svc_typ'] = type
    svc_obj['svc_port_end'] = None
    svc_obj['svc_member_names'] = member_names
    svc_obj['svc_member_refs'] = member_names
    svc_obj['svc_timeout'] = session_timeout

    if port_ranges is not None:
        range_names = ''
        ports, port_ends = extract_ports(port_ranges)
        split = (len(ports) > 1)
        for index, port in enumerate(ports):
            port_end = port_ends[index]
            full_name = name
            if split:
                full_name += '_' + str(port)
                range_names += full_name + list_delimiter
                if port_end != port:
                    port_range_local = port + '-' + port_end
                else:
                    port_range_local = port
                add_object(svc_objects, 'simple', full_name, color, proto, port_range_local, None, None, import_id, full_config)

            svc_obj['svc_port'] = port
            svc_obj['svc_port_end'] = port_end

        if split:
            range_names = range_names[:-1]
            svc_obj['svc_member_refs'] = range_names
            svc_obj['svc_member_names'] = range_names
            svc_obj['svc_typ'] = 'group'
            svc_obj['svc_port'] = None
            svc_obj['svc_port_end'] = None

    svc_objects.extend([svc_obj])


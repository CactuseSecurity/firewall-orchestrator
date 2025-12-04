import re
from typing import Any
from fw_modules.checkpointR8x import cp_const
from fwo_const import LIST_DELIMITER
from fwo_exceptions import FwoImporterErrorInconsistencies


# collect_svcobjects writes svc info into global users dict
def collect_svc_objects(object_table: dict[str, Any], svc_objects: list[dict[str, Any]]):
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


def _set_default_values(obj: dict[str, Any]):
    """
    Set default values for color, comments, and domain_uid.
    """
    if 'color' not in obj or obj['color'] == '' or obj['color'] == 'none':
        obj['color'] = 'black'
    
    if 'comments' not in obj or obj['comments'] == '':
        obj['comments'] = None
    
    obj['domain_uid'] = get_obj_domain_uid(obj)


def _get_rpc_number(obj: dict[str, Any]) -> str | None:
    """
    Extract RPC number from interface-uuid or program-number.
    Returns RPC number or None.
    """
    if 'interface-uuid' in obj:
        return obj['interface-uuid']
    if 'program-number' in obj:
        return obj['program-number']
    return None


def _get_session_timeout(obj: dict[str, Any]) -> str | None:
    """
    Extract and stringify session timeout.
    Returns session timeout as string or None.
    """
    if 'session-timeout' in obj:
        return str(obj['session-timeout'])
    return None


def _get_member_references(obj: dict[str, Any]) -> str | None:
    """
    Process members list and return concatenated member references.
    Returns member reference string or None.
    """
    if 'members' not in obj:
        return None
    
    member_refs = ''
    for member in obj['members']:
        if isinstance(member, str):
            member_refs += member + LIST_DELIMITER
        elif isinstance(member, dict) and 'uid' in member and isinstance(member['uid'], str):
            member_refs += member['uid'] + LIST_DELIMITER
    return member_refs[:-1] if member_refs else None


def _get_protocol_number(obj: dict[str, Any]) -> int | None:
    """
    Extract and validate protocol number from object.
    Returns validated protocol number or None.
    """
    proto_map = {
        'service-tcp': 6,
        'service-udp': 17,
        'service-icmp': 1
    }
    
    proto = None
    if 'type' in obj and obj['type'] in proto_map:
        proto = proto_map[obj['type']]
    elif 'ip-protocol' in obj:
        proto = obj['ip-protocol']
    
    return proto if proto is None or proto >= 0 else None


def collect_single_svc_object(obj: dict[str, Any]) -> None:
    """
    Collects a single service object and appends its details to the svc_objects list.
    Handles different types of service objects and normalizes port information.
    """
    obj['proto'] = _get_protocol_number(obj)
    
    obj['svc_member_refs'] = _get_member_references(obj)
    # svc_member_names are added later in add_member_names_for_svc_group()

    obj['session_timeout'] = _get_session_timeout(obj)
    obj['rpc_nr'] = _get_rpc_number(obj)

    obj['port'], obj['port_end'] = normalize_port(obj)
    _set_default_values(obj)
 

def normalize_port(obj: dict[str, Any]) -> tuple[str|None, str|None]:
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
            match_result_list = match.group().split('-')
            return match_result_list[0], match_result_list[1]

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


def get_obj_domain_uid(obj: dict[str, Any]) -> str:
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
def resolve_svc_uid_to_name(uid: str, svc_objects: list[dict[str, Any]]) -> str:
    for obj in svc_objects:
        if obj['svc_uid'] == uid:
            return obj['svc_name']
    raise FwoImporterErrorInconsistencies('Service object member uid ' + uid + ' not found')


def add_member_names_for_svc_group(idx: int, svc_objects: list[dict[str, Any]]) -> None:
    member_names = ''
    group = svc_objects.pop(idx)

    if 'svc_member_refs' in group and group['svc_member_refs'] is not None:
        svc_member_refs = group['svc_member_refs'].split(LIST_DELIMITER)
        for ref in svc_member_refs:
            member_name = resolve_svc_uid_to_name(ref, svc_objects)
            member_names += member_name + LIST_DELIMITER
        group['svc_member_names'] = member_names[:-1]

    svc_objects.insert(idx, group)


def normalize_service_objects(full_config: dict[str, Any], config2import: dict[str, Any], import_id: int) -> None:
    svc_objects: list[dict[str, Any]] = []
    for obj_dict in full_config['objects']:
            collect_svc_objects(obj_dict, svc_objects)
    for obj in svc_objects:
        obj.update({'control_id': import_id})
    for idx in range(0, len(svc_objects)-1):
        if svc_objects[idx]['svc_typ'] == 'group':
            add_member_names_for_svc_group(idx, svc_objects)
    config2import.update({'service_objects': svc_objects})

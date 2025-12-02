from typing import Any
from fwo_log import FWOLogger
import json
from fw_modules.checkpointR8x import cp_const
from fwo_const import LIST_DELIMITER
import fwo_api_call as fwo_api_call
import ipaddress 
import fwo_globals
import fwo_const
from fwo_base import cidr_to_range
from services.service_provider import ServiceProvider
from fwo_api_call import FwoApiCall, FwoApi


def normalize_network_objects(full_config: dict[str, Any], config2import: dict[str, Any], import_id: int, mgm_id: int=0):
    nw_objects: list[dict[str, Any]] = []
    global_domain = initialize_global_domain(full_config['objects'])
    
    for obj_dict in full_config['objects']:
        collect_nw_objects(obj_dict, nw_objects, global_domain, mgm_id=mgm_id)
        
        for nw_obj in nw_objects:
            nw_obj.update({'control_id': import_id})
            if nw_obj['obj_typ'] == 'interoperable-device':
                nw_obj.update({'obj_typ': 'external-gateway'})
            if nw_obj['obj_typ'] == 'CpmiVoipSipDomain':
                FWOLogger.info("found VOIP object - tranforming to empty group")
                nw_obj.update({'obj_typ': 'group'})
            set_dummy_ip_for_object_without_ip(nw_obj)

    for idx in range(0, len(nw_objects)-1):
        if nw_objects[idx]['obj_typ'] == 'group':
            add_member_names_for_nw_group(idx, nw_objects)

    config2import.update({'network_objects': nw_objects})

def set_dummy_ip_for_object_without_ip(nw_obj: dict[str, Any]) -> None:
    if nw_obj['obj_typ']!='group' and (nw_obj['obj_ip'] is None or nw_obj['obj_ip'] == ''):
        FWOLogger.warning("found object without IP :" + nw_obj['obj_name'] + " (type=" + nw_obj['obj_typ'] + ") - setting dummy IP")
        nw_obj.update({'obj_ip': fwo_const.DUMMY_IP})
        nw_obj.update({'obj_ip_end': fwo_const.DUMMY_IP})

def initialize_global_domain(objects : list[dict[str, Any]]) -> dict[str, Any]:
    """Returns CP Global Domain for MDS and standalone domain otherwise
    """

    if  len(objects) == 0:
        FWOLogger.warning("No objects found in full config, cannot initialize global domain")
        return {}
    
    if 'domain_uid' not in objects[0] or 'domain_name' not in objects[0]:
        FWOLogger.debug("No domain information found in objects, this seems to be a standalone management")
        return {}
    
    global_domain = {'domain': {
        'uid': objects[0]['domain_uid'],
        'name': objects[0]['domain_name']}}

    return global_domain


def collect_nw_objects(object_table: dict[str, Any], nw_objects: list[dict[str, Any]], global_domain: dict[str, Any], mgm_id: int=0) -> None:
    """Collect nw_objects from object tables and write them into global nw_objects dict
    """

    if object_table['type'] not in cp_const.nw_obj_table_names:
        return
    for chunk in object_table['chunks']:
        if 'objects' in chunk:
            for obj in chunk['objects']:

                if is_obj_already_collected(nw_objects, obj):
                    continue
                member_refs, member_names = handle_members(obj)  
                ip_addr = get_ip_of_obj(obj, mgm_id=mgm_id)
                obj_type, first_ip, last_ip = handle_object_type_and_ip(obj, ip_addr)  
                comments = get_comment_and_color_of_obj(obj)

                nw_objects.append({'obj_uid': obj['uid'], 'obj_name': obj['name'], 'obj_color': obj['color'],
                                    'obj_comment': comments, 'obj_domain': get_domain_uid(obj, global_domain),
                                    'obj_typ': obj_type, 'obj_ip': first_ip, 'obj_ip_end': last_ip,
                                    'obj_member_refs': member_refs, 'obj_member_names': member_names})


def get_domain_uid(obj: dict[str, Any], global_domain: dict[str, Any]) -> str | dict[str, Any] | None:
    """Returns the domain UID for the given object.
    If the object has a 'domain' key with a 'uid', it returns that UID.
    Otherwise, it returns the global domain UID.
    """
    if 'domain' not in obj or 'uid' not in obj['domain']:
        return obj.update({'domain': global_domain}) #TODO: check if the None value is wanted
    else:
        return obj['domain']['uid']
    
        
def is_obj_already_collected(nw_objects: list[dict[str, Any]], obj: dict[str, Any]) -> bool:
    if 'uid' not in obj:
        FWOLogger.warning("found nw_object without uid: " + str(obj))
        return False

    if 'domain' in obj:
        for already_collected_obj in nw_objects:
            if obj['uid'] == already_collected_obj['obj_uid'] and obj['domain']['uid'] == already_collected_obj['obj_domain']:
                return True
    else:
        FWOLogger.warning("found nw_object without domain: " + obj['uid'])

    return False


def handle_members(obj: dict[str, Any]) -> tuple[str | None, str | None]:
    """Gets group member uids, currently no member_names
    """
    member_refs = None
    member_names = None
    if 'members' in obj:
        member_refs = ''
        member_names = ''
        for member in obj['members']:
            member_refs += member + LIST_DELIMITER
        member_refs = member_refs[:-1]
        if obj['members'] == '':
            obj['members'] = None
    return member_refs, member_names

def handle_object_type_and_ip(obj: dict[str, Any], ip_addr: str | None) -> tuple[str, str | None, str | None]:
    obj_type = 'undef'
    ipArray = cidr_to_range(ip_addr)
    first_ip = None
    last_ip = None
    if len(ipArray)==2:
        first_ip = ipArray[0]
        last_ip  = ipArray[1]
    elif len(ipArray)==1:
        first_ip = ipArray[0]
        last_ip  = None

    if 'type' in obj:
        obj_type = obj['type']

    if obj_type == 'updatable-object':
        first_ip = '0.0.0.0/32'
        last_ip = '255.255.255.255/32'
        obj_type = 'dynamic_net_obj'
        
    if obj_type in ['group-with-exclusion', 'security-zone', 'dynamic-object']:
        obj_type = 'group'
        # TODO: handle exclusion groups correctly

    if obj_type == 'dns-domain':
        obj_type = 'domain'
        first_ip = '0.0.0.0/32'
        last_ip = '255.255.255.255/32'

    if obj_type == 'security-zone':
        first_ip = '0.0.0.0/32'
        last_ip = '255.255.255.255/32'
        obj_type = 'network'

    if obj_type in ['address-range', 'multicast-address-range']:
        obj_type = 'ip_range'
        if '-' in str(ip_addr):
            first_ip, last_ip = str(ip_addr).split('-')
        else:
            FWOLogger.warning("parse_network::collect_nw_objects - found range object '" +
                        obj['name'] + "' without hyphen: " + ip_addr)
    elif obj_type in cp_const.cp_specific_object_types:
        FWOLogger.debug(f"rewriting non-standard cp-host-type '{obj['name']}' with object type '{obj_type}' to host", 6)
        FWOLogger.debug("obj_dump:" + json.dumps(obj, indent=3), 6)
        obj_type = 'host'

    return obj_type, first_ip, last_ip

def get_comment_and_color_of_obj(obj: dict[str, Any]) -> str | None:
    """Returns comment and sets missing color to black
    """
    if 'comments' not in obj or obj['comments'] == '':
        comments = None
    else:
        comments = obj['comments']
    if 'color' not in obj or obj['color'] == '' or obj['color'] == 'none':
        obj['color'] = 'black'
    return comments

# for members of groups, the name of the member obj needs to be fetched separately (starting from API v1.?)
def resolve_nw_uid_to_name(uid: str, nw_objects: list[dict[str, Any]]) -> str:
    # return name of nw_objects element where obj_uid = uid
    for obj in nw_objects:
        if obj['obj_uid'] == uid:
            return obj['obj_name']
    return 'ERROR: uid "' + uid + '" not found'


def add_member_names_for_nw_group(idx: int, nw_objects: list[dict[str, Any]]) -> None:
    group = nw_objects.pop(idx)
    if group['obj_member_refs'] == '' or group['obj_member_refs'] is None:
        #member_names = None
        #obj_member_refs = None
        group['obj_member_names'] = None
        group['obj_member_refs'] = None
    else:
        member_names = ''
        obj_member_refs = group['obj_member_refs'].split(LIST_DELIMITER)
        for ref in obj_member_refs:
            member_name = resolve_nw_uid_to_name(ref, nw_objects)
            member_names += member_name + LIST_DELIMITER
        group['obj_member_names'] = member_names[:-1]
    nw_objects.insert(idx, group)


def validate_ip_address(address: str) -> bool:
    try:
        # ipaddress.ip_address(address)
        ipaddress.ip_network(address)
        return True
        # print("IP address {} is valid. The object returned is {}".format(address, ip))
    except ValueError:
        return False
        # print("IP address {} is not valid".format(address)) 


def get_ip_of_obj(obj: dict[str, Any], mgm_id: int | None = None) -> str | None:
    if 'ipv4-address' in obj:
        ip_addr = obj['ipv4-address']
    elif 'ipv6-address' in obj:
        ip_addr = obj['ipv6-address']
    elif 'subnet4' in obj:
        ip_addr = obj['subnet4'] + '/' + str(obj['mask-length4'])
    elif 'subnet6' in obj:
        ip_addr = obj['subnet6'] + '/' + str(obj['mask-length6'])
    elif 'ipv4-address-first' in obj and 'ipv4-address-last' in obj:
        ip_addr = obj['ipv4-address-first'] + '-' + str(obj['ipv4-address-last'])
    elif 'ipv6-address-first' in obj and 'ipv6-address-last' in obj:
        ip_addr = obj['ipv6-address-first'] + '-' + str(obj['ipv6-address-last'])
    else:
        ip_addr = None

    ## fix malformed ip addresses (should not regularly occur and constitutes a data issue in CP database)
    if ip_addr is None or ('type' in obj and (obj['type'] == 'address-range' or obj['type'] == 'multicast-address-range')):
        pass   # ignore None and ranges here
    elif not validate_ip_address(ip_addr):
        alert_description = "object is not a valid ip address (" + str(ip_addr) + ")"
        service_provider = ServiceProvider()
        global_state = service_provider.get_global_state()
        api_call = FwoApiCall(FwoApi(api_uri=global_state.import_state.fwo_config.fwo_api_url, jwt=global_state.import_state.Jwt))
        api_call.create_data_issue(severity=2, obj_name=obj['name'], object_type=obj['type'], description=alert_description, mgm_id=mgm_id) 
        alert_description = "object '" + obj['name'] + "' (type=" + obj['type'] + ") is not a valid ip address (" + str(ip_addr) + ")"
        api_call.set_alert(title="import error", severity=2, description=alert_description, source='import', alert_code=17, mgm_id=mgm_id)
        ip_addr = fwo_const.DUMMY_IP  # setting syntactically correct dummy ip
    return ip_addr


def make_host(ip_in: str) -> str | None:
    ip_obj: ipaddress.IPv4Address | ipaddress.IPv6Address = ipaddress.ip_address(ip_in)
    
    # If it's a valid address, append the appropriate CIDR notation
    if isinstance(ip_obj, ipaddress.IPv4Address):
        return f"{ip_in}/32"
    elif isinstance(ip_obj, ipaddress.IPv6Address): # TODO: check if just else is sufficient # type: ignore
        return f"{ip_in}/128"

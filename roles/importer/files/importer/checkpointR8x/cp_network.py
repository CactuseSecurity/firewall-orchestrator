from fwo_log import getFwoLogger
import json
import cp_const
from fwo_const import list_delimiter
import fwo_alert, fwo_api
import ipaddress 


def normalize_network_objects(full_config, config2import, import_id, mgm_id=0, debug_level=0):
    nw_objects = []
    logger = getFwoLogger()

    for obj_table in full_config['object_tables']:
        collect_nw_objects(obj_table, nw_objects,
                           debug_level=debug_level, mgm_id=mgm_id)
    for nw_obj in nw_objects:
        nw_obj.update({'control_id': import_id})
        if nw_obj['obj_typ'] == 'interoperable-device':
            nw_obj.update({'obj_typ': 'external-gateway'})
        # set a dummy IP address for objects without IP addreses
        if nw_obj['obj_typ']!='group' and (nw_obj['obj_ip'] is None or nw_obj['obj_ip'] == ''):
            logger.warning("found object without IP :" + nw_obj['obj_name'] + " (type=" + nw_obj['obj_typ'] + ") - setting dummy IP")
            nw_obj.update({'obj_ip': '0.0.0.0/32'})

    for idx in range(0, len(nw_objects)-1):
        if nw_objects[idx]['obj_typ'] == 'group':
            add_member_names_for_nw_group(idx, nw_objects)

    config2import.update({'network_objects': nw_objects})


# collect_nw_objects from object tables and write them into global nw_objects dict
def collect_nw_objects(object_table, nw_objects, debug_level=0, mgm_id=0):
    logger = getFwoLogger()

    if object_table['object_type'] in cp_const.nw_obj_table_names:
        for chunk in object_table['object_chunks']:
            if 'objects' in chunk:
                for obj in chunk['objects']:
                    ip_addr = ''
                    member_refs = None
                    member_names = None
                    if 'members' in obj:
                        member_refs = ''
                        member_names = ''
                        for member in obj['members']:
                            member_refs += member + list_delimiter
                        member_refs = member_refs[:-1]
                        if obj['members'] == '':
                            obj['members'] = None
                        
                    ip_addr = get_ip_of_obj(obj, mgm_id=mgm_id)
                    first_ip = ip_addr
                    last_ip = None
                    obj_type = 'undef'
                    if 'type' in obj:
                        obj_type = obj['type']
                    elif 'uid-in-updatable-objects-repository' in obj:
                        obj_type = 'group'
                        obj['name'] = obj['name-in-updatable-objects-repository']
                        obj['uid'] = obj['uid-in-updatable-objects-repository']
                        obj['color'] = 'black'
                    if obj_type == 'dns-domain':
                        first_ip = None
                        last_ip = None
                        obj_type = 'group'

                    if obj_type == 'group-with-exclusion':
                        first_ip = None
                        last_ip = None
                        obj_type = 'group'
                        # TODO: handle exclusion groups correctly

                    if obj_type == 'security-zone':
                        first_ip = '0.0.0.0/32'
                        last_ip = '255.255.255.255/32'
                        obj_type = 'network'

                    if obj_type == 'group':
                        first_ip = None
                        last_ip = None

                    if obj_type == 'address-range' or obj_type == 'multicast-address-range':
                        obj_type = 'ip_range'
                        if debug_level > 5:
                            logger.debug(
                                "parse_network::collect_nw_objects - found range object '" + obj['name'] + "' with ip: " + ip_addr)
                        if '-' in str(ip_addr):
                            first_ip, last_ip = str(ip_addr).split('-')
                        else:
                            logger.warning("parse_network::collect_nw_objects - found range object '" +
                                        obj['name'] + "' without hyphen: " + ip_addr)
                    elif obj_type in cp_const.cp_specific_object_types:
                        if debug_level > 5:
                            logger.debug("parse_network::collect_nw_objects - rewriting non-standard cp-host-type '" +
                                        obj['name'] + "' with object type '" + obj_type + "' to host")
                            logger.debug("obj_dump:" + json.dumps(obj, indent=3))
                        obj_type = 'host'
                    # adding the object:
                    if not 'comments' in obj or obj['comments'] == '':
                        obj['comments'] = None
                    nw_objects.extend([{'obj_uid': obj['uid'], 'obj_name': obj['name'], 'obj_color': obj['color'],
                                        'obj_comment': obj['comments'],
                                        'obj_typ': obj_type, 'obj_ip': first_ip, 'obj_ip_end': last_ip,
                                        'obj_member_refs': member_refs, 'obj_member_names': member_names}])


# for members of groups, the name of the member obj needs to be fetched separately (starting from API v1.?)
def resolve_nw_uid_to_name(uid, nw_objects):
    # return name of nw_objects element where obj_uid = uid
    for obj in nw_objects:
        if obj['obj_uid'] == uid:
            return obj['obj_name']
    return 'ERROR: uid "' + uid + '" not found'


def add_member_names_for_nw_group(idx, nw_objects):
    group = nw_objects.pop(idx)
    if group['obj_member_refs'] == '' or group['obj_member_refs'] == None:
        #member_names = None
        #obj_member_refs = None
        group['obj_member_names'] = None
        group['obj_member_refs'] = None
    else:
        member_names = ''
        obj_member_refs = group['obj_member_refs'].split(list_delimiter)
        for ref in obj_member_refs:
            member_name = resolve_nw_uid_to_name(ref, nw_objects)
            member_names += member_name + list_delimiter
        group['obj_member_names'] = member_names[:-1]
    nw_objects.insert(idx, group)


def validate_ip_address(address):
    try:
        # ipaddress.ip_address(address)
        ipaddress.ip_network(address)
        return True
        # print("IP address {} is valid. The object returned is {}".format(address, ip))
    except ValueError:
        return False
        # print("IP address {} is not valid".format(address)) 


def get_ip_of_obj(obj, mgm_id=None):
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
        alerter = fwo_alert.getFwoAlerter()
        alert_description = "object is not a valid ip address (" + str(ip_addr) + ")"
        fwo_api.create_data_issue(alerter['fwo_api_base_url'], alerter['jwt'], severity=2, obj_name=obj['name'], object_type=obj['type'], description=alert_description, mgm_id=mgm_id) 
        alert_description = "object '" + obj['name'] + "' (type=" + obj['type'] + ") is not a valid ip address (" + str(ip_addr) + ")"
        fwo_api.setAlert(alerter['fwo_api_base_url'], alerter['jwt'], title="import error", severity=2, role='importer', \
            description=alert_description, source='import', alertCode=17, mgm_id=mgm_id)
        ip_addr = '0.0.0.0/32'  # setting syntactically correct dummy ip
    return ip_addr

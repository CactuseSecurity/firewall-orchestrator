from fwo_log import getFwoLogger
import json
import cp_const
from fwo_const import list_delimiter
import fwo_alert, fwo_api
import ipaddress 


def normalize_network_objects(full_config, config2import, import_id, mgm_id=0):
    nw_objects = []
    logger = getFwoLogger()

    for obj_table in full_config['object_tables']:
        collect_nw_objects(obj_table, nw_objects, mgm_id=mgm_id)
    for nw_obj in nw_objects:
        nw_obj.update({'control_id': import_id})
        if nw_obj['obj_typ'] == 'interoperable-device':
            nw_obj.update({'obj_typ': 'external-gateway'})
        if nw_obj['obj_typ'] == 'CpmiVoipSipDomain':
            logger.info("found VOIP object - tranforming to empty group")
            nw_obj.update({'obj_typ': 'group'})
        # set a dummy IP address for objects without IP addreses
        if nw_obj['obj_typ']!='group' and (nw_obj['obj_ip'] is None or nw_obj['obj_ip'] == ''):
            logger.warning("found object without IP :" + nw_obj['obj_name'] + " (type=" + nw_obj['obj_typ'] + ") - setting dummy IP")
            nw_obj.update({'obj_ip': cp_const.dummy_ip})

    for idx in range(0, len(nw_objects)-1):
        if nw_objects[idx]['obj_typ'] == 'group':
            add_member_names_for_nw_group(idx, nw_objects)

    config2import.update({'network_objects': nw_objects})


# collect_nw_objects from object tables and write them into global nw_objects dict
def collect_nw_objects(object_table, nw_objects, mgm_id=0):
    logger = getFwoLogger()

    if object_table['object_type'] not in cp_const.nw_obj_table_names:
        return
    
    for chunk in object_table['object_chunks']:
        if 'objects' not in chunk:
            break
        for obj in chunk['objects']:
            if 'comments' not in obj or obj['comments'] == '':
                obj['comments'] = None

            if 'uid' in obj and obj['uid']=='e9ba0c50-ddd7-4aa8-9df6-1c4045ba10bb':
                logger.debug(f"found SIP nw object with uid {obj['uid']} in object dictionary")

            member_refs, member_names = set_members(obj)                        

            first_ip, last_ip, obj_type = ip_and_type_handling(obj, mgm_id=mgm_id)

            obj_to_add = {
                'uid': obj['uid'],
                'name': obj['name'],
                'typ': obj_type,
                'color': obj.get('color', 'black'),
                'comments': obj.get('comments', None),
                'ip': first_ip,
                'ip_end': last_ip,
                'member_refs': member_refs,
                'member_names': member_names
            }

            update_or_add_nw_object(nw_objects, obj_to_add) 


def ip_and_type_handling(obj, mgm_id=0):
    logger = getFwoLogger()
    ip_addr = get_ip_of_obj(obj, mgm_id=mgm_id)
    ip_array = cidrToRange(ip_addr)
    
    if len(ip_array)==2:
        first_ip = ip_array[0]
        last_ip  = ip_array[1]
    elif len(ip_array)==1:
        first_ip = ip_array[0]
        last_ip  = None
    else:
        logger.warning(f"found strange ip: {ip_addr}")
    
    obj_type = 'undef'
    if 'type' in obj:
        obj_type = obj['type']
    if 'uid-in-updatable-objects-repository' in obj:
        obj_type = 'group'
        obj['name'] = obj['name-in-updatable-objects-repository']
        obj['uid'] = obj['uid-in-updatable-objects-repository']
        obj['color'] = 'black'
    if obj_type in ['updatable-object', 'access-role', 'group-with-exclusion', 'security-zone', 'dns-domain']:
        obj_type = 'group'

    if obj_type == 'group-with-exclusion':
        first_ip = None
        last_ip = None
        obj_type = 'group'

    if obj_type == 'security-zone':
        first_ip = cp_const.dummy_ip
        last_ip = '255.255.255.255/32'
        obj_type = 'network'

    if obj_type == 'group':
        first_ip = None
        last_ip = None

    if obj_type == 'address-range' or obj_type == 'multicast-address-range':
        obj_type = 'ip_range'
        if '-' in str(ip_addr):
            first_ip, last_ip = str(ip_addr).split('-')
        else:
            logger.warning("parse_network::collect_nw_objects - found range object '" +
                        obj['name'] + "' without hyphen: " + ip_addr)
    elif obj_type in cp_const.cp_specific_object_types:
        obj_type = 'host'
                    
    return first_ip, last_ip, obj_type


def set_members(obj):
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
    
    return member_refs, member_names


def update_or_add_nw_object(nw_objects, obj): # obj_uid, obj_name, obj_typ, obj_color, obj_comment, obj_ip, obj_ip_end=None, obj_member_refs=None, obj_member_names=None):
    """
    Update an existing network object in the nw_objects list or add it if it does not exist.
    """
    for existing_obj in nw_objects:
        if existing_obj['obj_uid'] == obj['uid']:
            if obj['typ'] == 'host' and obj['ip'] is not None and obj['ip'] != cp_const.dummy_ip:
                # Update existing gateway object, ignore all other caess of duplicate objects
                existing_obj.update({
                    'obj_uid': obj['uid'],
                    'obj_name': obj['name'],
                    'obj_color': obj['color'],
                    'obj_comment': obj['comments'],
                    'obj_typ': obj['typ'],
                    'obj_ip': obj['ip'],
                    'obj_ip_end': obj['ip_end'],
                    'obj_member_refs': obj['member_refs'],
                    'obj_member_names': obj['member_names']
                })
            return

    # If not found, append new object
    nw_objects.append({
        'obj_uid': obj['uid'],
        'obj_name': obj['name'],
        'obj_color': obj['color'],
        'obj_comment': obj['comments'],
        'obj_typ': obj['typ'],
        'obj_ip': obj['ip'],
        'obj_ip_end': obj['ip_end'],
        'obj_member_refs': obj['member_refs'],
        'obj_member_names': obj['member_names']
    })


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
        ip_addr = cp_const.dummy_ip  # set dummy ip address if the ip address is not valid
    return ip_addr


def make_host(ip_in):
    ip_obj = ipaddress.ip_address(ip_in)
    
    # If it's a valid address, append the appropriate CIDR notation
    if isinstance(ip_obj, ipaddress.IPv4Address):
        return f"{ip_in}/32"
    elif isinstance(ip_obj, ipaddress.IPv6Address):
        return f"{ip_in}/128"


def cidrToRange(ip):
    logger = getFwoLogger()

    if isinstance(ip, str):
        if ip.startswith('5002:abcd:1234:2800'):
            logger.debug("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! found test ip " + ip)

        # dealing with ranges:
        if '-' in ip:
            return '-'.split(ip)

        ipVersion = validIPAddress(ip)
        if ipVersion=='Invalid':
            logger.warning("error while decoding ip '" + ip + "'")
            return [ip]
        elif ipVersion=='IPv4':
            net = ipaddress.IPv4Network(ip)
        elif ipVersion=='IPv6':
            net = ipaddress.IPv6Network(ip)    
        return [make_host(str(net.network_address)), make_host(str(net.broadcast_address))]
            
    return [ip]


def validIPAddress(IP: str) -> str: 
    try: 
        t = type(ipaddress.ip_address(IP))
        if t is ipaddress.IPv4Address:
            return "IPv4"
        elif t is ipaddress.IPv6Address:
            return "IPv6"
        else:
            return 'Invalid'
    except:
        try:
            t = type(ipaddress.ip_network(IP))
            if t is ipaddress.IPv4Network:
                return "IPv4"
            elif t is ipaddress.IPv6Network:
                return "IPv6"
            else:
                return 'Invalid'        
        except:
            return "Invalid"

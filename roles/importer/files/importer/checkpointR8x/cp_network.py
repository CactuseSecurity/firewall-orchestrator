from fwo_log import getFwoLogger
import json
import cp_const
from fwo_const import list_delimiter
import fwo_alert, fwo_api
import ipaddress 
import traceback


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
        if nw_obj['obj_typ'] == 'CpmiVoipSipDomain':
            logger.info(f"found VOIP object - tranforming to empty group")
            nw_obj.update({'obj_typ': 'group'})
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

                    if 'uid' in obj and obj['uid']=='e9ba0c50-ddd7-4aa8-9df6-1c4045ba10bb':
                        logger.debug(f"found SIP nw object with uid {obj['uid']} in object dictionary")
                
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

                    ipArray = cidrToRange(ip_addr)
                    
#                    first_ip, last_ip = get_first_and_last_ip(ip_addr)

                    if len(ipArray)==2:
                        first_ip = ipArray[0]
                        last_ip  = ipArray[1]
                    elif len(ipArray)==1:
                        first_ip = ipArray[0]
                        last_ip  = None
                    else:
                        logger.warning("found strange ip: " + ip_addr)

                    obj_type = 'undef'
                    if 'type' in obj:
                        obj_type = obj['type']
                    if 'uid-in-updatable-objects-repository' in obj:
                        obj_type = 'group'
                        obj['name'] = obj['name-in-updatable-objects-repository']
                        if 'uid' in obj:
                            obj['uid'] = obj['uid']
                        else:
                            obj['uid'] = obj['name-in-updatable-objects-repository']
                        obj['color'] = 'black'
                    # TODO: handle exclusion groups, access-roles correctly
                    if obj_type in ['updatable-object', 'access-role', 'group-with-exclusion', 'security-zone', 'dns-domain']:
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
                            logger.debug(f"rewriting non-standard cp-host-type '{obj['name']}' with object type '{obj_type}' to host")
                            logger.debug("obj_dump:" + json.dumps(obj, indent=3))
                        obj_type = 'host'
                    # adding the object:
                    if not 'comments' in obj or obj['comments'] == '':
                        comments = None
                    else:
                        comments = obj['comments']
                    nw_objects.extend([{'obj_uid': obj['uid'], 'obj_name': obj['name'], 'obj_color': obj['color'],
                                        'obj_comment': comments,
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


def makeHost(ipIn):
    ip_obj = ipaddress.ip_address(ipIn)
    
    # If it's a valid address, append the appropriate CIDR notation
    if isinstance(ip_obj, ipaddress.IPv4Address):
        return f"{ipIn}/32"
    elif isinstance(ip_obj, ipaddress.IPv6Address):
        return f"{ipIn}/128"

# def get_first_and_last_ip(cidr_notation):
#     # Create an ip_network object
#     network = ipaddress.ip_network(cidr_notation, strict=False)
    
#     # Get the first IP address in the network
#     first_ip = makeHost(network.network_address)
    
#     # Get the last IP address in the network
#     last_ip = makeHost(network.broadcast_address)
    
#     return first_ip, last_ip


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
        return [makeHost(str(net.network_address)), makeHost(str(net.broadcast_address))]
            
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

from asyncio.log import logger
from fwo_log import getFwoLogger
from fwo_const import list_delimiter
import ipaddress


def normalize_nwobjects(full_config, config2import, import_id, jwt=None, mgm_id=None):
    nw_objects = []
    for obj_orig in full_config["networkObjects"]:
        nw_objects.append(parse_object(obj_orig, import_id, config2import, nw_objects))
    for obj_grp_orig in full_config["networkObjectGroups"]:
        obj_grp = extract_base_object_infos(obj_grp_orig, import_id, config2import, nw_objects)
        obj_grp["obj_typ"] = "group"
        obj_grp["obj_member_refs"], obj_grp["obj_member_names"] = parse_obj_group(obj_grp_orig, import_id, nw_objects, config2import)
        nw_objects.append(obj_grp)
    config2import['network_objects'] = nw_objects


def extract_base_object_infos(obj_orig, import_id, config2import, nw_objects):
    obj = {}

    if "type" in obj_orig:
        obj["obj_name"] = obj_orig["name"]
        obj["obj_uid"] = obj_orig["id"]
        if 'description' in obj_orig:
            obj["obj_comment"] = obj_orig["description"] 
        if 'etag' in obj_orig and not 'obj_comment' in obj:
            obj["obj_comment"] = obj_orig["etag"] 
        obj['control_id'] = import_id
    return obj


def parse_obj_group(orig_grp, import_id, nw_objects, config2import, id = None):
    refs = []
    names = []
    if "properties" in orig_grp:
        if 'ipAddresses' in orig_grp['properties']:
            for ip in orig_grp['properties']['ipAddresses']:
                new_obj = parse_object(add_network_object(config2import, ip=ip), import_id, config2import, nw_objects)
                names.append(new_obj['obj_name'])
                refs.append(new_obj['obj_uid'])
                nw_objects.append(new_obj)
    return list_delimiter.join(refs), list_delimiter.join(names)


def parse_obj_list(ip_list, import_id, config, id):
    refs = []
    names = []
    for ip in ip_list:
        # TODO: lookup ip in network_objects and re-use
        ip_obj = {}
        ip_obj['obj_name'] = ip
        ip_obj['obj_uid'] = ip_obj['obj_name'] + "_" + id
        try:
            ipaddress.ip_network(ip)
            # valid ip
            ip_obj['obj_ip'] = ip
        except Exception:
            # no valid ip - asuming azureTag
            ip_obj['obj_ip'] = '0.0.0.0/0'
            ip = '0.0.0.0/0'
            ip_obj['obj_name'] = "#"+ip_obj['obj_name']
        ip_obj['obj_type'] = 'simple'
        ip_obj['obj_typ'] = 'host'
        if "/" in ip:
            ip_obj['obj_typ'] = 'network'

        if "-" in ip: # ip range
            ip_obj['obj_typ'] = 'ip_range'
            ip_range = ip.split("-")
            ip_obj['obj_ip'] = ip_range[0]
            ip_obj['obj_ip_end'] = ip_range[1]
        
        ip_obj['control_id'] = import_id

        config.append(ip_obj)
        refs.append(ip_obj['obj_uid'])
        names.append(ip_obj['obj_name'])
    return list_delimiter.join(refs), list_delimiter.join(names)


def parse_object(obj_orig, import_id, config2import, nw_objects):
    obj = extract_base_object_infos(obj_orig, import_id, config2import, nw_objects)
    if obj_orig["type"] == "network":  # network
        obj["obj_typ"] = "network"
        cidr = obj_orig["value"].split("/")
        if str.isdigit(cidr[1]):
            obj['obj_ip'] = cidr[0] + "/" + cidr[1]
        else: # not real cidr (netmask after /)
            obj['obj_ip'] = cidr[0] + "/" + str(IPAddress(cidr[1]).netmask_bits())    
    elif obj_orig["type"] == "host": # host
        obj["obj_typ"] = "host"
        obj["obj_ip"] = obj_orig["ip"]
        if obj_orig["ip"].find(":") != -1:  # ipv6
            obj["obj_ip"] += "/128"
        else:                               # ipv4
            obj["obj_ip"] += "/32"
    elif obj_orig["type"] == "ip_range": # ip range
        obj['obj_typ'] = 'ip_range'
        ip_range = obj_orig['ip'].split("-")
        obj['obj_ip'] = ip_range[0]
        obj['obj_ip_end'] = ip_range[1]
    elif obj_orig["type"] == "FQDN": # fully qualified domain name
        obj['obj_typ'] = 'network'
        obj['obj_ip'] = "0.0.0.0/0"
        obj['obj_uid'] = obj_orig["id"]
    else:                            # unknown type
        obj["obj_name"] = obj["obj_name"] + " [not supported]"
        obj['obj_typ'] = 'network'
        obj['obj_ip'] = "0.0.0.0/0"
    obj['control_id'] = import_id

    return obj


def add_network_object(config2import, ip=None):
    if "-" in str(ip):
        type = 'ip_range'
    else:
        type = 'host'
    return {'ip': ip, 'name': ip, 'id': ip, 'type': type}

from asyncio.log import logger
from fwo_log import getFwoLogger
from fwo_const import list_delimiter
import ipaddress


def normalize_nwobjects(full_config, config2import, import_id, jwt=None, mgm_id=None):
    nw_objects = []
    for obj_orig in full_config["/Objects/Addresses"]:
        nw_objects.append(parse_object(obj_orig, import_id, config2import, nw_objects))

    for obj_grp_orig in full_config["/Objects/AddressGroups"]:
        obj_grp = extract_base_object_infos(obj_grp_orig, import_id, config2import, nw_objects)
        obj_grp["obj_typ"] = "group"
        obj_grp["obj_member_refs"], obj_grp["obj_member_names"] = parse_obj_group(obj_grp_orig, import_id, nw_objects, config2import)
        nw_objects.append(obj_grp)

    config2import['network_objects'] = nw_objects


def parse_object(obj_orig, import_id, config2import, nw_objects):
    obj = extract_base_object_infos(obj_orig, import_id, config2import, nw_objects)
    obj['obj_ip'] = obj_orig['ip-netmask']
    if '/' in obj['obj_ip'] and not '/32' in obj['obj_ip']:
        obj['obj_typ'] = 'network'
    else:
        obj['obj_typ'] = 'host'
    return obj


def extract_base_object_infos(obj_orig, import_id, config2import, nw_objects):
    obj = {}
    obj["obj_name"] = obj_orig["@name"]
    obj["obj_uid"] = obj_orig["@name"]
    if 'description' in obj_orig:
        obj["obj_comment"] = obj_orig["description"] 
    if 'tag' in obj_orig:
        tag_list = ",".join(obj_orig["tag"]['member'])
        if 'obj_comment' in obj:
            obj["obj_comment"] += ("; tags: " + tag_list)
        else:
            obj["obj_comment"] = tag_list
    obj['control_id'] = import_id
    return obj


def parse_obj_group(orig_grp, import_id, nw_objects, config2import, id = None):
    refs = []
    names = []
    if "dynamic" in orig_grp:
        pass
    if "static" in orig_grp and "member" in orig_grp["static"]:
        for m in orig_grp['static']['member']:
            names.append(m)
            refs.append(m)
    return list_delimiter.join(refs), list_delimiter.join(names)


def parse_obj_list(nw_obj_list, import_id, obj_list, id, type='network'):
    refs = []
    names = []
    for obj_name in nw_obj_list:
        names.append(obj_name)
        refs.append(lookup_obj_uid(obj_name, obj_list, import_id, type=type))
    return list_delimiter.join(refs), list_delimiter.join(names)


# def add_network_object(config2import, ip=None):
#     if "-" in str(ip):
#         type = 'ip_range'
#     else:
#         type = 'host'
#     return {'ip': ip, 'name': ip, 'id': ip, 'type': type}


def lookup_obj_uid(obj_name, obj_list, import_id, type='network'):
    for o in obj_list:
        if type=='network' and 'obj_name' in o:
            if o['obj_name']==obj_name:
                return o['obj_uid']
        elif type=='service' and 'svc_name' in o:
            if o['svc_name']==obj_name:
                return o['svc_uid']
        else:
            logger.warning("could not find object name in object " + str(o))

    # could not find existing obj in obj list, so creating new one
    if type=='network':
        refs, names = add_ip_obj([obj_name], obj_list, import_id)
        return refs ## assuming only one object here
    elif type=='service':
        logger.warning("could not find service object " + str(obj_name))
    else:
        logger.warning("unknown object type '" + type + "' for object " + str(obj_name))
    return None


def add_ip_obj(ip_list, obj_list, import_id):
    refs = []
    names = []
    for ip in ip_list:
        # TODO: lookup ip in network_objects and re-use
        ip_obj = {}
        ip_obj['obj_name'] = ip
        ip_obj['obj_uid'] = ip_obj['obj_name']
        try:
            ipaddress.ip_network(ip)
            # valid ip
            ip_obj['obj_ip'] = ip
        except:
            # no valid ip - asusming Tag
            ip_obj['obj_ip'] = '0.0.0.0/0'
            ip = '0.0.0.0/0'
            ip_obj['obj_name'] = "#"+ip_obj['obj_name']
            ip_obj['obj_uid'] = ip_obj['obj_name']
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

        obj_list.append(ip_obj)
        refs.append(ip_obj['obj_uid'])
        names.append(ip_obj['obj_name'])
    return list_delimiter.join(refs), list_delimiter.join(names)

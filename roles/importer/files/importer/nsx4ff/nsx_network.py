from asyncio.log import logger
from fwo_log import getFwoLogger
from fwo_const import list_delimiter
import ipaddress


def normalize_nwobjects(full_config, config2import, import_id, jwt=None, mgm_id=None):
    logger = getFwoLogger()
    nw_objects = []
    nw_tagged_groups = {}
    for obj_orig in full_config["/Objects/Addresses"]:
        nw_objects.append(parse_object(obj_orig, import_id, config2import, nw_objects))
        if 'tag' in obj_orig and 'member' in obj_orig['tag']:
            logger.info("found simple network object with tags: " + obj_orig['@name'])
            for t in obj_orig['tag']['member']:
                collect_tag_information(nw_tagged_groups, "#"+t, obj_orig['@name'])

    for tag in nw_tagged_groups:
        logger.info("handling nw_tagged_group: " + tag + " with members: " + list_delimiter.join(nw_tagged_groups[tag]))
        obj = {}
        obj["obj_name"] = tag
        obj["obj_uid"] = tag
        obj["obj_comment"] = 'dynamic group defined by tagging'
        obj['control_id'] = import_id
        obj['obj_typ'] = 'group'
        members = nw_tagged_groups[tag] # parse_dynamic_object_group(obj_grp_orig, nw_tagged_groups)
        obj['obj_members'] = list_delimiter.join(members)
        obj['obj_member_refs'] = list_delimiter.join(members)
        nw_objects.append(obj)

    for obj_grp_orig in full_config["/Objects/AddressGroups"]:
        logger.info("found network group: " + obj_grp_orig['@name'])
        obj_grp = extract_base_object_infos(obj_grp_orig, import_id, config2import, nw_objects)
        obj_grp["obj_typ"] = "group"
        if 'static' in obj_grp_orig and 'filter' in obj_grp_orig['static']:
            obj_grp["obj_member_refs"], obj_grp["obj_member_names"] = parse_static_obj_group(obj_grp_orig, import_id, nw_objects, config2import)
        if 'dynamic' in obj_grp_orig and 'filter' in obj_grp_orig['dynamic']:
            members = parse_dynamic_object_group(obj_grp_orig, nw_tagged_groups)
            obj_grp["obj_member_refs"] = list_delimiter.join(members)
            obj_grp["obj_member_names"] = list_delimiter.join(members)
        nw_objects.append(obj_grp)
        if 'tag' in obj_grp_orig and 'member' in obj_grp_orig['tag']:
            logger.info("found network group with tags: " + obj_grp_orig['@name'])
            for t in obj_grp_orig['tag']['member']:
                logger.info("    found tag " + t)
                collect_tag_information(nw_tagged_groups, "#"+t, obj_grp_orig['@name'])
    
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


def parse_dynamic_object_group(orig_grp, nw_tagged_groups):
    if "dynamic" in orig_grp:
        if 'filter' in orig_grp['dynamic']:
            if ' ' not in orig_grp['dynamic']['filter']:
                # just a single tag
                # add all nw objects with the tag to this group
                tag = "#" + orig_grp['dynamic']['filter'][1:-1]
                if tag in nw_tagged_groups:
                    return nw_tagged_groups[tag]
            else:
                # later: deal with more complex tagging (and/or)
                return []
    return []


def parse_static_obj_group(orig_grp, import_id, nw_objects, config2import, id = None):
    refs = []
    names = []

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


def collect_tag_information(tagged_groups, tag, obj_name):
    if tag in tagged_groups.keys():
        tagged_groups[tag].append(obj_name)
    else:
        tagged_groups.update({tag: [obj_name]})

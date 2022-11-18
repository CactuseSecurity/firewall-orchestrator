from asyncio.log import logger
import random

from fwo_log import getFwoLogger
from fwo_const import list_delimiter
from netaddr import IPAddress

def normalize_nwobjects(full_config, config2import, import_id, jwt=None, mgm_id=None):
    logger = getFwoLogger()
    nw_objects = []
    for obj_orig in full_config["networkObjects"]:
        nw_objects.append(parse_object(obj_orig, import_id))
    for obj_grp_orig in full_config["networkObjectGroups"]:
        obj_grp = extract_base_object_infos(obj_grp_orig, import_id)
        obj_grp["obj_typ"] = "group"
        obj_grp["obj_member_refs"], obj_grp["obj_member_names"] = parse_obj_group(obj_grp_orig, import_id, nw_objects)
        nw_objects.append(obj_grp)
    config2import['network_objects'] = nw_objects

def parse_obj_group(orig_grp, import_id, nw_objects, id = None):
    refs = []
    names = []
    if "literals" in orig_grp:
        if id == None:
            id = orig_grp["id"] if "id" in orig_grp else random.random()
        for orig_literal in orig_grp["literals"]:
            literal = parse_object(orig_literal, import_id)
            literal["obj_uid"] += "_" + id
            nw_objects.append(literal)
            names.append(orig_literal["value"])
            refs.append(literal["obj_uid"])
    if "objects" in orig_grp:
        for orig_obj in orig_grp["objects"]:
            if "type" in orig_obj:
                if (orig_obj["type"] != "NetworkGroup" and orig_obj["type"] != "Host" and 
                    orig_obj["type"] != "Network" and orig_obj["type"] != "Range" and
                    orig_obj["type"] != "FQDN"):
                    logger = getFwoLogger()
                    logger.warn("Unknown network object type found: \"" + orig_obj["type"] + "\". Skipping.")             
                    break
            names.append(orig_obj["name"])
            refs.append(orig_obj["id"])

    return list_delimiter.join(refs), list_delimiter.join(names)

def extract_base_object_infos(obj_orig, import_id):
    logger = getFwoLogger()
    obj = {}
    if "id" in obj_orig:
        obj["obj_uid"] = obj_orig['id']
    else:
        obj["obj_uid"] = obj_orig["value"]
    if "name" in obj_orig:
        obj["obj_name"] = obj_orig["name"]
    else:
        obj["obj_name"] = obj_orig["value"]  
    if 'description' in obj_orig:
        obj["obj_comment"] = obj_orig["description"] 
    if 'color' in obj_orig:
        # TODO Do colors exist?
        logger.log("colors exist :)")
    obj['control_id'] = import_id
    return obj

def parse_object(obj_orig, import_id):
    obj = extract_base_object_infos(obj_orig, import_id)
    if obj_orig["type"] == "Network":  # network
        obj["obj_typ"] = "network"
        cidr = obj_orig["value"].split("/")
        if str.isdigit(cidr[1]):
            obj['obj_ip'] = cidr[0] + "/" + cidr[1]
        else: # not real cidr (netmask after /)
            obj['obj_ip'] = cidr[0] + "/" + str(IPAddress(cidr[1]).netmask_bits())    
    elif obj_orig["type"] == "Host": # host
        obj["obj_typ"] = "host"
        obj["obj_ip"] = obj_orig["value"]
        if obj_orig["value"].find(":") != -1:  # ipv6
            obj["obj_ip"] + "/64"
        else:                               # ipv4
            obj["obj_ip"] + "/32"
    elif obj_orig["type"] == "Range": # ip range
        obj['obj_typ'] = 'ip_range'
        ip_range = obj_orig['value'].split("-")
        obj['obj_ip'] = ip_range[0]
        obj['obj_ip_end'] = ip_range[1]
    elif obj_orig["type"] == "FQDN": # fully qualified domain name
        obj['obj_typ'] = 'network'
        obj['obj_ip'] = "0.0.0.0/0"
    else:                            # unknown type
        obj["obj_name"] = obj["obj_name"] + " [not supported]"
        obj['obj_typ'] = 'network'
        obj['obj_ip'] = "0.0.0.0/0"
    return obj

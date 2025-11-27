import random
from typing import Any

from fwo_log import FWOLogger
from fwo_const import LIST_DELIMITER
from netaddr import IPAddress

# Constants
DEFAULT_IP_ANY = "0.0.0.0/0"

def normalize_nwobjects(full_config: dict[str, Any], config2import: dict[str, Any], import_id: str):
    nw_objects: list[dict[str, Any]] = []
    for obj_orig in full_config["networkObjects"]:
        nw_objects.append(parse_object(obj_orig, import_id))
    for obj_grp_orig in full_config["networkObjectGroups"]:
        obj_grp = extract_base_object_infos(obj_grp_orig, import_id)
        obj_grp["obj_typ"] = "group"
        obj_grp["obj_member_refs"], obj_grp["obj_member_names"] = parse_obj_group(obj_grp_orig, import_id, nw_objects)
        nw_objects.append(obj_grp)
    config2import['network_objects'] = nw_objects

def parse_obj_group(orig_grp: dict[str, Any], import_id: str, nw_objects: list[dict[str, Any]], group_id: str | None = None):
    refs: list[str] = []
    names: list[str] = []
    
    if "literals" in orig_grp:
        refs, names = process_group_literals(orig_grp, import_id, nw_objects, group_id, refs, names)
    
    if "objects" in orig_grp:
        refs, names = process_group_objects(orig_grp, refs, names)

    return LIST_DELIMITER.join(refs), LIST_DELIMITER.join(names)

def process_group_literals(orig_grp: dict[str, Any], import_id: str, nw_objects: list[dict[str, Any]], 
                          group_id: str | None, refs: list[str], names: list[str]) -> tuple[list[str], list[str]]:
    if group_id is None:
        group_id = orig_grp["id"] if "id" in orig_grp else str(random.random())
    
    for orig_literal in orig_grp["literals"]:
        literal = parse_object(orig_literal, import_id)
        literal["obj_uid"] += "_" + str(group_id)
        nw_objects.append(literal)
        names.append(orig_literal["value"])
        refs.append(literal["obj_uid"])
    
    return refs, names

def process_group_objects(orig_grp: dict[str, Any], refs: list[str], names: list[str]) -> tuple[list[str], list[str]]:
    valid_types = {"NetworkGroup", "Host", "Network", "Range", "FQDN"}
    
    for orig_obj in orig_grp["objects"]:
        if "type" in orig_obj and orig_obj["type"] not in valid_types:
            FWOLogger.warning("Unknown network object type found: \"" + orig_obj["type"] + "\". Skipping.")             
            break
        names.append(orig_obj["name"])
        refs.append(orig_obj["id"])
    
    return refs, names

def extract_base_object_infos(obj_orig: dict[str, Any], import_id: str) -> dict[str, Any]:
    obj: dict[str, Any] = {}
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
        FWOLogger.debug("Color attribute found in object")
    obj['control_id'] = import_id
    return obj


def parse_object(obj_orig: dict[str, Any], import_id: str) -> dict[str, Any]:
    obj = extract_base_object_infos(obj_orig, import_id)
    
    if obj_orig["type"] == "Network":
        return parse_network_object(obj_orig, obj)
    elif obj_orig["type"] == "Host":
        return parse_host_object(obj_orig, obj)
    elif obj_orig["type"] == "Range":
        return parse_range_object(obj_orig, obj)
    elif obj_orig["type"] == "FQDN":
        return parse_fqdn_object(obj)
    else:
        return parse_unknown_object(obj)

def parse_network_object(obj_orig: dict[str, Any], obj: dict[str, Any]) -> dict[str, Any]:
    obj["obj_typ"] = "network"
    if "value" in obj_orig:
        cidr = obj_orig["value"].split("/")
        if str.isdigit(cidr[1]):
            obj['obj_ip'] = cidr[0] + "/" + cidr[1]
        else:  # not real cidr (netmask after /)
            obj['obj_ip'] = cidr[0] + "/" + str(IPAddress(cidr[1]).netmask_bits())
    else:
        FWOLogger.warning("missing value field in object - skipping: " + str(obj_orig))
        obj['obj_ip'] = "0.0.0.0"
    return obj

def parse_host_object(obj_orig: dict[str, Any], obj: dict[str, Any]) -> dict[str, Any]:
    obj["obj_typ"] = "host"
    if "value" in obj_orig:
        obj["obj_ip"] = obj_orig["value"]
        obj["obj_ip"] = add_subnet_mask_if_needed(obj["obj_ip"])
    else:
        FWOLogger.warning("missing value field in object - skipping: " + str(obj_orig))
        obj['obj_ip'] = DEFAULT_IP_ANY
    return obj

def add_subnet_mask_if_needed(ip_value: str) -> str:
    if ip_value.find("/") != -1:
        return ip_value
    if ip_value.find(":") != -1:  # ipv6
        return ip_value + "/128"
    else:  # ipv4
        return ip_value + "/32"

def parse_range_object(obj_orig: dict[str, Any], obj: dict[str, Any]) -> dict[str, Any]:
    obj['obj_typ'] = 'ip_range'
    ip_range = obj_orig['value'].split("-")
    obj['obj_ip'] = ip_range[0]
    obj['obj_ip_end'] = ip_range[1]
    return obj

def parse_fqdn_object(obj: dict[str, Any]) -> dict[str, Any]:
    obj['obj_typ'] = 'network'
    obj['obj_ip'] = DEFAULT_IP_ANY
    return obj

def parse_unknown_object(obj: dict[str, Any]) -> dict[str, Any]:
    obj["obj_name"] = obj["obj_name"] + " [not supported]"
    obj['obj_typ'] = 'network'
    obj['obj_ip'] = DEFAULT_IP_ANY
    return obj

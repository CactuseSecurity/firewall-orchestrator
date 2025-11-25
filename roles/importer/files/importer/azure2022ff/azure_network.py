from typing import Any
from netaddr import IPAddress
from fwo_const import LIST_DELIMITER
import ipaddress


def normalize_nwobjects(full_config: dict[str, Any], config2import: dict[str, Any], import_id: str, jwt: str | None = None, mgm_id: str | None = None) -> None:
    nw_objects: list[dict[str, Any]] = []
    for obj_orig in full_config["networkObjects"]:
        nw_objects.append(parse_object(obj_orig, import_id, config2import, nw_objects))
    for obj_grp_orig in full_config["networkObjectGroups"]:
        obj_grp = extract_base_object_infos(obj_grp_orig, import_id, config2import, nw_objects)
        obj_grp["obj_typ"] = "group"
        obj_grp["obj_member_refs"], obj_grp["obj_member_names"] = parse_obj_group(obj_grp_orig, import_id, nw_objects, config2import)
        nw_objects.append(obj_grp)
    config2import['network_objects'] = nw_objects


def extract_base_object_infos(obj_orig: dict[str, Any], import_id: str, config2import: dict[str, Any], nw_objects: list[dict[str, Any]]) -> dict[str, Any]:
    obj: dict[str, Any] = {}

    if "type" in obj_orig:
        obj["obj_name"] = obj_orig["name"]
        obj["obj_uid"] = obj_orig["id"]
        if 'description' in obj_orig:
            obj["obj_comment"] = obj_orig["description"] 
        if 'etag' in obj_orig and not 'obj_comment' in obj:
            obj["obj_comment"] = obj_orig["etag"] 
        obj['control_id'] = import_id
    return obj


def parse_obj_group(orig_grp: dict[str, Any], import_id: str, nw_objects: list[dict[str, Any]], config2import: dict[str, Any], id: str | None = None) -> tuple[str, str]:
    refs: list[str] = []
    names: list[str] = []
    if "properties" in orig_grp:
        if 'ipAddresses' in orig_grp['properties']:
            for ip in orig_grp['properties']['ipAddresses']:
                new_obj = parse_object(add_network_object(config2import, ip=ip), import_id, config2import, nw_objects)
                names.append(new_obj['obj_name'])
                refs.append(new_obj['obj_uid'])
                nw_objects.append(new_obj)
    return LIST_DELIMITER.join(refs), LIST_DELIMITER.join(names)


def parse_obj_list(ip_list: list[str], import_id: str, config: dict[str, Any], id: str | None = None) -> tuple[str, str]:
    refs: list[str] = []
    names: list[str] = []
    for ip in ip_list:
        # TODO: lookup ip in network_objects and re-use
        ip_obj: dict[str, Any] = {}
        ip_obj['obj_name'] = ip
        ip_obj['obj_uid'] = ip_obj['obj_name'] + "_" + (id if id is not None else "")
        try:
            ipaddress.ip_network(ip)
            # valid ip
            ip_obj['obj_ip'] = ip
        except Exception:
            # no valid ip - assuming azureTag
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

        config.append(ip_obj) # type: ignore # TODO: config is dict[str, Any], not list
        refs.append(ip_obj['obj_uid'])
        names.append(ip_obj['obj_name'])
    return LIST_DELIMITER.join(refs), LIST_DELIMITER.join(names)


def parse_object(obj_orig: dict[str, Any], import_id: str, config2import: dict[str, Any], nw_objects: list[dict[str, Any]]) -> dict[str, Any]:
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


def add_network_object(config2import: dict[str, Any], ip: str | None = None) -> dict[str, Any]:
    if "-" in str(ip):
        type = 'ip_range'
    else:
        type = 'host'
    return {'ip': ip, 'name': ip, 'id': ip, 'type': type}

import ipaddress
from typing import Any

from fw_modules.fortiadom5ff.fmgr_zone import find_zones_in_normalized_config
from fwo_base import sort_and_join_refs
from fwo_const import LIST_DELIMITER, NAT_POSTFIX
from fwo_exceptions import FwoImporterErrorInconsistencies
from fwo_log import FWOLogger


def normalize_network_objects(
    native_config: dict[str, Any],
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    nw_obj_types: list[str],
) -> None:
    nw_objects: list[dict[str, Any]] = []

    if "objects" not in native_config:
        return  # no objects to normalize
    # objects dicts have type as toplevel key due to rewrite_native_config_obj_type_as_key
    for current_obj_type in native_config["objects"]:
        if not (current_obj_type in nw_obj_types and "data" in native_config["objects"][current_obj_type]):
            continue
        for obj_orig in native_config["objects"][current_obj_type]["data"]:
            normalize_network_object(
                obj_orig,
                nw_objects,
                normalized_config_adom,
                normalized_config_global,
                native_config["objects"],
                current_obj_type,
            )

    if native_config.get("is-super-manager", False):
        # finally add "Original" network object for natting (only in global domain)
        original_obj_name = "Original"
        original_obj_uid = "Original"
        nw_objects.append(
            create_network_object(
                name=original_obj_name,
                type="network",
                ip="0.0.0.0",
                ip_end="255.255.255.255",
                uid=original_obj_uid,
                zone="global",
                color="black",
                comment='"original" network object created by FWO importer for NAT purposes',
            )
        )

    normalized_config_adom.update({"network_objects": nw_objects})


def get_obj_member_refs_list(
    obj_orig: dict[str, Any], native_config_objects: dict[str, Any], current_obj_type: str
) -> list[str]:
    obj_member_refs_list: list[str] = []
    for member_name in obj_orig["member"]:
        for obj_type in native_config_objects:
            if exclude_object_types_in_member_ref_search(obj_type, current_obj_type):
                continue
            for potential_member in native_config_objects[obj_type]["data"]:
                if potential_member["name"] == member_name:
                    obj_member_refs_list.append(potential_member.get("uuid", potential_member["name"]))
    if len(obj_member_refs_list) != len(obj_orig["member"]):
        raise FwoImporterErrorInconsistencies(
            f"Member inconsistent for object {obj_orig['name']}, found members={obj_orig['member']!s} and member_refs={obj_member_refs_list!s}"
        )
    return obj_member_refs_list


def exclude_object_types_in_member_ref_search(obj_type: str, current_obj_type: str) -> bool:
    # TODO expand for all kinds of missmatches in group and member
    skip_member_ref_loop = False
    if current_obj_type.endswith("firewall/addrgrp"):
        if obj_type.endswith("firewall/ippool"):
            skip_member_ref_loop = True
    return skip_member_ref_loop


def normalize_network_object(
    obj_orig: dict[str, Any],
    nw_objects: list[dict[str, Any]],
    normalized_config_adom: dict[str, Any],
    normalized_config_global: dict[str, Any],
    native_config_objects: dict[str, Any],
    current_obj_type: str,
) -> None:
    obj: dict[str, Any] = {}
    obj.update({"obj_name": obj_orig["name"]})
    if "subnet" in obj_orig:  # ipv4 object
        _parse_subnet(obj, obj_orig)
    elif "ip6" in obj_orig:  # ipv6 object
        normalize_network_object_ipv6(obj_orig, obj)
    elif "member" in obj_orig:  # addrgrp4, TODO for addrgrp6 change obj_typ to 'group_v6' and adjust obj_member_refs
        member_name_list: list[str] = obj_orig["member"]
        member_ref_list: list[str] = get_obj_member_refs_list(obj_orig, native_config_objects, current_obj_type)
        sorted_member_refs, sorted_member_names = sort_and_join_refs(list(zip(member_ref_list, member_name_list)))
        obj.update({"obj_typ": "group"})
        obj.update({"obj_member_names": sorted_member_names})
        obj.update({"obj_member_refs": sorted_member_refs})
    elif "startip" in obj_orig:  # ippool object
        obj.update({"obj_typ": "ip_range"})
        obj.update({"obj_ip": obj_orig["startip"]})
        obj.update({"obj_ip_end": obj_orig["endip"]})
    elif "start-ip" in obj_orig:  # standard ip range object
        obj.update({"obj_typ": "ip_range"})
        obj.update({"obj_ip": obj_orig["start-ip"]})
        obj.update({"obj_ip_end": obj_orig["end-ip"]})
    elif "extip" in obj_orig:  # vip object, simplifying to a single ip
        normalize_vip_object(obj_orig, obj, nw_objects)
    elif "wildcard-fqdn" in obj_orig:
        obj.update({"obj_typ": "domain"})
        obj.update({"obj_ip": "0.0.0.0"})
        obj.update({"obj_ip_end": "255.255.255.255"})
    else:  # 'fqdn' in obj_orig: # "fully qualified domain name address" // other unknown types
        obj.update({"obj_typ": "network"})
        obj.update({"obj_ip": "0.0.0.0"})
        obj.update({"obj_ip_end": "255.255.255.255"})

    # if obj_ip_end is not define, set it to obj_ip (assuming host)
    if obj.get("obj_ip_end") is None and obj.get("obj_typ") == "host":
        obj["obj_ip_end"] = obj.get("obj_ip")

    obj.update({"obj_comment": obj_orig.get("comment")})
    # TODO: deal with all other colors (will be currently ignored)
    # we would need a list of fortinet color codes, maybe:
    # https://community.fortinet.com/t5/Support-Forum/Object-color-codes-for-CLI/td-p/249479
    # if 'color' in obj_orig and obj_orig['color']==0:
    #    obj.update({'obj_color': 'black'})
    obj.update({"obj_color": "black"})

    obj.update(
        {"obj_uid": obj_orig.get("uuid", obj_orig["name"])}
    )  # using name as fallback, but this should not happen

    associated_interfaces = find_zones_in_normalized_config(
        obj_orig.get("associated-interface", []), normalized_config_adom, normalized_config_global
    )
    obj.update({"obj_zone": LIST_DELIMITER.join(associated_interfaces)})

    nw_objects.append(obj)


def _parse_subnet(obj: dict[str, Any], obj_orig: dict[str, Any]) -> None:
    ipa = ipaddress.ip_network(str(obj_orig["subnet"][0]) + "/" + str(obj_orig["subnet"][1]))
    if ipa.num_addresses > 1:
        obj.update({"obj_typ": "network"})
    else:
        obj.update({"obj_typ": "host"})
    obj.update({"obj_ip": str(ipa.network_address)})
    obj.update({"obj_ip_end": str(ipa.broadcast_address)})


def normalize_network_object_ipv6(obj_orig: dict[str, Any], obj: dict[str, Any]) -> None:
    ipa = ipaddress.ip_network(obj_orig["ip6"])
    if ipa.num_addresses > 1:
        obj.update({"obj_typ": "network"})
    else:
        obj.update({"obj_typ": "host"})
    obj.update({"obj_ip": str(ipa.network_address)})
    obj.update({"obj_ip_end": str(ipa.broadcast_address)})


def normalize_vip_object(obj_orig: dict[str, Any], obj: dict[str, Any], nw_objects: list[dict[str, Any]]) -> None:
    obj_zone = "global"
    obj.update({"obj_typ": "host"})
    if "extip" not in obj_orig or len(obj_orig["extip"]) == 0:
        FWOLogger.error("vip (extip): found empty extip field for " + obj_orig["name"])
    else:
        if len(obj_orig["extip"]) > 1:
            FWOLogger.warning(
                "vip (extip): found more than one extip, just using the first one for " + obj_orig["name"]
            )
        set_ip_in_obj(obj, obj_orig["extip"][0])  # resolving nat range if there is one
        nat_obj: dict[str, Any] = {}
        nat_obj.update({"obj_typ": "host"})
        nat_obj.update({"obj_color": "black"})
        nat_obj.update({"obj_comment": "FWO-auto-generated nat object for VIP"})
        if (
            "obj_ip_end" in obj
        ):  # this obj is a range - include the end ip in name and uid as well to avoid akey conflicts
            nat_obj.update({"obj_ip_end": str(obj["obj_ip_end"])})

        normalize_vip_object_nat_ip(obj_orig, obj, nat_obj)

        if "obj_ip_end" not in nat_obj:
            nat_obj.update({"obj_ip_end": str(obj["obj_nat_ip"])})

        if (
            "associated-interface" in obj_orig and len(obj_orig["associated-interface"]) > 0
        ):  # and obj_orig['associated-interface'][0] != 'any':
            obj_zone = obj_orig["associated-interface"][0]
        nat_obj.update({"obj_zone": obj_zone})
        # nat_obj.update({'control_id': import_state.ImportId})
        if (
            nat_obj not in nw_objects
        ):  # rare case when a destination nat is down for two different orig ips to the same dest ip
            nw_objects.append(nat_obj)


def normalize_vip_object_nat_ip(obj_orig: dict[str, Any], obj: dict[str, Any], nat_obj: dict[str, Any]) -> None:
    # now dealing with the nat ip obj (mappedip)
    if "mappedip" not in obj_orig or len(obj_orig["mappedip"]) == 0:
        FWOLogger.warning("vip (extip): found empty mappedip field for " + obj_orig["name"])
        return

    if len(obj_orig["mappedip"]) > 1:
        FWOLogger.warning("vip (extip): found more than one mappedip, just using the first one for " + obj_orig["name"])
    nat_ip = obj_orig["mappedip"][0]
    set_ip_in_obj(nat_obj, str(nat_ip))
    obj.update({"obj_nat_ip": str(nat_obj["obj_ip"])})  # save nat ip in vip obj
    if (
        "obj_ip_end" in nat_obj
    ):  # this nat obj is a range - include the end ip in name and uid as well to avoid akey conflicts
        obj.update({"obj_nat_ip_end": str(nat_obj["obj_ip_end"])})  # save nat ip in vip obj
        nat_obj.update({"obj_name": nat_obj["obj_ip"] + "-" + nat_obj["obj_ip_end"] + NAT_POSTFIX})
    else:
        obj.update({"obj_nat_ip_end": str(nat_obj["obj_ip"])})  # assuming host with obj_nat_ip_end = obj_nat_ip
        nat_obj.update({"obj_name": nat_obj["obj_ip"] + NAT_POSTFIX})
    nat_obj.update({"obj_uid": nat_obj["obj_name"]})
    ###### range handling


def set_ip_in_obj(
    nw_obj: dict[str, Any], ip: str
) -> None:  # add start and end ip in nw_obj if it is a range, otherwise do nothing
    if "-" in ip:  # dealing with range
        ip_start, ip_end = ip.split("-")
        nw_obj.update({"obj_ip": str(ip_start)})
        if ip_end != ip_start:
            nw_obj.update({"obj_ip_end": str(ip_end)})
    else:
        nw_obj.update({"obj_ip": str(ip)})


# for members of groups, the name of the member obj needs to be fetched separately (starting from API v1.?)
def resolve_nw_uid_to_name(uid: str, nw_objects: list[dict[str, Any]]) -> str:
    # return name of nw_objects element where obj_uid = uid
    for obj in nw_objects:
        if obj["obj_uid"] == uid:
            return obj["obj_name"]
    return 'ERROR: uid "' + uid + '" not found'


def add_member_names_for_nw_group(idx: int, nw_objects: list[dict[str, Any]]) -> None:
    group = nw_objects.pop(idx)
    if group["obj_member_refs"] == "" or group["obj_member_refs"] == None:
        group["obj_member_names"] = None
        group["obj_member_refs"] = None
    else:
        member_names = ""
        obj_member_refs = group["obj_member_refs"].split(LIST_DELIMITER)
        for ref in obj_member_refs:
            member_name = resolve_nw_uid_to_name(ref, nw_objects)
            member_names += member_name + LIST_DELIMITER
        group["obj_member_names"] = member_names[:-1]
    nw_objects.insert(idx, group)


def create_network_object(
    name: str, type: str, ip: str, ip_end: str | None, uid: str, color: str, comment: str | None, zone: str | None
) -> dict[str, Any]:
    return {
        "obj_name": name,
        "obj_typ": type,
        "obj_ip": ip,
        "obj_ip_end": ip_end,
        "obj_uid": uid,
        "obj_color": color,
        "obj_comment": comment,
        "obj_zone": zone,
    }


def get_nw_obj(nat_obj_name: str, nwobjects: list[dict[str, Any]]) -> dict[str, Any] | None:
    for obj in nwobjects:
        if "obj_name" in obj and obj["obj_name"] == nat_obj_name:
            return obj
    return None


# this removes all obj_nat_ip entries from all network objects
# these were used during import but might cause issues if imported into db
def remove_nat_ip_entries(config2import: dict[str, Any]) -> None:
    for obj in config2import["network_objects"]:
        if "obj_nat_ip" in obj:
            obj.pop("obj_nat_ip")


def get_first_ip_of_destination(obj_ref: str, config2import: dict[str, Any]) -> str | None:
    if LIST_DELIMITER in obj_ref:
        obj_ref = obj_ref.split(LIST_DELIMITER)[0]
        # if destination does not contain exactly one ip, raise a warning
        FWOLogger.info(
            "src nat behind interface: more than one NAT IP - just using the first one for routing decision for obj_ref "
            + obj_ref
        )

    for obj in config2import["network_objects"]:
        if "obj_uid" in obj and obj["obj_uid"] == obj_ref:
            if "obj_type" in obj and obj["obj_type"] == "group":
                if "obj_member_refs" in obj and LIST_DELIMITER in obj["obj_member_refs"]:
                    return get_first_ip_of_destination(obj["obj_member_refs"].split(LIST_DELIMITER)[0], config2import)
            elif "obj_ip" in obj:
                return obj["obj_ip"]
    FWOLogger.warning("src nat behind interface: found no IP info for destination object " + obj_ref)
    return None

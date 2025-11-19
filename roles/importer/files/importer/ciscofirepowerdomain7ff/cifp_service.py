import random
from typing import Any
from fwo_const import list_delimiter


def normalize_svcobjects(full_config: dict[str, Any], config2import: dict[str, Any], import_id: str) -> None:
    svc_objects: list[dict[str, Any]] = []
    for svc_orig in full_config["serviceObjects"]:
        svc_objects.append(parse_svc(svc_orig, import_id))
    for svc_grp_orig in full_config["serviceObjectGroups"]:
        svc_grp = extract_base_svc_infos(svc_grp_orig, import_id)
        svc_grp["svc_typ"] = "group"
        svc_grp["svc_member_refs"] , svc_grp["svc_member_names"] = parse_svc_group(svc_grp_orig, import_id, svc_objects)
        svc_objects.append(svc_grp)
    config2import['service_objects'] = svc_objects

def extract_base_svc_infos(svc_orig: dict[str, Any], import_id: str) -> dict[str, Any]:
    svc: dict[str, Any] = {}
    if "id" in svc_orig:
        svc["svc_uid"] = svc_orig["id"]
    else:
        svc["svc_uid"] = svc_orig["protocol"]
        if "port" in svc_orig:
            svc["svc_uid"] += "_" + svc_orig["port"] 
    if "name" in svc_orig:
        svc["svc_name"] = svc_orig["name"]
    else:
        svc["svc_name"] = svc_orig["protocol"]
        if "port" in svc_orig:
            svc["svc_name"] += "_" + svc_orig["port"] 
    if "svc_comment" in svc_orig:
        svc["svc_comment"] = svc_orig["comment"]
    svc["svc_timeout"] = None
    svc["svc_color"] = None
    svc["control_id"] = import_id 
    return svc

def parse_svc(orig_svc: dict[str, Any], import_id: str) -> dict[str, Any]:
    svc = extract_base_svc_infos(orig_svc, import_id)
    svc["svc_typ"] = "simple"
    parse_port(orig_svc, svc)
    if orig_svc["type"] == "ProtocolPortObject":
        if orig_svc["protocol"] == "TCP":
            svc["ip_proto"] = 6
        elif orig_svc["protocol"] == "UDP":
            svc["ip_proto"] = 17
        elif orig_svc["protocol"] == "ESP":
            svc["ip_proto"] = 50
        else:
            svc["svc_name"] += " [Protocol \"" + orig_svc["protocol"] + "\" not supported]"
        # TODO Icmp             
        # TODO add all protocols
    elif orig_svc["type"] == "PortLiteral":
        svc["ip_proto"] = orig_svc["protocol"]
    else:
        svc["svc_name"] += " [Not supported]"
    return svc

def parse_port(orig_svc: dict[str, Any], svc: dict[str, Any]) -> None:
    if "port" in orig_svc:
        if orig_svc["port"].find("-") != -1: # port range
            port_range = orig_svc["port"].split("-")
            svc["svc_port"] = port_range[0]
            svc["svc_port_end"] = port_range[1]
        else: # single port
            svc["svc_port"] = orig_svc["port"]
            svc["svc_port_end"] = None

def parse_svc_group(orig_svc_grp: dict[str, Any], import_id: str, svc_objects: list[dict[str, Any]], svcgrp_id: str | None = None) -> tuple[str, str]:
    refs: list[str] = []
    names: list[str] = []

    if "literals" in orig_svc_grp:
        if svcgrp_id is None:
            svcgrp_id = orig_svc_grp["id"] if "id" in orig_svc_grp else str(random.random())
        for orig_literal in orig_svc_grp["literals"]:
            literal = parse_svc(orig_literal, import_id)
            literal["svc_uid"] += "_" + str(svcgrp_id)
            svc_objects.append(literal)
            names.append(literal["svc_name"])
            refs.append(literal["svc_uid"])
    if "objects" in orig_svc_grp:
        for svc_orig in orig_svc_grp["objects"]:
            refs.append(svc_orig["id"])
            names.append(svc_orig["name"])
    return list_delimiter.join(refs), list_delimiter.join(names)
 
#!/usr/bin/env python3
"""Parser for Cisco ASA configuration files.

The script converts an ASA configuration into a simplified
normalized JSON structure which is inspired by the public
sample configuration provided at
https://fwodemodata.cactus.de/demo12_cpr8x_v9.json.

The parser is not a complete ASA parser.  It focuses on
constructs used in the example configuration: interfaces,
network objects and groups, service groups, time ranges and
ACLs.  Unknown lines are ignored so the script can be applied
to larger configurations without failing.

Usage:
    python asa_config_parser.py <asa_config_file>
"""

from __future__ import annotations

import re
from typing import List, Optional, Tuple

from netaddr import IPNetwork

import fwo_const
from pathlib import Path
from scrapli.driver import GenericDriver
import time

from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanagerlist import FwConfigManagerList
from fwo_log import getFwoLogger
from models.networkobject import NetworkObject
from ciscoasa9.asa_models import AccessGroupBinding, AccessList, AccessListEntry, AsaEnablePassword,\
    AsaNetworkObject, AsaNetworkObjectGroup, AsaServiceModule, AsaServiceObject, AsaServiceObjectGroup,\
    ClassMap, Config, DnsInspectParameters, EndpointKind, InspectionAction, Interface, MgmtAccessRule,\
    Names, NatRule, PolicyClass, PolicyMap, Route, ServicePolicyBinding  
from fwo_enums import ConfigAction
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from model_controllers.management_controller import ManagementController
from models.serviceobject import ServiceObject
from ciscoasa9.asa_parser_functions import _clean_lines, _consume_block, _parse_class_map_block, \
    _parse_dns_inspect_policy_map_block, _parse_interface_block, _parse_network_object_block, \
    _parse_network_object_group_block, _parse_policy_map_block, _parse_service_object_block, \
    _parse_service_object_group_block, _parse_endpoint
from ciscoasa9.asa_parser import parse_asa_config
from ciscoasa9.asa_maps import name_to_port, protocol_map
from models.rulebase import Rulebase
from models.rule import RuleNormalized, RuleAction, RuleTrack, RuleType
from models.gateway import Gateway
from models.rulebase_link import RulebaseLinkUidBased


def has_config_changed(full_config, mgm_details, force=False):
    full_config = full_config
    mgm_details = mgm_details
    force = force
    # dummy - may be filled with real check later on
    return True


def load_config_from_management(mgm_details: ManagementController, is_virtual_asa: bool) -> str:
    """Load ASA configuration from the management device using SSH.
    
    Args:
        mgm_details: ManagementController object with connection details.
        is_virtual_asa: Boolean indicating if the device is a virtual ASA inside of a FirePower instance.

    Returns:
        The raw configuration as a string.
    """
    logger = getFwoLogger()
    try:
        device = {
            "host": mgm_details.Hostname,
            "port": mgm_details.Port,
            "auth_username": mgm_details.ImportUser,
            "auth_password": mgm_details.Secret,
            "auth_strict_key": False,
            "transport_options": {"open_cmd": ["-o", "KexAlgorithms=+diffie-hellman-group14-sha1"]},
        }

        conn = GenericDriver(**device)
        conn.open()

        if is_virtual_asa:
            conn.send_command("connect module 1 console\n")
            time.sleep(2)
            conn.send_command("\n")
            time.sleep(2)

        if conn.get_prompt().endswith(">"):
            conn.send_interactive(
                [
                    ("enable", "Password", False),
                    (mgm_details.CloudClientSecret, "#", True)
                ]
            )

        if conn.get_prompt().endswith("#"):
            try:
                conn.send_command("terminal pager 0")
            except Exception as e:
                logger.warning(f"Could not disable paging: {e}")

        response = conn.send_interactive(
            [
                ("show running", ": end", False)
            ],
            timeout_ops=600
        )

        try:
            conn.send_command("exit")
        except Exception as e:
            logger.warning(f"Could not exit session cleanly: {e}")

        conn.close()
        return response.result.strip()
    except Exception as e:
        logger.error(f"Error connecting to device {mgm_details.Hostname}: {e}")
        return ""


def get_config(config_in: FwConfigManagerListController, importState: ImportStateController) -> tuple[int, FwConfigManagerList]:
    """
    Retrieve and parse the ASA configuration.

    Args:
        config_in: Configuration input details.
        importState: Current import state.
    
    Returns:
        A tuple containing the status code and the parsed configuration.
    """
    logger = getFwoLogger()

    logger.debug ( "starting checkpointAsa9/get_config" )

    is_virtual_asa = importState.MgmDetails.DeviceTypeName == "Cisco Asa on FirePower"

    if config_in.native_config_is_empty:
        # raw_config = load_config_from_management(importState.MgmDetails, is_virtual_asa)
        raw_config = load_config_from_file("asa.conf")
        config2import = parse_asa_config(raw_config)
        config_in.native_config = config2import.model_dump()

    normalize_config(config_in, importState)

    return 0, config_in


def load_config_from_file(filename: str) -> str:
    """Load ASA configuration from a file."""
    path = Path("roles", "importer", "files", "importer", "ciscoasa9", filename)
    with open(path, "r") as f:
        return f.read()




def normalize_config(config_in: FwConfigManagerListController, importState: ImportStateController):

    # Network Objects Normalization
    
    native_config = Config.model_validate(config_in.native_config)
    network_objects = []
    for object in native_config.objects:
        if isinstance(object, AsaNetworkObject):
            if object.ip_address is not None and object.subnet_mask is not None:
                ip_end = IPNetwork(f"{object.ip_address}/{object.subnet_mask}").broadcast
                ip_end = str(ip_end)
                ip_end = IPNetwork(f"{ip_end}/{32}") if ip_end else None
                obj = NetworkObject(
                    obj_uid=object.name,
                    obj_name=object.name,
                    obj_typ="network",
                    obj_ip=IPNetwork(object.ip_address),
                    obj_ip_end = ip_end,
                    obj_color=fwo_const.defaultColor,
                    obj_comment=object.description
                )
                network_objects.append(obj)

            elif object.ip_address is not None and object.subnet_mask is None:
                obj = NetworkObject(
                    obj_uid=object.name,
                    obj_name=object.name,
                    obj_typ="host",
                    obj_ip=IPNetwork(f"{object.ip_address}/32"),
                    obj_ip_end=IPNetwork(f"{object.ip_address}/32"),
                    obj_color=fwo_const.defaultColor,
                    obj_comment=object.description
                )
                network_objects.append(obj)

            # TODO ip range
                
            
    # network object groups
    for object_group in native_config.object_groups:
        if isinstance(object_group, AsaNetworkObjectGroup):
            obj = NetworkObject(
                obj_uid=object_group.name,
                obj_name=object_group.name,
                obj_typ="group",
                obj_member_names="|".join(object_group.objects),
                obj_member_refs=fwo_const.list_delimiter.join(object_group.objects),
                obj_color=fwo_const.defaultColor,
                obj_comment=object_group.description
            )
            network_objects.append(obj)

    network_objects = FwConfigNormalizedController.convertListToDict(list(nwobj.model_dump() for nwobj in network_objects), 'obj_uid')
    

    service_objects = {}
    for serviceObject in native_config.service_objects:
        if serviceObject.dst_port_eq is not None and len(serviceObject.dst_port_eq) > 0:
            obj = ServiceObject(
                svc_uid=serviceObject.name,
                svc_name=serviceObject.name,
                svc_port=int(serviceObject.dst_port_eq) if serviceObject.dst_port_eq.isdigit() else name_to_port[serviceObject.dst_port_eq]["port"],
                svc_port_end=int(serviceObject.dst_port_eq) if serviceObject.dst_port_eq.isdigit() else name_to_port[serviceObject.dst_port_eq]["port"],
                svc_color=fwo_const.defaultColor,
                svc_typ="simple",
                ip_proto=protocol_map.get(serviceObject.protocol, 0),
                svc_comment=serviceObject.description if serviceObject.dst_port_eq.isdigit() else name_to_port[serviceObject.dst_port_eq]["description"]
            )
            service_objects[serviceObject.name] = obj
        elif serviceObject.dst_port_range is not None and len(serviceObject.dst_port_range) == 2:
            obj = ServiceObject(
                svc_uid=serviceObject.name,
                svc_name=serviceObject.name,
                svc_port=int(serviceObject.dst_port_range[0]),
                svc_port_end=int(serviceObject.dst_port_range[1]),
                svc_color=fwo_const.defaultColor,
                svc_typ="simple",
                ip_proto=protocol_map.get(serviceObject.protocol, 0),
                svc_comment=serviceObject.description
            )
            service_objects[serviceObject.name] = obj

        elif serviceObject.dst_port_eq is not None and len(serviceObject.dst_port_eq) == 0 and (serviceObject.dst_port_range is None or len(serviceObject.dst_port_range) == 0):
            obj = ServiceObject(
                svc_uid=serviceObject.name,
                svc_name=serviceObject.name,
                svc_color=fwo_const.defaultColor,
                svc_typ="simple",
                ip_proto=protocol_map.get(serviceObject.protocol, 0),
                svc_comment=serviceObject.description
            )
            service_objects[serviceObject.name] = obj
    
    

    # create service objs for all ports
    for pr in ("tcp", "udp", "icmp", "ip"):
        obj_name = f"any-{pr}" 
        obj = ServiceObject(
            svc_uid=obj_name,
            svc_name=obj_name,
            svc_port=0,
            svc_port_end=65535,
            svc_color=fwo_const.defaultColor,
            svc_typ="simple",
            ip_proto=protocol_map.get(pr, 0),
            svc_comment="any " + pr
        )
        service_objects[obj_name] = obj

    # service object groups
    for serviceObjectGroup in native_config.service_object_groups:
        obj_names = []
        for protocol in serviceObjectGroup.proto_mode.split("-"):
            if protocol not in protocol_map and protocol != "service":
                raise ValueError(f"Unknown protocol in service object group: {protocol}")
            
            if protocol == "service":
                for obj in serviceObjectGroup.nested_groups:
                    obj_names.append(obj)
                for pr in serviceObjectGroup.protocols:
                    obj_name = f"any-{pr}"
                    obj_names.append(obj_name)
                continue
            
            for pr in serviceObjectGroup.ports_range:
                obj_name = f"{pr[0]}-{pr[1]}-{protocol}" if pr[0] != pr[1] else f"{pr[0]}-{protocol}"
                obj_names.append(obj_name)
                if obj_name in service_objects.keys():
                    continue
                obj = ServiceObject(
                    svc_uid=obj_name,
                    svc_name=obj_name,
                    svc_port=int(pr[0]),
                    svc_port_end=int(pr[1]),
                    svc_color=fwo_const.defaultColor,
                    svc_typ="simple",
                    ip_proto=protocol_map.get(protocol, 0),
                    svc_comment=serviceObjectGroup.description
                )
                service_objects[obj_name] = obj

        obj = ServiceObject(
            svc_uid=serviceObjectGroup.name,
            svc_name=serviceObjectGroup.name,
            svc_typ="group",
            svc_member_names=fwo_const.list_delimiter.join(obj_names),
            svc_member_refs=fwo_const.list_delimiter.join(obj_names),
            svc_color=fwo_const.defaultColor,
            svc_comment=serviceObjectGroup.description
        )
        service_objects[serviceObjectGroup.name] = obj


    for access_lists in native_config.access_lists:
        cnt = 1
        for entry in access_lists.entries:
            rule_name = f"{access_lists.name}-{cnt:04d}"
            src_ref = entry.src.value
            dst_ref = entry.dst.value
            if entry.protocol == "ip":
                if entry.src.value not in network_objects.keys():
                    obj = NetworkObject(
                        obj_uid=entry.src.value,
                        obj_name=entry.src.value,
                        obj_typ="host",
                        obj_ip=IPNetwork(f"{entry.src.value}/32"),
                        obj_ip_end=IPNetwork(f"{entry.src.value}/32"),
                        obj_color=fwo_const.defaultColor,
                        obj_comment=entry.description

                    )
                    network_objects[entry.src.value] = obj
                
                if entry.dst.value not in network_objects.keys():
                    obj = NetworkObject(
                        obj_uid=entry.dst.value,
                        obj_name=entry.dst.value,
                        obj_typ="host",
                        obj_ip=IPNetwork(f"{entry.dst.value}/32"),
                        obj_ip_end=IPNetwork(f"{entry.dst.value}/32"),
                        obj_color=fwo_const.defaultColor,
                        obj_comment=entry.description
                    )
                    network_objects[entry.dst.value] = obj

            elif entry.protocol in ("tcp", "udp", "icmp"):
                if entry.src.kind in ("object", "object-group"):
                    if entry.src.value not in network_objects.keys():
                        obj = NetworkObject(
                            obj_uid=entry.src.value,
                            obj_name=entry.src.value,
                            obj_typ="group" if entry.src.kind == "object-group" else "network",
                            obj_color=fwo_const.defaultColor,
                            obj_comment=entry.description
                        )
                        network_objects[entry.src.value] = obj
                else:
                    raise ValueError(f"Unknown source kind in access-list entry: {entry.src.kind}")
        
            else:
                raise ValueError(f"Unknown protocol in access-list entry: {entry.protocol}")


            # service object for protocol/port
            if entry.protocol in ("tcp", "udp", "icmp"):
                svc_obj_name = f"{entry.dst_port_eq}-{entry.protocol}"
                if svc_obj_name not in service_objects.keys() and entry.dst_port_eq is not None and len(entry.dst_port_eq) > 0:
                    obj = ServiceObject(
                        svc_uid=svc_obj_name,
                        svc_name=svc_obj_name,
                        svc_port=int(entry.dst_port_eq) if entry.dst_port_eq.isdigit() else name_to_port[entry.dst_port_eq]["port"],
                        svc_port_end=int(entry.dst_port_eq) if entry.dst_port_eq.isdigit() else name_to_port[entry.dst_port_eq]["port"],
                        svc_color=fwo_const.defaultColor,
                        svc_typ="simple",
                        ip_proto=protocol_map.get(entry.protocol, 0),
                        svc_comment=entry.description if entry.dst_port_eq.isdigit() else name_to_port[entry.dst_port_eq]["description"]
                    )
                    service_objects[svc_obj_name] = obj
            cnt += 1

    rulebases = build_rulebases_from_access_lists(native_config.access_lists, importState.MgmDetails.Uid)


    gateway1 = Gateway(
        Uid=native_config.hostname,
        Name=native_config.hostname,
        Routing=[],
        RulebaseLinks=[
            RulebaseLinkUidBased(to_rulebase_uid="acl1", link_type="ordered", is_initial=True, is_global=False, is_section=False),
            RulebaseLinkUidBased(from_rule_uid="acl1", to_rulebase_uid="acl2", link_type="ordered", is_initial=False, is_global=False, is_section=False)
            ],
        GlobalPolicyUid=None,
        EnforcedPolicyUids=[rb.uid for rb in rulebases],
        EnforcedNatPolicyUids=[],
        ImportDisabled=False,
        ShowInUI=True
    )



    normalized_config = FwConfigNormalized(
        action=ConfigAction.INSERT, 
        network_objects=network_objects,
        service_objects=service_objects,
        zone_objects={},
        rulebases=rulebases,
        gateways=[gateway1],
    )

    config_in.ManagerSet[0].Configs = [normalized_config]
    config_in.ManagerSet[0].ManagerUid = importState.MgmDetails.Uid
    
    return config_in


def build_rulebases_from_access_lists(access_lists: List[AccessList], mgm_uid: str) -> List[Rulebase]:
    rulebases = []

    for access_list in access_lists:
        rules = {}
        for idx, entry in enumerate(access_list.entries, start=1):
            rule_uid = f"{access_list.name}-{idx:04d}"

            svc_ref = ""
            if entry.dst_port_eq:
                svc_ref = f"{entry.dst_port_eq}-{entry.protocol}"
            else:
                svc_ref = f"any-{entry.protocol}"

            rule = RuleNormalized(
                rule_num=idx,
                rule_num_numeric=float(idx),
                rule_disabled=False,
                rule_src_neg=False,
                rule_src=entry.src.value,
                rule_src_refs=entry.src.value,
                rule_dst_neg=False,
                rule_dst=entry.dst.value,
                rule_dst_refs=entry.dst.value,
                rule_svc_neg=False,
                rule_svc=svc_ref,
                rule_svc_refs=svc_ref,
                rule_action=RuleAction.ACCEPT if entry.action == "permit" else RuleAction.DROP,
                rule_track=RuleTrack.NONE,
                rule_installon="",
                rule_time="",
                rule_name=f"{access_list.name}-{idx:04d}",
                rule_uid=rule_uid,
                rule_custom_fields=None,
                rule_implied=False,
                rule_type=RuleType.ACCESS,
                last_change_admin=None,
                parent_rule_uid=None,
                last_hit=None,
                rule_comment=entry.description,
                rule_src_zone=None,
                rule_dst_zone=None,
                rule_head_text=None
            )
            rules[rule_uid] = rule

        rulebase = Rulebase(
            uid=access_list.name,
            name=access_list.name,
            mgm_uid=mgm_uid,  # Replace with actual management UID if available
            is_global=False,
            Rules=rules
        )
        rulebases.append(rulebase)

    return rulebases

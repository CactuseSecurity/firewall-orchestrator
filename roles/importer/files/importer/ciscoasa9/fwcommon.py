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

from typing import List

from netaddr import IPAddress, IPNetwork

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
    AsaNetworkObject, AsaNetworkObjectGroup, AsaProtocolGroup, AsaServiceModule, AsaServiceObject, AsaServiceObjectGroup,\
    ClassMap, Config, DnsInspectParameters, EndpointKind, InspectionAction, Interface, MgmtAccessRule,\
    Names, NatRule, PolicyClass, PolicyMap, Route, ServicePolicyBinding  
from fwo_enums import ConfigAction
from model_controllers.fwconfig_normalized_controller import FwConfigNormalizedController
from model_controllers.management_controller import ManagementController
from models.serviceobject import ServiceObject
from ciscoasa9.asa_parser import parse_asa_config
from ciscoasa9.asa_maps import name_to_port, protocol_map
from models.rulebase import Rulebase
from models.rule import RuleNormalized, RuleAction, RuleTrack, RuleType
from models.gateway import Gateway
from models.rulebase_link import RulebaseLinkUidBased
from fwo_base import write_native_config_to_file
from ciscoasa9.asa_normalize import (
    create_objects_for_access_lists,
    normalize_network_objects,
    normalize_service_objects,
    normalize_service_object_groups
)


def has_config_changed(full_config, mgm_details, force=False):
    full_config = full_config
    mgm_details = mgm_details
    force = force
    # TODO: dummy - may be filled with real check later on
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

    write_native_config_to_file(importState, config_in.native_config)

    normalize_config(config_in, importState)

    return 0, config_in


def load_config_from_file(filename: str) -> str:
    """Load ASA configuration from a file."""
    path = Path("roles", "importer", "files", "importer", "ciscoasa9", filename)
    with open(path, "r") as f:
        return f.read()

def normalize_config(config_in: FwConfigManagerListController, importState: ImportStateController):
    """
    Normalize the ASA configuration into a structured format for the database.
    
    This function orchestrates the normalization process:
    1. Parse the native configuration
    2. Normalize network objects (hosts, networks, groups)
    3. Normalize service objects (ports, protocols, groups)
    4. Create objects for inline ACL definitions
    5. Build rulebases from access lists
    6. Create gateway and rulebase links
    7. Construct the final normalized configuration
    
    Args:
        config_in: Configuration input details containing native config.
        importState: Current import state with management details.
    
    Returns:
        Updated config_in with normalized configuration.
    """
    logger = getFwoLogger()
    
    # Parse the native configuration into structured objects
    native_config: Config = Config.model_validate(config_in.native_config)

    # Step 1: Normalize network objects (names, objects, object-groups)
    logger.debug("Normalizing network objects...")
    network_objects = normalize_network_objects(native_config, logger)

    # Step 2: Normalize service objects (service objects with ports/protocols)
    logger.debug("Normalizing service objects...")
    service_objects = normalize_service_objects(native_config)

    # Step 3: Normalize service object groups (including mixed protocol groups)
    logger.debug("Normalizing service object groups...")
    service_objects = normalize_service_object_groups(native_config, service_objects)

    # Step 4: Create objects for inline definitions in access lists
    logger.debug("Creating objects for access list inline definitions...")
    for access_list in native_config.access_lists:
        create_objects_for_access_lists(access_list, network_objects, service_objects, importState)

    # Step 5: Build rulebases from access lists
    logger.debug("Building rulebases from access lists...")
    rulebases = build_rulebases_from_access_lists(
        native_config.access_lists,
        importState.MgmDetails.Uid,
        protocol_groups=native_config.protocol_groups
    )

    # Step 6: Create rulebase links (ordered chain of rulebases)
    rulebase_links = []
    if len(rulebases) > 0:
        # First rulebase is the initial entry point
        rulebase_links.append(RulebaseLinkUidBased(
            to_rulebase_uid=rulebases[0].uid,
            link_type="ordered",
            is_initial=True,
            is_global=False,
            is_section=False
        ))
        # Link subsequent rulebases in order
        for idx in range(1, len(rulebases)):
            rulebase_links.append(RulebaseLinkUidBased(
                from_rulebase_uid=rulebases[idx-1].uid,
                to_rulebase_uid=rulebases[idx].uid,
                link_type="ordered",
                is_initial=False,
                is_global=False,
                is_section=False
            ))

    # Step 7: Create gateway object representing the ASA device
    logger.debug("Creating gateway object...")
    gateway = Gateway(
        Uid=native_config.hostname,
        Name=native_config.hostname,
        Routing=[],
        RulebaseLinks=rulebase_links,
        GlobalPolicyUid=None,
        EnforcedPolicyUids=[],
        EnforcedNatPolicyUids=[],
        ImportDisabled=False,
        ShowInUI=True
    )

    # Step 8: Construct the normalized configuration
    logger.debug("Constructing normalized configuration...")
    normalized_config = FwConfigNormalized(
        action=ConfigAction.INSERT,
        network_objects=network_objects,
        service_objects=service_objects,
        zone_objects={},  # ASA doesn't use zones like other firewalls
        rulebases=rulebases,
        gateways=[gateway]
    )

    # Update the configuration input with normalized data
    config_in.ManagerSet[0].Configs = [normalized_config]
    config_in.ManagerSet[0].ManagerUid = importState.MgmDetails.Uid

    return config_in


def build_rulebases_from_access_lists(access_lists: List[AccessList], mgm_uid: str, protocol_groups: List[AsaProtocolGroup]) -> List[Rulebase]:
    """
    Build rulebases from ASA access lists.
    
    Each access list becomes a separate rulebase containing normalized rules.
    Rules are created from ACL entries with proper service, source, and destination references.
    
    Args:
        access_lists: List of parsed ASA access lists.
        mgm_uid: Management UID for the device.
        protocol_groups: List of protocol groups for resolving protocol-group references.
    
    Returns:
        List of normalized rulebases.
    """
    rulebases = []

    for access_list in access_lists:
        rules = {}
        for idx, entry in enumerate(access_list.entries, start=1):
            rule_uid = f"{access_list.name}-{idx:04d}"

            # Determine service reference based on protocol and port information
            svc_ref = ""
            if entry.protocol.kind == "protocol" and entry.protocol.value in ("tcp", "udp", "icmp"):
                # Protocol with port specification
                if entry.dst_port.kind == "eq":
                    svc_ref = f"{entry.dst_port.value}-{entry.protocol.value}"
                elif entry.dst_port.kind == "range":
                    ports = entry.dst_port.value.split("-")
                    if len(ports) == 2:
                        svc_ref = f"{ports[0]}-{ports[1]}-{entry.protocol.value}"
                elif entry.dst_port.kind == "any":
                    svc_ref = f"any-{entry.protocol.value}"
                elif entry.dst_port.kind in ("service", "service-group"):
                    svc_ref = entry.dst_port.value
                else:
                    svc_ref = f"any-{entry.protocol.value}"
            elif entry.protocol.kind == "ip":
                # 'ip' means all protocols (tcp, udp, icmp)
                svc_ref = fwo_const.list_delimiter.join([f"any-{p}" for p in ("tcp", "udp", "icmp")])
            elif entry.protocol.kind in ("service-group", "service"):
                # Reference to service object or group
                svc_ref = entry.protocol.value
            elif entry.protocol.kind == "protocol-group":
                # Protocol group - resolve to list of protocols
                svc_ref = ""
                allowed_protocols = []
                for pg in protocol_groups:
                    if pg.name == entry.protocol.value:
                        allowed_protocols = pg.protocols
                        break
                
                for pr in allowed_protocols:
                    svc_ref = f"any-{pr}"
            else:
                # Default to all common protocols
                svc_ref = fwo_const.list_delimiter.join([f"any-{p}" for p in ("tcp", "udp", "icmp")])

            # Determine source reference (convert subnet mask to CIDR if present)
            src_ref = entry.src.value
            if entry.src.mask is not None:
                src_ref = str(IPNetwork(f"{entry.src.value}/{entry.src.mask}"))

            # Determine destination reference (convert subnet mask to CIDR if present)
            dst_ref = entry.dst.value
            if entry.dst.mask is not None:
                dst_ref = str(IPNetwork(f"{entry.dst.value}/{entry.dst.mask}"))

            # Create normalized rule
            rule = RuleNormalized(
                rule_num=idx,
                rule_num_numeric=float(idx),
                rule_disabled=entry.inactive,
                rule_src_neg=False,
                rule_src=src_ref,
                rule_src_refs=src_ref,
                rule_dst_neg=False,
                rule_dst=dst_ref,
                rule_dst_refs=dst_ref,
                rule_svc_neg=False,
                rule_svc=svc_ref,
                rule_svc_refs=svc_ref,
                rule_action=RuleAction.ACCEPT if entry.action == "permit" else RuleAction.REJECT,
                rule_track=RuleTrack.NONE,
                rule_installon="",
                rule_time="",
                rule_name=f"{access_list.name}-{idx:03d}",
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

        # Create rulebase for this access list
        rulebase = Rulebase(
            uid=access_list.name,
            name=access_list.name,
            mgm_uid=mgm_uid,
            is_global=False,
            Rules=rules
        )
        rulebases.append(rulebase)

    return rulebases

#!/usr/bin/env python3
"""ASA Configuration Import Module

This module handles the main configuration import workflow for Cisco ASA devices.
It provides functions to connect to devices, retrieve configurations, and
orchestrate the normalization process.
"""

from pathlib import Path
from scrapli.driver import GenericDriver
import time

from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fwconfigmanagerlist import FwConfigManagerList
from fwo_log import getFwoLogger
from model_controllers.management_controller import ManagementController
from ciscoasa9.asa_parser import parse_asa_config
from fwo_base import write_native_config_to_file
from ciscoasa9.asa_normalize import normalize_config


def has_config_changed(full_config, mgm_details, force=False):
    # We don't get this info from ASA, so we always return True
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
        raise


def get_config(config_in: FwConfigManagerListController, import_state: ImportStateController) -> tuple[int, FwConfigManagerList]:
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

    is_virtual_asa = import_state.MgmDetails.DeviceTypeName == "Cisco Asa on FirePower"

    if config_in.native_config_is_empty:
        # raw_config = load_config_from_management(import_state.MgmDetails, is_virtual_asa)
        raw_config = load_config_from_file("test_asa.conf")
        config2import = parse_asa_config(raw_config)
        config_in.native_config = config2import.model_dump()

    write_native_config_to_file(import_state, config_in.native_config)

    normalize_config(config_in, import_state)

    return 0, config_in


def load_config_from_file(filename: str) -> str:
    """Load ASA configuration from a file."""
    path = Path("roles", "importer", "files", "importer", "ciscoasa9", filename)
    with open(path, "r") as f:
        return f.read()

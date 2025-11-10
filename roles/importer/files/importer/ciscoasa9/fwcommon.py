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


def _is_transient_error(error: Exception) -> bool:
    """Determine if an error is transient and worth retrying.
    
    Args:
        error: The exception that occurred.
        
    Returns:
        True if the error appears to be transient (network, timeout, concurrent access).
    """
    error_str = str(error).lower()
    transient_indicators = [
        "timeout",
        "connection",
        "already in use",
        "concurrent",
        "prompt",
        "temporarily unavailable",
        "resource busy"
    ]
    return any(indicator in error_str for indicator in transient_indicators)


def load_config_from_management(mgm_details: ManagementController, is_virtual_asa: bool, max_retries: int = 8) -> str:
    """Load ASA configuration from the management device using SSH with exponential backoff retry.

    Args:
        mgm_details: ManagementController object with connection details.
        is_virtual_asa: Boolean indicating if the device is a virtual ASA inside of a FirePower instance.
        max_retries: Maximum number of retry attempts (default: 8).

    Returns:
        The raw configuration as a string.
        
    Raises:
        Exception: After all retry attempts are exhausted.
    """
    logger = getFwoLogger()

    logger.debug("Waiting 5 seconds before starting connection...")
    time.sleep(5)
    
    last_exception = None
    
    for attempt in range(max_retries):
        if attempt > 0:
            backoff_time = 2 ** attempt
            logger.info(f"Retry attempt {attempt + 1}/{max_retries} after {backoff_time:.2f} seconds backoff")
            time.sleep(backoff_time)
        else:
            logger.debug(f"Connection attempt {attempt + 1}/{max_retries}")
        
        conn = None
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

            # Check current prompt to determine if we need to enable
            current_prompt = conn.get_prompt()
            logger.debug(f"Current prompt: {current_prompt}")
            
            # If in user mode (>), enter enable mode
            if current_prompt.endswith(">"):
                logger.debug("Device is in user mode, entering enable mode")
                try:
                    conn.send_interactive(
                        [
                            ("enable", "Password", False),
                            (mgm_details.CloudClientSecret, "#", True)
                        ]
                    )
                except Exception as e:
                    logger.warning(f"Could not enter enable mode: {e}")
                    # Check if we're already enabled despite the error
                    current_prompt = conn.get_prompt()
                    if not current_prompt.endswith("#"):
                        raise
            
            # Verify we're in enabled mode before proceeding
            current_prompt = conn.get_prompt()
            if not current_prompt.endswith("#"):
                error_msg = f"Not in enabled mode (prompt: {current_prompt})."
                logger.error(error_msg)
                raise Exception(error_msg)
            
            logger.debug("Device is in enabled mode")
            
            # Disable paging
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
            
            # Success! Log and return
            if attempt > 0:
                logger.info(f"Successfully connected after {attempt + 1} attempt(s)")
            
            return response.result.strip()
            
        except Exception as e:
            last_exception = e
            error_msg = f"Error connecting to device {mgm_details.Hostname} (attempt {attempt + 1}/{max_retries}): {e}"
            
            # Provide more context for common issues
            if "Password" in str(e) or "enable" in str(e).lower():
                error_msg += "\nPossible causes: incorrect password, or device already in use by another import session"
            elif "prompt" in str(e).lower() or "timeout" in str(e).lower():
                error_msg += "\nPossible causes: concurrent access from multiple import processes, or device not responding as expected"
            
            # Try to close connection if it's still open
            try:
                if conn is not None:
                    conn.close()
            except:
                pass
            
            # Check if this is a transient error worth retrying
            is_transient = _is_transient_error(e)
            
            if attempt < max_retries - 1 and is_transient:
                logger.warning(error_msg + "\nThis appears to be a transient error, will retry...")
            else:
                # Last attempt or non-transient error
                if not is_transient:
                    logger.error(error_msg + "\nNon-transient error detected, not retrying.")
                else:
                    logger.error(error_msg + "\nMax retries exhausted.")
                raise Exception(error_msg)
    
    # Should never reach here, but just in case
    if last_exception:
        raise last_exception
    raise Exception(f"Failed to connect to device {mgm_details.Hostname} after {max_retries} attempts")


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
        raw_config = load_config_from_management(import_state.MgmDetails, is_virtual_asa)
        # raw_config = load_config_from_file("test_asa.conf")
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

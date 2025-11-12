#!/usr/bin/env python3
"""ASA Configuration Import Module

This module handles the main configuration import workflow for Cisco ASA devices.
It provides functions to connect to devices, retrieve configurations, and
orchestrate the normalization process.
"""

from pathlib import Path
from typing import Optional
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
from fwo_exceptions import FwoImporterError


def has_config_changed(full_config, mgm_details, force=False):
    # We don't get this info from ASA, so we always return True
    return True


def _connect_to_device(mgm_details: ManagementController) -> GenericDriver:
    """Establish SSH connection to the device.
    
    Args:
        mgm_details: ManagementController object with connection details.
        
    Returns:
        Connected GenericDriver instance.
    """
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
    return conn


def _prepare_virtual_asa(conn: GenericDriver) -> None:
    """Connect to ASA module on virtual device.
    
    Args:
        conn: Active connection to the device.
    """
    conn.send_command("connect module 1 console\n")
    time.sleep(2)
    conn.send_command("\n")
    time.sleep(2)


def _get_current_prompt(conn: GenericDriver) -> str:
    """Get the current prompt from the device.
    
    Args:
        conn: Active connection to the device.

    Returns:
        Current prompt as string.
    """
    try:
        return conn.get_prompt().strip()
    except Exception:
        logger = getFwoLogger()
        logger.warning("Could not get current prompt")
        return ""


def _ensure_enable_mode(conn: GenericDriver, mgm_details: ManagementController) -> None:
    """Ensure device is in enabled mode.
    
    Args:
        conn: Active connection to the device.
        mgm_details: ManagementController object with enable password.
        
    Raises:
        FwoImporterError: If unable to enter enabled mode.
    """
    logger = getFwoLogger()
    current_prompt = _get_current_prompt(conn)
    logger.debug(f"Current prompt: {current_prompt}")
    
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
            current_prompt = _get_current_prompt(conn)
            if current_prompt == "":  
                error_msg = "Could not retrieve prompt after attempting to enter enable mode."  
                logger.error(error_msg)
                raise FwoImporterError(error_msg) from e
            elif not current_prompt.endswith("#"):
                raise FwoImporterError("Failed to enter enable mode.")


    current_prompt = _get_current_prompt(conn)
    if not current_prompt.endswith("#"):
        error_msg = f"Not in enabled mode (prompt: {current_prompt})."
        logger.error(error_msg)
        raise FwoImporterError(error_msg)
    
    logger.debug("Device is in enabled mode")


def _get_running_config(conn: GenericDriver) -> str:
    """Retrieve running configuration from device.
    
    Args:
        conn: Active connection to the device.
        
    Returns:
        Running configuration as string.
    """
    logger = getFwoLogger()
    
    try:
        conn.send_command("terminal pager 0")
    except Exception as e:
        logger.warning(f"Could not disable paging: {e}")
    
    response = conn.send_interactive(
        [("show running", ": end", False)],
        timeout_ops=600
    )
    return response.result.strip()


def _safe_close_connection(conn: Optional[GenericDriver]) -> None:
    """Safely close connection with proper cleanup.
    
    Args:
        conn: Connection to close (can be None).
    """
    logger = getFwoLogger()
    if conn is None:
        return
    
    if not conn.isalive():
        logger.debug("Connection already closed")
        return
    
    try:
        conn.send_command("exit")
    except Exception as e:
        logger.warning(f"Could not exit session cleanly: {e}")
    
    try:
        conn.close()
    except Exception as e:
        logger.warning(f"Error closing connection: {e}")


def _handle_connection_error(e: Exception, mgm_details: ManagementController, attempt: int, max_retries: int) -> str:
    """Build detailed error message for connection failures.
    
    Args:
        e: The exception that occurred.
        mgm_details: Management details for context.
        attempt: Current attempt number.
        max_retries: Maximum retry attempts.
        
    Returns:
        Formatted error message.
    """
    error_msg = f"Error connecting to device {mgm_details.Hostname} (attempt {attempt + 1}/{max_retries}): {e}"
    
    error_str = str(e).lower()
    if "password" in error_str or "enable" in error_str:
        error_msg += "\nPossible causes: incorrect password, or device already in use by another import session"
    elif "prompt" in error_str or "timeout" in error_str:
        error_msg += "\nPossible causes: concurrent access from multiple import processes, or device not responding as expected"
    
    return error_msg


def _log_retry_attempt(attempt: int, max_retries: int) -> None:
    """Log retry attempt with exponential backoff.
    
    Args:
        attempt: Current attempt number (0-indexed).
        max_retries: Maximum number of retries.
    """
    logger = getFwoLogger()
    
    if attempt > 0:
        backoff_time = 2 ** (attempt + 1)
        logger.info(f"Retry attempt {attempt + 1}/{max_retries} after {backoff_time} seconds backoff")
        time.sleep(backoff_time)
    else:
        logger.debug(f"Connection attempt {attempt + 1}/{max_retries}")


def _retrieve_config_from_device(conn: GenericDriver, mgm_details: ManagementController, is_virtual_asa: bool) -> str:
    """Retrieve configuration from connected device.
    
    Args:
        conn: Active connection to the device.
        mgm_details: ManagementController object with connection details.
        is_virtual_asa: Whether this is a virtual ASA device.
        
    Returns:
        Running configuration as string.
    """
    if is_virtual_asa:
        _prepare_virtual_asa(conn)
    
    _ensure_enable_mode(conn, mgm_details)
    return _get_running_config(conn)    


def _attempt_connection(mgm_details: ManagementController, is_virtual_asa: bool, attempt: int, max_retries: int) -> str:
    """Attempt a single connection to retrieve configuration.
    
    Args:
        mgm_details: ManagementController object with connection details.
        is_virtual_asa: Whether this is a virtual ASA device.
        attempt: Current attempt number.
        max_retries: Maximum retry attempts.
        
    Returns:
        Running configuration as string.
        
    Raises:
        FwoImporterError: If connection fails and should not retry.
    """
    logger = getFwoLogger()
    conn = None
    
    try:
        conn = _connect_to_device(mgm_details)
        config = _retrieve_config_from_device(conn, mgm_details, is_virtual_asa)
        _safe_close_connection(conn)
        
        if attempt > 0:
            logger.info(f"Successfully connected after {attempt + 1} attempt(s)")
        
        return config
        
    except Exception as e:
        _safe_close_connection(conn)
        error_msg = _handle_connection_error(e, mgm_details, attempt, max_retries)
        
        if attempt < max_retries - 1:
            logger.warning(error_msg + "\nWill retry...")
        else:
            logger.error(error_msg + "\nMax retries exhausted.")
        raise FwoImporterError(error_msg) from e


def load_config_from_management(mgm_details: ManagementController, is_virtual_asa: bool, max_retries: int = 10) -> str:
    """Load ASA configuration from the management device using SSH with exponential backoff retry.

    Args:
        mgm_details: ManagementController object with connection details.
        is_virtual_asa: Boolean indicating if the device is a virtual ASA inside of a FirePower instance.
        max_retries: Maximum number of retry attempts (default: 10).

    Returns:
        The raw configuration as a string.
        
    Raises:
        FwoImporterError: After all retry attempts are exhausted.
    """    
    for attempt in range(max_retries):
        _log_retry_attempt(attempt, max_retries)
        
        try:
            return _attempt_connection(mgm_details, is_virtual_asa, attempt, max_retries)
        except FwoImporterError as e:
            if attempt >= max_retries - 1:
                raise
    raise FwoImporterError(f"Failed to connect to device {mgm_details.Hostname} after {max_retries} attempts")


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

    _ = import_state.MgmDetails.DeviceTypeName == "Cisco Asa on FirePower"

    if config_in.native_config_is_empty: # type: ignore
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

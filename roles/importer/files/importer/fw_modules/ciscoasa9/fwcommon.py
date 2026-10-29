#!/usr/bin/env python3
"""
ASA Configuration Import Module

This module handles the main configuration import workflow for Cisco ASA devices.
It provides functions to connect to devices, retrieve configurations, and
orchestrate the normalization process.
"""

from __future__ import annotations

import time
from pathlib import Path
from typing import TYPE_CHECKING, Any

import fwo_globals
from fw_modules.ciscoasa9.asa_normalize import normalize_config
from fw_modules.ciscoasa9.asa_parser import parse_asa_config
from fwo_base import write_native_config_to_file
from fwo_const import ALERT_CODE_SSH_HOST_KEY_REJECTED, SSH_KNOWN_HOSTS_FILE
from fwo_exceptions import FwoImporterError, SshHostKeyRejectedError
from fwo_log import FWOLogger
from models.fw_common import FwCommon
from scrapli.driver import GenericDriver
from scrapli.exceptions import ScrapliAuthenticationFailed

if TYPE_CHECKING:
    from collections.abc import Callable

    from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
    from model_controllers.import_state_controller import ImportStateController
    from model_controllers.management_controller import ManagementController


class CiscoAsa9Common(FwCommon):
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportStateController
    ) -> tuple[int, FwConfigManagerListController]:
        return get_config(config_in, import_state)


# managements whose unchecked host key was already reported, so the warning is written once per importer process
_unchecked_host_key_mgm_ids_warned: set[int] = set()


def _host_key_options(mgm_details: ManagementController) -> dict[str, Any]:
    """
    Return the scrapli options for checking the SSH host key of the device.

    The host key is checked against SSH_KNOWN_HOSTS_FILE unless certificate checking of firewall connections is
    switched off. Without the check any host is accepted, and the import credentials go to whoever answers.

    Raises:
        SshHostKeyRejectedError: if the host key is to be checked but SSH_KNOWN_HOSTS_FILE does not exist. scrapli
            would otherwise fall back to other known hosts files, whose keys FWO does not manage.

    """
    if fwo_globals.verify_certs is False:
        if mgm_details.mgm_id not in _unchecked_host_key_mgm_ids_warned:
            _unchecked_host_key_mgm_ids_warned.add(mgm_details.mgm_id)
            FWOLogger.warning(
                f"SSH host key checking is switched off for management {mgm_details.name} ({mgm_details.hostname}): "
                "any host key is accepted, so the import credentials can be intercepted. Add the verified host key "
                f'to {SSH_KNOWN_HOSTS_FILE} and switch on "check certificates of firewall connections".'
            )
        return {"auth_strict_key": False}
    if not Path(SSH_KNOWN_HOSTS_FILE).is_file():
        raise SshHostKeyRejectedError(
            f"SSH known hosts file {SSH_KNOWN_HOSTS_FILE} is missing on the importer host, so the host key of "
            f"{mgm_details.hostname} cannot be checked and the import credentials were not sent. Run the installer "
            "to recreate it and add the verified host keys, see documentation/certificates.md."
        )
    return {"auth_strict_key": True, "ssh_known_hosts_file": SSH_KNOWN_HOSTS_FILE}


def _connect_to_device(mgm_details: ManagementController) -> GenericDriver:
    """
    Establish SSH connection to the device.

    Args:
        mgm_details: ManagementController object with connection details.

    Returns:
        Connected GenericDriver instance.

    """
    device: dict[str, Any] = {
        "host": mgm_details.hostname,
        "port": mgm_details.port,
        "auth_username": mgm_details.import_user,
        "auth_password": mgm_details.secret,
        **_host_key_options(mgm_details),
        "transport_options": {"open_cmd": ["-o", "KexAlgorithms=+diffie-hellman-group14-sha1"]},
    }
    conn = GenericDriver(**device)
    try:
        conn.open()
    except ScrapliAuthenticationFailed as e:
        # ssh stops before the authentication, so no credential has been sent to the unverified host
        if "host key verification failed" in str(e).lower():
            raise SshHostKeyRejectedError(
                f"SSH host key of {mgm_details.hostname}:{mgm_details.port} is unknown or has changed, the import "
                f"credentials were not sent. Add the verified host key to {SSH_KNOWN_HOSTS_FILE} on the importer "
                "host, see documentation/certificates.md."
            ) from None
        raise
    return conn


def _prepare_virtual_asa(conn: GenericDriver) -> None:
    """
    Connect to ASA module on virtual device.

    Args:
        conn: Active connection to the device.

    """
    conn.send_command("connect module 1 console\n")
    time.sleep(2)
    conn.send_command("\n")
    time.sleep(2)


def _get_current_prompt(conn: GenericDriver) -> str:
    """
    Get the current prompt from the device.

    Args:
        conn: Active connection to the device.

    Returns:
        Current prompt as string.

    """
    try:
        return conn.get_prompt().strip()
    except Exception:
        FWOLogger.warning("Could not get current prompt")
        return ""


def _ensure_enable_mode(conn: GenericDriver, mgm_details: ManagementController) -> None:
    """
    Ensure device is in enabled mode.

    Args:
        conn: Active connection to the device.
        mgm_details: ManagementController object with enable password.

    Raises:
        FwoImporterError: If unable to enter enabled mode.

    """
    current_prompt = _get_current_prompt(conn)
    FWOLogger.debug(f"Current prompt: {current_prompt}")

    if current_prompt.endswith(">"):
        FWOLogger.debug("Device is in user mode, entering enable mode")
        try:
            enable_secret = mgm_details.additional_credential.secret if mgm_details.additional_credential else ""
            conn.send_interactive([("enable", "Password", False), (enable_secret, "#", True)])
        except Exception as e:
            FWOLogger.warning(f"Could not enter enable mode: {e}")
            current_prompt = _get_current_prompt(conn)
            if current_prompt == "":
                error_msg = "Could not retrieve prompt after attempting to enter enable mode."
                FWOLogger.error(error_msg)
                raise FwoImporterError(error_msg) from e
            if not current_prompt.endswith("#"):
                raise FwoImporterError("Failed to enter enable mode.")

    current_prompt = _get_current_prompt(conn)
    if not current_prompt.endswith("#"):
        error_msg = f"Not in enabled mode (prompt: {current_prompt})."
        FWOLogger.error(error_msg)
        raise FwoImporterError(error_msg)

    FWOLogger.debug("Device is in enabled mode")


def _get_running_config(conn: GenericDriver) -> str:
    """
    Retrieve running configuration from device.

    Args:
        conn: Active connection to the device.

    Returns:
        Running configuration as string.

    """
    try:
        conn.send_command("terminal pager 0")
    except Exception as e:
        FWOLogger.warning(f"Could not disable paging: {e}")

    response = conn.send_interactive([("show running", ": end", False)], timeout_ops=600)
    return response.result.strip()


def _safe_close_connection(conn: GenericDriver | None) -> None:
    """
    Safely close connection with proper cleanup.

    Args:
        conn: Connection to close (can be None).

    """
    if conn is None:
        return

    if not conn.isalive():
        FWOLogger.debug("Connection already closed")
        return

    try:
        conn.send_command("exit")
    except Exception as e:
        FWOLogger.warning(f"Could not exit session cleanly: {e}")

    try:
        conn.close()
    except Exception as e:
        FWOLogger.warning(f"Error closing connection: {e}")


def _handle_connection_error(e: Exception, mgm_details: ManagementController, attempt: int, max_retries: int) -> str:
    """
    Build detailed error message for connection failures.

    Args:
        e: The exception that occurred.
        mgm_details: Management details for context.
        attempt: Current attempt number.
        max_retries: Maximum retry attempts.

    Returns:
        Formatted error message.

    """
    error_msg = f"Error connecting to device {mgm_details.hostname} (attempt {attempt + 1}/{max_retries}): {e}"

    error_str = str(e).lower()
    if "password" in error_str or "enable" in error_str:
        error_msg += "\nPossible causes: incorrect password, or device already in use by another import session"
    elif "prompt" in error_str or "timeout" in error_str:
        error_msg += (
            "\nPossible causes: concurrent access from multiple import processes, or device not responding as expected"
        )

    return error_msg


def _log_retry_attempt(attempt: int, max_retries: int) -> None:
    """
    Log retry attempt with exponential backoff.

    Args:
        attempt: Current attempt number (0-indexed).
        max_retries: Maximum number of retries.

    """
    if attempt > 0:
        backoff_time = 2 ** (attempt + 1)
        FWOLogger.info(f"Retry attempt {attempt + 1}/{max_retries} after {backoff_time} seconds backoff")
        time.sleep(backoff_time)
    else:
        FWOLogger.debug(f"Connection attempt {attempt + 1}/{max_retries}")


def _retrieve_config_from_device(conn: GenericDriver, mgm_details: ManagementController, is_virtual_asa: bool) -> str:
    """
    Retrieve configuration from connected device.

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
    """
    Attempt a single connection to retrieve configuration.

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
    conn = None

    try:
        conn = _connect_to_device(mgm_details)
        config = _retrieve_config_from_device(conn, mgm_details, is_virtual_asa)
        _safe_close_connection(conn)

        FWOLogger.debug(f"Successfully connected after {attempt + 1} attempt(s)")

        return config

    except SshHostKeyRejectedError:
        _safe_close_connection(conn)
        raise
    except Exception as e:
        _safe_close_connection(conn)
        error_msg = _handle_connection_error(e, mgm_details, attempt, max_retries)

        if attempt < max_retries - 1:
            FWOLogger.warning(error_msg + "\nWill retry...")
        else:
            FWOLogger.error(error_msg + "\nMax retries exhausted.")
        raise FwoImporterError(error_msg) from e


def load_config_from_management(mgm_details: ManagementController, is_virtual_asa: bool, max_retries: int = 10) -> str:
    """
    Load ASA configuration from the management device using SSH with exponential backoff retry.

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
        except SshHostKeyRejectedError:
            FWOLogger.error(f"not retrying the connection to {mgm_details.hostname}: its SSH host key was rejected")
            raise
        except FwoImporterError as _:
            if attempt >= max_retries - 1:
                raise
    raise FwoImporterError(f"Failed to connect to device {mgm_details.hostname} after {max_retries} attempts")


def get_config(
    config_in: FwConfigManagerListController, import_state: ImportStateController
) -> tuple[int, FwConfigManagerListController]:
    """
    Retrieve and parse the ASA configuration.

    Args:
        config_in: Configuration input details.
        importState: Current import state.

    Returns:
        A tuple containing the status code and the parsed configuration.

    """
    FWOLogger.debug("starting ciscoAsa9/get_config")

    is_virtual_asa = import_state.state.mgm_details.device_type_name == "Cisco Asa on FirePower"

    if config_in.native_config_is_empty():
        # for debugging, use: raw_config = load_config_from_file("test_asa.conf")
        raw_config = _load_config_reporting_host_key(import_state, is_virtual_asa)
        config2import = parse_asa_config(raw_config)
        config_in.native_config = config2import.model_dump()

    write_native_config_to_file(import_state.state, config_in.native_config)

    normalize_config(config_in, import_state.state)

    return 0, config_in


def _load_config_reporting_host_key(import_state: ImportStateController, is_virtual_asa: bool) -> str:
    """
    Load the configuration from the device and keep the SSH host key alert of the management up to date.

    A rejected host key raises an alert, which the UI shows to admins together with the instructions. The alert is
    acknowledged by the next successful connection, so it is open exactly as long as the problem exists.
    """
    mgm_details = import_state.state.mgm_details
    try:
        raw_config = load_config_from_management(mgm_details, is_virtual_asa)
    except SshHostKeyRejectedError as rejection:
        rejection_text = str(rejection)
        _report_alert_change(
            lambda: import_state.api_call.set_alert(
                import_id=import_state.state.import_id,
                title="SSH host key rejected",
                mgm_id=mgm_details.mgm_id,
                severity=1,
                description=rejection_text,
                source="import",
                alert_code=ALERT_CODE_SSH_HOST_KEY_REJECTED,
                mgm_details=mgm_details,
            )
        )
        raise
    _report_alert_change(
        lambda: import_state.api_call.acknowledge_open_alerts(mgm_details.mgm_id, ALERT_CODE_SSH_HOST_KEY_REJECTED)
    )
    return raw_config


def _report_alert_change(change: Callable[[], None]) -> None:
    """Apply an alert change without letting a failure of the alert API hide the import result."""
    try:
        change()
    except Exception as e:
        FWOLogger.warning(f"could not update the SSH host key alert: {e!s}")


def load_config_from_file(filename: str) -> str:
    """Load ASA configuration from a file."""
    path = Path("roles", "importer", "files", "importer", "fw_modules", "ciscoasa9", filename)
    with open(path) as f:
        return f.read()

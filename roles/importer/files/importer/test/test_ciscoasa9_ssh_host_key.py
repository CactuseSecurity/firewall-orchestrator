from __future__ import annotations

import logging
from typing import TYPE_CHECKING
from unittest.mock import MagicMock

import fwo_globals
import pytest
from fw_modules.ciscoasa9 import fwcommon
from fwo_const import ALERT_CODE_SSH_HOST_KEY_REJECTED
from fwo_exceptions import SshHostKeyRejectedError
from scrapli.exceptions import ScrapliAuthenticationFailed

if TYPE_CHECKING:
    from pathlib import Path

    from model_controllers.management_controller import ManagementController
    from pytest_mock import MockerFixture

HOST_KEY_FAILURE = "Host key verification failed"
ASA_SECRET = "asa-import-secret-marker"  # noqa: S105


@pytest.fixture
def known_hosts_file(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> str:
    """Point the importer at an existing, empty known hosts file in the test directory."""
    known_hosts = tmp_path / "ssh_known_hosts"
    known_hosts.touch()
    monkeypatch.setattr(fwcommon, "SSH_KNOWN_HOSTS_FILE", str(known_hosts))
    return str(known_hosts)


@pytest.fixture
def generic_driver(mocker: MockerFixture, monkeypatch: pytest.MonkeyPatch) -> MagicMock:
    no_warnings_yet: set[int] = set()
    monkeypatch.setattr(fwcommon, "_unchecked_host_key_mgm_ids_warned", no_warnings_yet)
    return mocker.patch.object(fwcommon, "GenericDriver")


@pytest.mark.parametrize("verify_certs", [True, "/etc/ssl/certs/ca-certificates.crt", None])
def test_checks_host_key_against_fwo_known_hosts_file(
    generic_driver: MagicMock,
    known_hosts_file: str,
    management_controller: ManagementController,
    monkeypatch: pytest.MonkeyPatch,
    verify_certs: bool | str | None,
) -> None:
    monkeypatch.setattr(fwo_globals, "verify_certs", verify_certs)

    fwcommon._connect_to_device(management_controller)  # pyright: ignore[reportPrivateUsage]

    options = generic_driver.call_args.kwargs
    assert options["auth_strict_key"] is True
    assert options["ssh_known_hosts_file"] == known_hosts_file
    assert options["transport_options"]["open_cmd"][-2:] == ["-o", "GlobalKnownHostsFile=/dev/null"]


def test_unchecked_firewall_connections_accept_any_host_key_with_one_warning(
    generic_driver: MagicMock,
    management_controller: ManagementController,
    monkeypatch: pytest.MonkeyPatch,
    caplog: pytest.LogCaptureFixture,
) -> None:
    monkeypatch.setattr(fwo_globals, "verify_certs", False)
    caplog.set_level(logging.WARNING)

    fwcommon._connect_to_device(management_controller)  # pyright: ignore[reportPrivateUsage]
    fwcommon._connect_to_device(management_controller)  # pyright: ignore[reportPrivateUsage]

    options = generic_driver.call_args.kwargs
    warnings = [record for record in caplog.records if "SSH host key checking is switched off" in record.getMessage()]
    assert options["auth_strict_key"] is False
    assert "ssh_known_hosts_file" not in options
    assert "GlobalKnownHostsFile=/dev/null" not in options["transport_options"]["open_cmd"]
    assert len(warnings) == 1
    assert fwcommon.SSH_KNOWN_HOSTS_FILE in warnings[0].getMessage()


def test_rejected_host_key_raises_actionable_error_without_credentials(
    generic_driver: MagicMock,
    known_hosts_file: str,
    management_controller: ManagementController,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(fwo_globals, "verify_certs", True)
    management_controller.secret = ASA_SECRET
    generic_driver.return_value.open.side_effect = ScrapliAuthenticationFailed(HOST_KEY_FAILURE)

    with pytest.raises(SshHostKeyRejectedError) as raised:
        fwcommon._connect_to_device(management_controller)  # pyright: ignore[reportPrivateUsage]

    assert known_hosts_file in str(raised.value)
    assert management_controller.hostname in str(raised.value)
    assert ASA_SECRET not in str(raised.value)


@pytest.mark.usefixtures("known_hosts_file")
def test_other_authentication_failures_keep_their_error(
    generic_driver: MagicMock,
    management_controller: ManagementController,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(fwo_globals, "verify_certs", True)
    generic_driver.return_value.open.side_effect = ScrapliAuthenticationFailed("No route to host")

    with pytest.raises(ScrapliAuthenticationFailed, match="No route to host"):
        fwcommon._connect_to_device(management_controller)  # pyright: ignore[reportPrivateUsage]


def test_rejected_host_key_is_not_retried(mocker: MockerFixture, management_controller: ManagementController) -> None:
    connect = mocker.patch.object(fwcommon, "_connect_to_device", side_effect=SshHostKeyRejectedError("rejected"))
    mocker.patch.object(fwcommon, "_log_retry_attempt")

    with pytest.raises(SshHostKeyRejectedError):
        fwcommon.load_config_from_management(management_controller, is_virtual_asa=False, max_retries=5)

    assert connect.call_count == 1


def test_missing_known_hosts_file_fails_without_connecting(
    generic_driver: MagicMock,
    tmp_path: Path,
    management_controller: ManagementController,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    missing_file = str(tmp_path / "missing_known_hosts")
    monkeypatch.setattr(fwcommon, "SSH_KNOWN_HOSTS_FILE", missing_file)
    monkeypatch.setattr(fwo_globals, "verify_certs", True)

    with pytest.raises(SshHostKeyRejectedError) as raised:
        fwcommon._connect_to_device(management_controller)  # pyright: ignore[reportPrivateUsage]

    assert missing_file in str(raised.value)
    generic_driver.assert_not_called()


def import_state_mock(management_controller: ManagementController) -> MagicMock:
    import_state = MagicMock()
    import_state.state.mgm_details = management_controller
    import_state.state.import_id = 77
    return import_state


def test_rejected_host_key_raises_an_alert_for_the_management(
    mocker: MockerFixture, management_controller: ManagementController
) -> None:
    mocker.patch.object(fwcommon, "load_config_from_management", side_effect=SshHostKeyRejectedError("add the key"))
    import_state = import_state_mock(management_controller)

    with pytest.raises(SshHostKeyRejectedError):
        fwcommon._load_config_reporting_host_key(import_state, is_virtual_asa=False)  # pyright: ignore[reportPrivateUsage]

    alert = import_state.api_call.set_alert.call_args.kwargs
    assert alert["alert_code"] == ALERT_CODE_SSH_HOST_KEY_REJECTED
    assert alert["mgm_id"] == management_controller.mgm_id
    assert alert["description"] == "add the key"
    import_state.api_call.acknowledge_open_alerts.assert_not_called()


def test_successful_connection_closes_the_host_key_alert(
    mocker: MockerFixture, management_controller: ManagementController
) -> None:
    mocker.patch.object(fwcommon, "load_config_from_management", return_value="config")
    import_state = import_state_mock(management_controller)

    raw_config = fwcommon._load_config_reporting_host_key(import_state, is_virtual_asa=False)  # pyright: ignore[reportPrivateUsage]

    assert raw_config == "config"
    import_state.api_call.acknowledge_open_alerts.assert_called_once_with(
        management_controller.mgm_id, ALERT_CODE_SSH_HOST_KEY_REJECTED
    )
    import_state.api_call.set_alert.assert_not_called()


def test_failing_alert_api_keeps_the_host_key_error(
    mocker: MockerFixture, management_controller: ManagementController
) -> None:
    mocker.patch.object(fwcommon, "load_config_from_management", side_effect=SshHostKeyRejectedError("add the key"))
    import_state = import_state_mock(management_controller)
    import_state.api_call.set_alert.side_effect = RuntimeError("api down")

    with pytest.raises(SshHostKeyRejectedError, match="add the key"):
        fwcommon._load_config_reporting_host_key(import_state, is_virtual_asa=False)  # pyright: ignore[reportPrivateUsage]

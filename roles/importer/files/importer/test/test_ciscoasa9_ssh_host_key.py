from __future__ import annotations

import logging
from typing import TYPE_CHECKING

import fwo_globals
import pytest
from fw_modules.ciscoasa9 import fwcommon
from fwo_const import SSH_KNOWN_HOSTS_FILE
from fwo_exceptions import SshHostKeyRejectedError
from scrapli.exceptions import ScrapliAuthenticationFailed

if TYPE_CHECKING:
    from unittest.mock import MagicMock

    from model_controllers.management_controller import ManagementController
    from pytest_mock import MockerFixture

HOST_KEY_FAILURE = "Host key verification failed"
ASA_SECRET = "asa-import-secret-marker"  # noqa: S105


@pytest.fixture
def generic_driver(mocker: MockerFixture, monkeypatch: pytest.MonkeyPatch) -> MagicMock:
    no_warnings_yet: set[int] = set()
    monkeypatch.setattr(fwcommon, "_unchecked_host_key_mgm_ids_warned", no_warnings_yet)
    return mocker.patch.object(fwcommon, "GenericDriver")


@pytest.mark.parametrize("verify_certs", [True, "/etc/ssl/certs/ca-certificates.crt", None])
def test_checks_host_key_against_fwo_known_hosts_file(
    generic_driver: MagicMock,
    management_controller: ManagementController,
    monkeypatch: pytest.MonkeyPatch,
    verify_certs: bool | str | None,
) -> None:
    monkeypatch.setattr(fwo_globals, "verify_certs", verify_certs)

    fwcommon._connect_to_device(management_controller)  # pyright: ignore[reportPrivateUsage]

    options = generic_driver.call_args.kwargs
    assert options["auth_strict_key"] is True
    assert options["ssh_known_hosts_file"] == SSH_KNOWN_HOSTS_FILE


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
    assert len(warnings) == 1
    assert SSH_KNOWN_HOSTS_FILE in warnings[0].getMessage()


def test_rejected_host_key_raises_actionable_error_without_credentials(
    generic_driver: MagicMock, management_controller: ManagementController, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(fwo_globals, "verify_certs", True)
    management_controller.secret = ASA_SECRET
    generic_driver.return_value.open.side_effect = ScrapliAuthenticationFailed(HOST_KEY_FAILURE)

    with pytest.raises(SshHostKeyRejectedError) as raised:
        fwcommon._connect_to_device(management_controller)  # pyright: ignore[reportPrivateUsage]

    assert SSH_KNOWN_HOSTS_FILE in str(raised.value)
    assert management_controller.hostname in str(raised.value)
    assert ASA_SECRET not in str(raised.value)


def test_other_authentication_failures_keep_their_error(
    generic_driver: MagicMock, management_controller: ManagementController, monkeypatch: pytest.MonkeyPatch
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

# pyright: reportPrivateUsage=false
from types import SimpleNamespace
from typing import Any
from unittest.mock import MagicMock

import pytest
from fw_modules.ciscoasa9 import fwcommon
from fwo_exceptions import FwoImporterError
from pytest_mock import MockerFixture


def _mgm_details(enable_secret: str | None = "enable-secret") -> Any:  # noqa: S107
    return SimpleNamespace(
        hostname="asa.example.test",
        port=2222,
        import_user="importer",
        secret="ssh-secret",  # noqa: S106
        cloud_client_secret=enable_secret,
    )


def _conn_with_prompts(*prompts: str) -> MagicMock:
    conn = MagicMock()
    conn.get_prompt.side_effect = [SimpleNamespace(result=prompt) for prompt in prompts]
    return conn


def test_connect_to_device_builds_cli_with_default_definition(mocker: MockerFixture):
    cli_cls = mocker.patch.object(fwcommon, "Cli")

    conn = fwcommon._connect_to_device(_mgm_details())

    assert conn is cli_cls.return_value
    args, kwargs = cli_cls.call_args
    assert args == ("asa.example.test",)
    assert kwargs["port"] == 2222
    assert kwargs["definition_file_or_name"] == "default"
    assert kwargs["auth_options"].username == "importer"
    assert kwargs["auth_options"].password == "ssh-secret"  # noqa: S105
    assert kwargs["transport_options"].extra_open_args == ["-o", "KexAlgorithms=+diffie-hellman-group14-sha1"]
    assert kwargs["transport_options"].enable_strict_key is False
    cli_cls.return_value.open.assert_called_once_with()


def test_prepare_virtual_asa_attaches_module_console(mocker: MockerFixture):
    mocker.patch.object(fwcommon.time, "sleep")
    conn = MagicMock()

    fwcommon._prepare_virtual_asa(conn)

    conn.write_and_return.assert_called_once_with("connect module 1 console")
    conn.write_return.assert_called_once_with()


def test_get_current_prompt_strips_result():
    conn = _conn_with_prompts(" asa# \n")

    assert fwcommon._get_current_prompt(conn) == "asa#"


def test_get_current_prompt_returns_empty_string_on_error():
    conn = MagicMock()
    conn.get_prompt.side_effect = RuntimeError("boom")

    assert fwcommon._get_current_prompt(conn) == ""


def test_ensure_enable_mode_skips_enable_when_already_privileged():
    conn = _conn_with_prompts("asa#", "asa#")

    fwcommon._ensure_enable_mode(conn, _mgm_details())

    conn.send_prompted_input.assert_not_called()


def test_ensure_enable_mode_sends_enable_password():
    conn = _conn_with_prompts("asa>", "asa#")

    fwcommon._ensure_enable_mode(conn, _mgm_details())

    conn.send_prompted_input.assert_called_once_with(
        "enable", prompt="Password", prompt_pattern="", response="enable-secret", hidden_response=True
    )


def test_ensure_enable_mode_uses_empty_password_when_unset():
    conn = _conn_with_prompts("asa>", "asa#")

    fwcommon._ensure_enable_mode(conn, _mgm_details(enable_secret=None))

    assert conn.send_prompted_input.call_args.kwargs["response"] == ""


def test_ensure_enable_mode_raises_when_prompt_lost_after_enable_failure():
    conn = MagicMock()
    conn.get_prompt.side_effect = [SimpleNamespace(result="asa>"), RuntimeError("gone")]
    conn.send_prompted_input.side_effect = RuntimeError("timeout")

    with pytest.raises(FwoImporterError, match="Could not retrieve prompt"):
        fwcommon._ensure_enable_mode(conn, _mgm_details())


def test_ensure_enable_mode_raises_when_still_unprivileged_after_enable_failure():
    conn = _conn_with_prompts("asa>", "asa>")
    conn.send_prompted_input.side_effect = RuntimeError("bad password")

    with pytest.raises(FwoImporterError, match="Failed to enter enable mode"):
        fwcommon._ensure_enable_mode(conn, _mgm_details())


def test_ensure_enable_mode_raises_when_not_privileged_at_end():
    conn = _conn_with_prompts("asa>", "asa>")

    with pytest.raises(FwoImporterError, match="Not in enabled mode"):
        fwcommon._ensure_enable_mode(conn, _mgm_details())


def test_get_running_config_disables_pager_and_returns_stripped_result():
    conn = MagicMock()
    conn.send_input.side_effect = [None, SimpleNamespace(result="\nhostname asa\n: end\n")]

    config = fwcommon._get_running_config(conn)

    assert config == "hostname asa\n: end"
    assert conn.send_input.call_args_list[0].args == ("terminal pager 0",)
    assert conn.send_input.call_args_list[1].args == ("show running",)
    assert conn.send_input.call_args_list[1].kwargs == {"operation_timeout_ns": 600 * 1_000_000_000}


def test_get_running_config_continues_when_pager_cannot_be_disabled():
    conn = MagicMock()
    conn.send_input.side_effect = [RuntimeError("no pager"), SimpleNamespace(result=": end")]

    assert fwcommon._get_running_config(conn) == ": end"


def test_safe_close_connection_ignores_missing_connection():
    fwcommon._safe_close_connection(None)


def test_safe_close_connection_skips_unopened_connection():
    conn = MagicMock()
    conn.ptr = None

    fwcommon._safe_close_connection(conn)

    conn.close.assert_not_called()


def test_safe_close_connection_closes_open_connection():
    conn = MagicMock()

    fwcommon._safe_close_connection(conn)

    conn.close.assert_called_once_with()


def test_safe_close_connection_swallows_close_errors():
    conn = MagicMock()
    conn.close.side_effect = RuntimeError("already gone")

    fwcommon._safe_close_connection(conn)

    conn.close.assert_called_once_with()


def test_attempt_connection_returns_config_and_closes(mocker: MockerFixture):
    conn = MagicMock()
    mocker.patch.object(fwcommon, "_connect_to_device", return_value=conn)
    mocker.patch.object(fwcommon, "_retrieve_config_from_device", return_value=": end")

    assert fwcommon._attempt_connection(_mgm_details(), False, 0, 3) == ": end"
    conn.close.assert_called_once_with()


def test_attempt_connection_wraps_errors(mocker: MockerFixture):
    mocker.patch.object(fwcommon, "_connect_to_device", side_effect=RuntimeError("password rejected"))

    with pytest.raises(FwoImporterError, match="incorrect password"):
        fwcommon._attempt_connection(_mgm_details(), False, 2, 3)

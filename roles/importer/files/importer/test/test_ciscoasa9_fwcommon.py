from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest
from fw_modules.ciscoasa9.fwcommon import (
    CiscoAsa9Common,
    _attempt_connection,  # pyright: ignore[reportPrivateUsage]
    _connect_to_device,  # pyright: ignore[reportPrivateUsage]
    _ensure_enable_mode,  # pyright: ignore[reportPrivateUsage]
    _get_current_prompt,  # pyright: ignore[reportPrivateUsage]
    _get_running_config,  # pyright: ignore[reportPrivateUsage]
    _handle_connection_error,  # pyright: ignore[reportPrivateUsage]
    _log_retry_attempt,  # pyright: ignore[reportPrivateUsage]
    _prepare_virtual_asa,  # pyright: ignore[reportPrivateUsage]
    _retrieve_config_from_device,  # pyright: ignore[reportPrivateUsage]
    _safe_close_connection,  # pyright: ignore[reportPrivateUsage]
    get_config,
    load_config_from_file,
    load_config_from_management,
)
from fwo_exceptions import FwoImporterError
from pytest_mock import MockerFixture


def _make_conn(
    *,
    isalive: bool = True,
    prompt: str = "ASA#",
    prompt_raises: Exception | None = None,
    running_config: str = "hostname asa",
) -> MagicMock:
    conn = MagicMock()
    conn.isalive.return_value = isalive
    if prompt_raises:
        conn.get_prompt.side_effect = prompt_raises
    else:
        conn.get_prompt.return_value = f" {prompt} "
    mock_response = MagicMock()
    mock_response.result = f" {running_config} "
    conn.send_interactive.return_value = mock_response
    return conn


class TestHandleConnectionError:
    def _mgm(self) -> MagicMock:
        mgm = MagicMock()
        mgm.hostname = "10.0.0.1"
        return mgm

    def test_password_hint_when_error_mentions_password(self):
        msg = _handle_connection_error(Exception("Bad password"), self._mgm(), 0, 3)
        assert "incorrect password" in msg

    def test_enable_hint_when_error_mentions_enable(self):
        msg = _handle_connection_error(Exception("enable failed"), self._mgm(), 0, 3)
        assert "incorrect password" in msg

    def test_concurrency_hint_when_error_mentions_prompt(self):
        msg = _handle_connection_error(Exception("prompt timeout"), self._mgm(), 0, 3)
        assert "concurrent access" in msg

    def test_concurrency_hint_when_error_mentions_timeout(self):
        msg = _handle_connection_error(Exception("timeout exceeded"), self._mgm(), 0, 3)
        assert "concurrent access" in msg

    def test_no_extra_hint_for_generic_error(self):
        msg = _handle_connection_error(Exception("connection refused"), self._mgm(), 0, 3)
        assert "incorrect password" not in msg
        assert "concurrent access" not in msg

    def test_attempt_is_one_indexed_in_message(self):
        msg = _handle_connection_error(Exception("err"), self._mgm(), 2, 5)
        assert "3/5" in msg

    def test_hostname_included_in_message(self):
        msg = _handle_connection_error(Exception("err"), self._mgm(), 0, 1)
        assert "10.0.0.1" in msg


class TestConnectToDevice:
    @patch("fw_modules.ciscoasa9.fwcommon.GenericDriver")
    def test_opens_connection_with_correct_params(self, mock_driver_cls: MagicMock):
        mock_conn = MagicMock()
        mock_driver_cls.return_value = mock_conn

        mgm = MagicMock()
        mgm.hostname = "10.0.0.1"
        mgm.port = 22
        mgm.import_user = "admin"
        mgm.secret = "pass"  # noqa: S105

        result = _connect_to_device(mgm)

        mock_driver_cls.assert_called_once_with(
            host="10.0.0.1",
            port=22,
            auth_username="admin",
            auth_password="pass",  # noqa: S106
            auth_strict_key=False,
            transport_options={"open_cmd": ["-o", "KexAlgorithms=+diffie-hellman-group14-sha1"]},
        )
        mock_conn.open.assert_called_once()
        assert result is mock_conn


class TestGetCurrentPrompt:
    def test_returns_stripped_prompt_on_success(self):
        conn = _make_conn(prompt="ASA#")
        assert _get_current_prompt(conn) == "ASA#"

    def test_returns_empty_string_on_exception(self):
        conn = _make_conn(prompt_raises=Exception("read error"))
        assert _get_current_prompt(conn) == ""


class TestSafeCloseConnection:
    def test_none_conn_is_noop(self):
        _safe_close_connection(None)

    def test_dead_connection_skips_exit_and_close(self):
        conn = _make_conn(isalive=False)
        _safe_close_connection(conn)
        conn.send_command.assert_not_called()
        conn.close.assert_not_called()

    def test_live_conn_sends_exit_then_closes(self):
        conn = _make_conn(isalive=True)
        _safe_close_connection(conn)
        conn.send_command.assert_called_once_with("exit")
        conn.close.assert_called_once()

    def test_send_command_raises_but_close_still_called(self):
        conn = _make_conn(isalive=True)
        conn.send_command.side_effect = Exception("send failed")
        _safe_close_connection(conn)
        conn.close.assert_called_once()

    def test_close_raises_logs_warning_without_propagating(self):
        conn = _make_conn(isalive=True)
        conn.close.side_effect = Exception("close failed")
        _safe_close_connection(conn)


class TestPrepareVirtualAsa:
    @patch("fw_modules.ciscoasa9.fwcommon.time.sleep")
    def test_sends_module_connect_command(self, _mock_sleep: MagicMock):  # noqa: PT019
        conn = _make_conn()
        _prepare_virtual_asa(conn)
        assert conn.send_command.call_args_list[0].args[0] == "connect module 1 console\n"

    @patch("fw_modules.ciscoasa9.fwcommon.time.sleep")
    def test_sends_newline_after_module_connect(self, _mock_sleep: MagicMock):  # noqa: PT019
        conn = _make_conn()
        _prepare_virtual_asa(conn)
        assert conn.send_command.call_args_list[1].args[0] == "\n"


class TestGetRunningConfig:
    def test_returns_stripped_config(self):
        conn = _make_conn(running_config=" hostname asa ")
        result = _get_running_config(conn)
        assert result == "hostname asa"

    def test_paging_disable_failure_does_not_raise(self):
        conn = _make_conn()
        conn.send_command.side_effect = Exception("pager error")
        mock_response = MagicMock()
        mock_response.result = "hostname asa"
        conn.send_interactive.return_value = mock_response
        result = _get_running_config(conn)
        assert result == "hostname asa"


class TestEnsureEnableMode:
    def _mgm(self, enable_password: str = "dummy-enable-password") -> MagicMock:  # noqa: S107
        mgm = MagicMock()
        mgm.cloud_client_secret = enable_password
        return mgm

    def test_already_in_enable_mode_skips_interactive(self):
        conn = _make_conn(prompt="ASA#")
        _ensure_enable_mode(conn, self._mgm())
        conn.send_interactive.assert_not_called()

    def test_user_mode_enters_enable_successfully(self):
        conn = MagicMock()
        conn.get_prompt.side_effect = [" ASA> ", " ASA# "]
        mock_response = MagicMock()
        conn.send_interactive.return_value = mock_response
        _ensure_enable_mode(conn, self._mgm())
        conn.send_interactive.assert_called_once()

    def test_user_mode_enable_fails_but_recovers_with_hash_prompt(self):
        conn = MagicMock()
        conn.get_prompt.side_effect = [" ASA> ", " ASA# ", " ASA# "]
        conn.send_interactive.side_effect = Exception("timeout")
        _ensure_enable_mode(conn, self._mgm())

    def test_user_mode_enable_fails_empty_prompt_raises(self):
        conn = MagicMock()
        conn.get_prompt.side_effect = [" ASA> ", ""]
        conn.send_interactive.side_effect = Exception("timeout")
        with pytest.raises(FwoImporterError, match="Could not retrieve prompt"):
            _ensure_enable_mode(conn, self._mgm())

    def test_user_mode_enable_fails_non_hash_prompt_raises(self):
        conn = MagicMock()
        conn.get_prompt.side_effect = [" ASA> ", " ASA> "]
        conn.send_interactive.side_effect = Exception("timeout")
        with pytest.raises(FwoImporterError, match="Failed to enter enable mode"):
            _ensure_enable_mode(conn, self._mgm())

    def test_final_prompt_not_hash_raises(self):
        conn = MagicMock()
        conn.get_prompt.side_effect = [" ASA> ", " ASA> "]
        mock_response = MagicMock()
        conn.send_interactive.return_value = mock_response
        with pytest.raises(FwoImporterError, match="Not in enabled mode"):
            _ensure_enable_mode(conn, self._mgm())


class TestRetrieveConfigFromDevice:
    @pytest.fixture
    @patch("fw_modules.ciscoasa9.fwcommon._prepare_virtual_asa")
    def mock_prepare_virtual_asa(self, prepare: MagicMock) -> MagicMock:
        return prepare

    @pytest.fixture
    @patch("fw_modules.ciscoasa9.fwcommon._ensure_enable_mode")
    def mock_ensure_enable_mode(self, ensure_enable_mode: MagicMock) -> MagicMock:
        return ensure_enable_mode

    @pytest.fixture
    @patch("fw_modules.ciscoasa9.fwcommon._get_running_config", return_value="config")
    def mock_get_running_config(self, get_running_config: MagicMock) -> MagicMock:
        return get_running_config

    @patch("fw_modules.ciscoasa9.fwcommon._prepare_virtual_asa")
    @pytest.mark.usefixtures("mock_get_running_config", "mock_ensure_enable_mode")
    def test_virtual_asa_calls_prepare(self, mock_prepare: MagicMock):
        conn = MagicMock()
        mgm = MagicMock()
        _retrieve_config_from_device(conn, mgm, is_virtual_asa=True)
        mock_prepare.assert_called_once_with(conn)

    @patch("fw_modules.ciscoasa9.fwcommon._prepare_virtual_asa")
    @pytest.mark.usefixtures("mock_get_running_config", "mock_ensure_enable_mode")
    def test_non_virtual_asa_skips_prepare(self, mock_prepare: MagicMock):
        conn = MagicMock()
        mgm = MagicMock()
        _retrieve_config_from_device(conn, mgm, is_virtual_asa=False)
        mock_prepare.assert_not_called()


class TestLogRetryAttempt:
    @patch("fw_modules.ciscoasa9.fwcommon.time.sleep")
    def test_first_attempt_does_not_sleep(self, mock_sleep: MagicMock):
        _log_retry_attempt(0, 3)
        mock_sleep.assert_not_called()

    @patch("fw_modules.ciscoasa9.fwcommon.time.sleep")
    def test_subsequent_attempt_sleeps_with_backoff(self, mock_sleep: MagicMock):
        _log_retry_attempt(1, 3)
        mock_sleep.assert_called_once_with(4)


class TestAttemptConnection:
    @pytest.fixture
    @patch("fw_modules.ciscoasa9.fwcommon._safe_close_connection")
    def mock_safe_close_connection(self, safe_close: MagicMock) -> MagicMock:
        return safe_close

    @pytest.fixture
    @patch("fw_modules.ciscoasa9.fwcommon._retrieve_config_from_device")
    def mock_retrieve_config(self, retrieve: MagicMock) -> MagicMock:
        retrieve.return_value = "hostname asa"
        return retrieve

    @patch("fw_modules.ciscoasa9.fwcommon._connect_to_device")
    @patch("fw_modules.ciscoasa9.fwcommon._retrieve_config_from_device", return_value="hostname asa")
    @patch("fw_modules.ciscoasa9.fwcommon._safe_close_connection")
    def test_success_returns_config(self, _mock_close: MagicMock, _mock_retrieve: MagicMock, mock_connect: MagicMock):  # noqa: PT019
        mock_connect.return_value = MagicMock()
        mgm = MagicMock()
        result = _attempt_connection(mgm, False, 0, 3)  # noqa: FBT003
        assert result == "hostname asa"

    @patch("fw_modules.ciscoasa9.fwcommon._connect_to_device")
    @patch("fw_modules.ciscoasa9.fwcommon._retrieve_config_from_device")
    @patch("fw_modules.ciscoasa9.fwcommon._safe_close_connection")
    def test_exception_raises_fwo_importer_error(
        self, mock_close: MagicMock, mock_retrieve: MagicMock, mock_connect: MagicMock
    ):
        mock_connect.return_value = MagicMock()
        mock_retrieve.side_effect = Exception("SSH error")
        mgm = MagicMock()
        mgm.hostname = "10.0.0.1"
        with pytest.raises(FwoImporterError):
            _attempt_connection(mgm, False, 2, 3)  # noqa: FBT003
        mock_close.assert_called()

    @patch("fw_modules.ciscoasa9.fwcommon._connect_to_device")
    @patch("fw_modules.ciscoasa9.fwcommon._retrieve_config_from_device")
    @pytest.mark.usefixtures("mock_safe_close_connection")
    def test_connection_error_before_last_attempt_still_raises(self, mock_retrieve: MagicMock, mock_connect: MagicMock):
        mock_connect.return_value = MagicMock()
        mock_retrieve.side_effect = Exception("SSH error")
        mgm = MagicMock()
        mgm.hostname = "10.0.0.1"
        with pytest.raises(FwoImporterError):
            _attempt_connection(mgm, False, 0, 3)  # noqa: FBT003


class TestLoadConfigFromManagement:
    @patch("fw_modules.ciscoasa9.fwcommon._log_retry_attempt")
    @patch("fw_modules.ciscoasa9.fwcommon._attempt_connection", return_value="hostname asa")
    def test_first_attempt_success(self, mock_attempt: MagicMock, _mock_log: MagicMock):  # noqa: PT019
        mgm = MagicMock()
        result = load_config_from_management(mgm, False, max_retries=3)  # noqa: FBT003
        assert result == "hostname asa"
        mock_attempt.assert_called_once()

    @patch("fw_modules.ciscoasa9.fwcommon._log_retry_attempt")
    @patch("fw_modules.ciscoasa9.fwcommon._attempt_connection")
    def test_success_on_second_attempt(self, mock_attempt: MagicMock, _mock_log: MagicMock):  # noqa: PT019
        mock_attempt.side_effect = [FwoImporterError("fail"), "hostname asa"]
        mgm = MagicMock()
        result = load_config_from_management(mgm, False, max_retries=3)  # noqa: FBT003
        assert result == "hostname asa"

    @patch("fw_modules.ciscoasa9.fwcommon._log_retry_attempt")
    @patch("fw_modules.ciscoasa9.fwcommon._attempt_connection")
    def test_all_attempts_fail_raises(self, mock_attempt: MagicMock, _mock_log: MagicMock):  # noqa: PT019
        mock_attempt.side_effect = FwoImporterError("fail")
        mgm = MagicMock()
        mgm.hostname = "10.0.0.1"
        with pytest.raises(FwoImporterError):
            load_config_from_management(mgm, False, max_retries=2)  # noqa: FBT003

    def test_zero_max_retries_raises_immediately(self):
        mgm = MagicMock()
        mgm.hostname = "10.0.0.1"
        with pytest.raises(FwoImporterError, match="after 0 attempts"):
            load_config_from_management(mgm, False, max_retries=0)  # noqa: FBT003


class TestGetConfig:
    def _make_import_state(self, device_type_name: str = "Cisco ASA") -> MagicMock:
        import_state = MagicMock()
        import_state.state.mgm_details.device_type_name = device_type_name
        return import_state

    @pytest.fixture
    @patch("fw_modules.ciscoasa9.fwcommon.write_native_config_to_file")
    def mock_write_native_config(self, write_native: MagicMock) -> MagicMock:
        return write_native

    @pytest.fixture
    @patch("fw_modules.ciscoasa9.fwcommon.normalize_config")
    def mock_normalize_config(self, normalize: MagicMock) -> MagicMock:
        return normalize

    @patch("fw_modules.ciscoasa9.fwcommon.normalize_config")
    @pytest.mark.usefixtures("mock_write_native_config")
    def test_skips_load_when_native_config_already_populated(self, mock_normalize: MagicMock):
        import_state = self._make_import_state()
        config_in = MagicMock()
        config_in.native_config_is_empty.return_value = False

        status, result = get_config(config_in, import_state)

        assert status == 0
        assert result is config_in
        mock_normalize.assert_called_once()

    @patch("fw_modules.ciscoasa9.fwcommon.normalize_config")
    @pytest.mark.usefixtures("mock_write_native_config")
    @patch("fw_modules.ciscoasa9.fwcommon.parse_asa_config")
    @patch("fw_modules.ciscoasa9.fwcommon.load_config_from_management", return_value="raw config")
    def test_loads_and_parses_when_native_config_empty(
        self,
        mock_load: MagicMock,
        mock_parse: MagicMock,
        mock_normalize: MagicMock,
    ):
        import_state = self._make_import_state()
        config_in = MagicMock()
        config_in.native_config_is_empty.return_value = True
        mock_parse.return_value = MagicMock()
        mock_parse.return_value.model_dump.return_value = {"parsed": True}

        status, _result = get_config(config_in, import_state)

        assert status == 0
        mock_load.assert_called_once()
        mock_parse.assert_called_once_with("raw config")
        assert config_in.native_config == {"parsed": True}
        mock_normalize.assert_called_once()

    @patch("fw_modules.ciscoasa9.fwcommon.normalize_config")
    @patch("fw_modules.ciscoasa9.fwcommon.parse_asa_config")
    @patch("fw_modules.ciscoasa9.fwcommon.load_config_from_management", return_value="raw config")
    @pytest.mark.usefixtures("mock_write_native_config")
    def test_detects_virtual_asa_by_device_type_name(
        self,
        mock_load: MagicMock,
        mock_parse: MagicMock,
        _mock_normalize: MagicMock,  # noqa: PT019
    ):
        import_state = self._make_import_state(device_type_name="Cisco Asa on FirePower")
        config_in = MagicMock()
        config_in.native_config_is_empty.return_value = True
        mock_parse.return_value = MagicMock()
        mock_parse.return_value.model_dump.return_value = {}

        get_config(config_in, import_state)

        call_kwargs = mock_load.call_args
        assert call_kwargs.args[1] is True

    @patch("fw_modules.ciscoasa9.fwcommon.normalize_config")
    @patch("fw_modules.ciscoasa9.fwcommon.parse_asa_config")
    @patch("fw_modules.ciscoasa9.fwcommon.load_config_from_management", return_value="raw config")
    @pytest.mark.usefixtures("mock_write_native_config")
    def test_non_virtual_asa_passes_false(
        self,
        mock_load: MagicMock,
        mock_parse: MagicMock,
        _mock_normalize: MagicMock,  # noqa: PT019
    ):
        import_state = self._make_import_state(device_type_name="Cisco ASA")
        config_in = MagicMock()
        config_in.native_config_is_empty.return_value = True
        mock_parse.return_value = MagicMock()
        mock_parse.return_value.model_dump.return_value = {}

        get_config(config_in, import_state)

        call_kwargs = mock_load.call_args
        assert call_kwargs.args[1] is False


class TestLoadConfigFromFile:
    def test_reads_file_content(self, tmp_path: Path, mocker: MockerFixture):
        expected = "hostname asa\n"
        test_file = tmp_path / "test_asa.conf"
        test_file.write_text(expected)

        mocker.patch(
            "fw_modules.ciscoasa9.fwcommon.Path",
            return_value=test_file,
        )

        result = load_config_from_file("test_asa.conf")
        assert result == expected


class TestCiscoAsa9CommonGetConfig:
    @patch("fw_modules.ciscoasa9.fwcommon.get_config", return_value=(0, MagicMock()))
    def test_delegates_to_module_get_config(self, mock_get_config: MagicMock):
        asa = CiscoAsa9Common()
        config_in = MagicMock()
        import_state = MagicMock()

        asa.get_config(config_in, import_state)

        mock_get_config.assert_called_once_with(config_in, import_state)

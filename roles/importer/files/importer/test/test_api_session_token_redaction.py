from __future__ import annotations

import logging
from types import SimpleNamespace
from typing import TYPE_CHECKING, Any, cast
from unittest.mock import MagicMock, patch
from urllib.parse import quote, quote_plus

import pytest
import requests
from fw_modules.checkpointR8x import cp_getter
from fw_modules.fortiadom5ff import fmgr_getter
from fw_modules.fortiosmanagementREST import fos_getter
from fwo_exceptions import FwApiCallFailedError, FwApiError
from fwo_log import FWOLogger
from fwo_redaction import (
    MAX_REDACTION_DEPTH,
    REDACTED_VALUE,
    SESSION_FINGERPRINT_LENGTH,
    redact_secret_in_text,
    redact_sensitive_values,
    session_fingerprint,
)

if TYPE_CHECKING:
    from model_controllers.management_controller import ManagementController

SID_MARKER = "fwo-sweep-sid-marker-3"
SESSION_MARKER = "fwo-sweep-session-marker-4"
TOKEN_MARKER = "fwo-sweep-token-marker-5"  # noqa: S105
PASSWORD_MARKER = "fwo-sweep-password-marker-6"  # noqa: S105
CP_URL = "https://cp.invalid/web_api/"
FOS_URL = "https://fw.invalid/api/v2/cmdb/firewall/address"
HTTP_FORBIDDEN = 403
DEBUG_LEVEL_ALL = 10


@pytest.fixture
def debug_logging(caplog: pytest.LogCaptureFixture, monkeypatch: pytest.MonkeyPatch) -> pytest.LogCaptureFixture:
    """Capture every log line, including the most detailed debug output."""
    monkeypatch.setattr(FWOLogger.instance, "debug_level", DEBUG_LEVEL_ALL)
    caplog.set_level(logging.DEBUG)
    return caplog


def manager_mock() -> ManagementController:
    mock = SimpleNamespace(
        import_user="importer",
        secret=PASSWORD_MARKER,
        get_domain_string=lambda: "",
        build_fw_api_string=lambda: CP_URL,
    )
    return cast("ManagementController", mock)


class TestCheckPointApiCall:
    def test_transport_error_keeps_session_token_out_of_error_and_log(
        self, debug_logging: pytest.LogCaptureFixture
    ) -> None:
        with (
            patch.object(cp_getter.requests, "post", side_effect=requests.exceptions.ConnectTimeout("timed out")),
            pytest.raises(FwApiError) as raised,
        ):
            cp_getter.cp_api_call(CP_URL, "show-hosts", {"limit": 1}, SID_MARKER)

        assert SID_MARKER not in str(raised.value)
        assert SID_MARKER not in debug_logging.text
        assert f"{CP_URL}show-hosts" in str(raised.value)
        assert "'show-hosts'" in str(raised.value)
        assert "ConnectTimeout" in str(raised.value)

    def test_transport_error_redacts_credentials_in_payload(self) -> None:
        with (
            patch.object(cp_getter.requests, "post", side_effect=requests.exceptions.ConnectionError("reset")),
            pytest.raises(FwApiError) as raised,
        ):
            cp_getter.cp_api_call(CP_URL, "login", {"user": "importer", "password": PASSWORD_MARKER}, None)

        assert PASSWORD_MARKER not in str(raised.value)
        assert "importer" in str(raised.value)

    def test_garbled_response_warnings_show_fingerprint_instead_of_session(
        self, debug_logging: pytest.LogCaptureFixture
    ) -> None:
        cp_getter.log_global_assignments_error(SID_MARKER, CP_URL, {"limit": 1}, {"total": 0})

        assert SID_MARKER not in debug_logging.text
        assert session_fingerprint(SID_MARKER) in debug_logging.text


class TestCheckPointApiSession:
    @staticmethod
    def fail_inside_session() -> None:
        with cp_getter.api_session(manager_mock()) as sid:
            assert sid == SID_MARKER
            raise FwApiError("transport")

    def test_logs_out_when_the_block_fails(self) -> None:
        with (
            patch.object(cp_getter, "login", return_value=SID_MARKER),
            patch.object(cp_getter, "logout") as logout,
            pytest.raises(FwApiError, match="transport"),
        ):
            self.fail_inside_session()

        logout.assert_called_once_with(CP_URL, SID_MARKER)

    def test_logs_out_after_success(self) -> None:
        with (
            patch.object(cp_getter, "login", return_value=SID_MARKER),
            patch.object(cp_getter, "logout") as logout,
            cp_getter.api_session(manager_mock()),
        ):
            pass

        logout.assert_called_once_with(CP_URL, SID_MARKER)

    def test_failed_logout_neither_hides_the_error_nor_logs_the_session(
        self, debug_logging: pytest.LogCaptureFixture
    ) -> None:
        with (
            patch.object(cp_getter, "login", return_value=SID_MARKER),
            patch.object(cp_getter, "logout", side_effect=FwApiError("unreachable")),
            pytest.raises(ValueError, match="original"),
            cp_getter.api_session(manager_mock()),
        ):
            raise ValueError("original")

        assert SID_MARKER not in debug_logging.text
        assert session_fingerprint(SID_MARKER) in debug_logging.text


class TestFortiManagerApiCall:
    @staticmethod
    def payload() -> dict[str, Any]:
        return {"params": [{"data": {"user": "importer", "passwd": PASSWORD_MARKER}}]}

    def test_empty_result_keeps_session_and_password_out_of_error(self) -> None:
        response_json: dict[str, Any] = {"result": [], "session": SESSION_MARKER, "error": "no permission"}
        with patch.object(fmgr_getter.requests, "post") as post:
            post.return_value.json.return_value = response_json
            with pytest.raises(FwApiCallFailedError) as raised:
                fmgr_getter.api_call("https://fmg.invalid/jsonrpc", "", self.payload(), SESSION_MARKER)

        assert SESSION_MARKER not in str(raised.value)
        assert PASSWORD_MARKER not in str(raised.value)
        assert "https://fmg.invalid/jsonrpc" in str(raised.value)
        assert "no permission" in str(raised.value)

    def test_debug_log_keeps_session_and_password_out(self, debug_logging: pytest.LogCaptureFixture) -> None:
        with patch.object(fmgr_getter.requests, "post") as post:
            post.return_value.json.return_value = {"result": [{"status": {"code": 0}}]}
            fmgr_getter.api_call("https://fmg.invalid/jsonrpc", "", self.payload(), SESSION_MARKER)

        assert SESSION_MARKER not in debug_logging.text
        assert PASSWORD_MARKER not in debug_logging.text
        assert "https://fmg.invalid/jsonrpc" in debug_logging.text


class TestFortiOsApiCall:
    def test_transport_error_keeps_access_token_out_of_error(self) -> None:
        leaking_error = requests.exceptions.ConnectionError(
            f"Max retries exceeded with url: /api/v2/cmdb/firewall/address?access_token={TOKEN_MARKER}"
        )
        with (
            patch.object(fos_getter.requests, "get", side_effect=leaking_error),
            pytest.raises(FwApiCallFailedError) as raised,
        ):
            fos_getter.fortios_api_call("https://fw.invalid/api/v2/cmdb/firewall/address", TOKEN_MARKER)

        assert TOKEN_MARKER not in str(raised.value)
        assert "https://fw.invalid/api/v2/cmdb/firewall/address" in str(raised.value)
        assert raised.value.__cause__ is None

    def test_error_page_echoing_the_url_keeps_access_token_out_of_error(self) -> None:
        response = MagicMock(status_code=HTTP_FORBIDDEN)
        response.text = f"<p>denied: /api/v2/cmdb/firewall/address?access_token={TOKEN_MARKER}</p>"
        with (
            patch.object(fos_getter.requests, "get", return_value=response),
            pytest.raises(FwApiCallFailedError) as raised,
        ):
            fos_getter.fortios_api_call(FOS_URL, TOKEN_MARKER)

        assert TOKEN_MARKER not in str(raised.value)
        assert "denied: /api/v2/cmdb/firewall/address?access_token=" + REDACTED_VALUE in str(raised.value)

    def test_json_without_results_keeps_access_token_out_of_error(self) -> None:
        response = MagicMock(status_code=fos_getter.HTTP_OK)
        response.json.return_value = {
            "http_status": HTTP_FORBIDDEN,
            "message": f"invalid request /api/v2/cmdb?access_token={TOKEN_MARKER}",
            "access_token": TOKEN_MARKER,
        }
        with (
            patch.object(fos_getter.requests, "get", return_value=response),
            pytest.raises(FwApiCallFailedError) as raised,
        ):
            fos_getter.fortios_api_call(FOS_URL, TOKEN_MARKER)

        assert TOKEN_MARKER not in str(raised.value)
        assert "invalid request" in str(raised.value)


class TestRedaction:
    def test_redacts_sensitive_keys_at_any_depth_and_in_any_case(self) -> None:
        data = {
            "X-chkp-sid": SID_MARKER,
            "params": [{"data": {"Passwd": PASSWORD_MARKER, "name": "obj"}}],
            "session": SESSION_MARKER,
            "limit": 1,
        }

        redacted = redact_sensitive_values(data)

        assert redacted == {
            "X-chkp-sid": REDACTED_VALUE,
            "params": [{"data": {"Passwd": REDACTED_VALUE, "name": "obj"}}],
            "session": REDACTED_VALUE,
            "limit": 1,
        }
        assert data["session"] == SESSION_MARKER

    @pytest.mark.parametrize("secret", ["tok en/+=", "plain-token"])
    def test_redacts_a_known_secret_in_text_also_url_encoded(self, secret: str) -> None:
        text = f"raw {secret}, encoded {quote(secret, safe='')}, form {quote_plus(secret)}"

        redacted = redact_secret_in_text(text, secret)

        assert redacted == f"raw {REDACTED_VALUE}, encoded {REDACTED_VALUE}, form {REDACTED_VALUE}"

    def test_empty_secret_leaves_text_unchanged(self) -> None:
        assert redact_secret_in_text("some text", "") == "some text"
        assert redact_secret_in_text("some text", None) == "some text"

    def test_replaces_structures_nested_too_deeply(self) -> None:
        nested: dict[str, Any] = {"session": SESSION_MARKER}
        for _ in range(MAX_REDACTION_DEPTH + 1):
            nested = {"level": nested}

        assert SESSION_MARKER not in str(redact_sensitive_values(nested))

    def test_session_fingerprint_is_short_stable_and_not_the_session(self) -> None:
        fingerprint = session_fingerprint(SID_MARKER)

        assert len(fingerprint) == SESSION_FINGERPRINT_LENGTH
        assert fingerprint == session_fingerprint(SID_MARKER)
        assert fingerprint not in SID_MARKER
        assert session_fingerprint("") == "none"
        assert session_fingerprint(None) == "none"

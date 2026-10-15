"""Tests for the internal TLS identity used on FWO API and middleware calls."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, cast

import pytest
import requests
from fwo_api import FwoApi
from fwo_exceptions import FwoImporterError
from models.fwo_config import TlsIdentity
from models.fwo_config_controller import FwoConfigController, read_tls_identity
from test.utils.fwo_api_builder import build_fwo_api

CLIENT_CERTIFICATE = "/etc/fworch/secrets/client/client.crt"
CLIENT_PRIVATE_KEY = "/etc/fworch/secrets/client/client.key"
CA_CERTIFICATE = "/etc/fworch/fworch-internal-ca.crt"


def write_config_file(tmp_path: Path, overrides: dict[str, Any] | None = None) -> str:
    """Write a fworch.json fixture and return its path."""
    config: dict[str, Any] = {
        "middleware_uri": "https://127.0.0.1:8443/",
        "api_uri": "https://127.0.0.1:9443/api/v1/graphql",
        "product_version": "9.2.4",
        "tls_client_certificate": CLIENT_CERTIFICATE,
        "tls_client_private_key": CLIENT_PRIVATE_KEY,
        "tls_ca_certificate": CA_CERTIFICATE,
    }
    if overrides is not None:
        config.update(overrides)
    config_file = tmp_path / "fworch.json"
    config_file.write_text(json.dumps(config), encoding="utf-8")
    return str(config_file)


def use_config_file(config_file: str, monkeypatch: pytest.MonkeyPatch) -> None:
    """
    Point the config location at a fixture instead of /etc/fworch/fworch.json.

    Only the location is redirected: replacing the reader would leave the reader itself untested.
    """
    read_tls_identity.cache_clear()
    monkeypatch.setattr("models.fwo_config_controller.FWO_CONFIG_FILE", config_file)


class TestReadConfig:
    def test_read_config_accepts_config_with_tls_identity(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        importer_password = tmp_path / "importer_pwd"
        importer_password.write_text("secret\n", encoding="utf-8")
        monkeypatch.setattr("models.fwo_config_controller.IMPORTER_PWD_FILE", str(importer_password))
        read_tls_identity.cache_clear()
        config_file = write_config_file(tmp_path)

        config_controller = FwoConfigController(config_file)

        assert config_controller.fwo_config.fwo_api_url == "https://127.0.0.1:9443/api/v1/graphql"
        assert read_tls_identity(config_file) == TlsIdentity(CLIENT_CERTIFICATE, CLIENT_PRIVATE_KEY, CA_CERTIFICATE)

    def test_read_config_exits_when_ca_certificate_missing(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch, caplog: pytest.LogCaptureFixture
    ) -> None:
        importer_password = tmp_path / "importer_pwd"
        importer_password.write_text("secret\n", encoding="utf-8")
        monkeypatch.setattr("models.fwo_config_controller.IMPORTER_PWD_FILE", str(importer_password))
        config_file = tmp_path / "fworch.json"
        config_file.write_text(
            json.dumps(
                {
                    "middleware_uri": "https://127.0.0.1:8443/",
                    "api_uri": "https://127.0.0.1:9443/api/v1/graphql",
                    "product_version": "9.2.4",
                    "tls_client_certificate": CLIENT_CERTIFICATE,
                    "tls_client_private_key": CLIENT_PRIVATE_KEY,
                }
            ),
            encoding="utf-8",
        )

        read_tls_identity.cache_clear()

        with pytest.raises(SystemExit):
            FwoConfigController(str(config_file))

        # asserting only SystemExit let a regression through where the operator was told
        # "unspecified error" instead of which key is missing
        assert any("tls_ca_certificate" in message for message in caplog.messages), (
            f"the missing key must be named, got: {caplog.messages}"
        )


class TestConfigureInternalApiSession:
    def test_session_presents_client_identity_and_internal_ca(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        use_config_file(write_config_file(tmp_path), monkeypatch)

        with requests.Session() as session:
            FwoApi._configure_internal_api_session(session)  # pyright: ignore[reportPrivateUsage]

            assert session.cert == (CLIENT_CERTIFICATE, CLIENT_PRIVATE_KEY)
            # verify must name the CA file: requests resolves True to certifi,
            # which does not contain the internal CA.
            assert session.verify == CA_CERTIFICATE
            assert session.verify is not True
            assert session.verify is not False

    def test_missing_identity_reports_the_offending_key(self, tmp_path: Path) -> None:
        read_tls_identity.cache_clear()
        config_file = write_config_file(tmp_path)
        Path(config_file).write_text(json.dumps({"api_uri": "https://api/"}), encoding="utf-8")

        with pytest.raises(FwoImporterError, match="tls_client_certificate"):
            read_tls_identity(config_file)

    def test_missing_config_file_is_reported(self, tmp_path: Path) -> None:
        read_tls_identity.cache_clear()

        with pytest.raises(FwoImporterError, match="unable to access"):
            read_tls_identity(str(tmp_path / "absent.json"))


MIDDLEWARE_URI = "https://127.0.0.1:8443/"
API_URI = "https://127.0.0.1:9443/api/v1/graphql"


class _FakeResponse:
    def __init__(self, status_code: int = 200, text: str = "{}", json_data: Any = None) -> None:
        self.status_code = status_code
        self.text = text
        self._json_data: Any = {"data": {}} if json_data is None else json_data

    def json(self) -> Any:
        return self._json_data

    def raise_for_status(self) -> None:
        return None


# subclasses the real session so the context manager protocol used by FwoApi keeps working
class _IdentityRecordingSession(requests.Session):
    """Records the TLS identity in effect at the moment each request is issued."""

    def __init__(self, response: _FakeResponse) -> None:
        super().__init__()
        self._response = response
        self.identity_at_request: list[tuple[Any, Any]] = []

    def _record_and_respond(self) -> requests.Response:
        # captured per request, not once at the end: configuring the session after the
        # call would leave the request itself unauthenticated
        self.identity_at_request.append((self.cert, self.verify))
        return cast("requests.Response", self._response)

    def post(self, url: str | bytes, *_args: Any, **_kwargs: Any) -> requests.Response:  # noqa: ARG002
        return self._record_and_respond()

    def get(self, url: str | bytes, *_args: Any, **_kwargs: Any) -> requests.Response:  # noqa: ARG002
        return self._record_and_respond()


class TestEverySessionPresentsTheClientIdentity:
    """
    Guards the call sites rather than the helper.

    TestConfigureInternalApiSession proves the helper works; nothing proved that the
    outbound paths actually call it. A merge resolution that keeps the helper but drops
    the calls to it - the shape of conflict this module attracts - leaves the whole
    suite green while the importer fails mTLS on every API call.
    """

    @staticmethod
    def _assert_identity_presented(session: _IdentityRecordingSession) -> None:
        assert session.identity_at_request, "no request was issued - the test never reached the session"
        for cert, verify in session.identity_at_request:
            assert cert == (CLIENT_CERTIFICATE, CLIENT_PRIVATE_KEY), f"client identity missing, got cert={cert!r}"
            assert verify == CA_CERTIFICATE, f"internal CA not used for verification, got verify={verify!r}"

    def _recording_session(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch, response: _FakeResponse
    ) -> _IdentityRecordingSession:
        use_config_file(write_config_file(tmp_path), monkeypatch)
        session = _IdentityRecordingSession(response)
        monkeypatch.setattr(requests, "Session", lambda: session)
        return session

    def test_graphql_call_presents_identity(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        session = self._recording_session(tmp_path, monkeypatch, _FakeResponse(json_data={"data": {"ok": True}}))

        build_fwo_api(API_URI, "test-jwt").call("query { management { mgm_id } }")

        self._assert_identity_presented(session)

    def test_login_presents_identity(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        session = self._recording_session(
            tmp_path, monkeypatch, _FakeResponse(text=json.dumps({"AccessToken": "new-jwt"}))
        )

        FwoApi.login("importer", "secret", MIDDLEWARE_URI)

        self._assert_identity_presented(session)

    def test_token_refresh_presents_identity(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        """
        The refresh endpoint is the outbound call this branch adds, so it is the one
        path no pre-existing test covered. Without the client identity it is also the
        least visible failure: _try_refresh_jwt swallows the error and returns False.
        """
        session = self._recording_session(
            tmp_path, monkeypatch, _FakeResponse(text=json.dumps({"AccessToken": "new-jwt"}))
        )

        FwoApi.refresh("refresh-token", MIDDLEWARE_URI)

        self._assert_identity_presented(session)

    def test_middleware_endpoint_call_presents_identity(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        session = self._recording_session(tmp_path, monkeypatch, _FakeResponse(json_data={"ok": True}))

        build_fwo_api(API_URI, "test-jwt", user_management_api_base_url=MIDDLEWARE_URI).call_endpoint("GET", "User")

        self._assert_identity_presented(session)

from __future__ import annotations

import argparse
import json
from typing import TYPE_CHECKING, Any

import pytest
import requests

if TYPE_CHECKING:
    from collections.abc import Callable
    from types import TracebackType
    from typing import Self

    from _pytest.monkeypatch import MonkeyPatch

from scripts.customizing.provisioning.guardicore import guardicore_lib
from scripts.customizing.provisioning.guardicore.guardicore_lib import (
    FwoConfig,
    apply_fwo_ssl_settings,
    extract_label_items,
    load_fwo_ca_certificate,
    load_fwo_client_identity,
    login_fwo,
    login_guardicore,
    read_fwo_tls_config,
    resolve_ssl_verification_settings,
    run_graphql_query,
)

SAMPLE_FWO_CA_CERT = "/etc/ssl/certs/fwo-ca.pem"
SAMPLE_GUARDICORE_CA_CERT = "/etc/ssl/certs/guardicore-ca.pem"
SAMPLE_FWO_CLIENT_CERT = "/etc/fworch/secrets/client/client.crt"
SAMPLE_FWO_CLIENT_KEY = "/etc/fworch/secrets/client/client.key"


class SessionStub:
    cert: tuple[str, str]

    def __init__(self, post_handler: Callable[..., Any]) -> None:
        self.headers: dict[str, Any] = {}
        self.verify = True
        self._post_handler = post_handler

    def __enter__(self) -> Self:
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        del exc_type, exc, tb

    def post(self, *args: Any, **kwargs: Any) -> Any:
        return self._post_handler(*args, **kwargs)


def install_session_stub(monkeypatch: MonkeyPatch, post_handler: Callable[..., Any]) -> None:
    def fake_session() -> SessionStub:
        return SessionStub(post_handler)

    monkeypatch.setattr(requests, "Session", fake_session)


def test_extract_label_items_reads_objects_list():
    payload = {"objects": [{"key": "AppRole", "value": "Role-A"}]}

    items = extract_label_items(payload)

    assert items == [{"key": "AppRole", "value": "Role-A"}]


def test_extract_label_items_returns_dict_when_no_known_list_key_exists():
    payload = {"key": "AppRole", "value": "Role-A"}

    items = extract_label_items(payload)

    assert items == [payload]


def test_resolve_ssl_verification_settings_prefers_specific_flags():
    args = argparse.Namespace(
        insecure=True,
        fwo_insecure=False,
        guardicore_insecure=False,
        fwo_ca_cert=SAMPLE_FWO_CA_CERT,
        guardicore_ca_cert=SAMPLE_GUARDICORE_CA_CERT,
    )

    fwo_verify, guardicore_verify = resolve_ssl_verification_settings(args)

    assert fwo_verify == SAMPLE_FWO_CA_CERT
    assert guardicore_verify == SAMPLE_GUARDICORE_CA_CERT


def test_resolve_ssl_verification_settings_allows_endpoint_specific_insecure():
    args = argparse.Namespace(
        insecure=False,
        fwo_insecure=True,
        guardicore_insecure=True,
        fwo_ca_cert=SAMPLE_FWO_CA_CERT,
        guardicore_ca_cert=SAMPLE_GUARDICORE_CA_CERT,
    )

    fwo_verify, guardicore_verify = resolve_ssl_verification_settings(args)

    assert fwo_verify is False
    assert guardicore_verify is False


def test_login_fwo_returns_jwt_and_rejects_non_ok_response(monkeypatch: MonkeyPatch):
    class FakeResponse:
        status_code = 200
        text = "jwt"

    def post_fwo_login(endpoint: str, json: dict[str, Any], headers: dict[str, str], timeout: int) -> FakeResponse:
        del endpoint, json, headers, timeout
        return FakeResponse()

    install_session_stub(monkeypatch, post_fwo_login)

    assert login_fwo("user", "secret", "https://fwo", True, 10, RuntimeError) == "jwt"

    FakeResponse.status_code = 401
    FakeResponse.text = "denied"
    with pytest.raises(RuntimeError, match="status 401"):
        login_fwo("user", "secret", "https://fwo", True, 10, RuntimeError)


def test_login_guardicore_accepts_supported_token_keys(monkeypatch: MonkeyPatch):
    class FakeResponse:
        def raise_for_status(self) -> None:
            return None

        def json(self) -> dict[str, Any]:
            return {"accessToken": "gc-token"}

    def post_guardicore_login(
        endpoint: str,
        json: dict[str, Any],
        headers: dict[str, str],
        timeout: int,
    ) -> FakeResponse:
        del endpoint, json, headers, timeout
        return FakeResponse()

    install_session_stub(monkeypatch, post_guardicore_login)

    assert login_guardicore("user", "secret", "https://gc", True, 10, RuntimeError) == "gc-token"


def test_login_guardicore_rejects_invalid_json_and_missing_token(monkeypatch: MonkeyPatch):
    class FakeResponse:
        payload: dict[str, Any] | None = None

        def raise_for_status(self) -> None:
            return None

        def json(self) -> dict[str, Any]:
            if self.payload is None:
                raise ValueError("invalid")
            return self.payload

    def post_guardicore_login(
        endpoint: str,
        json: dict[str, Any],
        headers: dict[str, str],
        timeout: int,
    ) -> FakeResponse:
        del endpoint, json, headers, timeout
        return FakeResponse()

    install_session_stub(monkeypatch, post_guardicore_login)

    with pytest.raises(RuntimeError, match="not valid JSON"):
        login_guardicore("user", "secret", "https://gc", True, 10, RuntimeError)

    FakeResponse.payload = {"token": ""}
    with pytest.raises(RuntimeError, match="did not include a token"):
        login_guardicore("user", "secret", "https://gc", True, 10, RuntimeError)


def test_run_graphql_query_removes_line_breaks_in_payload(monkeypatch: MonkeyPatch):
    captured_payload: dict[str, Any] = {}

    class FakeResponse:
        def raise_for_status(self) -> None:
            return None

        def json(self) -> dict[str, Any]:
            return {"data": {"ok": True}}

    def post_graphql_query(url: str, json: dict[str, Any], timeout: int) -> FakeResponse:
        del url, timeout
        captured_payload.update(json)
        return FakeResponse()

    install_session_stub(monkeypatch, post_graphql_query)

    config = FwoConfig(
        graphql_url="https://fwo/graphql",
        jwt="jwt",
        verify_ssl=True,
        timeout_seconds=10,
        role="reporter",
    )
    query = "query Test {\\n  owner {\\n    id\\n  }\\n}"
    result = run_graphql_query(config, query, {"x": 1}, RuntimeError)

    assert result == {"data": {"ok": True}}
    assert "\n" not in captured_payload["query"]


def write_fwo_config(tmp_path: Any, include_tls: bool = True) -> str:
    config: dict[str, Any] = {"api_uri": "https://fwo/graphql"}
    if include_tls:
        config.update(
            {
                "tls_client_certificate": SAMPLE_FWO_CLIENT_CERT,
                "tls_client_private_key": SAMPLE_FWO_CLIENT_KEY,
                "tls_ca_certificate": SAMPLE_FWO_CA_CERT,
            }
        )
    config_path = tmp_path / "fworch.json"
    config_path.write_text(json.dumps(config), encoding="utf-8")
    return str(config_path)


def test_load_fwo_client_identity_and_ca_from_config(tmp_path: Any) -> None:
    read_fwo_tls_config.cache_clear()
    config_file = write_fwo_config(tmp_path)

    assert load_fwo_client_identity(config_file) == (SAMPLE_FWO_CLIENT_CERT, SAMPLE_FWO_CLIENT_KEY)
    assert load_fwo_ca_certificate(config_file) == SAMPLE_FWO_CA_CERT


def test_load_fwo_identity_is_optional_for_remote_installations(tmp_path: Any) -> None:
    read_fwo_tls_config.cache_clear()
    missing = str(tmp_path / "absent.json")

    assert load_fwo_client_identity(missing) is None
    assert load_fwo_ca_certificate(missing) is None

    read_fwo_tls_config.cache_clear()
    without_tls = write_fwo_config(tmp_path, include_tls=False)

    assert load_fwo_client_identity(without_tls) is None
    assert load_fwo_ca_certificate(without_tls) is None


def test_apply_fwo_ssl_settings_presents_client_identity(tmp_path: Any, monkeypatch: MonkeyPatch) -> None:
    read_fwo_tls_config.cache_clear()
    monkeypatch.setattr(guardicore_lib, "FWO_CONFIG_FILE", write_fwo_config(tmp_path))
    session = SessionStub(lambda *_args, **_kwargs: None)

    apply_fwo_ssl_settings(session, SAMPLE_FWO_CA_CERT)  # type: ignore[arg-type]

    assert session.cert == (SAMPLE_FWO_CLIENT_CERT, SAMPLE_FWO_CLIENT_KEY)
    assert session.verify == SAMPLE_FWO_CA_CERT


def test_apply_fwo_ssl_settings_skips_identity_when_absent(tmp_path: Any, monkeypatch: MonkeyPatch) -> None:
    read_fwo_tls_config.cache_clear()
    monkeypatch.setattr(guardicore_lib, "FWO_CONFIG_FILE", str(tmp_path / "absent.json"))
    session = SessionStub(lambda *_args, **_kwargs: None)

    apply_fwo_ssl_settings(session, True)  # type: ignore[arg-type]

    assert not hasattr(session, "cert")


def test_resolve_ssl_verification_defaults_fwo_to_internal_ca(tmp_path: Any, monkeypatch: MonkeyPatch) -> None:
    read_fwo_tls_config.cache_clear()
    monkeypatch.setattr(guardicore_lib, "FWO_CONFIG_FILE", write_fwo_config(tmp_path))
    args = argparse.Namespace(
        insecure=False, fwo_insecure=False, guardicore_insecure=False, fwo_ca_cert=None, guardicore_ca_cert=None
    )

    fwo_verify, guardicore_verify = resolve_ssl_verification_settings(args)

    # FWO gets the internal CA bundle, the external endpoint keeps the default store
    assert fwo_verify == SAMPLE_FWO_CA_CERT
    assert guardicore_verify is True


def test_read_fwo_tls_config_tolerates_a_missing_file(tmp_path: Any) -> None:
    read_fwo_tls_config.cache_clear()

    # remote FWO installations have no local config; that is not an error
    assert read_fwo_tls_config(str(tmp_path / "absent.json")) == {}


def test_read_fwo_tls_config_raises_on_a_corrupt_file(tmp_path: Any) -> None:
    read_fwo_tls_config.cache_clear()
    corrupt = tmp_path / "fworch.json"
    corrupt.write_text("{ not json", encoding="utf-8")

    # degrading to "no client identity" would surface as an opaque TLS failure instead
    with pytest.raises(json.JSONDecodeError):
        read_fwo_tls_config(str(corrupt))

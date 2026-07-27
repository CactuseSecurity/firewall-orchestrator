import builtins
import json
from pathlib import Path
from types import TracebackType
from typing import Any, ClassVar, Self
from unittest.mock import Mock

import pytest
import requests

from scripts.customizing import customizing

EXPECTED_MODELLING_SERVICE_ID = 7


class FakeResponse:
    def __init__(self, status_code: int = 200, text: str = "jwt", payload: dict[str, Any] | None = None) -> None:
        self.status_code = status_code
        self.text = text
        self.payload = payload if payload is not None else {}

    def raise_for_status(self) -> None:
        if self.status_code != customizing.HTTP_OK:
            error = requests.exceptions.HTTPError("failed")
            response = requests.Response()
            response.status_code = self.status_code
            error.response = response
            raise error

    def json(self) -> dict[str, Any]:
        return self.payload


TEST_CLIENT_CERTIFICATE = "/etc/fworch/secrets/client/client.crt"
TEST_CLIENT_PRIVATE_KEY = "/etc/fworch/secrets/client/client.key"
TEST_CA_CERTIFICATE = "/etc/fworch/fworch-internal-ca.crt"


def write_tls_config(tmp_path: Path) -> str:
    """Write a fworch.json holding the TLS identity and return its path."""
    config_path = tmp_path / "fworch.json"
    config_path.write_text(
        json.dumps(
            {
                "tls_client_certificate": TEST_CLIENT_CERTIFICATE,
                "tls_client_private_key": TEST_CLIENT_PRIVATE_KEY,
                "tls_ca_certificate": TEST_CA_CERTIFICATE,
            }
        ),
        encoding="utf-8",
    )
    return str(config_path)


class FakeSession:
    response: FakeResponse = FakeResponse()
    request_payloads: ClassVar[list[dict[str, Any]]] = []
    raise_on_post: requests.exceptions.RequestException | None = None

    def __init__(self) -> None:
        self.verify: bool | str = True
        self.cert: tuple[str, str] | None = None
        self.headers: dict[str, str] = {}

    def __enter__(self) -> Self:
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        return None

    def post(self, url: str, data: str, timeout: int | tuple[int, int]) -> FakeResponse:
        if self.raise_on_post is not None:
            raise self.raise_on_post
        self.request_payloads.append(
            {"url": url, "data": json.loads(data), "timeout": timeout, "headers": self.headers}
        )
        return self.response


@pytest.fixture(autouse=True)
def reset_fake_session(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    FakeSession.response = FakeResponse()
    FakeSession.request_payloads = []
    FakeSession.raise_on_post = None
    monkeypatch.setattr(customizing.requests, "Session", FakeSession)
    # Redirect the config location only; the reader itself must stay under test, or a
    # broken reader would go unnoticed the way it did for the importer.
    customizing.read_tls_identity.cache_clear()
    monkeypatch.setattr(customizing, "FWO_CONFIG_FILE", write_tls_config(tmp_path))


def test_call_sends_graphql_payload_and_returns_json() -> None:
    FakeSession.response = FakeResponse(payload={"data": {"ok": True}})

    result = customizing.call(
        "https://fwo/graphql",
        "jwt",
        "query Test { ok }",
        query_variables={"id": 1},
        role="admin",
    )

    assert result == {"data": {"ok": True}}
    assert FakeSession.request_payloads == [
        {
            "url": "https://fwo/graphql",
            "data": {"query": "query Test { ok }", "variables": {"id": 1}},
            "timeout": 600,
            "headers": {
                "Content-Type": "application/json",
                "Authorization": "Bearer jwt",
                "x-hasura-role": "admin",
            },
        }
    ]


def test_call_wraps_http_error() -> None:
    FakeSession.response = FakeResponse(status_code=500)

    with pytest.raises(customizing.CustomizingError, match="got error code: 500"):
        customizing.call("https://fwo/graphql", "jwt", "query")


def test_call_wraps_missing_response() -> None:
    FakeSession.raise_on_post = requests.exceptions.RequestException("down")

    with pytest.raises(customizing.CustomizingError, match="got no result"):
        customizing.call("https://fwo/graphql", "jwt", "query")


def test_login_returns_jwt_and_rejects_non_ok_response() -> None:
    FakeSession.response = FakeResponse(text="jwt-token")

    assert customizing.login("user", "secret", "https://middleware/") == "jwt-token"

    FakeSession.response = FakeResponse(status_code=401)
    with pytest.raises(customizing.CustomizingError, match="did not receive a JWT"):
        customizing.login("user", "secret", "https://middleware/")

    FakeSession.raise_on_post = requests.exceptions.RequestException("down")
    with pytest.raises(customizing.CustomizingError, match="no valid response"):
        customizing.login("user", "secret", "https://middleware/")


def test_get_and_set_helpers_handle_success_and_missing_results(monkeypatch: pytest.MonkeyPatch) -> None:
    call_mock = Mock(
        side_effect=[
            {"data": {"config": [{"config_value": "50"}]}},
            {"data": {"config": [{"config_key": "limit", "config_value": "50"}]}},
            {"data": {"insert_config": {"returning": [{"id": "limit"}]}}},
            {"data": {"insert_customtxt": {"returning": [{"id": "txt-id"}]}}},
            {"data": {"insert_modelling_service": {"returning": [{"id": EXPECTED_MODELLING_SERVICE_ID}]}}},
            None,
        ]
    )
    monkeypatch.setattr(customizing, "call", call_mock)

    assert customizing.get_config_value("https://fwo/graphql", "jwt", "limit") == "50"
    assert customizing.get_config_values("https://fwo/graphql", "jwt", "lim") == {"limit": "50"}
    assert customizing.set_config_values("https://fwo/graphql", "jwt", {"config_key": "limit"}) == "limit"
    assert customizing.set_custom_txt_values("https://fwo/graphql", "jwt", {"id": "txt-id"}) == "txt-id"
    assert (
        customizing.set_modelling_service_values("https://fwo/graphql", "jwt", {"name": "svc"})
        == EXPECTED_MODELLING_SERVICE_ID
    )
    assert customizing.set_config_values("https://fwo/graphql", "jwt") == -1


def test_get_and_set_helpers_return_none_or_minus_one_for_empty_payloads(monkeypatch: pytest.MonkeyPatch) -> None:
    call_mock = Mock(
        side_effect=[
            None,
            None,
            {"data": {"config": [{"other": "value"}]}},
            {"data": {"other": []}},
            {"data": {"insert_customtxt": {"returning": [{"id": ""}]}}},
            {"data": {"insert_modelling_service": {"returning": [{"id": 0}]}}},
        ]
    )
    monkeypatch.setattr(customizing, "call", call_mock)

    assert customizing.get_config_value("https://fwo/graphql", "jwt", "missing") is None
    assert customizing.get_config_values("https://fwo/graphql", "jwt", "missing") is None
    assert customizing.get_config_value("https://fwo/graphql", "jwt", "other") is None
    assert customizing.get_config_values("https://fwo/graphql", "jwt", "other") is None
    assert customizing.set_custom_txt_values("https://fwo/graphql", "jwt") == -1
    assert customizing.set_modelling_service_values("https://fwo/graphql", "jwt") == -1


def test_read_json_file_and_credentials(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    settings_path = tmp_path / "settings.json"
    settings_path.write_text('{"config": []}', encoding="utf-8")

    def fake_input(_prompt: str) -> str:
        return "alice"

    def fake_getpass(_prompt: str) -> str:
        return "secret"

    monkeypatch.setattr(builtins, "input", fake_input)
    monkeypatch.setattr(customizing.getpass, "getpass", fake_getpass)

    assert customizing.read_json_file(str(settings_path)) == {"config": []}
    assert customizing.get_credentials() == ("alice", "secret")

    with pytest.raises(customizing.CustomizingError, match="while reading file"):
        customizing.read_json_file(str(tmp_path / "missing.json"))


def test_call_presents_client_identity_and_internal_ca() -> None:
    captured: dict[str, Any] = {}
    original_post = FakeSession.post

    def capture_post(self: FakeSession, url: str, data: str, timeout: int | tuple[int, int]) -> FakeResponse:
        captured["cert"] = self.cert
        captured["verify"] = self.verify
        return original_post(self, url, data, timeout)

    FakeSession.post = capture_post  # type: ignore[method-assign]
    try:
        customizing.call("https://fwo/graphql", "jwt", "query Test { ok }")
    finally:
        FakeSession.post = original_post  # type: ignore[method-assign]

    assert captured["cert"] == (TEST_CLIENT_CERTIFICATE, TEST_CLIENT_PRIVATE_KEY)
    # verify must name the CA file: requests resolves True to the certifi
    # bundle, which does not contain FWO's internal CA.
    assert captured["verify"] == TEST_CA_CERTIFICATE


def test_read_tls_identity_returns_configured_paths(tmp_path: Path) -> None:
    config_path = tmp_path / "fworch.json"
    config_path.write_text(
        json.dumps(
            {
                "tls_client_certificate": TEST_CLIENT_CERTIFICATE,
                "tls_client_private_key": TEST_CLIENT_PRIVATE_KEY,
                "tls_ca_certificate": TEST_CA_CERTIFICATE,
            }
        ),
        encoding="utf-8",
    )
    customizing.read_tls_identity.cache_clear()

    assert customizing.read_tls_identity(str(config_path)) == (
        (TEST_CLIENT_CERTIFICATE, TEST_CLIENT_PRIVATE_KEY),
        TEST_CA_CERTIFICATE,
    )


def test_read_tls_identity_reports_missing_key(tmp_path: Path) -> None:
    config_path = tmp_path / "fworch.json"
    config_path.write_text(json.dumps({"tls_client_certificate": TEST_CLIENT_CERTIFICATE}), encoding="utf-8")
    customizing.read_tls_identity.cache_clear()

    with pytest.raises(customizing.CustomizingError, match="TLS identity missing"):
        customizing.read_tls_identity(str(config_path))


def test_configure_tls_applies_identity_to_session(tmp_path: Path) -> None:
    config_path = write_tls_config(tmp_path)
    customizing.read_tls_identity.cache_clear()
    session = FakeSession()

    customizing.configure_tls(session, config_path)  # type: ignore[arg-type]

    assert session.cert == (TEST_CLIENT_CERTIFICATE, TEST_CLIENT_PRIVATE_KEY)
    assert session.verify == TEST_CA_CERTIFICATE

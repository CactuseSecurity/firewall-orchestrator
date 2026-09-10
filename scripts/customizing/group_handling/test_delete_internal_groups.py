# ruff: noqa: INP001
import importlib.util
import json
import logging
import sys
from pathlib import Path
from types import ModuleType, TracebackType
from typing import Any, ClassVar, Self, cast

import pytest

SAMPLE_CLIENT_CERT = "/etc/fworch/secrets/client/client.crt"
SAMPLE_CLIENT_KEY = "/etc/fworch/secrets/client/client.key"
SAMPLE_CA_CERT = "/etc/fworch/fworch-internal-ca.crt"


def load_module() -> ModuleType:
    module_path = Path(__file__).with_name("delete-internal-groups.py")
    spec = importlib.util.spec_from_file_location("delete_internal_groups", module_path)
    if spec is None or spec.loader is None:
        raise RuntimeError("could not load delete-internal-groups.py")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    module_with_logger: Any = module
    module_with_logger.logger = logging.getLogger("delete-internal-groups-test")
    return module


class FakeResponse:
    def __init__(self, status_code: int = 200, text: str = "jwt", payload: Any = None) -> None:
        self.status_code = status_code
        self.text = text
        self.payload = payload if payload is not None else {"ok": True}

    def json(self) -> Any:
        return self.payload


class FakeSession:
    response: FakeResponse = FakeResponse()
    calls: ClassVar[list[dict[str, Any]]] = []
    raise_on_post: Exception | None = None
    cert: tuple[str, str]

    def __init__(self) -> None:
        self.verify: bool = True

    def __enter__(self) -> Self:
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        return None

    def get(self, url: str, json: dict[str, Any], headers: dict[str, str], timeout: tuple[int, int]) -> FakeResponse:
        self.calls.append({"method": "get", "url": url, "json": json, "headers": headers, "timeout": timeout})
        return self.response

    def delete(self, url: str, json: dict[str, Any], headers: dict[str, str], timeout: tuple[int, int]) -> FakeResponse:
        self.calls.append({"method": "delete", "url": url, "json": json, "headers": headers, "timeout": timeout})
        return self.response

    def post(self, url: str, json: dict[str, Any], headers: dict[str, str], timeout: tuple[int, int]) -> FakeResponse:
        if self.raise_on_post is not None:
            raise self.raise_on_post
        self.calls.append({"method": "post", "url": url, "json": json, "headers": headers, "timeout": timeout})
        return self.response


@pytest.fixture
def module(monkeypatch: pytest.MonkeyPatch) -> ModuleType:
    loaded = load_module()
    FakeSession.response = FakeResponse()
    FakeSession.calls = []
    FakeSession.raise_on_post = None
    monkeypatch.setattr(loaded, "Session", FakeSession)
    return loaded


def test_fwo_rest_api_call_dispatches_command(module: ModuleType) -> None:
    FakeSession.response = FakeResponse(payload=[{"GroupDn": "CN=test,OU=Groups"}])

    result = module.fwo_rest_api_call("https://fwo/api/", "jwt", "Group", module.HttpCommand.GET.value)

    assert result == [{"GroupDn": "CN=test,OU=Groups"}]
    assert FakeSession.calls[0]["url"] == "https://fwo/api/Group"
    assert FakeSession.calls[0]["headers"]["Authorization"] == "Bearer jwt"


def test_fwo_rest_api_call_exits_on_error(module: ModuleType) -> None:
    FakeSession.response = FakeResponse(status_code=500, text="failed")

    with pytest.raises(SystemExit):
        module.fwo_rest_api_call("https://fwo/api/", "jwt", "Group")


def test_get_jwt_token_returns_response_text(module: ModuleType) -> None:
    FakeSession.response = FakeResponse(text="jwt-token")

    assert module.get_jwt_token("user", "secret", "https://fwo/api/") == "jwt-token"
    assert FakeSession.calls[0]["url"] == "https://fwo/api/AuthenticationToken/Get"


def test_get_matching_groups_filters_by_group_dn(module: ModuleType, monkeypatch: pytest.MonkeyPatch) -> None:
    def fake_api_call(
        _api_url: str,
        _jwt: str,
        _endpoint_name: str,
        _command: str,
        payload: dict[str, Any] | None = None,
    ) -> list[dict[str, str]]:
        del payload
        return [
            {"GroupDn": "CN=keep-internal,OU=Groups"},
            {"GroupDn": "CN=other,OU=Groups"},
        ]

    monkeypatch.setattr(
        module,
        "fwo_rest_api_call",
        fake_api_call,
    )

    groups = module.get_matching_groups("jwt", "internal", "https://fwo/api/")

    assert groups == [{"GroupDn": "CN=keep-internal,OU=Groups"}]


def test_delete_groups_from_roles_and_extract_common_names(module: ModuleType, monkeypatch: pytest.MonkeyPatch) -> None:
    calls: list[dict[str, Any]] = []
    monkeypatch.setattr(module, "args", type("Args", (), {"api_url": "https://fwo/api/"})(), raising=False)
    monkeypatch.setattr(module, "jwt", "jwt", raising=False)

    def fake_api_call(
        api_url: str,
        jwt: str,
        endpoint_name: str,
        command: str,
        payload: dict[str, Any] | None = None,
    ) -> bool:
        calls.append(
            {"api_url": api_url, "jwt": jwt, "endpoint": endpoint_name, "command": command, "payload": payload}
        )
        return True

    monkeypatch.setattr(
        module,
        "fwo_rest_api_call",
        cast("Any", fake_api_call),
    )

    module.delete_groups_from_roles(["CN=group,OU=Groups"], ["admin", "reporter"])

    assert module.extract_common_names([{"GroupDn": "CN=group,OU=Groups"}]) == ["group"]
    assert [call["payload"] for call in calls] == [
        {"Role": "admin", "UserDn": "CN=group,OU=Groups"},
        {"Role": "reporter", "UserDn": "CN=group,OU=Groups"},
    ]


def test_read_tls_identity_returns_client_pair_and_internal_ca(tmp_path: Path) -> None:
    module = load_module()
    config_path = tmp_path / "fworch.json"
    config_path.write_text(
        json.dumps(
            {
                "tls_client_certificate": SAMPLE_CLIENT_CERT,
                "tls_client_private_key": SAMPLE_CLIENT_KEY,
                "tls_ca_certificate": SAMPLE_CA_CERT,
            }
        ),
        encoding="utf-8",
    )
    module.read_tls_identity.cache_clear()

    client_identity, ca_certificate = module.read_tls_identity(str(config_path))

    assert client_identity == (SAMPLE_CLIENT_CERT, SAMPLE_CLIENT_KEY)
    assert ca_certificate == SAMPLE_CA_CERT


def test_read_tls_identity_falls_back_to_default_store_when_config_absent(tmp_path: Path) -> None:
    module = load_module()
    module.read_tls_identity.cache_clear()

    client_identity, ca_certificate = module.read_tls_identity(str(tmp_path / "absent.json"))

    assert client_identity is None
    # never False: a missing local config must not silently disable verification
    assert ca_certificate is True


def test_configure_tls_never_disables_verification(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    module = load_module()
    config_path = tmp_path / "fworch.json"
    config_path.write_text(
        json.dumps(
            {
                "tls_client_certificate": SAMPLE_CLIENT_CERT,
                "tls_client_private_key": SAMPLE_CLIENT_KEY,
                "tls_ca_certificate": SAMPLE_CA_CERT,
            }
        ),
        encoding="utf-8",
    )
    module.read_tls_identity.cache_clear()
    monkeypatch.setattr(module, "FWO_CONFIG_FILE", str(config_path))
    session = FakeSession()

    module.configure_tls(session)

    assert session.verify == SAMPLE_CA_CERT
    assert session.cert == (SAMPLE_CLIENT_CERT, SAMPLE_CLIENT_KEY)


def test_read_tls_identity_raises_on_a_corrupt_config(tmp_path: Path) -> None:
    module = load_module()
    corrupt = tmp_path / "fworch.json"
    corrupt.write_text("{ not json", encoding="utf-8")
    module.read_tls_identity.cache_clear()

    # a missing file is tolerated, an unparsable one must not degrade silently
    with pytest.raises(json.JSONDecodeError):
        module.read_tls_identity(str(corrupt))

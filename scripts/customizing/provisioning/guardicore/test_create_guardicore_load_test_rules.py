from __future__ import annotations

import sys
from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
from typing import TYPE_CHECKING, Any, cast

import pytest

from scripts.customizing.provisioning.guardicore.test_create_guardicore_rules import SessionStub

if TYPE_CHECKING:
    from collections.abc import Callable

    from _pytest.monkeypatch import MonkeyPatch

TEST_GUARDICORE_TOKEN = "guardicore_token_for_tests"  # This is a dummy token for testing purposes only. It does not grant any access. #NOQA: S105
EXPECTED_CREATED_RULES = 1001


class StaticJsonResponse:
    def __init__(self, payload: dict[str, Any] | None = None) -> None:
        self.payload = payload or {}

    def raise_for_status(self) -> None:
        return None

    def json(self) -> dict[str, Any]:
        return self.payload


def load_module() -> Any:
    module_path = Path(__file__).with_name("create_guardicore_load_test_rules.py")
    spec = spec_from_file_location("create_guardicore_load_test_rules", module_path)
    assert spec is not None
    assert spec.loader is not None
    module = module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return cast("Any", module)


def install_session_stub(
    monkeypatch: MonkeyPatch,
    module: Any,
    *,
    get_handler: Callable[..., StaticJsonResponse] | None = None,
    post_handler: Callable[..., StaticJsonResponse] | None = None,
) -> SessionStub:
    session = SessionStub(get_handler=get_handler, post_handler=post_handler)  # pyright: ignore[reportArgumentType]
    monkeypatch.setattr(module.requests, "Session", lambda: session)
    return session


def build_guardicore_config(module: Any, timeout_seconds: int = 5) -> Any:
    return module.GuardicoreConfig(
        base_url="https://gc.local",
        token=TEST_GUARDICORE_TOKEN,
        verify_ssl=True,
        timeout_seconds=timeout_seconds,
    )


def test_build_ip_label_payload_uses_loadtest_key_and_subnet():
    module = load_module()

    payload = module.build_ip_label_payload("Label A", "10.0.0.1")

    assert payload == {
        "key": "LoadTest",
        "value": "Label A",
        "criteria": [{"field": "numeric_ip_addresses", "op": "SUBNET", "argument": "10.0.0.1"}],
    }


def test_create_labels_for_ips_returns_label_ids_from_succeeded(monkeypatch: MonkeyPatch):
    module = load_module()
    captured_calls: list[dict[str, Any]] = []

    def post_handler(endpoint: str, json: Any, timeout: int) -> StaticJsonResponse:
        captured_calls.append({"endpoint": endpoint, "json": json, "timeout": timeout})
        return StaticJsonResponse({"succeeded": ["label-a-id", "label-b-id"]})

    install_session_stub(monkeypatch, module, post_handler=post_handler)

    label_a_id, label_b_id = module.create_labels_for_ips(
        build_guardicore_config(module),
        "10.0.0.1",
        "Label A",
        "10.0.0.2",
        "Label B",
    )

    assert label_a_id == "label-a-id"
    assert label_b_id == "label-b-id"
    assert len(captured_calls) == 1


def test_create_labels_for_ips_raises_when_succeeded_ids_are_missing(monkeypatch: MonkeyPatch):
    module = load_module()

    def post_handler(endpoint: str, json: Any, timeout: int) -> StaticJsonResponse:
        del endpoint, json, timeout
        return StaticJsonResponse({"succeeded": ["label-a-id"]})

    install_session_stub(monkeypatch, module, post_handler=post_handler)

    with pytest.raises(module.GuardicoreLoadTestError):
        module.create_labels_for_ips(
            build_guardicore_config(module),
            "10.0.0.1",
            "Label A",
            "10.0.0.2",
            "Label B",
        )


def test_create_rules_posts_1001_tcp_rules(monkeypatch: MonkeyPatch):
    module = load_module()
    captured_calls: list[dict[str, Any]] = []

    def post_handler(endpoint: str, json: Any, timeout: int) -> StaticJsonResponse:
        captured_calls.append({"endpoint": endpoint, "json": json, "timeout": timeout})
        return StaticJsonResponse()

    install_session_stub(monkeypatch, module, post_handler=post_handler)

    created_rules = module.create_rules(build_guardicore_config(module), "src-id", "dst-id")

    assert created_rules == EXPECTED_CREATED_RULES
    assert len(captured_calls) == EXPECTED_CREATED_RULES
    assert all(call["endpoint"] == "https://gc.local/api/v4.0/visibility/policy/rules" for call in captured_calls)
    assert captured_calls[0]["json"]["ports"] == [1000]
    assert captured_calls[999]["json"]["ports"] == [1999]
    assert captured_calls[1000]["json"]["ports"] == [22]
    assert captured_calls[0]["json"]["ip_protocols"] == ["TCP"]
    assert captured_calls[0]["json"]["source"]["labels"]["or_labels"] == [{"and_labels": ["src-id"]}]
    assert captured_calls[0]["json"]["destination"]["labels"]["or_labels"] == [{"and_labels": ["dst-id"]}]


def test_publish_posts_revision(monkeypatch: MonkeyPatch):
    module = load_module()
    captured_calls: list[dict[str, Any]] = []

    def post_handler(endpoint: str, json: Any, timeout: int) -> StaticJsonResponse:
        captured_calls.append({"endpoint": endpoint, "json": json, "timeout": timeout})
        return StaticJsonResponse()

    install_session_stub(monkeypatch, module, post_handler=post_handler)

    module.publish(build_guardicore_config(module, timeout_seconds=15))

    assert captured_calls == [
        {
            "endpoint": "https://gc.local/api/v4.0/visibility/policy/revisions",
            "json": {"comments": "published guardicore load test rules"},
            "timeout": 15,
        }
    ]

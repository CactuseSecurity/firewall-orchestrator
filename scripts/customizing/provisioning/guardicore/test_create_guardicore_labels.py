from __future__ import annotations

import sys
from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    from types import ModuleType, TracebackType
    from typing import Self

    from _pytest.monkeypatch import MonkeyPatch

EXPECTED_FETCH_CALLS = 2


def _get_test_token() -> str:
    return "test-token"


def load_module() -> ModuleType:
    module_path = Path(__file__).with_name("create_guardicore_labels.py")
    spec = spec_from_file_location("create_guardicore_labels", module_path)
    assert spec is not None
    assert spec.loader is not None
    module = module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def test_parse_existing_label_pairs_from_list_payload():
    module = load_module()
    payload = [
        {"key": "AppRole", "value": "Role A"},
        {"key": "AppZone", "value": "Zone B"},
        {"key": "Broken"},
    ]

    pairs = module.parse_existing_label_pairs(payload)

    assert pairs == {("AppRole", "Role A"), ("AppZone", "Zone B")}


def test_parse_existing_label_pairs_from_object_list_payload():
    module = load_module()
    payload = {"objects": [{"key": "AppRole", "value": "Role A"}]}

    pairs = module.parse_existing_label_pairs(payload)

    assert pairs == {("AppRole", "Role A")}


def test_parse_existing_label_pairs_from_nested_label_payload():
    module = load_module()
    payload = {"objects": [{"label": {"key": " AppRole ", "value": " Role A "}}]}

    pairs = module.parse_existing_label_pairs(payload)

    assert pairs == {("AppRole", "Role A")}


def test_filter_missing_labels_skips_existing_key_value_pairs():
    module = load_module()
    labels = [
        module.LabelItem(key="AppRole", value="Role A", criteria=[]),
        module.LabelItem(key="AppZone", value="Zone B", criteria=[]),
    ]

    filtered = module.filter_missing_labels(labels, {("AppRole", "Role A")})

    assert filtered == [module.LabelItem(key="AppZone", value="Zone B", criteria=[])]


def test_post_guardicore_labels_skips_http_call_for_empty_payload(monkeypatch: MonkeyPatch):
    module = load_module()

    class FailingSessionFactory:
        def __call__(self) -> None:
            raise AssertionError("requests.Session() must not be called for empty payload")

    monkeypatch.setattr(module.requests, "Session", FailingSessionFactory())
    config = module.GuardicoreConfig(
        base_url="https://gc.local",
        token=_get_test_token(),
        verify_ssl=True,
        timeout_seconds=5,
    )

    module.post_guardicore_labels(config, [])


def test_fetch_existing_guardicore_labels_reads_paginated_list(monkeypatch: MonkeyPatch):
    module = load_module()

    class FakeResponse:
        def __init__(self, payload: dict[str, Any]) -> None:
            self.payload = payload

        def raise_for_status(self) -> None:
            return None

        def json(self) -> dict[str, Any]:
            return self.payload

    class FakeSession:
        def __init__(self) -> None:
            self.headers: dict[str, Any] = {}
            self.verify = True
            self.calls: list[tuple[str, dict[str, Any], int]] = []

        def __enter__(self) -> Self:
            return self

        def __exit__(
            self,
            exc_type: type[BaseException] | None,
            exc: BaseException | None,
            tb: TracebackType | None,
        ) -> None:
            return None

        def get(self, endpoint: str, params: dict[str, Any], timeout: int) -> FakeResponse:
            self.calls.append((endpoint, params, timeout))
            if params["offset"] == 0:
                return FakeResponse(
                    {
                        "objects": [
                            {"key": "AppRole", "value": "Role A"},
                            {"key": "AppZone", "value": "Zone B"},
                        ],
                        "total_count": 3,
                    }
                )
            return FakeResponse({"objects": [{"key": "AppRole", "value": "Role C"}], "total_count": 3})

    fake_session = FakeSession()
    monkeypatch.setattr(module.requests, "Session", lambda: fake_session)
    config = module.GuardicoreConfig(
        base_url="https://gc.local",
        token=_get_test_token(),
        verify_ssl=True,
        timeout_seconds=5,
    )

    pairs = module.fetch_existing_guardicore_labels(config)

    assert pairs == {("AppRole", "Role A"), ("AppZone", "Zone B"), ("AppRole", "Role C")}
    assert len(fake_session.calls) == EXPECTED_FETCH_CALLS

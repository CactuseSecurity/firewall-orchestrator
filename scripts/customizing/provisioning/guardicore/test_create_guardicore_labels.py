from __future__ import annotations

import sys
from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
from types import SimpleNamespace
from typing import TYPE_CHECKING, Any

import pytest

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


def test_post_guardicore_labels_raises_when_response_reports_failed_items(monkeypatch: MonkeyPatch):
    module = load_module()

    class FakeResponse:
        def raise_for_status(self) -> None:
            return None

        def json(self) -> dict[str, Any]:
            return {"failed_items": [{"key": "NetworkArea", "value": "Bad Label"}]}

    class FakeSession:
        def __init__(self) -> None:
            self.headers: dict[str, Any] = {}
            self.verify = True

        def __enter__(self) -> Self:
            return self

        def __exit__(
            self,
            exc_type: type[BaseException] | None,
            exc: BaseException | None,
            tb: TracebackType | None,
        ) -> None:
            return None

        def post(self, endpoint: str, json: list[dict[str, Any]], timeout: int) -> FakeResponse:
            return FakeResponse()

    monkeypatch.setattr(module.requests, "Session", lambda: FakeSession())
    config = module.GuardicoreConfig(
        base_url="https://gc.local",
        token=_get_test_token(),
        verify_ssl=True,
        timeout_seconds=5,
    )

    with pytest.raises(module.GuardicoreProvisioningError):
        module.post_guardicore_labels(config, [{"key": "NetworkArea", "value": "NeMo (APP-1) - A (NA-1)"}])


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
            if params["start_at"] == 0:
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


def test_build_graphql_query_uses_explicit_vars_not_ownerfilter():
    module = load_module()

    query = module.build_graphql_query(include_common_services=False, filter_by_app_ids=True)

    assert "ownerFilter" not in query
    assert "$appIds" in query
    assert "$groupTypes" in query
    assert "common_service_possible: { _eq: true }" not in query


def test_build_graphql_query_includes_common_services_clause_when_requested():
    module = load_module()

    query = module.build_graphql_query(include_common_services=True, filter_by_app_ids=False)

    assert "ownerFilter" not in query
    assert "$appIds" not in query
    assert "$groupTypes" in query
    assert "common_service_possible: { _eq: true }" in query


def test_build_ownerless_areas_graphql_query_targets_modelling_nwgroup_type_23_without_owner():
    module = load_module()

    query = module.build_ownerless_areas_graphql_query()

    assert "modelling_nwgroup" in query
    assert "group_type: { _eq: 23 }" in query
    assert "app_id: { _is_null: true }" in query


def test_build_graphql_variables_contains_explicit_parameters_only():
    module = load_module()

    variables = module.build_graphql_variables(app_ids=["APP-1"], include_group_types=[20, 21])

    assert variables == {"groupTypes": [20, 21], "appIds": ["APP-1"]}


def test_build_graphql_variables_uses_default_group_types_including_23():
    module = load_module()

    variables = module.build_graphql_variables()

    assert variables == {"groupTypes": [20, 21, 23]}


def test_build_label_from_group_maps_type_23_to_networkarea_when_include_empty():
    module = load_module()
    owner = {"name": "NeMo", "app_id_external": "APP-5630"}
    nwgroup = {
        "name": "NeMo Entwicklung",
        "id_string": "AREA-1",
        "group_type": 23,
        "nwobject_nwgroups": [],
    }

    label = module.build_label_from_group(owner, nwgroup, include_empty=True)

    assert label is not None
    assert label.key == "NetworkArea"
    assert label.value == "NeMo (APP-5630) - NeMo Entwicklung (AREA-1)"


def test_build_label_from_group_maps_na_prefix_to_networkarea_without_group_type():
    module = load_module()
    owner = {"name": "NeMo", "app_id_external": "APP-5630"}
    nwgroup = {
        "name": "NeMo Entwicklung",
        "id_string": "NA5005630-006",
        "nwobject_nwgroups": [],
    }

    label = module.build_label_from_group(owner, nwgroup, include_empty=True)

    assert label is not None
    assert label.key == "NetworkArea"


def test_build_label_from_group_maps_na_prefix_to_networkarea_even_with_group_type_20():
    module = load_module()
    owner = {"name": "NeMo", "app_id_external": "APP-5630"}
    nwgroup = {
        "name": "NeMo Entwicklung",
        "id_string": "NA5005630-006",
        "group_type": 20,
        "nwobject_nwgroups": [],
    }

    label = module.build_label_from_group(owner, nwgroup, include_empty=True)

    assert label is not None
    assert label.key == "NetworkArea"


def test_build_label_from_group_without_owner_uses_compact_value_format():
    module = load_module()
    nwgroup = {
        "name": "NeMo Entwicklung",
        "id_string": "NA5005630-006",
        "group_type": 23,
        "nwobject_nwgroups": [],
    }

    label = module.build_label_from_group({}, nwgroup, include_empty=True)

    assert label is not None
    assert label.key == "NetworkArea"
    assert label.value == "NeMo Entwicklung (NA5005630-006)"


def test_fetch_labels_from_fwo_merges_ownerless_type_23_groups(monkeypatch: MonkeyPatch):
    module = load_module()
    args = SimpleNamespace(
        fwo_graphql_url="https://fwo/graphql",
        timeout=5,
        fwo_role="reporter",
        include_common_services=False,
        app_ids=None,
        include_group_types=[20, 21, 23],
        include_empty=False,
    )

    def fake_run_graphql_query(
        config: Any,
        query: str,
        variables: dict[str, Any],
        error_cls: type[Exception],
    ) -> dict[str, Any]:
        if "owner(where:" in query:
            return {
                "data": {
                    "owner": [
                        {
                            "name": "NeMo",
                            "app_id_external": "APP-5630",
                            "nwgroups": [
                                {
                                    "name": "Role A",
                                    "id_string": "AR5005630-001",
                                    "group_type": 20,
                                    "nwobject_nwgroups": [],
                                }
                            ],
                        }
                    ]
                }
            }
        if "modelling_nwgroup" in query:
            return {
                "data": {
                    "modelling_nwgroup": [
                        {
                            "name": "NeMo Entwicklung",
                            "id_string": "NA5005630-006",
                            "group_type": 23,
                            "nwobject_nwgroups": [],
                        }
                    ]
                }
            }
        raise AssertionError(f"Unexpected query: {query}")

    monkeypatch.setattr(module, "run_graphql_query", fake_run_graphql_query)

    labels, group_type_counts = module.fetch_labels_from_fwo(args, jwt="token", fwo_verify=True)

    keys = [label.key for label in labels]
    values = [label.value for label in labels]
    assert "AppRole" in keys
    assert "NetworkArea" in keys
    assert "NeMo Entwicklung (NA5005630-006)" in values
    assert group_type_counts[20] == 1
    assert group_type_counts[23] == 1


def test_build_label_from_group_includes_approle_without_criteria_by_default():
    module = load_module()
    owner = {"name": "NeMo", "app_id_external": "APP-5630"}
    nwgroup = {
        "name": "NeMo Entwicklung",
        "id_string": "AR5005630-006",
        "group_type": 20,
        "nwobject_nwgroups": [],
    }

    label = module.build_label_from_group(owner, nwgroup, include_empty=False)

    assert label is not None
    assert label.key == "AppRole"
    assert label.criteria == []


def test_build_label_from_group_includes_networkarea_without_criteria_by_default():
    module = load_module()
    owner = {"name": "NeMo", "app_id_external": "APP-5630"}
    nwgroup = {
        "name": "NeMo Entwicklung",
        "id_string": "NA5005630-006",
        "group_type": 23,
        "nwobject_nwgroups": [],
    }

    label = module.build_label_from_group(owner, nwgroup, include_empty=False)

    assert label is not None
    assert label.key == "NetworkArea"
    assert label.criteria == []

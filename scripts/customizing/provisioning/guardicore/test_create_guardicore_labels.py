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

        def post(self, _endpoint: str, **_kwargs: Any) -> FakeResponse:
            return FakeResponse()

    monkeypatch.setattr(module.requests, "Session", FakeSession)
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


def test_label_helper_functions_build_expected_values(monkeypatch: MonkeyPatch):
    module = load_module()
    owner = {"name": "Payments", "app_id_external": "APP-001"}
    nwgroup = {
        "name": "Web",
        "id_string": "AR-1",
        "group_type": module.DEFAULT_GROUP_TYPE_APPROLE,
        "nwobject_nwgroups": [
            {"owner_network": {"ip": "10.0.0.1", "ip_end": "10.0.0.1"}},
            {"owner_network": {"ip": "10.0.0.0/24", "ip_end": "10.0.0.255/24"}},
            {"owner_network": {"ip": "", "ip_end": "10.0.0.2"}},
        ],
    }

    label = module.build_label_from_group(owner, nwgroup, include_empty=False)

    assert module.normalize_ip("10.0.0.0/24") == "10.0.0.0"
    assert module.label_key_from_id("AZ-1") == module.DEFAULT_GUARDICORE_KEY_APPZONE
    assert module.label_key_from_id("NA-1") == module.DEFAULT_GUARDICORE_KEY_NETWORKAREA
    assert module.label_key_from_id("unknown") is None
    assert label == module.LabelItem(
        key=module.DEFAULT_GUARDICORE_KEY_APPROLE,
        value="Payments (APP-001) - Web (AR-1)",
        criteria=[
            module.Criteria(field=module.DEFAULT_GUARDICORE_FIELD, op="SUBNET", argument="10.0.0.1"),
            module.Criteria(field=module.DEFAULT_GUARDICORE_FIELD, op="RANGE", argument="10.0.0.0-10.0.0.255"),
        ],
    )
    assert module.to_guardicore_payload([label])[0]["criteria"][0]["argument"] == "10.0.0.1"
    assert module.build_label_from_group(owner, {"name": "", "id_string": "AR-1"}, include_empty=True) is None
    assert module.build_label_from_group(owner, {"name": "NoId", "id_string": ""}, include_empty=True) is None
    assert module.build_label_from_group(owner, {"name": "NoKey", "id_string": "XX-1"}, include_empty=True) is None

    log_messages: list[str] = []

    def fake_post_guardicore_labels(config: Any, payload: list[dict[str, Any]]) -> None:
        del config, payload
        log_messages.append("posted")

    class FakeLogger:
        def info(self, message: str, *args: Any, **kwargs: Any) -> None:
            del message, args, kwargs

    monkeypatch.setattr(module, "post_guardicore_labels", fake_post_guardicore_labels)
    args = SimpleNamespace(batch_size=1, dry_run=False)
    sent_count, sent_by_key = module.send_labels_in_batches(
        args,
        [label, module.LabelItem(key="AppZone", value="Zone", criteria=[])],
        module.GuardicoreConfig("https://gc", "token", True, 5),
        FakeLogger(),
    )

    assert sent_count == 2
    assert sent_by_key == {"AppRole": 1, "AppZone": 1}
    assert log_messages == ["posted", "posted"]


def test_build_labels_and_counts_from_graphql_responses():
    module = load_module()
    response = {
        "data": {
            "owner": [
                {
                    "name": "Payments",
                    "app_id_external": "APP-001",
                    "nwgroups": [
                        {"name": "Area", "id_string": "NA-1", "group_type": module.DEFAULT_GROUP_TYPE_AREA},
                        {"name": "Zone", "id_string": "AZ-1", "group_type": module.DEFAULT_GROUP_TYPE_APPZONE},
                        {"name": "Ignored", "id_string": "XX-1", "group_type": 999},
                    ],
                }
            ],
            "modelling_nwgroup": [
                {"name": "Ownerless", "id_string": "NA-2", "group_type": module.DEFAULT_GROUP_TYPE_AREA},
                "ignored",
            ],
        }
    }

    labels = module.build_labels_from_response(response, include_empty=True)
    ownerless_labels = module.build_labels_from_ownerless_areas_response(response, include_empty=True)

    assert [label.key for label in labels] == ["NetworkArea", "AppZone"]
    assert [label.value for label in ownerless_labels] == ["Ownerless (NA-2)"]
    assert module.build_labels_from_ownerless_areas_response({"data": {"modelling_nwgroup": "bad"}}) == []
    assert module.count_group_types_from_response(response) == {
        module.DEFAULT_GROUP_TYPE_AREA: 1,
        module.DEFAULT_GROUP_TYPE_APPZONE: 1,
        999: 1,
    }
    assert module.count_group_types_from_ownerless_areas_response(response) == {module.DEFAULT_GROUP_TYPE_AREA: 1}
    assert module.count_group_types_from_ownerless_areas_response({"data": {"modelling_nwgroup": "bad"}}) == {}
    assert module.count_labels_by_key(labels) == {"NetworkArea": 1, "AppZone": 1}
    assert module.count_existing_pairs_by_key({(" AppRole ", "Role")}) == {"AppRole": 1}
    assert list(module.chunked(labels, 1)) == [[labels[0]], [labels[1]]]
    assert module.deduplicate_labels([labels[0], labels[0], labels[1]]) == labels


def test_guardicore_label_auth_and_fetch_helpers(monkeypatch: MonkeyPatch):
    module = load_module()
    args = SimpleNamespace(
        fwo_jwt=None,
        fwo_user=None,
        fwo_password=None,
        fwo_middleware_url=None,
        guardicore_user=None,
        guardicore_password=None,
        guardicore_url="https://gc",
        timeout=5,
        fwo_graphql_url="https://fwo/graphql",
        fwo_role="reporter",
        include_common_services=False,
        app_ids=["APP-001"],
        include_group_types=[module.DEFAULT_GROUP_TYPE_AREA],
        include_empty=True,
    )

    with pytest.raises(module.GuardicoreProvisioningError, match="--fwo-user"):
        module.require_login_fields(args)
    args.fwo_jwt = "jwt"
    module.require_login_fields(args)
    with pytest.raises(module.GuardicoreProvisioningError, match="--guardicore-user"):
        module.require_guardicore_fields(args)

    args.guardicore_user = "user"
    args.guardicore_password = "password"
    module.require_guardicore_fields(args)

    def fake_login_fwo(
        user: str,
        password: str,
        middleware_url: str,
        verify_ssl: bool | str,
        timeout: int,
        error_cls: type[Exception],
    ) -> str:
        del user, password, middleware_url, verify_ssl, timeout, error_cls
        return "login-jwt"

    monkeypatch.setattr(module, "login_fwo", fake_login_fwo)
    args.fwo_jwt = None
    args.fwo_user = "user"
    args.fwo_password = "password"
    args.fwo_middleware_url = "https://middleware"
    assert module.get_fwo_jwt(args, True) == "login-jwt"

    def fake_login_guardicore(
        user: str,
        password: str,
        base_url: str,
        verify_ssl: bool | str,
        timeout: int,
        error_cls: type[Exception],
    ) -> str:
        del user, password, base_url, verify_ssl, timeout, error_cls
        return "gc-token"

    monkeypatch.setattr(module, "login_guardicore", fake_login_guardicore)
    assert module.build_guardicore_config(args, False) == module.GuardicoreConfig("https://gc", "gc-token", False, 5)

    responses = [
        {
            "data": {
                "owner": [
                    {
                        "name": "Payments",
                        "app_id_external": "APP-001",
                        "nwgroups": [
                            {"name": "Area", "id_string": "NA-1", "group_type": module.DEFAULT_GROUP_TYPE_AREA}
                        ],
                    }
                ]
            }
        },
        {
            "data": {
                "modelling_nwgroup": [
                    {"name": "Ownerless", "id_string": "NA-2", "group_type": module.DEFAULT_GROUP_TYPE_AREA}
                ]
            }
        },
    ]

    def fake_run_graphql_query(
        config: Any,
        query: str,
        variables: dict[str, Any],
        error_cls: type[Exception],
    ) -> dict[str, Any]:
        del config, query, variables, error_cls
        return responses.pop(0)

    monkeypatch.setattr(module, "run_graphql_query", fake_run_graphql_query)

    labels, counts = module.fetch_labels_from_fwo(args, "jwt", True)

    assert [label.value for label in labels] == ["Payments (APP-001) - Area (NA-1)", "Ownerless (NA-2)"]
    assert counts == {module.DEFAULT_GROUP_TYPE_AREA: 2}


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
    owner: dict[str, Any] = {"name": "NeMo", "app_id_external": "APP-5630"}
    nwgroup: dict[str, Any] = {
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
    owner: dict[str, Any] = {"name": "NeMo", "app_id_external": "APP-5630"}
    nwgroup: dict[str, Any] = {
        "name": "NeMo Entwicklung",
        "id_string": "NA5005630-006",
        "nwobject_nwgroups": [],
    }

    label = module.build_label_from_group(owner, nwgroup, include_empty=True)

    assert label is not None
    assert label.key == "NetworkArea"


def test_build_label_from_group_maps_na_prefix_to_networkarea_even_with_group_type_20():
    module = load_module()
    owner: dict[str, Any] = {"name": "NeMo", "app_id_external": "APP-5630"}
    nwgroup: dict[str, Any] = {
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
    nwgroup: dict[str, Any] = {
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
        _config: Any,
        query: str,
        _variables: dict[str, Any],
        _error_cls: type[Exception],
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
    owner: dict[str, Any] = {"name": "NeMo", "app_id_external": "APP-5630"}
    nwgroup: dict[str, Any] = {
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
    owner: dict[str, Any] = {"name": "NeMo", "app_id_external": "APP-5630"}
    nwgroup: dict[str, Any] = {
        "name": "NeMo Entwicklung",
        "id_string": "NA5005630-006",
        "group_type": 23,
        "nwobject_nwgroups": [],
    }

    label = module.build_label_from_group(owner, nwgroup, include_empty=False)

    assert label is not None
    assert label.key == "NetworkArea"
    assert label.criteria == []

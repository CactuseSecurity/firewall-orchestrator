from __future__ import annotations

import sys
from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
from types import SimpleNamespace
from typing import TYPE_CHECKING, Any

import pytest

if TYPE_CHECKING:
    from collections.abc import Callable
    from types import ModuleType, TracebackType
    from typing import Self

    from _pytest.monkeypatch import MonkeyPatch

EXPECTED_PROTOCOL_PAYLOADS = 2
EXPECTED_PUBLISH_TIMEOUT = 15
EXPECTED_SECOND_OFFSET = 2
EXPECTED_PAGINATED_LABEL_COUNT = 3
TEST_GUARDICORE_TOKEN = (
    "guardicore_token_for_tests"  # This is a dummy token for testing purposes only. It does not grant any access.
)


class StaticJsonResponse:
    def __init__(self, payload: dict[str, Any] | None = None) -> None:
        self.payload = payload or {}

    def raise_for_status(self) -> None:
        return None

    def json(self) -> dict[str, Any]:
        return self.payload


class SessionStub:
    def __init__(
        self,
        get_handler: Callable[..., StaticJsonResponse] | None = None,
        post_handler: Callable[..., StaticJsonResponse] | None = None,
    ) -> None:
        self.headers: dict[str, Any] = {}
        self.verify = True
        self._get_handler = get_handler
        self._post_handler = post_handler

    def __enter__(self) -> Self:
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        return None

    def get(self, _endpoint: str, **kwargs: Any) -> StaticJsonResponse:
        if self._get_handler is None:
            return StaticJsonResponse()
        return self._get_handler(kwargs)

    def post(self, endpoint: str, json: Any, timeout: int) -> StaticJsonResponse:
        if self._post_handler is None:
            return StaticJsonResponse()
        return self._post_handler(endpoint, json, timeout)


def load_module() -> ModuleType:
    module_path = Path(__file__).with_name("create_guardicore_rules.py")
    spec = spec_from_file_location("create_guardicore_rules", module_path)
    assert spec is not None
    assert spec.loader is not None
    module = module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def install_session_stub(
    monkeypatch: MonkeyPatch,
    module: ModuleType,
    *,
    get_handler: Any = None,
    post_handler: Any = None,
) -> SessionStub:
    session = SessionStub(get_handler=get_handler, post_handler=post_handler)
    monkeypatch.setattr(module.requests, "Session", lambda: session)
    return session


def build_guardicore_config(module: ModuleType, timeout_seconds: int = 5) -> Any:
    return module.GuardicoreConfig(
        base_url="https://gc.local",
        token=TEST_GUARDICORE_TOKEN,
        verify_ssl=True,
        timeout_seconds=timeout_seconds,
    )


def build_label_list_payload(*objects: dict[str, Any]) -> dict[str, Any]:
    return {"objects": list(objects), "total_count": len(objects)}


def assert_single_label_map_result(
    stats: Any,
    approle_map: dict[str, list[str]],
    *,
    role_id: str,
    role_name: str,
    label_id: str,
    include_full_value_key: bool = False,
) -> None:
    assert approle_map[role_id] == [label_id]
    assert approle_map[role_name] == [label_id]
    assert stats.total_approle_labels == 1
    assert stats.unique_role_name_keys == 1
    assert stats.unique_role_id_keys == 1
    assert stats.approle_candidates_seen == 1
    if include_full_value_key:
        assert stats.unique_full_value_keys == 1
        assert stats.pages_fetched == 1
        assert stats.raw_label_objects_seen == 1
        assert stats.label_candidates_seen == 1


def fetch_approle_map_for_payload(
    monkeypatch: MonkeyPatch,
    module: ModuleType,
    *objects: dict[str, Any],
) -> tuple[dict[str, list[str]], Any]:
    def get_handler(_kwargs: dict[str, Any]) -> StaticJsonResponse:
        return StaticJsonResponse(build_label_list_payload(*objects))

    install_session_stub(
        monkeypatch,
        module,
        get_handler=get_handler,
    )
    return module.fetch_guardicore_approle_map(build_guardicore_config(module))


def test_resolve_approle_labels_returns_ids_and_missing_labels():
    module = load_module()
    connection_approles = [
        {"nwgroup": {"name": "Role-A", "id_string": "AR-001"}},
        {"nwgroup": {"name": "Role-B", "id_string": "AR-002"}},
        {"nwgroup": {"name": "Role-Missing", "id_string": "AR-404"}},
    ]
    approle_map = {
        "AR-001": ["id-a1"],
        "AR-002": ["id-b1", "id-b2"],
    }

    result = module.resolve_approle_labels(connection_approles, approle_map)

    assert result.label_ids == ["id-a1", "id-b1", "id-b2"]
    assert result.missing_labels == ["Role-Missing (AR-404)"]


def test_build_rule_payload_collects_ports_ranges_and_protocols():
    module = load_module()
    connection = {
        "id": 101,
        "owner": {"app_id_external": "APP-101", "name": "NeMo"},
        "source_approles": [{"nwgroup": {"name": "SrcRole", "id_string": "AR-SRC"}}],
        "destination_approles": [{"nwgroup": {"name": "DstRole", "id_string": "AR-DST"}}],
        "services": [
            {
                "service": {
                    "port": 80,
                    "port_end": 80,
                    "protocol": {"name": "tcp"},
                }
            },
            {
                "service": {
                    "port": 53,
                    "port_end": 53,
                    "protocol": {"name": "udp"},
                }
            },
        ],
        "service_groups": [
            {
                "service_group": {
                    "services": [
                        {
                            "service": {
                                "port": 2000,
                                "port_end": 3000,
                                "protocol": {"name": "tcp"},
                            }
                        }
                    ]
                }
            }
        ],
    }
    approle_map = {
        "AR-SRC": ["src-id"],
        "AR-DST": ["dst-id"],
    }

    result = module.build_rule_payload(
        connection=connection,
        approle_id_map=approle_map,
        default_ip_protocol="TCP",
        action="ALLOW",
        section_position="ALLOW",
    )

    assert result.skip_reason is None
    assert len(result.payloads) == EXPECTED_PROTOCOL_PAYLOADS
    payload_by_protocol = {payload["ip_protocols"][0]: payload for payload in result.payloads}
    tcp_payload = payload_by_protocol["TCP"]
    udp_payload = payload_by_protocol["UDP"]

    assert tcp_payload["ruleset_name"] == "FWOA101 FWOC101"
    assert udp_payload["ruleset_name"] == "FWOA101 FWOC101"
    assert tcp_payload["ports"] == [80]
    assert tcp_payload["port_ranges"] == [{"start": 2000, "end": 3000}]
    assert udp_payload["ports"] == [53]
    assert udp_payload["port_ranges"] == []
    assert tcp_payload["source"]["labels"]["or_labels"] == [{"and_labels": ["src-id"]}]
    assert tcp_payload["destination"]["labels"]["or_labels"] == [{"and_labels": ["dst-id"]}]


def test_build_rule_payload_omits_ports_for_icmp():
    module = load_module()
    connection = {
        "id": 150,
        "source_approles": [{"nwgroup": {"name": "SrcRole", "id_string": "AR-SRC"}}],
        "destination_approles": [{"nwgroup": {"name": "DstRole", "id_string": "AR-DST"}}],
        "services": [
            {
                "service": {
                    "port": None,
                    "port_end": None,
                    "proto_id": 1,
                    "protocol": {"name": None},
                }
            }
        ],
        "service_groups": [],
    }
    approle_map = {
        "AR-SRC": ["src-id"],
        "AR-DST": ["dst-id"],
    }

    result = module.build_rule_payload(
        connection=connection,
        approle_id_map=approle_map,
        default_ip_protocol="TCP",
        action="ALLOW",
        section_position="ALLOW",
    )

    assert result.skip_reason is None
    assert len(result.payloads) == 1
    payload = result.payloads[0]
    assert payload["ip_protocols"] == ["ICMP"]
    assert "ports" not in payload
    assert "port_ranges" not in payload


def test_build_guardicore_ruleset_name_falls_back_without_owner():
    module = load_module()

    ruleset_name = module.build_guardicore_ruleset_name({"id": 42})

    assert ruleset_name == "FWOC42"


def test_strip_app_id_prefix_removes_leading_prefix_until_first_digit():
    module = load_module()

    assert module.strip_app_id_prefix("APP-5630") == "5630"
    assert module.strip_app_id_prefix("A_42") == "42"
    assert module.strip_app_id_prefix("5630") == "5630"


def test_extract_connection_policy_groups_uses_used_interface_when_direct_lists_are_empty():
    module = load_module()
    connection: dict[str, Any] = {
        "source_approles": [],
        "source_areas": [],
        "used_interface": {
            "source_approles": [{"nwgroup": {"name": "SrcRole", "id_string": "AR-SRC"}}],
            "source_areas": [{"nwgroup": {"name": "Area A", "id_string": "NA-1"}}],
        },
    }

    source_groups = module.extract_connection_policy_groups(connection, "source")

    assert source_groups == [
        {"nwgroup": {"name": "SrcRole", "id_string": "AR-SRC"}},
        {"nwgroup": {"name": "Area A", "id_string": "NA-1"}},
    ]


def test_extract_services_uses_used_interface_when_direct_lists_are_empty():
    module = load_module()
    connection: dict[str, Any] = {
        "services": [],
        "service_groups": [],
        "used_interface": {
            "services": [{"service": {"name": "HTTPS", "port": 443, "port_end": 443}}],
            "service_groups": [
                {"service_group": {"services": [{"service": {"name": "DNS", "port": 53, "port_end": 53}}]}}
            ],
        },
    }

    services = module.extract_services(connection)

    assert services == [
        {"name": "HTTPS", "port": 443, "port_end": 443},
        {"name": "DNS", "port": 53, "port_end": 53},
    ]


def test_build_rule_payload_uses_used_interface_for_empty_connection_objects():
    module = load_module()
    connection = {
        "id": 301,
        "source_approles": [],
        "destination_approles": [{"nwgroup": {"name": "DstRole", "id_string": "AR-DST"}}],
        "services": [],
        "service_groups": [],
        "used_interface": {
            "source_approles": [{"nwgroup": {"name": "SrcRole", "id_string": "AR-SRC"}}],
            "services": [{"service": {"port": 443, "port_end": 443, "protocol": {"name": "tcp"}}}],
            "service_groups": [],
        },
    }
    approle_map = {
        "AR-SRC": ["src-id"],
        "AR-DST": ["dst-id"],
    }

    result = module.build_rule_payload(
        connection=connection,
        approle_id_map=approle_map,
        default_ip_protocol="TCP",
        action="ALLOW",
        section_position="ALLOW",
    )

    assert result.skip_reason is None
    assert len(result.payloads) == 1
    payload = result.payloads[0]
    assert payload["ip_protocols"] == ["TCP"]
    assert payload["ports"] == [443]
    assert payload["source"]["labels"]["or_labels"] == [{"and_labels": ["src-id"]}]
    assert payload["destination"]["labels"]["or_labels"] == [{"and_labels": ["dst-id"]}]


def test_build_rule_payload_uses_source_areas_for_networkarea_labels():
    module = load_module()
    connection = {
        "id": 302,
        "source_approles": [],
        "source_areas": [{"nwgroup": {"name": "Area Src", "id_string": "NA-SRC"}}],
        "destination_approles": [{"nwgroup": {"name": "DstRole", "id_string": "AR-DST"}}],
        "destination_areas": [],
        "services": [{"service": {"port": 443, "port_end": 443, "protocol": {"name": "tcp"}}}],
        "service_groups": [],
    }
    approle_map = {
        "NA-SRC": ["src-na-id"],
        "AR-DST": ["dst-id"],
    }

    result = module.build_rule_payload(
        connection=connection,
        approle_id_map=approle_map,
        default_ip_protocol="TCP",
        action="ALLOW",
        section_position="ALLOW",
    )

    assert result.skip_reason is None
    assert len(result.payloads) == 1
    payload = result.payloads[0]
    assert payload["source"]["labels"]["or_labels"] == [{"and_labels": ["src-na-id"]}]
    assert payload["destination"]["labels"]["or_labels"] == [{"and_labels": ["dst-id"]}]


def test_build_rule_payload_skips_esp_protocol():
    module = load_module()
    connection = {
        "id": 151,
        "source_approles": [{"nwgroup": {"name": "SrcRole", "id_string": "AR-SRC"}}],
        "destination_approles": [{"nwgroup": {"name": "DstRole", "id_string": "AR-DST"}}],
        "services": [
            {
                "service": {
                    "port": None,
                    "port_end": None,
                    "protocol": {"name": "esp"},
                }
            }
        ],
        "service_groups": [],
    }
    approle_map = {
        "AR-SRC": ["src-id"],
        "AR-DST": ["dst-id"],
    }

    result = module.build_rule_payload(
        connection=connection,
        approle_id_map=approle_map,
        default_ip_protocol="TCP",
        action="ALLOW",
        section_position="ALLOW",
    )

    assert result.payloads == []
    assert result.skip_reason == "unsupported Guardicore ip_protocols: ['ESP']"


def test_build_rule_payload_skips_when_approle_label_is_missing():
    module = load_module()
    connection = {
        "id": 202,
        "source_approles": [{"nwgroup": {"name": "SrcRole", "id_string": "AR-SRC"}}],
        "destination_approles": [{"nwgroup": {"name": "MissingRole", "id_string": "AR-MISSING"}}],
        "services": [],
        "service_groups": [],
    }
    approle_map = {
        "AR-SRC": ["src-id"],
    }

    result = module.build_rule_payload(
        connection=connection,
        approle_id_map=approle_map,
        default_ip_protocol="TCP",
        action="ALLOW",
        section_position="ALLOW",
    )

    assert result.payloads == []
    assert (
        result.skip_reason
        == "missing Guardicore AppRole/NetworkArea labels: source=[], destination=['MissingRole (AR-MISSING)']"
    )


def test_build_rule_payload_skips_with_source_destination_identifiers_when_label_sets_empty():
    module = load_module()
    connection = {
        "id": 203,
        "source_approles": [{"nwgroup": {"name": "SrcRole", "id_string": "AR-SRC"}}],
        "destination_approles": [],
        "services": [],
        "service_groups": [],
    }
    approle_map: dict[str, list[str]] = {}

    result = module.build_rule_payload(
        connection=connection,
        approle_id_map=approle_map,
        default_ip_protocol="TCP",
        action="ALLOW",
        section_position="ALLOW",
    )

    assert result.payloads == []
    assert (
        result.skip_reason
        == "missing Guardicore AppRole/NetworkArea labels: source=['SrcRole (AR-SRC)'], destination=[]"
    )


def test_build_missing_approle_warning_details_contains_all_approles_and_connection_json():
    module = load_module()
    connection = {
        "id": 4475,
        "name": "DEV - Admin Zugriff to NeMo Entwicklungsumgebung",
        "source_approles": [
            {"nwgroup": {"name": "Admin Role", "id_string": "AR-ADMIN-1"}},
            {"nwgroup": {"name": "Admin Role 2", "id_string": "AR-ADMIN-2"}},
        ],
        "destination_approles": [
            {"nwgroup": {"name": "NeMo Entwicklung", "id_string": "AR5005630-006"}},
        ],
    }

    details = module.build_missing_approle_warning_details(connection)

    assert "all_source_approles=['Admin Role (AR-ADMIN-1)', 'Admin Role 2 (AR-ADMIN-2)']" in details
    assert "all_destination_approles=['NeMo Entwicklung (AR5005630-006)']" in details
    assert '"id": 4475' in details
    assert '"name": "DEV - Admin Zugriff to NeMo Entwicklungsumgebung"' in details


def test_collect_ports_and_protocols_uses_default_protocol_for_empty_services():
    module = load_module()

    ports, port_ranges, protocols = module.collect_ports_and_protocols([], "TCP")

    assert ports == []
    assert port_ranges == []
    assert protocols == ["TCP"]


def test_build_graphql_query_hardcodes_filters_and_uses_appids_variable():
    module = load_module()

    query = module.build_graphql_query(filter_by_app_ids=True)
    variables = module.build_graphql_variables(app_ids=["APP-1"])

    assert "removed: { _eq: false }" in query
    assert "is_interface: { _eq: false }" in query
    assert "source_areas: nwgroup_connections" in query
    assert "destination_areas: nwgroup_connections" in query
    assert "group_type: { _eq: 20 }" in query
    assert "group_type: { _eq: 23 }" in query
    assert "used_interface: connection" in query
    assert "$appIds" in query
    assert "owner: { app_id_external: { _in: $appIds } }" in query
    assert "id_string" in query
    assert variables == {"appIds": ["APP-1"]}


def test_build_graphql_query_omits_owner_filter_when_no_app_ids_provided():
    module = load_module()

    query = module.build_graphql_query(filter_by_app_ids=False)
    variables = module.build_graphql_variables(app_ids=None)

    assert "removed: { _eq: false }" in query
    assert "is_interface: { _eq: false }" in query
    assert "source_areas: nwgroup_connections" in query
    assert "destination_areas: nwgroup_connections" in query
    assert "group_type: { _eq: 20 }" in query
    assert "group_type: { _eq: 23 }" in query
    assert "used_interface: connection" in query
    assert "$appIds" not in query
    assert "owner: { app_id_external: { _in: $appIds } }" not in query
    assert variables == {}


def test_extract_id_string_from_label_value_parses_trailing_parentheses():
    module = load_module()

    parsed = module.extract_id_string_from_label_value("My App (APP-1) - Role Name (AR-1234)")

    assert parsed == "AR-1234"


def test_extract_nwgroup_name_from_label_value_parses_fwo_style_value():
    module = load_module()

    parsed = module.extract_nwgroup_name_from_label_value("My App (APP-1) - NeMo-All_Nemo_Servers (AR-1234)")

    assert parsed == "NeMo-All_Nemo_Servers"


def test_fetch_guardicore_approle_map_adds_name_and_id_aliases(monkeypatch: MonkeyPatch):
    module = load_module()
    approle_map, stats = fetch_approle_map_for_payload(
        monkeypatch,
        module,
        {
            "id": "label-1",
            "key": "AppRole",
            "value": "My App (APP-1) - NeMo-All_Nemo_Servers (AR-1234)",
        },
    )

    assert_single_label_map_result(
        stats,
        approle_map,
        role_id="AR-1234",
        role_name="NeMo-All_Nemo_Servers",
        label_id="label-1",
        include_full_value_key=True,
    )


def test_fetch_guardicore_approle_map_reads_nested_label_shape(monkeypatch: MonkeyPatch):
    module = load_module()
    approle_map, stats = fetch_approle_map_for_payload(
        monkeypatch,
        module,
        {
            "label": {
                "id": "label-2",
                "key": "AppRole:",
                "value": "NeMo (APP-5630) - NeMo Entwicklung (AR5005630-006)",
            }
        },
    )

    assert_single_label_map_result(
        stats,
        approle_map,
        role_id="AR5005630-006",
        role_name="NeMo Entwicklung",
        label_id="label-2",
    )


def test_is_guardicore_policy_label_key_accepts_spacing_and_case_variants():
    module = load_module()

    assert module.is_guardicore_policy_label_key("AppRole")
    assert module.is_guardicore_policy_label_key(" app role ")
    assert module.is_guardicore_policy_label_key("APP_ROLE:")
    assert module.is_guardicore_policy_label_key("NetworkArea")
    assert module.is_guardicore_policy_label_key(" network area ")
    assert not module.is_guardicore_policy_label_key("AppZone")


def test_fetch_guardicore_approle_map_accepts_spaced_approle_key(monkeypatch: MonkeyPatch):
    module = load_module()
    approle_map, stats = fetch_approle_map_for_payload(
        monkeypatch,
        module,
        {
            "id": "label-3",
            "key": "App Role",
            "value": "NeMo (APP-5630) - NeMo Entwicklung (AR5005630-006)",
        },
    )

    assert_single_label_map_result(
        stats,
        approle_map,
        role_id="AR5005630-006",
        role_name="NeMo Entwicklung",
        label_id="label-3",
    )


def test_is_probable_policy_label_accepts_ar_or_na_suffix_when_key_is_not_policy_key():
    module = load_module()

    assert module.is_probable_policy_label("AppZone", "NeMo (APP-5630) - NeMo Entwicklung (AR5005630-006)")
    assert module.is_probable_policy_label("AppZone", "NeMo (APP-5630) - NeMo Entwicklung (NA5005630-006)")
    assert not module.is_probable_policy_label("AppZone", "NeMo (APP-5630) - NeMo Zone (AZ5005630-001)")


def test_fetch_guardicore_approle_map_accepts_networkarea_key(monkeypatch: MonkeyPatch):
    module = load_module()
    approle_map, stats = fetch_approle_map_for_payload(
        monkeypatch,
        module,
        {
            "id": "label-na-1",
            "key": "NetworkArea",
            "value": "NeMo (APP-5630) - NeMo Entwicklung (NA5005630-006)",
        },
    )

    assert_single_label_map_result(
        stats,
        approle_map,
        role_id="NA5005630-006",
        role_name="NeMo Entwicklung",
        label_id="label-na-1",
    )


def test_fetch_guardicore_approle_map_paginates_without_total_count(monkeypatch: MonkeyPatch):
    module = load_module()

    def get_handler(kwargs: dict[str, Any]) -> StaticJsonResponse:
        params = kwargs["params"]
        offset = params["start_at"]
        if offset == 0:
            return StaticJsonResponse(
                {
                    "objects": [
                        {"id": "label-1", "key": "AppRole", "value": "App A (APP-1) - Role A (AR-1)"},
                        {"id": "label-2", "key": "AppRole", "value": "App B (APP-2) - Role B (AR-2)"},
                    ]
                }
            )
        if offset == EXPECTED_SECOND_OFFSET:
            return StaticJsonResponse(
                {
                    "objects": [
                        {"id": "label-3", "key": "AppRole", "value": "App C (APP-3) - Role C (AR-3)"},
                    ]
                }
            )
        return StaticJsonResponse({"objects": []})

    install_session_stub(monkeypatch, module, get_handler=get_handler)
    approle_map, stats = module.fetch_guardicore_approle_map(build_guardicore_config(module))

    assert approle_map["AR-1"] == ["label-1"]
    assert approle_map["AR-2"] == ["label-2"]
    assert approle_map["AR-3"] == ["label-3"]
    assert approle_map["Role C"] == ["label-3"]
    assert stats.total_approle_labels == EXPECTED_PAGINATED_LABEL_COUNT
    assert stats.unique_role_name_keys == EXPECTED_PAGINATED_LABEL_COUNT
    assert stats.unique_role_id_keys == EXPECTED_PAGINATED_LABEL_COUNT
    assert stats.pages_fetched == EXPECTED_PAGINATED_LABEL_COUNT
    assert stats.raw_label_objects_seen == EXPECTED_PAGINATED_LABEL_COUNT
    assert stats.label_candidates_seen == EXPECTED_PAGINATED_LABEL_COUNT
    assert stats.approle_candidates_seen == EXPECTED_PAGINATED_LABEL_COUNT


def test_collect_ports_and_protocols_by_protocol_uses_proto_id_fallback_for_udp():
    module = load_module()
    services = [
        {"port": 53, "port_end": 53, "proto_id": 17, "protocol": None},
        {"port": 80, "port_end": 80, "proto_id": 6, "protocol": None},
    ]

    by_protocol = module.collect_ports_and_protocols_by_protocol(services, "TCP")

    assert by_protocol["UDP"] == ([53], [])
    assert by_protocol["TCP"] == ([80], [])


def test_collect_ports_and_protocols_by_protocol_uses_string_proto_id_fallback():
    module = load_module()
    services = [
        {"port": 53, "port_end": 53, "proto_id": "17", "protocol": None},
    ]

    by_protocol = module.collect_ports_and_protocols_by_protocol(services, "TCP")

    assert by_protocol["UDP"] == ([53], [])


def test_collect_ports_and_protocols_by_protocol_uses_protocol_object_id_fallback():
    module = load_module()
    services = [
        {"port": 53, "port_end": 53, "proto_id": None, "protocol": {"id": "17", "name": None}},
    ]

    by_protocol = module.collect_ports_and_protocols_by_protocol(services, "TCP")

    assert by_protocol["UDP"] == ([53], [])


def test_post_guardicore_revision_skips_http_call_for_empty_rulesets(monkeypatch: MonkeyPatch):
    module = load_module()

    class FailingSessionFactory:
        def __call__(self) -> None:
            raise AssertionError("requests.Session() must not be called for empty rulesets")

    monkeypatch.setattr(module.requests, "Session", FailingSessionFactory())
    module.post_guardicore_revision(build_guardicore_config(module), [], "comment")


def test_post_guardicore_revision_posts_once_with_comment_only(monkeypatch: MonkeyPatch):
    module = load_module()
    captured_calls: list[dict[str, Any]] = []

    def post_handler(endpoint: str, json: dict[str, Any], timeout: int) -> StaticJsonResponse:
        captured_calls.append({"endpoint": endpoint, "json": json, "timeout": timeout})
        return StaticJsonResponse()

    install_session_stub(monkeypatch, module, post_handler=post_handler)
    module.post_guardicore_revision(
        build_guardicore_config(module, timeout_seconds=EXPECTED_PUBLISH_TIMEOUT),
        ["FWOC3", "FWOC4"],
        "published rules by NeMo",
    )

    assert len(captured_calls) == 1
    assert captured_calls[0]["endpoint"] == "https://gc.local/api/v4.0/visibility/policy/revisions"
    assert captured_calls[0]["timeout"] == EXPECTED_PUBLISH_TIMEOUT
    assert captured_calls[0]["json"] == {"comments": "published rules by NeMo"}


def test_auth_validation_and_token_helpers(monkeypatch: MonkeyPatch):
    module = load_module()
    args = SimpleNamespace(
        fwo_jwt=None,
        fwo_user=None,
        fwo_password=None,
        fwo_middleware_url=None,
        guardicore_token=None,
        guardicore_user=None,
        guardicore_password=None,
        guardicore_url="https://gc",
        timeout=5,
    )

    with pytest.raises(module.GuardicoreRuleProvisioningError, match="--fwo-user"):
        module.require_login_fields(args)
    args.fwo_jwt = "jwt"
    module.require_login_fields(args)
    with pytest.raises(module.GuardicoreRuleProvisioningError, match="--guardicore-user"):
        module.require_guardicore_fields(args)
    args.guardicore_token = "gc-token"
    module.require_guardicore_fields(args)

    assert module.get_fwo_jwt(args, True) == "jwt"
    assert module.get_guardicore_token(args, True) == "gc-token"

    args.fwo_jwt = None
    args.fwo_user = "user"
    args.fwo_password = "password"
    args.fwo_middleware_url = "https://middleware"
    args.guardicore_token = None
    args.guardicore_user = "user"
    args.guardicore_password = "password"

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

    def fake_login_guardicore(
        user: str,
        password: str,
        base_url: str,
        verify_ssl: bool | str,
        timeout: int,
        error_cls: type[Exception],
    ) -> str:
        del user, password, base_url, verify_ssl, timeout, error_cls
        return "login-gc-token"

    monkeypatch.setattr(module, "login_fwo", fake_login_fwo)
    monkeypatch.setattr(module, "login_guardicore", fake_login_guardicore)

    assert module.get_fwo_jwt(args, False) == "login-jwt"
    assert module.get_guardicore_token(args, False) == "login-gc-token"


def test_fetch_connections_from_fwo_filters_invalid_payloads(monkeypatch: MonkeyPatch):
    module = load_module()
    config = module.FwoConfig("https://fwo/graphql", "jwt", True, 5, "reporter")
    responses = [
        {},
        {"data": {"modelling_connection": "bad"}},
        {"data": {"modelling_connection": [{"id": 1}, "ignored"]}},
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

    assert module.fetch_connections_from_fwo(config, None) == []
    assert module.fetch_connections_from_fwo(config, None) == []
    assert module.fetch_connections_from_fwo(config, ["APP-1"]) == [{"id": 1}]


def test_label_candidate_and_pagination_helpers():
    module = load_module()
    item = {"id": 1, "key": "AppRole", "value": "App (APP-1) - Role (AR-1)"}
    nested_item = {"label": {"id": "2", "key": "NetworkArea", "value": "Area (NA-1)"}}
    state = module.AppRoleMapBuildState(
        app_role_map={},
        full_value_keys=set(),
        role_name_keys=set(),
        role_id_keys=set(),
        label_ids_seen=set(),
        approle_full_value_counter=module.Counter(),
    )

    assert module.parse_existing_labels({"objects": [item]}) == [item]
    assert module.extract_label_id_key_value_candidates({"id": "", "key": "AppRole", "value": "Role"}) == []
    assert module.extract_label_id_key_value_candidates(nested_item) == [("2", "NetworkArea", "Area (NA-1)")]
    assert module.build_guardicore_label_query_params({}, "offset", 5, 2) == {"start_at": 5, "offset": 5}
    assert module.build_guardicore_label_query_params({}, "page", 5, 2) == {"page": 2}
    with pytest.raises(module.GuardicoreRuleProvisioningError, match="Unknown pagination mode"):
        module.build_guardicore_label_query_params({}, "bad", 0, 0)

    module.update_approle_map_state(state, [item, nested_item, {"id": "3", "key": "AppZone", "value": "Zone (AZ-1)"}])

    assert state.app_role_map["AR-1"] == ["1"]
    assert state.app_role_map["NA-1"] == ["2"]
    assert module.extract_page_objects("bad") == (None, [])
    assert module.extract_page_objects({"objects": "bad"}) == ({"objects": "bad"}, [])
    assert module.determine_page_progress("page", {}, [], 0, 1) == module.PageProgress(0, 2, True)
    assert module.determine_page_progress("offset", {"next_offset": 10}, [{}], 0, 1) == module.PageProgress(10, 1, True)

    stats = module.build_approle_map_stats(state, page_count=1, pagination_mode="page", page_start=1)
    assert stats.total_approle_labels == 2
    assert stats.pagination_mode == "page:1"


def test_runtime_config_logging_and_processing_helpers(monkeypatch: MonkeyPatch):
    module = load_module()
    args = SimpleNamespace(
        fwo_graphql_url="https://fwo/graphql",
        fwo_role="reporter",
        guardicore_url="https://gc",
        timeout=5,
        dry_run=True,
        default_ip_protocol="TCP",
        action="ALLOW",
        section_position="ALLOW",
        publish_comments="publish",
    )

    class FakeLogger:
        def info(self, message: str, *args: Any, **kwargs: Any) -> None:
            del message, args, kwargs

        def warning(self, message: str, *args: Any, **kwargs: Any) -> None:
            del message, args, kwargs

    logger = FakeLogger()
    fwo_config, guardicore_config = module.build_runtime_configs(args, True, False, "jwt", "gc-token")

    assert fwo_config.jwt == "jwt"
    assert guardicore_config.verify_ssl is False

    module.log_fetch_summary(logger, [], None)
    module.log_fetch_summary(logger, [{"id": 1}], ["APP-1"])
    module.log_skipped_connection(logger, {"id": 1, "name": "Conn"}, "missing AppRole")
    module.log_skipped_connection(logger, {"id": 2, "name": "Conn"}, "other")

    payload = {"ruleset_name": "ruleset", "ip_protocols": ["TCP"]}
    assert module.apply_rule_payloads(args, logger, guardicore_config, {"id": 1}, [payload]) == {"ruleset"}

    def fake_post_guardicore_revision(config: Any, rulesets: list[str], comments: str) -> None:
        del config, rulesets, comments

    monkeypatch.setattr(module, "post_guardicore_revision", fake_post_guardicore_revision)
    module.publish_revision_if_needed(args, logger, guardicore_config, ["ruleset"])
    args.dry_run = False
    module.publish_revision_if_needed(args, logger, guardicore_config, ["ruleset"])
    args.dry_run = True

    connection = {
        "id": 1,
        "source_approles": [{"nwgroup": {"name": "Src", "id_string": "AR-SRC"}}],
        "destination_approles": [{"nwgroup": {"name": "Dst", "id_string": "AR-DST"}}],
        "services": [{"service": {"port": 443, "port_end": 443, "protocol": {"name": "tcp"}}}],
        "service_groups": [],
    }
    created, skipped, rulesets = module.process_connections(
        args,
        logger,
        [
            connection,
            {"id": 2, "source_approles": [], "destination_approles": [], "services": [], "service_groups": []},
        ],
        {"AR-SRC": ["src"], "AR-DST": ["dst"]},
        guardicore_config,
    )

    assert created == 1
    assert skipped == 1
    assert rulesets == ["FWOC1"]

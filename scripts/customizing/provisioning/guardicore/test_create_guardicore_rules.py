from __future__ import annotations

import sys
from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    from types import ModuleType, TracebackType
    from typing import Self

    from _pytest.monkeypatch import MonkeyPatch

EXPECTED_PROTOCOL_PAYLOADS = 2
EXPECTED_PUBLISH_TIMEOUT = 15
TEST_GUARDICORE_TOKEN = "guardicore_token_for_tests"  # This is a dummy token for testing purposes only. It does not grant any access. #NOQA: S105


def load_module() -> ModuleType:
    module_path = Path(__file__).with_name("create_guardicore_rules.py")
    spec = spec_from_file_location("create_guardicore_rules", module_path)
    assert spec is not None
    assert spec.loader is not None
    module = module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def test_resolve_approle_labels_returns_ids_and_missing_names():
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
    assert result.missing_names == ["Role-Missing"]


def test_build_rule_payload_collects_ports_ranges_and_protocols():
    module = load_module()
    connection = {
        "id": 101,
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

    assert tcp_payload["ruleset_name"] == "FWOC101"
    assert udp_payload["ruleset_name"] == "FWOC101"
    assert tcp_payload["ports"] == [80]
    assert tcp_payload["port_ranges"] == [{"start": 2000, "end": 3000}]
    assert udp_payload["ports"] == [53]
    assert udp_payload["port_ranges"] == []
    assert tcp_payload["source"]["labels"]["or_labels"] == [{"and_labels": ["src-id"]}]
    assert tcp_payload["destination"]["labels"]["or_labels"] == [{"and_labels": ["dst-id"]}]


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
    assert result.skip_reason == "missing Guardicore AppRole labels: ['MissingRole']"


def test_collect_ports_and_protocols_uses_default_protocol_for_empty_services():
    module = load_module()

    ports, port_ranges, protocols = module.collect_ports_and_protocols([], "TCP")

    assert ports == []
    assert port_ranges == []
    assert protocols == ["TCP"]


def test_build_graphql_query_hardcodes_filters_and_uses_appids_variable():
    module = load_module()

    query = module.build_graphql_query()
    variables = module.build_graphql_variables(app_ids=["APP-1"])

    assert "removed: { _eq: false }" in query
    assert "is_interface: { _eq: false }" in query
    assert "owner: { app_id_external: { _in: $appIds } }" in query
    assert "id_string" in query
    assert variables == {"appIds": ["APP-1"]}


def test_extract_id_string_from_label_value_parses_trailing_parentheses():
    module = load_module()

    parsed = module.extract_id_string_from_label_value("My App (APP-1) - Role Name (AR-1234)")

    assert parsed == "AR-1234"


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
    config = module.GuardicoreConfig(
        base_url="https://gc.local",
        token=TEST_GUARDICORE_TOKEN,
        verify_ssl=True,
        timeout_seconds=5,
    )

    module.post_guardicore_revision(config, [], "comment")


def test_post_guardicore_revision_posts_rulesets_and_comment(monkeypatch: MonkeyPatch):
    module = load_module()
    captured: dict[str, Any] = {}

    class FakeResponse:
        def raise_for_status(self) -> None:
            return None

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

        def post(self, endpoint: str, json: dict[str, Any], timeout: int) -> FakeResponse:
            captured["endpoint"] = endpoint
            captured["json"] = json
            captured["timeout"] = timeout
            return FakeResponse()

    monkeypatch.setattr(module.requests, "Session", lambda: FakeSession())
    config = module.GuardicoreConfig(
        base_url="https://gc.local",
        token=TEST_GUARDICORE_TOKEN,
        verify_ssl=True,
        timeout_seconds=EXPECTED_PUBLISH_TIMEOUT,
    )

    module.post_guardicore_revision(config, ["FWOC3", "FWOC4"], "published rules by NeMo")

    assert captured["endpoint"] == "https://gc.local/api/v4.0/visibility/policy/revisions"
    assert captured["timeout"] == EXPECTED_PUBLISH_TIMEOUT
    assert captured["json"] == {"rulesets": ["FWOC3", "FWOC4"], "comments": "published rules by NeMo"}

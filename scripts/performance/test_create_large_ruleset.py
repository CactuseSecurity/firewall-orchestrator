from __future__ import annotations

import json
import sys
from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
from types import SimpleNamespace
from typing import TYPE_CHECKING, Any, cast

import pytest

if TYPE_CHECKING:
    from _pytest.capture import CaptureFixture
    from _pytest.monkeypatch import MonkeyPatch

RULEBASE_ID = 30
IMPORT_ID = 20
MANAGEMENT_ID = 10
DEVICE_ID = 20
CREATED_RULEBASE_ID = 40
CREATED_IMPORT_ID = 30
FIRST_GENERATED_ID = 101
PARSED_RULE_COUNT = 5
SUMMARY_RULE_COUNT = 2
SUMMARY_OBJECT_COUNT = 4
SUMMARY_SERVICE_COUNT = 3
COUNTER_IMPORT_ID = 77
COUNTER_CHANGE_COUNT = 1234


def load_module() -> Any:
    module_path = Path(__file__).with_name("create_large_ruleset.py")
    spec = spec_from_file_location("create_large_ruleset", module_path)
    assert spec is not None
    assert spec.loader is not None
    module = module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return cast("Any", module)


class RecordingClient:
    def __init__(self) -> None:
        self.calls: list[tuple[str, dict[str, Any]]] = []
        self.next_id = 100

    def call(self, query: str, variables: dict[str, Any] | None = None) -> dict[str, Any]:
        variables = variables or {}
        self.calls.append((query, variables))
        if "insert_import_control" in query:
            return {"insert_import_control": {"returning": [{"control_id": self._next_id()}]}}
        if "insert_import_credential" in query:
            return {"insert_import_credential": {"returning": [{"id": self._next_id()}]}}
        if "insert_management" in query:
            return {"insert_management": {"returning": [{"mgm_id": self._next_id()}]}}
        if "insert_device" in query:
            return {"insert_device": {"returning": [{"dev_id": self._next_id()}]}}
        if "insert_firewall_rulebase" in query:
            return {"insert_firewall_rulebase": {"returning": [{"id": self._next_id()}]}}
        if "insert_firewall_nw_object" in query:
            return self._returning_ids("insert_firewall_nw_object", "obj_id", len(variables["objects"]))
        if "insert_firewall_nw_service" in query:
            return self._returning_ids("insert_firewall_nw_service", "svc_id", len(variables["services"]))
        if "insert_firewall_rule(objects" in query:
            return self._returning_ids("insert_firewall_rule", "rule_id", len(variables["rules"]))
        return {}

    def _next_id(self) -> int:
        self.next_id += 1
        return self.next_id

    def _returning_ids(self, field: str, key: str, count: int) -> dict[str, Any]:
        return {field: {"returning": [{key: self._next_id()} for _ in range(count)]}}


def test_chunks_splits_rows_by_batch_size() -> None:
    module = load_module()
    rows = [{"id": index} for index in range(5)]

    assert module.chunks(rows, 2) == [[{"id": 0}, {"id": 1}], [{"id": 2}, {"id": 3}], [{"id": 4}]]


def test_build_headers_prefers_admin_secret(tmp_path: Path) -> None:
    module = load_module()
    admin_secret_file = tmp_path / "admin-secret"
    admin_secret_file.write_text("secret\n", encoding="utf-8")
    args = type(
        "Args",
        (),
        {
            "admin_secret_file": str(admin_secret_file),
            "jwt_file": None,
            "middleware_url": None,
            "user": "admin",
            "password_file": None,
            "timeout": 1,
            "insecure": False,
        },
    )()

    assert module.build_headers(args) == {"Content-Type": "application/json", "x-hasura-admin-secret": "secret"}


def test_graphql_client_raises_for_graphql_errors(monkeypatch: MonkeyPatch) -> None:
    module = load_module()

    class Response:
        def raise_for_status(self) -> None:
            return None

        def json(self) -> dict[str, Any]:
            return {"errors": [{"message": "broken"}]}

    def post(**kwargs: Any) -> Response:
        assert json.loads(kwargs["data"]) == {"query": "query", "variables": {"id": 1}}
        return Response()

    def fake_post(*_args: Any, **kwargs: Any) -> Response:
        return post(**kwargs)

    monkeypatch.setattr(module.requests, "post", fake_post)
    client = module.GraphqlClient(api_url="https://api", headers={"header": "value"}, timeout=5, verify=False)

    with pytest.raises(module.GraphqlError, match="broken"):
        client.call("query", {"id": 1})


def test_graphql_client_returns_data(monkeypatch: MonkeyPatch) -> None:
    module = load_module()

    class Response:
        def raise_for_status(self) -> None:
            return None

        def json(self) -> dict[str, Any]:
            return {"data": {"ok": True}}

    def fake_post(*_args: Any, **_kwargs: Any) -> Response:
        return Response()

    monkeypatch.setattr(module.requests, "post", fake_post)
    client = module.GraphqlClient(api_url="https://api", headers={}, timeout=5, verify=True)

    assert client.call("query") == {"ok": True}


def test_parse_args_sets_defaults(monkeypatch: MonkeyPatch) -> None:
    module = load_module()
    monkeypatch.setattr(
        sys, "argv", ["create_large_ruleset.py", "--api-url", "https://api", "--rules", str(PARSED_RULE_COUNT)]
    )

    args = module.parse_args()

    assert args.api_url == "https://api"
    assert args.rules == PARSED_RULE_COUNT
    assert args.prefix == module.DEFAULT_PREFIX
    assert args.batch_size == module.DEFAULT_BATCH_SIZE
    assert args.device_type_id == module.DEFAULT_DEVICE_TYPE_ID
    assert args.jwt_file is None
    assert args.admin_secret_file is None
    assert args.insecure is False


def test_read_password_reads_password_file(tmp_path: Path) -> None:
    module = load_module()
    password_file = tmp_path / "password"
    password_file.write_text("secret\n", encoding="utf-8")

    assert module.read_password(str(password_file)) == "secret"


def test_read_secret_reads_secret_file(tmp_path: Path) -> None:
    module = load_module()
    secret_file = tmp_path / "secret"
    secret_file.write_text("secret\n", encoding="utf-8")

    assert module.read_secret(str(secret_file)) == "secret"
    assert module.read_secret(None) is None


def test_login_posts_credentials(monkeypatch: MonkeyPatch) -> None:
    module = load_module()

    class Response:
        text = "jwt"

        def raise_for_status(self) -> None:
            return None

    captured: dict[str, Any] = {}

    def post(url: str, **kwargs: Any) -> Response:
        captured["url"] = url
        captured.update(kwargs)
        return Response()

    monkeypatch.setattr(module.requests, "post", post)

    assert module.login("https://mw/", "admin", "secret", 5, verify=False) == "jwt"
    assert captured["url"] == "https://mw/api/AuthenticationToken/GetTokenPair"
    assert json.loads(captured["data"]) == {"Username": "admin", "Password": "secret"}
    assert captured["verify"] is False


def test_build_headers_uses_existing_jwt(tmp_path: Path) -> None:
    module = load_module()
    jwt_file = tmp_path / "jwt"
    jwt_file.write_text("jwt\n", encoding="utf-8")
    args = SimpleNamespace(
        admin_secret_file=None,
        jwt_file=str(jwt_file),
        middleware_url=None,
        user="admin",
        password_file=None,
        timeout=1,
        insecure=False,
        role="reporter",
    )

    assert module.build_headers(args) == {
        "Content-Type": "application/json",
        "Authorization": "Bearer jwt",
        "x-hasura-role": "reporter",
    }


def test_build_headers_requires_auth_source() -> None:
    module = load_module()
    args = SimpleNamespace(
        admin_secret_file=None,
        jwt_file=None,
        middleware_url=None,
        user="admin",
        password_file=None,
        timeout=1,
        insecure=False,
        role="admin",
    )

    with pytest.raises(ValueError, match="Provide --jwt-file"):
        module.build_headers(args)


def test_create_management_and_device_reuses_existing_pair() -> None:
    module = load_module()
    client = RecordingClient()
    args = SimpleNamespace(management_id=7, device_id=8)

    assert module.create_management_and_device(client, args, "run") == (7, 8)
    assert client.calls == []


def test_create_management_and_device_rejects_partial_existing_pair() -> None:
    module = load_module()
    args = SimpleNamespace(management_id=7, device_id=None)

    with pytest.raises(ValueError, match="must be supplied together"):
        module.create_management_and_device(RecordingClient(), args, "run")


def test_create_management_and_device_creates_credential_management_and_device() -> None:
    module = load_module()
    client = RecordingClient()
    args = SimpleNamespace(management_id=None, device_id=None, prefix="perf", device_type_id=9)

    management_id, device_id = module.create_management_and_device(client, args, "123")

    assert (management_id, device_id) == (102, 103)
    assert [variables for _, variables in client.calls] == [
        {"name": "perf-credential-123"},
        {"name": "perf-management-123", "uid": "perf-mgm-123", "devTypeId": 9, "credentialId": 101},
        {"name": "perf-gateway-123", "uid": "perf-gw-123", "devTypeId": 9, "managementId": 102},
    ]


def test_create_import_returns_control_id() -> None:
    module = load_module()
    client = RecordingClient()

    import_id = module.create_import(client, MANAGEMENT_ID)

    assert import_id == FIRST_GENERATED_ID
    assert client.calls[0][1]["mgmId"] == MANAGEMENT_ID
    assert client.calls[0][1]["importTypeId"] == module.IMPORT_TYPE_RULE


def test_create_rulebase_and_link_rulebase_to_device() -> None:
    module = load_module()
    client = RecordingClient()

    rulebase_id = module.create_rulebase(client, "perf", "run", 10, IMPORT_ID)
    module.link_rulebase_to_device(client, 50, rulebase_id, IMPORT_ID)

    assert rulebase_id == FIRST_GENERATED_ID
    assert client.calls[0][1]["rulebase"] == {
        "name": "perf-rulebase-run",
        "uid": "perf-rb-run",
        "mgm_id": 10,
        "is_global": False,
        "created": IMPORT_ID,
    }
    assert client.calls[1][1]["link"] == {
        "gw_id": 50,
        "to_rulebase_id": 101,
        "link_type": module.LINK_TYPE_ORDERED,
        "is_initial": True,
        "is_global": False,
        "is_section": False,
        "created": IMPORT_ID,
    }


def test_create_objects_batches_rows_and_returns_ids() -> None:
    module = load_module()
    client = RecordingClient()

    object_ids = module.create_objects(client, "perf", "run", 10, 20, count=3, batch_size=2)

    assert object_ids == [101, 102, 103]
    assert [len(variables["objects"]) for _, variables in client.calls] == [2, 1]
    first_object = client.calls[0][1]["objects"][0]
    assert first_object == {
        "mgm_id": 10,
        "obj_name": "perf-host-000000",
        "obj_uid": "perf-run-host-000000",
        "obj_typ_id": module.OBJECT_TYPE_HOST,
        "obj_ip": "10.0.0.0/32",
        "obj_ip_end": "10.0.0.0/32",
        "obj_create": 20,
        "active": True,
    }


def test_create_services_batches_rows_and_returns_ids() -> None:
    module = load_module()
    client = RecordingClient()

    service_ids = module.create_services(client, "perf", "run", 10, 20, count=3, batch_size=2)

    assert service_ids == [101, 102, 103]
    assert [len(variables["services"]) for _, variables in client.calls] == [2, 1]
    assert client.calls[0][1]["services"][0] == {
        "mgm_id": 10,
        "svc_name": "perf-tcp-10000",
        "svc_uid": "perf-run-svc-000000",
        "svc_typ_id": module.SERVICE_TYPE_SIMPLE,
        "ip_proto_id": module.IP_PROTO_TCP,
        "svc_port": 10000,
        "svc_create": 20,
        "active": True,
    }


def test_create_rules_batches_rows_and_returns_ids() -> None:
    module = load_module()
    client = RecordingClient()

    rule_ids = module.create_rules(client, "perf", "run", 10, RULEBASE_ID, IMPORT_ID, count=3, batch_size=2)

    assert rule_ids == [101, 102, 103]
    assert [len(variables["rules"]) for _, variables in client.calls] == [2, 1]
    first_rule = client.calls[0][1]["rules"][0]
    assert first_rule["rule_name"] == "perf-rule-000000"
    assert first_rule["rule_uid"] == "perf-run-rule-000000"
    assert first_rule["rule_num_numeric"] == module.RULE_NUM_NUMERIC_STEP
    assert first_rule["rulebase_id"] == RULEBASE_ID
    assert first_rule["rule_create"] == IMPORT_ID
    assert first_rule["access_rule"] is True
    assert first_rule["nat_rule"] is False


def test_create_rule_metadata_batches_rows() -> None:
    module = load_module()
    client = RecordingClient()

    module.create_rule_metadata(client, "perf", "run", 10, 20, count=3, batch_size=2)

    assert [len(variables["metadata"]) for _, variables in client.calls] == [2, 1]
    assert client.calls[0][1]["metadata"][0] == {
        "mgm_id": 10,
        "rule_uid": "perf-run-rule-000000",
        "rule_created": 20,
    }


def test_create_rule_refs_builds_directional_and_resolved_refs() -> None:
    module = load_module()
    client = RecordingClient()

    module.create_rule_refs(client, [10, 11, 12], [1, 2], [20, 21], 100, 200, batch_size=2)

    assert [len(variables["ruleFroms"]) for _, variables in client.calls] == [2, 1]
    first_batch = client.calls[0][1]
    assert first_batch["ruleFroms"] == [
        {"rule_id": 10, "obj_id": 1, "rf_create": 200},
        {"rule_id": 11, "obj_id": 2, "rf_create": 200},
    ]
    assert first_batch["ruleTos"] == [
        {"rule_id": 10, "obj_id": 2, "rt_create": 200},
        {"rule_id": 11, "obj_id": 1, "rt_create": 200},
    ]
    assert first_batch["ruleServices"] == [
        {"rule_id": 10, "svc_id": 20, "rs_create": 200},
        {"rule_id": 11, "svc_id": 21, "rs_create": 200},
    ]
    assert first_batch["objectResolved"] == [
        {"mgm_id": 100, "rule_id": 10, "obj_id": 1, "created": 200},
        {"mgm_id": 100, "rule_id": 11, "obj_id": 2, "created": 200},
        {"mgm_id": 100, "rule_id": 10, "obj_id": 2, "created": 200},
        {"mgm_id": 100, "rule_id": 11, "obj_id": 1, "created": 200},
    ]
    assert first_batch["serviceResolved"] == [
        {"mgm_id": 100, "rule_id": 10, "svc_id": 20, "created": 200},
        {"mgm_id": 100, "rule_id": 11, "svc_id": 21, "created": 200},
    ]


def test_create_rule_gateway_refs_batches_rows() -> None:
    module = load_module()
    client = RecordingClient()

    module.create_rule_gateway_refs(client, [10, 11, 12], 50, 200, batch_size=2)

    assert [len(variables["rows"]) for _, variables in client.calls] == [2, 1]
    assert client.calls[0][1]["rows"][0] == {"rule_id": 10, "dev_id": 50, "created": 200}


def test_update_import_counters_writes_change_count() -> None:
    module = load_module()
    client = RecordingClient()

    module.update_import_counters(client, COUNTER_IMPORT_ID, COUNTER_CHANGE_COUNT)

    assert client.calls[0][1]["importId"] == COUNTER_IMPORT_ID
    assert client.calls[0][1]["changeCount"] == COUNTER_CHANGE_COUNT


def test_main_rejects_non_positive_counts(monkeypatch: MonkeyPatch) -> None:
    module = load_module()
    monkeypatch.setattr(
        module,
        "parse_args",
        lambda: SimpleNamespace(
            rules=0, services=1, batch_size=1, objects=module.MIN_OBJECTS_FOR_DISTINCT_REFS, insecure=False
        ),
    )

    with pytest.raises(ValueError, match="must be positive"):
        module.main()


def test_main_rejects_too_few_objects(monkeypatch: MonkeyPatch) -> None:
    module = load_module()
    monkeypatch.setattr(
        module,
        "parse_args",
        lambda: SimpleNamespace(rules=1, services=1, batch_size=1, objects=1, insecure=False),
    )

    with pytest.raises(ValueError, match="must be at least"):
        module.main()


def test_main_runs_creation_pipeline_and_prints_summary(
    monkeypatch: MonkeyPatch, capsys: CaptureFixture[str], tmp_path: Path
) -> None:
    module = load_module()
    calls: list[str] = []
    jwt_file = tmp_path / "jwt"
    jwt_file.write_text("jwt\n", encoding="utf-8")
    args = SimpleNamespace(
        rules=SUMMARY_RULE_COUNT,
        services=SUMMARY_SERVICE_COUNT,
        batch_size=10,
        objects=SUMMARY_OBJECT_COUNT,
        insecure=True,
        api_url="https://api",
        admin_secret_file=None,
        jwt_file=str(jwt_file),
        middleware_url=None,
        user="admin",
        password_file=None,
        timeout=5,
        role="admin",
        prefix="perf",
    )

    def fake_parse_args() -> SimpleNamespace:
        return args

    def fake_disable_tls_warnings(insecure: bool) -> None:
        calls.append(f"tls:{insecure}")

    def fake_create_management_and_device(*_args: Any) -> tuple[int, int]:
        return MANAGEMENT_ID, DEVICE_ID

    def fake_create_import(*_args: Any) -> int:
        return CREATED_IMPORT_ID

    def fake_create_rulebase(*_args: Any) -> int:
        return CREATED_RULEBASE_ID

    def fake_link_rulebase_to_device(*_args: Any) -> None:
        calls.append("link")

    def fake_create_objects(*_args: Any) -> list[int]:
        return [100, 101, 102, 103]

    def fake_create_services(*_args: Any) -> list[int]:
        return [200, 201, 202]

    def fake_create_rule_metadata(*_args: Any) -> None:
        calls.append("metadata")

    def fake_create_rules(*_args: Any) -> list[int]:
        return [300, 301]

    def fake_create_rule_refs(*_args: Any) -> None:
        calls.append("refs")

    def fake_create_rule_gateway_refs(*_args: Any) -> None:
        calls.append("gateways")

    def fake_update_import_counters(*_args: Any) -> None:
        calls.append("counters")

    monkeypatch.setattr(module, "parse_args", fake_parse_args)
    monkeypatch.setattr(module, "disable_tls_warnings_if_requested", fake_disable_tls_warnings)
    monkeypatch.setattr(module, "create_management_and_device", fake_create_management_and_device)
    monkeypatch.setattr(module, "create_import", fake_create_import)
    monkeypatch.setattr(module, "create_rulebase", fake_create_rulebase)
    monkeypatch.setattr(module, "link_rulebase_to_device", fake_link_rulebase_to_device)
    monkeypatch.setattr(module, "create_objects", fake_create_objects)
    monkeypatch.setattr(module, "create_services", fake_create_services)
    monkeypatch.setattr(module, "create_rule_metadata", fake_create_rule_metadata)
    monkeypatch.setattr(module, "create_rules", fake_create_rules)
    monkeypatch.setattr(module, "create_rule_refs", fake_create_rule_refs)
    monkeypatch.setattr(module, "create_rule_gateway_refs", fake_create_rule_gateway_refs)
    monkeypatch.setattr(module, "update_import_counters", fake_update_import_counters)

    assert module.main() == 0

    summary = json.loads(capsys.readouterr().out)
    assert calls == ["tls:True", "link", "metadata", "refs", "gateways", "counters"]
    assert summary["management_id"] == MANAGEMENT_ID
    assert summary["device_id"] == DEVICE_ID
    assert summary["import_id"] == CREATED_IMPORT_ID
    assert summary["rulebase_id"] == CREATED_RULEBASE_ID
    assert summary["rules"] == SUMMARY_RULE_COUNT
    assert summary["objects"] == SUMMARY_OBJECT_COUNT
    assert summary["services"] == SUMMARY_SERVICE_COUNT

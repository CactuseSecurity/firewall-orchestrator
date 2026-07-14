from __future__ import annotations

import json
from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    from pathlib import Path

    import pytest

from scripts.customizing.housekeeping import delete_unused_owners as module

REQUESTED_INTERFACE_OWNER_ID: int = 7
REQUESTED_INTERFACE_REFERENCE_COUNT: int = 2
INACTIVE_OWNER_COUNT: int = 2
OWNER_NETWORK_REFERENCE_COUNT: int = 2


def fake_resolve_credentials(*_: object) -> tuple[str, str]:
    return "user", "password"


def fake_login(*_: object, **__: object) -> str:
    return "jwt"


def fake_fetch_blocked_candidate(*_: object, **__: object) -> list[module.InactiveOwnerCandidate]:
    return [module.parse_owner_candidate(build_owner_payload(5, {"reports": 1}))]


def fake_fetch_mixed_candidates(*_: object, **__: object) -> list[module.InactiveOwnerCandidate]:
    return [
        module.parse_owner_candidate(build_owner_payload(3, name="DeleteMe")),
        module.parse_owner_candidate(build_owner_payload(4, {"connectionsByProposedAppId": 1}, name="KeepMe")),
    ]


def fake_delete_owner(
    _graphql_url: str,
    _jwt: str,
    owner_id: int,
    *,
    deleted_ids: list[int],
) -> dict[str, Any]:
    deleted_ids.append(owner_id)
    return {"id": owner_id, "name": "DeleteMe"}


def build_owner_payload(
    owner_id: int,
    reference_overrides: dict[str, int] | None = None,
    *,
    name: str = "Owner",
    app_id_external: str | None = "APP-1",
) -> dict[str, Any]:
    counts: dict[str, int] = dict.fromkeys(
        module.OWNER_REFERENCE_RELATIONS + module.OPTIONAL_REFERENCE_RELATIONS,
        0,
    )
    if reference_overrides:
        counts.update(reference_overrides)

    payload: dict[str, Any] = {
        "id": owner_id,
        "name": name,
        "app_id_external": app_id_external,
        "owner_lifecycle_state": {"name": "End of Life", "active_state": False},
    }
    relation_name: str
    for relation_name, count in counts.items():
        payload[f"{relation_name}_aggregate"] = {"aggregate": {"count": count}}
    return payload


def test_build_inactive_owners_query_without_owner_filter_excludes_owner_ids_variable() -> None:
    query = module.build_inactive_owners_query(filter_by_owner_ids=False, include_owner_network_references=False)

    assert "$ownerIds" not in query
    assert "active: { _eq: false }" in query
    assert "connectionsByProposedAppId_aggregate" in query
    assert "owner_networks_aggregate" not in query


def test_build_inactive_owners_query_with_owner_filter_includes_owner_ids_variable() -> None:
    query = module.build_inactive_owners_query(filter_by_owner_ids=True, include_owner_network_references=False)

    assert "($ownerIds: [Int!])" in query
    assert "id: { _in: $ownerIds }" in query


def test_build_inactive_owners_query_includes_owner_networks_when_requested() -> None:
    query = module.build_inactive_owners_query(filter_by_owner_ids=False, include_owner_network_references=True)

    assert "owner_networks_aggregate" in query


def test_parse_owner_candidate_marks_requested_interfaces_as_reference() -> None:
    candidate = module.parse_owner_candidate(
        build_owner_payload(
            REQUESTED_INTERFACE_OWNER_ID,
            {"connectionsByProposedAppId": REQUESTED_INTERFACE_REFERENCE_COUNT},
        )
    )

    assert candidate.owner_id == REQUESTED_INTERFACE_OWNER_ID
    assert not candidate.can_be_deleted
    assert candidate.references.total_references == REQUESTED_INTERFACE_REFERENCE_COUNT
    assert candidate.references.non_zero_counts() == {"connectionsByProposedAppId": REQUESTED_INTERFACE_REFERENCE_COUNT}


def test_parse_owner_candidate_without_references_can_be_deleted() -> None:
    candidate = module.parse_owner_candidate(build_owner_payload(11))

    assert candidate.can_be_deleted
    assert candidate.references.total_references == 0
    assert candidate.references.non_zero_counts() == {}


def test_parse_owner_candidate_ignores_owner_responsibles_for_deletion() -> None:
    payload = build_owner_payload(12)
    payload["owner_responsibles_aggregate"] = {"aggregate": {"count": 3}}

    candidate = module.parse_owner_candidate(payload)

    assert candidate.can_be_deleted
    assert candidate.references.total_references == 0
    assert candidate.references.non_zero_counts() == {}


def test_parse_owner_candidate_ignores_owner_networks_by_default() -> None:
    payload = build_owner_payload(13, {"owner_networks": OWNER_NETWORK_REFERENCE_COUNT})

    candidate = module.parse_owner_candidate(payload)

    assert candidate.can_be_deleted
    assert candidate.references.total_references == 0
    assert candidate.references.non_zero_counts() == {}


def test_parse_owner_candidate_counts_owner_networks_when_enabled() -> None:
    payload = build_owner_payload(14, {"owner_networks": OWNER_NETWORK_REFERENCE_COUNT})

    candidate = module.parse_owner_candidate(payload, include_owner_network_references=True)

    assert not candidate.can_be_deleted
    assert candidate.references.total_references == OWNER_NETWORK_REFERENCE_COUNT
    assert candidate.references.non_zero_counts() == {"owner_networks": OWNER_NETWORK_REFERENCE_COUNT}


def test_extract_aggregate_count_handles_missing_or_invalid_payloads() -> None:
    assert module.extract_aggregate_count({}, "reports") == 0
    assert module.extract_aggregate_count({"reports_aggregate": {}}, "reports") == 0
    assert module.extract_aggregate_count({"reports_aggregate": {"aggregate": {"count": "1"}}}, "reports") == 0


def test_parse_owner_candidate_normalizes_blank_optional_fields() -> None:
    payload = build_owner_payload(15, app_id_external=" ")
    payload["owner_lifecycle_state"] = {"name": " ", "active_state": "false"}

    candidate = module.parse_owner_candidate(payload)

    assert candidate.app_id_external is None
    assert candidate.lifecycle_state_name is None
    assert candidate.lifecycle_state_active is None


def test_fetch_inactive_owner_candidates_handles_empty_and_populated_response(monkeypatch: pytest.MonkeyPatch) -> None:
    call_results: list[dict[str, Any] | None] = [
        None,
        {"data": {"owner": [build_owner_payload(16)]}},
    ]

    def fake_call(
        graphql_url: str,
        jwt: str,
        query: str,
        query_variables: dict[str, Any] | str = "",
        role: str = "reporter",
    ) -> dict[str, Any] | None:
        del graphql_url, jwt, query, query_variables, role
        return call_results.pop(0)

    monkeypatch.setattr(module, "call", fake_call)

    assert module.fetch_inactive_owner_candidates("https://fwo/graphql", "jwt", None) == []

    candidates = module.fetch_inactive_owner_candidates("https://fwo/graphql", "jwt", [16])

    assert [candidate.owner_id for candidate in candidates] == [16]


def test_delete_owner_handles_success_and_invalid_responses(monkeypatch: pytest.MonkeyPatch) -> None:
    call_results: list[dict[str, Any] | None] = [
        {"data": {"delete_owner_by_pk": {"id": 17, "name": "Unused"}}},
        None,
        {"data": {"delete_owner_by_pk": None}},
    ]

    def fake_call(
        graphql_url: str,
        jwt: str,
        query: str,
        query_variables: dict[str, Any] | str = "",
        role: str = "reporter",
    ) -> dict[str, Any] | None:
        del graphql_url, jwt, query, query_variables, role
        return call_results.pop(0)

    monkeypatch.setattr(module, "call", fake_call)

    assert module.delete_owner("https://fwo/graphql", "jwt", 17) == {"id": 17, "name": "Unused"}

    try:
        module.delete_owner("https://fwo/graphql", "jwt", 18)
    except module.CustomizingError as exc:
        assert "returned no response" in str(exc)

    try:
        module.delete_owner("https://fwo/graphql", "jwt", 19)
    except module.CustomizingError as exc:
        assert "returned no deleted owner payload" in str(exc)


def test_build_report_payload_separates_deletable_and_blocked_owners() -> None:
    deletable_candidate = module.parse_owner_candidate(build_owner_payload(1, name="Unused"))
    blocked_candidate = module.parse_owner_candidate(build_owner_payload(2, {"reqtask_owners": 1}, name="Blocked"))

    payload = module.build_report_payload([deletable_candidate, blocked_candidate])

    assert payload["inactive_owner_count"] == INACTIVE_OWNER_COUNT
    assert payload["deletable_owner_count"] == 1
    assert payload["blocked_owner_count"] == 1
    assert payload["deletable_owners"][0]["id"] == 1
    assert payload["blocked_owners"][0]["references"] == {"reqtask_owners": 1}


def test_log_candidates_handles_empty_list(caplog: pytest.LogCaptureFixture) -> None:
    logger = module.get_logger(0)

    with caplog.at_level("INFO"):
        module.log_candidates(logger, [], execute=False)

    assert "No inactive owners found" in caplog.text


def test_run_cleanup_dry_run_exits_with_reference_status(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    config_path = tmp_path / "fworch.json"
    config_path.write_text(
        json.dumps({"middleware_uri": "https://fwo/", "api_uri": "https://fwo/graphql"}), encoding="utf-8"
    )

    monkeypatch.setattr(module, "resolve_credentials", fake_resolve_credentials)
    monkeypatch.setattr(module, "login", fake_login)
    monkeypatch.setattr(module, "fetch_inactive_owner_candidates", fake_fetch_blocked_candidate)

    args = module.parse_args(["--config-file", str(config_path), "--fail-on-references"])
    exit_code = module.run_cleanup(args, module.get_logger(0))

    assert exit_code == module.EXIT_CODE_REFERENCES_FOUND


def test_run_cleanup_execute_deletes_only_unreferenced_owners(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    config_path = tmp_path / "fworch.json"
    config_path.write_text(
        json.dumps({"middleware_uri": "https://fwo/", "api_uri": "https://fwo/graphql"}), encoding="utf-8"
    )
    deleted_ids: list[int] = []

    monkeypatch.setattr(module, "resolve_credentials", fake_resolve_credentials)
    monkeypatch.setattr(module, "login", fake_login)
    monkeypatch.setattr(module, "fetch_inactive_owner_candidates", fake_fetch_mixed_candidates)

    def fake_delete_owner_with_capture(graphql_url: str, jwt: str, owner_id: int) -> dict[str, Any]:
        return fake_delete_owner(
            graphql_url,
            jwt,
            owner_id,
            deleted_ids=deleted_ids,
        )

    monkeypatch.setattr(
        module,
        "delete_owner",
        fake_delete_owner_with_capture,
    )

    args = module.parse_args(["--config-file", str(config_path), "--execute"])
    exit_code = module.run_cleanup(args, module.get_logger(0))

    assert exit_code == 0
    assert deleted_ids == [3]


def test_main_returns_failure_when_cleanup_raises(monkeypatch: pytest.MonkeyPatch) -> None:
    def fake_run_cleanup(args: Any, logger: Any) -> int:
        del args, logger
        raise module.CustomizingError("failed")

    monkeypatch.setattr(
        module,
        "run_cleanup",
        fake_run_cleanup,
    )

    assert module.main([]) == 1


def test_parse_args_owner_network_references_default_to_ignored() -> None:
    args = module.parse_args([])

    assert args.include_owner_network_references is False


def test_parse_args_owner_network_references_can_be_enabled() -> None:
    args = module.parse_args(["--include-owner-network-references"])

    assert args.include_owner_network_references is True

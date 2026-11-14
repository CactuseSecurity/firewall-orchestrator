from __future__ import annotations

from typing import TYPE_CHECKING, Any
from unittest.mock import MagicMock

import fwo_const
import pytest
from fwo_exceptions import FwoApiWriteError
from model_controllers.rule_hit_import import RuleHitImport, StoredRuleHits, chunked, is_later
from models.rule_hit import RuleHit
from test.utils.test_utils import mock_get_graphql_code

if TYPE_CHECKING:
    from model_controllers.import_state_controller import ImportStateController
    from pytest_mock import MockerFixture

MGM_ID = 3
DEV_ID = 21
OTHER_DEV_ID = 22
OLD_HIT = "2026-11-01T00:00:00+00:00"
NEW_HIT = "2026-11-06T08:15:00+00:00"
NEWER_HIT = "2026-11-06T09:00:00+00:00"
STORED_RULES_QUERY = "query getRuleHitsOfManagements"
UPSERT_MUTATION = "mutation upsertRuleHits"
UPDATE_MUTATION = "mutation updateLasthits"


def _rule_hit(rule_uid: str, gateway_uid: str, last_hit: str, gateway_mgm_id: int = MGM_ID) -> RuleHit:
    return RuleHit(
        rule_mgm_id=MGM_ID, rule_uid=rule_uid, gateway_mgm_id=gateway_mgm_id, gateway_uid=gateway_uid, last_hit=last_hit
    )


def _stored_rule(
    rule_metadata_id: int, rule_uid: str, rule_last_hit: str | None, hits: list[dict[str, Any]]
) -> dict[str, Any]:
    return {
        "rule_metadata_id": rule_metadata_id,
        "mgm_id": MGM_ID,
        "rule_uid": rule_uid,
        "rule_last_hit": rule_last_hit,
        "rule_hits": hits,
    }


@pytest.fixture
def api() -> MagicMock:
    return MagicMock()


@pytest.fixture
def rule_hit_import(
    import_state_controller: ImportStateController, api: MagicMock, mocker: MockerFixture
) -> RuleHitImport:
    def graphql_code(paths: list[str]) -> str:
        return {
            "rule_metadata/getRuleHitsOfManagements.graphql": STORED_RULES_QUERY,
            "rule_metadata/upsertRuleHits.graphql": UPSERT_MUTATION,
            "rule_metadata/updateLastHits.graphql": UPDATE_MUTATION,
        }[paths[0].removeprefix(fwo_const.GRAPHQL_QUERY_PATH)]

    mock_get_graphql_code(mocker, None).side_effect = graphql_code
    import_state_controller.api_call = api
    import_state_controller.state.management_map = {"mock-uid": MGM_ID}
    import_state_controller.state.gateway_map = {MGM_ID: {"gw-1": DEV_ID, "gw-2": OTHER_DEV_ID}}
    return RuleHitImport(import_state_controller)


def _respond(api: MagicMock, stored_rules: list[dict[str, Any]]) -> None:
    def call(query: str, query_variables: dict[str, Any]) -> dict[str, Any]:
        if query == STORED_RULES_QUERY:
            return {"data": {"firewall_rule_metadata": stored_rules}}
        if query == UPSERT_MUTATION:
            return {"data": {"insert_firewall_rule_hit": {"affected_rows": len(query_variables["ruleHits"])}}}
        return {
            "data": {"update_firewall_rule_metadata_many": [{"affected_rows": 1} for _ in query_variables["hit_info"]]}
        }

    api.call.side_effect = call


def _calls(api: MagicMock, query: str) -> list[dict[str, Any]]:
    return [call.kwargs["query_variables"] for call in api.call.call_args_list if call.args[0] == query]


def test_changed_gateway_hits_and_later_rule_last_hits_are_written(
    rule_hit_import: RuleHitImport, api: MagicMock
) -> None:
    _respond(
        api,
        [
            # gateway hit unchanged: nothing written
            _stored_rule(1, "r1", NEW_HIT, [{"dev_id": DEV_ID, "rule_last_hit": "2026-11-06T08:15:00+00:00"}]),
            # new gateway hit later than the stored rule last hit (stored without timezone = UTC)
            _stored_rule(2, "r2", "2026-11-01T00:00:00", []),
            # never hit before
            _stored_rule(3, "r3", None, []),
        ],
    )

    result = rule_hit_import.write_rule_hits(
        [
            _rule_hit("r1", "gw-1", NEW_HIT),
            _rule_hit("r2", "gw-2", NEW_HIT),
            _rule_hit("r3", "gw-1", OLD_HIT),
        ]
    )

    assert result.gateway_hits_written == 2
    assert result.rule_last_hits_written == 2
    upserted = _calls(api, UPSERT_MUTATION)[0]["ruleHits"]
    assert upserted == [
        {"rule_metadata_id": 2, "dev_id": OTHER_DEV_ID, "rule_last_hit": NEW_HIT},
        {"rule_metadata_id": 3, "dev_id": DEV_ID, "rule_last_hit": OLD_HIT},
    ]
    updates = _calls(api, UPDATE_MUTATION)[0]["hit_info"]
    assert updates == [
        {"where": {"rule_metadata_id": {"_eq": 2}}, "_set": {"rule_last_hit": NEW_HIT}},
        {"where": {"rule_metadata_id": {"_eq": 3}}, "_set": {"rule_last_hit": OLD_HIT}},
    ]


def test_rule_last_hit_never_moves_backwards(rule_hit_import: RuleHitImport, api: MagicMock) -> None:
    _respond(api, [_stored_rule(1, "r1", NEWER_HIT, [{"dev_id": DEV_ID, "rule_last_hit": OLD_HIT}])])

    result = rule_hit_import.write_rule_hits([_rule_hit("r1", "gw-1", NEW_HIT)])

    # the changed gateway hit is stored, but the later stored rule last hit is kept
    assert result.gateway_hits_written == 1
    assert result.rule_last_hits_written == 0
    assert _calls(api, UPDATE_MUTATION) == []


def test_unknown_gateways_and_rules_are_skipped(rule_hit_import: RuleHitImport, api: MagicMock) -> None:
    _respond(api, [_stored_rule(1, "r1", None, [])])

    result = rule_hit_import.write_rule_hits(
        [
            _rule_hit("r1", "gw-unknown", NEW_HIT),
            _rule_hit("r1", "gw-1", NEW_HIT, gateway_mgm_id=99),
            _rule_hit("new-rule", "gw-1", NEW_HIT),
        ]
    )

    assert result.gateway_hits_written == 0
    assert result.rule_last_hits_written == 0


def test_a_rule_reported_twice_for_a_gateway_counts_with_its_latest_hit(
    rule_hit_import: RuleHitImport, api: MagicMock
) -> None:
    _respond(api, [_stored_rule(1, "r1", None, [])])

    rule_hit_import.write_rule_hits(
        [_rule_hit("r1", "gw-1", NEWER_HIT), _rule_hit("r1", "gw-1", OLD_HIT), _rule_hit("r1", "gw-2", NEW_HIT)]
    )

    upserted = _calls(api, UPSERT_MUTATION)[0]["ruleHits"]
    assert upserted == [
        {"rule_metadata_id": 1, "dev_id": DEV_ID, "rule_last_hit": NEWER_HIT},
        {"rule_metadata_id": 1, "dev_id": OTHER_DEV_ID, "rule_last_hit": NEW_HIT},
    ]
    assert _calls(api, UPDATE_MUTATION)[0]["hit_info"][0]["_set"] == {"rule_last_hit": NEWER_HIT}


def test_no_rule_hits_writes_nothing(rule_hit_import: RuleHitImport, api: MagicMock) -> None:
    result = rule_hit_import.write_rule_hits([])

    assert result.gateway_hits_written == 0
    api.call.assert_not_called()


def test_hits_are_written_in_chunks(
    rule_hit_import: RuleHitImport, api: MagicMock, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(fwo_const, "API_CALL_CHUNK_SIZE", 1)
    _respond(api, [_stored_rule(1, "r1", None, []), _stored_rule(2, "r2", None, [])])

    result = rule_hit_import.write_rule_hits([_rule_hit("r1", "gw-1", NEW_HIT), _rule_hit("r2", "gw-1", NEW_HIT)])

    assert result.gateway_hits_written == 2
    assert len(_calls(api, UPSERT_MUTATION)) == 2
    assert len(_calls(api, UPDATE_MUTATION)) == 2


@pytest.mark.parametrize("failing_query", [STORED_RULES_QUERY, UPSERT_MUTATION, UPDATE_MUTATION])
def test_api_errors_are_raised(failing_query: str, rule_hit_import: RuleHitImport, api: MagicMock) -> None:
    _respond(api, [_stored_rule(1, "r1", None, [])])
    regular_call: Any = api.call.side_effect

    def call(query: str, query_variables: dict[str, Any]) -> dict[str, Any]:
        if query == failing_query:
            return {"errors": [{"message": "denied"}]}
        result: dict[str, Any] = regular_call(query, query_variables)
        return result

    api.call.side_effect = call

    with pytest.raises(FwoApiWriteError, match="denied"):
        rule_hit_import.write_rule_hits([_rule_hit("r1", "gw-1", NEW_HIT)])


def test_rule_without_gateway_hits_keeps_its_last_hit() -> None:
    stored_hits = {(MGM_ID, "r1"): StoredRuleHits(rule_metadata_id=1, rule_last_hit=OLD_HIT)}

    assert RuleHitImport.collect_changed_rule_last_hits(stored_hits) == {}


def test_helpers() -> None:
    assert is_later(NEW_HIT, OLD_HIT)
    assert not is_later(OLD_HIT, OLD_HIT)
    assert chunked([{"a": 1}, {"a": 2}, {"a": 3}], 2) == [[{"a": 1}, {"a": 2}], [{"a": 3}]]

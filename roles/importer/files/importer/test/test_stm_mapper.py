from __future__ import annotations

from typing import Any
from unittest.mock import MagicMock

import networking.graphql.import_state_queries as queries
import pytest
from fwo_exceptions import FwoImporterError
from services.stm_mapper import StmMapper

STM_RESPONSES: dict[str, dict[str, Any]] = {
    queries.GET_TRACK_MAP: {"stm_track": [{"track_name": "log", "track_id": 2}]},
    queries.GET_ACTION_MAP: {"stm_action": [{"action_name": "accept", "action_id": 1, "allowed": True}]},
    queries.GET_LINK_TYPE_MAP: {"stm_link_type": [{"name": "ordered", "id": 2}]},
    queries.GET_COLOR_MAP: {"stm_color": [{"color_name": "red", "color_id": 5}]},
    queries.GET_NETWORK_OBJ_TYPE_MAP: {"stm_obj_typ": [{"obj_typ_name": "host", "obj_typ_id": 3}]},
    queries.GET_SERVICE_OBJ_TYPE_MAP: {"stm_svc_typ": [{"svc_typ_name": "simple", "svc_typ_id": 1}]},
    queries.GET_USER_OBJ_TYPE_MAP: {"stm_usr_typ": [{"usr_typ_name": "group", "usr_typ_id": 1}]},
    queries.GET_PROTOCOL_MAP: {"stm_ip_proto": [{"ip_proto_name": "TCP", "ip_proto_id": 6}]},
    queries.GET_GATEWAY_MAP: {
        "device": [
            {"mgm_id": 1, "dev_uid": "gw-a", "dev_id": 10},
            {"mgm_id": 1, "dev_uid": "gw-b", "dev_id": 11},
            {"mgm_id": 2, "dev_uid": "gw-c", "dev_id": 20},
        ]
    },
}


def fake_api_call(failing_query: str | None = None) -> MagicMock:
    def call(query: str, query_variables: dict[str, Any]) -> dict[str, Any]:  # noqa: ARG001
        if query == failing_query:
            raise FwoImporterError("api down")
        return {"data": STM_RESPONSES[query]}

    api_call = MagicMock()
    api_call.call.side_effect = call
    return api_call


@pytest.fixture
def stm_mapper() -> StmMapper:
    return StmMapper(fake_api_call())


class TestLookups:
    def test_action_and_track_lookups_ignore_case(self, stm_mapper: StmMapper) -> None:
        assert stm_mapper.lookup_action("Accept") == 1
        assert stm_mapper.lookup_track("LOG") == 2

    def test_link_type_and_object_type_lookups(self, stm_mapper: StmMapper) -> None:
        assert stm_mapper.lookup_link_type("ordered") == 2
        assert stm_mapper.lookup_network_obj_type_id("host") == 3
        assert stm_mapper.lookup_service_obj_type_id("simple") == 1
        assert stm_mapper.lookup_user_obj_type_id("group") == 1

    def test_protocol_lookup_ignores_case(self, stm_mapper: StmMapper) -> None:
        assert stm_mapper.lookup_protocol_id("tcp") == 6

    def test_color_lookups(self, stm_mapper: StmMapper) -> None:
        assert stm_mapper.lookup_color_id("red") == 5
        assert stm_mapper.lookup_color_id("unknown") == 1  # falls back to black
        assert stm_mapper.lookup_color_id_unresolved("unknown") is None

    def test_gateway_lookups_are_per_management(self, stm_mapper: StmMapper) -> None:
        assert stm_mapper.lookup_gateway_id("gw-b", 1) == 11
        assert stm_mapper.lookup_all_gateway_ids(1) == [10, 11]
        assert stm_mapper.lookup_all_gateway_ids(99) == []

    @pytest.mark.parametrize(
        ("method", "args"),
        [
            ("lookup_action", ("drop",)),
            ("lookup_track", ("alert",)),
            ("lookup_link_type", ("inline",)),
            ("lookup_network_obj_type_id", ("network",)),
            ("lookup_service_obj_type_id", ("group",)),
            ("lookup_user_obj_type_id", ("simple",)),
            ("lookup_protocol_id", ("udp",)),
            ("lookup_gateway_id", ("gw-c", 1)),
        ],
    )
    def test_unknown_values_raise(self, stm_mapper: StmMapper, method: str, args: tuple[Any, ...]) -> None:
        with pytest.raises(FwoImporterError):
            getattr(stm_mapper, method)(*args)


@pytest.mark.parametrize("failing_query", list(STM_RESPONSES))
def test_api_errors_while_loading_maps_raise(failing_query: str) -> None:
    with pytest.raises(FwoImporterError):
        StmMapper(fake_api_call(failing_query))

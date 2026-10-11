from __future__ import annotations

from typing import TYPE_CHECKING
from unittest.mock import MagicMock

from models.fwconfig_normalized import FwConfigNormalized
from services.uid2id_mapper import Uid2IdMapper
from states.management_state import ManagementState

if TYPE_CHECKING:
    from states.import_state import ImportState


def build_mapper() -> Uid2IdMapper:
    mapper = Uid2IdMapper(MagicMock())
    mapper.nwobj_uid2id.set("nw-uid", 1, is_global=True)
    mapper.svc_uid2id.set("svc-uid", 2, is_global=True)
    mapper.rule_uid2id.set("rule-uid", 3)
    return mapper


class TestClone:
    def test_shares_the_fwo_api(self) -> None:
        mapper = build_mapper()

        assert mapper.clone().fwo_api is mapper.fwo_api

    def test_copies_all_mappings(self) -> None:
        clone = build_mapper().clone()

        assert clone.get_network_object_id("nw-uid") == 1
        assert clone.get_service_object_id("svc-uid") == 2
        assert clone.get_rule_id("rule-uid") == 3

    def test_changes_to_the_clone_do_not_leak_back(self) -> None:
        mapper = build_mapper()
        clone = mapper.clone()

        clone.nwobj_uid2id.set("nw-uid", 10, is_global=True)
        clone.rulebase_uid2id.set("rb-uid", 4)

        assert mapper.get_network_object_id("nw-uid") == 1
        assert mapper.rulebase_uid2id.get("rb-uid") is None


class TestManagementStateMapper:
    def test_sub_management_gets_clone_of_super_mapper(self, import_state: ImportState) -> None:
        super_mapper = build_mapper()
        import_state.super_uid2id_mapper = super_mapper

        management_state = ManagementState(import_state, MagicMock(), 2, FwConfigNormalized(), is_super_manager=False)

        assert management_state.uid2id_mapper is not super_mapper
        assert management_state.uid2id_mapper.fwo_api is super_mapper.fwo_api
        assert management_state.uid2id_mapper.get_network_object_id("nw-uid") == 1

    def test_management_without_super_mapper_gets_empty_mapper(self, import_state: ImportState) -> None:
        import_state.super_uid2id_mapper = None
        fwo_api = MagicMock()

        management_state = ManagementState(import_state, fwo_api, 2, FwConfigNormalized(), is_super_manager=True)

        assert management_state.uid2id_mapper.fwo_api is fwo_api
        assert management_state.uid2id_mapper.nwobj_uid2id.get("nw-uid") is None

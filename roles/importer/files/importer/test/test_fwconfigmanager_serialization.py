import json
from typing import Any

from model_controllers.fwconfigmanager_controller import FwConfigManagerController
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.fwconfigmanager import FwConfigManager

PASCAL_CASE_KEYS = {
    "ManagerUid",
    "ManagerName",
    "IsSuperManager",
    "DomainUid",
    "DomainName",
    "SubManagerIds",
    "Configs",
}
SNAKE_CASE_KEYS = {
    "manager_uid",
    "manager_name",
    "is_super_manager",
    "domain_uid",
    "domain_name",
    "sub_manager_ids",
    "configs",
}


def build_dummy_manager() -> FwConfigManager:
    return FwConfigManager(
        manager_uid="mgr-uid-1",
        manager_name="dummy-manager",
        is_super_manager=True,
        domain_uid="dom-uid-1",
        domain_name="dummy-domain",
        sub_manager_ids=[2, 3],
        configs=[],
    )


def test_model_dump_uses_aliases_by_default():
    dumped = build_dummy_manager().model_dump()

    assert set(dumped) == PASCAL_CASE_KEYS
    assert dumped["ManagerUid"] == "mgr-uid-1"
    assert dumped["SubManagerIds"] == [2, 3]


def test_model_dump_json_uses_aliases_by_default():
    dumped = json.loads(build_dummy_manager().model_dump_json())

    assert set(dumped) == PASCAL_CASE_KEYS


def test_explicit_by_alias_false_returns_field_names():
    manager = build_dummy_manager()

    assert set(manager.model_dump(by_alias=False)) == SNAKE_CASE_KEYS
    assert set(json.loads(manager.model_dump_json(by_alias=False))) == SNAKE_CASE_KEYS


def test_validation_accepts_aliases_and_field_names():
    by_alias = FwConfigManager.model_validate(build_dummy_manager().model_dump())
    by_name = FwConfigManager.model_validate(build_dummy_manager().model_dump(by_alias=False))

    assert by_alias == build_dummy_manager()
    assert by_name == build_dummy_manager()


def test_json_round_trip_keeps_values():
    original = build_dummy_manager()

    restored = FwConfigManager.model_validate_json(original.model_dump_json())

    assert restored == original


def test_nested_manager_in_list_serializes_with_aliases():
    manager_list = FwConfigManagerListController()
    manager_list.add_manager(build_dummy_manager())

    plain_dump: dict[str, Any] = manager_list.model_dump()
    json_dump: dict[str, Any] = json.loads(manager_list.to_json_string(pretty_print=True))

    assert set(plain_dump["ManagerSet"][0]) == PASCAL_CASE_KEYS
    assert set(json_dump["ManagerSet"][0]) == PASCAL_CASE_KEYS


def test_manager_list_round_trip_from_json_string():
    manager_list = FwConfigManagerListController()
    manager_list.add_manager(build_dummy_manager())

    restored = FwConfigManagerListController.model_validate_json(manager_list.to_json_string(pretty_print=True))

    assert restored.ManagerSet == [build_dummy_manager()]


def test_controller_subclass_inherits_alias_serialization():
    controller = FwConfigManagerController(
        manager_uid="mgr-uid-1",
        manager_name="dummy-manager",
        dependant_manager_uids=["dep-1"],
        configs=[],
    )

    dumped = controller.model_dump()

    assert dumped["ManagerUid"] == "mgr-uid-1"
    assert dumped["DependantManagerUids"] == ["dep-1"]
    assert "manager_uid" not in dumped

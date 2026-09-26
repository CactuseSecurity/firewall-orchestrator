from __future__ import annotations

import json
from enum import Enum
from typing import TYPE_CHECKING, Any

import fwo_base
import pytest
from fwo_base import (
    cidr_to_range,
    deserialize_class_to_dict_rec,
    ensure_device_name,
    extend_string_list,
    find_all_diffs,
    generate_hash_from_dict,
    init_service_provider,
    register_global_state,
    replace_none_with_empty,
    sanitize,
    sort_and_join,
    sort_and_join_refs,
    string_is_uri,
    valid_ip_address,
    write_native_config_to_file,
)
from fwo_const import LIST_DELIMITER
from fwo_enums import ConfFormat
from fwo_log import FWOLogger
from services.enums import Services
from services.global_state import GlobalState
from services.group_flats_mapper import GroupFlatsMapper
from services.uid2id_mapper import Uid2IdMapper

if TYPE_CHECKING:
    from pathlib import Path

    from model_controllers.import_state_controller import ImportStateController
    from services.service_provider import ServiceProvider

NATIVE_CONFIG_DEBUG_LEVEL = 7


class _Color(Enum):
    RED = "red"


class _Node:
    def __init__(self, name: str, color: _Color | None = None) -> None:
        self.name = name
        self.color = color
        self.children: list[_Node] = []
        self.parent: _Node | None = None


class TestStringHelpers:
    def test_sanitize_none_returns_none(self) -> None:
        assert sanitize(None) is None

    def test_sanitize_removes_quotes_and_line_breaks(self) -> None:
        assert sanitize('a "quoted"\nText') == "a quoted Text"

    def test_sanitize_lowercases_on_request(self) -> None:
        assert sanitize("MiXeD", lower=True) == "mixed"

    @pytest.mark.parametrize(
        ("list_string", "src_dict", "expected"),
        [
            (None, {"k": ["a", "b"]}, "a|b"),
            ("", {}, ""),
            ("x|y", {"k": ["z"]}, "x|y|z"),
            ("x", {"other": ["z"]}, "x"),
        ],
    )
    def test_extend_string_list(self, list_string: str | None, src_dict: dict[str, list[str]], expected: str) -> None:
        assert extend_string_list(list_string, src_dict, "k", LIST_DELIMITER) == expected

    @pytest.mark.parametrize(
        ("value", "is_uri"),
        [
            ("http://host/cfg", True),
            ("https://host/cfg", True),
            ("file:///tmp/cfg.json", True),
            ("fw.example.com", False),
            ("ftp://host", False),
        ],
    )
    def test_string_is_uri(self, value: str, is_uri: bool) -> None:
        assert bool(string_is_uri(value)) is is_uri

    def test_replace_none_with_empty(self) -> None:
        assert replace_none_with_empty(None) == "<EMPTY>"
        assert replace_none_with_empty("") == "<EMPTY>"
        assert replace_none_with_empty("value") == "value"

    def test_sort_and_join(self) -> None:
        assert sort_and_join(["b", "c", "a"]) == "a|b|c"

    def test_sort_and_join_refs_sorts_by_name(self) -> None:
        assert sort_and_join_refs([("uid-2", "beta"), ("uid-1", "alpha")]) == ("uid-1|uid-2", "alpha|beta")

    def test_generate_hash_from_dict_ignores_key_order(self) -> None:
        assert generate_hash_from_dict({"a": 1, "b": 2}) == generate_hash_from_dict({"b": 2, "a": 1})
        assert generate_hash_from_dict({"a": 1}) != generate_hash_from_dict({"a": 2})


class TestDeserializeClassToDict:
    def test_simple_values_are_returned_unchanged(self) -> None:
        assert deserialize_class_to_dict_rec(None) is None
        assert deserialize_class_to_dict_rec(5) == 5
        assert deserialize_class_to_dict_rec("s") == "s"
        assert deserialize_class_to_dict_rec(ConfFormat.NORMALIZED) is ConfFormat.NORMALIZED

    def test_objects_lists_dicts_and_enums_are_converted(self) -> None:
        root = _Node("root", _Color.RED)
        root.children.append(_Node("child"))

        result = deserialize_class_to_dict_rec({"nodes": [root]})

        assert result == {
            "nodes": [
                {
                    "name": "root",
                    "color": "red",
                    "children": [{"name": "child", "color": None, "children": [], "parent": None}],
                    "parent": None,
                }
            ]
        }

    def test_circular_references_are_marked(self) -> None:
        root = _Node("root")
        child = _Node("child")
        child.parent = root
        root.children.append(child)

        result: Any = deserialize_class_to_dict_rec(root)

        assert result["children"][0]["parent"] == "<Circular reference to _Node>"

    def test_objects_without_dict_are_returned_as_is(self) -> None:
        value = (1, 2)

        assert deserialize_class_to_dict_rec(value) is value


class TestIpHelpers:
    def test_cidr_to_range_splits_ranges(self) -> None:
        assert cidr_to_range("10.0.0.0-10.0.0.255") == ["10.0.0.0", "10.0.0.255"]

    def test_cidr_to_range_ipv4_network(self) -> None:
        assert cidr_to_range("10.0.0.0/24") == ["10.0.0.0", "10.0.0.255"]

    def test_cidr_to_range_ipv6_network(self) -> None:
        assert cidr_to_range("2001:db8::/127") == ["2001:db8::", "2001:db8::1"]

    def test_cidr_to_range_invalid_ip_is_returned_unchanged(self) -> None:
        assert cidr_to_range("not-an-ip") == ["not", "an", "ip"]
        assert cidr_to_range("garbage") == ["garbage"]

    def test_cidr_to_range_non_string(self) -> None:
        assert cidr_to_range(None) == [""]

    @pytest.mark.parametrize(
        ("ip", "expected"),
        [
            ("10.0.0.1", "IPv4"),
            ("10.0.0.0/8", "IPv4"),
            ("2001:db8::1", "IPv6"),
            ("2001:db8::/32", "IPv6"),
            ("300.1.1.1", "Invalid"),
        ],
    )
    def test_valid_ip_address(self, ip: str, expected: str) -> None:
        assert valid_ip_address(ip) == expected

    def test_valid_ip_address_falls_back_to_single_address(self, monkeypatch: pytest.MonkeyPatch) -> None:
        def reject_network(_ip: str, strict: bool = True) -> None:  # noqa: ARG001
            raise ValueError("no network")

        monkeypatch.setattr(fwo_base.ipaddress, "ip_network", reject_network)

        assert valid_ip_address("10.0.0.1") == "IPv4"
        assert valid_ip_address("2001:db8::1") == "IPv6"


class TestFindAllDiffs:
    def test_equal_structures_have_no_diffs(self) -> None:
        assert find_all_diffs({"a": [1, {"b": 2}]}, {"a": [1, {"b": 2}]}) == []

    def test_missing_keys_are_reported_on_both_sides(self) -> None:
        diffs = find_all_diffs({"a": 1}, {"b": 1})

        assert diffs == ["Key 'a' missing in second object at root", "Key 'b' missing in first object at root"]

    def test_list_length_mismatch(self) -> None:
        assert find_all_diffs([1], [1, 2]) == ["list length mismatch at root: 1 != 2"]

    def test_value_mismatch_path(self) -> None:
        assert find_all_diffs({"a": [1, 2]}, {"a": [1, 3]}) == ["Value mismatch at root.a[1]: 2 != 3"]

    def test_none_and_empty_string_are_equal_unless_strict(self) -> None:
        assert find_all_diffs(None, "") == []
        assert find_all_diffs(None, "", strict=True) == ["Value mismatch at root: None != "]


class TestWriteNativeConfigToFile:
    def test_writes_config_in_debug_mode(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch, import_state_controller: ImportStateController
    ) -> None:
        monkeypatch.setattr(FWOLogger.instance, "debug_level", NATIVE_CONFIG_DEBUG_LEVEL)
        monkeypatch.setattr(fwo_base, "IMPORT_TMP_PATH", str(tmp_path))
        state = import_state_controller.state

        write_native_config_to_file(state, {"native": True})

        written_file = tmp_path / f"mgm_id_{state.mgm_details.mgm_id}_config_native.json"
        assert json.loads(written_file.read_text()) == {"native": True}

    def test_skips_writing_below_debug_level(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch, import_state_controller: ImportStateController
    ) -> None:
        monkeypatch.setattr(FWOLogger.instance, "debug_level", 0)
        monkeypatch.setattr(fwo_base, "IMPORT_TMP_PATH", str(tmp_path))

        write_native_config_to_file(import_state_controller.state, {"native": True})

        assert list(tmp_path.iterdir()) == []

    def test_reraises_write_errors(
        self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch, import_state_controller: ImportStateController
    ) -> None:
        monkeypatch.setattr(FWOLogger.instance, "debug_level", NATIVE_CONFIG_DEBUG_LEVEL)
        monkeypatch.setattr(fwo_base, "IMPORT_TMP_PATH", str(tmp_path / "missing"))

        with pytest.raises(FileNotFoundError):
            write_native_config_to_file(import_state_controller.state, {})


class TestEnsureDeviceName:
    def test_keeps_existing_device_matching_gateway_map(self, import_state_controller: ImportStateController) -> None:
        mgm_details = import_state_controller.state.mgm_details
        mgm_details.devices = [{"name": "gw1"}]
        import_state_controller.state.gateway_map = {mgm_details.current_mgm_id: {"gw1": 1}}

        ensure_device_name(import_state_controller)

        assert mgm_details.devices == [{"name": "gw1"}]

    def test_replaces_device_with_first_gateway_uid(self, import_state_controller: ImportStateController) -> None:
        mgm_details = import_state_controller.state.mgm_details
        mgm_details.devices = [{"name": "unknown"}]
        import_state_controller.state.gateway_map = {mgm_details.current_mgm_id: {"gw1": 1}}

        ensure_device_name(import_state_controller)

        assert mgm_details.devices == [{"name": "gw1"}]

    def test_falls_back_to_management_name(self, import_state_controller: ImportStateController) -> None:
        mgm_details = import_state_controller.state.mgm_details
        mgm_details.devices = []

        ensure_device_name(import_state_controller)

        assert mgm_details.devices == [{"name": mgm_details.name}]

    def test_falls_back_to_hostname_without_name(self, import_state_controller: ImportStateController) -> None:
        mgm_details = import_state_controller.state.mgm_details
        mgm_details.devices = []
        mgm_details.name = ""

        ensure_device_name(import_state_controller)

        assert mgm_details.devices == [{"name": mgm_details.hostname}]


class TestServiceProviderSetup:
    def test_init_service_provider_registers_import_services(
        self,
        service_provider: ServiceProvider,
        import_state_controller: ImportStateController,
        monkeypatch: pytest.MonkeyPatch,
    ) -> None:
        monkeypatch.setattr(fwo_base.fwo_config, "read_config", lambda: {"fwo_api_base_url": "https://api"})
        service_provider.reset()

        provider = init_service_provider()
        register_global_state(import_state_controller)

        assert provider.get_fwo_config() == {"fwo_api_base_url": "https://api"}
        assert isinstance(provider.get_service(Services.GROUP_FLATS_MAPPER, import_id=1), GroupFlatsMapper)
        assert isinstance(provider.get_service(Services.PREV_GROUP_FLATS_MAPPER, import_id=1), GroupFlatsMapper)
        assert isinstance(provider.get_service(Services.UID2ID_MAPPER, import_id=1), Uid2IdMapper)

    def test_register_global_state_wraps_import_state(
        self, service_provider: ServiceProvider, import_state_controller: ImportStateController
    ) -> None:
        service_provider.reset()

        register_global_state(import_state_controller)

        global_state = service_provider.get_global_state()
        assert isinstance(global_state, GlobalState)
        assert global_state.import_state is import_state_controller

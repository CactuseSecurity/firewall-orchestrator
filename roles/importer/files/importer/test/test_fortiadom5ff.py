# pyright: reportPrivateUsage=false

import json
from datetime import datetime, timezone
from typing import Any

import fwo_const
import pytest
from fw_modules.fortiadom5ff import fmgr_rule
from fw_modules.fortiadom5ff.fmgr_rule import (
    build_addr_list,
    build_nat_addr_list,
    extract_nat_config_fields,
    find_addr_ref,
    ip_type,
    parse_nat_rules_in_rulebase,
    rule_parse_action,
    rule_parse_addresses,
    rule_parse_installon,
    rule_parse_last_hit,
    rule_parse_negation_flags,
    rule_parse_service,
    rule_parse_time,
    rule_parse_tracking_info,
)
from fw_modules.fortiadom5ff.fmgr_service import (
    FORTI_IP_PROTOCOL_NUMBER_ANY,
    FORTI_PROTOCOL_IP,
    handle_svc_protocol,
    normalize_service_object,
)
from fw_modules.fortiadom5ff.fwcommon import to_time_object
from fwo_exceptions import (
    FwoDeviceWithoutLocalPackageError,
    FwoImporterErrorInconsistenciesError,
    ImportInterruptionError,
)
from models.rulebase import Rulebase
from models.time_object import TimeObject
from pytest_mock import MockerFixture


def _empty_normalized_config() -> dict[str, Any]:
    return {
        "network_objects": [],
        "service_objects": [],
        "zone_objects": [],
        "policies": [],
        "rules": [],
    }


class TestToTimeObject:
    @staticmethod
    def _expected_as_utc(date_part: str, time_part: str) -> str:
        return (
            datetime.strptime(f"{date_part} {time_part}", "%Y/%m/%d %H:%M")
            .astimezone()
            .astimezone(timezone.utc)
            .isoformat(timespec="seconds")
        )

    def test_to_time_object_parses_list_timestamps(self):
        time_obj = to_time_object(
            {
                "name": "work-hours",
                "start": ["12:00", "2026/02/17"],
                "end": ["18:30", "2026/02/17"],
            }
        )

        assert time_obj.time_obj_uid == "work-hours"
        assert time_obj.time_obj_name == "work-hours"
        assert time_obj.start_time == self._expected_as_utc("2026/02/17", "12:00")
        assert time_obj.end_time == self._expected_as_utc("2026/02/17", "18:30")

    def test_to_time_object_parses_single_string_timestamp(self):
        time_obj = to_time_object(
            {
                "name": "legacy-format",
                "start": "00:00 2020/01/01",
                "end": "23:59 2020/01/01",
            }
        )

        assert time_obj.start_time == self._expected_as_utc("2020/01/01", "00:00")
        assert time_obj.end_time == self._expected_as_utc("2020/01/01", "23:59")

    @pytest.mark.parametrize(
        ("start_time", "end_time", "expected_start", "expected_end"),
        [
            (
                "2026-03-11T11:57:00+01:00",
                "2026-03-11T12:57:00+01:00",
                "2026-03-11T10:57:00+00:00",
                "2026-03-11T11:57:00+00:00",
            ),
            (
                " 2026-03-11T11:57:00+0200 ",
                " 2026-03-11T12:57:00+0200 ",
                "2026-03-11T09:57:00+00:00",
                "2026-03-11T10:57:00+00:00",
            ),
        ],
    )
    def test_time_object_converts_supported_timestamp_formats_to_utc(
        self,
        start_time: str,
        end_time: str,
        expected_start: str,
        expected_end: str,
    ):
        time_obj = TimeObject(
            time_obj_uid="tz-conversion",
            time_obj_name="tz-conversion",
            start_time=start_time,
            end_time=end_time,
        )

        assert time_obj.start_time == expected_start
        assert time_obj.end_time == expected_end

    def test_time_object_rejects_invalid_timestamp_with_shared_message(self):
        with pytest.raises(
            ValueError,
            match=r"Time value 'not-a-timestamp' must be an ISO 8601 timestamp like YYYY-MM-DDTHH:MM\[:SS\]\[Z\|±HH:MM\|±HHMM\]; timestamps without a timezone are treated as UTC",
        ):
            TimeObject(
                time_obj_uid="broken",
                time_obj_name="broken",
                start_time="not-a-timestamp",
            )

    def test_to_time_object_returns_none_for_default_start_time(self):
        time_obj = to_time_object(
            {
                "name": "all-day",
                "start": "00:00",
                "end": None,
            }
        )

        assert time_obj.start_time is None
        assert time_obj.end_time is None

    def test_to_time_object_logs_warning_for_unsupported_time_only_format(self, mocker: MockerFixture):
        warning_mock = mocker.patch("fwo_log.FWOLogger.warning")

        time_obj = to_time_object(
            {
                "name": "unsupported",
                "start": "12:00",
                "end": "15:00",
            }
        )

        assert time_obj.start_time is None
        assert time_obj.end_time is None
        assert warning_mock.call_count == 2

    def test_to_time_object_logs_warning_for_invalid_datetime(self, mocker: MockerFixture):
        warning_mock = mocker.patch("fwo_log.FWOLogger.warning")

        time_obj = to_time_object(
            {
                "name": "broken-date",
                "start": ["12:00", "2026/13/17"],
                "end": ["99:99", "2026/02/17"],
            }
        )

        assert time_obj.start_time is None
        assert time_obj.end_time is None
        assert warning_mock.call_count == 2

    @pytest.mark.parametrize("missing_name", [None, ""])
    def test_to_time_object_raises_on_missing_name(self, missing_name: str | None):
        with pytest.raises(ImportInterruptionError):
            to_time_object(
                {
                    "name": missing_name,
                    "start": ["12:00", "2026/02/17"],
                    "end": ["18:00", "2026/02/17"],
                }
            )


def test_rule_parse_last_hit_returns_offset_aware_iso_timestamp():
    epoch_seconds = 1761998205

    parsed = rule_parse_last_hit({"_last_hit": epoch_seconds})

    assert parsed is not None
    parsed_time = datetime.fromisoformat(parsed)
    assert parsed_time.tzinfo is not None
    assert int(parsed_time.timestamp()) == epoch_seconds


@pytest.mark.parametrize(
    ("native_service", "expected_protocol"),
    [
        ({"protocol": 1}, 1),
        ({"protocol": FORTI_PROTOCOL_IP, "protocol-number": 47}, 47),
        ({"protocol": 6}, 58),
    ],
)
def test_handle_svc_protocol_maps_forti_protocol_numbers(
    native_service: dict[str, int],
    expected_protocol: int,
):
    service_objects: list[dict[str, object]] = []

    handle_svc_protocol(native_service, service_objects, "simple", "svc", "foreground", None)

    assert service_objects[0]["ip_proto"] == expected_protocol


def test_handle_svc_protocol_uses_any_protocol_for_ip_without_protocol_number():
    service_objects: list[dict[str, object]] = []

    handle_svc_protocol({"protocol": FORTI_PROTOCOL_IP}, service_objects, "simple", "svc", "foreground", None)

    assert service_objects[0]["ip_proto"] == -1


def test_handle_svc_protocol_uses_any_protocol_for_ip_protocol_number_zero():
    service_objects: list[dict[str, object]] = []

    handle_svc_protocol(
        {"protocol": FORTI_PROTOCOL_IP, "protocol-number": FORTI_IP_PROTOCOL_NUMBER_ANY},
        service_objects,
        "simple",
        "svc",
        "foreground",
        None,
    )

    assert service_objects[0]["ip_proto"] == -1
    assert service_objects[0]["svc_port"] is None
    assert service_objects[0]["svc_port_end"] is None


def test_handle_svc_protocol_ignores_unsupported_protocol(mocker: MockerFixture):
    warning_mock = mocker.patch("fwo_log.FWOLogger.warning")
    service_objects: list[dict[str, object]] = []

    handle_svc_protocol({"protocol": 99}, service_objects, "simple", "svc", "foreground", None)

    assert service_objects == []
    warning_mock.assert_called_once()


def test_normalize_service_object_uses_any_protocol_for_protocol_zero_service():
    service_objects: list[dict[str, object]] = []

    normalize_service_object({"name": "custom-any", "protocol": 0}, service_objects)

    assert service_objects == [
        {
            "svc_typ": "simple",
            "svc_name": "custom-any",
            "svc_color": "foreground",
            "svc_uid": "custom-any",
            "svc_comment": None,
            "ip_proto": -1,
            "svc_port": None,
            "svc_port_end": None,
            "svc_member_refs": None,
            "svc_member_names": None,
            "svc_timeout": None,
            "rpc_nr": None,
        }
    ]


def test_normalize_service_object_preserves_ports_for_all_named_service():
    service_objects: list[dict[str, object]] = []

    normalize_service_object({"name": "ALL", "protocol": 5, "tcp-portrange": ["443"]}, service_objects)

    assert service_objects == [
        {
            "svc_typ": "simple",
            "svc_name": "ALL",
            "svc_color": "foreground",
            "svc_uid": "ALL",
            "svc_comment": None,
            "ip_proto": 6,
            "svc_port": "443",
            "svc_port_end": "443",
            "svc_member_refs": None,
            "svc_member_names": None,
            "svc_timeout": None,
            "rpc_nr": None,
        }
    ]


def test_normalize_service_object_group_without_protocol_keeps_members_and_no_protocol():
    service_objects: list[dict[str, object]] = []

    normalize_service_object({"name": "grp", "member": ["svcB", "svcA"]}, service_objects)

    assert len(service_objects) == 1
    assert service_objects[0]["svc_typ"] == "group"
    # groups stay without a protocol, their members carry it
    assert service_objects[0]["ip_proto"] is None
    assert service_objects[0]["svc_member_names"] == fwo_const.LIST_DELIMITER.join(["svcA", "svcB"])


def test_normalize_service_object_group_with_protocol_field_stays_a_group():
    # regression guard: the "member" branch must be checked before "protocol",
    # otherwise a group that also carries a stray protocol field would be
    # misparsed as a simple protocol-based service and lose its members
    service_objects: list[dict[str, object]] = []

    normalize_service_object({"name": "grp2", "member": ["svcA", "svcB"], "protocol": 5}, service_objects)

    assert len(service_objects) == 1
    assert service_objects[0]["svc_typ"] == "group"
    assert service_objects[0]["ip_proto"] is None
    assert service_objects[0]["svc_member_names"] == fwo_const.LIST_DELIMITER.join(["svcA", "svcB"])


def test_normalize_service_object_multi_protocol_split_parent_group_has_no_protocol():
    service_objects: list[dict[str, object]] = []

    normalize_service_object(
        {"name": "multi", "protocol": 5, "tcp-portrange": ["80"], "udp-portrange": ["53"]}, service_objects
    )

    parent_group = next(svc for svc in service_objects if svc["svc_name"] == "multi")
    assert parent_group["svc_typ"] == "group"
    assert parent_group["ip_proto"] is None
    assert parent_group["svc_member_names"] == fwo_const.LIST_DELIMITER.join(["multi_tcp", "multi_udp"])


def test_normalize_service_object_rpc_service_without_port_ranges():
    service_objects: list[dict[str, object]] = []

    normalize_service_object({"name": "rpc-svc", "protocol": 11}, service_objects)

    assert len(service_objects) == 1
    assert service_objects[0]["svc_typ"] == "rpc"
    assert service_objects[0]["ip_proto"] is None


def test_extract_nat_config_fields_serializes_poolname_and_fixedport():
    nat_config_fields = extract_nat_config_fields(
        {
            "nat": 1,
            "ippool": 1,
            "poolname": ["pool-a", "pool-b"],
            "fixedport": 1,
        }
    )

    assert json.loads(nat_config_fields) == {
        "fixedport": 1,
        "ippool": 1,
        "nat_type": "nat",
        "poolname": ["pool-a", "pool-b"],
    }


def test_parse_nat_rules_in_rulebase_keeps_translation_metadata_on_translated_rule():
    normalized_config_adom = {
        "network_objects": [
            {"obj_name": "src-net", "obj_uid": "src-net-uid", "obj_ip": "10.0.0.0/24"},
            {"obj_name": "dst-net", "obj_uid": "dst-net-uid", "obj_ip": "10.0.1.0/24"},
            {"obj_name": "pool-a", "obj_uid": "pool-a-uid", "obj_ip": "10.0.2.1/32"},
        ],
        "zone_objects": [{"zone_name": "inside"}, {"zone_name": "outside"}],
        "policies": [],
        "rules": [],
    }
    normalized_config_global: dict[str, list[Any]] = {
        "network_objects": [],
        "zone_objects": [],
        "policies": [],
        "rules": [],
    }
    native_rulebase = {
        "data": [
            {
                "uuid": "nat-rule-uid",
                "name": "nat-rule",
                "nat": 1,
                "status": 1,
                "srcaddr": ["src-net"],
                "dstaddr": ["dst-net"],
                "service": ["ALL"],
                "srcintf": ["inside"],
                "dstintf": ["outside"],
                "ippool": 1,
                "poolname": ["pool-a"],
                "fixedport": 1,
            }
        ]
    }
    normalized_nat_rulebase = Rulebase(uid="nat-rulebase-test", name="NAT", mgm_uid="mgm", rules={})

    parse_nat_rules_in_rulebase(
        normalized_config_adom,
        normalized_config_global,
        native_rulebase,
        normalized_nat_rulebase,
    )

    assert set(normalized_nat_rulebase.rules) == {"nat-rule-uid-original", "nat-rule-uid-translated"}

    original_rule = normalized_nat_rulebase.rules["nat-rule-uid-original"]
    assert original_rule.rule_custom_fields is None

    translated_rule = normalized_nat_rulebase.rules["nat-rule-uid-translated"]
    assert translated_rule.rule_src == "pool-a"
    assert translated_rule.rule_src_refs == "pool-a-uid"
    assert translated_rule.rule_dst == "Original"
    assert translated_rule.rule_dst_refs == "Original"
    assert translated_rule.rule_src_zone == "inside"
    assert translated_rule.rule_dst_zone == "outside"
    assert json.loads(translated_rule.rule_custom_fields or "{}") == {
        "fixedport": 1,
        "ippool": 1,
        "nat_type": "nat",
        "poolname": ["pool-a"],
    }


def test_parse_nat_rules_in_rulebase_supports_ipv6_nat_pool_translation():
    normalized_config_adom = {
        "network_objects": [
            {"obj_name": "src-net-v6", "obj_uid": "src-net-v6-uid", "obj_ip": "2001:db8::/64"},
            {"obj_name": "dst-net-v6", "obj_uid": "dst-net-v6-uid", "obj_ip": "2001:db8:1::/64"},
            {"obj_name": "pool-v6", "obj_uid": "pool-v6-uid", "obj_ip": "2001:db8:2::1/128"},
        ],
        "zone_objects": [{"zone_name": "inside"}, {"zone_name": "outside"}],
        "policies": [],
        "rules": [],
    }
    normalized_config_global: dict[str, list[Any]] = {
        "network_objects": [],
        "zone_objects": [],
        "policies": [],
        "rules": [],
    }
    native_rulebase = {
        "data": [
            {
                "uuid": "nat-rule-v6-uid",
                "name": "nat-rule-v6",
                "nat": 1,
                "status": 1,
                "srcaddr6": ["src-net-v6"],
                "dstaddr6": ["dst-net-v6"],
                "service": ["ALL"],
                "srcintf": ["inside"],
                "dstintf": ["outside"],
                "ippool": 1,
                "poolname6": ["pool-v6"],
                "fixedport": 1,
            }
        ]
    }
    normalized_nat_rulebase = Rulebase(uid="nat-rulebase-v6-test", name="NAT", mgm_uid="mgm", rules={})

    parse_nat_rules_in_rulebase(
        normalized_config_adom,
        normalized_config_global,
        native_rulebase,
        normalized_nat_rulebase,
    )

    assert set(normalized_nat_rulebase.rules) == {"nat-rule-v6-uid-original", "nat-rule-v6-uid-translated"}

    translated_rule = normalized_nat_rulebase.rules["nat-rule-v6-uid-translated"]
    assert translated_rule.rule_src == "pool-v6"
    assert translated_rule.rule_src_refs == "pool-v6-uid"
    assert translated_rule.rule_dst == "Original"
    assert translated_rule.rule_dst_refs == "Original"
    assert translated_rule.rule_src_zone == "inside"
    assert translated_rule.rule_dst_zone == "outside"
    assert json.loads(translated_rule.rule_custom_fields or "{}") == {
        "fixedport": 1,
        "ippool": 1,
        "nat_type": "nat",
        "poolname6": ["pool-v6"],
    }


def test_parse_nat_rules_in_rulebase_resolves_dnat_vip_to_translated_destination():
    normalized_config_adom = {
        "network_objects": [
            {"obj_name": "src-net", "obj_uid": "src-net-uid", "obj_ip": "10.0.0.0/24"},
            {
                "obj_name": "vip-obj",
                "obj_uid": "vip-obj-uid",
                "obj_native_type": "firewall/vip",
                "obj_ip": "1.2.3.4",
                "obj_nat_ip": "10.0.0.5",
                "obj_nat_ip_end": "10.0.0.5",
            },
            {"obj_name": "10.0.0.5_NatNwObj", "obj_uid": "10.0.0.5_NatNwObj", "obj_ip": "10.0.0.5"},
        ],
        "zone_objects": [{"zone_name": "inside"}, {"zone_name": "outside"}],
        "policies": [],
        "rules": [],
    }
    normalized_config_global: dict[str, list[Any]] = {
        "network_objects": [],
        "zone_objects": [],
        "policies": [],
        "rules": [],
    }
    native_rulebase = {
        "data": [
            {
                "uuid": "dnat-rule-uid",
                "name": "dnat-rule",
                "status": 1,
                "srcaddr": ["src-net"],
                "dstaddr": ["vip-obj"],
                "service": ["ALL"],
                "srcintf": ["inside"],
                "dstintf": ["outside"],
            }
        ]
    }
    normalized_nat_rulebase = Rulebase(uid="nat-rulebase-dnat-test", name="NAT", mgm_uid="mgm", rules={})

    parse_nat_rules_in_rulebase(
        normalized_config_adom,
        normalized_config_global,
        native_rulebase,
        normalized_nat_rulebase,
    )

    assert set(normalized_nat_rulebase.rules) == {"dnat-rule-uid-original", "dnat-rule-uid-translated"}

    original_rule = normalized_nat_rulebase.rules["dnat-rule-uid-original"]
    assert original_rule.rule_dst == "vip-obj"

    translated_rule = normalized_nat_rulebase.rules["dnat-rule-uid-translated"]
    assert translated_rule.rule_dst == "10.0.0.5_NatNwObj"
    assert translated_rule.rule_dst_refs == "10.0.0.5_NatNwObj"


class TestRuleFieldParsers:
    def test_rule_parse_action_defaults_to_drop(self):
        assert rule_parse_action({}) == fmgr_rule.RuleAction.DROP

    def test_rule_parse_action_returns_drop_for_zero(self):
        assert rule_parse_action({"action": 0}) == fmgr_rule.RuleAction.DROP

    @pytest.mark.parametrize(
        ("native_rule", "expected"),
        [
            ({}, fmgr_rule.RuleTrack.NONE),
            ({"logtraffic": 0}, fmgr_rule.RuleTrack.NONE),
            ({"logtraffic": 1}, fmgr_rule.RuleTrack.LOG),
            ({"logtraffic": "disable"}, fmgr_rule.RuleTrack.NONE),
            ({"logtraffic": "all"}, fmgr_rule.RuleTrack.LOG),
        ],
    )
    def test_rule_parse_tracking_info(self, native_rule: dict[str, Any], expected: fmgr_rule.RuleTrack):
        assert rule_parse_tracking_info(native_rule) == expected

    def test_rule_parse_service_uses_explicit_services(self):
        rule_svc_list, rule_svc_refs_list = rule_parse_service({"service": ["HTTPS", "HTTP"]})
        assert rule_svc_list == ["HTTP", "HTTPS"]
        assert rule_svc_refs_list == ["HTTP", "HTTPS"]

    def test_rule_parse_service_falls_back_to_all_for_internet_service_name(self):
        rule_svc_list, rule_svc_refs_list = rule_parse_service({"internet-service-name": ["Google-Gmail"]})
        assert rule_svc_list == ["ALL"]
        assert rule_svc_refs_list == ["ALL"]

    def test_rule_parse_service_falls_back_to_all_for_internet_service_src_name(self):
        rule_svc_list, rule_svc_refs_list = rule_parse_service({"internet-service-src-name": ["Google-Gmail"]})
        assert rule_svc_list == ["ALL"]
        assert rule_svc_refs_list == ["ALL"]

    def test_rule_parse_service_returns_empty_lists_when_nothing_matches(self):
        assert rule_parse_service({}) == ([], [])

    @pytest.mark.parametrize(
        ("native_rule", "expected"),
        [
            ({"srcaddr-negate": 1}, (True, False, False)),
            ({"srcaddr-negate": "disable"}, (True, False, False)),
            ({"internet-service-src-negate": 1}, (True, False, False)),
            ({"dstaddr-negate": 1}, (False, True, False)),
            ({"service-negate": 1}, (False, False, True)),
            ({}, (False, False, False)),
        ],
    )
    def test_rule_parse_negation_flags(self, native_rule: dict[str, Any], expected: tuple[bool, bool, bool]):
        assert rule_parse_negation_flags(native_rule) == expected

    def test_rule_parse_installon_joins_sorted_vdom_names(self):
        native_rule = {
            "scope_member": [
                {"name": "fw2", "vdom": "root"},
                {"name": "fw1", "vdom": "root"},
            ]
        }
        assert rule_parse_installon(native_rule) == "fw1_root|fw2_root"

    def test_rule_parse_installon_returns_none_when_absent(self):
        assert rule_parse_installon({}) is None

    def test_rule_parse_time_joins_schedule_entries(self):
        assert rule_parse_time({"schedule": ["always", "extra"]}) == "always|extra"

    def test_rule_parse_time_returns_none_when_absent(self):
        assert rule_parse_time({}) is None

    def test_rule_parse_last_hit_returns_none_when_absent(self):
        assert rule_parse_last_hit({}) is None


class TestAddressBuilders:
    def test_build_addr_list_v4_src_combines_addr_and_internet_service(self):
        addr_list: list[str] = []
        addr_ref_list: list[str] = []
        native_rule = {
            "srcaddr": ["b-net"],
            "internet-service-src-name": ["a-net"],
        }
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "a-net", "obj_uid": "a-uid", "obj_ip": "10.0.0.0/24"},
            {"obj_name": "b-net", "obj_uid": "b-uid", "obj_ip": "10.0.1.0/24"},
        ]
        build_addr_list(
            native_rule, "src", normalized_config_adom, _empty_normalized_config(), addr_list, addr_ref_list, is_v4=True
        )
        assert addr_list == ["b-net", "a-net"]
        assert addr_ref_list == ["b-uid", "a-uid"]

    def test_build_addr_list_v6_src(self):
        addr_list: list[str] = []
        addr_ref_list: list[str] = []
        native_rule = {"srcaddr6": ["net-v6"]}
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "net-v6", "obj_uid": "net-v6-uid", "obj_ip": "2001:db8::/64"},
        ]
        build_addr_list(
            native_rule,
            "src",
            normalized_config_adom,
            _empty_normalized_config(),
            addr_list,
            addr_ref_list,
            is_v4=False,
        )
        assert addr_list == ["net-v6"]
        assert addr_ref_list == ["net-v6-uid"]

    def test_build_addr_list_v4_dst_combines_addr_and_internet_service(self):
        addr_list: list[str] = []
        addr_ref_list: list[str] = []
        native_rule = {
            "dstaddr": ["b-net"],
            "internet-service-name": ["a-net"],
        }
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "a-net", "obj_uid": "a-uid", "obj_ip": "10.0.0.0/24"},
            {"obj_name": "b-net", "obj_uid": "b-uid", "obj_ip": "10.0.1.0/24"},
        ]
        build_addr_list(
            native_rule, "dst", normalized_config_adom, _empty_normalized_config(), addr_list, addr_ref_list, is_v4=True
        )
        assert addr_list == ["b-net", "a-net"]
        assert addr_ref_list == ["b-uid", "a-uid"]

    def test_build_addr_list_v6_dst(self):
        addr_list: list[str] = []
        addr_ref_list: list[str] = []
        native_rule = {"dstaddr6": ["net-v6"]}
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "net-v6", "obj_uid": "net-v6-uid", "obj_ip": "2001:db8::/64"},
        ]
        build_addr_list(
            native_rule,
            "dst",
            normalized_config_adom,
            _empty_normalized_config(),
            addr_list,
            addr_ref_list,
            is_v4=False,
        )
        assert addr_list == ["net-v6"]
        assert addr_ref_list == ["net-v6-uid"]

    def test_build_nat_addr_list_prefers_ipv6_when_present(self):
        addr_list: list[str] = []
        addr_ref_list: list[str] = []
        native_rule = {"srcaddr6": ["src-v6"], "dstaddr6": ["dst-v6"]}
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "src-v6", "obj_uid": "src-v6-uid", "obj_ip": "2001:db8::/64"},
        ]
        build_nat_addr_list(
            native_rule, "src", normalized_config_adom, _empty_normalized_config(), addr_list, addr_ref_list
        )
        assert addr_list == ["src-v6"]
        assert addr_ref_list == ["src-v6-uid"]

    def test_build_nat_addr_list_v4_dst(self):
        addr_list: list[str] = []
        addr_ref_list: list[str] = []
        native_rule = {"dstaddr": ["dst-v4"]}
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "dst-v4", "obj_uid": "dst-v4-uid", "obj_ip": "10.0.0.0/24"},
        ]
        build_nat_addr_list(
            native_rule, "dst", normalized_config_adom, _empty_normalized_config(), addr_list, addr_ref_list
        )
        assert addr_list == ["dst-v4"]
        assert addr_ref_list == ["dst-v4-uid"]

    def test_rule_parse_addresses_rejects_invalid_target(self):
        with pytest.raises(FwoImporterErrorInconsistenciesError):
            rule_parse_addresses({}, "bogus", _empty_normalized_config(), _empty_normalized_config(), is_nat=False)

    def test_find_addr_ref_raises_when_not_found(self):
        with pytest.raises(FwoImporterErrorInconsistenciesError):
            find_addr_ref(
                "missing",
                is_v4=True,
                normalized_config_adom=_empty_normalized_config(),
                normalized_config_global=_empty_normalized_config(),
            )

    def test_ip_type_defaults_to_v4_for_empty_ip(self):
        assert ip_type({"obj_ip": ""}) == fmgr_rule.ip_v4_type

    def test_ip_type_detects_v6(self):
        assert ip_type({"obj_ip": "2001:db8::/64"}) == fmgr_rule.ip_v6_type


class TestRulebaseLinkHelpers:
    def test_should_skip_rulebase_link_skips_nat_links(self):
        assert fmgr_rule._should_skip_rulebase_link({"link_type": "nat", "to_rulebase_uid": "x"}, []) is True

    def test_should_skip_rulebase_link_skips_already_fetched(self):
        assert fmgr_rule._should_skip_rulebase_link({"type": "ordered", "to_rulebase_uid": "x"}, ["x"]) is True

    def test_should_skip_rulebase_link_skips_empty_uid(self):
        assert fmgr_rule._should_skip_rulebase_link({"type": "ordered", "to_rulebase_uid": ""}, []) is True

    def test_should_skip_rulebase_link_returns_false_for_new_link(self):
        assert fmgr_rule._should_skip_rulebase_link({"type": "ordered", "to_rulebase_uid": "y"}, ["x"]) is False

    def test_find_rulebase_to_parse_returns_match(self):
        rulebases: list[dict[str, Any]] = [{"uid": "a", "data": []}, {"uid": "b", "data": []}]
        assert fmgr_rule.find_rulebase_to_parse(rulebases, "b") == {"uid": "b", "data": []}

    def test_find_rulebase_to_parse_returns_empty_when_missing(self):
        assert fmgr_rule.find_rulebase_to_parse([{"uid": "a"}], "missing") == {}

    def test_find_rulebase_to_parse_for_link_uses_adom_first(self):
        native_config: dict[str, Any] = {"rulebases": [{"uid": "adom-rb", "data": []}]}
        native_config_global: dict[str, Any] = {"rulebases": [{"uid": "global-rb", "data": []}]}
        rulebase, found_in_global = fmgr_rule._find_rulebase_to_parse_for_link(
            {"to_rulebase_uid": "adom-rb"}, native_config, native_config_global, is_global_loop_iteration=False
        )
        assert rulebase == {"uid": "adom-rb", "data": []}
        assert found_in_global is False

    def test_find_rulebase_to_parse_for_link_falls_back_to_global(self):
        native_config: dict[str, Any] = {"rulebases": []}
        native_config_global: dict[str, Any] = {"rulebases": [{"uid": "global-rb", "data": []}]}
        rulebase, found_in_global = fmgr_rule._find_rulebase_to_parse_for_link(
            {"to_rulebase_uid": "global-rb"}, native_config, native_config_global, is_global_loop_iteration=False
        )
        assert rulebase == {"uid": "global-rb", "data": []}
        assert found_in_global is True

    def test_find_rulebase_to_parse_for_link_skips_global_lookup_during_global_iteration(self):
        native_config: dict[str, Any] = {"rulebases": []}
        native_config_global: dict[str, Any] = {"rulebases": [{"uid": "global-rb", "data": []}]}
        rulebase, found_in_global = fmgr_rule._find_rulebase_to_parse_for_link(
            {"to_rulebase_uid": "global-rb"}, native_config, native_config_global, is_global_loop_iteration=True
        )
        assert rulebase == {}
        assert found_in_global is False

    def test_append_normalized_rulebase_appends_to_global_when_found_in_global(self):
        normalized_config_adom: dict[str, Any] = {"policies": []}
        normalized_config_global: dict[str, Any] = {"policies": []}
        rulebase = Rulebase(uid="rb", name="rb", mgm_uid="mgm")
        fmgr_rule._append_normalized_rulebase(
            normalized_config_adom, normalized_config_global, rulebase, found_rulebase_in_global=True
        )
        assert normalized_config_global["policies"] == [rulebase]
        assert normalized_config_adom["policies"] == []

    def test_append_normalized_rulebase_appends_to_adom_by_default(self):
        normalized_config_adom: dict[str, Any] = {"policies": []}
        normalized_config_global: dict[str, Any] = {"policies": []}
        rulebase = Rulebase(uid="rb", name="rb", mgm_uid="mgm")
        fmgr_rule._append_normalized_rulebase(
            normalized_config_adom, normalized_config_global, rulebase, found_rulebase_in_global=False
        )
        assert normalized_config_adom["policies"] == [rulebase]
        assert normalized_config_global["policies"] == []

    def test_initialize_normalized_rulebase_uses_type_as_uid_and_name(self):
        rulebase = fmgr_rule.initialize_normalized_rulebase({"type": "rules_adom_v4"}, "mgm-uid")
        assert rulebase.uid == "rules_adom_v4"
        assert rulebase.name == "rules_adom_v4"
        assert rulebase.mgm_uid == "mgm-uid"
        assert rulebase.rules == {}


class TestParseRulebase:
    def test_parse_rulebase_adds_implicit_deny_when_not_global(self):
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["zone_objects"] = [{"zone_name": "any"}]
        normalized_config_global = _empty_normalized_config()
        rulebase_to_parse = {
            "data": [
                {
                    "uuid": "rule-1",
                    "name": "allow-all",
                    "status": 1,
                    "action": 1,
                    "srcaddr": ["all"],
                    "dstaddr": ["all"],
                    "service": ["ALL"],
                    "srcintf": ["any"],
                    "dstintf": ["any"],
                }
            ]
        }
        normalized_config_adom["network_objects"] = [
            {"obj_name": "all", "obj_uid": "all-uid", "obj_ip": "0.0.0.0/32"},
            {"obj_name": "all", "obj_uid": "all-uid-v6", "obj_ip": "::/0"},
        ]
        normalized_rulebase = Rulebase(uid="rb", name="rb", mgm_uid="mgm")

        fmgr_rule.parse_rulebase(
            normalized_config_adom, normalized_config_global, rulebase_to_parse, normalized_rulebase, False
        )

        assert set(normalized_rulebase.rules) == {"rule-1", "rb_implicit_deny"}
        assert normalized_rulebase.rules["rb_implicit_deny"].rule_action == fmgr_rule.RuleAction.DROP
        assert normalized_rulebase.rules["rb_implicit_deny"].rule_implied is True

    def test_parse_rulebase_skips_implicit_deny_when_found_in_global(self):
        normalized_config_adom = _empty_normalized_config()
        normalized_config_global = _empty_normalized_config()
        rulebase_to_parse: dict[str, Any] = {"data": []}
        normalized_rulebase = Rulebase(uid="rb-global", name="rb-global", mgm_uid="mgm")

        fmgr_rule.parse_rulebase(
            normalized_config_adom, normalized_config_global, rulebase_to_parse, normalized_rulebase, True
        )

        assert normalized_rulebase.rules == {}


class TestParseSingleRule:
    def _base_normalized_config(self) -> dict[str, Any]:
        config = _empty_normalized_config()
        config["zone_objects"] = [{"zone_name": "inside"}, {"zone_name": "outside"}]
        config["network_objects"] = [
            {"obj_name": "src-net", "obj_uid": "src-net-uid", "obj_ip": "10.0.0.0/24"},
            {"obj_name": "dst-net", "obj_uid": "dst-net-uid", "obj_ip": "10.0.1.0/24"},
        ]
        return config

    def test_parse_single_rule_builds_normalized_access_rule(self):
        normalized_config_adom = self._base_normalized_config()
        normalized_config_global = _empty_normalized_config()
        native_rule = {
            "uuid": "rule-uid",
            "name": "my-rule",
            "status": 1,
            "action": 1,
            "srcaddr": ["src-net"],
            "dstaddr": ["dst-net"],
            "service": ["HTTPS"],
            "srcintf": ["inside"],
            "dstintf": ["outside"],
            "comments": "a comment",
        }
        rulebase = Rulebase(uid="rb", name="rb", mgm_uid="mgm")

        fmgr_rule.parse_single_rule(normalized_config_adom, normalized_config_global, native_rule, rulebase)

        rule = rulebase.rules["rule-uid"]
        assert rule.rule_disabled is False
        assert rule.rule_action == fmgr_rule.RuleAction.ACCEPT
        assert rule.rule_src == "src-net"
        assert rule.rule_dst == "dst-net"
        assert rule.rule_svc == "HTTPS"
        assert rule.rule_comment == "a comment"
        assert rule.access_rule is True
        assert rule.nat_rule is False

    def test_parse_single_rule_defaults_to_disabled_when_status_missing(self):
        normalized_config_adom = self._base_normalized_config()
        normalized_config_global = _empty_normalized_config()
        native_rule = {
            "uuid": "rule-disabled",
            "srcaddr": ["src-net"],
            "dstaddr": ["dst-net"],
            "service": [],
            "srcintf": ["inside"],
            "dstintf": ["outside"],
        }
        rulebase = Rulebase(uid="rb", name="rb", mgm_uid="mgm")

        fmgr_rule.parse_single_rule(normalized_config_adom, normalized_config_global, native_rule, rulebase)

        assert rulebase.rules["rule-disabled"].rule_disabled is True

    def test_parse_single_rule_raises_when_uuid_missing(self):
        normalized_config_adom = self._base_normalized_config()
        normalized_config_global = _empty_normalized_config()
        native_rule = {
            "srcaddr": ["src-net"],
            "dstaddr": ["dst-net"],
            "service": [],
            "srcintf": ["inside"],
            "dstintf": ["outside"],
        }
        rulebase = Rulebase(uid="rb", name="rb", mgm_uid="mgm")

        with pytest.raises(FwoImporterErrorInconsistenciesError):
            fmgr_rule.parse_single_rule(normalized_config_adom, normalized_config_global, native_rule, rulebase)


class TestAddImplicitDenyRule:
    def test_add_implicit_deny_rule_uses_all_wildcard_objects(self):
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "all", "obj_uid": "all-uid", "obj_ip": "0.0.0.0/32"},
            {"obj_name": "all", "obj_uid": "all-uid-v6", "obj_ip": "::/0"},
        ]
        normalized_config_adom["zone_objects"] = [{"zone_name": "any"}]
        normalized_config_global = _empty_normalized_config()
        rulebase = Rulebase(uid="rb", name="rb", mgm_uid="mgm")

        fmgr_rule.add_implicit_deny_rule(normalized_config_adom, normalized_config_global, rulebase)

        deny_rule = rulebase.rules["rb_implicit_deny"]
        assert deny_rule.rule_action == fmgr_rule.RuleAction.DROP
        assert deny_rule.rule_implied is True
        assert deny_rule.rule_src == "all|all"
        assert deny_rule.rule_dst == "all|all"
        assert deny_rule.rule_svc == "ALL"


class TestNormalizeRulebases:
    def test_normalize_rulebases_parses_gateway_rulebases_and_adds_implicit_deny(self):
        gateway = {
            "rulebase_links": [
                {"to_rulebase_uid": "rb1", "type": "ordered", "is_section": False},
            ]
        }
        native_config: dict[str, Any] = {
            "gateways": [gateway],
            "rulebases": [
                {
                    "uid": "rb1",
                    "type": "rb1",
                    "data": [
                        {
                            "uuid": "r1",
                            "name": "r1",
                            "status": 1,
                            "action": 1,
                            "srcaddr": ["all"],
                            "dstaddr": ["all"],
                            "service": ["ALL"],
                            "srcintf": ["any"],
                            "dstintf": ["any"],
                        }
                    ],
                }
            ],
            "nat_rulebases": [],
        }
        native_config_global: dict[str, Any] = {}
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "all", "obj_uid": "all-uid", "obj_ip": "0.0.0.0/32"},
            {"obj_name": "all", "obj_uid": "all-uid-v6", "obj_ip": "::/0"},
        ]
        normalized_config_adom["zone_objects"] = [{"zone_name": "any"}]
        normalized_config_global = _empty_normalized_config()

        fmgr_rule.normalize_rulebases(
            "mgm-uid", native_config, native_config_global, normalized_config_adom, normalized_config_global, False
        )

        assert len(normalized_config_adom["policies"]) == 1
        rulebase = normalized_config_adom["policies"][0]
        assert rulebase.uid == "rb1"
        assert set(rulebase.rules) == {"r1", "rb1_implicit_deny"}

    def test_normalize_rulebases_skips_already_fetched_global_rulebase(self):
        gateway = {
            "rulebase_links": [
                {"to_rulebase_uid": "rb-global", "type": "ordered", "is_section": False},
            ]
        }
        native_config: dict[str, Any] = {
            "gateways": [gateway],
            "rulebases": [],
            "nat_rulebases": [],
        }
        normalized_config_global = _empty_normalized_config()
        normalized_config_global["policies"] = [Rulebase(uid="rb-global", name="rb-global", mgm_uid="mgm-uid")]
        normalized_config_adom = _empty_normalized_config()

        fmgr_rule.normalize_rulebases(
            "mgm-uid", native_config, {}, normalized_config_adom, normalized_config_global, False
        )

        assert normalized_config_adom["policies"] == []

    def test_normalize_rulebases_links_nat_rulebase_for_every_gateway_sharing_package(self):
        gateway1: dict[str, Any] = {
            "rulebase_links": [{"to_rulebase_uid": "rb1", "type": "ordered", "is_section": False}]
        }
        gateway2: dict[str, Any] = {
            "rulebase_links": [{"to_rulebase_uid": "rb1", "type": "ordered", "is_section": False}]
        }
        native_config: dict[str, Any] = {
            "gateways": [gateway1, gateway2],
            "rulebases": [
                {
                    "uid": "rb1",
                    "type": "rb1",
                    "data": [
                        {
                            "uuid": "nat-1",
                            "name": "nat-1",
                            "nat": 1,
                            "status": 1,
                            "srcaddr": ["src-net"],
                            "dstaddr": ["dst-net"],
                            "service": ["ALL"],
                            "srcintf": ["inside"],
                            "dstintf": ["outside"],
                        }
                    ],
                }
            ],
            "nat_rulebases": [],
        }
        native_config_global: dict[str, Any] = {}
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "src-net", "obj_uid": "src-net-uid", "obj_ip": "10.0.0.0/24"},
            {"obj_name": "dst-net", "obj_uid": "dst-net-uid", "obj_ip": "10.0.1.0/24"},
            {"obj_name": "all", "obj_uid": "all-uid", "obj_ip": "0.0.0.0/32"},
            {"obj_name": "all", "obj_uid": "all-uid-v6", "obj_ip": "::/0"},
        ]
        normalized_config_adom["zone_objects"] = [
            {"zone_name": "inside"},
            {"zone_name": "outside"},
            {"zone_name": "any"},
        ]
        normalized_config_global = _empty_normalized_config()

        fmgr_rule.normalize_rulebases(
            "mgm-uid", native_config, native_config_global, normalized_config_adom, normalized_config_global, False
        )

        # Only one NAT rulebase should be created for the shared package...
        nat_rulebases = [rb for rb in normalized_config_adom["policies"] if rb.uid == "nat-rulebase-rb1"]
        assert len(nat_rulebases) == 1

        # ...but both gateways must get their own link to it.
        for gateway in (gateway1, gateway2):
            nat_links = [link for link in gateway["rulebase_links"] if link.get("type") == "nat"]
            assert nat_links == [
                {
                    "from_rulebase_uid": "rb1",
                    "to_rulebase_uid": "nat-rulebase-rb1",
                    "type": "nat",
                    "is_initial": False,
                    "is_global": False,
                    "is_section": False,
                }
            ]

    def test_normalize_rulebases_for_each_link_destination_warns_on_missing_rulebase(self, mocker: MockerFixture):
        warning_mock = mocker.patch("fwo_log.FWOLogger.warning")
        gateway = {"rulebase_links": [{"to_rulebase_uid": "missing-rb", "type": "ordered", "is_section": False}]}
        native_config: dict[str, Any] = {"rulebases": [], "nat_rulebases": []}
        normalized_config_adom = _empty_normalized_config()
        normalized_config_global = _empty_normalized_config()

        fmgr_rule.normalize_rulebases_for_each_link_destination(
            gateway,
            "mgm-uid",
            [],
            native_config,
            {},
            False,
            normalized_config_adom,
            normalized_config_global,
        )

        assert normalized_config_adom["policies"] == []
        warning_mock.assert_called_once()


class TestFindPackages:
    def test_find_packages_returns_local_and_global_pkg_names(self):
        structure = {"adom1": {"dev1": {"vdom1": {"local": "lpkg", "global": "gpkg"}}}}
        local_pkg, global_pkg = fmgr_rule.find_packages(structure, "adom1", {"name": "dev1_vdom1"})
        assert local_pkg == "lpkg"
        assert global_pkg == "gpkg"

    def test_find_packages_returns_empty_strings_when_package_info_missing(self):
        structure: dict[str, Any] = {"adom1": {"dev1": {"vdom1": {}}}}
        assert fmgr_rule.find_packages(structure, "adom1", {"name": "dev1_vdom1"}) == ("", "")

    def test_find_packages_raises_when_device_not_found(self):
        structure = {"adom1": {"dev1": {"vdom1": {"local": "lpkg", "global": "gpkg"}}}}
        with pytest.raises(FwoDeviceWithoutLocalPackageError):
            fmgr_rule.find_packages(structure, "adom1", {"name": "unknown-device"})


class TestRulebaseFetchHelpers:
    def test_is_rulebase_already_fetched(self):
        rulebases = [{"type": "rb_v4_pkg"}]
        assert fmgr_rule.is_rulebase_already_fetched(rulebases, "rb_v4_pkg") is True
        assert fmgr_rule.is_rulebase_already_fetched(rulebases, "rb_v6_pkg") is False

    def test_has_rulebase_data_marks_metadata_and_true_for_nonempty_data(self):
        rulebases = [{"type": "rb_v4_pkg", "data": [{"x": 1}]}]
        result = fmgr_rule.has_rulebase_data(rulebases, "rb_v4_pkg", is_global=True, version="v4", pkg_name="pkg")
        assert result is True
        assert rulebases[0]["uid"] == "rb_v4_pkg"
        assert rulebases[0]["name"] == "rb_v4_pkg"
        assert rulebases[0]["is_v4"] is True
        assert rulebases[0]["is_global"] is True
        assert rulebases[0]["package"] == "pkg"

    def test_has_rulebase_data_removes_empty_global_rulebase(self):
        rulebases: list[dict[str, Any]] = [{"type": "rb_v6_pkg", "data": []}]
        result = fmgr_rule.has_rulebase_data(rulebases, "rb_v6_pkg", is_global=True, version="v6", pkg_name="pkg")
        assert result is False
        assert rulebases == []

    def test_has_rulebase_data_keeps_empty_local_rulebase(self):
        rulebases: list[dict[str, Any]] = [{"type": "rb_v6_pkg", "data": []}]
        result = fmgr_rule.has_rulebase_data(rulebases, "rb_v6_pkg", is_global=False, version="v6", pkg_name="pkg")
        assert result is False
        assert len(rulebases) == 1

    def test_has_rulebase_data_returns_false_when_not_found(self):
        assert fmgr_rule.has_rulebase_data([], "rb_v4_pkg", is_global=False, version="v4", pkg_name="pkg") is False

    def test_build_link_marks_initial_when_no_previous_rulebase(self):
        link = fmgr_rule.build_link(None, "rb_v4_pkg", is_global=True)
        assert link == {
            "from_rulebase_uid": None,
            "from_rule_uid": None,
            "to_rulebase_uid": "rb_v4_pkg",
            "type": "ordered",
            "is_global": True,
            "is_initial": True,
            "is_section": False,
        }

    def test_build_link_chains_to_previous_rulebase(self):
        link = fmgr_rule.build_link("rb_v4_pkg", "rb_v6_pkg", is_global=False)
        assert link["from_rulebase_uid"] == "rb_v4_pkg"
        assert link["is_initial"] is False

    def test_link_rulebase_links_only_versions_with_data(self):
        rulebases: list[dict[str, Any]] = [
            {"type": "rb_v4_pkg", "data": [{"x": 1}]},
            {"type": "rb_v6_pkg", "data": []},
        ]
        link_list: list[Any] = []
        result = fmgr_rule.link_rulebase(link_list, rulebases, "pkg", "rb", None, is_global=False)
        assert result == "rb_v4_pkg"
        assert len(link_list) == 1
        assert link_list[0]["to_rulebase_uid"] == "rb_v4_pkg"


class TestGetAndLinkRulebases:
    @staticmethod
    def _fake_update_config(
        config_json: list[dict[str, Any]],
        _sid: str,
        _api_base_url: str,
        _api_path: str,
        result_name: str,
        **_kwargs: Any,
    ) -> None:
        config_json.append({"type": result_name, "data": [{"uuid": "native-rule"}]})

    def test_get_and_link_global_rulebase_skips_when_no_global_package(self, mocker: MockerFixture):
        api_mock = mocker.patch("fw_modules.fortiadom5ff.fmgr_getter.update_config_with_fortinet_api_call")
        native_config_global: dict[str, Any] = {"rulebases": []}

        result = fmgr_rule.get_and_link_global_rulebase(
            "header", "prev", "", native_config_global, "sid", "url", [], 100, []
        )

        assert result == "prev"
        api_mock.assert_not_called()

    def test_get_and_link_global_rulebase_fetches_and_links_v4_and_v6(self, mocker: MockerFixture):
        mocker.patch(
            "fw_modules.fortiadom5ff.fmgr_getter.update_config_with_fortinet_api_call",
            side_effect=self._fake_update_config,
        )
        native_config_global: dict[str, Any] = {"rulebases": []}
        link_list: list[Any] = []

        result = fmgr_rule.get_and_link_global_rulebase(
            "header", None, "gpkg", native_config_global, "sid", "url", [], 100, link_list
        )

        assert result == "rules_global_header_v6_gpkg"
        assert len(link_list) == 2
        assert len(native_config_global["rulebases"]) == 2

    def test_get_and_link_local_rulebase_fetches_and_links(self, mocker: MockerFixture):
        mocker.patch(
            "fw_modules.fortiadom5ff.fmgr_getter.update_config_with_fortinet_api_call",
            side_effect=self._fake_update_config,
        )
        native_config_adom: dict[str, Any] = {"rulebases": []}
        link_list: list[Any] = []

        result = fmgr_rule.get_and_link_local_rulebase(
            "rules_adom", None, "adom1", "lpkg", native_config_adom, "sid", "url", [], 100, link_list
        )

        assert result == "rules_adom_v6_lpkg"
        assert len(link_list) == 2
        assert len(native_config_adom["rulebases"]) == 2

    def test_get_access_policy_populates_device_config_rulebase_links(self, mocker: MockerFixture):
        mocker.patch(
            "fw_modules.fortiadom5ff.fmgr_getter.update_config_with_fortinet_api_call",
            side_effect=self._fake_update_config,
        )
        structure = {"adom1": {"dev1": {"vdom1": {"local": "lpkg", "global": "gpkg"}}}}
        native_config_adom: dict[str, Any] = {"rulebases": []}
        native_config_global: dict[str, Any] = {"rulebases": []}
        device_config: dict[str, Any] = {"rulebase_links": []}

        fmgr_rule.get_access_policy(
            "sid",
            "url",
            native_config_adom,
            native_config_global,
            structure,
            "adom1",
            {"name": "dev1_vdom1"},
            device_config,
            100,
        )

        assert len(device_config["rulebase_links"]) == 6
        assert len(native_config_adom["rulebases"]) == 2
        assert len(native_config_global["rulebases"]) == 4


class TestNatMiscHelpers:
    def test_add_users_to_rule_adds_groups_and_users(self):
        rule = {"rule_src": "src-net", "rule_src_refs": "src-net-uid"}
        fmgr_rule.add_users_to_rule({"groups": ["grp1"], "users": ["user1"]}, rule)

        assert rule["rule_src"] == "user1@grp1@src-net"
        assert rule["rule_src_refs"] == "user1@grp1@src-net-uid"

    def test_add_users_to_rule_is_noop_without_groups_or_users(self):
        rule = {"rule_src": "src-net", "rule_src_refs": "src-net-uid"}
        fmgr_rule.add_users_to_rule({}, rule)

        assert rule["rule_src"] == "src-net"
        assert rule["rule_src_refs"] == "src-net-uid"

    def test_is_nat_rule_detects_snat_flag(self):
        is_snat, is_dnat = fmgr_rule.is_nat_rule({"nat": 1}, _empty_normalized_config(), _empty_normalized_config())
        assert is_snat is True
        assert is_dnat is False

    def test_is_nat_rule_detects_dnat_via_vip_object(self):
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "vip-obj", "obj_native_type": "firewall/vip"},
        ]
        native_rule = {"dstaddr": ["vip-obj"]}

        is_snat, is_dnat = fmgr_rule.is_nat_rule(native_rule, normalized_config_adom, _empty_normalized_config())
        assert is_snat is False
        assert is_dnat is True

    def test_is_nat_rule_returns_false_false_for_plain_rule(self):
        is_snat, is_dnat = fmgr_rule.is_nat_rule({}, _empty_normalized_config(), _empty_normalized_config())
        assert (is_snat, is_dnat) == (False, False)

    def test_parse_nat_ip_creates_translated_network_object(self):
        normalized_config_adom = _empty_normalized_config()
        native_rule = {"uuid": "rule-1", "name": "rule-name"}

        translated_ips, translated_uids = fmgr_rule.parse_nat_ip(
            ["1.2.3.4", "255.255.255.255"], native_rule, normalized_config_adom
        )

        assert translated_ips == ["1.2.3.4/32"]
        assert translated_uids == ["rule-1_Translated_IP"]
        created_obj = normalized_config_adom["network_objects"][0]
        assert created_obj["obj_uid"] == "rule-1_Translated_IP"
        assert created_obj["obj_ip"] == "1.2.3.4"
        assert created_obj["obj_ip_end"] == "1.2.3.4"

    def test_parse_nat_ip_preserves_translated_range_for_wider_mask(self):
        normalized_config_adom = _empty_normalized_config()
        native_rule = {"uuid": "rule-1", "name": "rule-name"}

        translated_ips, translated_uids = fmgr_rule.parse_nat_ip(
            ["1.2.3.0", "255.255.255.0"], native_rule, normalized_config_adom
        )

        assert translated_ips == ["1.2.3.0/24"]
        assert translated_uids == ["rule-1_Translated_IP"]
        created_obj = normalized_config_adom["network_objects"][0]
        assert created_obj["obj_ip"] == "1.2.3.0"
        assert created_obj["obj_ip_end"] == "1.2.3.255"

    def test_parse_nat_ip_warns_and_returns_empty_for_unexpected_entry_count(self, mocker: MockerFixture):
        warning_mock = mocker.patch("fwo_log.FWOLogger.warning")
        normalized_config_adom = _empty_normalized_config()

        translated_ips, translated_uids = fmgr_rule.parse_nat_ip(["1.2.3.4"], {}, normalized_config_adom)

        assert (translated_ips, translated_uids) == ([], [])
        warning_mock.assert_called_once()


class TestPrepareTranslatedNatFields:
    def test_replaces_source_with_original_when_unchanged(self):
        result = fmgr_rule.prepare_translated_nat_fields(
            rule_src_list=["src-net"],
            rule_dst_list=["dst-net"],
            rule_svc_list=["ALL"],
            translated_src_list=["src-net"],
            translated_src_refs_list=["src-net-uid"],
            translated_dst_list=["dst-net"],
            translated_dst_refs_list=["dst-net-uid"],
            translated_svc_list=["ALL"],
            translated_svc_refs_list=["ALL"],
            native_rule={},
            is_snat=False,
            is_dnat=False,
            normalized_config_adom=_empty_normalized_config(),
        )
        translated_src_list, translated_src_refs_list, translated_dst_list, translated_dst_refs_list, *_ = result
        assert translated_src_list == ["Original"]
        assert translated_src_refs_list == ["Original"]
        assert translated_dst_list == ["Original"]
        assert translated_dst_refs_list == ["Original"]

    def test_snat_without_ippool_uses_outgoing_interface_ip(self):
        result = fmgr_rule.prepare_translated_nat_fields(
            rule_src_list=["src-net"],
            rule_dst_list=["dst-net"],
            rule_svc_list=["ALL"],
            translated_src_list=["translated-net"],
            translated_src_refs_list=["translated-net-uid"],
            translated_dst_list=["dst-net"],
            translated_dst_refs_list=["dst-net-uid"],
            translated_svc_list=["ALL"],
            translated_svc_refs_list=["ALL"],
            native_rule={"ippool": 0},
            is_snat=True,
            is_dnat=False,
            normalized_config_adom=_empty_normalized_config(),
        )
        translated_src_list, translated_src_refs_list, *_ = result
        assert translated_src_list == ["Outgoing Interface IP"]
        assert translated_src_refs_list == ["Outgoing_Interface_IP"]

    def test_keeps_dst_when_dnat_even_if_unchanged(self):
        result = fmgr_rule.prepare_translated_nat_fields(
            rule_src_list=["src-net"],
            rule_dst_list=["dst-net"],
            rule_svc_list=["ALL"],
            translated_src_list=["src-net"],
            translated_src_refs_list=["src-net-uid"],
            translated_dst_list=["dst-net"],
            translated_dst_refs_list=["dst-net-uid"],
            translated_svc_list=["ALL"],
            translated_svc_refs_list=["ALL"],
            native_rule={},
            is_snat=False,
            is_dnat=True,
            normalized_config_adom=_empty_normalized_config(),
        )
        _, _, translated_dst_list, translated_dst_refs_list, *_ = result
        assert translated_dst_list == ["dst-net"]
        assert translated_dst_refs_list == ["dst-net-uid"]

    def test_rtp_nat_creates_translated_ip_object(self):
        normalized_config_adom = _empty_normalized_config()
        result = fmgr_rule.prepare_translated_nat_fields(
            rule_src_list=["src-net"],
            rule_dst_list=["dst-net"],
            rule_svc_list=["ALL"],
            translated_src_list=["translated-net"],
            translated_src_refs_list=["translated-net-uid"],
            translated_dst_list=["dst-net"],
            translated_dst_refs_list=["dst-net-uid"],
            translated_svc_list=["ALL"],
            translated_svc_refs_list=["ALL"],
            native_rule={"rtp-nat": 1, "natip": ["1.2.3.4", "255.255.255.255"], "uuid": "rule-1"},
            is_snat=False,
            is_dnat=False,
            normalized_config_adom=normalized_config_adom,
        )
        translated_src_list, translated_src_refs_list, *_ = result
        assert translated_src_list == ["1.2.3.4/32"]
        assert translated_src_refs_list == ["rule-1_Translated_IP"]


class TestAsList:
    def test_as_list_returns_nonempty_list_unchanged(self):
        assert fmgr_rule._as_list(["a", "b"]) == ["a", "b"]

    def test_as_list_wraps_nonempty_string(self):
        assert fmgr_rule._as_list("a") == ["a"]

    @pytest.mark.parametrize("value", [[], "", None, 5])
    def test_as_list_returns_none_for_unsupported_values(self, value: Any):
        assert fmgr_rule._as_list(value) is None


class TestGetNatTranslatedSource:
    def test_get_nat_translated_source_wraps_string_poolname(self):
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "pool-a", "obj_uid": "pool-a-uid", "obj_ip": "10.0.2.1/32"}
        ]
        native_rule = {"ippool": 1, "poolname": "pool-a"}

        translated_src_list, translated_src_refs_list = fmgr_rule.get_nat_translated_source(
            native_rule, normalized_config_adom, _empty_normalized_config()
        )

        assert translated_src_list == ["pool-a"]
        assert translated_src_refs_list == ["pool-a-uid"]

    def test_get_nat_translated_source_falls_back_to_source_addresses_without_ippool(self):
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "src-net", "obj_uid": "src-net-uid", "obj_ip": "10.0.0.0/24"}
        ]
        native_rule = {"srcaddr": ["src-net"]}

        translated_src_list, translated_src_refs_list = fmgr_rule.get_nat_translated_source(
            native_rule, normalized_config_adom, _empty_normalized_config()
        )

        assert translated_src_list == ["src-net"]
        assert translated_src_refs_list == ["src-net-uid"]


class TestGetNatTranslatedDestination:
    def test_resolves_vip_destination_to_mapped_ip_object(self):
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {
                "obj_name": "vip-obj",
                "obj_uid": "vip-obj-uid",
                "obj_native_type": "firewall/vip",
                "obj_nat_ip": "10.0.0.5",
                "obj_nat_ip_end": "10.0.0.5",
            },
            {"obj_name": "10.0.0.5_NatNwObj", "obj_uid": "10.0.0.5_NatNwObj", "obj_ip": "10.0.0.5"},
        ]

        translated_dst_list, translated_dst_refs_list = fmgr_rule.get_nat_translated_destination(
            True, ["vip-obj"], ["vip-obj-uid"], normalized_config_adom, _empty_normalized_config()
        )

        assert translated_dst_list == ["10.0.0.5_NatNwObj"]
        assert translated_dst_refs_list == ["10.0.0.5_NatNwObj"]

    def test_leaves_non_vip_destination_unchanged(self):
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "dst-net", "obj_uid": "dst-net-uid", "obj_ip": "10.0.1.0/24"}
        ]

        translated_dst_list, translated_dst_refs_list = fmgr_rule.get_nat_translated_destination(
            True, ["dst-net"], ["dst-net-uid"], normalized_config_adom, _empty_normalized_config()
        )

        assert translated_dst_list == ["dst-net"]
        assert translated_dst_refs_list == ["dst-net-uid"]

    def test_passes_through_unchanged_when_not_dnat(self):
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {
                "obj_name": "vip-obj",
                "obj_uid": "vip-obj-uid",
                "obj_native_type": "firewall/vip",
                "obj_nat_ip": "10.0.0.5",
                "obj_nat_ip_end": "10.0.0.5",
            },
        ]

        translated_dst_list, translated_dst_refs_list = fmgr_rule.get_nat_translated_destination(
            False, ["vip-obj"], ["vip-obj-uid"], normalized_config_adom, _empty_normalized_config()
        )

        assert translated_dst_list == ["vip-obj"]
        assert translated_dst_refs_list == ["vip-obj-uid"]


class TestParseNatRulesInRulebaseEdgeCases:
    def test_skips_rules_that_are_neither_snat_nor_dnat(self):
        normalized_config_adom = _empty_normalized_config()
        normalized_config_global = _empty_normalized_config()
        rulebase_to_parse: dict[str, Any] = {"data": [{"uuid": "plain-rule", "srcaddr": [], "dstaddr": []}]}
        normalized_nat_rulebase = Rulebase(uid="nat-rb", name="NAT", mgm_uid="mgm")

        parse_nat_rules_in_rulebase(
            normalized_config_adom, normalized_config_global, rulebase_to_parse, normalized_nat_rulebase
        )

        assert normalized_nat_rulebase.rules == {}

    def test_skips_nat_rules_without_uuid(self, mocker: MockerFixture):
        warning_mock = mocker.patch("fwo_log.FWOLogger.warning")
        normalized_config_adom = _empty_normalized_config()
        normalized_config_global = _empty_normalized_config()
        rulebase_to_parse: dict[str, Any] = {"data": [{"nat": 1, "srcaddr": [], "dstaddr": []}]}
        normalized_nat_rulebase = Rulebase(uid="nat-rb", name="NAT", mgm_uid="mgm")

        parse_nat_rules_in_rulebase(
            normalized_config_adom, normalized_config_global, rulebase_to_parse, normalized_nat_rulebase
        )

        assert normalized_nat_rulebase.rules == {}
        warning_mock.assert_called_once()


class TestNatRulebaseWiring:
    def test_insert_parent_nat_rulebase_creates_and_appends_once(self):
        normalized_config_adom: dict[str, Any] = {"policies": []}
        rulebase = fmgr_rule.insert_parent_nat_rulebase(normalized_config_adom, "rb1", "mgm-uid")

        assert rulebase.uid == "nat-rulebase-rb1"
        assert rulebase.name == "NAT"
        assert normalized_config_adom["policies"] == [rulebase]

        rulebase_again = fmgr_rule.insert_parent_nat_rulebase(normalized_config_adom, "rb1", "mgm-uid")
        assert len(normalized_config_adom["policies"]) == 1
        assert rulebase_again is rulebase

    def test_insert_nat_rulebase_link_adds_link_once(self):
        gateway: dict[str, Any] = {"rulebase_links": []}
        fmgr_rule.insert_nat_rulebase_link("rb1", "nat-rulebase-rb1", gateway)
        assert gateway["rulebase_links"] == [
            {
                "from_rulebase_uid": "rb1",
                "to_rulebase_uid": "nat-rulebase-rb1",
                "type": "nat",
                "is_initial": False,
                "is_global": False,
                "is_section": False,
            }
        ]

        fmgr_rule.insert_nat_rulebase_link("rb1", "nat-rulebase-rb1", gateway)
        assert len(gateway["rulebase_links"]) == 1

    def test_new_process_nat_rules_for_rulebase_skips_when_no_nat_rules(self):
        gateway: dict[str, Any] = {"rulebase_links": []}
        normalized_config_adom = _empty_normalized_config()
        normalized_config_global = _empty_normalized_config()
        rulebase_to_parse = {"data": [{"uuid": "plain-rule"}]}
        normalized_rulebase = Rulebase(uid="rb1", name="rb1", mgm_uid="mgm")

        fmgr_rule.new_process_nat_rules_for_rulebase(
            gateway, normalized_config_adom, normalized_config_global, rulebase_to_parse, normalized_rulebase
        )

        assert normalized_config_adom["policies"] == []
        assert gateway["rulebase_links"] == []

    def test_new_process_nat_rules_for_rulebase_creates_nat_rulebase_and_link(self):
        gateway: dict[str, Any] = {"rulebase_links": []}
        normalized_config_adom = _empty_normalized_config()
        normalized_config_adom["network_objects"] = [
            {"obj_name": "src-net", "obj_uid": "src-net-uid", "obj_ip": "10.0.0.0/24"},
            {"obj_name": "dst-net", "obj_uid": "dst-net-uid", "obj_ip": "10.0.1.0/24"},
        ]
        normalized_config_adom["zone_objects"] = [{"zone_name": "inside"}, {"zone_name": "outside"}]
        normalized_config_global = _empty_normalized_config()
        rulebase_to_parse = {
            "data": [
                {
                    "uuid": "nat-1",
                    "name": "nat-1",
                    "nat": 1,
                    "status": 1,
                    "srcaddr": ["src-net"],
                    "dstaddr": ["dst-net"],
                    "service": ["ALL"],
                    "srcintf": ["inside"],
                    "dstintf": ["outside"],
                }
            ]
        }
        normalized_rulebase = Rulebase(uid="rb1", name="rb1", mgm_uid="mgm")

        fmgr_rule.new_process_nat_rules_for_rulebase(
            gateway, normalized_config_adom, normalized_config_global, rulebase_to_parse, normalized_rulebase
        )

        assert len(normalized_config_adom["policies"]) == 1
        nat_rulebase = normalized_config_adom["policies"][0]
        assert nat_rulebase.uid == "nat-rulebase-rb1"
        assert set(nat_rulebase.rules) == {"nat-1-original", "nat-1-translated"}
        assert gateway["rulebase_links"] == [
            {
                "from_rulebase_uid": "rb1",
                "to_rulebase_uid": "nat-rulebase-rb1",
                "type": "nat",
                "is_initial": False,
                "is_global": False,
                "is_section": False,
            }
        ]

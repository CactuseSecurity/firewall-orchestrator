import json
from datetime import datetime, timezone
from typing import Any

import pytest
from fw_modules.fortiadom5ff.fmgr_rule import (
    extract_nat_config_fields,
    parse_nat_rules_in_rulebase,
    rule_parse_last_hit,
)
from fw_modules.fortiadom5ff.fwcommon import to_time_object
from fwo_exceptions import ImportInterruptionError
from models.rulebase import Rulebase
from models.time_object import TimeObject
from pytest_mock import MockerFixture


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

from typing import Any

import pytest
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType


def _base_rule(**overrides: Any) -> RuleNormalized:
    defaults = {
        "rule_num": 1,
        "rule_num_numeric": 1.0,
        "rule_disabled": False,
        "rule_src_neg": False,
        "rule_src": "src",
        "rule_src_refs": "src_ref",
        "rule_dst_neg": False,
        "rule_dst": "dst",
        "rule_dst_refs": "dst_ref",
        "rule_svc_neg": False,
        "rule_svc": "svc",
        "rule_svc_refs": "svc_ref",
        "rule_action": RuleAction.ACCEPT,
        "rule_track": RuleTrack.NONE,
        "rule_implied": False,
        "rule_type": RuleType.ACCESS,
    }
    merged: Any = {**defaults, **overrides}
    return RuleNormalized(**merged)


@pytest.mark.parametrize(
    ("last_hit", "expected"),
    [
        ("2026-03-11T11:57:00+01:00", "2026-03-11T10:57:00+00:00"),
        ("2026-03-11T11:57:00", "2026-03-11T11:57:00+00:00"),
        ("2026-03-11T11:57:00Z", "2026-03-11T11:57:00+00:00"),
        ("2026-04-01T13:19+0200", "2026-04-01T11:19:00+00:00"),
        ("2026-04-01T13:19-0530", "2026-04-01T18:49:00+00:00"),
        (" 2026-03-11T11:57:00Z ", "2026-03-11T11:57:00+00:00"),
    ],
)
def test_last_hit_normalizes_supported_timestamp_formats(last_hit: str, expected: str):
    rule = _base_rule(last_hit=last_hit)
    assert rule.last_hit == expected


def test_last_hit_rejects_invalid_timestamp_with_updated_message():
    with pytest.raises(
        ValueError,
        match=r"Rule last_hit value 'not-a-timestamp' must be an ISO 8601 timestamp like YYYY-MM-DDTHH:MM\[:SS\]\[Z\|±HH:MM\|±HHMM\]; timestamps without a timezone are treated as UTC",
    ):
        _base_rule(last_hit="not-a-timestamp")


@pytest.mark.parametrize(
    ("field", "value_a", "value_b"),
    [
        ("rule_src_zone", "trust", "untrust"),
        ("rule_dst_zone", "dmz", "wan"),
        ("rule_src_zone", None, "trust"),
        ("rule_dst_zone", "dmz", None),
    ],
)
def test_rules_differing_only_in_zone_are_unequal(field: str, value_a: str | None, value_b: str | None):
    """Zone changes alter traffic flow; they must never be silently ignored during diff."""
    rule_a = _base_rule(**{field: value_a})
    rule_b = _base_rule(**{field: value_b})
    assert rule_a != rule_b

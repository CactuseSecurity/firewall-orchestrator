import pytest
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType


@pytest.mark.parametrize(
    ("last_hit", "expected"),
    [
        ("2026-03-11T11:57:00+01:00", "2026-03-11T10:57:00+00:00"),
        ("2026-03-11T11:57:00", "2026-03-11T11:57:00+00:00"),
        ("2026-03-11T11:57:00Z", "2026-03-11T11:57:00+00:00"),
        ("2026-04-01T13:19+0200", "2026-04-01T11:19:00+00:00"),
    ],
)
def test_last_hit_accepts_supported_timestamp_formats(last_hit: str, expected: str):
    rule = RuleNormalized(
        rule_num=1,
        rule_num_numeric=1.0,
        rule_disabled=False,
        rule_src_neg=False,
        rule_src="src",
        rule_src_refs="src_ref",
        rule_dst_neg=False,
        rule_dst="dst",
        rule_dst_refs="dst_ref",
        rule_svc_neg=False,
        rule_svc="svc",
        rule_svc_refs="svc_ref",
        rule_action=RuleAction.ACCEPT,
        rule_track=RuleTrack.NONE,
        rule_implied=False,
        rule_type=RuleType.ACCESS,
        last_hit=last_hit,
    )

    assert rule.last_hit == expected

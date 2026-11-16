from __future__ import annotations

import pytest
from models.rule_hit import RuleHit
from pydantic import ValidationError


def _rule_hit(last_hit: str) -> RuleHit:
    return RuleHit(rule_mgm_id=1, rule_uid="rule-1", gateway_mgm_id=2, gateway_uid="gw-1", last_hit=last_hit)


@pytest.mark.parametrize(
    ("last_hit", "expected"),
    [
        ("2026-11-06T10:15+0200", "2026-11-06T08:15:00+00:00"),
        ("2026-11-06T10:15:30Z", "2026-11-06T10:15:30+00:00"),
        ("2026-11-06T10:15:30", "2026-11-06T10:15:30+00:00"),
    ],
)
def test_last_hit_is_normalized_to_utc(last_hit: str, expected: str) -> None:
    assert _rule_hit(last_hit).last_hit == expected


def test_invalid_last_hit_is_rejected() -> None:
    with pytest.raises(ValidationError, match="Rule hit last_hit value 'yesterday'"):
        _rule_hit("yesterday")

import pytest
from models.rule import RuleAction


def test_caseinsensitiveenum_matches_value_and_name_ignoring_case():
    assert RuleAction("accept") is RuleAction.ACCEPT
    assert RuleAction("ACCEPT") is RuleAction.ACCEPT


def test_caseinsensitiveenum_raises_on_unknown_value():
    with pytest.raises(ValueError, match="not a valid RuleAction"):
        RuleAction("unknown-action")


def test_caseinsensitiveenum_members_are_strings():
    assert isinstance(RuleAction.ACCEPT, str)

from typing import List

class ImportStatistics:
    ErrorCount: int
    ErrorDetails: List[str]
    NetworkObjectAddCount: int
    NetworkObjectDeleteCount: int
    NetworkObjectChangeCount: int
    ServiceObjectAddCount: int
    ServiceObjectDeleteCount: int
    ServiceObjectChangeCount: int
    UserObjectAddCount: int
    UserObjectDeleteCount: int
    UserObjectChangeCount: int
    ZoneObjectAddCount: int
    ZoneObjectDeleteCount: int
    ZoneObjectChangeCount: int
    RuleAddCount: int
    RuleDeleteCount: int
    RuleChangeCount: int
    RuleMoveCount: int  # when a rule is moved within the same rulebase
    rulebase_add_count: int
    rulebase_change_count: int
    rulebase_delete_count: int
    rule_enforce_change_count: int
    
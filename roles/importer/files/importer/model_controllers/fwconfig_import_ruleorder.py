from fwo_const import rule_num_numeric_steps

class RuleOrderService:
    _instance = None
    _current_rule_num_numeric: float

    def __new__(cls):
        if cls._instance is None:
            cls._instance = super(RuleOrderService, cls).__new__(cls)
            cls._instance._current_rule_num_numeric = 0
        return cls._instance

    @property
    def current_rule_num_numeric(self) -> float:
        return self._current_rule_num_numeric

    def get_new_rule_num_numeric(self) -> float:
        self._current_rule_num_numeric += rule_num_numeric_steps
        return self._current_rule_num_numeric
    
    def reset_rule_num_numeric(self):
        self._current_rule_num_numeric = 0.0

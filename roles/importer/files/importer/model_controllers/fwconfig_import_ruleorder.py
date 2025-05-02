from fwo_const import rule_num_numeric_steps
from models.fwconfig_normalized import FwConfigNormalized
from fwo_base import compute_min_moves

class RuleOrderService:
    """
        A singleton service that holds data and provides logic to compute rule order values.
    """

    _instance = None
    _current_rule_num_numeric: float
    _previous_config: FwConfigNormalized
    _normalized_config: FwConfigNormalized


    def __new__(cls):
        """
            Singleton pattern: Creates instance and sets defaults if constructed first time and sets that object to a protected class variable. 
            If the constructor is called when there is already an instance returns that instance instead. That way there will only be one instance of this type throudgh the whole runtime.
        """

        if cls._instance is None:
            cls._instance = super(RuleOrderService, cls).__new__(cls)
            cls._instance._current_rule_num_numeric = 0
            cls._previous_config = None
            cls._normalized_config = None
            cls._compute_min_moves_result = None
            cls._source_rule_uids = []
            cls._target_rule_uids = []
            cls._deleted_rule_uids = {}
            cls._new_rule_uids = {}
            cls._moved_rule_uids = {}
            cls._source_rules_flat = []
            cls._target_rules_flat = []
            cls._needs_rule_num_numeric_update_rule_uids = []
        return cls._instance


    @property
    def current_rule_num_numeric(self) -> float:
        return self._current_rule_num_numeric
    
    @property
    def target_rules_flat(self) -> list:
        return self._target_rules_flat
    
    @property
    def compute_min_moves_result(self):
        return self._compute_min_moves_result
    

    def get_new_rule_num_numeric(self) -> float:
        self._current_rule_num_numeric += rule_num_numeric_steps
        return self._current_rule_num_numeric
    

    def reset_rule_num_numeric(self):
        self._current_rule_num_numeric = 0.0


    def initialize(self, previous_config: FwConfigNormalized, normalized_config: FwConfigNormalized):
        self._previous_config = previous_config
        self._normalized_config = normalized_config

        self._source_rule_uids = rule_uids = [
            rule_uid
            for rulebase in self._previous_config.rulebases
            for rule_uid in rulebase.Rules.keys()
        ]

        self._target_rule_uids = rule_uids = [
            rule_uid
            for rulebase in self._normalized_config.rulebases
            for rule_uid in rulebase.Rules.keys()
        ]

        self._compute_min_moves_result = compute_min_moves(self._source_rule_uids, self._target_rule_uids)

        for rulebase in self._previous_config.rulebases:
            self._deleted_rule_uids[rulebase.uid] = [
                deletion_rule_uid
                for _, deletion_rule_uid in self._compute_min_moves_result["deletions"]
                if any(deletion_rule_uid == rule_uid for rule_uid in rulebase.Rules.keys())
            ]
            self._moved_rule_uids[rulebase.uid] = [
                move_rule_uid
                for _, move_rule_uid, target_index in self._compute_min_moves_result["reposition_moves"]
                if any(move_rule_uid == rule_uid for rule_uid in rulebase.Rules.keys())
            ]
            self._needs_rule_num_numeric_update_rule_uids.extend(self._moved_rule_uids[rulebase.uid])

            self._source_rules_flat.extend(rulebase.Rules.values())
            
        for rulebase in self._normalized_config.rulebases:
            self._new_rule_uids[rulebase.uid] = [
                insertion_rule_uid
                for target_index, insertion_rule_uid in self._compute_min_moves_result["insertions"]
                if any(insertion_rule_uid == rule_uid for rule_uid in rulebase.Rules.keys())
            ]

            self._target_rules_flat.extend(rulebase.Rules.values())
            self._needs_rule_num_numeric_update_rule_uids.extend(self._new_rule_uids[rulebase.uid])
        
        for rule_uid in self._needs_rule_num_numeric_update_rule_uids:
            self.update_rule_num_numeric(rule_uid)

        return self._deleted_rule_uids, self._new_rule_uids, self._moved_rule_uids


    def update_rule_num_numeric(self, rule_uid):
        index, changed_rule = next(
            (i, rule) for i, rule in enumerate(self._target_rules_flat) if rule.rule_uid == rule_uid
        )
        previous_rule_num_numeric = 0.0
        new_rule_num_numeric = 0.0
        next_num_numeric  = 0.0

        if index > 0:
            previous_rule_num_numeric = self._target_rules_flat[index - 1].rule_num_numeric

        if index < len(self._target_rule_uids) - 1:
            next_num_numeric = self._target_rules_flat[index + 1].rule_num_numeric

        if next_num_numeric == 0:
            new_rule_num_numeric = self._target_rules_flat[index].rule_num_numeric
        else:
            new_rule_num_numeric = (previous_rule_num_numeric + next_num_numeric) / 2

        changed_rule.rule_num_numeric = new_rule_num_numeric


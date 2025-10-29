from fwo_const import rule_num_numeric_steps
from models.fwconfig_normalized import FwConfigNormalized
from models.rule import RuleNormalized
from fwo_exceptions import FwoApiFailure
from fwo_log import getFwoLogger
from services.service_provider import ServiceProvider
from services.enums import Services

class RuleOrderService:
    """
        A singleton service that holds data and provides logic to compute rule order values.
    """

    def __init__(self):
        self.initialize()

    @property
    def target_rules_flat(self) -> list[RuleNormalized]:
        return self._target_rules_flat
    
    @property
    def compute_min_moves_result(self):
        return self._compute_min_moves_result
    
    @property
    def logger(self):
        return self._logger
    
    @property
    def updated_values(self):
        return self._updated_rules
    
    @property
    def previous_config(self):
        return self._previous_config
    

    def initialize(self):
        self._service_provider = ServiceProvider()
        self._global_state = self._service_provider.get_service(Services.GLOBAL_STATE)
        self._normalized_config = self._global_state.normalized_config
        self._target_rule_uids, self._target_rules_flat = self._parse_rule_uids_and_objects_from_config(self._normalized_config)
        self._previous_config = self._global_state.previous_config
        self._source_rule_uids, self._source_rules_flat = self._parse_rule_uids_and_objects_from_config(self._previous_config)

        self._compute_min_moves_result = {
            "deletions": [], 
            "insertions": [], 
            "reposition_moves": [], 
            "moves": [], 
            "operations": []
        }

        self._deleted_rule_uids = {}
        self._new_rule_uids = {}
        self._moved_rule_uids = {}

        self._inserts_and_moves = {}
        self._updated_rules = []



    def update_rule_num_numerics(self):
        
        self._logger = getFwoLogger(debug_level=self._global_state.import_state.DebugLevel)

        from fwo_base import compute_min_moves # lazy import to avoid circular import conflict
        self._compute_min_moves_result = compute_min_moves(self._source_rule_uids, self._target_rule_uids)

        for rulebase in self._normalized_config.rulebases:

            previous_rulebase_uids = []
            previous_configs_rulebase = next((rb for rb in self._previous_config.rulebases if rb.uid == rulebase.uid), None)
            if previous_configs_rulebase:
                previous_rulebase_uids.extend(list(previous_configs_rulebase.Rules.keys()))

            rule_uids = list(rulebase.Rules.keys())

            self._new_rule_uids[rulebase.uid] = [
                insertion_uid
                for _, insertion_uid in self._compute_min_moves_result["insertions"]
                if insertion_uid in rule_uids
            ]

            self._moved_rule_uids[rulebase.uid] = [
                move_uid
                for _, move_uid, _ in self._compute_min_moves_result["reposition_moves"]
                if move_uid in rule_uids
            ]
            
            # Add undetected moves across rulebases.

            for rule_uid in rule_uids:
                if rule_uid not in self._new_rule_uids[rulebase.uid] and rule_uid not in self._moved_rule_uids[rulebase.uid] and rule_uid not in previous_rulebase_uids:
                    self._moved_rule_uids[rulebase.uid].append(rule_uid)

            if (len(self._moved_rule_uids) > 0 or len(self._new_rule_uids) > 0) and not rulebase.uid in self._inserts_and_moves:
                self._inserts_and_moves[rulebase.uid] = []

            self._inserts_and_moves[rulebase.uid].extend(self._new_rule_uids[rulebase.uid])
            self._inserts_and_moves[rulebase.uid].extend(self._moved_rule_uids[rulebase.uid])

        for rulebase in self._previous_config.rulebases:
            rule_uids = list(rulebase.Rules.keys())

            self._deleted_rule_uids[rulebase.uid] = [
                deletion_uid
                for _, deletion_uid in self._compute_min_moves_result["deletions"]
                if deletion_uid in rule_uids
            ]

        # Update rule_num_numeric

        self._update_rule_num_numerics()

        return self._deleted_rule_uids, self._new_rule_uids, self._moved_rule_uids


    def _update_rule_num_numerics(self):

        # Set initial rule_num_numerics if it is the first import.

        if len(self._source_rules_flat) == 0:
            self._set_initial_rule_num_numerics()
            return

        for rulebase_uid, rule_uids in self._inserts_and_moves.items():
            for rule_uid in rule_uids:
                        
                # Compute value if it is a consecutive insert.

                if(self._is_part_of_consecutive_insert(rule_uid)):
                     _, rule = self._get_index_and_rule_object_from_flat_list(self.target_rules_flat, rule_uid)
                     rule.rule_num_numeric = self._calculate_rule_num_numeric_for_consecutive_insert(rule_uid, rulebase_uid)
                     self.updated_values.append(rule_uid)

                # Handle singular inserts and moves.
                
                elif self._is_rule_uid_in_return_object(rule_uid, self._new_rule_uids) or self._is_rule_uid_in_return_object(rule_uid, self._moved_rule_uids):
                    self._update_rule_num_numeric_on_singular_insert_or_move(rule_uid, rulebase_uid) ## TODO: Change method name

                # Raise if unexpected rule uid.

                else:
                    raise FwoApiFailure(message="RuleOrderService: Unexpected rule_uid.")


    def _set_initial_rule_num_numerics(self):
            for _, rule_uids in self._inserts_and_moves.items():
                current_rule_num_numeric = 0
                for rule_uid in rule_uids:
                    _, changed_rule = self._get_index_and_rule_object_from_flat_list(self.target_rules_flat, rule_uid)
                    current_rule_num_numeric += rule_num_numeric_steps
                    changed_rule.rule_num_numeric = current_rule_num_numeric


    def _parse_rule_uids_and_objects_from_config(self, config: FwConfigNormalized):
        uids_and_rules = [
            (rule_uid, rule)
            for rulebase in config.rulebases
            for rule_uid, rule in rulebase.Rules.items()
        ]

        return map(list, zip(*uids_and_rules)) if uids_and_rules else ([], [])


    def _update_rule_num_numeric_on_singular_insert_or_move(self, rule_uid, target_rulebase_uid): 

        next_rules_rule_num_numeric = 0.0
        previous_rule_num_numeric = 0.0

        target_rulebase = next((rulebase for rulebase in self._normalized_config.rulebases if rulebase.uid == target_rulebase_uid), None)
        unchanged_target_rulebase = next((rulebase for rulebase in self._previous_config.rulebases if rulebase.uid == target_rulebase_uid), None)

        if target_rulebase is None:
            return
        changed_and_unchanged_rules = list(target_rulebase.Rules.values())

        if unchanged_target_rulebase:
            changed_and_unchanged_rules.extend(list(unchanged_target_rulebase.Rules.values()))
        
        index, changed_rule = self._get_index_and_rule_object_from_flat_list(list(target_rulebase.Rules.values()), rule_uid)
        prev_rule_uid, next_rule_uid = self._get_adjacent_list_element(list(target_rulebase.Rules.keys()), index)

        if not prev_rule_uid:
            min_num_numeric_rule = min((r for r in changed_and_unchanged_rules  if r.rule_num_numeric != 0), key=lambda x: x.rule_num_numeric, default=None)
            
            if min_num_numeric_rule:
                changed_rule.rule_num_numeric = min_num_numeric_rule.rule_num_numeric / 2 or 1
            else:
                changed_rule.rule_num_numeric = rule_num_numeric_steps

            
        elif not next_rule_uid:
            changed_rule.rule_num_numeric = rule_num_numeric_steps

            max_num_numeric_rule = max((r for r in changed_and_unchanged_rules), key=lambda x: x.rule_num_numeric, default=None)

            if max_num_numeric_rule:
                changed_rule.rule_num_numeric += max_num_numeric_rule.rule_num_numeric
                    
        else:
            previous_rule_num_numeric = self._get_relevant_rule_num_numeric(prev_rule_uid, self._target_rules_flat, False, target_rulebase)
            next_rules_rule_num_numeric = self._get_relevant_rule_num_numeric(next_rule_uid, self._target_rules_flat, True, target_rulebase)
            if next_rules_rule_num_numeric > 0:
                changed_rule.rule_num_numeric = (previous_rule_num_numeric + next_rules_rule_num_numeric) / 2
            else:
                changed_rule.rule_num_numeric = previous_rule_num_numeric + rule_num_numeric_steps

        self._updated_rules.append(changed_rule.rule_uid)
                    


    def _calculate_rule_num_numeric_for_consecutive_insert(self, rule_uid: str, rulebase_uid: str) -> float:
        index, _ = self._get_index_and_rule_object_from_flat_list(self.target_rules_flat, rule_uid)
        _index = index
        prev_rule_num_numeric = 0
        next_rule_num_numeric = 0

        target_rulebase = next(rulebase for rulebase in self._normalized_config.rulebases if rulebase.uid == rulebase_uid)

        while prev_rule_num_numeric == 0:
            
            prev_rule_uid, _ = self._get_adjacent_list_element(self._target_rule_uids, _index)

            if prev_rule_uid and prev_rule_uid in list(next(rulebase for rulebase in self._normalized_config.rulebases if rulebase.uid == rulebase_uid).Rules.keys()):
                prev_rule_num_numeric = self._get_relevant_rule_num_numeric(prev_rule_uid, self._target_rules_flat, False, target_rulebase)
                _index -= 1
            else:
                break

        _index = index

        while next_rule_num_numeric == 0:
            
            _, next_rule_uid = self._get_adjacent_list_element(self._target_rule_uids, _index)

            if next_rule_uid and next_rule_uid in list(next(rulebase for rulebase in self._normalized_config.rulebases if rulebase.uid == rulebase_uid).Rules.keys()):
                next_rule_num_numeric = self._get_relevant_rule_num_numeric(next_rule_uid, self._target_rules_flat, True, target_rulebase)
                _index += 1
            else:
                break

        if next_rule_num_numeric == 0:
            next_rule_num_numeric = prev_rule_num_numeric + rule_num_numeric_steps
            return next_rule_num_numeric
        

        return (prev_rule_num_numeric + next_rule_num_numeric) / 2

    def _is_part_of_consecutive_insert(self, rule_uid: str):

        # Only inserts.

        if not self._is_rule_uid_in_return_object(rule_uid, self._new_rule_uids):
            return False

        # Cant be consecutive, if there is only one insert

        number_of_rule_uids = 0

        for rulebase in self._new_rule_uids.values():
            number_of_rule_uids += len(rulebase)

        if number_of_rule_uids < 2:
            return False
        
        # Evaluate adjacent rule_uids
        
        index, _ = self._get_index_and_rule_object_from_flat_list(self._target_rules_flat, rule_uid)

        prev_rule_uid, next_rule_uid = self._get_adjacent_list_element(self._target_rule_uids, index)

        if prev_rule_uid and self._is_rule_uid_in_return_object(prev_rule_uid, self._new_rule_uids):
            return True
        
        if next_rule_uid and self._is_rule_uid_in_return_object(next_rule_uid, self._new_rule_uids):
            return True


    def _get_adjacent_list_element(self, lst, index):
        if not lst or index < 0 or index >= len(lst):
            return None, None

        prev_item = lst[index - 1] if index - 1 >= 0 else None
        next_item = lst[index + 1] if index + 1 < len(lst) else None
        return prev_item, next_item

    
    def _get_index_and_rule_object_from_flat_list(self, flat_list, rule_uid):
        return next(
            (i, rule) for i, rule in enumerate(flat_list) if rule.rule_uid == rule_uid
        )
    

    def _get_relevant_rule_num_numeric(
        self,
        rule_uid,
        flat_list,
        ascending: bool,
        target_rulebase
    ) -> float:
        """
        Returns the relevant rule_num_numeric for rule_uid.
        - Prefers already updated rules
        - Handles consecutive inserts
        - Handles new/moved rules relative to neighbors in the target
        - Falls back to the source rules
        Always returns a numeric value.
        """

        # 1) Already updated rule? -> simple return
        if rule_uid in self._updated_rules:
            _, rule = self._get_index_and_rule_object_from_flat_list(flat_list, rule_uid)
            return float(rule.rule_num_numeric)

        # 2) Part of a consecutive insert? -> defined value (0)
        if self._is_part_of_consecutive_insert(rule_uid):
            return 0.0

        # 3) New or moved rule? -> determine neighbors in the target
        if self._is_rule_uid_in_return_object(rule_uid, self._new_rule_uids) or \
        self._is_rule_uid_in_return_object(rule_uid, self._moved_rule_uids):
            return self._compute_num_for_changed_rule(rule_uid, ascending, target_rulebase)

        # 4) Fallback: value from the source rules
        _, rule = self._get_index_and_rule_object_from_flat_list(self._source_rules_flat, rule_uid)
        return float(rule.rule_num_numeric)


    def _compute_num_for_changed_rule(self, rule_uid, ascending: bool, target_rulebase) -> float:
        """Calculates rule_num_numeric for a new/moved rule relative to its neighbors in the target."""
        # Get rule & neighbors in the target
        index, changed_rule = self._get_index_and_rule_object_from_flat_list(
            target_rulebase.Rules.values(), rule_uid
        )
        prev_uid, next_uid = self._get_adjacent_list_element(list(target_rulebase.Rules.keys()), index)

        if ascending:
            return self._num_for_ascending_case(changed_rule, next_uid, target_rulebase)
        else:
            return self._num_for_descending_case(changed_rule, prev_uid, target_rulebase)


    def _num_for_ascending_case(self, changed_rule, next_uid, target_rulebase) -> float:
        """
        Ascending:
        - If a next neighbor exists, recursively use its relevant value
        - Otherwise, align with the maximum rule in the target (and update changed_rule)
        """
        if next_uid:
            return float(self._get_relevant_rule_num_numeric(
                next_uid, None, True, target_rulebase
            ))

        max_rule = self._max_num_numeric_rule(target_rulebase)
        if max_rule:
            changed_rule.rule_num_numeric = max_rule.rule_num_numeric
            return float(max_rule.rule_num_numeric)

        # If no max exists, set to 0
        changed_rule.rule_num_numeric = 0
        return 0.0


    def _num_for_descending_case(self, changed_rule, prev_uid, target_rulebase) -> float:
        """
        Descending:
        - If a previous neighbor exists, recursively use its relevant value
        - Otherwise, halve the minimum > 0 (or fall back to a step value)
        """
        if prev_uid:
            return float(self._get_relevant_rule_num_numeric(
                prev_uid, None, False, target_rulebase
            ))

        min_rule = self._min_nonzero_num_numeric_rule(target_rulebase)
        if min_rule:
            # Halve the min value or use 1 – whichever is larger (as intended in original)
            half = min_rule.rule_num_numeric / 2.0
            changed_rule.rule_num_numeric = half if half >= 1 else 1
            return float(changed_rule.rule_num_numeric)

        # Fallback if there are no >0 values
        # step = getattr(self, "rule_num_numeric_steps", 1)
        # changed_rule.rule_num_numeric = step
        return 0


    def _max_num_numeric_rule(self, target_rulebase):
        """Return the rule with the maximum rule_num_numeric, or None if empty."""
        return max(
            (r for r in target_rulebase.Rules.values()),
            key=lambda x: x.rule_num_numeric,
            default=None
        )


    def _min_nonzero_num_numeric_rule(self, target_rulebase):
        """Return the rule with the minimum non-zero rule_num_numeric, or None if none exist."""
        return min(
            (r for r in target_rulebase.Rules.values() if getattr(r, "rule_num_numeric", 0) != 0),
            key=lambda x: x.rule_num_numeric,
            default=None
        )


    def _is_rule_uid_in_return_object(self, rule_uid, return_object):
        for rule_uids in return_object.values():
            for _rule_uid in rule_uids:
                if rule_uid == _rule_uid:
                    return True
                
        return False
    

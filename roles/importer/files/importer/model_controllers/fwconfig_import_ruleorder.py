from typing import TYPE_CHECKING
from fwo_const import rule_num_numeric_steps
from models.fwconfig_normalized import FwConfigNormalized
from fwo_base import compute_min_moves
from fwo_exceptions import FwoApiFailure
if TYPE_CHECKING:
    from model_controllers.fwconfig_import_rule import FwConfigImportRule
from fwo_log import getFwoLogger
import traceback

class RuleOrderService:
    """
        A singleton service that holds data and provides logic to compute rule order values.
    """

    _instance = None
    _fw_config_import_rule = None

    _current_rule_num_numeric: float
    _previous_config: FwConfigNormalized
    

    def __new__(cls):
        """
            Singleton pattern: Creates instance and sets defaults if constructed first time and sets that object to a protected class variable. 
            If the constructor is called when there is already an instance returns that instance instead. That way there will only be one instance of this type throudgh the whole runtime.
        """

        if cls._instance is None:
            from model_controllers.fwconfig_import_rule import FwConfigImportRule
            cls._instance = super(RuleOrderService, cls).__new__(cls)
            cls._instance._current_rule_num_numeric = 0
            cls._instance._previous_config = None
            cls._instance._compute_min_moves_result = None
            cls._instance._source_rule_uids = []
            cls._instance._source_rules_flat = []
            cls._instance._target_rule_uids = []
            cls._instance._target_rules_flat = []
            cls._instance._deleted_rule_uids = {}
            cls._instance._new_rule_uids = {}
            cls._instance._moved_rule_uids = {}
            cls._instance._needs_rule_num_numeric_update_rule_uids = []
            cls._instance._updated_rules = []
            cls._instance._logger = None
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
    
    @property
    def fw_config_import_rule(self):
        return self._fw_config_import_rule
    
    @property
    def logger(self):
        return self._logger
    
    @property
    def updated_values(self):
        return self._updated_rules
    
    @property
    def previous_config(self):
        return self._previous_config
    

    def get_new_rule_num_numeric(self) -> float:
        self._current_rule_num_numeric += rule_num_numeric_steps
        return self._current_rule_num_numeric
    

    def reset_to_defaults(self):
        self._current_rule_num_numeric = 0.0
        self._previous_config = None
        self._normalized_config = None
        self._compute_min_moves_result = None
        self._source_rule_uids = []
        self._target_rule_uids = []
        self._deleted_rule_uids = {}
        self._new_rule_uids = {}
        self._moved_rule_uids = {}
        self._source_rules_flat = []
        self._target_rules_flat = []
        self._needs_rule_num_numeric_update_rule_uids = []
        self._updated_rules = []


    def initialize(self, previous_config: FwConfigNormalized, fw_config_import_rule: "FwConfigImportRule"):

        self._logger = getFwoLogger(debug_level=fw_config_import_rule.ImportDetails.DebugLevel)
        self.reset_to_defaults()
        self._previous_config = previous_config
        self._fw_config_import_rule = fw_config_import_rule

        # Parse configs to rule uid lists and rule object lists, for easier handling.

        self._source_rule_uids, self._source_rules_flat = self._parse_rule_uids_and_objects_from_config(self._previous_config)
        self._target_rule_uids, self._target_rules_flat = self._parse_rule_uids_and_objects_from_config(self._fw_config_import_rule.NormalizedConfig)

        # Compute needed operations and prepare return objects.

        self._compute_min_moves_result = compute_min_moves(self._source_rule_uids, self._target_rule_uids)

        for rulebase in self._fw_config_import_rule.NormalizedConfig.rulebases:
            rule_uids = set(rulebase.Rules.keys())

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

            self._needs_rule_num_numeric_update_rule_uids.extend(self._new_rule_uids[rulebase.uid])

        for rulebase in self._previous_config.rulebases:
            rule_uids = set(rulebase.Rules.keys())

            self._deleted_rule_uids[rulebase.uid] = [
                deletion_uid
                for _, deletion_uid in self._compute_min_moves_result["deletions"]
                if deletion_uid in rule_uids
            ]


            self._needs_rule_num_numeric_update_rule_uids.extend(self._moved_rule_uids[rulebase.uid])

        # Update rule_num_numeric

        self.update_rule_num_numerics()

        return self._deleted_rule_uids, self._new_rule_uids, self._moved_rule_uids


    def update_rule_num_numerics(self):
        for rulebase in self.fw_config_import_rule.NormalizedConfig.rulebases:
            rules = list(rulebase.Rules.values())
            for index, rule in enumerate(rules):
                if rule.rule_uid in self._needs_rule_num_numeric_update_rule_uids:

            # Set initial rule_num_numerics if it is the first import.

                    if len(self._source_rules_flat) == 0:
                        _, changed_rule = self._get_index_and_rule_object_from_flat_list(self.target_rules_flat, rule.rule_uid)
                        changed_rule.rule_num_numeric = self.get_new_rule_num_numeric()
                        if index == len(rules) - 1:
                            self._current_rule_num_numeric = 0
                        continue
                    
                    # Compute value if it is a consecutive insert.

                    fixed_value = 0

                    if(self._is_part_of_consecutive_insert(rule.rule_uid)):
                        fixed_value = self._calculate_rule_num_numeric_for_consecutive_insert(rule.rule_uid)

                    # Handle inserts and moves.
                    
                    if self._is_rule_uid_in_return_object(rule.rule_uid, self._new_rule_uids) or self._is_rule_uid_in_return_object(rule.rule_uid, self._moved_rule_uids):
                        self._update_rule_num_numeric_on_singular_insert_or_move(rule.rule_uid, fixed_value) ## TODO: Change method name

                    # Raise if unexpected rule uid.

                    else:
                        raise FwoApiFailure(message="RuleOrderService: Unexpected rule_uid.")


    def _parse_rule_uids_and_objects_from_config(self, config: FwConfigNormalized):
        uids_and_rules = [
            (rule_uid, rule)
            for rulebase in config.rulebases
            for rule_uid, rule in rulebase.Rules.items()
        ]

        return map(list, zip(*uids_and_rules)) if uids_and_rules else ([], [])


    def _update_rule_num_numeric_on_singular_insert_or_move(self, rule_uid, fixed_value=0): 

        new_rule_num_numeric = 0.0
        next_rules_rule_num_numeric = 0.0
        previous_rule_num_numeric = 0.0

        index, changed_rule = self._get_index_and_rule_object_from_flat_list(self._target_rules_flat, rule_uid)
        prev_rule_uid, next_rule_uid = self._get_adjacent_list_element(self._target_rule_uids, index)

        if not prev_rule_uid:
            next_rules_rule_num_numeric = self._get_relevant_rule_num_numeric(next_rule_uid, self.fw_config_import_rule.ImportDetails, self._target_rules_flat)
            changed_rule.rule_num_numeric = next_rules_rule_num_numeric / 2 or 1
            
        elif not next_rule_uid:
            previous_rule_num_numeric = self._get_relevant_rule_num_numeric(prev_rule_uid, self.fw_config_import_rule.ImportDetails, self._target_rules_flat)
            changed_rule.rule_num_numeric = previous_rule_num_numeric + rule_num_numeric_steps
                    
        else:
            previous_rule_num_numeric = self._get_relevant_rule_num_numeric(prev_rule_uid, self.fw_config_import_rule.ImportDetails, self._target_rules_flat)
            next_rules_rule_num_numeric = self._get_relevant_rule_num_numeric(next_rule_uid, self.fw_config_import_rule.ImportDetails, self._target_rules_flat)

            if fixed_value > 0: # True if it is a consecutive insert.
                changed_rule.rule_num_numeric = fixed_value
            else:    
                changed_rule.rule_num_numeric = (previous_rule_num_numeric + next_rules_rule_num_numeric) / 2

        self._updated_rules.append(changed_rule.rule_uid)
                    


    def _calculate_rule_num_numeric_for_consecutive_insert(self, rule_uid):
        index, _ = self._get_index_and_rule_object_from_flat_list(self.target_rules_flat, rule_uid)
        _index = index
        prev_rule_num_numeric = 0
        next_rule_num_numeric = 0

        while prev_rule_num_numeric == 0:
            
            prev_rule_uid, _ = self._get_adjacent_list_element(self._target_rule_uids, _index)

            if prev_rule_uid:
                prev_rule_num_numeric = self._get_relevant_rule_num_numeric(prev_rule_uid, self.fw_config_import_rule.ImportDetails, self._target_rules_flat)
                _index -= 1
            else:
                break

        _index = index

        while next_rule_num_numeric == 0:
            
            _, next_rule_uid = self._get_adjacent_list_element(self._target_rule_uids, _index)

            if next_rule_uid:
                next_rule_num_numeric = self._get_relevant_rule_num_numeric(next_rule_uid, self.fw_config_import_rule.ImportDetails, self._target_rules_flat)
                _index += 1
            else:
                break

        if next_rule_num_numeric == 0:
            next_rule_num_numeric = prev_rule_num_numeric + rule_num_numeric_steps
        

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


    def _get_rule_num_numeric_from_db(self, rule_uid, import_state):

        get_rule_num_numeric_by_uid_query ="""query GetRuleNumNumericByUid($rule_uid: String!) {
            rule(where: { rule_uid: { _eq: $rule_uid }, removed: { _is_null: true } }) {
                rule_num_numeric
            }
        }
        """

        try:
            query_result = import_state.call(get_rule_num_numeric_by_uid_query, queryVariables={ 'rule_uid': rule_uid })
            if 'errors' in query_result:
                self._logger.exception(f"fwo_api:importNwObject - error in addNewRuleMetadata: {str(query_result['errors'])}")
                raise FwoApiFailure(message="Failed to query rule_num_numeric by uid for unknown reason.")
        except Exception:
            raise FwoApiFailure(f"Failed to query rule_num_numeric by uid: {str(traceback.format_exc())}")
        
        return query_result["data"]["rule"][0]["rule_num_numeric"]
    

    def _get_relevant_rule_num_numeric(self, rule_uid, import_state, flat_list):
        relevant_rule_num_numeric = 0.0

        if rule_uid in self._updated_rules:
            _, rule = self._get_index_and_rule_object_from_flat_list(flat_list, rule_uid)
            relevant_rule_num_numeric = rule.rule_num_numeric
        elif self._is_part_of_consecutive_insert(rule_uid):
            relevant_rule_num_numeric = 0
        else:
            relevant_rule_num_numeric = self._get_rule_num_numeric_from_db(rule_uid, import_state)

        return relevant_rule_num_numeric
    

    def _is_rule_uid_in_return_object(self, rule_uid, return_object):
        for rule_uids in return_object.values():
            for _rule_uid in rule_uids:
                if rule_uid == _rule_uid:
                    return True
                
        return False
from typing import TYPE_CHECKING, Any

from fwo_const import RULE_NUM_NUMERIC_STEPS
from fwo_exceptions import FwoApiFailure
from models.rule import RuleNormalized
from models.rulebase import Rulebase
from services.enums import Services
from services.global_state import GlobalState
from services.service_provider import ServiceProvider

if TYPE_CHECKING:
    from models.fwconfig_normalized import FwConfigNormalized

# Constants
CONFIG_OBJECTS_NOT_INITIALIZED_ERROR = (
    "Config objects in global state not correctly initialized — expected non-None values."
)


class RuleOrderService:
    """
    A singleton service that holds data and provides logic to compute rule order values.
    """

    _service_provider: ServiceProvider
    _global_state: GlobalState
    _normalized_config: "FwConfigNormalized | None"
    _previous_config: "FwConfigNormalized | None"

    _target_rule_uids: list[str]
    _target_rules_flat: list[RuleNormalized]
    _source_rule_uids: list[str]
    _source_rules_flat: list[RuleNormalized]

    _min_moves: dict[str, Any]

    _deleted_rule_uids: dict[str, list[str]]
    _new_rule_uids: dict[str, list[str]]
    _moved_rule_uids: dict[str, list[str]]

    _inserts_and_moves: dict[str, list[str]]
    _updated_rules: list[str]

    def __init__(self):
        self._initialize(set_configs=False)

    @property
    def target_rules_flat(self) -> list[RuleNormalized]:
        return self._target_rules_flat

    def update_rule_order_diffs(self) -> dict[str, dict[str, list[str]]]:
        """
        Determines diffs that are relevant for the rule order and updates rule_num_numeric in the corresponding rules in normalized config.
        """
        self._initialize()
        self._calculate_necessary_transformations()
        self._update_rule_num_numerics()

        return {
            "deleted_rule_uids": self._deleted_rule_uids,
            "new_rule_uids": self._new_rule_uids,
            "moved_rule_uids": self._moved_rule_uids,
        }

    def _initialize(self, set_configs: bool = True) -> None:
        """
        Prepares rule order service to calculate rule order diffs and update rule num numerics.
        Expects previous config object and normalized config object in global state.
        """
        # Get logger and global state.

        self._service_provider = ServiceProvider()
        self._global_state = self._service_provider.get_service(Services.GLOBAL_STATE)

        # Verifies config objects in global state.

        if set_configs:  # make premature initialization via __init__ possible
            if not self._global_state.normalized_config or not self._global_state.previous_config:
                raise ValueError(CONFIG_OBJECTS_NOT_INITIALIZED_ERROR)

            # Instantiate objects for next steps.

            self._normalized_config = self._global_state.normalized_config
            self._previous_config = self._global_state.previous_config

        self._deleted_rule_uids = {}
        self._new_rule_uids = {}
        self._moved_rule_uids = {}

        self._inserts_and_moves = {}
        self._updated_rules = []

    def _calculate_necessary_transformations(self):
        if self._normalized_config is None or self._previous_config is None:
            raise ValueError(CONFIG_OBJECTS_NOT_INITIALIZED_ERROR)

        # Parse rules from config to flat lists.

        self._target_rule_uids, self._target_rules_flat = self._parse_rule_uids_and_objects_from_config(
            self._normalized_config
        )
        self._source_rule_uids, self._source_rules_flat = self._parse_rule_uids_and_objects_from_config(
            self._previous_config
        )

        # Compute necessary mutations.

        from fwo_base import compute_min_moves  # lazy import to avoid circular import conflict

        self._min_moves = compute_min_moves(
            self._source_rule_uids, self._target_rule_uids
        )  # use flat lists to get moves across rulebases right

        # Translate _min_moves to rulebase uid keyed dictionaries of rule uid lists.

        for rulebase in self._normalized_config.rulebases:
            previous_rulebase_uids: list[str] = []
            previous_configs_rulebase = next(
                (rb for rb in self._previous_config.rulebases if rb.uid == rulebase.uid), None
            )
            if previous_configs_rulebase:
                previous_rulebase_uids.extend(list(previous_configs_rulebase.rules.keys()))

            rule_uids = list(rulebase.rules.keys())

            self._new_rule_uids[rulebase.uid] = [
                insertion_uid for _, insertion_uid in self._min_moves["insertions"] if insertion_uid in rule_uids
            ]

            self._moved_rule_uids[rulebase.uid] = [
                move_uid for _, move_uid, _ in self._min_moves["reposition_moves"] if move_uid in rule_uids
            ]

            # Add undetected moves (i.e. across rulebases).
            for rule_uid in rule_uids:
                if (
                    rule_uid not in self._new_rule_uids[rulebase.uid]
                    and rule_uid not in self._moved_rule_uids[rulebase.uid]
                    and rule_uid not in previous_rulebase_uids
                ):
                    self._moved_rule_uids[rulebase.uid].append(rule_uid)

            self._inserts_and_moves[rulebase.uid] = []
            self._inserts_and_moves[rulebase.uid].extend(self._new_rule_uids[rulebase.uid])
            self._inserts_and_moves[rulebase.uid].extend(self._moved_rule_uids[rulebase.uid])

        for rulebase in self._previous_config.rulebases:
            rule_uids = list(rulebase.rules.keys())

            self._deleted_rule_uids[rulebase.uid] = [
                deletion_uid for _, deletion_uid in self._min_moves["deletions"] if deletion_uid in rule_uids
            ]

    def _update_rule_num_numerics(self):
        # Set initial rule_num_numerics if it is the first import.

        if len(self._source_rules_flat) == 0:
            self._set_initial_rule_num_numerics()
            return

        for rulebase_uid, rule_uids in self._inserts_and_moves.items():
            for rule_uid in rule_uids:
                # Compute value if it is a consecutive insert.

                if self._is_part_of_consecutive_insert(rule_uid):
                    self._update_rule_on_consecutive_insert(rule_uid, rulebase_uid)
                    self._updated_rules.append(rule_uid)

                # Handle singular inserts and moves.

                elif self._is_rule_uid_in_return_object(
                    rule_uid, self._new_rule_uids
                ) or self._is_rule_uid_in_return_object(rule_uid, self._moved_rule_uids):
                    self._update_rule_on_move_or_insert(rule_uid, rulebase_uid)
                    self._updated_rules.append(rule_uid)

                # Raise if unexpected rule uid.

                else:
                    raise FwoApiFailure(message="RuleOrderService: Unexpected rule_uid.")

    def _set_initial_rule_num_numerics(self):
        for rule_uids in self._inserts_and_moves.values():
            current_rule_num_numeric = 0
            for rule_uid in rule_uids:
                _, changed_rule = self._get_index_and_rule_object_from_flat_list(self._target_rules_flat, rule_uid)
                current_rule_num_numeric += RULE_NUM_NUMERIC_STEPS
                changed_rule.rule_num_numeric = current_rule_num_numeric

    def _update_rule_on_move_or_insert(self, rule_uid: str, target_rulebase_uid: str) -> None:
        next_rules_rule_num_numeric = 0.0
        previous_rule_num_numeric = 0.0

        if self._normalized_config is None or self._previous_config is None:
            raise ValueError(CONFIG_OBJECTS_NOT_INITIALIZED_ERROR)
        target_rulebase = next(
            (rulebase for rulebase in self._normalized_config.rulebases if rulebase.uid == target_rulebase_uid), None
        )
        unchanged_target_rulebase = next(
            (rulebase for rulebase in self._previous_config.rulebases if rulebase.uid == target_rulebase_uid), None
        )

        if target_rulebase is None:
            return
        changed_and_unchanged_rules = list(target_rulebase.rules.values())

        if unchanged_target_rulebase:
            changed_and_unchanged_rules.extend(list(unchanged_target_rulebase.rules.values()))

        index, changed_rule = self._get_index_and_rule_object_from_flat_list(
            list(target_rulebase.rules.values()), rule_uid
        )
        prev_rule_uid, next_rule_uid = self._get_adjacent_list_element(list(target_rulebase.rules.keys()), index)

        if not prev_rule_uid:
            min_num_numeric_rule = min(
                (r for r in changed_and_unchanged_rules if r.rule_num_numeric != 0),
                key=lambda x: x.rule_num_numeric,
                default=None,
            )

            if min_num_numeric_rule:
                changed_rule.rule_num_numeric = max(min_num_numeric_rule.rule_num_numeric / 2, 1)
            else:
                changed_rule.rule_num_numeric = RULE_NUM_NUMERIC_STEPS

        elif not next_rule_uid:
            changed_rule.rule_num_numeric = RULE_NUM_NUMERIC_STEPS

            max_num_numeric_rule = max(changed_and_unchanged_rules, key=lambda x: x.rule_num_numeric, default=None)

            if max_num_numeric_rule:
                changed_rule.rule_num_numeric += max_num_numeric_rule.rule_num_numeric

        else:
            previous_rule_num_numeric = self._get_relevant_rule_num_numeric(
                prev_rule_uid, self._target_rules_flat, False, target_rulebase
            )
            next_rules_rule_num_numeric = self._get_relevant_rule_num_numeric(
                next_rule_uid, self._target_rules_flat, True, target_rulebase
            )
            if next_rules_rule_num_numeric > 0:
                changed_rule.rule_num_numeric = (previous_rule_num_numeric + next_rules_rule_num_numeric) / 2
            else:
                changed_rule.rule_num_numeric = previous_rule_num_numeric + RULE_NUM_NUMERIC_STEPS

    def _update_rule_on_consecutive_insert(self, rule_uid: str, rulebase_uid: str) -> None:
        index, rule = self._get_index_and_rule_object_from_flat_list(self._target_rules_flat, rule_uid)
        _index = index
        prev_rule_num_numeric = 0
        next_rule_num_numeric = 0

        if self._normalized_config is None:
            raise ValueError(CONFIG_OBJECTS_NOT_INITIALIZED_ERROR)
        target_rulebase = next(
            rulebase for rulebase in self._normalized_config.rulebases if rulebase.uid == rulebase_uid
        )

        while prev_rule_num_numeric == 0:
            prev_rule_uid, _ = self._get_adjacent_list_element(self._target_rule_uids, _index)

            if prev_rule_uid and prev_rule_uid in list(
                next(
                    rulebase for rulebase in self._normalized_config.rulebases if rulebase.uid == rulebase_uid
                ).rules.keys()
            ):
                prev_rule_num_numeric = self._get_relevant_rule_num_numeric(
                    prev_rule_uid, self._target_rules_flat, False, target_rulebase
                )
                _index -= 1
            else:
                break

        _index = index

        while next_rule_num_numeric == 0:
            _, next_rule_uid = self._get_adjacent_list_element(self._target_rule_uids, _index)

            if next_rule_uid and next_rule_uid in list(
                next(
                    rulebase for rulebase in self._normalized_config.rulebases if rulebase.uid == rulebase_uid
                ).rules.keys()
            ):
                next_rule_num_numeric = self._get_relevant_rule_num_numeric(
                    next_rule_uid, self._target_rules_flat, True, target_rulebase
                )
                _index += 1
            else:
                break

        if next_rule_num_numeric == 0:
            next_rule_num_numeric = prev_rule_num_numeric + RULE_NUM_NUMERIC_STEPS
            rule.rule_num_numeric = next_rule_num_numeric
            return

        rule.rule_num_numeric = (prev_rule_num_numeric + next_rule_num_numeric) / 2

    def _parse_rule_uids_and_objects_from_config(
        self, config: "FwConfigNormalized"
    ) -> tuple[list[str], list[RuleNormalized]]:
        uids_and_rules = [
            (rule_uid, rule) for rulebase in config.rulebases for rule_uid, rule in rulebase.rules.items()
        ]

        if not uids_and_rules:
            return ([], [])

        uids, rules = zip(*uids_and_rules, strict=False)
        return (list(uids), list(rules))

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
        return None

    def _get_adjacent_list_element(self, lst: list[str], index: int) -> tuple[str | None, str | None]:
        if not lst or index < 0 or index >= len(lst):
            return None, None

        prev_item = lst[index - 1] if index - 1 >= 0 else None
        next_item = lst[index + 1] if index + 1 < len(lst) else None
        return prev_item, next_item

    def _get_index_and_rule_object_from_flat_list(self, flat_list: list[RuleNormalized], rule_uid: str):
        return next((i, rule) for i, rule in enumerate(flat_list) if rule.rule_uid == rule_uid)

    def _get_relevant_rule_num_numeric(
        self,
        rule_uid: str,
        flat_list: list[RuleNormalized] | None,  # TODO flat_list should not be needed here
        ascending: bool,
        target_rulebase: Rulebase,
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
            _, rule = self._get_index_and_rule_object_from_flat_list(self._target_rules_flat, rule_uid)
            return float(rule.rule_num_numeric)

        # 2) Part of a consecutive insert? -> defined value (0)
        if self._is_part_of_consecutive_insert(rule_uid):
            return 0.0

        # 3) New or moved rule? -> determine neighbors in the target
        if self._is_rule_uid_in_return_object(rule_uid, self._new_rule_uids) or self._is_rule_uid_in_return_object(
            rule_uid, self._moved_rule_uids
        ):
            return self._compute_num_for_changed_rule(rule_uid, ascending, target_rulebase)

        # 4) Fallback: value from the source rules
        _, rule = self._get_index_and_rule_object_from_flat_list(self._source_rules_flat, rule_uid)
        return float(rule.rule_num_numeric)

    def _compute_num_for_changed_rule(self, rule_uid: str, ascending: bool, target_rulebase: Rulebase) -> float:
        """Calculates rule_num_numeric for a new/moved rule relative to its neighbors in the target."""
        # Get rule & neighbors in the target
        index, changed_rule = self._get_index_and_rule_object_from_flat_list(
            list(target_rulebase.rules.values()), rule_uid
        )
        prev_uid, next_uid = self._get_adjacent_list_element(list(target_rulebase.rules.keys()), index)

        if ascending:
            return self._num_for_ascending_case(changed_rule, next_uid, target_rulebase)
        return self._num_for_descending_case(changed_rule, prev_uid, target_rulebase)

    def _num_for_ascending_case(
        self, changed_rule: RuleNormalized, next_uid: str | None, target_rulebase: Rulebase
    ) -> float:
        """
        Ascending:
        - If a next neighbor exists, recursively use its relevant value
        - Otherwise, align with the maximum rule in the target (and update changed_rule)
        """
        if next_uid:
            return float(self._get_relevant_rule_num_numeric(next_uid, None, True, target_rulebase))

        max_rule = self._max_num_numeric_rule(target_rulebase)
        if max_rule:
            changed_rule.rule_num_numeric = max_rule.rule_num_numeric
            return float(max_rule.rule_num_numeric)

        # If no max exists, set to 0
        changed_rule.rule_num_numeric = 0
        return 0.0

    def _num_for_descending_case(
        self, changed_rule: RuleNormalized, prev_uid: str | None, target_rulebase: Rulebase
    ) -> float:
        """
        Descending:
        - If a previous neighbor exists, recursively use its relevant value
        - Otherwise, halve the minimum > 0 (or fall back to a step value)
        """
        if prev_uid:
            return float(self._get_relevant_rule_num_numeric(prev_uid, None, False, target_rulebase))

        min_rule = self._min_nonzero_num_numeric_rule(target_rulebase)
        if min_rule:
            # Halve the min value or use 1 – whichever is larger (as intended in original)
            half = min_rule.rule_num_numeric / 2.0
            changed_rule.rule_num_numeric = max(half, 1)
            return float(changed_rule.rule_num_numeric)

        # Fallback if there are no >0 values
        # step = getattr(self, "rule_num_numeric_steps", 1)
        # changed_rule.rule_num_numeric = step
        return 0

    def _max_num_numeric_rule(self, target_rulebase: Rulebase):
        """Return the rule with the maximum rule_num_numeric, or None if empty."""
        return max(target_rulebase.rules.values(), key=lambda x: x.rule_num_numeric, default=None)

    def _min_nonzero_num_numeric_rule(self, target_rulebase: Rulebase):
        """Return the rule with the minimum non-zero rule_num_numeric, or None if none exist."""
        return min(
            (r for r in target_rulebase.rules.values() if getattr(r, "rule_num_numeric", 0) != 0),
            key=lambda x: x.rule_num_numeric,
            default=None,
        )

    def _is_rule_uid_in_return_object(self, rule_uid: str, return_object: Any) -> bool:
        for rule_uids in return_object.values():
            for _rule_uid in rule_uids:
                if rule_uid == _rule_uid:
                    return True

        return False

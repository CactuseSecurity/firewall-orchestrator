import json
import traceback
from collections.abc import Callable
from datetime import datetime
from enum import Enum
from typing import Any, TypeVar

import fwo_const
from fwo_api import FwoApi
from fwo_exceptions import (
    FwoApiWriteError,
    FwoImporterError,
    FwoImporterErrorInconsistenciesError,
)
from fwo_log import ChangeLogger, FWOLogger
from model_controllers.fwconfig_import_ruleorder import update_rule_order_diffs
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from models.networkobject import NetworkObject
from models.rule import Rule, RuleNormalized
from models.rule_from import RuleFrom
from models.rule_metadatum import RuleMetadatum
from models.rule_service import RuleService
from models.rule_to import RuleTo
from models.rulebase import Rulebase, RulebaseForImport
from models.serviceobject import ServiceObject
from models.time_object import TimeObject
from services.global_state import GlobalState
from services.group_flats_mapper import GroupFlatsMapper
from services.service_provider import ServiceProvider
from services.uid2id_mapper import Uid2IdMapper


class RefType(Enum):
    SRC = "rule_from"
    DST = "rule_to"
    SVC = "rule_service"
    NWOBJ_RESOLVED = "rule_nwobj_resolved"
    SVC_RESOLVED = "rule_svc_resolved"
    USER_RESOLVED = "rule_user_resolved"
    SRC_ZONE = "rule_from_zone"
    DST_ZONE = "rule_to_zone"
    TIME = "rule_time"


# this class is used for importing rules and rule refs into the FWO API
class FwConfigImportRule:
    global_state: GlobalState
    import_details: ImportStateController
    normalized_config: FwConfigNormalized | None = None
    uid2id_mapper: Uid2IdMapper
    group_flats_mapper: GroupFlatsMapper
    prev_group_flats_mapper: GroupFlatsMapper

    def __init__(self):
        service_provider = ServiceProvider()
        self.global_state = service_provider.get_global_state()
        self.import_details = self.global_state.import_state
        # TODO: why is there a state where this is initialized with normalized_config = None? - see #3154
        self.normalized_config = self.global_state.normalized_config
        self.uid2id_mapper = service_provider.get_uid2id_mapper(self.import_details.state.import_id)
        self.group_flats_mapper = service_provider.get_group_flats_mapper(self.import_details.state.import_id)
        self.prev_group_flats_mapper = service_provider.get_prev_group_flats_mapper(self.import_details.state.import_id)

    def update_rulebase_diffs(self, prev_config: FwConfigNormalized) -> None:
        if self.normalized_config is None:
            raise FwoImporterError("cannot update rulebase diffs: normalized_config is None")

        # set rule_num_numeric values based on rule order changes and moves. needs to be done
        # before any other processing to have the correct rule numbers available for all following steps
        update_rule_order_diffs(prev_config, self.normalized_config)

        # collect rules with rulebase information for diffing, rule_uid -> (rule, rulebase_uid)
        prev_rules: dict[str, RuleNormalized] = {
            rule_uid: rule for rb in prev_config.rulebases for rule_uid, rule in rb.rules.items()
        }
        prev_rule_to_rulebase: dict[str, str] = {
            rule_uid: rb.uid for rb in prev_config.rulebases for rule_uid in rb.rules
        }
        curr_rules: dict[str, RuleNormalized] = {
            rule_uid: rule for rb in self.normalized_config.rulebases for rule_uid, rule in rb.rules.items()
        }
        curr_rule_to_rulebase: dict[str, str] = {
            rule_uid: rb.uid for rb in self.normalized_config.rulebases for rule_uid in rb.rules
        }

        (
            added_rule_uids,
            removed_rule_uids,
            changed_rule_uids,
        ) = self.get_all_rule_diffs(prev_rules, curr_rules, prev_rule_to_rulebase, curr_rule_to_rulebase)

        # collect hit information for all rules with hit data
        new_hit_information: list[dict[str, Any]] = []
        self.collect_all_hit_information(prev_config, new_hit_information)

        # update rule_metadata before adding rules
        _, _ = self.add_new_rule_metadata([curr_rules[rule_uid] for rule_uid in added_rule_uids])
        self.update_rule_metadata_last_hit(new_hit_information)

        # fetch initial rule and rulebase ids
        self.uid2id_mapper.update_rule_mapping()
        self.uid2id_mapper.update_rulebase_mapping()

        # add new rulebases
        new_rulebases = [
            rb
            for rb in self.normalized_config.rulebases
            if rb.uid not in {prev_rb.uid for prev_rb in prev_config.rulebases}
        ]
        num_added_rulebases, new_rulebase_ids = self.add_new_rulebases(new_rulebases)
        self.uid2id_mapper.add_rulebase_mappings(new_rulebase_ids)

        num_inserted_rules, inserted_rule_ids = self.add_new_rules(
            {
                rule_uid: (curr_rules[rule_uid], curr_rule_to_rulebase[rule_uid])
                for rule_uid in (added_rule_uids | changed_rule_uids) if curr_rules[rule_uid].xlate_rule_uid is None
            }
        )
        self.uid2id_mapper.add_rule_mappings(inserted_rule_ids)

        # add new NAT rules separately after all non-NAT rules have been added, to ensure that all xlate rules are already in the database
        # and can be referenced by their new numeric id in the xlate_rule field of the NAT rules
        num_inserted_nat_rules, inserted_nat_rule_ids = self.add_new_rules(
            {
                rule_uid: (curr_rules[rule_uid], curr_rule_to_rulebase[rule_uid])
                for rule_uid in (added_rule_uids | changed_rule_uids) if curr_rules[rule_uid].xlate_rule_uid is not None
            }
        )
        num_inserted_rules += num_inserted_nat_rules
        self.uid2id_mapper.add_rule_mappings(inserted_nat_rule_ids)

        refs_added = self.add_new_refs(prev_config)

        num_set_removed_rules, _removed_rule_ids = self.mark_rules_removed(list(removed_rule_uids | changed_rule_uids))
        # remove old rulebases
        removed_rulebase_uids = [
            prev_rb.uid
            for prev_rb in prev_config.rulebases
            if prev_rb.uid not in {rb.uid for rb in self.normalized_config.rulebases}
        ]
        num_deleted_rulebases = self.mark_rulebases_removed(removed_rulebase_uids)
        refs_removed = self.remove_outdated_refs(prev_config)

        rule_to_gw_refs_added, rule_to_gw_refs_removed = self.update_rule_enforced_on_gateway(changed_rule_uids)

        self.write_changelog_rules(
            [curr_rules[rule_uid] for rule_uid in added_rule_uids],
            [prev_rules[rule_uid] for rule_uid in removed_rule_uids],
            [(prev_rules[rule_uid], curr_rules[rule_uid]) for rule_uid in changed_rule_uids],
        )

        num_moved_rules = self.count_moved_rules(
            {rule_uid: (prev_rules[rule_uid], prev_rule_to_rulebase[rule_uid]) for rule_uid in changed_rule_uids},
            {rule_uid: (curr_rules[rule_uid], curr_rule_to_rulebase[rule_uid]) for rule_uid in changed_rule_uids},
        )
        num_added_rules = len(added_rule_uids)
        num_removed_rules = len(removed_rule_uids)
        num_changed_rules = len(changed_rule_uids)

        self.import_details.state.stats.increment_rulebase_add_count(num_added_rulebases)
        self.import_details.state.stats.increment_rulebase_delete_count(num_deleted_rulebases)
        self.import_details.state.stats.increment_rule_add_count(num_added_rules)
        self.import_details.state.stats.increment_rule_delete_count(num_removed_rules)
        self.import_details.state.stats.increment_rule_move_count(num_moved_rules)
        self.import_details.state.stats.increment_rule_change_count(num_changed_rules)
        self.import_details.state.stats.increment_rule_ref_add_count(refs_added + rule_to_gw_refs_added)
        self.import_details.state.stats.increment_rule_ref_delete_count(refs_removed + rule_to_gw_refs_removed)

        # change counts returned from db mutations should match counts calculated from diffs, if not log a warning
        if num_inserted_rules != len(added_rule_uids) + len(changed_rule_uids):
            FWOLogger.warning(
                f"Number of inserted rules ({num_inserted_rules}) does not match number of added + changed rules ({len(added_rule_uids) + len(changed_rule_uids)} = {len(added_rule_uids)} + {len(changed_rule_uids)})"
            )
        if num_set_removed_rules != len(removed_rule_uids) + len(changed_rule_uids):
            FWOLogger.warning(
                f"Number of removed rules ({num_set_removed_rules}) does not match number of removed + changed rules ({len(removed_rule_uids) + len(changed_rule_uids)} = {len(removed_rule_uids)} + {len(changed_rule_uids)})"
            )

    def get_all_rule_diffs(
        self,
        prev_rules: dict[str, RuleNormalized],
        curr_rules: dict[str, RuleNormalized],
        prev_rule_to_rulebase: dict[str, str],
        curr_rule_to_rulebase: dict[str, str],
    ) -> tuple[set[str], set[str], set[str]]:
        """
        Get all rule differences between previous and current config.

        Args:
            prev_rules: Dictionary of rule_uid to RuleNormalized for previous config
            curr_rules: Dictionary of rule_uid to RuleNormalized for current config
            prev_rule_to_rulebase: Mapping of rule_uid to rulebase_uid for previous config
            curr_rule_to_rulebase: Mapping of rule_uid to rulebase_uid for current config

        Returns:
            added_rule_uids: Set of rule_uids for new rules
            removed_rule_uids: Set of rule_uids for removed rules
            changed_rule_uids: Set of rule_uids for rules that exist in both configs but have changes

        """
        changed_rule_uids: set[str] = set()

        if self.normalized_config is None:
            raise FwoImporterError("cannot get rule diffs: normalized_config is None")

        added_rule_uids = set(curr_rules.keys()) - set(prev_rules.keys())
        removed_rule_uids = set(prev_rules.keys()) - set(curr_rules.keys())
        for rule_uid in set(curr_rules.keys()) & set(prev_rules.keys()):
            curr_rule = curr_rules[rule_uid]
            prev_rule = prev_rules[rule_uid]
            curr_rb_uid = curr_rule_to_rulebase.get(rule_uid)
            prev_rb_uid = prev_rule_to_rulebase.get(rule_uid)
            if curr_rule != prev_rule or curr_rb_uid != prev_rb_uid:
                changed_rule_uids.add(rule_uid)

        return added_rule_uids, removed_rule_uids, changed_rule_uids

    def collect_all_hit_information(self, prev_config: FwConfigNormalized, new_hit_information: list[dict[str, Any]]):
        """
        Consolidated hit information collection for ALL rules that need hit updates.

        Args:
            prev_config: Previous configuration for comparison
            new_hit_information: List to append hit update information to

        """
        processed_rules: set[str] = set()

        def add_hit_update(new_hit_information: list[dict[str, Any]], rule: RuleNormalized):
            """Add a hit information update entry for a rule."""
            new_hit_information.append(
                {
                    "where": {
                        "rule_uid": {"_eq": rule.rule_uid},
                        "mgm_id": {"_eq": self.import_details.state.mgm_details.current_mgm_id},
                    },
                    "_set": {"rule_last_hit": rule.last_hit},
                }
            )

        # check all rulebases in current config
        if self.normalized_config is None:
            raise FwoImporterError("cannot collect hit information: normalized_config is None")

        for current_rulebase in self.normalized_config.rulebases:
            previous_rulebase = prev_config.get_rulebase_or_none(current_rulebase.uid)

            for rule_uid in current_rulebase.rules:
                current_rule = current_rulebase.rules[rule_uid]
                previous_rule = previous_rulebase.rules.get(rule_uid) if previous_rulebase else None

                if current_rule.last_hit is None:
                    continue  # No hit information to update

                if previous_rule is None or (current_rule.last_hit != previous_rule.last_hit):
                    # rulebase or rule is new or hit information changed
                    add_hit_update(new_hit_information, current_rule)
                    processed_rules.add(rule_uid)

    def update_rule_metadata_last_hit(self, new_hit_information: list[dict[str, Any]]):
        """
        Updates rule_metadata.rule_last_hit for all rules with hit information changes.
        This method executes the actual database updates for hit information.

        Args:
            new_hit_information (list[dict]): The hit information to update.

        """
        if len(new_hit_information) > 0:
            update_last_hit_mutation = FwoApi.get_graphql_code(
                [fwo_const.GRAPHQL_QUERY_PATH + "rule_metadata/updateLastHits.graphql"]
            )
            query_variables = {"hit_info": new_hit_information}

            try:
                import_result = self.import_details.api_call.call(
                    update_last_hit_mutation,
                    query_variables=query_variables,
                    analyze_payload=True,
                )
                if "errors" in import_result:
                    FWOLogger.exception(
                        f"fwo_api:importNwObject - error in addNewRuleMetadata: {import_result['errors']!s}"
                    )
                    # do not count last hit changes as changes here
            except Exception:
                raise FwoApiWriteError(f"failed to update RuleMetadata last hit info: {traceback.format_exc()!s}")

    def get_rule_refs(
        self, rule: RuleNormalized, is_prev: bool = False
    ) -> dict[RefType, list[tuple[str, str | None]] | list[str]]:
        froms: list[tuple[str, str | None]] = []
        tos: list[tuple[str, str | None]] = []
        users: list[str] = []
        nwobj_resolveds = []
        svc_resolveds = []
        user_resolveds = []
        from_zones = []
        to_zones = []
        for refs in rule.rule_src_refs.split(fwo_const.LIST_DELIMITER):
            user_ref = None
            if fwo_const.USER_DELIMITER in refs:
                src_ref, user_ref = refs.split(fwo_const.USER_DELIMITER)
                users.append(user_ref)
            else:
                src_ref = refs
            froms.append((src_ref, user_ref))
        for refs in rule.rule_dst_refs.split(fwo_const.LIST_DELIMITER):
            user_ref = None
            if fwo_const.USER_DELIMITER in refs:
                dst_ref, user_ref = refs.split(fwo_const.USER_DELIMITER)
                users.append(user_ref)
            else:
                dst_ref = refs
            tos.append((dst_ref, user_ref))
        svcs = rule.rule_svc_refs.split(fwo_const.LIST_DELIMITER)
        if is_prev:
            nwobj_resolveds = self.prev_group_flats_mapper.get_network_object_flats([ref[0] for ref in froms + tos])
            svc_resolveds = self.prev_group_flats_mapper.get_service_object_flats(svcs)
            user_resolveds = self.prev_group_flats_mapper.get_user_flats(users)
        else:
            nwobj_resolveds = self.group_flats_mapper.get_network_object_flats([ref[0] for ref in froms + tos])
            svc_resolveds = self.group_flats_mapper.get_service_object_flats(svcs)
            user_resolveds = self.group_flats_mapper.get_user_flats(users)
        from_zones = rule.rule_src_zone.split(fwo_const.LIST_DELIMITER) if rule.rule_src_zone else []
        to_zones = rule.rule_dst_zone.split(fwo_const.LIST_DELIMITER) if rule.rule_dst_zone else []
        times = rule.rule_time.split(fwo_const.LIST_DELIMITER) if rule.rule_time else []
        return {
            RefType.SRC: froms,
            RefType.DST: tos,
            RefType.SVC: svcs,
            RefType.NWOBJ_RESOLVED: nwobj_resolveds,
            RefType.SVC_RESOLVED: svc_resolveds,
            RefType.USER_RESOLVED: user_resolveds,
            RefType.SRC_ZONE: from_zones,
            RefType.DST_ZONE: to_zones,
            RefType.TIME: times,
        }

    T = TypeVar("T")

    def _lookup_object(
        self,
        uid: str,
        previous: bool,
        config_accessor: Callable[[FwConfigNormalized], dict[str, T]],
        object_type_name: str,
    ) -> T:
        """Generic object lookup from config with fallback to global config."""
        config = self.global_state.previous_config if previous else self.normalized_config
        global_config = (
            self.global_state.previous_global_config if previous else self.global_state.global_normalized_config
        )
        config_type = "previous" if previous else "current"

        if config is None:
            raise FwoImporterError(f"cannot lookup {object_type_name}: {config_type} config is None")

        obj = config_accessor(config).get(uid, None)
        if obj is None:
            # try lookup in global config
            if global_config is None:
                raise FwoImporterError(f"{object_type_name} not found in {config_type} config: {uid}")
            obj = config_accessor(global_config).get(uid, None)
            if obj is None:
                raise FwoImporterError(
                    f"{object_type_name} not found in {config_type} config and {config_type} global config: {uid}"
                )
        return obj

    def lookup_network_object(self, uid: str, previous: bool = False) -> NetworkObject:
        return self._lookup_object(uid, previous, lambda cfg: cfg.network_objects, "network object")

    def lookup_service_object(self, uid: str, previous: bool = False) -> ServiceObject:
        return self._lookup_object(uid, previous, lambda cfg: cfg.service_objects, "service object")

    def lookup_user(self, uid: str, previous: bool = False) -> dict[str, Any]:
        return self._lookup_object(uid, previous, lambda cfg: cfg.users, "user")

    def lookup_zone(self, uid: str, previous: bool = False) -> dict[str, Any]:
        return self._lookup_object(uid, previous, lambda cfg: cfg.zone_objects, "zone")

    def lookup_time(self, uid: str, previous: bool = False) -> TimeObject:
        return self._lookup_object(uid, previous, lambda cfg: cfg.time_objects, "time object")

    def is_ref_unchanged(self, ref_type: RefType, ref_uid: tuple[str, str | None] | str) -> bool:
        """
        Check if a reference object is unchanged between previous and current config.

        Returns True if the object is the same in both configs, False if it changed.
        """
        if ref_type in (RefType.SRC, RefType.DST):
            if not isinstance(ref_uid, tuple) or len(ref_uid) != 2:  # noqa: PLR2004
                raise TypeError(
                    f"ref_uid for {ref_type.name} must be a tuple of length 2, not {type(ref_uid).__name__}"
                )
            nwobj_uid, user_uid = ref_uid
            prev_nwobj = self.lookup_network_object(nwobj_uid, previous=True)
            curr_nwobj = self.lookup_network_object(nwobj_uid, previous=False)
            prev_user = self.lookup_user(user_uid, previous=True) if user_uid else None
            curr_user = self.lookup_user(user_uid, previous=False) if user_uid else None
            return (prev_nwobj, prev_user) == (curr_nwobj, curr_user)

        if not isinstance(ref_uid, str):
            raise TypeError(f"ref_uid must be str, not {type(ref_uid).__name__}")

        if ref_type == RefType.NWOBJ_RESOLVED:
            return self.lookup_network_object(ref_uid, previous=True) == self.lookup_network_object(
                ref_uid, previous=False
            )
        if ref_type in (RefType.SVC, RefType.SVC_RESOLVED):
            return self.lookup_service_object(ref_uid, previous=True) == self.lookup_service_object(
                ref_uid, previous=False
            )
        if ref_type == RefType.USER_RESOLVED:
            return self.lookup_user(ref_uid, previous=True) == self.lookup_user(ref_uid, previous=False)
        if ref_type in (RefType.SRC_ZONE, RefType.DST_ZONE):
            return self.lookup_zone(ref_uid, previous=True) == self.lookup_zone(ref_uid, previous=False)
        if ref_type == RefType.TIME:
            return self.lookup_time(ref_uid, previous=True) == self.lookup_time(ref_uid, previous=False)

        raise FwoImporterError(f"unknown ref type: {ref_type}")

    def get_ref_remove_statement(
        self, ref_type: RefType, rule_uid: str, ref_uid: tuple[str, str | None] | str
    ) -> dict[str, Any]:
        if ref_type in (RefType.SRC, RefType.DST):
            nwobj_uid, user_uid = ref_uid
            statement = {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"obj_id": {"_eq": self.uid2id_mapper.get_network_object_id(nwobj_uid, before_update=True)}},
                ]
            }
            if user_uid:
                statement["_and"].append(
                    {"user_id": {"_eq": self.uid2id_mapper.get_user_id(user_uid, before_update=True)}}
                )
            else:
                statement["_and"].append({"user_id": {"_is_null": True}})
            return statement
        if ref_type in (RefType.SVC, RefType.SVC_RESOLVED):
            return {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"svc_id": {"_eq": self.uid2id_mapper.get_service_object_id(ref_uid, before_update=True)}},  # type: ignore # ref_uid is str here   # noqa: PGH003
                ]
            }
        if ref_type == RefType.NWOBJ_RESOLVED:
            return {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"obj_id": {"_eq": self.uid2id_mapper.get_network_object_id(ref_uid, before_update=True)}},  # type: ignore # ref_uid is str here  # noqa: PGH003
                ]
            }
        if ref_type == RefType.USER_RESOLVED:
            return {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"user_id": {"_eq": self.uid2id_mapper.get_user_id(ref_uid, before_update=True)}},  # type: ignore # ref_uid is str here  # noqa: PGH003
                ]
            }
        if ref_type in (RefType.SRC_ZONE, RefType.DST_ZONE):
            return {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"zone_id": {"_eq": self.uid2id_mapper.get_zone_object_id(ref_uid, before_update=True)}},  # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict  # noqa: PGH003
                ]
            }
        if ref_type == RefType.TIME:
            return {
                "_and": [
                    {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                    {"time_obj_id": {"_eq": self.uid2id_mapper.get_time_object_id(ref_uid, before_update=True)}},  # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict  # noqa: PGH003
                ]
            }
        raise FwoImporterError(f"unknown ref type: {ref_type}")

    def get_outdated_refs_to_remove(
        self, prev_rule: RuleNormalized, rule: RuleNormalized | None, remove_all: bool
    ) -> dict[RefType, list[dict[str, Any]]]:
        """
        Get the references that need to be removed for a rule based on comparison with the previous rule.

        Args:
            prev_rule (RuleNormalized): The previous version of the rule.
            rule (RuleNormalized | None): The current version of the rule.
            remove_all (bool): If True, all references will be removed. If False, it will check for changes in references that need to be removed.

        """
        ref_uids: dict[RefType, list[tuple[str, str | None]] | list[str]] = {ref_type: [] for ref_type in RefType}

        if not remove_all and rule is not None:
            ref_uids = self.get_rule_refs(rule)
        prev_ref_uids = self.get_rule_refs(prev_rule, is_prev=True)
        refs_to_remove: dict[RefType, list[dict[str, Any]]] = {}
        for ref_type in RefType:
            refs_to_remove[ref_type] = []
            for prev_ref_uid in prev_ref_uids[ref_type]:
                if prev_ref_uid in ref_uids[ref_type] and self.is_ref_unchanged(ref_type, prev_ref_uid):
                    continue  # ref not removed or changed
                # ref removed or changed
                if prev_rule.rule_uid is None:
                    raise FwoImporterError(
                        f"previous reference UID is None: {prev_ref_uid} in rule {prev_rule.rule_uid}"
                    )
                refs_to_remove[ref_type].append(
                    self.get_ref_remove_statement(ref_type, prev_rule.rule_uid, prev_ref_uid)
                )
        return refs_to_remove

    def get_refs_to_remove(self, prev_config: FwConfigNormalized) -> dict[RefType, list[dict[str, Any]]]:
        all_refs_to_remove: dict[RefType, list[dict[str, Any]]] = {ref_type: [] for ref_type in RefType}
        if self.normalized_config is None:
            raise FwoImporterError("cannot remove outdated refs: normalized_config is None")
        for prev_rulebase in prev_config.rulebases:
            rules: dict[str, RuleNormalized] = {}
            rules = next(
                (rb.rules for rb in self.normalized_config.rulebases if rb.uid == prev_rulebase.uid),
                rules,
            )
            for prev_rule in prev_rulebase.rules.values():
                uid = prev_rule.rule_uid
                if uid is None:
                    raise FwoImporterError(f"rule UID is None: {prev_rule} in rulebase {prev_rulebase.name}")
                rule_removed_or_changed = (
                    uid not in rules or prev_rule != rules[uid]
                )  # rule removed or changed -> all refs need to be removed
                rule_refs_to_remove = self.get_outdated_refs_to_remove(
                    prev_rule, rules.get(uid, None), rule_removed_or_changed
                )
                for ref_type, ref_statements in rule_refs_to_remove.items():
                    all_refs_to_remove[ref_type].extend(ref_statements)
        return all_refs_to_remove

    def remove_outdated_refs(self, prev_config: FwConfigNormalized) -> int:
        """
        Remove all types of outdated rule references based on comparison with the previous configuration. This includes
        source, destination, service, resolved network object, resolved service, resolved user, source zone, and
        destination zone references.
        """
        all_refs_to_remove = self.get_refs_to_remove(prev_config)

        if not any(all_refs_to_remove.values()):
            return 0

        import_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "rule/updateRuleRefs.graphql"])

        query_variables: dict[str, Any] = {
            "importId": self.import_details.state.import_id,
            "ruleFroms": all_refs_to_remove[RefType.SRC],
            "ruleTos": all_refs_to_remove[RefType.DST],
            "ruleServices": all_refs_to_remove[RefType.SVC],
            "ruleNwObjResolveds": all_refs_to_remove[RefType.NWOBJ_RESOLVED],
            "ruleSvcResolveds": all_refs_to_remove[RefType.SVC_RESOLVED],
            "ruleUserResolveds": all_refs_to_remove[RefType.USER_RESOLVED],
            "ruleFromZones": all_refs_to_remove[RefType.SRC_ZONE],
            "ruleToZones": all_refs_to_remove[RefType.DST_ZONE],
            "ruleTimes": all_refs_to_remove[RefType.TIME],
        }

        try:
            import_result = self.import_details.api_call.call(
                import_mutation, query_variables=query_variables, analyze_payload=True
            )
            if "errors" in import_result:
                FWOLogger.error(f"failed to remove outdated rule references: {import_result['errors']!s}")
                raise FwoApiWriteError(f"failed to remove outdated rule references: {import_result['errors']!s}")
            return sum(
                import_result["data"][f"update_{ref_type.value}"].get("affected_rows", 0) for ref_type in RefType
            )
        except Exception:
            raise FwoApiWriteError(f"failed to remove outdated rule references: {traceback.format_exc()!s}")

    def get_ref_add_statement(
        self,
        ref_type: RefType,
        rule: RuleNormalized,
        ref_uid: tuple[str, str | None] | str,
    ) -> dict[str, Any]:
        if rule.rule_uid is None:
            raise FwoImporterError(
                f"rule UID is None: {rule} in rulebase during get_ref_add_statement"
            )  # should not happen

        import_id = self.import_details.state.import_id
        mgm_id = self.import_details.state.mgm_details.current_mgm_id

        if ref_type == RefType.SRC:
            nwobj_uid, user_uid = ref_uid
            _ = self.uid2id_mapper.get_network_object_id(nwobj_uid)  # check if nwobj exists
            return RuleFrom(
                rule_id=self.uid2id_mapper.get_rule_id(rule.rule_uid),
                obj_id=self.uid2id_mapper.get_network_object_id(nwobj_uid),
                user_id=self.uid2id_mapper.get_user_id(user_uid) if user_uid else None,
                rf_create=import_id,
                rf_last_seen=import_id,  # TODO: to be removed in the future
                negated=rule.rule_src_neg,
            ).model_dump()
        if ref_type == RefType.DST:
            nwobj_uid, user_uid = ref_uid
            return RuleTo(
                rule_id=self.uid2id_mapper.get_rule_id(rule.rule_uid),
                obj_id=self.uid2id_mapper.get_network_object_id(nwobj_uid),
                user_id=self.uid2id_mapper.get_user_id(user_uid) if user_uid else None,
                rt_create=import_id,
                rt_last_seen=import_id,  # TODO: to be removed in the future
                negated=rule.rule_dst_neg,
            ).model_dump()
        if ref_type == RefType.SVC:
            return RuleService(
                rule_id=self.uid2id_mapper.get_rule_id(rule.rule_uid),
                svc_id=self.uid2id_mapper.get_service_object_id(ref_uid),  # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict  # noqa: PGH003
                rs_create=import_id,
                rs_last_seen=import_id,  # TODO: to be removed in the future
            ).model_dump()
        if ref_type == RefType.NWOBJ_RESOLVED:
            return {
                "mgm_id": mgm_id,
                "rule_id": self.uid2id_mapper.get_rule_id(rule.rule_uid),
                "obj_id": self.uid2id_mapper.get_network_object_id(ref_uid),  # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict  # noqa: PGH003
                "created": import_id,
            }
        if ref_type == RefType.SVC_RESOLVED:
            return {
                "mgm_id": mgm_id,
                "rule_id": self.uid2id_mapper.get_rule_id(rule.rule_uid),
                "svc_id": self.uid2id_mapper.get_service_object_id(ref_uid),  # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict  # noqa: PGH003
                "created": import_id,
            }
        if ref_type == RefType.USER_RESOLVED:
            return {
                "mgm_id": mgm_id,
                "rule_id": self.uid2id_mapper.get_rule_id(rule.rule_uid),
                "user_id": self.uid2id_mapper.get_user_id(ref_uid),  # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict  # noqa: PGH003
                "created": import_id,
            }
        if ref_type in (RefType.SRC_ZONE, RefType.DST_ZONE):
            return {
                "rule_id": self.uid2id_mapper.get_rule_id(rule.rule_uid),
                "zone_id": self.uid2id_mapper.get_zone_object_id(ref_uid),  # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict  # noqa: PGH003
                "created": import_id,
            }
        if ref_type == RefType.TIME:
            return {
                "rule_id": self.uid2id_mapper.get_rule_id(rule.rule_uid),
                "time_obj_id": self.uid2id_mapper.get_time_object_id(ref_uid),  # type: ignore # ref_uid is str here TODO: Cleanup ref_uid dict  # noqa: PGH003
                "created": import_id,
            }
        return None

    def get_new_refs_to_add(
        self, rule: RuleNormalized, prev_rule: RuleNormalized | None, add_all: bool
    ) -> dict[RefType, list[dict[str, Any]]]:
        """
        Get the references that need to be added for a rule based on comparison with the previous rule.

        Args:
            rule (RuleNormalized): The current version of the rule.
            prev_rule (RuleNormalized): The previous version of the rule.
            add_all (bool): If True, all references will be added. If False, it will check for changes in references that need to be added.

        """
        prev_ref_uids: dict[RefType, list[tuple[str, str | None]] | list[str]] = {ref_type: [] for ref_type in RefType}
        if not add_all and prev_rule is not None:
            prev_ref_uids = self.get_rule_refs(prev_rule, is_prev=True)
        ref_uids = self.get_rule_refs(rule)
        refs_to_add: dict[RefType, list[dict[str, Any]]] = {}
        for ref_type in RefType:
            refs_to_add[ref_type] = []
            for ref_uid in ref_uids[ref_type]:
                if ref_uid in prev_ref_uids[ref_type] and self.is_ref_unchanged(ref_type, ref_uid):
                    continue  # ref not added or changed
                # ref added or changed
                refs_to_add[ref_type].append(self.get_ref_add_statement(ref_type, rule, ref_uid))
        return refs_to_add

    def add_new_refs(self, prev_config: FwConfigNormalized) -> int:
        """
        Add all types of new references for all rules compared to the previous config. This includes source, destination,
        service, resolved network objects, resolved services, resolved users, source zones, and destination zones

        Args:
            prev_config (FwConfigNormalized): The previous configuration for comparison.

        Returns:
            int: The total number of references added.

        """
        all_refs_to_add: dict[RefType, list[dict[str, Any]]] = {ref_type: [] for ref_type in RefType}
        if self.normalized_config is None:
            raise FwoImporterError("cannot add new refs: normalized_config is None")
        for rulebase in self.normalized_config.rulebases:
            prev_rules: dict[str, RuleNormalized] = {}
            prev_rules = next(
                (rb.rules for rb in prev_config.rulebases if rb.uid == rulebase.uid),
                prev_rules,
            )
            for rule in rulebase.rules.values():
                uid = rule.rule_uid
                if uid is None:
                    raise FwoImporterError(f"rule UID is None: {rule} in rulebase {rulebase.name}")
                rule_added_or_changed = (
                    uid not in prev_rules or rule != prev_rules[uid]
                )  # rule added or changed -> all refs need to be added
                rule_refs_to_add = self.get_new_refs_to_add(rule, prev_rules.get(uid, None), rule_added_or_changed)
                for ref_type, ref_statements in rule_refs_to_add.items():
                    all_refs_to_add[ref_type].extend(ref_statements)

        if not any(all_refs_to_add.values()):
            return 0

        import_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "rule/insertRuleRefs.graphql"])
        query_variables = {
            "ruleFroms": all_refs_to_add[RefType.SRC],
            "ruleTos": all_refs_to_add[RefType.DST],
            "ruleServices": all_refs_to_add[RefType.SVC],
            "ruleNwObjResolveds": all_refs_to_add[RefType.NWOBJ_RESOLVED],
            "ruleSvcResolveds": all_refs_to_add[RefType.SVC_RESOLVED],
            "ruleUserResolveds": all_refs_to_add[RefType.USER_RESOLVED],
            "ruleFromZones": all_refs_to_add[RefType.SRC_ZONE],
            "ruleToZones": all_refs_to_add[RefType.DST_ZONE],
            "ruleTimes": all_refs_to_add[RefType.TIME],
        }

        try:
            import_result = self.import_details.api_call.call(
                import_mutation, query_variables=query_variables, analyze_payload=True
            )
        except Exception:
            raise FwoApiWriteError(f"failed to add new rule references: {traceback.format_exc()!s}")
        if "errors" in import_result:
            raise FwoApiWriteError(f"failed to add new rule references: {import_result['errors']!s}")
        return sum(import_result["data"][f"insert_{ref_type.value}"].get("affected_rows", 0) for ref_type in RefType)

    # adds new rule_metadatum to the database
    def add_new_rule_metadata(self, new_rules: list[RuleNormalized]) -> tuple[int, list[int]]:
        """
        Adds new rule metadata entries for the given rules.

        Args:
            new_rules (list[RuleNormalized]): List of RuleNormalized objects for new rules.

        Returns:
            tuple[int, list[int]]: A tuple containing the number of changes made and a list of newly added rule metadata IDs.

        """
        changes: int = 0
        new_rule_ids: list[int] = []

        add_new_rule_metadata_mutation = """mutation upsertRuleMetadata($ruleMetadata: [rule_metadata_insert_input!]!) {
             insert_rule_metadata(objects: $ruleMetadata, on_conflict: {constraint: rule_metadata_mgm_id_rule_uid_unique, update_columns: []}) {
                affected_rows
                returning {
                    rule_metadata_id
                }
            }
        }
        """

        add_new_rule_metadata: list[dict[str, Any]] = self.prepare_new_rule_metadata(new_rules)
        query_variables = {"ruleMetadata": add_new_rule_metadata}

        FWOLogger.debug(json.dumps(query_variables), 10)  # just for debugging purposes

        try:
            import_result = self.import_details.api_call.call(
                add_new_rule_metadata_mutation,
                query_variables=query_variables,
                analyze_payload=True,
            )
        except Exception:
            raise FwoApiWriteError(f"failed to write new RulesMetadata: {traceback.format_exc()!s}")
        if "errors" in import_result:
            raise FwoApiWriteError(f"failed to write new RulesMetadata: {import_result['errors']!s}")
        # reduce change number by number of rulebases
        changes = import_result["data"]["insert_rule_metadata"]["affected_rows"]

        return changes, new_rule_ids

    def add_new_rules(self, rules: dict[str, tuple[RuleNormalized, str]]) -> tuple[int, list[dict[str, Any]]]:
        """
        Insert new and changed rules into the database and return the number of changes and the new rule IDs.

        Args:
            rules (dict[str, tuple[RuleNormalized, str]]): Dictionary mapping rule_uid to a tuple of RuleNormalized and rulebase_uid.

        Returns:
            tuple[int, list[dict]]: A tuple containing the number of changes made and a list of dictionaries,
                each with 'rule_id' and 'rule_uid' for each newly added rule.

        """
        changes: int = 0
        new_rule_ids: list[dict[str, Any]] = []

        upsert_rules = """mutation upsertRules($rules: [rule_insert_input!]!) {
                insert_rule(
                    objects: $rules,
                ) {
                    affected_rows,
                    returning {
                        rule_id,
                        rule_uid
                    }
                }
            }
        """
        new_rules: list[Rule] = [
            self.prepare_rule_for_import(rule, rulebase_uid) for _rule_uid, (rule, rulebase_uid) in rules.items()
        ]
        if len(new_rules) > 0:
            query_variables = {"rules": [rule.model_dump() for rule in new_rules]}
            try:
                import_result = self.import_details.api_call.call(
                    upsert_rules, query_variables=query_variables, analyze_payload=True
                )
            except Exception:
                FWOLogger.exception(
                    f"fwo_api:addRulesWithinRulebases - error in addRulesWithinRulebases: {traceback.format_exc()!s}"
                )
                raise FwoApiWriteError(f"failed to write new rules: {traceback.format_exc()!s}")
            if "errors" in import_result:
                FWOLogger.exception(
                    f"fwo_api:addRulesWithinRulebases - error in addRulesWithinRulebases: {import_result['errors']!s}"
                )
                raise FwoApiWriteError(f"failed to write new rules: {import_result['errors']!s}")
            changes += import_result["data"]["insert_rule"]["affected_rows"]
            new_rule_ids += import_result["data"]["insert_rule"]["returning"]
        return changes, new_rule_ids

    def add_new_rulebases(self, new_rulebases: list[Rulebase]) -> tuple[int, list[dict[str, Any]]]:
        """
        Adds new rulebases to the database without adding their rules.

        Args:
            new_rulebases (list[Rulebase]): A list of Rulebase objects to be added.

        Returns:
            tuple[int, list[dict[str, Any]]]: A tuple containing the number of changes made and a list of dictionaries,
                each with 'uid' and 'id' for each newly added rulebase.

        """
        add_rulebases_without_rules_mutation = """mutation upsertRulebaseWithoutRules($rulebases: [rulebase_insert_input!]!) {
                insert_rulebase(
                    objects: $rulebases,
                ) {
                    affected_rows,
                    returning { uid, id }
                }
            }
        """

        new_rulebases_for_import = [
            RulebaseForImport.from_rulebase(
                rb,
                self.import_details.state.mgm_details.current_mgm_id,
                self.import_details.state.import_id,
            )
            for rb in new_rulebases
        ]
        query_variables = {"rulebases": [rb.model_dump(by_alias=True) for rb in new_rulebases_for_import]}

        try:
            import_result = self.import_details.api_call.call(
                add_rulebases_without_rules_mutation,
                query_variables=query_variables,
                analyze_payload=True,
            )
        except Exception:
            FWOLogger.exception(f"fwo_api:importRules - error in addNewRulebases: {traceback.format_exc()!s}")
            raise FwoApiWriteError(f"failed to write new rulebases: {traceback.format_exc()!s}")
        if "errors" in import_result:
            FWOLogger.exception(f"fwo_api:importRules - error in addNewRulebases: {import_result['errors']!s}")
            raise FwoApiWriteError(f"failed to write new rulebases: {import_result['errors']!s}")

        return import_result["data"]["insert_rulebase"]["affected_rows"], import_result["data"]["insert_rulebase"][
            "returning"
        ]

    def prepare_new_rule_metadata(self, new_rules: list[RuleNormalized]) -> list[dict[str, Any]]:
        if self.normalized_config is None:
            raise FwoImporterError("cannot prepare new rule metadata: normalized_config is None")

        new_rule_metadata: list[dict[str, Any]] = []

        for rule in new_rules:
            if not rule.rule_uid:
                raise FwoImporterError(f"rule UID is None: {rule} in rulebase during prepare_new_rule_metadata")
            rm4import = RuleMetadatum(
                rule_uid=rule.rule_uid,
                mgm_id=self.import_details.state.mgm_details.current_mgm_id,
                rule_created=self.import_details.state.import_id,
                rule_last_hit=rule.last_hit,
            )
            new_rule_metadata.append(rm4import.model_dump())
        # TODO: add other fields
        return new_rule_metadata

    def mark_rulebases_removed(self, removed_rulebase_uids: list[str]) -> int:
        """
        Marks rulebases as removed in the database.

        Args:
            removed_rulebase_uids (list[str]): A list of rulebase UIDs to be marked as removed.

        Returns:
            int: The number of rulebases that were marked as removed.

        """
        if len(removed_rulebase_uids) == 0:
            return 0

        remove_mutation = """
            mutation markRulebasesRemoved($importId: bigint!, $ids: [Int!]!) {
                update_rulebase(where: {removed: { _is_null: true }, id: {_in: $ids}}, _set: {removed: $importId}) {
                    affected_rows
                }
            }
        """
        query_variables: dict[str, Any] = {
            "importId": self.import_details.state.import_id,
            "ids": [self.uid2id_mapper.get_rulebase_id(uid) for uid in removed_rulebase_uids],
        }

        try:
            remove_result = self.import_details.api_call.call(
                remove_mutation, query_variables=query_variables, analyze_payload=True
            )
        except Exception:
            raise FwoApiWriteError(f"failed to remove rulebases: {traceback.format_exc()!s}")
        if "errors" in remove_result:
            raise FwoApiWriteError(f"failed to remove rulebases: {remove_result['errors']!s}")

        return int(remove_result["data"]["update_rulebase"]["affected_rows"])

    def mark_rules_removed(self, rule_uids_to_remove: list[str]) -> tuple[int, list[int]]:
        """
        Marks removed and changed rules as removed in the database and returns the number of changes and the list of removed rule IDs.
        """
        changes = 0
        rule_ids_to_remove = [
            self.uid2id_mapper.get_rule_id(rule_uid, before_update=True) for rule_uid in rule_uids_to_remove
        ]

        remove_mutation = """
            mutation markRulesRemoved($importId: bigint!, $ruleIds: [bigint!]!) {
                update_rule(where: {removed: { _is_null: true }, rule_id: {_in: $ruleIds}}, _set: {removed: $importId, active:false}) {
                    affected_rows
                    returning { rule_id }
                }
            }
        """
        query_variables: dict[str, Any] = {
            "importId": self.import_details.state.import_id,
            "ruleIds": rule_ids_to_remove,
        }

        try:
            remove_result = self.import_details.api_call.call(
                remove_mutation, query_variables=query_variables, analyze_payload=True
            )
        except Exception:
            raise FwoApiWriteError(f"failed to remove rules: {traceback.format_exc()!s}")
        if "errors" in remove_result:
            raise FwoApiWriteError(f"failed to remove rules: {remove_result['errors']!s}")
        changes = int(remove_result["data"]["update_rule"]["affected_rows"])
        removed_rule_ids = [item["rule_id"] for item in remove_result["data"]["update_rule"]["returning"]]

        return changes, removed_rule_ids

    # TODO: find a better place for these kind of functions that simply return from config data
    @staticmethod
    def get_rule_to_gw_refs(
        rulebases: list[Rulebase],
        global_rulebases: list[Rulebase] | None,
        gateways: list[Gateway],
    ) -> set[tuple[str, str]]:
        """
        Get all rule_enforced_on_gateway (rule to gateway) refs based on the given rulebases and gateways.
        """
        # first, gather all rule to gateway references from install-on fields
        # need to check global rulebases for rules installed on this mgms gws as well
        rulebases_to_check = rulebases + (global_rulebases or [])
        rule_to_gw_refs = {
            (rule_uid, gw_installon)
            for rulebase in rulebases_to_check
            for rule_uid, rule in rulebase.rules.items()
            for gw_installon in (rule.rule_installon.split(fwo_const.LIST_DELIMITER) if rule.rule_installon else [])
        }
        rules_with_installon = {rule_uid for rule_uid, _ in rule_to_gw_refs}

        def lookup_rb_by_uid(rb_uid: str) -> Rulebase:
            rb = next((rb for rb in rulebases if rb.uid == rb_uid), None)
            if rb:
                return rb
            if not global_rulebases:
                raise FwoImporterErrorInconsistenciesError(
                    f"could not find rulebase with UID {rb_uid} in previous config"
                )
            rb = next((rb for rb in global_rulebases if rb.uid == rb_uid), None)
            if not rb:
                raise FwoImporterErrorInconsistenciesError(
                    f"could not find rulebase with UID {rb_uid} in previous global config"
                )
            return rb

        # second, gather all rule to gateway references from enforced policies on gateways
        rule_to_gw_refs.update(
            (rule_uid, gateway.Uid or "")
            for gateway in gateways
            for rulebase_link in gateway.RulebaseLinks
            for rule_uid in lookup_rb_by_uid(rulebase_link.to_rulebase_uid).rules
            # if rule has installon, this is the source of truth for enforced on gateway refs
            if rule_uid not in rules_with_installon
        )
        gw_uids: set[str] = {gw.Uid for gw in gateways if gw.Uid is not None}
        # filter for all gateways which are part of the current management in the database
        return {(rule_uid, gw_uid) for rule_uid, gw_uid in rule_to_gw_refs if gw_uid in gw_uids}

    def update_rule_enforced_on_gateway(self, changed_rule_uids: set[str]) -> tuple[int, int]:
        """
        Update the rule_enforced_on_gateway table based on changes in rule to gateway references.

        Args:
            changed_rule_uids (set[str]): set of UIDs of rules that changed in this import

        Returns:
            tuple[int, int]: A tuple containing the number of added references and the number of removed references

        """
        if not self.global_state.previous_config:
            raise FwoImporterError("cannot update rule enforced on gateway: previous_config is None")
        if not self.global_state.normalized_config:
            raise FwoImporterError("cannot update rule enforced on gateway: normalized_config is None")
        prev_rule_to_gw_refs = self.get_rule_to_gw_refs(
            self.global_state.previous_config.rulebases,
            self.global_state.previous_global_config.rulebases if self.global_state.previous_global_config else None,
            self.global_state.previous_config.gateways,
        )
        new_rule_to_gw_refs = self.get_rule_to_gw_refs(
            self.global_state.normalized_config.rulebases,
            self.global_state.global_normalized_config.rulebases
            if self.global_state.global_normalized_config
            else None,
            self.global_state.normalized_config.gateways,
        )
        # check for changed rule_uid -> gw uid assignments
        refs_to_add = new_rule_to_gw_refs - prev_rule_to_gw_refs
        refs_to_remove = prev_rule_to_gw_refs - new_rule_to_gw_refs
        # also add unchanged assignments where the rule was changed -> need to be removed and inserted with new rule id
        changed_rule_to_gw_refs = {
            (rule_uid, gw_uid) for rule_uid, gw_uid in new_rule_to_gw_refs if rule_uid in changed_rule_uids
        }
        refs_to_add.update(changed_rule_to_gw_refs)
        refs_to_remove.update(changed_rule_to_gw_refs)

        added_refs, removed_refs = 0, 0

        if refs_to_remove:
            remove_mutation = FwoApi.get_graphql_code(
                [fwo_const.GRAPHQL_QUERY_PATH + "rule/updateRuleEnforcedOnGateway.graphql"]
            )
            remove_variables = {
                "rulesEnforcedOnGateway": [
                    {
                        "_and": [
                            {"rule_id": {"_eq": self.uid2id_mapper.get_rule_id(rule_uid, before_update=True)}},
                            {"dev_id": {"_eq": self.import_details.state.lookup_gateway_id(gw_uid)}},
                        ]
                    }
                    for rule_uid, gw_uid in refs_to_remove
                ],
                "importId": self.import_details.state.import_id,
            }
            try:
                remove_result = self.import_details.api_call.call(
                    remove_mutation,
                    query_variables=remove_variables,
                    analyze_payload=True,
                )
            except Exception:
                FWOLogger.exception(f"failed to remove rule enforced on gateway refs: {traceback.format_exc()!s}")
                raise FwoApiWriteError(f"failed to remove rule enforced on gateway refs: {traceback.format_exc()!s}")
            if "errors" in remove_result:
                FWOLogger.exception(
                    f"fwo_api:update_rule_enforced_on_gateway - error while updating moved rules refs: {remove_result['errors']!s}"
                )
                raise FwoApiWriteError(f"failed to remove rule enforced on gateway refs: {remove_result['errors']!s}")
            removed_refs = int(remove_result["data"]["update_rule_enforced_on_gateway"]["affected_rows"])
        if refs_to_add:
            add_mutation = FwoApi.get_graphql_code(
                [fwo_const.GRAPHQL_QUERY_PATH + "rule/insertRuleEnforcedOnGateway.graphql"]
            )
            add_variables = {
                "rulesEnforcedOnGateway": [
                    {
                        "rule_id": self.uid2id_mapper.get_rule_id(rule_uid),
                        "dev_id": self.import_details.state.lookup_gateway_id(gw_uid),
                        "created": self.import_details.state.import_id,
                    }
                    for rule_uid, gw_uid in refs_to_add
                ]
            }
            try:
                add_result = self.import_details.api_call.call(
                    add_mutation, query_variables=add_variables, analyze_payload=True
                )
            except Exception:
                FWOLogger.exception(f"failed to add rule enforced on gateway refs: {traceback.format_exc()!s}")
                raise FwoApiWriteError(f"failed to add rule enforced on gateway refs: {traceback.format_exc()!s}")
            if "errors" in add_result:
                FWOLogger.exception(
                    f"fwo_api:update_rule_enforced_on_gateway - error while adding moved rules refs: {add_result['errors']!s}"
                )
                raise FwoApiWriteError(f"failed to add rule enforced on gateway refs: {add_result['errors']!s}")
            added_refs = int(add_result["data"]["insert_rule_enforced_on_gateway"]["affected_rows"])

        return added_refs, removed_refs

    def prepare_rule_for_import(self, rule: RuleNormalized, rulebase_uid: str) -> Rule:
        rulebase_id = self.uid2id_mapper.get_rulebase_id(rulebase_uid)
        xlate_rule_id = self.uid2id_mapper.get_rule_id(rule.xlate_rule_uid) if rule.xlate_rule_uid else None
        return Rule(
            mgm_id=self.import_details.state.mgm_details.current_mgm_id,
            rule_num=rule.rule_num,
            rule_disabled=rule.rule_disabled,
            rule_src_neg=rule.rule_src_neg,
            rule_src=rule.rule_src,
            rule_src_refs=rule.rule_src_refs,
            rule_dst_neg=rule.rule_dst_neg,
            rule_dst=rule.rule_dst,
            rule_dst_refs=rule.rule_dst_refs,
            rule_svc_neg=rule.rule_svc_neg,
            rule_svc=rule.rule_svc,
            rule_svc_refs=rule.rule_svc_refs,
            rule_action=rule.rule_action,
            rule_track=rule.rule_track,
            rule_time=rule.rule_time,
            rule_name=rule.rule_name,
            rule_uid=rule.rule_uid,
            rule_custom_fields=rule.rule_custom_fields,
            rule_implied=rule.rule_implied,
            rule_comment=rule.rule_comment,
            rule_from_zone=None,  # TODO: to be removed or changed to string of joined zone names
            rule_to_zone=None,  # TODO: to be removed or changed to string of joined zone names
            access_rule=True,
            nat_rule=rule.nat_rule,
            is_global=False,
            rulebase_id=rulebase_id,
            rule_create=self.import_details.state.import_id,
            rule_last_seen=self.import_details.state.import_id,
            rule_num_numeric=rule.rule_num_numeric,
            action_id=self.import_details.state.lookup_action(rule.rule_action),
            track_id=self.import_details.state.lookup_track(rule.rule_track),
            rule_head_text=rule.rule_head_text,
            rule_installon=rule.rule_installon,
            last_change_admin=None,  # TODO: get id from rule.last_change_admin
            xlate_rule=xlate_rule_id,
        )

    def write_changelog_rules(
        self,
        added_rules: list[RuleNormalized],
        removed_rules: list[RuleNormalized],
        changed_rules: list[tuple[RuleNormalized, RuleNormalized]],
    ) -> None:
        """
        Writes changelog entries for added, removed, and changed rules.

        Args:
            new_rules (list[RuleNormalized]): List of newly added rules.
            removed_rules (list[RuleNormalized]): List of removed rules.
            changed_rules (list[tuple[RuleNormalized, RuleNormalized]]): List of tuples containing old and new versions of changed rules.

        """
        added_rules_ids = [
            self.uid2id_mapper.get_rule_id(rule.rule_uid) for rule in added_rules if rule.rule_uid is not None
        ]
        removed_rules_ids = [
            self.uid2id_mapper.get_rule_id(rule.rule_uid) for rule in removed_rules if rule.rule_uid is not None
        ]
        changed_rules_ids: list[tuple[int, int]] = []  # (new_rule_id, old_rule_id)
        for old_rule, new_rule in changed_rules:
            if (
                new_rule.rule_uid is not None
                and old_rule.rule_uid is not None
                and self.is_change_security_relevant(old_rule, new_rule)
            ):
                rule_uid = new_rule.rule_uid
                changed_rules_ids.append(
                    (
                        self.uid2id_mapper.get_rule_id(rule_uid),
                        self.uid2id_mapper.get_rule_id(rule_uid, before_update=True),
                    )
                )

        changelog_rule_insert_objects = self.prepare_changelog_rules_insert_objects(
            added_rules_ids, removed_rules_ids, changed_rules_ids
        )

        update_changelog_rules = FwoApi.get_graphql_code(
            [fwo_const.GRAPHQL_QUERY_PATH + "rule/updateChanglogRules.graphql"]
        )

        query_variables = {"rule_changes": changelog_rule_insert_objects}

        if len(changelog_rule_insert_objects) > 0:
            try:
                update_changelog_rules_result = self.import_details.api_call.call(
                    update_changelog_rules,
                    query_variables=query_variables,
                    analyze_payload=True,
                )
                if "errors" in update_changelog_rules_result:
                    FWOLogger.exception(
                        f"error while adding changelog entries for objects: {update_changelog_rules_result['errors']!s}"
                    )
            except Exception:
                FWOLogger.exception(
                    f"fatal error while adding changelog entries for objects: {traceback.format_exc()!s}"
                )

    def prepare_changelog_rules_insert_objects(
        self,
        added_rules_ids: list[int],
        removed_rules_ids: list[int],
        changed_rules_ids: list[tuple[int, int]],
    ) -> list[dict[str, Any]]:
        """
        Creates two lists of insert arguments for the changelog_rules db table, one for new rules, one for deleted.
        """
        change_logger = ChangeLogger()
        changelog_rule_insert_objects: list[dict[str, Any]] = []
        import_time = datetime.now().isoformat()
        change_typ = 3
        if self.import_details.state.is_initial_import or self.import_details.state.is_clearing_import:
            change_typ = 2  # initial - to be ignored in change reports

        changelog_rule_insert_objects.extend(
            [
                change_logger.create_changelog_import_object(
                    "rule",
                    self.import_details.state,
                    "I",
                    change_typ,
                    import_time,
                    rule_id,
                )
                for rule_id in added_rules_ids
            ]
        )

        changelog_rule_insert_objects.extend(
            [
                change_logger.create_changelog_import_object(
                    "rule",
                    self.import_details.state,
                    "D",
                    change_typ,
                    import_time,
                    rule_id,
                )
                for rule_id in removed_rules_ids
            ]
        )

        changelog_rule_insert_objects.extend(
            [
                change_logger.create_changelog_import_object(
                    "rule",
                    self.import_details.state,
                    "C",
                    change_typ,
                    import_time,
                    new_rule_id,
                    old_rule_id,
                )
                for new_rule_id, old_rule_id in changed_rules_ids
            ]
        )

        return changelog_rule_insert_objects

    def is_change_security_relevant(self, old_rule: RuleNormalized, new_rule: RuleNormalized) -> bool:
        """
        Checks if a change between an old and a new version of a rule is security-relevant,
        meaning it should be included in the changelog and change reports.
        """
        exclude = {
            "last_hit",
            "rule_num",
            "rule_src_zone",
            "rule_dst_zone",
            "rule_name",
            "rule_comment",
            "rule_custom_fields",
        }
        old_dict = old_rule.model_dump(exclude=exclude)
        new_dict = new_rule.model_dump(exclude=exclude)
        return old_dict != new_dict

    def count_moved_rules(
        self,
        removed_rules: dict[str, tuple[RuleNormalized, str]],
        added_rules: dict[str, tuple[RuleNormalized, str]],
    ) -> int:
        """
        Counts the number of moved rules based on comparison of removed and added (= changed) rules.

        Args:
            removed_rules (dict[str, tuple[RuleNormalized, str]]): A dictionary mapping rule_uid -> (RuleNormalized, rulebase_uid) for removed rules.
            added_rules (dict[str, tuple[RuleNormalized, str]]): A dictionary mapping rule_uid -> (RuleNormalized, rulebase_uid) for added rules.

        Returns:
            int: The number of moved rules.

        """
        moved_rules = 0

        for rule_uid in set(removed_rules.keys()) & set(added_rules.keys()):
            old_rule, old_rb_uid = removed_rules[rule_uid]
            new_rule, new_rb_uid = added_rules[rule_uid]
            old_rule_num = old_rule.rule_num_numeric
            new_rule_num = new_rule.rule_num_numeric
            if old_rb_uid != new_rb_uid or old_rule_num != new_rule_num:
                moved_rules += 1
        return moved_rules

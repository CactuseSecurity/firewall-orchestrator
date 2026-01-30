from typing import TYPE_CHECKING, Any

import fwo_const
from fwo_exceptions import FwoImporterErrorInconsistenciesError
from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import (
    FwConfigManagerListController,
)
from models.fwconfig_normalized import FwConfigNormalized
from models.import_state import ImportState
from models.rulebase import Rulebase

if TYPE_CHECKING:
    from models.networkobject import NetworkObject
    from models.rule import RuleNormalized
    from models.rulebase_link import RulebaseLinkUidBased


class FwConfigImportCheckConsistency:
    issues: dict[str, Any]
    import_state: ImportState

    def __init__(self, import_state: ImportState):
        self.import_state = import_state
        self.issues = {}
        self.network_objects_to_remove: list[str] = []
        self.service_objects_to_remove: list[str] = []
        self.user_objects_to_remove: list[str] = []
        self.rules_to_remove: list[str] = []
        self.invalid_rulebase_links_exist: bool = False

    # pre-flight checks
    def check_fwconfig_managerlist_consistency(self, config: FwConfigManagerListController):
        """
        Check the consistency of all given normalized configs of the given manager list.
        If inconsistencies are found, an exception is raised.

        Args:
            config (FwConfigManagerListController): The configurations to check.

        Raises:
            FwoImporterErrorInconsistenciesError: If inconsistencies are found in the configurations

        """
        global_config: FwConfigNormalized | None = None
        for mgr in sorted(config.ManagerSet, key=lambda m: not getattr(m, "IsSuperManager", False)):
            if len(mgr.configs) == 0:
                continue
            if len(mgr.configs) > 1:
                raise FwoImporterErrorInconsistenciesError(
                    f"Manager {mgr.manager_uid} has more than one config, which is currently not supported."
                )
            if mgr.is_super_manager:
                global_config = mgr.configs[0]
            self.check_config_consistency(mgr.configs[0], global_config, fix_config=False)

    def check_config_consistency(
        self, config: FwConfigNormalized, global_config: FwConfigNormalized | None, fix_config: bool
    ):
        """
        Check the consistency of a given normalized config and corresponding global normalized config.
        If inconsistencies are found, an exception is raised.
        When fix_config is set to True, the given configs will be modified to fix certain inconsistencies, namely
        unresolvable references in groups (network/service/user), rules (containing network/service/user/zone refs),
        and rulebase links (containing rulebase/rule refs).

        All objects (network/service/user/rules/rulebase links) containing unresolvable references will be removed from the config.
        Any corresponding corrupt entries in the database are expected to be removed later, according to collected
            - network_objects_to_remove
            - service_objects_to_remove
            - user_objects_to_remove
            - rules_to_remove
            - invalid_rulebase_links_exist

        Args:
            config (FwConfigNormalized): The configuration to check.
            global_config (FwConfigNormalized | None): The corresponding global configuration to check.
            fix_config (bool): Whether to attempt to fix inconsistencies.

        Raises:
            FwoImporterErrorInconsistenciesError: If inconsistencies are found in the configurations

        """
        self.check_color_consistency(config, fix=True)
        self.check_network_object_consistency(config, global_config, fix_unresolvable_refs=fix_config)
        self.check_service_object_consistency(config, global_config, fix_inconsistencies=fix_config)
        self.check_user_object_consistency(config, global_config, fix_unresolvable_refs=fix_config)
        self.check_zone_object_consistency(config, global_config, fix_unresolvable_refs=fix_config)
        self.check_rulebase_consistency(config, fix_inconsistencies=fix_config)
        self.check_gateway_consistency(config)
        self.check_rulebase_link_consistency(config, global_config, fix_inconsistencies=fix_config)

        if len(self.issues) > 0:
            raise FwoImporterErrorInconsistenciesError(
                "Inconsistencies found in the configuration: " + str(self.issues)
            )

        FWOLogger.debug("Consistency check completed without issues remaining.")

    def check_network_object_consistency(
        self, config: FwConfigNormalized, global_config: FwConfigNormalized | None, fix_unresolvable_refs: bool
    ):
        # add all new obj refs from all rules
        all_used_obj_refs: list[str] = []
        for rb in config.rulebases:
            all_used_obj_refs += self._collect_all_used_objects_from_rules(rb)

        all_used_obj_refs += self._collect_all_used_objects_from_groups(config)

        all_network_object_uids = set(config.network_objects.keys())
        if global_config is not None:
            all_network_object_uids |= set(global_config.network_objects.keys())

        # now make list unique and get all refs not contained in network_objects
        unresolvable_nw_obj_refs = set(all_used_obj_refs) - all_network_object_uids

        if len(unresolvable_nw_obj_refs) > 0:
            if fix_unresolvable_refs:
                self.remove_nwobj_refs_from_config(config, unresolvable_nw_obj_refs)
            else:
                self.issues.update({"unresolvableNwObRefs": list(unresolvable_nw_obj_refs)})

        self._check_network_object_types_exist(config)
        self._check_objects_with_missing_ips(config)

    def _check_network_object_types_exist(self, config: FwConfigNormalized):
        all_used_obj_types: set[str] = set()

        for obj_id in config.network_objects:
            all_used_obj_types.add(config.network_objects[obj_id].obj_typ)
        missing_nw_obj_types = all_used_obj_types - self.import_state.network_obj_type_map.keys()

        if len(missing_nw_obj_types) > 0:
            self.issues.update({"unresolvableNwObjTypes": list(missing_nw_obj_types)})

    def _collect_all_used_objects_from_groups(
        self,
        single_config: FwConfigNormalized,
    ) -> list[str]:
        all_used_obj_refs: list[str] = []
        # add all nw obj refs from groups
        for obj_id in single_config.network_objects:
            if single_config.network_objects[obj_id].obj_typ == "group":
                obj_member_refs = single_config.network_objects[obj_id].obj_member_refs
                if obj_member_refs is not None and len(obj_member_refs) > 0:
                    all_used_obj_refs += obj_member_refs.split(fwo_const.LIST_DELIMITER)
        return all_used_obj_refs

    def _collect_all_used_objects_from_rules(self, rb: Rulebase) -> list[str]:
        all_used_obj_refs: list[str] = []
        for rule_uid in rb.rules:
            all_used_obj_refs += rb.rules[rule_uid].rule_src_refs.split(fwo_const.LIST_DELIMITER)
            all_used_obj_refs += rb.rules[rule_uid].rule_dst_refs.split(fwo_const.LIST_DELIMITER)

        return all_used_obj_refs

    def _check_objects_with_missing_ips(self, config: FwConfigNormalized):
        # check if there are any objects with obj_typ<>group and empty ip addresses (breaking constraint)
        non_group_nw_obj_with_missing_ips: list[NetworkObject] = []
        for obj_id in config.network_objects:
            if config.network_objects[obj_id].obj_typ != "group":
                ip1 = config.network_objects[obj_id].obj_ip
                ip2 = config.network_objects[obj_id].obj_ip_end
                if ip1 is None or ip2 is None:
                    non_group_nw_obj_with_missing_ips.append(config.network_objects[obj_id])
        if len(non_group_nw_obj_with_missing_ips) > 0:
            self.issues.update(
                {"non-group network object with undefined IP addresse(s)": list(non_group_nw_obj_with_missing_ips)}
            )

    def check_service_object_consistency(
        self, config: FwConfigNormalized, global_config: FwConfigNormalized | None, fix_inconsistencies: bool
    ):
        # check if all uid refs are valid
        all_used_obj_refs: set[str] = set()
        all_used_obj_refs |= self._collect_service_object_refs_from_rules(config)
        all_used_obj_refs |= self._collect_all_service_object_refs_from_groups(config)
        self._check_service_object_types_exist(config)

        # get all refs not contained in service_objects
        all_service_object_uids = set(config.service_objects.keys())
        if global_config is not None:
            all_service_object_uids |= set(global_config.service_objects.keys())

        unresolvable_obj_refs = all_used_obj_refs - all_service_object_uids

        if len(unresolvable_obj_refs) > 0:
            if fix_inconsistencies:
                self.remove_svcobj_refs_from_config(config, unresolvable_obj_refs)
            else:
                self.issues.update({"unresolvableSvcObjRefs": list(unresolvable_obj_refs)})

    def _check_service_object_types_exist(self, config: FwConfigNormalized):
        # check that all obj_typ exist
        all_used_obj_types: set[str] = set()
        for obj_id in config.service_objects:
            all_used_obj_types.add(config.service_objects[obj_id].svc_typ)
        missing_obj_types = all_used_obj_types - self.import_state.service_obj_type_map.keys()
        if len(missing_obj_types) > 0:
            self.issues.update({"unresolvableSvcObjTypes": list(missing_obj_types)})

    def _collect_all_service_object_refs_from_groups(
        self,
        single_config: FwConfigNormalized,
    ) -> set[str]:
        all_used_obj_refs: set[str] = set()
        for obj_id in single_config.service_objects:
            if (
                single_config.service_objects[obj_id].svc_typ == "group"
                and single_config.service_objects[obj_id].svc_member_refs is not None
            ):
                member_refs = single_config.service_objects[obj_id].svc_member_refs
                if member_refs is None or len(member_refs) == 0:
                    continue
                all_used_obj_refs |= set(member_refs.split(fwo_const.LIST_DELIMITER))
        return all_used_obj_refs

    def _collect_service_object_refs_from_rules(
        self,
        single_config: FwConfigNormalized,
    ) -> set[str]:
        all_used_obj_refs: set[str] = set()
        for rb in single_config.rulebases:
            for rule_uid in rb.rules:
                all_used_obj_refs |= set(rb.rules[rule_uid].rule_svc_refs.split(fwo_const.LIST_DELIMITER))
        return all_used_obj_refs

    def check_user_object_consistency(
        self, config: FwConfigNormalized, global_config: FwConfigNormalized | None, fix_unresolvable_refs: bool
    ):
        all_used_obj_refs: set[str] = set()
        # add all user refs from all rules
        all_used_obj_refs |= self._collect_users_from_rules(config)
        all_used_obj_refs |= self._collect_users_from_groups(config)
        self._check_user_types_exist(config)

        all_user_object_uids = set(config.users.keys())
        if global_config is not None:
            all_user_object_uids |= set(global_config.users.keys())

        # now make list unique and get all refs not contained in users
        unresolvable_obj_refs = set(all_used_obj_refs) - all_user_object_uids

        if len(unresolvable_obj_refs) > 0:
            if fix_unresolvable_refs:
                self.remove_userobj_refs_from_config(config, unresolvable_obj_refs)
            else:
                self.issues.update({"unresolvableUserObjRefs": list(unresolvable_obj_refs)})

    def _collect_users_from_rules(self, single_config: FwConfigNormalized) -> set[str]:
        all_used_obj_refs: set[str] = set()
        for rb in single_config.rulebases:
            for rule_uid in rb.rules:
                if fwo_const.USER_DELIMITER in rb.rules[rule_uid].rule_src_refs:
                    all_used_obj_refs |= set(
                        self._collect_users_from_refs(rb.rules[rule_uid].rule_src_refs.split(fwo_const.LIST_DELIMITER))
                    )
                    all_used_obj_refs |= set(
                        self._collect_users_from_refs(rb.rules[rule_uid].rule_dst_refs.split(fwo_const.LIST_DELIMITER))
                    )
        return all_used_obj_refs

    def _collect_users_from_refs(self, nw_refs: list[str]) -> list[str]:
        user_refs: list[str] = []
        for ref in nw_refs:
            split_result = ref.split(fwo_const.USER_DELIMITER)
            if len(split_result) == 2:  # noqa: PLR2004
                user_refs.append(split_result[0])
        return user_refs

    def _collect_users_from_groups(self, config: FwConfigNormalized) -> set[str]:
        all_used_obj_refs: set[str] = set()
        for obj_id in config.users:
            if config.users[obj_id]["user_typ"] == "group" and config.users[obj_id]["user_member_refs"] is not None:
                member_refs = config.users[obj_id]["user_member_refs"]
                if member_refs is None or len(member_refs) == 0:
                    continue
                all_used_obj_refs |= set(member_refs.split(fwo_const.LIST_DELIMITER))
        return all_used_obj_refs

    def _check_user_types_exist(self, single_config: FwConfigNormalized):
        # check that all obj_typ exist
        all_used_obj_types: set[str] = set()
        for obj_id in single_config.users:
            all_used_obj_types.add(single_config.users[obj_id]["user_typ"])  # make list unique
        missing_obj_types = all_used_obj_types - self.import_state.user_obj_type_map.keys()
        if len(missing_obj_types) > 0:
            self.issues.update({"unresolvableUserObjTypes": list(missing_obj_types)})

    def check_zone_object_consistency(
        self, config: FwConfigNormalized, global_config: FwConfigNormalized | None, fix_unresolvable_refs: bool
    ):
        all_used_obj_refs: set[str] = set()
        all_used_obj_refs |= self._collect_zone_refs_from_rules(config)

        all_zone_object_uids = set(config.zone_objects.keys())
        if global_config is not None:
            all_zone_object_uids |= set(global_config.zone_objects.keys())

        # get all refs not contained in zone_objects
        unresolvable_object_refs = all_used_obj_refs - all_zone_object_uids
        if len(unresolvable_object_refs) > 0:
            if fix_unresolvable_refs:
                self.remove_zoneobj_refs_from_config(config, unresolvable_object_refs)
            else:
                self.issues.update({"unresolvableZoneObjRefs": list(unresolvable_object_refs)})

    def _collect_zone_refs_from_rules(self, single_config: FwConfigNormalized) -> set[str]:
        all_used_zones_refs: set[str] = set()
        for rb in single_config.rulebases:
            for rule_uid in rb.rules:
                rule = rb.rules[rule_uid]
                if rule.rule_src_zone is not None:
                    all_used_zones_refs.update(rule.rule_src_zone.split(fwo_const.LIST_DELIMITER))
                if rule.rule_dst_zone is not None:
                    all_used_zones_refs.update(rule.rule_dst_zone.split(fwo_const.LIST_DELIMITER))
        return all_used_zones_refs

    # check if all color refs are valid (in the DB)
    # fix=True means that missing color refs will be replaced by the default color (black)
    def check_color_consistency(self, config: FwConfigNormalized, fix: bool):
        """
        Check that all color refs used in the config are resolvable

        Args:
            config (FwConfigNormalized): The configuration to check.
            fix (bool): Fix unresolvable color references by replacing them with the default color.

        """
        (
            all_used_nw_obj_color_ref_set,
            all_used_svc_color_ref_set,
            all_used_user_color_ref_set,
        ) = self._collect_all_used_colors(config)

        (
            unresolvable_nw_obj_colors,
            unresolvable_svc_colors,
            unresolvable_user_colors,
        ) = self._check_resolvability_of_used_colors(
            all_used_nw_obj_color_ref_set,
            all_used_svc_color_ref_set,
            all_used_user_color_ref_set,
        )

        if fix:
            self._fix_colors(
                config,
                unresolvable_nw_obj_colors,
                unresolvable_svc_colors,
                unresolvable_user_colors,
            )
        elif (
            len(unresolvable_nw_obj_colors) > 0 or len(unresolvable_svc_colors) > 0 or len(unresolvable_user_colors) > 0
        ):
            self.issues.update(
                {
                    "unresolvableColorRefs": {
                        "nwObjColors": unresolvable_nw_obj_colors,
                        "svcColors": unresolvable_svc_colors,
                        "userColors": unresolvable_user_colors,
                    }
                }
            )

    def _collect_all_used_colors(self, single_config: FwConfigNormalized):
        all_used_nw_obj_color_ref_set: set[str] = set()
        all_used_svc_color_ref_set: set[str] = set()
        all_used_user_color_ref_set: set[str] = set()

        for uid in single_config.network_objects:
            if single_config.network_objects[uid].obj_color is not None:  # type: ignore #TODO: obj_color cant be None  # noqa: PGH003
                all_used_nw_obj_color_ref_set.add(single_config.network_objects[uid].obj_color)
        for uid in single_config.service_objects:
            if single_config.service_objects[uid].svc_color is not None:  # type: ignore #TODO: svc_color cant be None  # noqa: PGH003
                all_used_svc_color_ref_set.add(single_config.service_objects[uid].svc_color)
        for uid in single_config.users:
            if single_config.users[uid]["user_color"] is not None:
                all_used_user_color_ref_set.add(single_config.users[uid]["user_color"])

        return (
            all_used_nw_obj_color_ref_set,
            all_used_svc_color_ref_set,
            all_used_user_color_ref_set,
        )

    def _check_resolvability_of_used_colors(
        self,
        all_used_nw_obj_color_ref_set: set[str],
        all_used_svc_color_ref_set: set[str],
        all_used_user_color_ref_set: set[str],
    ):
        unresolvable_nw_obj_colors: list[str] = []
        unresolvable_svc_colors: list[str] = []
        unresolvable_user_colors: list[str] = []
        # check all nwobj color refs
        for color_string in all_used_nw_obj_color_ref_set:
            color_id = self.import_state.lookup_color_id(color_string)
            if color_id is None:  # type: ignore # TODO: lookupColorId cant return None  # noqa: PGH003
                unresolvable_nw_obj_colors.append(color_string)

        # check all nwobj color refs
        for color_string in all_used_svc_color_ref_set:
            color_id = self.import_state.lookup_color_id(color_string)
            if color_id is None:  # type: ignore # TODO: lookupColorId cant return None  # noqa: PGH003
                unresolvable_svc_colors.append(color_string)

        # check all user color refs
        for color_string in all_used_user_color_ref_set:
            color_id = self.import_state.lookup_color_id(color_string)
            if color_id is None:  # type: ignore # TODO: lookupColorId cant return None  # noqa: PGH003
                unresolvable_user_colors.append(color_string)

        return (
            unresolvable_nw_obj_colors,
            unresolvable_svc_colors,
            unresolvable_user_colors,
        )

    def _fix_colors(
        self,
        config: FwConfigNormalized,
        unresolvable_nw_obj_colors: list[str],
        unresolvable_svc_colors: list[str],
        unresolvable_user_colors: list[str],
    ):
        # Replace unresolvable network object colors
        for obj in config.network_objects.values():
            if obj.obj_color in unresolvable_nw_obj_colors:
                obj.obj_color = fwo_const.DEFAULT_COLOR
        # Replace unresolvable service object colors
        for obj in config.service_objects.values():
            if obj.svc_color in unresolvable_svc_colors:
                obj.svc_color = fwo_const.DEFAULT_COLOR
        # Replace unresolvable user object colors
        for obj in config.users.values():
            if obj["user_color"] in unresolvable_user_colors:
                obj["user_color"] = fwo_const.DEFAULT_COLOR

    def _extract_rule_track_n_action_refs(
        self,
        rulebases: list[Rulebase],
    ) -> tuple[set[str], set[str]]:
        track_refs: set[str] = set()
        action_refs: set[str] = set()
        for rb in rulebases:
            track_refs.update(rule.rule_track for rule in rb.rules.values())
            action_refs.update(rule.rule_action for rule in rb.rules.values())
        return track_refs, action_refs

    def check_rulebase_consistency(self, config: FwConfigNormalized, fix_inconsistencies: bool):
        all_used_track_refs, all_used_action_refs = self._extract_rule_track_n_action_refs(config.rulebases)

        unresolvable_tracks = all_used_track_refs - self.import_state.tracks.keys()
        if unresolvable_tracks:
            self.issues.update({"unresolvableRuleTracks": list(unresolvable_tracks)})

        unresolvable_actions = all_used_action_refs - self.import_state.actions.keys()
        if unresolvable_actions:
            self.issues.update({"unresolvableRuleActions": list(unresolvable_actions)})

        seen_rule_uids: set[str] = set()
        rules_missing_uid = 0
        duplicate_rule_uids: list[str] = []
        rules_with_empty_src_or_dst: list[str] = []
        for rb in config.rulebases:
            for rule in rb.rules.values():
                if rule.rule_uid is None:
                    rules_missing_uid += 1
                    continue
                if rule.rule_uid in seen_rule_uids:
                    duplicate_rule_uids.append(rule.rule_uid)
                seen_rule_uids.add(rule.rule_uid)
                if rule.rule_src == "" or rule.rule_dst == "":
                    rules_with_empty_src_or_dst.append(rule.rule_uid)

        if fix_inconsistencies:
            self.fix_rulebase_inconsistencies(
                config,
                set(duplicate_rule_uids + rules_with_empty_src_or_dst),
            )
        else:
            if len(duplicate_rule_uids) > 0:
                self.issues.update({"duplicateRuleUids": duplicate_rule_uids})
            if len(rules_with_empty_src_or_dst) > 0:
                self.issues.update({"rulesWithEmptySrcOrDst": rules_with_empty_src_or_dst})
        # rules missing uid are serious enough to always raise an issue
        if rules_missing_uid > 0:
            self.issues.update({"rulesMissingUidCount": rules_missing_uid})

    # e.g. check routing, interfaces refs
    def check_gateway_consistency(self, config: FwConfigNormalized):
        # TODO: implement
        pass

    def check_rulebase_link_consistency(
        self, config: FwConfigNormalized, global_config: FwConfigNormalized | None, fix_inconsistencies: bool = False
    ):
        unresolvable_rulebases: set[str] = set()
        unresolvable_rules: set[str] = set()

        all_rulebase_uids, all_rule_uids = self.get_all_refs_from_rb_links(config, global_config)

        for gw in config.gateways:
            for rulebase_link in gw.RulebaseLinks:
                if rulebase_link.from_rulebase_uid and rulebase_link.from_rulebase_uid not in all_rulebase_uids:
                    unresolvable_rulebases.add(f"'{rulebase_link.from_rulebase_uid}' in gw '{gw.Uid}'")
                if rulebase_link.to_rulebase_uid not in all_rulebase_uids:
                    unresolvable_rulebases.add(f"'{rulebase_link.to_rulebase_uid}' in gw '{gw.Uid}'")
                if rulebase_link.from_rule_uid and rulebase_link.from_rule_uid not in all_rule_uids:
                    unresolvable_rules.add(f"'{rulebase_link.from_rule_uid}' in gw '{gw.Uid}'")

        if fix_inconsistencies and (len(unresolvable_rulebases) > 0 or len(unresolvable_rules) > 0):
            self.fix_rulebase_link_inconsistencies(config, all_rulebase_uids, all_rule_uids)
        else:
            if len(unresolvable_rulebases) > 0:
                self.issues.update({"unresolvableRulebaseLinksRulebases": list(unresolvable_rulebases)})
            if len(unresolvable_rules) > 0:
                self.issues.update({"unresolvableRulebaseLinksRules": list(unresolvable_rules)})

    def get_all_refs_from_rb_links(
        self, config: FwConfigNormalized, global_config: FwConfigNormalized | None
    ) -> tuple[set[str], set[str]]:
        all_rulebase_uids: set[str] = set()
        all_rule_uids: set[str] = set()
        for rb in config.rulebases:
            all_rulebase_uids.add(rb.uid)
            for rule_uid in rb.rules:
                all_rule_uids.add(rule_uid)
        if global_config is not None:
            for rb in global_config.rulebases:
                all_rulebase_uids.add(rb.uid)
                for rule_uid in rb.rules:
                    all_rule_uids.add(rule_uid)
        return all_rulebase_uids, all_rule_uids

    def _cascade_remove_nwobj_groups(self, config: FwConfigNormalized, unresolvable_nw_obj_refs: set[str]):
        """
        Iteratively remove network group objects that reference unresolvable objects.
        Groups referencing removed groups are also removed (cascade effect).
        Extends the unresolvable_nw_obj_refs set with newly found unresolvable group object UIDs.
        """
        while True:
            newly_unresolvable: set[str] = set()
            for obj_uid, nw_obj in config.network_objects.items():
                if obj_uid in unresolvable_nw_obj_refs:
                    continue
                if nw_obj.obj_member_refs is not None:
                    member_refs = set(nw_obj.obj_member_refs.split(fwo_const.LIST_DELIMITER))
                    if member_refs & unresolvable_nw_obj_refs:
                        newly_unresolvable.add(obj_uid)
            if not newly_unresolvable:
                break
            unresolvable_nw_obj_refs |= newly_unresolvable
            self.network_objects_to_remove.extend(newly_unresolvable)

        config.network_objects = {
            obj_uid: nw_obj
            for obj_uid, nw_obj in config.network_objects.items()
            if obj_uid not in unresolvable_nw_obj_refs
        }

    def remove_nwobj_refs_from_config(self, config: FwConfigNormalized, unresolvable_nw_obj_refs: set[str]):
        """
        Remove rules and network group objects containing unresolvable network object references from the given config.

        Args:
            config (FwConfigNormalized): The configuration to modify.
            unresolvable_nw_obj_refs (set[str]): Set of unresolvable network object references to remove.

        """
        self._cascade_remove_nwobj_groups(config, unresolvable_nw_obj_refs)

        # remove rules containing unresolvable nw obj refs
        for rb in config.rulebases:
            filtered_rules: dict[str, RuleNormalized] = {}
            for rule_uid, rule in rb.rules.items():
                src_refs = set(rule.rule_src_refs.split(fwo_const.LIST_DELIMITER))
                dst_refs = set(rule.rule_dst_refs.split(fwo_const.LIST_DELIMITER))
                if src_refs & unresolvable_nw_obj_refs or dst_refs & unresolvable_nw_obj_refs:
                    self.rules_to_remove.append(rule_uid)
                else:
                    filtered_rules[rule_uid] = rule
            rb.rules = filtered_rules

    def _cascade_remove_svcobj_groups(self, config: FwConfigNormalized, unresolvable_svc_obj_refs: set[str]):
        """
        Iteratively remove service group objects that reference unresolvable objects.
        Groups referencing removed groups are also removed (cascade effect).
        Extends the unresolvable_svc_obj_refs set with newly found unresolvable group object UIDs.
        """
        while True:
            newly_unresolvable: set[str] = set()
            for obj_uid, svc_obj in config.service_objects.items():
                if obj_uid in unresolvable_svc_obj_refs:
                    continue
                if svc_obj.svc_member_refs is not None:
                    member_refs = set(svc_obj.svc_member_refs.split(fwo_const.LIST_DELIMITER))
                    if member_refs & unresolvable_svc_obj_refs:
                        newly_unresolvable.add(obj_uid)
            if not newly_unresolvable:
                break
            unresolvable_svc_obj_refs |= newly_unresolvable
            self.service_objects_to_remove.extend(newly_unresolvable)

        config.service_objects = {
            obj_uid: svc_obj
            for obj_uid, svc_obj in config.service_objects.items()
            if obj_uid not in unresolvable_svc_obj_refs
        }

    def remove_svcobj_refs_from_config(self, config: FwConfigNormalized, unresolvable_svc_obj_refs: set[str]):
        """
        Remove rules and service group objects containing unresolvable service object references from the given config.

        Args:
            config (FwConfigNormalized): The configuration to modify.
            unresolvable_svc_obj_refs (set[str]): Set of unresolvable service object references to remove.

        """
        self._cascade_remove_svcobj_groups(config, unresolvable_svc_obj_refs)

        # remove rules containing unresolvable svc obj refs
        for rb in config.rulebases:
            filtered_rules: dict[str, RuleNormalized] = {}
            for rule_uid, rule in rb.rules.items():
                svc_refs = set(rule.rule_svc_refs.split(fwo_const.LIST_DELIMITER))
                if svc_refs & unresolvable_svc_obj_refs:
                    self.rules_to_remove.append(rule_uid)
                else:
                    filtered_rules[rule_uid] = rule
            rb.rules = filtered_rules

    def _cascade_remove_userobj_groups(self, config: FwConfigNormalized, unresolvable_user_obj_refs: set[str]):
        """
        Iteratively remove user group objects that reference unresolvable objects.
        Groups referencing removed groups are also removed (cascade effect).
        Extends the unresolvable_user_obj_refs set with newly found unresolvable group object UIDs.
        """
        while True:
            newly_unresolvable: set[str] = set()
            for obj_uid, user_obj in config.users.items():
                if obj_uid in unresolvable_user_obj_refs:
                    continue
                if user_obj["user_member_refs"] is not None:
                    member_refs = set(user_obj["user_member_refs"].split(fwo_const.LIST_DELIMITER))
                    if member_refs & unresolvable_user_obj_refs:
                        newly_unresolvable.add(obj_uid)
            if not newly_unresolvable:
                break
            unresolvable_user_obj_refs |= newly_unresolvable
            self.user_objects_to_remove.extend(newly_unresolvable)

        config.users = {
            obj_uid: user_obj for obj_uid, user_obj in config.users.items() if obj_uid not in unresolvable_user_obj_refs
        }

    def remove_userobj_refs_from_config(self, config: FwConfigNormalized, unresolvable_user_obj_refs: set[str]):
        """
        Remove rules and user group objects containing unresolvable user object references from the given config.

        Args:
            config (FwConfigNormalized): The configuration to modify.
            unresolvable_user_obj_refs (set[str]): Set of unresolvable user object references to remove.

        """
        self._cascade_remove_userobj_groups(config, unresolvable_user_obj_refs)

        # remove rules containing unresolvable user obj refs
        for rb in config.rulebases:
            filtered_rules: dict[str, RuleNormalized] = {}
            for rule_uid, rule in rb.rules.items():
                src_refs = rule.rule_src_refs.split(fwo_const.LIST_DELIMITER)
                dst_refs = rule.rule_dst_refs.split(fwo_const.LIST_DELIMITER)
                src_user_refs = self._collect_users_from_refs(src_refs)
                dst_user_refs = self._collect_users_from_refs(dst_refs)
                if set(src_user_refs) & unresolvable_user_obj_refs or set(dst_user_refs) & unresolvable_user_obj_refs:
                    self.rules_to_remove.append(rule_uid)
                else:
                    filtered_rules[rule_uid] = rule
            rb.rules = filtered_rules

    def remove_zoneobj_refs_from_config(self, config: FwConfigNormalized, unresolvable_zone_obj_refs: set[str]):
        """
        Remove rules containing unresolvable zone object references from the given config.

        Args:
            config (FwConfigNormalized): The configuration to modify.
            unresolvable_zone_obj_refs (set[str]): Set of unresolvable zone object references to remove.

        """
        # remove rules containing unresolvable zone obj refs
        for rb in config.rulebases:
            filtered_rules: dict[str, RuleNormalized] = {}
            for rule_uid, rule in rb.rules.items():
                src_zone_refs: set[str] = set()
                dst_zone_refs: set[str] = set()
                if rule.rule_src_zone is not None:
                    src_zone_refs = set(rule.rule_src_zone.split(fwo_const.LIST_DELIMITER))
                if rule.rule_dst_zone is not None:
                    dst_zone_refs = set(rule.rule_dst_zone.split(fwo_const.LIST_DELIMITER))
                if src_zone_refs & unresolvable_zone_obj_refs or dst_zone_refs & unresolvable_zone_obj_refs:
                    self.rules_to_remove.append(rule_uid)
                else:
                    filtered_rules[rule_uid] = rule
            rb.rules = filtered_rules

    def fix_rulebase_inconsistencies(self, config: FwConfigNormalized, rule_uids_to_remove: set[str]):
        """
        Remove rules with the given UIDs from all rulebases in the given config.

        Args:
            config (FwConfigNormalized): The configuration to modify.
            rule_uids_to_remove (set[str]): Set of rule UIDs to remove.

        """
        for rb in config.rulebases:
            filtered_rules: dict[str, RuleNormalized] = {}
            for rule_uid, rule in rb.rules.items():
                if rule_uid in rule_uids_to_remove:
                    self.rules_to_remove.append(rule_uid)
                else:
                    filtered_rules[rule_uid] = rule
            rb.rules = filtered_rules

    def fix_rulebase_link_inconsistencies(
        self, config: FwConfigNormalized, all_rulebase_uids: set[str], all_rule_uids: set[str]
    ):
        """
        Remove rulebase links containing unresolvable references from the given config.

        Args:
            config (FwConfigNormalized): The configuration to modify.
            all_rulebase_uids (set[str]): Set of all rulebase UIDs.
            all_rule_uids (set[str]): Set of all rule UIDs.

        """
        for gw in config.gateways:
            filtered_rulebase_links: list[RulebaseLinkUidBased] = []
            for rulebase_link in gw.RulebaseLinks:
                if (
                    (rulebase_link.from_rulebase_uid and rulebase_link.from_rulebase_uid not in all_rulebase_uids)
                    or (rulebase_link.from_rule_uid and rulebase_link.from_rule_uid not in all_rule_uids)
                    or (rulebase_link.to_rulebase_uid not in all_rulebase_uids)
                ):
                    self.invalid_rulebase_links_exist = True
                else:
                    filtered_rulebase_links.append(rulebase_link)
            gw.RulebaseLinks = filtered_rulebase_links

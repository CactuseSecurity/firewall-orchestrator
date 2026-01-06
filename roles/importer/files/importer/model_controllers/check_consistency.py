from typing import TYPE_CHECKING, Any

import fwo_const
from fwo_exceptions import FwoImporterErrorInconsistenciesError
from fwo_log import FWOLogger
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfigmanager_controller import FwConfigManager
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from models.rulebase import Rulebase
from models.rulebase_link import RulebaseLinkUidBased
from services.service_provider import ServiceProvider

if TYPE_CHECKING:
    from models.networkobject import NetworkObject


# this class is used for importing a config into the FWO API
class FwConfigImportCheckConsistency(FwConfigImport):
    issues: dict[str, Any]
    maps: FwConfigImportObject  # = FwConfigImportObject()
    config: FwConfigNormalized = FwConfigNormalized()

    # merges all configs in the set together to prepare for consistency checks
    def __init__(self, import_details: ImportStateController, config_list: FwConfigManagerListController):
        service_provider = ServiceProvider()
        self._global_state = service_provider.get_global_state()
        self.import_state = import_details
        self.issues = {}

        self.maps = FwConfigImportObject()  # TODO: don't use like this (separation of concerns) - see #3154
        for mgr in config_list.ManagerSet:
            for cfg in mgr.configs:
                import_worker = FwConfigImport()
                self.config.merge(cfg)
                self.maps.network_object_type_map.update(import_worker.fwconfig_import_object.network_object_type_map)
                self.maps.service_object_type_map.update(import_worker.fwconfig_import_object.service_object_type_map)
                self.maps.user_object_type_map.update(import_worker.fwconfig_import_object.user_object_type_map)

    # pre-flight checks
    def check_config_consistency(self, config: FwConfigManagerListController):
        self.check_color_consistency(config, fix=True)
        self.check_network_object_consistency(config)
        self.check_service_object_consistency(config)
        self.check_user_object_consistency(config)
        self.check_zone_object_consistency(config)
        self.check_rulebase_consistency(config)
        self.check_gateway_consistency(config)
        self.check_rulebase_link_consistency(config)

        if len(self.issues) > 0:
            raise FwoImporterErrorInconsistenciesError(
                "Inconsistencies found in the configuration: " + str(self.issues)
            )

        FWOLogger.debug("Consistency check completed without issues.")

    def check_network_object_consistency(self, config: FwConfigManagerListController):
        # check if all uid refs are valid
        global_objects: set[str] = set()
        single_config: FwConfigNormalized

        # add all new obj refs from all rules
        for mgr in sorted(config.ManagerSet, key=lambda m: not getattr(m, "IsSuperManager", False)):
            if mgr.is_super_manager:
                global_objects = config.get_all_network_object_uids(mgr.manager_uid)
            all_used_obj_refs: list[str] = []
            for single_config in mgr.configs:
                for rb in single_config.rulebases:
                    all_used_obj_refs += self._collect_all_used_objects_from_rules(rb)

                all_used_obj_refs += self._collect_all_used_objects_from_groups(single_config)

            # now make list unique and get all refs not contained in network_objects
            unresolvable_nw_obj_refs = (
                set(all_used_obj_refs) - config.get_all_network_object_uids(mgr.manager_uid) - global_objects
            )
            if len(unresolvable_nw_obj_refs) > 0:
                self.issues.update({"unresolvableNwObRefs": list(unresolvable_nw_obj_refs)})

            self._check_network_object_types_exist(mgr)
            self._check_objects_with_missing_ips(mgr)

    def _check_network_object_types_exist(self, mgr: FwConfigManager):
        all_used_obj_types: set[str] = set()

        for single_config in mgr.configs:
            for obj_id in single_config.network_objects:
                all_used_obj_types.add(single_config.network_objects[obj_id].obj_typ)
            missing_nw_obj_types = all_used_obj_types - self.maps.network_object_type_map.keys()
            if len(missing_nw_obj_types) > 0:
                self.issues.update({"unresolvableNwObjTypes": list(missing_nw_obj_types)})

    @staticmethod
    def _collect_all_used_objects_from_groups(single_config: FwConfigNormalized) -> list[str]:
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
        for rule_id in rb.rules:
            all_used_obj_refs += rb.rules[rule_id].rule_src_refs.split(fwo_const.LIST_DELIMITER)
            all_used_obj_refs += rb.rules[rule_id].rule_dst_refs.split(fwo_const.LIST_DELIMITER)

        return all_used_obj_refs

    def _check_objects_with_missing_ips(self, single_config: FwConfigManager):
        # check if there are any objects with obj_typ<>group and empty ip addresses (breaking constraint)
        non_group_nw_obj_with_missing_ips: list[NetworkObject] = []
        for conf in single_config.configs:
            for obj_id in conf.network_objects:
                if conf.network_objects[obj_id].obj_typ != "group":
                    ip1 = conf.network_objects[obj_id].obj_ip
                    ip2 = conf.network_objects[obj_id].obj_ip_end
                    if ip1 is None or ip2 is None:
                        non_group_nw_obj_with_missing_ips.append(conf.network_objects[obj_id])
        if len(non_group_nw_obj_with_missing_ips) > 0:
            self.issues.update(
                {"non-group network object with undefined IP addresse(s)": list(non_group_nw_obj_with_missing_ips)}
            )

    def check_service_object_consistency(self, config: FwConfigManagerListController):
        # check if all uid refs are valid
        global_objects: set[str] = set()

        for mgr in sorted(config.ManagerSet, key=lambda m: not getattr(m, "IsSuperManager", False)):
            if len(mgr.configs) == 0:
                continue
            if mgr.is_super_manager:
                global_objects = config.get_all_service_object_uids(mgr.manager_uid)
            all_used_obj_refs: set[str] = set()
            for single_config in mgr.configs:
                all_used_obj_refs |= self._collect_service_object_refs_from_rules(single_config)
                all_used_obj_refs |= self._collect_all_service_object_refs_from_groups(single_config)
                self._check_service_object_types_exist(single_config)
                # now make list unique
                all_used_obj_refs = set(all_used_obj_refs)
                # and get all refs not contained in serivce_objects

            unresolvable_obj_refs = (
                all_used_obj_refs - config.get_all_service_object_uids(mgr.manager_uid) - global_objects
            )
            if len(unresolvable_obj_refs) > 0:
                self.issues.update({"unresolvableSvcObRefs": list(unresolvable_obj_refs)})

    def _check_service_object_types_exist(self, single_config: FwConfigNormalized):
        # check that all obj_typ exist
        all_used_obj_types: set[str] = set()
        for obj_id in single_config.service_objects:
            all_used_obj_types.add(single_config.service_objects[obj_id].svc_typ)
        missing_obj_types = list(all_used_obj_types) - self.maps.service_object_type_map.keys()
        if len(missing_obj_types) > 0:
            self.issues.update({"unresolvableSvcObjTypes": list(missing_obj_types)})

    @staticmethod
    def _collect_all_service_object_refs_from_groups(single_config: FwConfigNormalized) -> set[str]:
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

    @staticmethod
    def _collect_service_object_refs_from_rules(single_config: FwConfigNormalized) -> set[str]:
        all_used_obj_refs: set[str] = set()
        for rb in single_config.rulebases:
            for rule_id in rb.rules:
                all_used_obj_refs |= set(rb.rules[rule_id].rule_svc_refs.split(fwo_const.LIST_DELIMITER))
        return all_used_obj_refs

    def check_user_object_consistency(self, config: FwConfigManagerListController):
        global_objects: set[str] = set()
        # add all user refs from all rules
        for mgr in sorted(config.ManagerSet, key=lambda m: not getattr(m, "IsSuperManager", False)):
            all_used_obj_refs: list[str] = []
            if mgr.is_super_manager:
                global_objects = config.get_all_user_object_uids(mgr.manager_uid)
            for single_config in mgr.configs:
                all_used_obj_refs += self._collect_users_from_rules(single_config)
                self._collect_users_from_groups(single_config, all_used_obj_refs)
                self._check_user_types_exist(single_config)

            # now make list unique and get all refs not contained in users
            unresolvable_obj_refs = (
                set(all_used_obj_refs) - config.get_all_user_object_uids(mgr.manager_uid) - global_objects
            )
            if len(unresolvable_obj_refs) > 0:
                self.issues.update({"unresolvableUserObjRefs": list(unresolvable_obj_refs)})

    def _collect_users_from_rules(self, single_config: FwConfigNormalized) -> list[str]:
        all_used_obj_refs: list[str] = []
        for rb in single_config.rulebases:
            for rule_id in rb.rules:
                if fwo_const.USER_DELIMITER in rb.rules[rule_id].rule_src_refs:
                    all_used_obj_refs += self._collect_users_from_rule(
                        rb.rules[rule_id].rule_src_refs.split(fwo_const.LIST_DELIMITER)
                    )
                    all_used_obj_refs += self._collect_users_from_rule(
                        rb.rules[rule_id].rule_dst_refs.split(fwo_const.LIST_DELIMITER)
                    )
        return all_used_obj_refs

    def _collect_users_from_groups(self, _single_config: FwConfigNormalized, _all_used_obj_refs: list[str]):
        return

    def _check_user_types_exist(self, single_config: FwConfigNormalized):
        # check that all obj_typ exist
        all_used_obj_types: set[str] = set()
        for obj_id in single_config.users:
            all_used_obj_types.add(single_config.users[obj_id].user_typ)  # make list unique
        missing_obj_types = (
            list(set(all_used_obj_types)) - self.maps.user_object_type_map.keys()
        )  # TODO: why list(set())?
        if len(missing_obj_types) > 0:
            self.issues.update({"unresolvableUserObjTypes": list(missing_obj_types)})

    @staticmethod
    def _collect_users_from_rule(list_of_elements: list[str]) -> list[str]:
        user_refs: list[str] = []
        for el in list_of_elements:
            split_result = el.split(fwo_const.USER_DELIMITER)
            if len(split_result) == 2:  # noqa: PLR2004
                user_refs.append(split_result[0])
        return user_refs

    def check_zone_object_consistency(self, config: FwConfigManagerListController):
        global_objects: set[str] = set()
        for mgr in sorted(config.ManagerSet, key=lambda m: not getattr(m, "IsSuperManager", False)):
            if len(mgr.configs) == 0:
                continue
            if mgr.is_super_manager:
                global_objects = config.get_all_zone_names(mgr.manager_uid)

            all_used_obj_refs: set[str] = set()
            for single_config in mgr.configs:
                all_used_obj_refs |= self._collect_zone_refs_from_rules(single_config)
                # now make list unique
                all_used_obj_refs = set(all_used_obj_refs)

            # and get all refs not contained in zone_objects
            unresolvable_object_refs = all_used_obj_refs - config.get_all_zone_names(mgr.manager_uid) - global_objects
            if len(unresolvable_object_refs) > 0:
                self.issues.update({"unresolvableZoneObRefs": list(unresolvable_object_refs)})

    @staticmethod
    def _collect_zone_refs_from_rules(single_config: FwConfigNormalized) -> set[str]:
        all_used_zones_refs: set[str] = set()
        for rb in single_config.rulebases:
            for rule_id in rb.rules:
                rule = rb.rules[rule_id]
                if rule.rule_src_zone is not None:
                    all_used_zones_refs.update(rule.rule_src_zone.split(fwo_const.LIST_DELIMITER))
                if rule.rule_dst_zone is not None:
                    all_used_zones_refs.update(rule.rule_dst_zone.split(fwo_const.LIST_DELIMITER))
        return all_used_zones_refs

    # check if all color refs are valid (in the DB)
    # fix=True means that missing color refs will be replaced by the default color (black)
    def check_color_consistency(self, config: FwConfigManagerListController, fix: bool = True):
        self.import_state.set_color_ref_map()

        # collect all colors

        for mgr in config.ManagerSet:
            for single_config in mgr.configs:
                all_used_nw_obj_color_ref_set, all_used_svc_color_ref_set, all_used_user_color_ref_set = (
                    self._collect_all_used_colors(single_config)
                )

                unresolvable_nw_obj_colors, unresolvable_svc_colors, unresolvable_user_colors = (
                    self._check_resolvability_of_used_colors(
                        all_used_nw_obj_color_ref_set, all_used_svc_color_ref_set, all_used_user_color_ref_set
                    )
                )

                if fix:
                    self._fix_colors(
                        single_config, unresolvable_nw_obj_colors, unresolvable_svc_colors, unresolvable_user_colors
                    )
                elif (
                    len(unresolvable_nw_obj_colors) > 0
                    or len(unresolvable_svc_colors) > 0
                    or len(unresolvable_user_colors) > 0
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

    @staticmethod
    def _collect_all_used_colors(single_config: FwConfigNormalized):
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
            if single_config.users[uid].user_color is not None:
                all_used_user_color_ref_set.add(single_config.users[uid].user_color)

        return all_used_nw_obj_color_ref_set, all_used_svc_color_ref_set, all_used_user_color_ref_set

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
            color_id = self.import_state.state.lookup_color_id(color_string)
            if color_id is None:  # type: ignore # TODO: lookupColorId cant return None  # noqa: PGH003
                unresolvable_nw_obj_colors.append(color_string)

        # check all nwobj color refs
        for color_string in all_used_svc_color_ref_set:
            color_id = self.import_state.state.lookup_color_id(color_string)
            if color_id is None:  # type: ignore # TODO: lookupColorId cant return None  # noqa: PGH003
                unresolvable_svc_colors.append(color_string)

        # check all user color refs
        for color_string in all_used_user_color_ref_set:
            color_id = self.import_state.state.lookup_color_id(color_string)
            if color_id is None:  # type: ignore # TODO: lookupColorId cant return None  # noqa: PGH003
                unresolvable_user_colors.append(color_string)

        return unresolvable_nw_obj_colors, unresolvable_svc_colors, unresolvable_user_colors

    @staticmethod
    def _fix_colors(
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
            if obj.user_color in unresolvable_user_colors:
                obj.user_color = fwo_const.DEFAULT_COLOR

    @staticmethod
    def _extract_rule_track_n_action_refs(rulebases: list[Rulebase]) -> tuple[list[str], list[str]]:
        track_refs: list[str] = []
        action_refs: list[str] = []
        for rb in rulebases:
            track_refs.extend(rule.rule_track for rule in rb.rules.values())
            action_refs.extend(rule.rule_action for rule in rb.rules.values())
        return track_refs, action_refs

    def check_rulebase_consistency(self, config: FwConfigManagerListController):
        all_used_track_refs: list[str] = []
        all_used_action_refs: list[str] = []

        for mgr in config.ManagerSet:
            for single_config in mgr.configs:
                track_refs, action_refs = self._extract_rule_track_n_action_refs(single_config.rulebases)
                all_used_track_refs.extend(track_refs)
                all_used_action_refs.extend(action_refs)

                all_used_track_refs = list(set(all_used_track_refs))
                all_used_action_refs = list(set(all_used_action_refs))

                unresolvable_tracks = all_used_track_refs - self.import_state.state.tracks.keys()
                if unresolvable_tracks:
                    self.issues.update({"unresolvableRuleTracks": list(unresolvable_tracks)})

                unresolvable_actions = all_used_action_refs - self.import_state.state.actions.keys()
                if unresolvable_actions:
                    self.issues.update({"unresolvableRuleActions": list(unresolvable_actions)})

    # e.g. check routing, interfaces refs
    def check_gateway_consistency(self, config: FwConfigManagerListController):
        # TODO: implement
        pass

    # e.g. check rule to rule refs
    # TODO: check if the rule & rulebases referenced belong to either
    #       - the same submanager or
    #       - the super manager but not another sub manager
    def check_rulebase_link_consistency(self, config: FwConfigManagerListController):
        broken_rulebase_links: list[dict[str, Any]] = []

        all_rulebase_uids, all_rule_uids = self._collect_uids(config)

        # check consistency of links
        for mgr in config.ManagerSet:
            if self.import_state.state.mgm_details.import_disabled:
                continue
            for single_config in mgr.configs:
                # now check rblinks for all gateways
                for gw in single_config.gateways:
                    self._check_rulebase_links_for_gateway(gw, broken_rulebase_links, all_rule_uids, all_rulebase_uids)

        if len(broken_rulebase_links) > 0:
            self.issues.update({"brokenRulebaseLinks": broken_rulebase_links})

    def _check_rulebase_links_for_gateway(
        self,
        gw: Gateway,
        broken_rulebase_links: list[dict[str, Any]],
        all_rule_uids: set[str],
        all_rulebase_uids: set[str],
    ):
        if not gw.ImportDisabled:
            for rbl in gw.RulebaseLinks:
                self._check_rulebase_link(gw, rbl, broken_rulebase_links, all_rule_uids, all_rulebase_uids)

    def _collect_uids(self, config: FwConfigManagerListController):
        all_rulebase_uids: set[str] = set()
        all_rule_uids: set[str] = set()
        for mgr in config.ManagerSet:
            if self.import_state.state.mgm_details.import_disabled:
                continue
            for single_config in mgr.configs:
                # collect rulebase UIDs
                for rb in single_config.rulebases:
                    all_rulebase_uids.add(rb.uid)
                    # collect rule UIDs
                    for rule_uid in rb.rules:
                        all_rule_uids.add(rule_uid)
        return all_rulebase_uids, all_rule_uids

    def _check_rulebase_link(
        self,
        gw: Gateway,
        rbl: RulebaseLinkUidBased,
        broken_rulebase_links: list[dict[str, Any]],
        all_rule_uids: set[str],
        all_rulebase_uids: set[str],
    ):
        if (
            rbl.from_rulebase_uid is not None
            and rbl.from_rulebase_uid != ""
            and rbl.from_rulebase_uid not in all_rulebase_uids
        ):
            self._add_issue(broken_rulebase_links, rbl, gw, "from_rulebase_uid broken")
        if rbl.to_rulebase_uid != "" and rbl.to_rulebase_uid not in all_rulebase_uids:
            self._add_issue(broken_rulebase_links, rbl, gw, "to_rulebase_uid broken")
        if rbl.from_rule_uid is not None and rbl.from_rule_uid != "" and rbl.from_rule_uid not in all_rule_uids:
            self._add_issue(broken_rulebase_links, rbl, gw, "from_rule_uid broken")

    @staticmethod
    def _add_issue(broken_rulebase_links: list[dict[str, Any]], rbl: RulebaseLinkUidBased, gw: Gateway, error_txt: str):
        rbl_dict = rbl.to_dict()
        rbl_dict.update({"error": error_txt})
        rbl_dict.update({"gw": f"{gw.Name} ({gw.Uid})"})
        if rbl_dict not in broken_rulebase_links:
            broken_rulebase_links.append(rbl_dict)

"""Test helper to assemble and tweak FwConfigNormalized instances."""

from __future__ import annotations

import random
from typing import Iterable, TypeVar

from fwo_base import ConfigAction
from fwo_const import DUMMY_IP, LIST_DELIMITER, RULE_NUM_NUMERIC_STEPS
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from models.import_state import ImportState
from models.networkobject import NetworkObject
from models.rule import RuleAction, RuleNormalized, RuleTrack, RuleType
from models.rulebase import Rulebase
from models.rulebase_link import RulebaseLink, RulebaseLinkUidBased
from models.serviceobject import ServiceObject
from netaddr import IPNetwork

from .uid_manager import UidManager

T = TypeVar("T")


class FwConfigBuilder:
    """Utility for creating and modifying normalized configs in unit tests."""

    def __init__(self, seed: int = 42):
        self._seed = seed
        self.reset()

    def reset(self) -> None:
        self.uid_manager = UidManager()
        self._rng = random.Random(self._seed)

    @staticmethod
    def empty_config() -> FwConfigNormalized:
        return FwConfigNormalized(
            action=ConfigAction.INSERT,
            gateways=[],
            network_objects={},
            service_objects={},
            users={},
            zone_objects={},
            rulebases=[],
        )

    def build_config(
        self,
        rulebases: int = 1,
        rules_per_rulebase: int = 1,
        network_object_count: int = 3,
        service_object_count: int = 2,
        include_gateway: bool = True,
    ) -> tuple[FwConfigNormalized, str]:
        config = self.empty_config()
        mgm_uid = self.uid_manager.create_uid()

        for _ in range(network_object_count):
            self.add_network_object(config)

        for _ in range(service_object_count):
            self.add_service_object(config)

        for _ in range(rulebases):
            rb = self.add_rulebase(config, mgm_uid)
            for _ in range(rules_per_rulebase):
                rule = self.add_rule(config, rb.uid)
                self.add_references_to_rule(config, rule)

        if include_gateway:
            self.add_gateway(config)

        return config, mgm_uid

    def add_network_object(self, config: FwConfigNormalized, *, name: str | None = None) -> NetworkObject:
        uid = self.uid_manager.create_uid()
        obj = NetworkObject(
            obj_uid=uid,
            obj_name=name or f"nw-{uid}",
            obj_ip=IPNetwork(DUMMY_IP),
            obj_ip_end=IPNetwork(DUMMY_IP),
            obj_color="black",
            obj_typ="group",
            obj_member_names="",
            obj_member_refs="",
        )
        config.network_objects[uid] = obj
        return obj

    def add_service_object(self, config: FwConfigNormalized, *, name: str | None = None) -> ServiceObject:
        uid = self.uid_manager.create_uid()
        svc = ServiceObject(
            svc_uid=uid,
            svc_name=name or f"svc-{uid}",
            svc_color="black",
            svc_typ="group",
            svc_port=None,
            svc_port_end=None,
            ip_proto=None,
            svc_member_names="",
            svc_member_refs="",
        )
        config.service_objects[uid] = svc
        return svc

    def add_rulebase(
        self,
        config: FwConfigNormalized,
        mgm_uid: str,
        rulebase: Rulebase | None = None,
        *,
        name: str | None = None,
    ) -> Rulebase:
        if rulebase is None:
            uid = self.uid_manager.create_uid()
            rb = Rulebase(uid=uid, name=name or f"rb-{uid}", mgm_uid=mgm_uid)
        else:
            rb = rulebase
        config.rulebases.append(rb)
        return rb

    def add_rule(
        self,
        config: FwConfigNormalized,
        rulebase_uid: str,
        rule: RuleNormalized | None = None,
        *,
        name: str | None = None,
        rule_type: RuleType = RuleType.SECTIONHEADER,
    ) -> RuleNormalized:
        if rule is None:
            uid = self.uid_manager.create_uid()
            normalized_rule = RuleNormalized(
                rule_num=0,
                rule_num_numeric=0.0,
                rule_disabled=False,
                rule_src_neg=False,
                rule_src="",
                rule_src_refs="",
                rule_dst_neg=False,
                rule_dst="",
                rule_dst_refs="",
                rule_svc_neg=False,
                rule_svc="",
                rule_svc_refs="",
                rule_action=RuleAction.ACCEPT,
                rule_track=RuleTrack.NONE,
                rule_installon=None,
                rule_time=None,
                rule_name=name or f"rule-{uid}",
                rule_uid=uid,
                rule_custom_fields=None,
                rule_implied=False,
                rule_type=rule_type,
                last_change_admin=None,
                parent_rule_uid=None,
                last_hit=None,
                rule_comment=None,
                rule_src_zone=None,
                rule_dst_zone=None,
                rule_head_text=None,
            )
        else:
            uid = rule.rule_uid or self.uid_manager.create_uid()
            normalized_rule = rule

        rulebase = self._get_rulebase(config, rulebase_uid)
        rulebase.rules[uid] = normalized_rule
        return normalized_rule

    def add_references_to_rule(
        self,
        config: FwConfigNormalized,
        rule: RuleNormalized,
        *,
        num_src: int = 1,
        num_dst: int = 1,
        num_svc: int = 1,
    ) -> None:
        src_objs = self._pick(config.network_objects.values(), num_src)
        dst_objs = self._pick(config.network_objects.values(), num_dst)
        svc_objs = self._pick(config.service_objects.values(), num_svc)

        rule.rule_src = LIST_DELIMITER.join(obj.obj_name for obj in src_objs)
        rule.rule_src_refs = LIST_DELIMITER.join(obj.obj_uid for obj in src_objs)
        rule.rule_dst = LIST_DELIMITER.join(obj.obj_name for obj in dst_objs)
        rule.rule_dst_refs = LIST_DELIMITER.join(obj.obj_uid for obj in dst_objs)
        rule.rule_svc = LIST_DELIMITER.join(svc.svc_name for svc in svc_objs)
        rule.rule_svc_refs = LIST_DELIMITER.join(svc.svc_uid for svc in svc_objs)

    def add_gateway(self, config: FwConfigNormalized, *, name: str | None = None) -> Gateway:
        uid = self.uid_manager.create_uid()
        gw = Gateway(Uid=uid, Name=name or f"gw-{uid}", RulebaseLinks=self.create_rulebase_links(config))
        config.gateways.append(gw)
        return gw

    def add_cp_section_header(
        self, gateway: Gateway, from_rulebase_uid: str, to_rulebase_uid: str, from_rule_uid: str
    ) -> None:
        gateway.RulebaseLinks.append(
            RulebaseLinkUidBased(
                from_rulebase_uid=from_rulebase_uid,
                from_rule_uid=from_rule_uid,
                to_rulebase_uid=to_rulebase_uid,
                link_type="ordered",
                is_initial=False,
                is_global=False,
                is_section=True,
            )
        )

    def add_inline_layer(
        self,
        gateway: Gateway,
        from_rulebase_uid: str,
        from_rule_uid: str,
        to_rulebase_uid: str,
        *,
        index: int = 0,
    ) -> None:
        if index == 0:
            index = len(gateway.RulebaseLinks)
        gateway.RulebaseLinks.insert(
            index,
            RulebaseLinkUidBased(
                from_rulebase_uid=from_rulebase_uid,
                from_rule_uid=from_rule_uid,
                to_rulebase_uid=to_rulebase_uid,
                link_type="inline",
                is_initial=False,
                is_global=False,
                is_section=False,
            ),
        )

    def create_rulebase_links(self, config: FwConfigNormalized) -> list[RulebaseLinkUidBased]:
        if not config.rulebases:
            return []

        links: list[RulebaseLinkUidBased] = [
            RulebaseLinkUidBased(
                from_rulebase_uid=None,
                from_rule_uid=None,
                to_rulebase_uid=config.rulebases[0].uid,
                link_type="ordered",
                is_initial=True,
                is_global=False,
                is_section=False,
            )
        ]

        for previous, current in zip(config.rulebases, config.rulebases[1:]):
            last_rule_uid = list(previous.rules.keys())[-1]
            links.append(
                RulebaseLinkUidBased(
                    from_rulebase_uid=previous.uid,
                    from_rule_uid=last_rule_uid,
                    to_rulebase_uid=current.uid,
                    link_type="ordered",
                    is_initial=False,
                    is_global=False,
                    is_section=False,
                )
            )
        return links

    def _get_rulebase(self, config: FwConfigNormalized, rulebase_uid: str) -> Rulebase:
        rulebase = config.get_rulebase_or_none(rulebase_uid)
        if rulebase is None:
            raise KeyError(f"Rulebase with UID {rulebase_uid} not found")
        return rulebase

    def _pick(self, items: Iterable[T], count: int) -> list[T]:
        pool = list(items)
        if not pool:
            return []
        if count >= len(pool):
            return pool
        return self._rng.sample(pool, count)

    def update_rule_map_and_rulebase_map(self, config: FwConfigNormalized, import_state: ImportState):
        import_state.rulebase_map = {}
        import_state.rule_map = {}

        rulebase_id = 1
        rule_id = 1

        for rulebase in config.rulebases:
            import_state.rulebase_map[rulebase.uid] = rulebase_id
            rulebase_id += 1
            for rule in rulebase.rules.values():
                if rule.rule_uid:
                    import_state.rule_map[rule.rule_uid] = rule_id
                    rule_id += 1

    def update_rb_links(
        self,
        rulebase_links: list[RulebaseLinkUidBased],
        gateway_id: int,
        fwconfig_import_gateway: FwConfigImportGateway,
    ):
        new_rb_links: list[RulebaseLink] = []
        link_id = 0
        global_state = fwconfig_import_gateway.get_global_state()
        rb_link_controller = fwconfig_import_gateway.get_rb_link_controller()

        for link in rulebase_links:
            link_id += 1

            link_type = 0
            match link.link_type:
                case "ordered":
                    link_type = 2
                case "inline":
                    link_type = 3
                case "concatenated":
                    link_type = 4
                case "domain":
                    link_type = 5
                case _:
                    link_type = 0

            new_rb_links.append(
                RulebaseLink(
                    id=link_id,
                    gw_id=gateway_id,
                    from_rule_id=global_state.import_state.state.lookup_rule(link.from_rule_uid or ""),
                    from_rulebase_id=global_state.import_state.state.lookup_rulebase_id(link.from_rulebase_uid)
                    if link.from_rulebase_uid
                    else None,
                    to_rulebase_id=global_state.import_state.state.lookup_rulebase_id(link.to_rulebase_uid),
                    link_type=link_type,
                    is_initial=link.is_initial,
                    is_global=link.is_global,
                    is_section=link.is_section,
                    created=0,
                )
            )

        rb_link_controller.rb_links = new_rb_links

    def update_rule_num_numerics(self, config: FwConfigNormalized):
        for rulebase in config.rulebases:
            new_num_numeric = 0
            for rule in rulebase.rules.values():
                new_num_numeric += RULE_NUM_NUMERIC_STEPS
                rule.rule_num_numeric = new_num_numeric

from __future__ import annotations

from typing import TYPE_CHECKING

import fwo_const
from fw_modules.guardicoremanagementREST import gc_const
from fw_modules.guardicoremanagementREST.gc_models import GcObjectRef
from fwo_log import FWOLogger
from models.networkobject import NetworkObject
from netaddr import AddrFormatError, IPAddress, IPNetwork, IPRange, IPSet

if TYPE_CHECKING:
    from fw_modules.guardicoremanagementREST.gc_models import (
        GcAndLabels,
        GcAsset,
        GcLabel,
        GcLabelGroup,
        GcLabelRef,
        GcOrLabels,
        GcRuleSide,
        GuardicoreConfig,
    )

IPV4_VERSION = 4
IPV4_HOST_PREFIX = 32
IPV6_HOST_PREFIX = 128


def parse_ip_set(argument: object) -> IPSet:
    """
    Parse an IP address, a CIDR or a range "start-end" into an IP set; return an empty set for anything else.
    """
    text = str(argument).strip() if argument is not None else ""
    try:
        if "-" in text:
            start, end = (part.strip() for part in text.split("-", 1))
            return IPSet(IPRange(start, end))
        return IPSet([IPNetwork(text)])
    except (AddrFormatError, ValueError, TypeError):
        FWOLogger.warning(f"ignoring unparsable Guardicore IP value '{text}'")
        return IPSet()


def get_label_name(label_ref: GcLabelRef, labels_by_id: dict[str, GcLabel]) -> str:
    label = labels_by_id.get(label_ref.id)
    key = label.key if label is not None else label_ref.key
    value = label.value if label is not None else label_ref.value
    if key is None or value is None:
        return label_ref.id
    return f"{key}{gc_const.GC_LABEL_NAME_SEPARATOR}{value}"


def build_cidr_object(cidr: IPNetwork) -> NetworkObject:
    """Build a host or network object spanning the CIDR, with start and end as host addresses."""
    host_prefix = IPV4_HOST_PREFIX if cidr.version == IPV4_VERSION else IPV6_HOST_PREFIX
    is_host = cidr.prefixlen == host_prefix
    first_ip = IPAddress(cidr.first, cidr.version)
    last_ip = IPAddress(cidr.last, cidr.version)
    return NetworkObject(
        obj_uid=str(cidr.cidr),
        obj_name=str(first_ip) if is_host else str(cidr.cidr),
        obj_ip=IPNetwork(f"{first_ip}/{host_prefix}"),
        obj_ip_end=IPNetwork(f"{last_ip}/{host_prefix}"),
        obj_color=fwo_const.DEFAULT_COLOR,
        obj_typ="host" if is_host else "network",
    )


class GcNetworkObjectCollector:
    """
    Turns Guardicore labels and rule sides into FWO network objects.

    Every label, label group, asset and address classification resolves to a set of IP addresses, which becomes a
    group whose members are one object per CIDR. Combinations of labels that Guardicore ANDs within a rule side
    become groups of the intersection of the label IP sets.
    """

    def __init__(self, native_config: GuardicoreConfig) -> None:
        self.network_objects: dict[str, NetworkObject] = {}
        self.labels_by_id: dict[str, GcLabel] = {label.id: label for label in native_config.labels}
        self.label_groups_by_id: dict[str, GcLabelGroup] = {group.id: group for group in native_config.label_groups}
        self.assets_by_id: dict[str, GcAsset] = {asset.id: asset for asset in native_config.assets}
        self.label_ip_sets: dict[str, IPSet] = {}
        self._collect_label_ip_sets(native_config)

    def _collect_label_ip_sets(self, native_config: GuardicoreConfig) -> None:
        for label in native_config.labels:
            ip_set = IPSet()
            for criterion in label.dynamic_criteria:
                if criterion.field != gc_const.GC_IP_CRITERIA_FIELD:
                    continue
                if criterion.op in gc_const.GC_IP_CRITERIA_OPS or criterion.op == gc_const.GC_IP_RANGE_CRITERIA_OP:
                    ip_set |= parse_ip_set(criterion.argument)
            self.label_ip_sets[label.id] = ip_set
        # assets list the labels they carry, no matter whether assigned statically, dynamically or implicitly
        for asset in native_config.assets:
            asset_ip_set = self.get_asset_ip_set(asset)
            for label_ref in asset.labels:
                self.label_ip_sets[label_ref.id] = self.label_ip_sets.get(label_ref.id, IPSet()) | asset_ip_set

    @staticmethod
    def get_asset_ip_set(asset: GcAsset) -> IPSet:
        ip_set = IPSet()
        for ip in asset.get_ip_addresses():
            ip_set |= parse_ip_set(ip)
        return ip_set

    def add_group(self, uid: str, name: str, ip_set: IPSet, comment: str | None = None) -> str:
        """Add a group with one member object per CIDR of the IP set and return its uid."""
        if uid in self.network_objects:
            return uid
        member_uids: list[str] = []
        member_names: list[str] = []
        for cidr in ip_set.iter_cidrs():
            member = self.network_objects.setdefault(str(cidr.cidr), build_cidr_object(cidr))
            member_uids.append(member.obj_uid)
            member_names.append(member.obj_name)
        self.network_objects[uid] = NetworkObject(
            obj_uid=uid,
            obj_name=name,
            obj_color=fwo_const.DEFAULT_COLOR,
            obj_typ="group",
            obj_member_refs=fwo_const.LIST_DELIMITER.join(member_uids) if member_uids else None,
            obj_member_names=fwo_const.LIST_DELIMITER.join(member_names) if member_names else None,
            obj_comment=comment,
        )
        return uid

    def add_label_groups(self) -> None:
        """Add one group per Guardicore label, also for labels without any IP address."""
        for label in self.labels_by_id.values():
            self.add_label(label.id)

    def add_label(self, label_id: str) -> str:
        label = self.labels_by_id.get(label_id)
        name = f"{label.key}{gc_const.GC_LABEL_NAME_SEPARATOR}{label.value}" if label is not None else label_id
        if label is None:
            FWOLogger.warning(f"Guardicore label {label_id} is referenced but unknown, importing it without IPs")
        comment = label.comment if label is not None and label.comment else None
        return self.add_group(label_id, name, self.label_ip_sets.get(label_id, IPSet()), comment)

    def add_and_labels(self, and_labels: GcAndLabels) -> str | None:
        """Add the group for an AND combination of labels and return its uid (None if it is empty)."""
        label_refs = sorted(and_labels.and_labels, key=lambda ref: ref.id)
        if len(label_refs) == 0:
            return None
        if len(label_refs) == 1:
            return self.add_label(label_refs[0].id)
        ip_set = self.label_ip_sets.get(label_refs[0].id, IPSet())
        for label_ref in label_refs[1:]:
            ip_set &= self.label_ip_sets.get(label_ref.id, IPSet())
        uid = gc_const.GC_AND_LABEL_UID_SEPARATOR.join(ref.id for ref in label_refs)
        name = gc_const.GC_AND_LABEL_NAME_SEPARATOR.join(
            sorted(get_label_name(ref, self.labels_by_id) for ref in label_refs)
        )
        return self.add_group(uid, name, ip_set, "intersection of Guardicore labels")

    def get_or_labels_ip_set(self, or_labels: GcOrLabels) -> IPSet:
        ip_set = IPSet()
        for and_labels in or_labels.or_labels:
            if len(and_labels.and_labels) == 0:
                continue
            and_ip_set = self.label_ip_sets.get(and_labels.and_labels[0].id, IPSet())
            for label_ref in and_labels.and_labels[1:]:
                and_ip_set &= self.label_ip_sets.get(label_ref.id, IPSet())
            ip_set |= and_ip_set
        return ip_set

    def add_label_group(self, group_ref: GcObjectRef) -> str:
        group = self.label_groups_by_id.get(group_ref.id)
        if group is None:
            FWOLogger.warning(f"Guardicore label group {group_ref.id} is referenced but unknown, importing it empty")
            return self.add_group(group_ref.id, group_ref.name or group_ref.id, IPSet())
        ip_set = self.get_or_labels_ip_set(group.include_labels) - self.get_or_labels_ip_set(group.exclude_labels)
        name = group.name or f"{group.key}{gc_const.GC_LABEL_NAME_SEPARATOR}{group.value}"
        return self.add_group(group.id, name, ip_set, "Guardicore label group")

    def add_asset(self, asset_id: str, asset_name: str | None = None) -> str:
        asset = self.assets_by_id.get(asset_id)
        ip_set = self.get_asset_ip_set(asset) if asset is not None else IPSet()
        name = (asset.name if asset is not None else None) or asset_name or asset_id
        if asset is None:
            FWOLogger.warning(f"Guardicore asset {asset_id} is referenced but unknown, importing it without IPs")
        return self.add_group(gc_const.GC_ASSET_UID_PREFIX + asset_id, name, ip_set, "Guardicore asset")

    def add_subnet(self, subnet: str) -> str | None:
        ip_set = parse_ip_set(subnet)
        cidrs = ip_set.iter_cidrs()
        if len(cidrs) == 1:
            return self.network_objects.setdefault(str(cidrs[0].cidr), build_cidr_object(cidrs[0])).obj_uid
        if len(cidrs) == 0:
            return None
        return self.add_group(subnet, subnet, ip_set)

    def add_address_classification(self, classification: str) -> str | None:
        private_ip_set = IPSet(gc_const.GC_PRIVATE_NETWORKS)
        if classification == gc_const.GC_ADDRESS_CLASSIFICATION_PRIVATE:
            ip_set = private_ip_set
        elif classification == gc_const.GC_ADDRESS_CLASSIFICATION_INTERNET:
            ip_set = IPSet([fwo_const.ANY_IP_IPV4]) - private_ip_set
        else:
            FWOLogger.warning(f"ignoring unknown Guardicore address classification '{classification}'")
            return None
        return self.add_group(
            f"address_classification:{classification}", classification, ip_set, "Guardicore address classification"
        )

    def add_any(self) -> str:
        if gc_const.GC_ANY_OBJECT_UID not in self.network_objects:
            self.network_objects[gc_const.GC_ANY_OBJECT_UID] = NetworkObject(
                obj_uid=gc_const.GC_ANY_OBJECT_UID,
                obj_name=gc_const.GC_ANY_OBJECT_NAME,
                obj_ip=IPNetwork(fwo_const.ANY_IP_START),
                obj_ip_end=IPNetwork(fwo_const.ANY_IP_END),
                obj_color=fwo_const.DEFAULT_COLOR,
                obj_typ="network",
            )
        return gc_const.GC_ANY_OBJECT_UID

    def resolve_rule_side(self, side: GcRuleSide, rule_id: str) -> list[str]:
        """
        Return the uids of the network objects a rule side consists of (ORed); an empty side is "any".
        """
        uids: list[str | None] = []
        if side.labels is not None:
            uids.extend(self.add_and_labels(and_labels) for and_labels in side.labels.or_labels)
        uids.extend(self.add_label_group(group_ref) for group_ref in side.label_groups)
        uids.extend(self.add_label_group(GcObjectRef(id=group_id)) for group_id in side.label_group_ids)
        uids.extend(self.add_asset(asset_ref.id, asset_ref.name) for asset_ref in side.assets)
        uids.extend(self.add_asset(asset_id) for asset_id in side.asset_ids)
        uids.extend(self.add_subnet(subnet) for subnet in side.subnets)
        if side.address_classification:
            uids.append(self.add_address_classification(side.address_classification))

        unsupported = side.get_unsupported_fields()
        if unsupported:
            FWOLogger.warning(f"Guardicore rule {rule_id}: ignoring unsupported rule side fields {unsupported}")

        resolved = list(dict.fromkeys(uid for uid in uids if uid is not None))
        return resolved or [self.add_any()]

    def get_names(self, uids: list[str]) -> list[str]:
        return [self.network_objects[uid].obj_name for uid in uids]

from __future__ import annotations

from typing import Any, cast

from pydantic import BaseModel, ConfigDict, Field, field_validator


class GcLabelRef(BaseModel):
    """Reference to a label as used in rules, label groups and assets: a full object or only the label id."""

    model_config = ConfigDict(extra="ignore")

    id: str
    key: str | None = None
    value: str | None = None

    @field_validator("id", mode="before")
    @classmethod
    def convert_id(cls, value: object) -> object:
        return str(value) if isinstance(value, int) else value


def convert_ids_to_refs(items: object) -> object:
    """Accept plain ids (as in published versions) besides reference objects."""
    if isinstance(items, list):
        return [{"id": item} if isinstance(item, str) else item for item in cast("list[object]", items)]
    return items


class GcAndLabels(BaseModel):
    model_config = ConfigDict(extra="ignore")

    and_labels: list[GcLabelRef] = []

    @field_validator("and_labels", mode="before")
    @classmethod
    def convert_and_labels(cls, items: object) -> object:
        return convert_ids_to_refs(items)


class GcOrLabels(BaseModel):
    model_config = ConfigDict(extra="ignore")

    or_labels: list[GcAndLabels] = []


class GcCriterion(BaseModel):
    model_config = ConfigDict(extra="ignore")

    field: str | None = None
    op: str | None = None
    argument: Any = None


class GcLabel(BaseModel):
    model_config = ConfigDict(extra="ignore")

    id: str
    key: str
    value: str
    dynamic_criteria: list[GcCriterion] = []
    static_criteria: list[GcCriterion] = []
    implicit_criteria: list[GcCriterion] = []
    comment: str | None = None


class GcLabelGroup(BaseModel):
    model_config = ConfigDict(extra="ignore")

    id: str
    key: str | None = None
    value: str | None = None
    name: str | None = None
    include_labels: GcOrLabels = Field(default_factory=GcOrLabels)
    exclude_labels: GcOrLabels = Field(default_factory=GcOrLabels)


class GcNic(BaseModel):
    model_config = ConfigDict(extra="ignore")

    ip_addresses: list[str] = []


class GcAsset(BaseModel):
    model_config = ConfigDict(extra="ignore")

    id: str
    name: str = ""
    status: str | None = None
    nics: list[GcNic] = []
    labels: list[GcLabelRef] = []
    os_info: dict[str, Any] | None = None

    def get_ip_addresses(self) -> list[str]:
        ips: list[str] = []
        for nic in self.nics:
            ips.extend(ip for ip in nic.ip_addresses if ip not in ips)
        return ips


class GcObjectRef(BaseModel):
    """Reference to an asset or label group within a rule side."""

    model_config = ConfigDict(extra="ignore")

    id: str
    name: str | None = None


class GcRuleSide(BaseModel):
    """
    Source or destination of a rule. An empty side matches any address.

    Fields this importer cannot express as addresses (e.g. processes or domains) end up in model_extra.
    """

    model_config = ConfigDict(extra="allow")

    labels: GcOrLabels | None = None
    label_groups: list[GcObjectRef] = []
    label_group_ids: list[str] = []
    assets: list[GcObjectRef] = []
    asset_ids: list[str] = []
    subnets: list[str] = []
    address_classification: str | None = None

    @field_validator("label_groups", "assets", mode="before")
    @classmethod
    def convert_object_refs(cls, items: object) -> object:
        return convert_ids_to_refs(items)

    def get_unsupported_fields(self) -> list[str]:
        return sorted(key for key, value in (self.model_extra or {}).items() if value)


class GcPortRange(BaseModel):
    model_config = ConfigDict(extra="ignore")

    start: int
    end: int


class GcIcmpMatch(BaseModel):
    model_config = ConfigDict(extra="ignore")

    icmp_type: int | None = None
    icmp_codes: list[int] = []


class GcAuthor(BaseModel):
    model_config = ConfigDict(extra="ignore")

    username: str | None = None


class GcRule(BaseModel):
    model_config = ConfigDict(extra="ignore")

    id: str
    action: str
    section_position: str
    enabled: bool = True
    comments: str | None = None
    ruleset_name: str | None = None
    ip_protocols: list[str] = []
    ports: list[int] = []
    port_ranges: list[GcPortRange] = []
    exclude_ports: list[int] = []
    exclude_port_ranges: list[GcPortRange] = []
    icmp_matches: list[GcIcmpMatch] = []
    source: GcRuleSide = Field(default_factory=GcRuleSide)
    destination: GcRuleSide = Field(default_factory=GcRuleSide)
    last_hit: int | None = None
    author: GcAuthor | None = None


class GuardicoreConfig(BaseModel):
    """Native Guardicore configuration as fetched from the management API (and stored as native config)."""

    model_config = ConfigDict(extra="ignore")

    labels: list[GcLabel] = []
    label_groups: list[GcLabelGroup] = []
    assets: list[GcAsset] = []
    rules: list[GcRule] = []

from __future__ import annotations

from dataclasses import dataclass

from pydantic import BaseModel, ConfigDict, Field


class AzureModel(BaseModel):
    model_config = ConfigDict(populate_by_name=True, extra="ignore")


class AzureIpGroupProperties(AzureModel):
    ip_addresses: list[str] = Field(default_factory=list, alias="ipAddresses")


class AzureIpGroup(AzureModel):
    id: str
    name: str
    properties: AzureIpGroupProperties = Field(default_factory=AzureIpGroupProperties)


class AzureRule(AzureModel):
    name: str
    rule_type: str = Field(alias="ruleType")
    description: str | None = None
    ip_protocols: list[str] = Field(default_factory=list, alias="ipProtocols")
    source_addresses: list[str] = Field(default_factory=list, alias="sourceAddresses")
    source_ip_groups: list[str] = Field(default_factory=list, alias="sourceIpGroups")
    destination_addresses: list[str] = Field(default_factory=list, alias="destinationAddresses")
    destination_ip_groups: list[str] = Field(default_factory=list, alias="destinationIpGroups")
    destination_fqdns: list[str] = Field(default_factory=list, alias="destinationFqdns")
    destination_ports: list[str] = Field(default_factory=list, alias="destinationPorts")


class AzureRuleCollectionAction(AzureModel):
    type: str


class AzureRuleCollection(AzureModel):
    name: str
    rule_collection_type: str = Field(alias="ruleCollectionType")
    priority: int | None = None
    action: AzureRuleCollectionAction | None = None
    rules: list[AzureRule] = Field(default_factory=list[AzureRule])


class AzureRuleCollectionGroupProperties(AzureModel):
    priority: int | None = None
    rule_collections: list[AzureRuleCollection] = Field(
        default_factory=list[AzureRuleCollection], alias="ruleCollections"
    )


class AzureRuleCollectionGroup(AzureModel):
    id: str
    name: str
    properties: AzureRuleCollectionGroupProperties = Field(default_factory=AzureRuleCollectionGroupProperties)


class AzureFirewallPolicy(AzureModel):
    name: str
    rule_collection_groups: list[AzureRuleCollectionGroup] = Field(
        default_factory=list[AzureRuleCollectionGroup], alias="ruleCollectionGroups"
    )


class AzureConfig(AzureModel):
    ip_groups: list[AzureIpGroup] = Field(default_factory=list[AzureIpGroup], alias="ipGroups")
    firewall_policies: list[AzureFirewallPolicy] = Field(
        default_factory=list[AzureFirewallPolicy], alias="firewallPolicies"
    )


@dataclass(frozen=True)
class AzureDevice:
    """An FWO device (Azure Firewall) and the name of the firewall policy enforced on it."""

    uid: str
    name: str
    policy_name: str

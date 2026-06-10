# model of OPNsense firewall

from __future__ import annotations

from enum import Enum
from typing import Literal

from netaddr_pydantic import IPAddress, IPNetwork
from pydantic import (AliasPath, BaseModel, ConfigDict, Field, field_validator,
                      model_validator)


class UserScopeEnum(str, Enum):
    SYSTEM = "system"
    USER   = "user"

# https://github.com/opnsense/core/blob/8bc595681e13fec63ef0f6e3fcc292cfff67496c/src/opnsense/mvc/app/models/OPNsense/Firewall/Alias.xml#L27
class AliasTypeEnum(str, Enum):
    HOST         = "host"
    NETWORK      = "network"
    PORT         = "port"
    URL          = "url"
    URLTABLE     = "urltable"
    URLJSON      = "urljson"
    GEOIP        = "geoip"
    NETWORKGROUP = "networkgroup"
    MAC          = "mac"
    ASN          = "asn"
    DYNIPV6HOST  = "dynipv6host"
    AUTHGROUP    = "authgroup"
    INTERNAL     = "internal"
    EXTERNALl    = "external"

# https://github.com/opnsense/core/blob/8bc595681e13fec63ef0f6e3fcc292cfff67496c/src/opnsense/mvc/app/models/OPNsense/Firewall/Filter.xml#L44
class FilterRuleActionEnum(str, Enum):
    PASS    = "pass"
    BLOCK   = "block"
    REJECT  = "reject"

# https://github.com/opnsense/core/blob/8bc595681e13fec63ef0f6e3fcc292cfff67496c/src/opnsense/mvc/app/models/OPNsense/Firewall/Filter.xml#L64
class FilterRuleDirEnum(str, Enum):
    IN  = "in"
    OUT = "out"
    ANY = "any"

# https://github.com/opnsense/core/blob/8bc595681e13fec63ef0f6e3fcc292cfff67496c/src/opnsense/mvc/app/models/OPNsense/Firewall/Filter.xml#L73
class FilterRuleIPProtoEnum(str, Enum):
    INET4 = "inet"
    INET6 = "inet6"
    INET46 = "inet46"

class OPNsenseIfGroup(BaseModel):
    uuid: str                 = Field(alias="@uuid")    # /opnsense/ifgroups/ifgroupentry[x]/@uuid
    name: str                 = Field(alias="ifname")   # /opnsense/ifgroups/ifgroupentry[x]/ifname
    members: list[str]                                  # /opnsense/ifgroups/ifgroupentry[x]/members
    description: str   | None = Field(alias="descr")    # /opnsense/ifgroups/ifgroupentry[x]/descr

    @field_validator("members", mode="before")
    @classmethod
    def normalize_members(cls, value: str) -> list[str]:
        if value is None:
            return []
        if isinstance(value, str):
            return [str(v) for v in value.split(",")]
        if isinstance(value, list):
            return value
        return [str(value)]

class OPNsenseGateway(BaseModel):
    uuid: str                      = Field(alias="@uuid")     # /opnsense/OPNsense/Gateways/gateway_item[x]/@uuid
    disabled: bool                                            # /opnsense/OPNsense/Gateways/gateway_item[x]/disabled
    name: str                                                 # /opnsense/OPNsense/Gateways/gateway_item[x]/name
    interface: str                                            # /opnsense/OPNsense/Gateways/gateway_item[x]/interface
    gw_addr: IPAddress             = Field(alias="gateway")   # /opnsense/OPNsense/Gateways/gateway_item[x]/gateway
    is_default_gw: bool            = Field(alias="defaultgw") # /opnsense/OPNsense/Gateways/gateway_item[x]/defaultgw

class OPNsensePort(BaseModel):                                  # retrievable via port aliases
    name: str                                                   # readable name
    is_range: bool = False                                      # true, if portrange
    port: int = Field(gt=0, le=65535)                           # 0 < port <= 65535
    port_end: int | None = Field(gt=0, le=65535, default=None)  # end port of range if is_range

class OPNsenseHost(BaseModel):                              # retrievable via host aliases
    name: str                                               # readable name
    is_range: bool = False                                  # true, if hostrange
    host: IPAddress                                         #
    host_end: IPAddress | None = None                       #

class OPNsenseNetwork(BaseModel):                           # retrievable via host aliases
    name: str                                               # readable name
    net: IPNetwork                                          #

# https://github.com/opnsense/core/blob/8bc595681e13fec63ef0f6e3fcc292cfff67496c/src/opnsense/mvc/app/models/OPNsense/Firewall/Alias.xml
class OPNsenseAlias(BaseModel):
    uuid: str               = Field(alias="@uuid")                              # /opnsense/OPNsense/Firewall/Aliases/alias[x]/@uuid
    enabled: bool                                                               # /opnsense/OPNsense/Firewall/Aliases/alias[x]/enabled
    name: str                                                                   # /opnsense/OPNsense/Firewall/Aliases/alias[x]/name
    type: AliasTypeEnum                                                         # /opnsense/OPNsense/Firewall/Aliases/alias[x]/type
    value: list[str]        = Field(alias="content")                            # /opnsense/OPNsense/Firewall/Aliases/alias[x]/content
    is_used_by: list[OPNsenseAlias | OPNsenseAccessRule | OPNsenseNATRule] = [] # linking metaobjects using this object
    description: str | None = ""                                                # /opnsense/OPNsense/Firewall/Aliases/alias[x]/description

    @field_validator("value", mode="before")
    @classmethod
    def normalize_value(cls, value: str) -> list[str]:
        if value is None:
            return []
        if isinstance(value, str):
            return [str(v) for v in value.split("\n")]
        if isinstance(value, list):
            return value
        return [str(value)]

class OPNsenseHostAlias(OPNsenseAlias):
    type: AliasTypeEnum = Field(default=AliasTypeEnum.HOST, frozen=True)                                                # /opnsense/OPNsense/Firewall/Aliases/alias[x]/type
    childs: list[str | OPNsenseHost | OPNsenseNetwork | OPNsenseHostAlias | OPNsenseNetworkAlias] = Field(default=[])   # /opnsense/OPNsense/Firewall/Aliases/alias[x]/content

class OPNsenseNetworkAlias(OPNsenseAlias):
    type: AliasTypeEnum = Field(default=AliasTypeEnum.NETWORK, frozen=True)                                             # /opnsense/OPNsense/Firewall/Aliases/alias[x]/type
    childs: list[str | OPNsenseHost | OPNsenseNetwork | OPNsenseHostAlias | OPNsenseNetworkAlias] = Field(default=[])   # /opnsense/OPNsense/Firewall/Aliases/alias[x]/content

class OPNsensePortAlias(OPNsenseAlias):
    type: AliasTypeEnum      = Field(default=AliasTypeEnum.NETWORK, frozen=True)    # /opnsense/OPNsense/Firewall/Aliases/alias[x]/type
    childs: list[OPNsensePort | OPNsensePortAlias] = Field(default=[])              # /opnsense/OPNsense/Firewall/Aliases/alias[x]/content


# https://github.com/opnsense/core/blob/8bc595681e13fec63ef0f6e3fcc292cfff67496c/src/opnsense/mvc/app/models/OPNsense/Firewall/Filter.xml
class OPNsenseAccessRule(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    uuid: str                                                                   = Field(alias="@uuid")                                                         # /opnsense/filter/rule[x]/@uuid
    disabled: bool                                                              = False                                                                        # /opnsense/filter/rule[x]/disabled
    action: FilterRuleActionEnum                                                = Field(alias="type", default=FilterRuleActionEnum.PASS)                       # /opnsense/filter/rule[x]/type
    is_floating: bool                                                    | None = Field(alias="floating", default=False)                                       # /opnsense/filter/rule[x]/floating
    any_interface: bool                                                         = False                                                                        # /opnsense/filter/rule[x]/interface -> not existing
    interface: list[str]                                                 | None = ["Any"]                                                                      # /opnsense/filter/rule[x]/interface
    interface_neg: bool                                                  | None = Field(alias="interfacenot", default=False)                                   # /opnsense/filter/rule[x]/interfacenot
    direction: FilterRuleDirEnum                                                = FilterRuleDirEnum.IN                                                         # /opnsense/filter/rule[x]/direction
    logging: bool                                                        | None = Field(alias="log", default=False)                                            # /opnsense/filter/rule[x]/log
    ipprotocol: FilterRuleIPProtoEnum                                           = FilterRuleIPProtoEnum.INET4                                                  # /opnsense/filter/rule[x]/ipproto
    protocol: str                                                               = "Any"                                                                        # /opnsense/filter/rule[x]/protocol
    source_address: list[str | OPNsenseHostAlias | OPNsenseNetworkAlias] | None = Field(validation_alias=AliasPath("source", "address"), default=["Any"])      # /opnsense/filter/rule[x]/source/address
    source_network: list[str]                                            | None = Field(validation_alias=AliasPath("source", "network"), default=None)         # /opnsense/filter/rule[x]/source/network
    source_neg: bool                                                            = Field(validation_alias=AliasPath("source", "not"), default=False)            # /opnsense/filter/rule[x]/source/not
    source_port: list[str | OPNsensePortAlias]                           | None = Field(validation_alias=AliasPath("source", "port"), default=["Any"])         # /opnsense/filter/rule[x]/source/port
    dest_address: list[str | OPNsenseHostAlias | OPNsenseNetworkAlias]   | None = Field(validation_alias=AliasPath("destination", "address"), default=["Any"]) # /opnsense/filter/rule[x]/source/address
    dest_network: list[str]                                              | None = Field(validation_alias=AliasPath("destination", "network"), default=None)    # /opnsense/filter/rule[x]/source/network
    dest_neg: bool                                                       | None = Field(validation_alias=AliasPath("destination", "not"), default=False)       # /opnsense/filter/rule[x]/destination/not
    dest_port: list[str | OPNsensePortAlias]                             | None = Field(validation_alias=AliasPath("destination", "port"), default=["Any"])    # /opnsense/filter/rule[x]/destination/port
    description: str                                                     | None = Field(alias="descr", default="")                                             # /opnsense/filter/rule[x]/description

    @field_validator("interface", "source_port", "dest_port", "source_address", "dest_address", "source_network", "dest_network", mode="before")
    @classmethod
    def normalize_str_list(cls, value: str) -> list[str]:
        if value is None:
            return []
        if isinstance(value, str):
            return [str(v) for v in value.split(",")]
        if isinstance(value, list):
            return value
        return [str(value)]

    @field_validator("source_neg", "dest_neg", "interface_neg", "logging", mode="before")
    @classmethod
    def normalize_negate(cls, value: str) -> bool:
        if value is None:
            return False
        if value == '1':
            return True
        if value == True:
            return True
        return False

    @field_validator("is_floating", mode="before")
    @classmethod
    def normalize_floating(cls, value: str) -> bool:
        if value is None:
            return False
        if value == 'yes':
            return True
        if value == True:
            return True
        return False

# https://github.com/opnsense/core/blob/8bc595681e13fec63ef0f6e3fcc292cfff67496c/src/opnsense/mvc/app/models/OPNsense/Firewall/DNat.xml
class OPNsenseNATRule(BaseModel):
    is_outbound: bool                                                        = False                                                                       # /opnsense/nat/outbound
    disabled: bool                                                           = False                                                                       # /opnsense/nat/outbound/rule[x]/disabled
    interface: list[str]                                              | None = ["Any"]                                                                     # /opnsense/nat/outbound/rule[x]/interface
    logging: bool                                                            = Field(alias="log", default=False)                                           # /opnsense/nat/outbound/rule[x]/log
    ipprotocol: FilterRuleIPProtoEnum                                        = FilterRuleIPProtoEnum.INET4                                                 # /opnsense/nat/outbound/rule[x]/ipproto
    protocol: str                                                            = "Any"                                                                       # /opnsense/nat/outbound/rule[x]/protocol
    source_net: list[str | OPNsenseHostAlias | OPNsenseNetworkAlias]  | None = Field(validation_alias=AliasPath("source", "network"), default=None)        # /opnsense/nat/outbound/rule[x]/source[/network]
    source_addr: list[str | OPNsenseHostAlias | OPNsenseNetworkAlias] | None = Field(validation_alias=AliasPath("source", "address"), default=None)        # /opnsense/nat/outbound/rule[x]/source[/address]
    source_neg: bool                                                         = Field(validation_alias=AliasPath("source", "not"), default=False)           # /opnsense/nat/outbound/rule[x]/source/not
    source_port: list[str]                                            | None = Field(alias="sourceport", default=["Any"])                                  # /opnsense/nat/outbound/rule[x]/source/port
    dest_net: list[str | OPNsenseHostAlias | OPNsenseNetworkAlias]    | None = Field(validation_alias=AliasPath("destination", "network"), default=None)   # /opnsense/nat/outbound/rule[x]/source[/network]
    dest_addr: list[str | OPNsenseHostAlias | OPNsenseNetworkAlias]   | None = Field(validation_alias=AliasPath("destination", "address"), default=None)   # /opnsense/nat/outbound/rule[x]/source[/address]
    dest_neg: bool                                                           = Field(validation_alias=AliasPath("destination", "not"), default=False)      # /opnsense/nat/outbound/rule[x]/destination/not
    dest_port: list[str]                                              | None = Field(alias="dstport", default=["Any"])                                     # /opnsense/nat/outbound/rule[x]/destination/port
    description: str                                                         = Field(alias="descr", default="")                                            # /opnsense/nat/outbound/rule[x]/description
    xlat_addr: str | OPNsenseHostAlias | OPNsenseNetworkAlias         | None = Field(alias="target", default=None)                                         # /opnsense/nat/outbound/rule[x]/target
    xlat_port: str | OPNsensePortAlias                                | None = Field(alias="local-port", default=None)                                     # /opnsense/nat/rule[x]/local-port

    @field_validator("interface", "source_net", "source_addr", "dest_net", "dest_addr", "dest_port", "source_port", mode="before")
    @classmethod
    def normalize_str_list(cls, value: str) -> list[str]:
        if value is None:
            return []
        if isinstance(value, str):
            return [str(v) for v in value.split(",")]
        if isinstance(value, list):
            return value
        return [str(value)]

    @field_validator("source_neg", "dest_neg", "logging", mode="before")
    @classmethod
    def normalize_negate(cls, value: str) -> bool:
        if value is None:
            return False
        if value == '1':
            return True
        if value == True:
            return True
        return False

class OPNsenseInterface(BaseModel):
    name: str               | None = None                                  # /opnsense/interfaces[x]            (identifier)
    enabled: bool                  = Field(alias="enable", default=True)   # /opnsense/interfaces[x]/enable
    hw_interface: str              = Field(alias="if")                     # /opnsense/interfaces[x]/if         (assigned hardware interface)
    description: str               = Field(alias="descr")                  # /opnsense/interfaces[x]/descr
    ip4_address: IPAddress  | None = Field(alias="ipaddr", default=None)   # /opnsense/interfaces[x]/ipaddr
    ip4_subnet: int         | None = Field(alias="subnet", default=None)   # /opnsense/interfaces[x]/subnet
    ip6_address: IPAddress  | None = Field(alias="ipaddrv6", default=None) # /opnsense/interfaces[x]/ipaddrv6
    ip6_subnet: int         | None = Field(alias="subnetv6", default=None) # /opnsense/interfaces[x]/subnetv6
    type: Literal["group", "none", "undef"] | None = "undef"               # /opnsense/interfaces[x]/type

#https://github.com/opnsense/core/blob/8bc595681e13fec63ef0f6e3fcc292cfff67496c/src/opnsense/mvc/app/models/OPNsense/Auth/User.xml
class OPNsenseUser(BaseModel):
    uuid: str                      = Field(alias="@uuid") # /opnsense/system/user/@uuid
    uid: int                                              # /opnsense/system/user/uid
    name: str                                             # /opnsense/system/user/name
    disabled: bool                                        # /opnsense/system/user/disabled
    scope: UserScopeEnum                                  # /opnsense/system/user/scope
    email: str              | None                        # /opnsense/system/user/email
    privileges: list[str]   | None = Field(alias="priv")  # /opnsense/system/user/priv
    expires: str           | None                         # /opnsense/system/user/expires
    description: str               = Field(alias="descr") # /opnsense/system/user/descr

    @field_validator("privileges", mode="before")
    @classmethod
    def normalize_privileges(cls, value: str) -> list[str]:
        if value is None:
            return []
        if isinstance(value, str):
            return [str(v) for v in value.split(",")]
        if isinstance(value, list):
            return value
        return [str(value)]

class OPNsenseUserGroup(BaseModel):
    uuid: str                     = Field(alias="@uuid")  # /opnsense/system/group/@uuid
    gid: int                                              # /opnsense/system/group/gid
    name: str                                             # /opnsense/system/group/name
    scope: UserScopeEnum                                  # /opnsense/system/group/scope
    description: str       | None                         # /opnsense/system/group/description
    privileges: list[str]         = Field(alias="priv")   # /opnsense/system/group/priv
    member_uids: list[int] | None = Field(alias="member") # /opnsense/system/group/member

    @field_validator("privileges", mode="before")
    @classmethod
    def normalize_privileges(cls, value: str) -> list[str]:
        if value is None:
            return []
        if isinstance(value, str):
            return [str(v) for v in value.split(",")]
        if isinstance(value, list):
            return value
        return [str(value)]

    @field_validator("member_uids", mode="before")
    @classmethod
    def normalize_members(cls, value: str) -> list[int]:
        if value is None:
            return []
        if isinstance(value, str):
            return [int(v) for v in value.split(",")]
        if isinstance(value, list):
            return value
        return [int(value)]

class OPNsenseConfig(BaseModel):
    hostname: str                                               # /opnsense/system/hostname + /opnsense/system/domain
    last_change: str                              | None = None # /opnsense/revision/time
    user_groups: list[OPNsenseUserGroup]          | None = None # /opnsense/system/group[]
    users: list[OPNsenseUser]                     | None = None # /opnsense/system/user[]
    interfaces: dict[str, OPNsenseInterface]      | None = None # /opnsense/interfaces
    interface_groups: dict[str, OPNsenseIfGroup]  | None = None # /opnsense/ifgroups
    access_rules: list[OPNsenseAccessRule]        | None = None # /opnsense/filter
    nat_rules: list[OPNsenseNATRule]              | None = None # /opnsense/nat
    aliases: dict[str, OPNsenseAlias]             | None = None # /opnsense/OPNsense/Firewall/Alias
    host_aliases: dict[str, OPNsenseHostAlias]    | None = None # /opnsense/OPNsense/Firewall/Alias[type[text() = "host"]]
    net_aliases:  dict[str, OPNsenseNetworkAlias] | None = None # /opnsense/OPNsense/Firewall/Alias[type[text() = "network"]]
    port_aliases: dict[str, OPNsensePortAlias]    | None = None # /opnsense/OPNsense/Firewall/Alias[type[text() = "port"]]
    gateways: list[OPNsenseGateway]               | None = None # /opnsense/OPNsense/Gateways
    ports: dict[str, OPNsensePort]                | None = None # e.g. via /opnsense/OPNsense/Firewall/Alias[type[text() = "port"]] or via rules retrieved

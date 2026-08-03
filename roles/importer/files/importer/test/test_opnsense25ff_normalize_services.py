# pyright: reportPrivateUsage=false
# tests target internal service-normalization helpers, hence private-usage is allowed here
from typing import TYPE_CHECKING, Any

import pytest
from fw_modules.opnsense25ff.opnsense_model import (
    OPNsenseAccessRule,
    OPNsenseConfig,
    OPNsensePort,
    OPNsensePortAlias,
)
from fw_modules.opnsense25ff.opnsense_normalize_services import _normalize_services_from_port_alias, normalize_services
from fw_modules.opnsense25ff.opnsense_normalizer import _create_normalized_rule_from_access_rule
from pytest_mock import MockerFixture

if TYPE_CHECKING:
    from models.serviceobject import ServiceObject


def _port_alias(name: str) -> OPNsensePortAlias:
    return OPNsensePortAlias.model_validate(
        {"@uuid": f"uid-{name}", "enabled": True, "name": name, "content": "x", "description": name}
    )


def test_normalize_services_from_port_alias_builds_nested_group() -> None:
    inner = _port_alias("inner-ports")
    inner.childs.append(OPNsensePort(name="p-80", is_range=False, port=80, port_end=None))
    outer = _port_alias("outer-ports")
    outer.childs.append(inner)
    outer.childs.append(OPNsensePort(name="p-1000-2000", is_range=True, port=1000, port_end=2000))

    normalized: dict[str, ServiceObject] = {}
    svc = _normalize_services_from_port_alias(outer, normalized, 0)

    assert svc.svc_typ == "group"
    assert svc.svc_uid == "uid-outer-ports"
    assert svc.svc_member_names is not None
    assert set(svc.svc_member_names.split("|")) == {"inner-ports", "p-1000-2000"}

    assert normalized["p-80"].svc_port == 80
    assert (normalized["p-1000-2000"].svc_port, normalized["p-1000-2000"].svc_port_end) == (1000, 2000)
    assert normalized["inner-ports"].svc_typ == "group"
    assert normalized["inner-ports"].svc_member_names == "p-80"
    assert "outer-ports" in normalized  # the group registers itself too


def test_normalize_services_adds_builtin_named_ports() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-https",
            "type": "pass",
            "descr": "allow https to fw",
            "destination": {"network": "(self)", "port": "https"},
        }
    )
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = normalize_services(config)

    assert services["https"].svc_port == 443
    assert services["https"].svc_port_end == 443


def test_normalize_services_adds_builtin_imap_port() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-imap",
            "type": "pass",
            "descr": "allow imap to fw",
            "destination": {"network": "(self)", "port": "imap"},
        }
    )
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = normalize_services(config)

    assert services["imap"].svc_port == 143
    assert services["imap"].svc_port_end == 143
    assert "placeholder" not in (services["imap"].svc_comment or "")


def test_normalize_services_creates_placeholder_for_unknown_named_port() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-smtp",
            "type": "pass",
            "descr": "allow smtp to fw",
            "destination": {"network": "(self)", "port": "smtp"},
        }
    )
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = normalize_services(config)

    # unknown named ports must not vanish (they would crash ref resolution later)
    assert services["smtp"].svc_typ == "simple"
    assert services["smtp"].svc_port is None
    smtp_comment = services["smtp"].svc_comment
    assert smtp_comment is not None
    assert "placeholder" in smtp_comment


def test_normalize_services_creates_protocol_service_for_icmp() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-icmp",
            "type": "pass",
            "descr": "allow ping to fw",
            "protocol": "ICMP",
            "destination": {"network": "(self)"},
        }
    )
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = normalize_services(config)

    assert "ICMP" in services
    assert services["ICMP"].svc_port is None
    assert services["ICMP"].ip_proto == 1
    assert _create_normalized_rule_from_access_rule(rule).rule_svc == "ICMP"


def test_normalize_services_disambiguates_icmpv6() -> None:
    rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "r-icmp6",
            "type": "pass",
            "descr": "allow ping6 to fw",
            "protocol": "ICMP",
            "ipprotocol": "inet6",
            "destination": {"network": "(self)"},
        }
    )
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = normalize_services(config)

    assert "ICMPv6" in services
    assert services["ICMPv6"].ip_proto == 58
    assert _create_normalized_rule_from_access_rule(rule).rule_svc == "ICMPv6"


@pytest.mark.parametrize(
    ("os_protocol", "expected_proto"),
    [
        ("sctp", 132),
        ("l2tp", 115),
        ("eigrp", 88),
        ("vrrp", 112),
        ("ipv6-icmp", 58),
        ("ipcomp", 108),
    ],
)
def test_normalize_services_resolves_further_ip_protocols(os_protocol: str, expected_proto: int) -> None:
    rule = OPNsenseAccessRule.model_validate(
        {"@uuid": "r-proto", "type": "pass", "descr": "d", "protocol": os_protocol}
    )
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = normalize_services(config)

    assert services[os_protocol.upper()].ip_proto == expected_proto


def test_normalize_services_warns_about_unknown_ip_protocol(mocker: MockerFixture) -> None:
    warning = mocker.patch("fw_modules.opnsense25ff.opnsense_normalizer.FWOLogger.warning")
    rule = OPNsenseAccessRule.model_validate(
        {"@uuid": "r-unknown-proto", "type": "pass", "descr": "d", "protocol": "made-up"}
    )
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = normalize_services(config)

    assert services["MADE-UP"].ip_proto is None
    assert any("no IP protocol number known" in str(call) for call in warning.call_args_list)


def _port_rule(uid: str, protocol: str, dest_port: str | None) -> OPNsenseAccessRule:
    destination: dict[str, Any] = {"any": None}
    if dest_port is not None:
        destination["port"] = dest_port
    return OPNsenseAccessRule.model_validate(
        {"@uuid": uid, "type": "pass", "descr": "d", "protocol": protocol, "destination": destination}
    )


def test_normalize_services_separates_tcp_and_udp_port_services() -> None:
    tcp_rule = _port_rule("r-tcp", "tcp", "53")
    udp_rule = _port_rule("r-udp", "udp", "53")
    config = OPNsenseConfig(hostname="fw", access_rules=[tcp_rule, udp_rule])

    services = normalize_services(config)

    assert (services["53/tcp"].svc_port, services["53/tcp"].ip_proto) == (53, 6)
    assert (services["53/udp"].svc_port, services["53/udp"].ip_proto) == (53, 17)
    assert services["53/tcp"].svc_uid != services["53/udp"].svc_uid
    assert _create_normalized_rule_from_access_rule(tcp_rule).rule_svc == "53/tcp"
    assert _create_normalized_rule_from_access_rule(udp_rule).rule_svc == "53/udp"


def test_normalize_services_keeps_protocol_for_rules_without_port() -> None:
    rule = _port_rule("r-tcp-any", "tcp", None)
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = normalize_services(config)

    # a "tcp, any port" rule must not degrade to the protocol-agnostic Any service
    assert services["Any/tcp"].ip_proto == 6
    assert (services["Any/tcp"].svc_port, services["Any/tcp"].svc_port_end) == (1, 65535)
    assert _create_normalized_rule_from_access_rule(rule).rule_svc == "Any/tcp"


def test_normalize_services_instantiates_port_alias_per_protocol() -> None:
    alias = _port_alias("web-ports")
    alias.childs.append(OPNsensePort(name="80", is_range=False, port=80, port_end=None))
    alias.childs.append(OPNsensePort(name="443", is_range=False, port=443, port_end=None))
    rule = _port_rule("r-alias", "tcp", "web-ports")
    config = OPNsenseConfig(hostname="fw", port_aliases={alias.name: alias}, access_rules=[rule])

    services = normalize_services(config)

    qualified_group = services["web-ports/tcp"]
    assert qualified_group.svc_typ == "group"
    assert qualified_group.svc_member_names == "443/tcp|80/tcp"
    # groups stay without a protocol, their members carry it
    assert qualified_group.ip_proto is None
    assert services["80/tcp"].ip_proto == 6
    assert services["443/tcp"].ip_proto == 6
    # the protocol-agnostic alias objects are still normalized
    assert services["web-ports"].svc_member_names == "443|80"


@pytest.mark.parametrize("dest_port", ["8000:8080", "8000-8080"])
def test_normalize_services_resolves_direct_port_range(dest_port: str) -> None:
    rule = _port_rule("r-range", "tcp", dest_port)
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = normalize_services(config)

    qualified_name = f"{dest_port}/tcp"
    svc = services[qualified_name]
    # port ranges must keep their bounds instead of degrading to an unresolved placeholder
    assert (svc.svc_port, svc.svc_port_end) == (8000, 8080)
    assert svc.ip_proto == 6
    assert "placeholder" not in (svc.svc_comment or "")
    assert _create_normalized_rule_from_access_rule(rule).rule_svc == qualified_name


def test_normalize_services_matches_direct_port_range_to_alias_member() -> None:
    alias = _port_alias("high-ports")
    alias.childs.append(OPNsensePort(name="8000:8080", is_range=True, port=8000, port_end=8080))
    rule = _port_rule("r-range-direct", "any", "8000:8080")
    config = OPNsenseConfig(hostname="fw", port_aliases={alias.name: alias}, access_rules=[rule])

    services = normalize_services(config)

    # the alias member and the directly referenced range normalize to the very same service
    assert (services["8000:8080"].svc_port, services["8000:8080"].svc_port_end) == (8000, 8080)
    assert services["high-ports"].svc_member_names == "8000:8080"


def test_normalize_services_creates_placeholder_for_malformed_port_range() -> None:
    rule = _port_rule("r-bad-range", "tcp", "8000:http")
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = normalize_services(config)

    svc = services["8000:http/tcp"]
    assert svc.svc_port is None
    assert "placeholder" in (svc.svc_comment or "")


@pytest.mark.parametrize("os_protocol", ["any", "tcp/udp"])
def test_normalize_services_keeps_unqualified_names_for_ambiguous_protocols(os_protocol: str) -> None:
    rule = _port_rule("r-ambiguous", os_protocol, "53")
    config = OPNsenseConfig(hostname="fw", access_rules=[rule])

    services = normalize_services(config)

    # neither "any" nor "tcp/udp" can be expressed as a single ip_proto
    assert services["53"].ip_proto is None
    assert "53/tcp" not in services
    assert _create_normalized_rule_from_access_rule(rule).rule_svc == "53"

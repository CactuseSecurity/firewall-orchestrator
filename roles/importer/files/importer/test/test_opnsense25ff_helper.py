import pytest
from fw_modules.opnsense25ff.opnsense_helper import (
    is_int,
    is_ip,
    is_ip_range,
    is_ip_subnet,
    link_opnsense_ports_from_port_aliases,
    xlinking_rules_to_aliases,
)
from fw_modules.opnsense25ff.opnsense_model import (
    OPNsenseAccessRule,
    OPNsenseConfig,
    OPNsenseHostAlias,
    OPNsensePort,
    OPNsensePortAlias,
)


def _port_alias(name: str, content: str) -> OPNsensePortAlias:
    return OPNsensePortAlias.model_validate(
        {"@uuid": f"uid-{name}", "enabled": True, "name": name, "content": content, "description": ""}
    )


def _host_alias(name: str, content: str) -> OPNsenseHostAlias:
    return OPNsenseHostAlias.model_validate(
        {"@uuid": f"uid-{name}", "enabled": True, "name": name, "content": content, "description": ""}
    )


@pytest.mark.parametrize(
    ("value", "expected"),
    [("80", True), ("0", True), ("-5", True), ("80:90", False), ("abc", False), ("", False), ("3.5", False)],
)
def test_is_int(value: str, expected: bool) -> None:
    assert is_int(value) is expected


@pytest.mark.parametrize(
    ("value", "expected"),
    [
        ("192.0.2.1", True),
        ("::1", True),
        ("192.0.2.0/24", False),
        ("192.0.2.1-192.0.2.9", False),
        ("host", False),
        ("", False),
    ],
)
def test_is_ip(value: str, expected: bool) -> None:
    assert is_ip(value) is expected


@pytest.mark.parametrize(
    ("value", "expected"),
    [
        ("192.0.2.0/24", True),
        ("2001:db8::/32", True),
        ("192.0.2.1", False),
        ("192.0.2.1-192.0.2.9", False),
        ("notnet", False),
    ],
)
def test_is_ip_subnet(value: str, expected: bool) -> None:
    assert is_ip_subnet(value) is expected


@pytest.mark.parametrize(
    ("value", "expected"),
    [
        ("192.0.2.1-192.0.2.9", True),
        ("2001:db8::1-2001:db8::5", True),
        ("192.0.2.1", False),
        ("192.0.2.1-", False),
        ("a-b", False),
    ],
)
def test_is_ip_range(value: str, expected: bool) -> None:
    assert is_ip_range(value) is expected


def test_link_opnsense_ports_expands_ports_ranges_and_nested_aliases() -> None:
    web = _port_alias("web", "80\n443")
    rng = _port_alias("rng", "8000:8080")
    nested = _port_alias("nested", "web")
    config = OPNsenseConfig(hostname="fw", port_aliases={"web": web, "rng": rng, "nested": nested})

    link_opnsense_ports_from_port_aliases(config)

    # plain ports become OPNsensePort children
    assert [type(child) for child in web.childs] == [OPNsensePort, OPNsensePort]
    first_port, second_port = web.childs
    assert isinstance(first_port, OPNsensePort)
    assert isinstance(second_port, OPNsensePort)
    assert (first_port.port, first_port.is_range) == (80, False)
    assert (second_port.port, second_port.is_range) == (443, False)

    # port ranges become a single range child
    range_child = rng.childs[0]
    assert isinstance(range_child, OPNsensePort)
    assert range_child.is_range is True
    assert (range_child.port, range_child.port_end) == (8000, 8080)
    assert range_child.name == "__p_8000:8080"

    # a value pointing at another alias links the alias object itself
    nested_child = nested.childs[0]
    assert isinstance(nested_child, OPNsensePortAlias)
    assert nested_child is web


def test_xlinking_rules_to_aliases_replaces_refs_and_records_usage() -> None:
    host = _host_alias("internal-hosts", "192.0.2.1")
    web = _port_alias("web", "80")
    rule = OPNsenseAccessRule.model_validate(
        {"@uuid": "rule-1", "source": {"address": "internal-hosts", "port": "web"}, "descr": "r"}
    )
    config = OPNsenseConfig(
        hostname="fw",
        host_aliases={"internal-hosts": host},
        port_aliases={"web": web},
        access_rules=[rule],
    )

    xlinking_rules_to_aliases(config)

    # string refs are replaced by the resolved alias objects
    assert len(rule.source_address) == 1
    assert rule.source_address[0] is host
    assert len(rule.source_port) == 1
    assert rule.source_port[0] is web
    # the rule is registered as a user of both aliases (back-reference)
    assert any(used is rule for used in host.is_used_by)
    assert any(used is rule for used in web.is_used_by)

import pytest
from fw_modules.fortiosmanagementREST.fos_models import (
    FortiOSConfig,
    NwObjAddress,
    NwObjAddress6,
    NwObjInternetService,
    NwObjVip,
)
from fw_modules.fortiosmanagementREST.fos_network import (
    normalize_internet_services,
    normalize_ipv4_network_objects,
    normalize_single_ipv6_network_object,
    normalize_vips,
    parse_fortios_ip_range,
)


def _build_vip(name: str, uuid: str | None, extip: str | None) -> NwObjVip:
    return NwObjVip(
        name=name,
        q_origin_key=name,
        uuid=uuid,
        extip=extip,
        mappedip=[],
    )


def test_normalize_vips_single_host_extip():
    config = FortiOSConfig()
    config.nw_obj_vip = [_build_vip("vip_host", "uuid-host", "10.0.0.5")]
    lookup: dict[str, str] = {}

    result = list(normalize_vips(config, lookup))

    assert len(result) == 1
    obj = result[0]
    assert obj.obj_name == "vip_host"
    assert obj.obj_uid == "uuid-host"
    assert obj.obj_typ == "host"
    assert str(obj.obj_ip) == "10.0.0.5/32"
    assert str(obj.obj_ip_end) == "10.0.0.5/32"
    # rule lookup must resolve the VIP by name
    assert lookup["vip_host"] == "uuid-host"


def test_normalize_vips_range_extip():
    config = FortiOSConfig()
    config.nw_obj_vip = [_build_vip("vip_range", "uuid-range", "10.0.0.1-10.0.0.10")]
    lookup: dict[str, str] = {}

    obj = next(iter(normalize_vips(config, lookup)))

    assert obj.obj_typ == "ip_range"
    assert str(obj.obj_ip) == "10.0.0.1/32"
    assert str(obj.obj_ip_end) == "10.0.0.10/32"


def test_normalize_vips_missing_extip_falls_back_to_name_uid_and_dummy_ip():
    config = FortiOSConfig()
    config.nw_obj_vip = [_build_vip("vip_empty", None, None)]
    lookup: dict[str, str] = {}

    obj = next(iter(normalize_vips(config, lookup)))

    assert obj.obj_uid == "vip_empty"
    assert obj.obj_typ == "host"
    assert str(obj.obj_ip) == "0.0.0.0/32"
    assert str(obj.obj_ip_end) == "0.0.0.0/32"
    assert lookup["vip_empty"] == "vip_empty"


def test_parse_fortios_ip_range_treats_equal_range_endpoints_as_host():
    ip_start, ip_end, obj_typ = parse_fortios_ip_range("10.0.0.1-10.0.0.1", "vip_host", "test")

    assert str(ip_start) == "10.0.0.1/32"
    assert str(ip_end) == "10.0.0.1/32"
    assert obj_typ == "host"


def test_normalize_single_ipv6_network_object_marks_explicit_end_ip_as_range():
    native_object = NwObjAddress6.model_validate(
        {
            "name": "ipv6_range",
            "q_origin_key": "ipv6_range",
            "uuid": "uuid-ipv6-range",
            "type": "ipprefix",
            "ip6": "2001:db8::1/128",
            "end-ip": "2001:db8::2",
        }
    )
    lookup: dict[str, str] = {}

    result = normalize_single_ipv6_network_object(native_object, lookup)

    assert result.obj_typ == "ip_range"
    assert lookup["ipv6_range"] == "uuid-ipv6-range"


def test_normalize_single_ipv6_network_object_treats_equal_endpoints_as_host():
    native_object = NwObjAddress6.model_validate(
        {
            "name": "ipv6_host",
            "q_origin_key": "ipv6_host",
            "uuid": "uuid-ipv6-host",
            "type": "ipprefix",
            "ip6": "2001:db8::1/128",
            "end-ip": "2001:db8::1",
        }
    )
    lookup: dict[str, str] = {}

    result = normalize_single_ipv6_network_object(native_object, lookup)

    assert result.obj_typ == "host"
    assert lookup["ipv6_host"] == "uuid-ipv6-host"


@pytest.mark.parametrize(
    ("native_type", "normalized_type"),
    [
        ("fqdn", "domain"),
        ("wildcard-fqdn", "domain"),
        ("dynamic", "dynamic_net_obj"),
        ("geography", "dynamic_net_obj"),
        ("interface-subnet", "dynamic_net_obj"),
    ],
)
def test_normalize_ipv4_non_static_objects_have_no_ip_range(native_type: str, normalized_type: str) -> None:
    native_object = NwObjAddress.model_validate(
        {
            "name": f"ipv4_{native_type}",
            "q_origin_key": f"ipv4_{native_type}",
            "uuid": f"uuid-ipv4-{native_type}",
            "type": native_type,
        }
    )
    config = FortiOSConfig()
    config.nw_obj_address = [native_object]
    lookup: dict[str, str] = {}

    result = next(normalize_ipv4_network_objects(config, lookup))

    assert result.obj_typ == normalized_type
    assert result.obj_ip is None
    assert result.obj_ip_end is None
    assert lookup[native_object.name] == native_object.uuid


def test_normalize_ipv4_wildcard_has_no_range_because_pattern_may_be_non_contiguous() -> None:
    native_object = NwObjAddress.model_validate(
        {
            "name": "ipv4_wildcard",
            "q_origin_key": "ipv4_wildcard",
            "uuid": "uuid-ipv4-wildcard",
            "type": "wildcard",
        }
    )
    config = FortiOSConfig()
    config.nw_obj_address = [native_object]
    lookup: dict[str, str] = {}

    result = next(normalize_ipv4_network_objects(config, lookup))

    assert result.obj_typ == "dynamic_net_obj"
    assert result.obj_ip is None
    assert result.obj_ip_end is None
    assert lookup[native_object.name] == native_object.uuid


@pytest.mark.parametrize(
    ("native_type", "normalized_type"),
    [("fqdn", "domain"), ("dynamic", "dynamic_net_obj"), ("template", "dynamic_net_obj")],
)
def test_normalize_ipv6_non_static_objects_have_no_ip_range(native_type: str, normalized_type: str) -> None:
    native_object = NwObjAddress6.model_validate(
        {
            "name": f"ipv6_{native_type}",
            "q_origin_key": f"ipv6_{native_type}",
            "uuid": f"uuid-ipv6-{native_type}",
            "type": native_type,
        }
    )
    lookup: dict[str, str] = {}

    result = normalize_single_ipv6_network_object(native_object, lookup)

    assert result.obj_typ == normalized_type
    assert result.obj_ip is None
    assert result.obj_ip_end is None
    assert lookup[native_object.name] == native_object.uuid


def test_normalize_internet_services_have_no_ip_range() -> None:
    native_object = NwObjInternetService.model_validate(
        {
            "id": 1,
            "q_origin_key": 1,
            "name": "internet-service",
            "icon-id": 1,
            "direction": "destination",
            "database": "internet-service",
            "ip-range-number": 1,
            "extra-ip-range-number": 0,
            "ip-number": 1,
            "ip6-range-number": 0,
            "extra-ip6-range-number": 0,
            "singularity": 0,
            "obsolete": 0,
        }
    )
    config = FortiOSConfig()
    config.nw_obj_internet_service = [native_object]
    lookup: dict[str, str] = {}

    result = next(normalize_internet_services(config, lookup))

    assert result.obj_typ == "dynamic_net_obj"
    assert result.obj_ip is None
    assert result.obj_ip_end is None
    assert lookup[native_object.name] == native_object.name

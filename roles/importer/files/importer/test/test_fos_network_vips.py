from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig, NwObjAddress6, NwObjVip
from fw_modules.fortiosmanagementREST.fos_network import (
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


def test_normalize_vips_missing_extip_falls_back_to_name_uid_and_full_range():
    config = FortiOSConfig()
    config.nw_obj_vip = [_build_vip("vip_empty", None, None)]
    lookup: dict[str, str] = {}

    obj = next(iter(normalize_vips(config, lookup)))

    assert obj.obj_uid == "vip_empty"
    assert obj.obj_typ == "network"
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

from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig, NwObjVip
from fw_modules.fortiosmanagementREST.fos_network import normalize_vips


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

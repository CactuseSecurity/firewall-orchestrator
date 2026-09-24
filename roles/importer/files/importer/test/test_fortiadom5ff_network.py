from typing import Any

from fw_modules.fortiadom5ff.fmgr_network import normalize_network_objects
from fw_modules.fortiadom5ff.fmgr_rule import find_addr_ref


def normalize_fortimanager_object(object_type: str, native_object: dict[str, Any]) -> dict[str, Any]:
    native_config = {"objects": {object_type: {"data": [native_object]}}}
    normalized_config_adom: dict[str, Any] = {"zone_objects": []}
    normalized_config_global: dict[str, Any] = {"zone_objects": []}

    normalize_network_objects(
        native_config,
        normalized_config_adom,
        normalized_config_global,
        [object_type],
    )

    return normalized_config_adom["network_objects"][0]


def test_dynamic_object_has_no_static_ip_range():
    object_type = "nw_obj_global_firewall/internet-service-basic"

    dynamic_object = normalize_fortimanager_object(
        object_type,
        {
            "name": "FortiManager Dynamic Object",
            "q_origin_key": 123,
        },
    )

    assert dynamic_object["obj_typ"] == "dynamic_net_obj"
    assert dynamic_object["obj_ip"] is None
    assert dynamic_object["obj_ip_end"] is None


def test_fqdn_object_has_no_static_ip_range():
    object_type = "nw_obj_global_firewall/address"

    fqdn_object = normalize_fortimanager_object(
        object_type,
        {
            "name": "FortiManager FQDN Object",
            "fqdn": "example.test",
        },
    )

    assert fqdn_object["obj_typ"] == "domain"
    assert fqdn_object["obj_ip"] is None
    assert fqdn_object["obj_ip_end"] is None


def test_find_addr_ref_resolves_addressless_objects_by_address_family():
    dynamic_object = normalize_fortimanager_object(
        "nw_obj_global_firewall/internet-service-basic",
        {"name": "dynamic-object", "q_origin_key": 1},
    )
    ipv4_fqdn = normalize_fortimanager_object(
        "nw_obj_global_firewall/address",
        {"name": "ipv4-fqdn", "uuid": "ipv4-fqdn-uid", "fqdn": "ipv4.example.test"},
    )
    ipv6_fqdn = normalize_fortimanager_object(
        "nw_obj_global_firewall/address6",
        {"name": "ipv6-fqdn", "uuid": "ipv6-fqdn-uid", "fqdn": "ipv6.example.test"},
    )
    ipv6_pool = normalize_fortimanager_object(
        "nw_obj_global_firewall/ippool6",
        {"name": "ipv6-pool", "uuid": "ipv6-pool-uid", "startip": "2001:db8::1", "endip": "2001:db8::ff"},
    )
    normalized_config_adom = {"network_objects": [dynamic_object, ipv4_fqdn, ipv6_fqdn, ipv6_pool]}
    normalized_config_global: dict[str, list[dict[str, Any]]] = {"network_objects": []}

    assert (
        find_addr_ref(
            "dynamic-object",
            is_v4=True,
            normalized_config_adom=normalized_config_adom,
            normalized_config_global=normalized_config_global,
        )
        == "dynamic-object"
    )
    assert (
        find_addr_ref(
            "ipv4-fqdn",
            is_v4=True,
            normalized_config_adom=normalized_config_adom,
            normalized_config_global=normalized_config_global,
        )
        == "ipv4-fqdn-uid"
    )
    assert (
        find_addr_ref(
            "ipv6-fqdn",
            is_v4=False,
            normalized_config_adom=normalized_config_adom,
            normalized_config_global=normalized_config_global,
        )
        == "ipv6-fqdn-uid"
    )
    assert (
        find_addr_ref(
            "ipv6-pool",
            is_v4=False,
            normalized_config_adom=normalized_config_adom,
            normalized_config_global=normalized_config_global,
        )
        == "ipv6-pool-uid"
    )

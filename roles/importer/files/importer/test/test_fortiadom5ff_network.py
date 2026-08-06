from typing import Any

from fw_modules.fortiadom5ff.fmgr_network import normalize_network_objects


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

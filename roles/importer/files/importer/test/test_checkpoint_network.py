from typing import Any

from fw_modules.checkpointR8x.cp_network import normalize_network_objects


def normalize_checkpoint_object(table_type: str, object_type: str) -> dict[str, Any]:
    native_config = {
        "objects": [
            {
                "type": table_type,
                "chunks": [
                    {
                        "objects": [
                            {
                                "uid": f"{object_type}-uid",
                                "name": f"Checkpoint {object_type}",
                                "color": "black",
                                "type": object_type,
                                "domain": {"uid": "global-domain"},
                            }
                        ]
                    }
                ],
            }
        ]
    }
    normalized_config: dict[str, Any] = {}

    normalize_network_objects(native_config, normalized_config, import_id=1)

    return normalized_config["network_objects"][0]


def test_updatable_object_has_no_static_ip_range():
    dynamic_object = normalize_checkpoint_object("updatable-objects", "updatable-object")

    assert dynamic_object["obj_typ"] == "dynamic_net_obj"
    assert dynamic_object["obj_ip"] is None
    assert dynamic_object["obj_ip_end"] is None


def test_fqdn_object_has_no_static_ip_range():
    fqdn_object = normalize_checkpoint_object("dns-domains", "dns-domain")

    assert fqdn_object["obj_typ"] == "domain"
    assert fqdn_object["obj_ip"] is None
    assert fqdn_object["obj_ip_end"] is None

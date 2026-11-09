from typing import Any

import fwo_const
from fw_modules.checkpointR8x import cp_const, cp_object_resolver
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


def test_access_role_has_no_static_ip_range():
    access_role = normalize_checkpoint_object("access-roles", "access-role")

    assert access_role["obj_typ"] == "access-role"
    assert access_role["obj_ip"] is None
    assert access_role["obj_ip_end"] is None


def test_legacy_voip_domain_object_is_normalized_as_host_without_real_ip():
    voip_object = normalize_checkpoint_object("CpmiVoipSipDomain", "CpmiVoipSipDomain")

    assert voip_object["obj_typ"] == "host"
    assert voip_object["obj_ip"] == fwo_const.DUMMY_IP
    assert voip_object["obj_ip_end"] == fwo_const.DUMMY_IP


def test_new_style_voip_domain_objects_are_normalized_like_legacy_ones():
    for voip_type in ["voip-domain-sip-proxy", "voip-domain-mgcp-call-agent", "voip-domain-h323-gatekeeper"]:
        voip_object = normalize_checkpoint_object(voip_type, voip_type)

        assert voip_object["obj_uid"] == f"{voip_type}-uid", voip_type
        assert voip_object["obj_typ"] == "host", voip_type
        assert voip_object["obj_ip"] == fwo_const.DUMMY_IP, voip_type


def test_unknown_object_table_is_skipped():
    native_config: dict[str, Any] = {
        "objects": [
            {
                "type": "unknown-table",
                "chunks": [{"objects": [{"uid": "u-1", "name": "x", "color": "black", "type": "unknown-table"}]}],
            }
        ]
    }
    normalized_config: dict[str, Any] = {}

    normalize_network_objects(native_config, normalized_config, import_id=1)

    assert normalized_config["network_objects"] == []


def test_voip_object_only_referenced_via_object_dictionary_is_resolvable():
    voip_uid = "e9ba0c50-ddd7-4aa8-9df6-1c4045ba10bb"
    object_dictionary: list[dict[str, Any]] = [
        {
            "uid": voip_uid,
            "name": "Lync_Test_VoIP",
            "type": "voip-domain-sip-proxy",
            "domain": {"uid": "41e821a0-3720-11e3-aa6e-0800200c9fde", "name": "SMC User", "domain-type": "domain"},
            "icon": "NetworkObjects/VoIPSIP",
            "color": "black",
        }
    ]
    native_config: dict[str, Any] = {"objects": []}
    normalized_config: dict[str, Any] = {}

    cp_object_resolver.resolve_ref_from_object_dictionary(
        voip_uid, object_dictionary, native_config, field_name="destination"
    )
    normalize_network_objects(native_config, normalized_config, import_id=1)

    normalized_uids = [nw_obj["obj_uid"] for nw_obj in normalized_config["network_objects"]]
    assert normalized_uids == [voip_uid]


def test_voip_and_dictionary_only_types_are_detected():
    assert cp_const.is_voip_domain_obj_type("CpmiVoipSipDomain")
    assert cp_const.is_voip_domain_obj_type("voip-domain-sip-proxy")
    assert not cp_const.is_voip_domain_obj_type("gsn_handover_group")
    assert not cp_const.is_voip_domain_obj_type("host")
    assert cp_const.is_object_dictionary_only_obj_type("gsn_handover_group")
    assert cp_const.is_object_dictionary_only_obj_type("voip-domain-sccp-call-manager")
    assert not cp_const.is_object_dictionary_only_obj_type("hosts")


def test_dictionary_only_types_are_not_fetched_via_type_specific_show_command():
    fetched_types = [
        obj_type for obj_type in cp_const.api_obj_types if cp_const.is_object_dictionary_only_obj_type(obj_type)
    ]

    assert fetched_types == []

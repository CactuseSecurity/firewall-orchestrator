import pytest
from fw_modules.ciscofirepowerdomain7ff.fwcommon import CiscoFirepowerDomain7ffCommon
from fw_modules.fortiadom5ff.fmgr_user import normalize_users
from fw_modules.fortiosmanagementREST import fos_const
from fw_modules.nsx4ff.fwcommon import Nsx4ffCommon
from fw_modules.paloaltomanagement2023ff.fwcommon import PaloAltoManagement2023ffCommon
from fwo_base import ConfFormat
from model_controllers.fwconfigmanager_controller import FwConfigManagerController
from model_controllers.gateway_controller import GatewayController
from models.fwconfig import FwConfig
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway


def test_normalize_users_converts_simple_and_group_users() -> None:
    config_to_import: dict[str, list[dict[str, str | None]]] = {}
    full_config = {
        "users": [
            {"name": "alice", "comment": "person", "color": 7},
            {"name": "admins", "member": ["alice", "bob"], "color": 0},
        ]
    }

    normalize_users(full_config, config_to_import, ["users"])

    assert config_to_import["user_objects"] == [
        {
            "user_typ": "simple",
            "user_name": "alice",
            "user_color": "7",
            "user_uid": "alice",
            "user_comment": "person",
            "user_member_refs": None,
            "user_member_names": None,
        },
        {
            "user_typ": "group",
            "user_name": "admins",
            "user_color": None,
            "user_uid": "admins",
            "user_comment": None,
            "user_member_refs": "alice|bob",
            "user_member_names": "alice|bob",
        },
    ]


def test_unsupported_common_importers_raise_clear_errors() -> None:
    importers_and_names = [
        (PaloAltoManagement2023ffCommon(), "Palo Alto Management 2023 ff"),
        (CiscoFirepowerDomain7ffCommon(), "Cisco Firepower Domain 7ff"),
        (Nsx4ffCommon(), "NSX 4ff"),
    ]

    for importer, name in importers_and_names:
        with pytest.raises(NotImplementedError, match=name):
            importer.get_config(None, None)  # type: ignore[arg-type]


def test_import_models_and_gateway_controller_preserve_values() -> None:
    config = FwConfig(ConfigFormat=ConfFormat.FORTINET, FwConf={"native": True})
    normalized_config = FwConfigNormalized()
    manager = FwConfigManagerController.from_json(
        {
            "manager_uid": "manager-uid",
            "mgm_name": "manager",
            "is_global": True,
            "dependant_manager_uids": ["dependent"],
            "configs": [normalized_config],
        }
    )
    gateway = Gateway(Uid="gateway-uid", Name="gateway")
    controller = GatewayController(gateway)

    assert config.ConfigFormat is ConfFormat.FORTINET
    assert config.FwConf == {"native": True}
    assert str(manager) == f"manager-uid({manager.configs!s})"
    assert controller == gateway
    assert controller != object()


def test_fortios_scopes_cover_network_service_user_and_rule_types() -> None:
    assert [f"nw_obj_{object_type}" for object_type in fos_const.NW_OBJ_TYPES] == fos_const.NW_OBJ_SCOPE
    assert [f"svc_obj_{object_type}" for object_type in fos_const.SVC_OBJ_TYPES] == fos_const.SVC_OBJ_SCOPE
    assert [f"user_obj_{object_type}" for object_type in fos_const.USER_OBJ_TYPES] == fos_const.USER_SCOPE
    assert fos_const.RULE_SCOPE == fos_const.RULE_ACCESS_SCOPE + fos_const.RULE_NAT_SCOPE

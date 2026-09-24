import unittest.mock
from typing import Any

from fw_modules.checkpointR8x import fwcommon
from model_controllers.fwconfigmanagerlist_controller import (
    FwConfigManagerListController,
)
from model_controllers.import_state_controller import ImportStateController


class TestCheckpointNativeFileImport:
    def test_checkpoint_native_file_import_skips_login(self, import_state_controller: ImportStateController):
        config_in = FwConfigManagerListController()
        config_in.native_config = {
            "domains": [
                {
                    "domain_name": "",
                    "domain_uid": "",
                    "is-super-manager": False,
                    "management_name": "unit-test",
                    "management_uid": "uid",
                    "objects": [],
                    "rulebases": [],
                    "nat_rulebases": [],
                    "gateways": [],
                }
            ]
        }

        import_state = import_state_controller

        fwcommon.cp_getter.login = unittest.mock.Mock(
            side_effect=AssertionError("login should not be called for native config imports")
        )

        fwcommon.normalize_config = unittest.mock.Mock(return_value=config_in)

        _, result = fwcommon.get_config(config_in, import_state)

        assert result is config_in

    def test_get_ordered_layer_uids_adds_access_layers_once_for_multiple_matching_targets(
        self,
    ):
        policy = {
            "uid": "policy-uid",
            "targets": [{"uid": "device-uid"}, {"uid": "all"}],
            "access-layers": [
                {"uid": "layer-1", "domain": "domain-a"},
                {"uid": "layer-2", "domain": "domain-a"},
            ],
        }
        device_config = {"uid": "device-uid"}

        ordered_layer_uids = fwcommon.get_ordered_layer_uids(policy, device_config, "domain-a")

        assert ordered_layer_uids == ["policy-uid", "layer-1", "layer-2"]

    def test_define_global_rulebase_link_skips_local_package_uid(self):
        # ordered_layer_uids carries the local policy/package uid at index 0 (see
        # get_ordered_layer_uids), followed by the real local access layers. A global
        # placeholder must be linked to the first real layer, not to the package uid.
        device_config: dict[str, list[Any]] = {"rulebase_links": []}
        ordered_layer_uids = ["local-policy-uid", "local-layer-1"]

        placeholder_chunk = {
            "rulebase": [
                {
                    "type": "access-section",
                    "uid": "global-section-uid",
                    "rulebase": [{"type": "place-holder", "uid": "placeholder-rule-uid"}],
                }
            ]
        }
        native_config_global_domain: dict[str, list[Any]] = {
            "rulebases": [
                {"uid": "global-policy-uid", "name": "global-policy", "chunks": []},
                {
                    "uid": "global-layer-uid",
                    "name": "global-layer",
                    "chunks": [placeholder_chunk],
                },
            ]
        }
        global_policy_rulebases_uid_list = ["global-policy-uid", "global-layer-uid"]

        fwcommon.define_global_rulebase_link(
            device_config,
            ordered_layer_uids,
            native_config_global_domain,
            global_policy_rulebases_uid_list,
        )

        assert device_config["rulebase_links"] == [
            {
                "from_rulebase_uid": "global-section-uid",
                "from_rule_uid": None,
                "to_rulebase_uid": "local-layer-1",
                "type": "domain",
                "is_global": False,
                "is_initial": False,
                "is_section": False,
            }
        ]

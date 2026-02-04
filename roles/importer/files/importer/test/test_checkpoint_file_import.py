import unittest.mock

from fw_modules.checkpointR8x import fwcommon
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
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

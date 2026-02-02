from fw_modules.checkpointR8x import fwcommon
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from test.mocking.mock_import_state import MockImportStateController


def test_checkpoint_native_file_import_skips_login(monkeypatch):
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

    import_state = MockImportStateController(stub_setCoreData=True)

    def fake_login(_details):
        raise AssertionError("login should not be called for native config imports")

    def fake_normalize_config(_state, _config_in, _parsing_config_only, _sid):
        return config_in

    monkeypatch.setattr(fwcommon.cp_getter, "login", fake_login)
    monkeypatch.setattr(fwcommon, "normalize_config", fake_normalize_config)

    _, result = fwcommon.get_config(config_in, import_state)

    assert result is config_in

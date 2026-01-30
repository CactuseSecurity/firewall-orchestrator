from common import get_config_uri, set_filename
from test.mocking.mock_import_state import MockImportStateController


def test_get_config_uri_prefers_hostname_uri():
    import_state = MockImportStateController()
    import_state.state.mgm_details.hostname = "https://example.com/config.json"
    import_state.state.mgm_details.domain_name = "https://example.com/ignored.json"

    assert get_config_uri(import_state) == "https://example.com/config.json"


def test_get_config_uri_falls_back_to_config_path_uri():
    import_state = MockImportStateController()
    import_state.state.mgm_details.hostname = "fw.example.com"
    import_state.state.mgm_details.domain_name = "file:///tmp/config.json"

    assert get_config_uri(import_state) == "file:///tmp/config.json"


def test_set_filename_uses_config_uri_when_present():
    import_state = MockImportStateController()
    import_state.state.mgm_details.hostname = "fw.example.com"
    import_state.state.mgm_details.domain_name = "https://example.com/config.json"

    set_filename(import_state)

    assert import_state.state.import_file_name == "https://example.com/config.json"

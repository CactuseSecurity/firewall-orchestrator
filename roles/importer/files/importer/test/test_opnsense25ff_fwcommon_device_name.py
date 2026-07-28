import pytest
import requests
from fw_modules.opnsense25ff import fwcommon
from fwo_exceptions import FwoNativeConfigFetchError
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from pytest_mock import MockerFixture


class TestEnsureDeviceName:
    def test_ensure_device_name_uses_gateway_uid(
        self,
        import_state_controller: ImportStateController,
    ) -> None:
        import_state = import_state_controller
        import_state.state.gateway_map = {import_state.state.mgm_details.current_mgm_id: {"gw-uid": 1}}
        import_state.state.mgm_details.devices = []

        fwcommon.ensure_device_name(import_state)

        assert import_state.state.mgm_details.devices[0]["name"] == "gw-uid"

    def test_ensure_device_name_overrides_non_matching_device(
        self,
        import_state_controller: ImportStateController,
    ) -> None:
        import_state = import_state_controller
        import_state.state.gateway_map = {import_state.state.mgm_details.current_mgm_id: {"gw-uid": 1}}
        import_state.state.mgm_details.devices = [{"name": "native-hostname"}]

        fwcommon.ensure_device_name(import_state)

        assert import_state.state.mgm_details.devices[0]["name"] == "gw-uid"


def test_get_config_fetches_sanitizes_and_normalizes_config(
    mocker: MockerFixture,
    import_state_controller: ImportStateController,
) -> None:
    config = FwConfigManagerListController.generate_empty_config()
    response = mocker.Mock()
    response.content = b"<opnsense><system /></opnsense>"
    response.raise_for_status = mocker.Mock()
    session = mocker.MagicMock()
    session.__enter__.return_value = session
    session.get.return_value = response
    mocker.patch.object(fwcommon.requests, "Session", return_value=session)
    mocker.patch.object(fwcommon.xmltodict, "parse", return_value={"opnsense": {"system": {}}})
    sanitizer = mocker.patch.object(
        fwcommon,
        "remove_opnsense_sensitive_data",
        return_value={"opnsense": {"sanitized": True}},
    )
    normalizer = mocker.patch.object(fwcommon, "normalize_opnsense_config", return_value=config)

    rc, result = fwcommon.get_config(config, import_state_controller)

    assert rc == 0
    assert result is config
    assert config.native_config == {"opnsense": {"sanitized": True}}
    session.get.assert_called_once_with(
        "https://mock.example.com:443/api/core/backup/download/this",
        timeout=60,
    )
    response.raise_for_status.assert_called_once_with()
    assert session.verify == import_state_controller.state.verify_certs
    assert session.auth.username == "mock-user"
    assert session.auth.password == "mock-secret"  # noqa: S105
    sanitizer.assert_called_once_with({"opnsense": {"system": {}}})
    normalizer.assert_called_once_with(config, import_state=import_state_controller)


def test_get_config_wraps_request_errors(
    mocker: MockerFixture,
    import_state_controller: ImportStateController,
) -> None:
    config = FwConfigManagerListController.generate_empty_config()
    session = mocker.MagicMock()
    session.__enter__.return_value = session
    session.get.side_effect = requests.exceptions.Timeout("timeout")
    mocker.patch.object(fwcommon.requests, "Session", return_value=session)
    logger = mocker.patch.object(fwcommon.FWOLogger, "exception")

    with pytest.raises(FwoNativeConfigFetchError, match="API request failed"):
        fwcommon.get_config(config, import_state_controller)

    logger.assert_called_once_with("[-] get_config: API request failed: timeout", exc_info=True)


def test_get_config_logs_unexpected_processing_errors_with_traceback(
    mocker: MockerFixture,
    import_state_controller: ImportStateController,
) -> None:
    config = FwConfigManagerListController.generate_empty_config()
    response = mocker.Mock()
    response.content = b"<opnsense>invalid</opnsense>"
    response.raise_for_status = mocker.Mock()
    session = mocker.MagicMock()
    session.__enter__.return_value = session
    session.get.return_value = response
    mocker.patch.object(fwcommon.requests, "Session", return_value=session)
    mocker.patch.object(fwcommon.xmltodict, "parse", side_effect=ValueError("invalid XML"))
    logger = mocker.patch.object(fwcommon.FWOLogger, "exception")

    with pytest.raises(ValueError, match="invalid XML"):
        fwcommon.get_config(config, import_state_controller)

    logger.assert_called_once_with(
        "[-] get_config: failed to process OPNsense configuration",
        exc_info=True,
    )

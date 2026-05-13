import pytest
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoImporterError
from pytest_mock import MockerFixture
from states.global_state import GlobalState
from states.import_state import ImportState


@pytest.fixture
def import_state(
    api_call: FwoApiCall,
    api_connection: FwoApi,
    mocker: MockerFixture,
) -> ImportState:
    mgm_id = 1
    mock_mgm = mocker.Mock(mgm_id=mgm_id, current_mgm_id=mgm_id)

    mocker.patch("states.import_state.ManagementController.get_mgm_details", return_value={})
    mocker.patch("states.import_state.ManagementController.from_json", return_value=mock_mgm)

    api_call.get_last_complete_import = mocker.Mock(return_value=(0, ""))
    api_call.get_config_value = mocker.Mock(return_value="30")

    return ImportState(fwo_api=api_connection, fwo_api_call=api_call, mgm_id=mgm_id)


class TestFwConfigImportObjectGetProtocolMap:
    def test_get_protocol_map(
        self,
        mocker: MockerFixture,
        global_state: GlobalState,
    ):
        # Arrange
        global_state.fwo_api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "stm_ip_proto": [
                        {
                            "ip_proto_name": "tcp",
                            "ip_proto_id": 6,
                        }
                    ]
                }
            }
        )
        expected_protocol_map = {
            "tcp": 6,
        }

        # Act
        global_state.stm_mapper.set_protocol_map(global_state.fwo_api_call)

        # Assert
        assert global_state.stm_mapper.protocol_map == expected_protocol_map

    def test_get_protocol_map_with_exception(
        self,
        global_state: GlobalState,
        mocker: MockerFixture,
    ):
        # Arrange
        global_state.fwo_api_call.call = mocker.Mock(side_effect=Exception("Unexpected error occurred"))
        global_state.stm_mapper.protocol_map = {}
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")

        # Act and Assert
        with pytest.raises(FwoImporterError):
            global_state.stm_mapper.set_protocol_map(global_state.fwo_api_call)

        # Assert
        mock_logger.assert_called_once()
        assert global_state.stm_mapper.protocol_map == {}


class TestFwConfigImportObjectGetUserObjTypeMap:
    def test_get_userobj_type_map(
        self,
        mocker: MockerFixture,
        global_state: GlobalState,
    ):
        # Arrange
        global_state.fwo_api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "stm_usr_typ": [
                        {
                            "usr_typ_name": "user",
                            "usr_typ_id": 1,
                        }
                    ]
                }
            }
        )
        expected_userobj_type_map = {
            "user": 1,
        }

        # Act
        global_state.stm_mapper.set_user_obj_type_map(global_state.fwo_api_call)

        # Assert
        assert global_state.stm_mapper.user_obj_type_map == expected_userobj_type_map

    def test_get_userobj_type_map_with_exception(
        self,
        global_state: GlobalState,
        mocker: MockerFixture,
    ):
        # Arrange
        global_state.fwo_api_call.call = mocker.Mock(side_effect=Exception("Unexpected error occurred"))
        global_state.stm_mapper.user_obj_type_map = {}
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")

        # Act and Assert
        with pytest.raises(FwoImporterError):
            global_state.stm_mapper.set_user_obj_type_map(global_state.fwo_api_call)

        # Assert
        mock_logger.assert_called_once()
        assert global_state.stm_mapper.user_obj_type_map == {}


class TestFwConfigImportObjectGetServiceObjTypeMap:
    def test_get_serviceobj_type_map(
        self,
        mocker: MockerFixture,
        global_state: GlobalState,
    ):
        # Arrange
        global_state.fwo_api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "stm_svc_typ": [
                        {
                            "svc_typ_name": "service",
                            "svc_typ_id": 1,
                        }
                    ]
                }
            }
        )
        expected_serviceobj_type_map = {
            "service": 1,
        }

        # Act
        global_state.stm_mapper.set_service_obj_type_map(global_state.fwo_api_call)

        # Assert
        assert global_state.stm_mapper.service_obj_type_map == expected_serviceobj_type_map

    def test_get_serviceobj_type_map_with_exception(
        self,
        global_state: GlobalState,
        mocker: MockerFixture,
    ):
        # Arrange
        global_state.fwo_api_call.call = mocker.Mock(side_effect=Exception("Unexpected error occurred"))
        global_state.stm_mapper.service_obj_type_map = {}
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")

        # Act and Assert
        with pytest.raises(FwoImporterError):
            global_state.stm_mapper.set_service_obj_type_map(global_state.fwo_api_call)

        # Assert
        mock_logger.assert_called_once()
        assert global_state.stm_mapper.service_obj_type_map == {}


class TestFwConfigImportObjectGetNetworkObjTypeMap:
    def test_get_networkobj_type_map(
        self,
        mocker: MockerFixture,
        global_state: GlobalState,
    ):
        # Arrange
        global_state.fwo_api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "stm_obj_typ": [
                        {
                            "obj_typ_name": "network",
                            "obj_typ_id": 1,
                        }
                    ]
                }
            }
        )
        expected_networkobj_type_map = {
            "network": 1,
        }

        # Act
        global_state.stm_mapper.set_network_obj_type_map(global_state.fwo_api_call)

        # Assert
        assert global_state.stm_mapper.network_obj_type_map == expected_networkobj_type_map

    def test_get_networkobj_type_map_with_exception(
        self,
        global_state: GlobalState,
        mocker: MockerFixture,
    ):
        # Arrange
        global_state.fwo_api_call.call = mocker.Mock(side_effect=Exception("Unexpected error occurred"))
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")

        # Act
        with pytest.raises(FwoImporterError):
            global_state.stm_mapper.set_network_obj_type_map(global_state.fwo_api_call)

        # Assert
        mock_logger.assert_called_once()


class TestFwConfigImportObjectLookupObjType:
    def test_lookup_obj_type_unknown(
        self,
        global_state: GlobalState,
    ):
        # Arrange
        obj_type_str = "some-obj-type"

        # Act and Assert
        with pytest.raises(FwoImporterError):
            global_state.stm_mapper.lookup_network_obj_type_id(obj_type_str)

    def test_lookup_obj_type_known(
        self,
        global_state: GlobalState,
    ):
        # Arrange
        obj_type_str = "imported"
        expected_obj_type = 2
        global_state.stm_mapper.network_obj_type_map = {
            "builtin": 1,
            "imported": 2,
            "custom": 3,
        }

        # Act
        obj_type = global_state.stm_mapper.lookup_network_obj_type_id(obj_type_str)

        # Assert
        assert obj_type == expected_obj_type

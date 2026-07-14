import pytest
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoImporterError
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import ManagementController
from models.import_state import ImportState
from pytest_mock import MockerFixture
from services.uid2id_mapper import Uid2IdMapper


@pytest.fixture
def import_state_controller(
    management_controller: ManagementController,
    api_call: FwoApiCall,
    api_connection: FwoApi,
) -> ImportStateController:
    import_state = ImportState()
    import_state.mgm_details = management_controller
    import_state.tracks = {"ordered": 2, "inline": 3, "concatenated": 4, "domain": 5}
    import_state.link_types = {
        "ordered": 2,
        "inline": 3,
        "concatenated": 4,
        "domain": 5,
    }
    controller = ImportStateController(state=import_state, api_call=api_call)
    controller.state = import_state
    controller.api_call = api_call
    controller.api_connection = api_connection
    return controller


class TestFwConfigImportObjectGetProtocolMap:
    def test_get_protocol_map(
        self,
        fwconfig_import_object: FwConfigImportObject,
        import_state_controller: ImportStateController,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
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
        import_state_controller.set_protocol_map()

        # Assert
        assert import_state_controller.state.protocol_map == expected_protocol_map

    def test_get_protocol_map_with_exception(
        self,
        fwconfig_import_object: FwConfigImportObject,
        import_state_controller: ImportStateController,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            side_effect=Exception("Unexpected error occurred")
        )
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")

        # Act and Assert
        with pytest.raises(FwoImporterError):
            import_state_controller.set_protocol_map()

        # Assert
        mock_logger.assert_called_once()
        assert import_state_controller.state.protocol_map == {}


class TestFwConfigImportObjectGetUserObjTypeMap:
    def test_get_userobj_type_map(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
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
        fwconfig_import_object.import_state.set_user_obj_type_map()

        # Assert
        assert fwconfig_import_object.import_state.state.user_obj_type_map == expected_userobj_type_map

    def test_get_userobj_type_map_with_exception(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            side_effect=Exception("Unexpected error occurred")
        )
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")

        # Act and Assert
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.import_state.set_user_obj_type_map()

        # Assert
        mock_logger.assert_called_once()
        assert fwconfig_import_object.import_state.state.user_obj_type_map == {}


class TestFwConfigImportObjectGetServiceObjTypeMap:
    def test_get_serviceobj_type_map(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
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
        fwconfig_import_object.import_state.set_service_obj_type_map()

        # Assert
        assert fwconfig_import_object.import_state.state.service_obj_type_map == expected_serviceobj_type_map

    def test_get_serviceobj_type_map_with_exception(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            side_effect=Exception("Unexpected error occurred")
        )
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")

        # Act and Assert
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.import_state.set_service_obj_type_map()

        # Assert
        mock_logger.assert_called_once()
        assert fwconfig_import_object.import_state.state.service_obj_type_map == {}


class TestFwConfigImportObjectGetNetworkObjTypeMap:
    def test_get_networkobj_type_map(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
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
        fwconfig_import_object.import_state.set_network_obj_type_map()

        # Assert
        assert fwconfig_import_object.import_state.state.network_obj_type_map == expected_networkobj_type_map

    def test_get_networkobj_type_map_with_exception(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            side_effect=Exception("Unexpected error occurred")
        )
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.import_state.set_network_obj_type_map()

        # Assert
        mock_logger.assert_called_once()


class TestFwConfigImportObjectLookupObjType:
    def test_lookup_obj_type_unknown(
        self,
        import_state_controller: ImportStateController,
    ):
        # Arrange
        obj_type_str = "some-obj-type"

        # Act and Assert
        with pytest.raises(FwoImporterError):
            import_state_controller.state.lookup_network_obj_type_id(obj_type_str)

    def test_lookup_obj_type_known(
        self,
        import_state_controller: ImportStateController,
    ):
        # Arrange
        obj_type_str = "imported"
        expected_obj_type = 2
        import_state_controller.state.network_obj_type_map = {
            "builtin": 1,
            "imported": 2,
            "custom": 3,
        }

        # Act
        obj_type = import_state_controller.state.lookup_network_obj_type_id(obj_type_str)

        # Assert
        assert obj_type == expected_obj_type


class TestUid2IdMapperFirewallSchemaMappings:
    def test_update_object_mappings_use_firewall_schema_response_keys(
        self,
        uid2id_mapper: Uid2IdMapper,
        api_connection: FwoApi,
        mocker: MockerFixture,
    ):
        responses = [
            {"data": {"firewall_nw_object": [{"obj_uid": "obj-uid", "obj_id": 101}]}},
            {"data": {"firewall_nw_service": [{"svc_uid": "svc-uid", "svc_id": 102}]}},
            {"data": {"firewall_nw_user": [{"user_uid": "user-uid", "user_id": 103}]}},
            {"data": {"firewall_zone": [{"zone_name": "zone-name", "zone_id": 104}]}},
        ]
        api_connection.call = mocker.Mock(side_effect=responses)

        uid2id_mapper.update_network_object_mapping(["obj-uid"])
        uid2id_mapper.update_service_object_mapping(["svc-uid"])
        uid2id_mapper.update_user_mapping(["user-uid"])
        uid2id_mapper.update_zone_mapping(["zone-name"])

        assert uid2id_mapper.get_network_object_id("obj-uid") == 101
        assert uid2id_mapper.get_service_object_id("svc-uid") == 102
        assert uid2id_mapper.get_user_id("user-uid") == 103
        assert uid2id_mapper.get_zone_object_id("zone-name") == 104

    def test_update_rulebase_mapping_uses_firewall_schema_response_key(
        self,
        uid2id_mapper: Uid2IdMapper,
        api_connection: FwoApi,
        mocker: MockerFixture,
    ):
        api_connection.call = mocker.Mock(
            return_value={"data": {"firewall_rulebase": [{"uid": "rb-uid", "id": 201}]}},
        )
        uid2id_mapper.update_rulebase_mapping = Uid2IdMapper.update_rulebase_mapping.__get__(
            uid2id_mapper, Uid2IdMapper
        )

        uid2id_mapper.update_rulebase_mapping(["rb-uid"])

        assert uid2id_mapper.get_rulebase_id("rb-uid") == 201

import unittest.mock

import pytest
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.fwconfig_import_ruleorder import RuleOrderService
from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import (
    ConnectionInfo,
    CredentialInfo,
    DeviceInfo,
    DomainInfo,
    ManagementController,
    ManagerInfo,
)
from models.fwconfig_normalized import FwConfigNormalized
from models.import_state import ImportState
from pytest_mock import MockerFixture
from services.enums import Lifetime, Services
from services.global_state import GlobalState
from services.group_flats_mapper import GroupFlatsMapper
from services.service_provider import ServiceProvider
from services.uid2id_mapper import Uid2IdMapper
from unit_tests.utils.config_builder import FwConfigBuilder


@pytest.fixture
def api_call(mocker: MockerFixture) -> FwoApiCall:
    fwo_api_call: FwoApiCall = unittest.mock.create_autospec(FwoApiCall)
    fwo_api_call.call = mocker.MagicMock()
    return fwo_api_call


@pytest.fixture
def api_connection(mocker: MockerFixture) -> FwoApi:
    fwo_api_connection: FwoApi = unittest.mock.create_autospec(FwoApi)
    fwo_api_connection.call = mocker.MagicMock()
    return fwo_api_connection


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

    import_state_controller: ImportStateController = unittest.mock.create_autospec(ImportStateController)
    import_state_controller.state = import_state
    import_state_controller.api_call = api_call
    import_state_controller.api_connection = api_connection

    return import_state_controller


@pytest.fixture
def group_flats_mapper() -> GroupFlatsMapper:
    group_flats_mapper = unittest.mock.create_autospec(GroupFlatsMapper)
    return group_flats_mapper


@pytest.fixture
def global_state(
    import_state_controller: ImportStateController,
) -> GlobalState:
    global_state = GlobalState(import_state_controller)
    global_state.normalized_config = FwConfigNormalized()
    global_state.global_normalized_config = FwConfigNormalized()
    global_state.previous_config = FwConfigNormalized()
    global_state.previous_global_config = FwConfigNormalized()
    return global_state


@pytest.fixture
def fwconfig_import_gateway() -> FwConfigImportGateway:
    return FwConfigImportGateway()


@pytest.fixture
def management_controller() -> ManagementController:
    mgm_controller = ManagementController(
        mgm_id=3,
        uid="mock-uid",
        devices=[],
        device_info=DeviceInfo(name="Mock Management", type_name="MockDevice", type_version="1.0"),
        connection_info=ConnectionInfo(hostname="mock.example.com", port=443),
        importer_hostname="mock-importer",
        credential_info=CredentialInfo(
            secret="mock-secret", import_user="mock-user", cloud_client_id="", cloud_client_secret=""
        ),
        manager_info=ManagerInfo(is_super_manager=False, sub_manager_ids=[], sub_managers=[]),
        domain_info=DomainInfo(domain_name="mock-domain", domain_uid="mock-domain-uid"),
        import_disabled=False,
    )
    return mgm_controller


@pytest.fixture
def fwconfig_builder() -> FwConfigBuilder:
    return FwConfigBuilder()


@pytest.fixture(autouse=True)
def service_provider(
    global_state: GlobalState,
    group_flats_mapper: GroupFlatsMapper,
) -> ServiceProvider:
    service_provider = ServiceProvider()
    service_provider.reset()

    service_provider.register(Services.GLOBAL_STATE, lambda: global_state, Lifetime.SINGLETON)
    service_provider.register(Services.RULE_ORDER_SERVICE, RuleOrderService, Lifetime.SINGLETON)
    service_provider.register(Services.GROUP_FLATS_MAPPER, lambda: group_flats_mapper, Lifetime.IMPORT)
    service_provider.register(Services.PREV_GROUP_FLATS_MAPPER, lambda: group_flats_mapper, Lifetime.IMPORT)
    service_provider.register(Services.UID2ID_MAPPER, Uid2IdMapper, Lifetime.IMPORT)
    service_provider.register(Services.RULE_ORDER_SERVICE, RuleOrderService, Lifetime.IMPORT)
    return service_provider


@pytest.fixture
def rule_order_service(
    service_provider: ServiceProvider,
) -> RuleOrderService:
    return service_provider.get_rule_order_service()


@pytest.fixture
def fwconfig_import_rule_mock() -> FwConfigImportRule:
    fw_config_import_rule: FwConfigImportRule = unittest.mock.create_autospec(FwConfigImportRule)
    return fw_config_import_rule


@pytest.fixture
def fwconfig_import_rule() -> FwConfigImportRule:
    fw_config_import_rule = FwConfigImportRule()
    fw_config_import_rule.create_new_rule_version = unittest.mock.MagicMock()
    return fw_config_import_rule


@pytest.fixture
def fwconfig_import_object() -> FwConfigImportObject:
    return FwConfigImportObject()


@pytest.fixture
def fw_config_import_object() -> FwConfigImportObject:
    fw_config_import_object: FwConfigImportObject = unittest.mock.create_autospec(FwConfigImportObject)
    fw_config_import_object.get_network_obj_type_map = unittest.mock.Mock(
        return_value={"network": 1, "group": 2, "host": 3, "machine_range": 4}
    )
    fw_config_import_object.get_service_obj_type_map = unittest.mock.Mock(
        return_value={"simple": 1, "group": 2, "rpc": 3}
    )
    fw_config_import_object.get_user_obj_type_map = unittest.mock.Mock(return_value={"group": 1, "simple": 2})
    return fw_config_import_object

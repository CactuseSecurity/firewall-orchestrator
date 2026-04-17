import unittest.mock

import pytest
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_const import FWO_CONFIG_FILENAME
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfig_import_rule import FwConfigImportRule
from model_controllers.rulebase_link_controller import RulebaseLinkController
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager
from models.fwo_config_controller import FwoConfigController
from pytest_mock import MockerFixture
from services.group_flats_mapper import GroupFlatsMapper
from services.stm_mapper import StmMapper
from services.uid2id_mapper import Uid2IdMapper
from states.global_state import GlobalState
from states.import_state import ImportState
from states.management_state import ManagementState
from test.utils.config_builder import FwConfigBuilder


@pytest.fixture
def api_call(mocker: MockerFixture, api_connection: FwoApi) -> FwoApiCall:
    fwo_api_call: FwoApiCall = unittest.mock.create_autospec(FwoApiCall)
    fwo_api_call.call = mocker.MagicMock()
    fwo_api_call.api = api_connection

    return fwo_api_call


@pytest.fixture
def api_connection(mocker: MockerFixture) -> FwoApi:
    fwo_api_connection: FwoApi = unittest.mock.create_autospec(FwoApi)
    fwo_api_connection.login = unittest.mock.Mock(return_value="blabla")
    fwo_api_connection.call = mocker.MagicMock()

    return fwo_api_connection


@pytest.fixture
def import_state(
    api_call: FwoApiCall,
    api_connection: FwoApi,
    mocker: MockerFixture,
) -> ImportState:
    super_management_id = 1
    mock_mgm = mocker.Mock(mgm_id=super_management_id, current_mgm_id=super_management_id)

    mocker.patch("states.import_state.ManagementController.get_mgm_details", return_value={})
    mocker.patch("states.import_state.ManagementController.from_json", return_value=mock_mgm)

    api_call.get_last_complete_import = mocker.Mock(return_value=(0, ""))
    api_call.get_config_value = mocker.Mock(return_value="30")

    import_state = ImportState(
        super_management_id,
        api_connection,
        api_call,
    )

    import_state.super_uid2id_mapper = Uid2IdMapper(api_connection)
    return import_state


@pytest.fixture
def group_flats_mapper() -> GroupFlatsMapper:
    return unittest.mock.create_autospec(GroupFlatsMapper)


@pytest.fixture
def stm_mapper() -> StmMapper:

    stm_mapper = StmMapper.__new__(StmMapper)
    stm_mapper.color_map = {"black": 1, "red": 2, "green": 3, "blue": 4}
    stm_mapper.network_obj_type_map = {"network": 1, "group": 2, "host": 3, "machine_range": 4}
    stm_mapper.service_obj_type_map = {"simple": 1, "group": 2, "rpc": 3}
    stm_mapper.user_obj_type_map = {"group": 1, "simple": 2}
    stm_mapper.link_types = {"ordered": 2, "inline": 3, "concatenated": 4, "domain": 5}
    stm_mapper.gateway_map = {3: {"mock-gateway-uid": 1}}
    stm_mapper.protocol_map = {"tcp": 6, "udp": 17, "icmp": 1}
    stm_mapper.actions = {"none": 1, "accept": 2}
    stm_mapper.tracks = {"none": 1, "log": 2}
    return stm_mapper


@pytest.fixture
def global_state(
    api_call: FwoApiCall,
    api_connection: FwoApi,
    stm_mapper: StmMapper,
) -> GlobalState:

    with unittest.mock.patch.object(
        FwoConfigController,
        "read_config",
        return_value=(
            "https://middleware.local",
            "https://api.local",
            "importer",
            "test-importer-pwd",
            0,
            150,
            90,
        ),
    ):
        GlobalState.login_to_api = unittest.mock.Mock(return_value=None)
        GlobalState.__init__ = unittest.mock.Mock(return_value=None)
        global_state = GlobalState(FWO_CONFIG_FILENAME, force=True, clear=False, debug_level=0)
        global_state.fwo_config_controller = FwoConfigController(
            FWO_CONFIG_FILENAME, force=True, clear=False, debug_level=0
        )
        global_state.fwo_api = api_connection
        global_state.fwo_api_call = api_call
        global_state.stm_mapper = stm_mapper
        return global_state


@pytest.fixture
def management_state(
    import_state: ImportState,
    api_connection: FwoApi,
) -> ManagementState:
    return ManagementState(import_state, api_connection, 1, FwConfigNormalized(), is_super_manager=True)


@pytest.fixture
def fwconfig_import_gateway() -> FwConfigImportGateway:
    return FwConfigImportGateway()


@pytest.fixture
def fwconfig_builder() -> FwConfigBuilder:
    return FwConfigBuilder()


@pytest.fixture
def fwconfig_import_rule_mock() -> FwConfigImportRule:
    fw_config_import_rule: FwConfigImportRule = unittest.mock.create_autospec(FwConfigImportRule)
    return fw_config_import_rule


@pytest.fixture
def fwconfig_import_rule(
    import_state: ImportState,
    management_state: ManagementState,
    global_state: GlobalState,
) -> FwConfigImportRule:
    return FwConfigImportRule(
        import_state=import_state,
        management_state=management_state,
        global_state=global_state,
    )


@pytest.fixture
def fwconfig_import_object() -> FwConfigImportObject:
    return FwConfigImportObject()


@pytest.fixture
def fw_config_manager() -> FwConfigManager:
    return FwConfigManager(
        manager_uid="mock-manager-uid",
        manager_name="Mock Manager",
        is_super_manager=False,
        domain_uid="mock-domain-uid",
        domain_name="Mock Domain",
        sub_manager_ids=[],
        configs=[],
    )


@pytest.fixture
def uid2id_mapper(import_state: ImportState, api_connection: FwoApi) -> Uid2IdMapper:
    return (
        import_state.super_uid2id_mapper
        if import_state.super_uid2id_mapper is not None
        else Uid2IdMapper(api_connection)
    )


@pytest.fixture
def rb_link_controller() -> RulebaseLinkController:
    return RulebaseLinkController()

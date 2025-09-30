from test.mocking.mock_config import MockFwConfigNormalizedBuilder
from fwo_globals import set_global_values
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.fwconfigmanager import FwConfigManager
from test.mocking.mock_import_state import MockImportStateController


def main(
    rule_config=(10, 10, 10),
    network_object_config=10,
    service_config=10,
    user_config=10,
    gateway_uid="cbdd1e35-b6e9-4ead-b13f-fd6389e34987",
    gateway_name="sting-gw",
    manager_uid="6ae3760206b9bfbd2282b5964f6ea07869374f427533c72faa7418c28f7a77f2",
    manager_name="sting-mgmt",
    domain_uid="domain uid",
    domain_name="domain name",
    debug_level=8,
):
    mock_config_builder = MockFwConfigNormalizedBuilder()
    mock_config, _ = mock_config_builder.build_config(
        {
            "rule_config": list(rule_config),
            "network_object_config": network_object_config,
            "service_config": service_config,
            "user_config": user_config,
            "gateway_uid": gateway_uid,
            "gateway_name": gateway_name,
        }
    )

    fw_mock_import_state = MockImportStateController(stub_setCoreData=True)
    set_global_values(debug_level_in=debug_level)

    fw_config_manager_list_controller = FwConfigManagerListController()
    fw_config_manager = FwConfigManager(
        ManagerUid=manager_uid,
        ManagerName=manager_name,
        DomainUid=domain_uid,
        DomainName=domain_name

    )
    fw_config_manager.Configs.append(mock_config)
    fw_config_manager_list_controller.ManagerSet.append(fw_config_manager)
    file_path = fw_config_manager_list_controller.storeFullNormalizedConfigToFile(fw_mock_import_state)

    print(f"MockConfig: File saved to '{file_path}'")

if __name__ == "__main__":
    main()


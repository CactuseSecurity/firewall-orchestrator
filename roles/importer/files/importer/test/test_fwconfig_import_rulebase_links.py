from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from model_controllers.fwconfig_import import FwConfigImport
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized
from models.gateway import Gateway
from pytest_mock import MockerFixture


def test_fix_rulebase_links_in_db_uses_firewall_rulebase_link_result(
    import_state_controller: ImportStateController,
    api_call: FwoApiCall,
    mocker: MockerFixture,
):
    importer = FwConfigImport()
    graphql_mock = mocker.patch.object(FwoApi, "get_graphql_code", return_value="mutation")
    api_call.call = mocker.Mock(
        return_value={"data": {"update_firewall_rulebase_link": {"affected_rows": 0}}},
    )

    importer.fix_rulebase_links_in_db(FwConfigNormalized())

    graphql_mock.assert_called_once()
    api_call.call.assert_called_once_with(
        "mutation",
        query_variables={
            "mgmId": import_state_controller.state.mgm_details.current_mgm_id,
            "importId": import_state_controller.state.import_id,
        },
    )
    assert import_state_controller.state.stats.statistics.inconsistent_rulebase_link_delete_count == 0


def test_fix_rulebase_links_in_db_refreshes_previous_config_after_removal(
    import_state_controller: ImportStateController,
    api_call: FwoApiCall,
    mocker: MockerFixture,
):
    importer = FwConfigImport()
    previous_config = FwConfigNormalized(gateways=[Gateway(Uid="gw-uid")])
    mocker.patch.object(FwoApi, "get_graphql_code", side_effect=["mutation", "query"])
    api_call.call = mocker.Mock(
        side_effect=[
            {"data": {"update_firewall_rulebase_link": {"affected_rows": 1}}},
            {
                "data": {
                    "device": [
                        {
                            "dev_uid": "gw-uid",
                            "rulebase_links": [
                                {
                                    "rulebaseByFromRulebaseId": {"uid": "parent-rb"},
                                    "rule": {"rule_uid": "rule-uid"},
                                    "rulebase": {"uid": "child-rb"},
                                    "stm_link_type": {"name": "section"},
                                    "is_initial": False,
                                    "is_global": True,
                                    "is_section": True,
                                },
                            ],
                        },
                    ],
                },
            },
        ],
    )

    importer.fix_rulebase_links_in_db(previous_config)

    assert import_state_controller.state.stats.statistics.inconsistent_rulebase_link_delete_count == 1
    assert len(previous_config.gateways[0].RulebaseLinks) == 1
    refreshed_link = previous_config.gateways[0].RulebaseLinks[0]
    assert refreshed_link.from_rulebase_uid == "parent-rb"
    assert refreshed_link.from_rule_uid == "rule-uid"
    assert refreshed_link.to_rulebase_uid == "child-rb"
    assert refreshed_link.link_type == "section"
    assert refreshed_link.is_global is True

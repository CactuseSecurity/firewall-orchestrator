from fw_modules.opnsense25ff.opnsense_helper import enrich_opnsense_net_and_hosts
from fw_modules.opnsense25ff.opnsense_model import (
    FilterRuleActionEnum,
    OPNsenseAccessRule,
    OPNsenseConfig,
    OPNsenseHost,
    OPNsenseHostAlias,
    OPNsenseIfGroup,
    OPNsenseNetwork,
)
from fw_modules.opnsense25ff.opnsense_normalizer import (
    _create_rulebases_from_access_rules,  # pyright: ignore[reportPrivateUsage]
)
from fwo_const import RULE_NUM_NUMERIC_STEPS
from model_controllers.fwconfig_import_ruleorder import update_rule_order_diffs
from models.fwconfig_normalized import FwConfigNormalized


def test_enrich_opnsense_net_and_hosts_keeps_child_types() -> None:
    alias = OPNsenseHostAlias.model_validate(
        {
            "@uuid": "alias-hosts-uid",
            "enabled": True,
            "name": "alias-hosts",
            "content": "192.0.2.1\n192.0.2.5-192.0.2.6\n192.0.2.0/24\nexternal-ref",
            "description": "test hosts",
        }
    )
    config = OPNsenseConfig(hostname="opnsense-test", host_aliases={alias.name: alias})

    enrich_opnsense_net_and_hosts(config)

    child_types = [type(child) for child in alias.childs]
    assert child_types == [OPNsenseHost, OPNsenseHost, OPNsenseNetwork, str]
    first_host, second_host, network, ref = alias.childs
    assert isinstance(first_host, OPNsenseHost)
    assert str(first_host.host) == "192.0.2.1"
    assert isinstance(second_host, OPNsenseHost)
    assert second_host.is_range is True
    assert isinstance(network, OPNsenseNetwork)
    assert str(network.net) == "192.0.2.0/24"
    assert ref == "external-ref"


def test_create_rulebases_from_access_rules_groups_expected_rules() -> None:
    interface_group = OPNsenseIfGroup.model_validate(
        {
            "@uuid": "ifgroup-lan-uid",
            "ifname": "lan_group",
            "members": "lan,opt1",
            "descr": "LAN group",
        }
    )
    floating_rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "floating-rule-uid",
            "floating": "yes",
            "interface": ["wan"],
            "type": FilterRuleActionEnum.PASS,
            "descr": "floating rule",
        }
    )
    unmatched_rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "unmatched-rule-uid",
            "interface": ["wan"],
            "type": FilterRuleActionEnum.PASS,
            "descr": "unmatched interface rule",
        }
    )
    grouped_rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "grouped-rule-uid",
            "interface": [interface_group.name],
            "type": FilterRuleActionEnum.PASS,
            "descr": "grouped interface rule",
        }
    )
    second_grouped_rule = OPNsenseAccessRule.model_validate(
        {
            "@uuid": "second-grouped-rule-uid",
            "interface": [interface_group.name],
            "type": FilterRuleActionEnum.PASS,
            "descr": "second grouped interface rule",
        }
    )
    config = OPNsenseConfig(
        hostname="opnsense-test",
        interface_groups={interface_group.name: interface_group},
        access_rules=[floating_rule, unmatched_rule, grouped_rule, second_grouped_rule],
    )

    rulebase_list = _create_rulebases_from_access_rules(config, "mgm-uid")
    rulebases = {rulebase.name: rulebase for rulebase in rulebase_list}

    assert set(rulebases) == {"floating", "lan_group"}
    assert set(rulebases["floating"].rules) == {"floating-rule-uid"}
    assert set(rulebases["lan_group"].rules) == {"grouped-rule-uid", "second-grouped-rule-uid"}
    assert rulebases["floating"].rules["floating-rule-uid"].rule_num == 0
    assert rulebases["floating"].rules["floating-rule-uid"].rule_num_numeric == 0
    assert rulebases["lan_group"].rules["grouped-rule-uid"].rule_num == 0
    assert rulebases["lan_group"].rules["grouped-rule-uid"].rule_num_numeric == 0

    normalized_config = FwConfigNormalized(rulebases=rulebase_list)
    update_rule_order_diffs(FwConfigNormalized(), normalized_config)

    assert rulebases["floating"].rules["floating-rule-uid"].rule_num_numeric == RULE_NUM_NUMERIC_STEPS
    assert rulebases["lan_group"].rules["grouped-rule-uid"].rule_num_numeric == RULE_NUM_NUMERIC_STEPS
    assert rulebases["lan_group"].rules["second-grouped-rule-uid"].rule_num_numeric == 2 * RULE_NUM_NUMERIC_STEPS

from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig


def test_rules_dict_coerces_to_list():
    config = FortiOSConfig.model_validate(
        {
            "rules": {
                "rules": [
                    {
                        "policyid": 1,
                        "name": "rule1",
                        "action": "accept",
                        "srcintf": [{"name": "port1", "q_origin_key": "port1"}],
                        "dstintf": [{"name": "port2", "q_origin_key": "port2"}],
                        "srcaddr": [{"name": "all", "q_origin_key": "all"}],
                        "dstaddr": [{"name": "all", "q_origin_key": "all"}],
                        "service": [{"name": "ALL", "q_origin_key": "ALL"}],
                        "schedule": "always",
                        "status": "enable",
                        "logtraffic": "all",
                        "q_origin_key": 1,
                        "uuid": "uuid-rule1",
                    }
                ]
            }
        }
    )

    assert len(config.rules) == 1

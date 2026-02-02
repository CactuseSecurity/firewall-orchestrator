from fw_modules.fortiosmanagementREST.fos_models import NwObjAddress


def test_subnet_list_coerces_to_string():
    obj = NwObjAddress.model_validate(
        {
            "name": "net1",
            "q_origin_key": "net1",
            "uuid": "uuid1",
            "type": "ipmask",
            "subnet": ["1.2.3.4", 32],
        }
    )

    assert obj.subnet == "1.2.3.4 32"

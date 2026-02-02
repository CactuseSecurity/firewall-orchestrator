from fw_modules.checkpointR8x import cp_service


def test_get_protocol_number_handles_string_protocol():
    obj = {"ip-protocol": "17"}

    assert cp_service._get_protocol_number(obj) == 17


def test_get_rpc_number_stringifies_program_number():
    obj = {"program-number": 100235}

    assert cp_service._get_rpc_number(obj) == "100235"

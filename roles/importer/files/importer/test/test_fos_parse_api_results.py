from typing import Any

import pytest
from fw_modules.fortiosmanagementREST.fos_getter import parse_api_results
from fw_modules.fortiosmanagementREST.fos_models import NwObjAddress
from pydantic import ValidationError

DUMMY_ADDRESSES: list[dict[str, Any]] = [
    {
        "name": "host_web01",
        "q_origin_key": "host_web01",
        "uuid": "11111111-1111-1111-1111-111111111111",
        "type": "ipmask",
        "subnet": "10.0.0.10 255.255.255.255",
    },
    {
        "name": "range_dhcp",
        "q_origin_key": "range_dhcp",
        "uuid": "22222222-2222-2222-2222-222222222222",
        "type": "iprange",
        "start-ip": "10.0.1.100",
        "end-ip": "10.0.1.200",
    },
]


def test_parse_api_results_returns_model_instances():
    result = parse_api_results(NwObjAddress, DUMMY_ADDRESSES)

    assert len(result) == len(DUMMY_ADDRESSES)
    assert all(isinstance(address, NwObjAddress) for address in result)
    assert result[0].name == "host_web01"
    assert result[0].subnet == "10.0.0.10 255.255.255.255"


def test_parse_api_results_resolves_field_aliases():
    result = parse_api_results(NwObjAddress, DUMMY_ADDRESSES)

    assert result[1].start_ip == "10.0.1.100"
    assert result[1].end_ip == "10.0.1.200"


def test_parse_api_results_empty_list():
    assert parse_api_results(NwObjAddress, []) == []


def test_parse_api_results_rejects_invalid_data():
    invalid_address = {**DUMMY_ADDRESSES[0], "type": "not-a-valid-type"}

    with pytest.raises(ValidationError):
        parse_api_results(NwObjAddress, [invalid_address])

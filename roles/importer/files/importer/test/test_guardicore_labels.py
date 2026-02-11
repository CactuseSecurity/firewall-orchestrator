import json
import sys
from pathlib import Path
from typing import Any

for parent in Path(__file__).resolve().parents:
    if (parent / "scripts").is_dir():
        sys.path.insert(0, str(parent))
        break

from scripts.customizing.provisioning.guardicore.create_guardicore_labels import (
    build_graphql_query,
    build_graphql_variables,
    build_labels_from_response,
    criteria_from_network,
    parse_app_ids,
    parse_group_types,
    to_guardicore_payload,
)

SAMPLE_RESPONSE: dict[str, Any] = {
    "data": {
        "owner": [
            {
                "app_id_external": "COM-1617",
                "name": "Netzwerk - NTP",
                "common_service_possible": True,
                "nwgroups": [
                    {
                        "name": "test4443212",
                        "id_string": "AR0011617-006",
                        "app_id": 369,
                        "nwobject_nwgroups": [
                            {
                                "owner_network": {
                                    "name": "host_3.4.5.7",
                                    "ip": "3.4.5.7/32",
                                    "ip_end": "3.4.5.7/32",
                                }
                            },
                            {
                                "owner_network": {
                                    "name": "netz_17.188.192.16/29",
                                    "ip": "17.188.192.16/32",
                                    "ip_end": "17.188.192.23/32",
                                }
                            },
                        ],
                    },
                    {
                        "name": "",
                        "id_string": "AZ11617",
                        "app_id": 369,
                        "nwobject_nwgroups": [],
                    },
                ],
            }
        ]
    }
}


def test_criteria_from_network_subnet():
    criteria = criteria_from_network("3.4.5.7/32", "3.4.5.7/32")
    assert criteria is not None
    assert criteria.op == "SUBNET"
    assert criteria.argument == "3.4.5.7/32"


def test_criteria_from_network_range():
    criteria = criteria_from_network("17.188.192.16/32", "17.188.192.23/32")
    assert criteria is not None
    assert criteria.op == "RANGE"
    assert criteria.argument == "17.188.192.16-17.188.192.23"


def test_build_labels_from_response_skips_empty_by_default():
    labels = build_labels_from_response(json.loads(json.dumps(SAMPLE_RESPONSE)))
    assert len(labels) == 1
    assert labels[0].key == "AppRole"
    assert labels[0].value == "AR0011617-006"


def test_build_labels_from_response_includes_empty_when_requested():
    labels = build_labels_from_response(
        json.loads(json.dumps(SAMPLE_RESPONSE)),
        include_empty=True,
    )
    assert len(labels) == 2
    values = {label.value for label in labels}
    assert "AZ11617" in values


def test_to_guardicore_payload():
    labels = build_labels_from_response(json.loads(json.dumps(SAMPLE_RESPONSE)))
    payload = to_guardicore_payload(labels)
    assert payload[0]["key"] == "AppRole"
    assert payload[0]["value"] == "AR0011617-006"
    assert payload[0]["criteria"][0]["field"] == "numeric_ip_addresses"


def test_parse_app_ids():
    app_ids = parse_app_ids('["APP-1234","APP-2345"]')
    assert app_ids == ["APP-1234", "APP-2345"]


def test_parse_group_types():
    group_types = parse_group_types("[20,21]")
    assert group_types == [20, 21]


def test_build_graphql_query_without_app_filter():
    query = build_graphql_query()
    assert "query getARsAndAZs($ownerFilter: owner_bool_exp!)" in query
    assert "$ownerFilter" in query


def test_build_graphql_query_with_app_filter():
    query = build_graphql_query()
    assert "$ownerFilter" in query


def test_build_graphql_variables_without_app_filter():
    variables = build_graphql_variables()
    assert variables == {
        "ownerFilter": {
            "_or": [
                {"_and": [{"nwgroups": {"group_type": {"_in": [20, 21]}}}]},
            ]
        }
    }


def test_build_graphql_variables_with_app_filter():
    variables = build_graphql_variables(["APP-1234", "APP-2345"])
    assert variables == {
        "ownerFilter": {
            "_or": [
                {
                    "_and": [
                        {"nwgroups": {"group_type": {"_in": [20, 21]}}},
                        {"app_id_external": {"_in": ["APP-1234", "APP-2345"]}},
                    ]
                },
            ]
        }
    }


def test_build_graphql_variables_include_common_services():
    variables = build_graphql_variables(include_common_services=True)
    assert variables == {
        "ownerFilter": {
            "_or": [
                {"_and": [{"nwgroups": {"group_type": {"_in": [20, 21]}}}]},
                {"common_service_possible": {"_eq": True}},
            ]
        }
    }


def test_build_graphql_variables_with_custom_group_types():
    variables = build_graphql_variables(include_group_types=[22])
    assert variables == {
        "ownerFilter": {
            "_or": [
                {"_and": [{"nwgroups": {"group_type": {"_in": [22]}}}]},
            ]
        }
    }

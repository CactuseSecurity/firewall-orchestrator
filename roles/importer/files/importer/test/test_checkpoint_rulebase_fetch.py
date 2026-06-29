from unittest.mock import patch

from fw_modules.checkpointR8x import cp_getter


def test_get_rulebases_in_chunks_merges_sections_split_across_api_pages():
    first_chunk = {
        "uid": "rb-uid",
        "name": "Layer 1",
        "total": 502,
        "from": 1,
        "to": 500,
        "rulebase": [
            {
                "type": "access-section",
                "uid": "section-uid",
                "name": "Section 408-502",
                "from": 408,
                "to": 500,
                "rulebase": [{"uid": f"rule-{rule_number}", "type": "access-rule"} for rule_number in range(408, 501)],
            }
        ],
    }
    second_chunk = {
        "uid": "rb-uid",
        "name": "Layer 1",
        "total": 502,
        "from": 501,
        "to": 502,
        "rulebase": [
            {
                "type": "access-section",
                "uid": "section-uid",
                "name": "Section 408-502",
                "from": 501,
                "to": 502,
                "rulebase": [
                    {"uid": "rule-501", "type": "access-rule"},
                    {"uid": "rule-502", "type": "access-rule"},
                ],
            }
        ],
    }
    with (
        patch.object(cp_getter, "cp_api_call", side_effect=[first_chunk, second_chunk]) as cp_api_call,
        patch.object(cp_getter, "resolve_ref_list_from_object_dictionary"),
    ):
        rulebase = cp_getter.get_rulebases_in_chunks(
            "rb-uid",
            {"limit": 500},
            "https://example.invalid/",
            "access",
            "sid",
            {"objects": []},
        )

    assert cp_api_call.call_count == 2
    assert len(rulebase["chunks"]) == 1
    merged_section = rulebase["chunks"][0]["rulebase"][0]
    assert merged_section["uid"] == "section-uid"
    assert merged_section["from"] == 408
    assert merged_section["to"] == 502
    assert len(merged_section["rulebase"]) == 95
    assert [rule["uid"] for rule in merged_section["rulebase"][-2:]] == ["rule-501", "rule-502"]

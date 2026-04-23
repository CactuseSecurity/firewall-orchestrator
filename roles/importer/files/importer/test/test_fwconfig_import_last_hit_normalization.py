from fwo_base import find_all_diffs
from model_controllers.fwconfig_import import FwConfigImport


class TestFwconfigImportLastHitNormalization:
    def test_normalize_last_hit_converts_supported_formats(self):
        importer = FwConfigImport()
        config_dict = {
            "rulebases": [
                {
                    "rules": {
                        "rule1": {"last_hit": "2025-11-19 16:01:08"},
                        "rule2": {"last_hit": "2025-11-19T16:01+0100"},
                        "rule3": {"last_hit": "2025-11-19T16:01:59+0100"},
                        "rule4": {"last_hit": None},
                    }
                }
            ]
        }

        importer._normalize_last_hit_for_consistency_diff(config_dict)  # pyright: ignore[reportPrivateUsage]

        rules = config_dict["rulebases"][0]["rules"]
        assert rules["rule1"]["last_hit"] == "2025-11-19T16:01"
        assert rules["rule2"]["last_hit"] == "2025-11-19T16:01"
        assert rules["rule3"]["last_hit"] == "2025-11-19T16:01"
        assert rules["rule4"]["last_hit"] is None

    def test_normalize_last_hit_keeps_unknown_format_unchanged(self):
        importer = FwConfigImport()
        config_dict = {"rulebases": [{"rules": {"rule1": {"last_hit": "unexpected-format"}}}]}

        importer._normalize_last_hit_for_consistency_diff(config_dict)  # pyright: ignore[reportPrivateUsage]

        rules = config_dict["rulebases"][0]["rules"]
        assert rules["rule1"]["last_hit"] == "unexpected-format"

    def test_strict_diff_is_equal_after_last_hit_normalization(self):
        importer = FwConfigImport()
        normalized_config_dict = {"rulebases": [{"rules": {"rule1": {"last_hit": "2025-11-19T16:01+0100"}}}]}
        normalized_config_from_db_dict = {"rulebases": [{"rules": {"rule1": {"last_hit": "2025-11-19 16:01:08"}}}]}

        importer._normalize_last_hit_for_consistency_diff(normalized_config_dict)  # pyright: ignore[reportPrivateUsage]
        importer._normalize_last_hit_for_consistency_diff(normalized_config_from_db_dict)  # pyright: ignore[reportPrivateUsage]
        all_diffs = find_all_diffs(normalized_config_dict, normalized_config_from_db_dict, strict=True)

        assert all_diffs == []

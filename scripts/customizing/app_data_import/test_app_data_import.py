import argparse
import logging
import sys
import tempfile
import unittest
from pathlib import Path

import pytest

from scripts.customizing.app_data_import.get_owner_data3_from_normalized_csvs import (
    apply_owner_column_overrides,
    build_git_repo_url,
    normalize_option_value_args,
    parse_criticality_recert_period_mapping,
    parse_included_owners_filters,
    parse_responsibles_columns,
)
from scripts.customizing.fwo_custom_lib.app_data_models import Appip, Owner
from scripts.customizing.fwo_custom_lib.basic_helpers import (
    read_custom_config,
    read_custom_config_with_default,
)
from scripts.customizing.fwo_custom_lib.read_app_data_csv import (
    ExtractAppDataCsvOptions,
    extract_app_data_from_csv,
    extract_ip_data_from_csv,
    parse_csv_separator_arg,
)

# Make module directory importable
MODULE_DIR: Path = Path(__file__).resolve().parent
sys.path.insert(0, str(MODULE_DIR))
# Also add shared library folder for imports when running via test discovery
sys.path.insert(0, str(MODULE_DIR.parent / "fwo_custom_lib"))


class AppDataImportTests(unittest.TestCase):
    def setUp(self) -> None:
        self.logger: logging.Logger = logging.getLogger("app-data-import-tests")
        self.logger.addHandler(logging.NullHandler())
        self.ldap_path: str = "CN={USERID}"
        self.import_source: str = "testsource"
        self.debug_level: int = 0

    def test_extract_app_data_from_csv_builds_owner(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA\nMy App,APP-001,user1,false\n")

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
            )

            self.assertEqual(len(app_list), 1)
            owner: Owner = app_list[0]
            self.assertEqual(owner.name, "My App")
            self.assertEqual(owner.app_id_external, "APP-001")
            self.assertEqual(owner.main_user, "")
            self.assertEqual(owner.recert_period_days, 365)
            self.assertEqual(owner.import_source, self.import_source)
            self.assertEqual(owner.owner_lifecycle_state, "unknown")
            self.assertNotIn("criticality", owner.to_json())

    def test_extract_app_data_from_csv_supports_semicolon_separator(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Name;col: Alfabet-ID;bogus: TISO;bogus: kwITA\nMy App;APP-001;user1;false\n")

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                csv_separator=";",
            )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].app_id_external, "APP-001")

    def test_extract_app_data_from_csv_reads_cp1252_encoded_file(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            owner_csv_content: str = (
                "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA\nMärchen App,APP-001,user1,false\n"
            )
            owner_csv_path.write_bytes(owner_csv_content.encode("cp1252"))

            app_list: list[Owner] = []
            with self.assertLogs("app-data-import-tests", level="WARNING") as log_context:
                extract_app_data_from_csv(
                    "owners.csv",
                    app_list,
                    self.ldap_path,
                    self.import_source,
                    Owner,
                    self.logger,
                    self.debug_level,
                    base_dir=tmpdir,
                )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].name, "Märchen App")
            self.assertTrue(any("fallback encoding cp1252" in message for message in log_context.output))

    def test_extract_app_data_from_csv_accepts_options_object(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA\nMy App,APP-001,user1,false\n")

            app_list: list[Owner] = []
            options: ExtractAppDataCsvOptions = ExtractAppDataCsvOptions(base_dir=tmpdir)
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                options=options,
            )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].app_id_external, "APP-001")

    def test_extract_ip_data_from_csv_adds_app_server(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            ip_csv_path: Path = Path(tmpdir) / "ips.csv"
            with open(ip_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Alfabet-ID,col: IP\nAPP-001,10.0.0.1\n")

            owner: Owner = Owner("My App", "APP-001", "CN=user1", 365, 365, import_source=self.import_source)
            app_dict: dict[str, Owner] = {"APP-001": owner}

            extract_ip_data_from_csv(
                "ips.csv",
                app_dict,
                Appip,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
            )

            self.assertEqual(len(owner.app_servers), 1)
            app_server: Appip = owner.app_servers[0]
            self.assertEqual(app_server.name, "host_10.0.0.1")
            self.assertEqual(str(app_server.ip_start), "10.0.0.1")
            self.assertEqual(str(app_server.ip_end), "10.0.0.1")
            self.assertEqual(app_server.type, "host")
            self.assertEqual(app_server.app_id_external, "APP-001")

    def test_extract_ip_data_from_csv_supports_semicolon_separator(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            ip_csv_path: Path = Path(tmpdir) / "ips.csv"
            with open(ip_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Alfabet-ID;col: IP\nAPP-001;10.0.0.1\n")

            owner: Owner = Owner("My App", "APP-001", "CN=user1", 365, 365, import_source=self.import_source)
            app_dict: dict[str, Owner] = {"APP-001": owner}

            extract_ip_data_from_csv(
                "ips.csv",
                app_dict,
                Appip,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                csv_separator=";",
            )

            self.assertEqual(len(owner.app_servers), 1)
            self.assertEqual(str(owner.app_servers[0].ip_start), "10.0.0.1")

    def test_extract_ip_data_from_csv_reads_cp1252_encoded_file(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            ip_csv_path: Path = Path(tmpdir) / "ips.csv"
            ip_csv_content: str = "col: Alfabet-ID,col: IP,Kommentar\nAPP-001,10.0.0.1,ä\n"
            ip_csv_path.write_bytes(ip_csv_content.encode("cp1252"))

            owner: Owner = Owner("My App", "APP-001", "CN=user1", 365, 365, import_source=self.import_source)
            app_dict: dict[str, Owner] = {"APP-001": owner}

            with self.assertLogs("app-data-import-tests", level="WARNING") as log_context:
                extract_ip_data_from_csv(
                    "ips.csv",
                    app_dict,
                    Appip,
                    self.logger,
                    self.debug_level,
                    base_dir=tmpdir,
                )

            self.assertEqual(len(owner.app_servers), 1)
            self.assertTrue(any("fallback encoding cp1252" in message for message in log_context.output))

    def test_extract_app_data_from_csv_allows_custom_header_patterns(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "Application Name,Application Identifier,Main Owner,Recert Active\n"
                    "My Other App,APP-002,user2,true\n"
                )

            app_list: list[Owner] = []
            header_patterns: dict[str, str] = {
                "name": r"Application Name",
                "app_id": r"Application Identifier",
                "owner_kwita": r"Recert Active",
            }
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                column_patterns=header_patterns,
            )

            owner: Owner = app_list[0]
            self.assertEqual(owner.name, "My Other App")
            self.assertEqual(owner.app_id_external, "APP-002")
            self.assertEqual(owner.main_user, "")
            self.assertEqual(owner.recert_period_days, 182)

    def test_extract_app_data_from_csv_applies_default_recert_active_state(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA\nMy App,APP-004,user4,false\n")

            app_list_true: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list_true,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                default_recert_active_state=True,
            )
            self.assertTrue(app_list_true[0].recert_active)

            app_list_false: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list_false,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                default_recert_active_state=False,
            )
            self.assertFalse(app_list_false[0].recert_active)

    def test_extract_ip_data_from_csv_allows_custom_header_patterns(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            ip_csv_path: Path = Path(tmpdir) / "ips.csv"
            with open(ip_csv_path, "w", encoding="utf-8") as fh:
                fh.write("Identifier,IP Address\nAPP-003,10.0.0.0/30\n")

            owner: Owner = Owner("My App", "APP-003", "CN=user1", 365, 365, import_source=self.import_source)
            app_dict: dict[str, Owner] = {"APP-003": owner}
            header_patterns: dict[str, str] = {"app_id": r"Identifier", "ip": r"IP Address"}

            extract_ip_data_from_csv(
                "ips.csv",
                app_dict,
                Appip,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                column_patterns=header_patterns,
            )

            app_server: Appip = owner.app_servers[0]
            self.assertEqual(app_server.name, "network_10.0.0.0_30")
            self.assertEqual(str(app_server.ip_start), "10.0.0.0")
            self.assertEqual(str(app_server.ip_end), "10.0.0.3")
            self.assertEqual(app_server.type, "network")

    def test_extract_app_data_from_csv_reads_owner_lifecycle_state(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA,Lifecycle State\n"
                    "App Lifecycle,APP-012,user12,false,active\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
            )

            owner: Owner = app_list[0]
            self.assertEqual(owner.owner_lifecycle_state, "active")
            self.assertEqual(owner.to_json()["owner_lifecycle_state"], "active")

    def test_extract_app_data_from_csv_uses_fallback_owner_lifecycle_without_column(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA\nFallback Lifecycle,APP-012,user12,false\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                column_patterns=apply_owner_column_overrides({}, ""),
                fallback_owner_lifecycle="retired",
            )

            owner: Owner = app_list[0]
            self.assertEqual(owner.owner_lifecycle_state, "retired")
            self.assertEqual(owner.to_json()["owner_lifecycle_state"], "retired")

    def test_extract_app_data_from_csv_matches_valid_app_id_prefix_case_insensitively(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA\nApp One,APP-012,user12,false\n")

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                valid_app_id_prefixes=["ApP-"],
            )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].app_id_external, "APP-012")

    def test_extract_app_data_from_csv_filters_by_included_owners_column(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA,Aktive Firewallregel\n"
                    "App One,APP-005,user5,false,Ja\n"
                    "App Two,APP-006,user6,false,Nein\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                included_owners_column="Aktive Firewallregel",
                include_values=["Ja"],
            )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].app_id_external, "APP-005")

    def test_extract_app_data_from_csv_filters_by_multiple_include_values(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA,Aktive Firewallregel\n"
                    "App One,APP-007,user7,false,Ja\n"
                    "App Two,APP-008,user8,false,Ausnahme\n"
                    "App Three,APP-009,user9,false,Nein\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                included_owners_column="Aktive Firewallregel",
                include_values=["Ja", "Ausnahme"],
            )

            self.assertEqual(len(app_list), 2)
            self.assertEqual({owner.app_id_external for owner in app_list}, {"APP-007", "APP-008"})

    def test_extract_app_data_from_csv_ignores_filter_if_column_missing(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA\n"
                    "App One,APP-010,user10,false\n"
                    "App Two,APP-011,user11,false\n"
                )

            app_list: list[Owner] = []
            with self.assertLogs("app-data-import-tests", level="WARNING") as log_context:
                extract_app_data_from_csv(
                    "owners.csv",
                    app_list,
                    self.ldap_path,
                    self.import_source,
                    Owner,
                    self.logger,
                    self.debug_level,
                    base_dir=tmpdir,
                    included_owners_column="Aktive Firewallregel",
                    include_values=["Ja"],
                )

            self.assertEqual(len(app_list), 2)
            self.assertTrue(any("optional filter column" in message for message in log_context.output))

    def test_extract_app_data_from_csv_filters_by_multiple_columns(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA,Aktive Firewallregel,Importieren\n"
                    "App One,APP-012,user12,false,Ja,Ja\n"
                    "App Two,APP-013,user13,false,Ja,Nein\n"
                    "App Three,APP-014,user14,false,Nein,Ja\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                included_owners_filters={
                    "Aktive Firewallregel": ("Ja",),
                    "Importieren": ("Ja",),
                },
            )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].app_id_external, "APP-012")

    def test_extract_app_data_from_csv_skips_file_if_required_column_missing(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Name,bogus: TISO,bogus: kwITA\nApp One,user10,false\n")

            app_list: list[Owner] = []
            with self.assertLogs("app-data-import-tests", level="WARNING") as log_context:
                extract_app_data_from_csv(
                    "owners.csv",
                    app_list,
                    self.ldap_path,
                    self.import_source,
                    Owner,
                    self.logger,
                    self.debug_level,
                    base_dir=tmpdir,
                )

            self.assertEqual(len(app_list), 0)
            self.assertTrue(any("skipping csv file" in message for message in log_context.output))

    def test_extract_ip_data_from_csv_skips_file_if_required_column_missing(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            ip_csv_path: Path = Path(tmpdir) / "ips.csv"
            with open(ip_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Alfabet-ID\nAPP-001\n")

            owner: Owner = Owner("My App", "APP-001", "CN=user1", 365, 365, import_source=self.import_source)
            app_dict: dict[str, Owner] = {"APP-001": owner}

            with self.assertLogs("app-data-import-tests", level="WARNING") as log_context:
                extract_ip_data_from_csv(
                    "ips.csv",
                    app_dict,
                    Appip,
                    self.logger,
                    self.debug_level,
                    base_dir=tmpdir,
                )

            self.assertEqual(len(owner.app_servers), 0)
            self.assertTrue(any("skipping csv file" in message for message in log_context.output))

    def test_parse_included_owners_filters_reuses_single_include_values_group(self) -> None:
        included_owners_filters: dict[str, tuple[str, ...]] | None = parse_included_owners_filters(
            ["Aktive Firewallregel", "Importieren"],
            [["Ja", "Ausnahme"]],
        )

        self.assertEqual(
            included_owners_filters,
            {
                "Aktive Firewallregel": ("Ja", "Ausnahme"),
                "Importieren": ("Ja", "Ausnahme"),
            },
        )

    def test_parse_included_owners_filters_matches_groups_to_columns(self) -> None:
        included_owners_filters: dict[str, tuple[str, ...]] | None = parse_included_owners_filters(
            ["Aktive Firewallregel", "Importieren"],
            [["Ja"], ["Nein", "Ausnahme"]],
        )

        self.assertEqual(
            included_owners_filters,
            {
                "Aktive Firewallregel": ("Ja",),
                "Importieren": ("Nein", "Ausnahme"),
            },
        )

    def test_parse_included_owners_filters_rejects_mismatched_group_count(self) -> None:
        with pytest.raises(
            argparse.ArgumentTypeError,
            match="number of --includeValues groups must match --filterColumn occurrences",
        ):
            parse_included_owners_filters(
                ["Aktive Firewallregel", "Importieren"],
                [["Ja"], ["Nein"], ["Ausnahme"]],
            )

    def test_parse_csv_separator_arg_accepts_supported_values(self) -> None:
        self.assertEqual(parse_csv_separator_arg(","), ",")
        self.assertEqual(parse_csv_separator_arg(";"), ";")

    def test_parse_csv_separator_arg_rejects_unsupported_values(self) -> None:
        with pytest.raises(argparse.ArgumentTypeError, match="invalid csv separator"):
            parse_csv_separator_arg("|")

    def test_normalize_option_value_args_allows_dash_prefixed_delimiter_value(self) -> None:
        self.assertEqual(
            normalize_option_value_args(
                ["--compositeIdFieldsDelimiterStr", "-abc", "--debug", "2"],
                ("--compositeIdFieldsDelimiterStr",),
            ),
            ["--compositeIdFieldsDelimiterStr=-abc", "--debug", "2"],
        )

    def test_read_custom_config_parses_json_with_comments(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write(
                    """
                    {
                      // single line comment
                      "ldapPath": "CN={USERID}", # hash comment
                      "csvSeparator": ",",
                      /* block comment */
                      "validAppIdPrefixes": ["app-", "com-",],
                    }
                    """
                )

            ldap_path: str = read_custom_config(str(config_path), "ldapPath", self.logger)
            valid_prefixes: list[str] = read_custom_config_with_default(
                str(config_path), "validAppIdPrefixes", [], self.logger
            )
            missing_with_default: str = read_custom_config_with_default(
                str(config_path), "missing", "fallback", self.logger
            )

            self.assertEqual(ldap_path, "CN={USERID}")
            self.assertEqual(valid_prefixes, ["app-", "com-"])
            self.assertEqual(missing_with_default, "fallback")

    def test_apply_owner_column_overrides_sets_lifecycle_state_pattern(self) -> None:
        patterns: dict[str, str] = {"name": r".*?:\s*Name"}
        updated_patterns: dict[str, str] = apply_owner_column_overrides(patterns, "Lifecycle Status")

        self.assertEqual(updated_patterns["name"], r".*?:\s*Name")
        self.assertEqual(updated_patterns["owner_lifecycle_state"], r"^\s*Lifecycle\ Status\s*$")

    def test_get_owner_data3_imports_owner_lifecycle_state_from_override_column(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA,Lifecycle Status\n"
                    "Overridden Lifecycle App,APP-013,user13,false,retired\n"
                )

            app_list: list[Owner] = []
            owner_header_patterns: dict[str, str] = apply_owner_column_overrides({}, "Lifecycle Status")
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                column_patterns=owner_header_patterns,
            )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].owner_lifecycle_state, "retired")

    def test_extract_app_data_from_csv_builds_composite_app_id_external(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,bogus: TISO,bogus: kwITA,Org Unit,System Name\nComposite App,user14,false,OPS,SRV-01\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                composite_id_fields=("Org Unit", "System Name"),
                composite_id_fields_delimiter_str="::",
            )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].app_id_external, "OPS::SRV-01")

    def test_extract_app_data_from_csv_truncates_composite_id_fields(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,bogus: TISO,bogus: kwITA,Typ,ID\n"
                    "Composite Truncate App,user15,false,APPLICATION,123456\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                composite_id_fields=("Typ", "ID"),
                composite_id_fields_delimiter_str="-",
                composite_id_fields_max_length=[3, 4],
            )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].app_id_external, "APP-1234")

    def test_extract_app_data_from_csv_skips_file_for_invalid_composite_max_length_count(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Name,bogus: TISO,bogus: kwITA,Typ,ID\nComposite App,user16,false,APP,1234\n")

            app_list: list[Owner] = []
            with self.assertLogs("app-data-import-tests", level="WARNING") as log_context:
                extract_app_data_from_csv(
                    "owners.csv",
                    app_list,
                    self.ldap_path,
                    self.import_source,
                    Owner,
                    self.logger,
                    self.debug_level,
                    base_dir=tmpdir,
                    composite_id_fields=("Typ", "ID"),
                    composite_id_fields_delimiter_str="-",
                    composite_id_fields_max_length=[3],
                )

            self.assertEqual(len(app_list), 0)
            self.assertTrue(
                any("compositeIdFields and compositeIdFieldsMaxLength count differ" in m for m in log_context.output)
            )

    def test_extract_app_data_from_csv_imports_criticality_when_header_configured(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA,Criticality\n"
                    "App Critical,APP-014,user14,false,High\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                criticality_column_header="Criticality",
            )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].to_json().get("criticality"), "High")

    def test_extract_app_data_from_csv_applies_criticality_recert_period_mapping(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA,Criticality\n"
                    "App Critical,APP-015,user15,false,3 High\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                criticality_column_header="Criticality",
                criticality_recert_period_mapping={"1": 360, "2": 360, "3": 180, "4": 180, "5": 180},
            )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].recert_period_days, 180)
            self.assertEqual(app_list[0].days_until_first_recert, 180)

    def test_extract_app_data_from_csv_uses_kwita_when_no_criticality_mapping_match(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA,Criticality\n"
                    "App Critical Fallback,APP-016,user16,true,9 Low\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                criticality_column_header="Criticality",
                criticality_recert_period_mapping={"1": 360, "2": 360, "3": 180},
            )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].recert_period_days, 182)

    def test_parse_criticality_recert_period_mapping_parses_entries(self) -> None:
        mapping: dict[str, int] = parse_criticality_recert_period_mapping(["1:360", "2:360", "3:180"])
        self.assertEqual(mapping, {"1": 360, "2": 360, "3": 180})

    def test_build_git_repo_url_includes_credentials_when_configured(self) -> None:
        repo_url: str | None = build_git_repo_url(
            "github.example.de/cmdb/app-export",
            "git-user-1",
            "secret",
            self.logger,
            "CMDB",
        )

        self.assertEqual(repo_url, "https://git-user-1:secret@github.example.de/cmdb/app-export")

    def test_build_git_repo_url_uses_anonymous_access_when_credentials_missing(self) -> None:
        repo_url: str | None = build_git_repo_url(
            "https://github.example.de/cmdb/app-export",
            None,
            None,
            self.logger,
            "CMDB",
        )

        self.assertEqual(repo_url, "https://github.example.de/cmdb/app-export")

    def test_build_git_repo_url_returns_none_when_repo_missing(self) -> None:
        with self.assertLogs("app-data-import-tests", level="WARNING") as log_context:
            repo_url: str | None = build_git_repo_url(None, None, None, self.logger, "CMDB")

        self.assertIsNone(repo_url)
        self.assertTrue(any("git repo url missing" in message for message in log_context.output))

    def test_parse_responsibles_columns_parses_grouped_entries(self) -> None:
        parsed: dict[str, tuple[str, ...]] = parse_responsibles_columns(
            ["1:UserId", "UserID Vertreter", "2:UserIDs Mitwirkende", "3:UserID Leiter OE"]
        )
        self.assertEqual(
            parsed,
            {
                "1": ("UserId", "UserID Vertreter"),
                "2": ("UserIDs Mitwirkende",),
                "3": ("UserID Leiter OE",),
            },
        )

    def test_parse_responsibles_columns_parses_quoted_grouped_entries(self) -> None:
        parsed: dict[str, tuple[str, ...]] = parse_responsibles_columns(
            ['1:UserID "UserID Vertreter"', '2:"UserIDs Mitwirkende"', '3:"UserID Leiter OE"']
        )
        self.assertEqual(
            parsed,
            {
                "1": ("UserID", "UserID Vertreter"),
                "2": ("UserIDs Mitwirkende",),
                "3": ("UserID Leiter OE",),
            },
        )

    def test_extract_app_data_from_csv_imports_responsibles_when_configured(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA,UserId,UserID Vertreter,UserIDs Mitwirkende,UserID Leiter OE\n"
                    "App Resp,APP-017,user17,false,uid-main,uid-deputy,uid-collab,uid-lead\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                responsibles_columns_headers={
                    "1": ("UserId", "UserID Vertreter"),
                    "2": ("UserIDs Mitwirkende",),
                    "3": ("UserID Leiter OE",),
                },
            )

            self.assertEqual(len(app_list), 1)
            owner_json: dict[str, object] = app_list[0].to_json()
            self.assertEqual(
                owner_json.get("responsibles"),
                {
                    "1": ["CN=uid-main", "CN=uid-deputy"],
                    "2": ["CN=uid-collab"],
                    "3": ["CN=uid-lead"],
                },
            )

    def test_extract_app_data_from_csv_leaves_responsibles_empty_when_not_configured(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA\nFallback App,APP-017,user17,false\n")

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
            )

            self.assertEqual(len(app_list), 1)
            owner_json: dict[str, object] = app_list[0].to_json()
            self.assertIsNone(owner_json.get("responsibles"))

    def test_extract_app_data_from_csv_imports_responsibles_without_owner_tiso_column(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: kwITA,UserId,UserID Vertreter,UserIDs Mitwirkende\n"
                    "App Resp,APP-021,false,uid-main,uid-deputy,uid-collab\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                responsibles_columns_headers={
                    "1": ("UserId", "UserID Vertreter"),
                    "2": ("UserIDs Mitwirkende",),
                },
            )

            self.assertEqual(len(app_list), 1)
            self.assertEqual(app_list[0].main_user, "CN=uid-main")
            owner_json: dict[str, object] = app_list[0].to_json()
            self.assertEqual(
                owner_json.get("responsibles"),
                {
                    "1": ["CN=uid-main", "CN=uid-deputy"],
                    "2": ["CN=uid-collab"],
                },
            )

    def test_extract_app_data_from_csv_uses_custom_level_two_responsible_pattern(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA\nFallback App,APP-018,user18,false\n")

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                level_two_responsible_pattern="ROLE_@@AppId@@_@@AppPrefix@@",
            )

            self.assertEqual(len(app_list), 1)
            owner_json: dict[str, object] = app_list[0].to_json()
            self.assertEqual(
                owner_json.get("responsibles"),
                {"2": ["ROLE_018_APP"]},
            )

    def test_extract_app_data_from_csv_splits_multi_user_responsibles_values(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    "col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA,UserID,UserID Vertreter,UserIDs Mitwirkende,UserID Leiter OE\n"
                    'App Resp Split,APP-018,user18,false,K9M4,R2N8,"A7B2,K9M4,X3T8,J5L1,D8R6",R8M4\n'
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                responsibles_columns_headers={
                    "10": ("UserID", "UserID Vertreter"),
                    "20": ("UserIDs Mitwirkende",),
                    "30": ("UserID Leiter OE",),
                },
            )

            self.assertEqual(len(app_list), 1)
            owner_json: dict[str, object] = app_list[0].to_json()
            self.assertEqual(
                owner_json.get("responsibles"),
                {
                    "10": ["CN=K9M4", "CN=R2N8"],
                    "20": ["CN=A7B2", "CN=K9M4", "CN=X3T8", "CN=J5L1", "CN=D8R6"],
                    "30": ["CN=R8M4"],
                },
            )
            self.assertEqual(app_list[0].main_user, "")

    def test_extract_app_data_from_csv_uses_custom_level_two_pattern_without_separator(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write("col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA\nFallback App,APP017,user18,false\n")

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                level_two_responsible_pattern="A_@@AppPrefix@@_@@AppId@@_FW_RULEMGT",
            )

            self.assertEqual(len(app_list), 1)
            owner_json: dict[str, object] = app_list[0].to_json()
            self.assertEqual(
                owner_json.get("responsibles"),
                {"2": ["A_APP_017_FW_RULEMGT"]},
            )

    def test_extract_app_data_from_csv_handles_long_separator_free_ids(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            owner_csv_path: Path = Path(tmpdir) / "owners.csv"
            long_app_id: str = f"APP{'7' * 20000}"
            with open(owner_csv_path, "w", encoding="utf-8") as fh:
                fh.write(
                    f"col: Name,col: Alfabet-ID,bogus: TISO,bogus: kwITA\nFallback App,{long_app_id},user18,false\n"
                )

            app_list: list[Owner] = []
            extract_app_data_from_csv(
                "owners.csv",
                app_list,
                self.ldap_path,
                self.import_source,
                Owner,
                self.logger,
                self.debug_level,
                base_dir=tmpdir,
                level_two_responsible_pattern="A_@@AppPrefix@@_@@AppId@@_FW_RULEMGT",
            )

            self.assertEqual(len(app_list), 1)
            owner_json: dict[str, object] = app_list[0].to_json()
            self.assertEqual(
                owner_json.get("responsibles"),
                {"2": [f"A_APP_{'7' * 20000}_FW_RULEMGT"]},
            )


if __name__ == "__main__":
    unittest.main()

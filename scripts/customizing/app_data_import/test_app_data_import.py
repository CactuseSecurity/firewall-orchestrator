import logging
import sys
import tempfile
import unittest
from pathlib import Path

from scripts.customizing.app_data_import.get_owner_data3_from_normalized_csvs import (
    apply_owner_column_overrides,
    parse_criticality_recert_period_mapping,
)
from scripts.customizing.fwo_custom_lib.app_data_models import Appip, Owner
from scripts.customizing.fwo_custom_lib.basic_helpers import (
    read_custom_config,
    read_custom_config_with_default,
)
from scripts.customizing.fwo_custom_lib.read_app_data_csv import (
    extract_app_data_from_csv,
    extract_ip_data_from_csv,
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
            self.assertEqual(owner.main_user, "CN=user1")
            self.assertEqual(owner.recert_period_days, 365)
            self.assertEqual(owner.import_source, self.import_source)
            self.assertEqual(owner.owner_lifecycle_state, "unknown")
            self.assertNotIn("criticality", owner.to_json())

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
                "owner_tiso": r"Main Owner",
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
            self.assertEqual(owner.main_user, "CN=user2")
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


if __name__ == "__main__":
    unittest.main()

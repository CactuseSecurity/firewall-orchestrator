import logging
import sys
import tempfile
import unittest
from pathlib import Path

# Make module directory importable
MODULE_DIR: Path = Path(__file__).resolve().parent
sys.path.insert(0, str(MODULE_DIR))
# Also add shared library folder for imports when running via test discovery
sys.path.insert(0, str(MODULE_DIR.parent / "fwo_custom_lib"))

from scripts.customizing.fwo_custom_lib.app_data_models import Appip, Owner
from scripts.customizing.fwo_custom_lib.read_app_data_csv import (
    extract_app_data_from_csv,
    extract_ip_data_from_csv,
)


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


if __name__ == "__main__":
    unittest.main()

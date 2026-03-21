import logging
import tempfile
import unittest
from pathlib import Path

from scripts.customizing.fwo_custom_lib.app_data_models import Owner
from scripts.customizing.iiq_request_missing_roles.iiq_request_missing_fwmgt_roles import (
    FWO_TMP_DIR,
    get_tisos_from_owner_dict,
    resolve_debug_level,
    resolve_git_depth,
    resolve_import_from_folder,
    resolve_local_repo_base_dir,
)


class IiqRequestMissingRolesTests(unittest.TestCase):
    def setUp(self) -> None:
        self.logger: logging.Logger = logging.getLogger("iiq-request-missing-roles-tests")
        self.logger.addHandler(logging.NullHandler())

    def test_resolve_local_repo_base_dir_prefers_iiq_config_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            expected_repo_dir: str = str(Path(tmpdir) / "fworch-iiq-config-repos")
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write(
                    f"""
                    {{
                      "iiqLocalRepoBaseDir": "{expected_repo_dir}"
                    }}
                    """
                )

            resolved: str = resolve_local_repo_base_dir(str(config_path), None, self.logger)

            self.assertEqual(resolved, expected_repo_dir)

    def test_resolve_local_repo_base_dir_prefers_shared_config_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            expected_repo_dir: str = str(Path(tmpdir) / "fworch-shared-config-repos")
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write(
                    f"""
                    {{
                      "localRepoBaseDir": "{expected_repo_dir}",
                      "iiqLocalRepoBaseDir": "{Path(tmpdir) / "fworch-iiq-config-repos"}"
                    }}
                    """
                )

            resolved: str = resolve_local_repo_base_dir(str(config_path), None, self.logger)

            self.assertEqual(resolved, expected_repo_dir)

    def test_resolve_local_repo_base_dir_prefers_cli_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            config_repo_dir: str = str(Path(tmpdir) / "fworch-iiq-config-repos")
            cli_repo_dir: str = str(Path(tmpdir) / "fworch-iiq-cli-repos")
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write(
                    f"""
                    {{
                      "iiqLocalRepoBaseDir": "{config_repo_dir}"
                    }}
                    """
                )

            resolved: str = resolve_local_repo_base_dir(str(config_path), cli_repo_dir, self.logger)

            self.assertEqual(resolved, cli_repo_dir)

    def test_resolve_local_repo_base_dir_falls_back_to_iiq_default(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write("{}")

            resolved: str = resolve_local_repo_base_dir(str(config_path), None, self.logger)

            self.assertEqual(resolved, FWO_TMP_DIR)

    def test_resolve_import_from_folder_prefers_cli_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            config_import_dir: str = str(Path(tmpdir) / "config-import")
            cli_import_dir: str = str(Path(tmpdir) / "cli-import")
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write(
                    f"""
                    {{
                      "importFromFolder": "{config_import_dir}"
                    }}
                    """
                )

            resolved: str | None = resolve_import_from_folder(str(config_path), cli_import_dir, self.logger)

            self.assertEqual(resolved, cli_import_dir)

    def test_resolve_import_from_folder_reads_config_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            expected_import_dir: str = str(Path(tmpdir) / "config-import")
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write(
                    f"""
                    {{
                      "importFromFolder": "{expected_import_dir}"
                    }}
                    """
                )

            resolved: str | None = resolve_import_from_folder(str(config_path), None, self.logger)

            self.assertEqual(resolved, expected_import_dir)

    def test_resolve_debug_level_reads_config_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write('{"debug": "3"}')

            resolved: int = resolve_debug_level(str(config_path), None, self.logger)

            self.assertEqual(resolved, 3)

    def test_resolve_debug_level_prefers_cli_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write('{"debug": "1"}')

            resolved: int = resolve_debug_level(str(config_path), "4", self.logger)

            self.assertEqual(resolved, 4)

    def test_resolve_git_depth_reads_config_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write('{"depth": "5"}')

            resolved: int | None = resolve_git_depth(str(config_path), None, self.logger)

            self.assertEqual(resolved, 5)

    def test_resolve_git_depth_prefers_cli_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write('{"depth": "2"}')

            resolved: int | None = resolve_git_depth(str(config_path), 7, self.logger)

            self.assertEqual(resolved, 7)

    def test_get_tisos_from_owner_dict_uses_level_one_responsible(self) -> None:
        owner: Owner = Owner(
            "Example App",
            "APP-001",
            "CN=legacy-user,OU=Users,DC=example,DC=org",
            365,
            365,
            responsibles={"1": ["CN=tiso-user,OU=Users,DC=example,DC=org"]},
        )

        tisos: dict[str, str] = get_tisos_from_owner_dict({"APP-001": owner})

        self.assertEqual(tisos, {"APP-001": "tiso-user"})

    def test_get_tisos_from_owner_dict_skips_owner_without_level_one_responsible(self) -> None:
        owner: Owner = Owner("Example App", "APP-001", "CN=legacy-user,OU=Users,DC=example,DC=org", 365, 365)

        with self.assertLogs("iiq-request-missing-roles", level="WARNING") as log_context:
            tisos: dict[str, str] = get_tisos_from_owner_dict({"APP-001": owner})

        self.assertEqual(tisos, {})
        self.assertTrue(any("has no level 1 responsible" in message for message in log_context.output))

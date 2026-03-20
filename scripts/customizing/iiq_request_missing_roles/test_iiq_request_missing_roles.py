import logging
import tempfile
import unittest
from pathlib import Path

from scripts.customizing.iiq_request_missing_roles.iiq_request_missing_fwmgt_roles import (
    FWO_TMP_DIR,
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

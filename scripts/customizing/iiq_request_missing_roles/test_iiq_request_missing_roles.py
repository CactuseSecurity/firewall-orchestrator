import json
import logging
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

import pytest
from netaddr import IPAddress

import scripts.customizing.iiq_request_missing_roles.iiq_request_missing_fwmgt_roles as iiq_roles
from scripts.customizing.fwo_custom_lib.app_data_models import Appip, Owner
from scripts.customizing.fwo_custom_lib.basic_helpers import get_logger
from scripts.customizing.iiq_request_missing_roles.iiq_request_missing_fwmgt_roles import (
    FWO_TMP_DIR,
    get_git_repo,
    get_tisos_from_owner_dict,
    get_tisos_orgids,
    init_statistics,
    remove_apps_without_ip_addresses,
    request_all_roles,
    resolve_debug_level,
    resolve_git_depth,
    resolve_import_from_folder,
    resolve_local_repo_base_dir,
    resolve_responsibles_columns_headers,
    write_stats_to_file,
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
                json.dump({"iiqLocalRepoBaseDir": expected_repo_dir}, fh)

            resolved: str = resolve_local_repo_base_dir(str(config_path), None, self.logger)

            self.assertEqual(resolved, expected_repo_dir)

    def test_resolve_local_repo_base_dir_prefers_shared_config_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            expected_repo_dir: str = str(Path(tmpdir) / "fworch-shared-config-repos")
            iiq_repo_dir: str = str(Path(tmpdir) / "fworch-iiq-config-repos")
            with open(config_path, "w", encoding="utf-8") as fh:
                json.dump({"localRepoBaseDir": expected_repo_dir, "iiqLocalRepoBaseDir": iiq_repo_dir}, fh)

            resolved: str = resolve_local_repo_base_dir(str(config_path), None, self.logger)

            self.assertEqual(resolved, expected_repo_dir)

    def test_resolve_local_repo_base_dir_prefers_cli_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            config_repo_dir: str = str(Path(tmpdir) / "fworch-iiq-config-repos")
            cli_repo_dir: str = str(Path(tmpdir) / "fworch-iiq-cli-repos")
            with open(config_path, "w", encoding="utf-8") as fh:
                json.dump({"iiqLocalRepoBaseDir": config_repo_dir}, fh)

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
                json.dump({"importFromFolder": config_import_dir}, fh)

            resolved: str | None = resolve_import_from_folder(str(config_path), cli_import_dir, self.logger)

            self.assertEqual(resolved, cli_import_dir)

    def test_resolve_import_from_folder_reads_config_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            expected_import_dir: str = str(Path(tmpdir) / "config-import")
            with open(config_path, "w", encoding="utf-8") as fh:
                json.dump({"importFromFolder": expected_import_dir}, fh)

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

    def test_resolve_responsibles_columns_headers_reads_config_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write(
                    """
                    {
                      "responsiblesColumns": {
                        "1": ["TISO UserID", "TISO Backup"],
                        "2": ["Owner UserID"]
                      }
                    }
                    """
                )

            resolved: dict[str, tuple[str, ...]] | None = resolve_responsibles_columns_headers(
                str(config_path), self.logger
            )

            self.assertEqual(resolved, {"1": ("TISO UserID", "TISO Backup"), "2": ("Owner UserID",)})

    def test_resolve_responsibles_columns_headers_reads_list_config_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write(
                    """
                    {
                      "responsiblesColumns": ["1:TISO UserID", "1:TISO Backup", "2:Owner UserID"]
                    }
                    """
                )

            resolved: dict[str, tuple[str, ...]] | None = resolve_responsibles_columns_headers(
                str(config_path), self.logger
            )

            self.assertEqual(resolved, {"1": ("TISO UserID", "TISO Backup"), "2": ("Owner UserID",)})

    def test_resolve_responsibles_columns_headers_returns_none_when_missing(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write("{}")

            resolved: dict[str, tuple[str, ...]] | None = resolve_responsibles_columns_headers(
                str(config_path), self.logger
            )

            self.assertIsNone(resolved)

    def test_resolve_responsibles_columns_headers_reads_snake_case_list_config_value(self) -> None:
        with tempfile.TemporaryDirectory() as tmpdir:
            config_path: Path = Path(tmpdir) / "customizingConfig.json"
            with open(config_path, "w", encoding="utf-8") as fh:
                fh.write(
                    """
                    {
                      "responsibles_columns": ["1:TISO UserID"]
                    }
                    """
                )

            resolved: dict[str, tuple[str, ...]] | None = resolve_responsibles_columns_headers(
                str(config_path), self.logger
            )

            self.assertEqual(resolved, {"1": ("TISO UserID",)})

    def test_get_tisos_from_owner_dict_uses_level_one_responsible(self) -> None:
        owner: Owner = Owner(
            "Example App",
            "APP-001",
            365,
            365,
            responsibles={"1": ["CN=tiso-user,OU=Users,DC=example,DC=org"]},
        )

        tisos: dict[str, str] = get_tisos_from_owner_dict({"APP-001": owner})

        self.assertEqual(tisos, {"APP-001": "tiso-user"})

    def test_get_tisos_from_owner_dict_skips_owner_without_level_one_responsible(self) -> None:
        owner: Owner = Owner("Example App", "APP-001", 365, 365)

        with self.assertLogs("iiq-request-missing-roles", level="WARNING") as log_context:
            tisos: dict[str, str] = get_tisos_from_owner_dict({"APP-001": owner})

        self.assertEqual(tisos, {})
        self.assertTrue(any("has no level 1 responsible" in message for message in log_context.output))

    def test_remove_apps_without_ip_addresses_keeps_only_valid_ipv4_owner(self) -> None:
        iiq_roles.logger = get_logger(0)
        valid_owner: Owner = Owner("Valid", "APP-001", 365, 365)
        valid_owner.app_servers.append(Appip("APP-001", IPAddress("10.0.0.1"), IPAddress("10.0.0.1"), "host", "srv"))
        invalid_owner: Owner = Owner("Invalid", "APP-002", 365, 365)

        owners: dict[str, Owner] = {"APP-001": valid_owner, "APP-002": invalid_owner}

        remove_apps_without_ip_addresses(owners)

        self.assertEqual(list(owners), ["APP-001"])


def test_init_statistics_creates_lists_and_counts() -> None:
    stats = init_statistics()

    assert stats["apps_newly_requested"] == []
    assert stats["apps_newly_requested_count"] == 0
    assert stats["existing_technical_functions"] == []
    assert stats["existing_technical_functions_count"] == 0


def test_write_stats_to_file_creates_log_file(tmp_path: Path) -> None:
    stats = {"apps_newly_requested": ["APP-001"], "apps_newly_requested_count": 1}

    write_stats_to_file(stats, str(tmp_path))

    log_files = list(tmp_path.glob("*_iiq_request.log"))
    assert len(log_files) == 1
    assert json.loads(log_files[0].read_text(encoding="utf-8")) == stats


def test_get_git_repo_exits_when_update_fails() -> None:
    repo_target_dir = "repo"
    with (
        patch(
            "scripts.customizing.iiq_request_missing_roles.iiq_request_missing_fwmgt_roles.update_git_repo",
            return_value=False,
        ),
        pytest.raises(SystemExit),
    ):
        get_git_repo("git.example.org/repo.git", "user", "p@ss word", repo_target_dir, depth=1)


def test_get_tisos_orgids_returns_resolved_values_and_can_dump(capsys: pytest.CaptureFixture[str]) -> None:
    iiq_roles.logger = get_logger(0)
    iiq_client = Mock()
    org_ids = {"tiso-1": "ORG-1"}
    iiq_client.get_org_id.side_effect = org_ids.get

    resolved = get_tisos_orgids({"APP-001": "tiso-1", "APP-002": "tiso-2"}, iiq_client)

    assert resolved == {"tiso-1": "ORG-1"}

    dump_org_ids = {"tiso-3": "ORG-3"}
    iiq_client.get_org_id.side_effect = dump_org_ids.get
    with pytest.raises(SystemExit):
        get_tisos_orgids({"APP-003": "tiso-3"}, iiq_client, exit_after_dump=True)

    assert capsys.readouterr().out == "tiso-3,ORG-3\n"


def test_request_all_roles_skips_missing_inputs_and_requests_needed_role() -> None:
    iiq_roles.logger = get_logger(0)
    existing_owner = Owner("Existing", "APP-001", 365, 365)
    requested_owner = Owner("Requested", "APP-002", 365, 365)
    missing_tiso_owner = Owner("Missing TISO", "APP-003", 365, 365)
    missing_org_owner = Owner("Missing Org", "APP-004", 365, 365)
    iiq_client = Mock()
    iiq_client.app_functions_exist_in_iiq.side_effect = [True, False]
    stats = init_statistics()

    request_all_roles(
        {
            "APP-001": existing_owner,
            "APP-002": requested_owner,
            "APP-003": missing_tiso_owner,
            "APP-004": missing_org_owner,
        },
        {"APP-001": "tiso-1", "APP-002": "tiso-2", "APP-004": "tiso-4"},
        {"tiso-1": "ORG-1", "tiso-2": "ORG-2"},
        iiq_client,
        stats,
        first=0,
        run_workflow=False,
    )

    iiq_client.request_group_creation.assert_called_once_with(
        "APP",
        "002",
        "ORG-2",
        "tiso-2",
        "Requested",
        stats,
        run_workflow=False,
    )

import logging
from pathlib import Path
from unittest.mock import Mock, patch

import git
import pytest

from scripts.customizing.fwo_custom_lib.git_helpers import (
    cleanup_repo_target_dir,
    commit_and_push_deletions,
    parse_git_depth_arg,
    read_file_from_git_repo,
    split_repo_url_credentials,
    update_git_repo,
)

EXPECTED_DEPTH: int = 5
UPDATED_DEPTH: int = 7
CLONE_DEPTH: int = 3
REPO_TARGET_DIR: str = str(Path("var") / "tmp" / "repo")


def test_parse_git_depth_arg_accepts_positive_integer() -> None:
    assert parse_git_depth_arg(str(EXPECTED_DEPTH)) == EXPECTED_DEPTH


def test_parse_git_depth_arg_rejects_non_positive_values() -> None:
    with pytest.raises(ValueError, match="invalid git depth value: 0"):
        parse_git_depth_arg("0")


def test_parse_git_depth_arg_rejects_non_integer_values() -> None:
    with pytest.raises(ValueError, match="invalid git depth value: abc"):
        parse_git_depth_arg("abc")


def test_split_repo_url_credentials_sanitizes_username_only_url_without_credentials() -> None:
    clone_url, username, password = split_repo_url_credentials("https://git-user@git.example.org/group/repo.git")

    assert clone_url == "https://git.example.org/group/repo.git"
    assert username is None
    assert password is None


def test_split_repo_url_credentials_sanitizes_password_only_url_without_credentials() -> None:
    clone_url, username, password = split_repo_url_credentials("https://:secret@git.example.org/group/repo.git")

    assert clone_url == "https://git.example.org/group/repo.git"
    assert username is None
    assert password is None


def test_update_git_repo_replaces_existing_repo_with_clean_clone_when_depth_not_set() -> None:
    logger: logging.Logger = logging.getLogger("git-helper-tests")
    repo_path_mock: Mock = Mock()
    repo_path_mock.exists.return_value = True
    repo_path_mock.is_dir.return_value = True
    parent_path_mock: Mock = Mock()
    repo_path_mock.parent = parent_path_mock

    with (
        patch("scripts.customizing.fwo_custom_lib.git_helpers.Path", return_value=repo_path_mock),
        patch("scripts.customizing.fwo_custom_lib.git_helpers.shutil.rmtree") as rmtree_mock,
        patch("scripts.customizing.fwo_custom_lib.git_helpers.git.Repo.clone_from") as clone_from_mock,
    ):
        update_git_repo("https://example.invalid/repo.git", REPO_TARGET_DIR, logger)

    rmtree_mock.assert_called_once_with(repo_path_mock)
    parent_path_mock.mkdir.assert_called_once_with(parents=True, exist_ok=True)
    clone_from_mock.assert_called_once_with("https://example.invalid/repo.git", REPO_TARGET_DIR)


def test_update_git_repo_replaces_existing_repo_file_before_clone() -> None:
    logger: logging.Logger = logging.getLogger("git-helper-tests")
    repo_path_mock: Mock = Mock()
    repo_path_mock.exists.return_value = True
    repo_path_mock.is_dir.return_value = False
    parent_path_mock: Mock = Mock()
    repo_path_mock.parent = parent_path_mock

    with (
        patch("scripts.customizing.fwo_custom_lib.git_helpers.Path", return_value=repo_path_mock),
        patch("scripts.customizing.fwo_custom_lib.git_helpers.shutil.rmtree") as rmtree_mock,
        patch("scripts.customizing.fwo_custom_lib.git_helpers.git.Repo.clone_from") as clone_from_mock,
    ):
        update_git_repo("https://example.invalid/repo.git", REPO_TARGET_DIR, logger, depth=UPDATED_DEPTH)

    repo_path_mock.unlink.assert_called_once_with()
    rmtree_mock.assert_not_called()
    parent_path_mock.mkdir.assert_called_once_with(parents=True, exist_ok=True)
    clone_from_mock.assert_called_once_with("https://example.invalid/repo.git", REPO_TARGET_DIR, depth=UPDATED_DEPTH)


def test_update_git_repo_omits_depth_for_clone_when_not_set() -> None:
    logger: logging.Logger = logging.getLogger("git-helper-tests")
    repo_path_mock: Mock = Mock()
    repo_path_mock.exists.return_value = False
    parent_path_mock: Mock = Mock()
    repo_path_mock.parent = parent_path_mock

    with (
        patch("scripts.customizing.fwo_custom_lib.git_helpers.Path", return_value=repo_path_mock),
        patch("scripts.customizing.fwo_custom_lib.git_helpers.git.Repo.clone_from") as clone_from_mock,
    ):
        update_git_repo("https://example.invalid/repo.git", REPO_TARGET_DIR, logger, branch="main")

    parent_path_mock.mkdir.assert_called_once_with(parents=True, exist_ok=True)
    clone_from_mock.assert_called_once_with("https://example.invalid/repo.git", REPO_TARGET_DIR, branch="main")


def test_update_git_repo_passes_depth_for_clone_when_set() -> None:
    logger: logging.Logger = logging.getLogger("git-helper-tests")
    repo_path_mock: Mock = Mock()
    repo_path_mock.exists.return_value = False
    parent_path_mock: Mock = Mock()
    repo_path_mock.parent = parent_path_mock

    with (
        patch("scripts.customizing.fwo_custom_lib.git_helpers.Path", return_value=repo_path_mock),
        patch("scripts.customizing.fwo_custom_lib.git_helpers.git.Repo.clone_from") as clone_from_mock,
    ):
        update_git_repo("https://example.invalid/repo.git", REPO_TARGET_DIR, logger, branch="main", depth=CLONE_DEPTH)

    parent_path_mock.mkdir.assert_called_once_with(parents=True, exist_ok=True)
    clone_from_mock.assert_called_once_with(
        "https://example.invalid/repo.git",
        REPO_TARGET_DIR,
        branch="main",
        depth=CLONE_DEPTH,
    )


def test_update_git_repo_does_not_use_askpass_for_partial_credentials() -> None:
    logger: logging.Logger = logging.getLogger("git-helper-tests")
    repo_path_mock: Mock = Mock()
    repo_path_mock.exists.return_value = False
    parent_path_mock: Mock = Mock()
    repo_path_mock.parent = parent_path_mock

    with (
        patch("scripts.customizing.fwo_custom_lib.git_helpers.Path", return_value=repo_path_mock),
        patch("scripts.customizing.fwo_custom_lib.git_helpers.git.Repo.clone_from") as clone_from_mock,
    ):
        update_git_repo("https://git-user@example.invalid/repo.git", REPO_TARGET_DIR, logger)

    parent_path_mock.mkdir.assert_called_once_with(parents=True, exist_ok=True)
    clone_from_mock.assert_called_once_with("https://example.invalid/repo.git", REPO_TARGET_DIR)


def test_update_git_repo_removes_partial_repo_after_clone_failure() -> None:
    logger: logging.Logger = logging.getLogger("git-helper-tests")
    repo_path_mock: Mock = Mock()
    repo_path_mock.exists.return_value = False
    parent_path_mock: Mock = Mock()
    repo_path_mock.parent = parent_path_mock

    with (
        patch("scripts.customizing.fwo_custom_lib.git_helpers.Path", return_value=repo_path_mock),
        patch(
            "scripts.customizing.fwo_custom_lib.git_helpers.git.Repo.clone_from",
            side_effect=RuntimeError("clone failed"),
        ),
        patch("scripts.customizing.fwo_custom_lib.git_helpers.shutil.rmtree") as rmtree_mock,
    ):
        repo_updated: bool = update_git_repo("https://example.invalid/repo.git", REPO_TARGET_DIR, logger)

    assert repo_updated is False
    parent_path_mock.mkdir.assert_called_once_with(parents=True, exist_ok=True)
    rmtree_mock.assert_not_called()
    repo_path_mock.unlink.assert_not_called()


def test_read_file_from_git_repo_removes_repo_directory_after_read() -> None:
    logger: logging.Logger = logging.getLogger("git-helper-tests")
    repo_path_mock: Mock = Mock()
    repo_path_mock.exists.return_value = True
    repo_path_mock.is_dir.return_value = True

    with (
        patch("scripts.customizing.fwo_custom_lib.git_helpers.Path", return_value=repo_path_mock),
        patch("scripts.customizing.fwo_custom_lib.git_helpers.update_git_repo", return_value=True),
        patch("builtins.open", create=True) as open_mock,
        patch("scripts.customizing.fwo_custom_lib.git_helpers.shutil.rmtree") as rmtree_mock,
    ):
        open_mock.return_value.__enter__.return_value.read.return_value = "file content"

        file_contents: str = read_file_from_git_repo(
            "https://example.invalid/repo.git",
            REPO_TARGET_DIR,
            "sample.txt",
            logger,
        )

    assert file_contents == "file content"
    rmtree_mock.assert_called_once_with(repo_path_mock)


def test_cleanup_repo_target_dir_removes_existing_directory() -> None:
    repo_path_mock: Mock = Mock()
    repo_path_mock.exists.return_value = True
    repo_path_mock.is_dir.return_value = True

    with (
        patch("scripts.customizing.fwo_custom_lib.git_helpers.Path", return_value=repo_path_mock),
        patch("scripts.customizing.fwo_custom_lib.git_helpers.shutil.rmtree") as rmtree_mock,
    ):
        cleanup_repo_target_dir(REPO_TARGET_DIR)

    rmtree_mock.assert_called_once_with(repo_path_mock)


LOGGER: logging.Logger = logging.getLogger("test_git_helpers")
COMMIT_MESSAGE: str = "chore: remove imported log data"


def create_clone_with_origin(tmp_path: Path) -> tuple[Path, git.Repo, git.Repo]:
    """Build a bare origin with one committed CSV file and a clone working on it."""
    origin_path: Path = tmp_path / "origin.git"
    origin: git.Repo = git.Repo.init(origin_path, bare=True, initial_branch="main")
    seed_path: Path = tmp_path / "seed"
    seed: git.Repo = git.Repo.clone_from(str(origin_path), str(seed_path))
    configure_identity(seed)
    (seed_path / "logs.csv").write_text("App ID,Log count\nAPP-1,1\n", encoding="utf-8")
    seed.git.add("logs.csv")
    seed.index.commit("chore: add sample log data csv files")
    seed.git.push("origin", "HEAD:refs/heads/main")

    clone_path: Path = tmp_path / "clone"
    clone: git.Repo = git.Repo.clone_from(str(origin_path), str(clone_path), branch="main")
    configure_identity(clone)
    return clone_path, clone, origin


def configure_identity(repo: git.Repo) -> None:
    with repo.config_writer() as config:
        config.set_value("user", "name", "FWO Test")
        config.set_value("user", "email", "test@local")


def test_commit_and_push_deletions_removes_the_file_from_origin(tmp_path: Path) -> None:
    clone_path, clone, origin = create_clone_with_origin(tmp_path)

    pushed: bool = commit_and_push_deletions(str(clone_path), [clone_path / "logs.csv"], COMMIT_MESSAGE, LOGGER)

    assert pushed
    assert not (clone_path / "logs.csv").exists()
    assert clone.head.commit.message.strip() == COMMIT_MESSAGE
    assert "logs.csv" not in origin.head.commit.tree


def test_commit_and_push_deletions_without_changes_creates_no_commit(tmp_path: Path) -> None:
    clone_path, clone, _ = create_clone_with_origin(tmp_path)
    commit_before: str = clone.head.commit.hexsha

    pushed: bool = commit_and_push_deletions(str(clone_path), [], COMMIT_MESSAGE, LOGGER)

    assert pushed
    assert clone.head.commit.hexsha == commit_before


def test_commit_and_push_deletions_reports_a_file_outside_the_repository(tmp_path: Path) -> None:
    clone_path, _, _ = create_clone_with_origin(tmp_path)
    outside_file: Path = tmp_path / "outside.csv"
    outside_file.write_text("App ID,Log count\n", encoding="utf-8")

    pushed: bool = commit_and_push_deletions(str(clone_path), [outside_file], COMMIT_MESSAGE, LOGGER)

    assert not pushed
    assert outside_file.exists()


def test_commit_and_push_deletions_reports_a_failing_push(tmp_path: Path) -> None:
    clone_path, clone, _ = create_clone_with_origin(tmp_path)
    clone.delete_remote(clone.remote("origin"))

    pushed: bool = commit_and_push_deletions(str(clone_path), [clone_path / "logs.csv"], COMMIT_MESSAGE, LOGGER)

    assert not pushed

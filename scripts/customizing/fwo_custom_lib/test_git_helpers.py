import logging
from pathlib import Path
from unittest.mock import Mock, patch

import pytest

from scripts.customizing.fwo_custom_lib.git_helpers import (
    cleanup_repo_target_dir,
    parse_git_depth_arg,
    read_file_from_git_repo,
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

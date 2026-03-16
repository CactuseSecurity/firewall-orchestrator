import logging
from pathlib import Path
from unittest.mock import Mock, patch

import pytest

from scripts.customizing.fwo_custom_lib.git_helpers import parse_git_depth_arg, update_git_repo

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


def test_update_git_repo_omits_depth_for_pull_when_not_set() -> None:
    logger: logging.Logger = logging.getLogger("git-helper-tests")
    repo_mock: Mock = Mock()
    repo_mock.remotes.origin.pull = Mock()

    with (
        patch("scripts.customizing.fwo_custom_lib.git_helpers.Path.exists", return_value=True),
        patch("scripts.customizing.fwo_custom_lib.git_helpers.git.Repo", return_value=repo_mock),
    ):
        update_git_repo("https://example.invalid/repo.git", REPO_TARGET_DIR, logger)

    repo_mock.remotes.origin.pull.assert_called_once_with()


def test_update_git_repo_passes_depth_for_pull_when_set() -> None:
    logger: logging.Logger = logging.getLogger("git-helper-tests")
    repo_mock: Mock = Mock()
    repo_mock.remotes.origin.pull = Mock()

    with (
        patch("scripts.customizing.fwo_custom_lib.git_helpers.Path.exists", return_value=True),
        patch("scripts.customizing.fwo_custom_lib.git_helpers.git.Repo", return_value=repo_mock),
    ):
        update_git_repo("https://example.invalid/repo.git", REPO_TARGET_DIR, logger, depth=UPDATED_DEPTH)

    repo_mock.remotes.origin.pull.assert_called_once_with(depth=UPDATED_DEPTH)


def test_update_git_repo_omits_depth_for_clone_when_not_set() -> None:
    logger: logging.Logger = logging.getLogger("git-helper-tests")

    with (
        patch("scripts.customizing.fwo_custom_lib.git_helpers.Path.exists", return_value=False),
        patch("scripts.customizing.fwo_custom_lib.git_helpers.git.Repo.clone_from") as clone_from_mock,
    ):
        update_git_repo("https://example.invalid/repo.git", REPO_TARGET_DIR, logger, branch="main")

    clone_from_mock.assert_called_once_with("https://example.invalid/repo.git", REPO_TARGET_DIR, branch="main")


def test_update_git_repo_passes_depth_for_clone_when_set() -> None:
    logger: logging.Logger = logging.getLogger("git-helper-tests")

    with (
        patch("scripts.customizing.fwo_custom_lib.git_helpers.Path.exists", return_value=False),
        patch("scripts.customizing.fwo_custom_lib.git_helpers.git.Repo.clone_from") as clone_from_mock,
    ):
        update_git_repo("https://example.invalid/repo.git", REPO_TARGET_DIR, logger, branch="main", depth=CLONE_DEPTH)

    clone_from_mock.assert_called_once_with(
        "https://example.invalid/repo.git",
        REPO_TARGET_DIR,
        branch="main",
        depth=CLONE_DEPTH,
    )

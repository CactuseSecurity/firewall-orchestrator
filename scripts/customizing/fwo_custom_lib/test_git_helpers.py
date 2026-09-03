import logging
from pathlib import Path
from typing import cast
from unittest.mock import ANY, Mock, patch

import git
import pytest

from scripts.customizing.fwo_custom_lib.git_helpers import (
    FALLBACK_COMMITTER_EMAIL,
    FALLBACK_COMMITTER_NAME,
    build_askpass_env,
    build_non_interactive_git_env,
    cleanup_repo_target_dir,
    commit_and_push_deletions,
    ensure_committer_identity,
    parse_git_depth_arg,
    read_file_from_git_repo,
    rebase_onto_remote,
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
    clone_from_mock.assert_called_once_with("https://example.invalid/repo.git", REPO_TARGET_DIR, env=ANY)


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
    clone_from_mock.assert_called_once_with(
        "https://example.invalid/repo.git", REPO_TARGET_DIR, env=ANY, depth=UPDATED_DEPTH
    )


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
    clone_from_mock.assert_called_once_with("https://example.invalid/repo.git", REPO_TARGET_DIR, env=ANY, branch="main")


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
        env=ANY,
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
    clone_environment: dict[str, str] = clone_from_mock.call_args.kwargs["env"]
    assert "GIT_ASKPASS" not in clone_environment
    # incomplete credentials must not turn the clone into an interactive one: git asking for the
    # missing half would block the calling import until the middleware is restarted
    assert clone_environment["GIT_TERMINAL_PROMPT"] == "0"
    assert clone_environment["GIT_CONFIG_PARAMETERS"] == "'credential.helper='"
    assert clone_environment["SSH_ASKPASS_REQUIRE"] == "never"


def test_build_askpass_env_keeps_the_clone_non_interactive(tmp_path: Path) -> None:
    askpass_environment: dict[str, str] = build_askpass_env(str(tmp_path), "git-user", "git-secret")

    assert askpass_environment["GIT_ASKPASS_USERNAME"] == "git-user"
    assert askpass_environment["GIT_ASKPASS_PASSWORD"] == "git-secret"
    assert askpass_environment["GIT_TERMINAL_PROMPT"] == "0"
    assert askpass_environment["GIT_CONFIG_PARAMETERS"] == "'credential.helper='"


def test_build_non_interactive_git_env_keeps_a_configured_ssh_command(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("GIT_SSH_COMMAND", "ssh -i /etc/fworch/secrets/git_key")

    assert build_non_interactive_git_env()["GIT_SSH_COMMAND"] == "ssh -i /etc/fworch/secrets/git_key"


def test_build_non_interactive_git_env_disables_ssh_prompts_without_a_configured_command(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.delenv("GIT_SSH_COMMAND", raising=False)

    assert build_non_interactive_git_env()["GIT_SSH_COMMAND"] == "ssh -o BatchMode=yes"


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


def test_commit_and_push_deletions_retries_an_existing_local_commit(tmp_path: Path) -> None:
    clone_path, clone, origin = create_clone_with_origin(tmp_path)
    origin_url: str = str(origin.working_dir)
    clone.delete_remote(clone.remote("origin"))
    first_push: bool = commit_and_push_deletions(str(clone_path), [clone_path / "logs.csv"], COMMIT_MESSAGE, LOGGER)
    clone.create_remote("origin", origin_url)

    second_push: bool = commit_and_push_deletions(str(clone_path), [clone_path / "logs.csv"], COMMIT_MESSAGE, LOGGER)

    assert not first_push
    assert second_push
    assert "logs.csv" not in origin.head.commit.tree


def create_shallow_clone(tmp_path: Path) -> tuple[Path, git.Repo]:
    """Clone the sample repository with a truncated history, as the --depth argument does."""
    create_clone_with_origin(tmp_path)
    # a second commit gives the shallow clone something to be shallow about
    seed: git.Repo = git.Repo(tmp_path / "seed")
    (tmp_path / "seed" / "second.csv").write_text("App ID,Log count\nAPP-2,2\n", encoding="utf-8")
    seed.git.add("second.csv")
    seed.index.commit("chore: add more sample log data")
    seed.git.push("origin", "HEAD:refs/heads/main")

    shallow_path: Path = tmp_path / "shallow"
    # git ignores --depth for a plain local path, the file:// url makes it a real shallow clone
    shallow: git.Repo = git.Repo.clone_from(
        f"file://{tmp_path / 'origin.git'}", str(shallow_path), branch="main", depth=1
    )
    configure_identity(shallow)
    assert shallow.git.rev_parse("--is-shallow-repository").strip() == "true"
    return shallow_path, shallow


def test_commit_and_push_deletions_completes_a_shallow_clone_before_pushing(tmp_path: Path) -> None:
    shallow_path, shallow = create_shallow_clone(tmp_path)

    pushed: bool = commit_and_push_deletions(
        str(shallow_path), [shallow_path / "second.csv"], COMMIT_MESSAGE, LOGGER, "user", "password"
    )

    assert pushed
    assert shallow.git.rev_parse("--is-shallow-repository").strip() == "false"
    assert "second.csv" not in git.Repo(tmp_path / "origin.git").head.commit.tree


def test_commit_and_push_deletions_completes_a_shallow_clone_without_credentials(tmp_path: Path) -> None:
    # a repository which needs no credentials is pushed through the same preparation
    shallow_path, shallow = create_shallow_clone(tmp_path)

    pushed: bool = commit_and_push_deletions(str(shallow_path), [shallow_path / "second.csv"], COMMIT_MESSAGE, LOGGER)

    assert pushed
    assert shallow.git.rev_parse("--is-shallow-repository").strip() == "false"
    assert "second.csv" not in git.Repo(tmp_path / "origin.git").head.commit.tree


def remove_configured_identity(repo: git.Repo) -> None:
    with repo.config_writer() as config:
        config.remove_option("user", "name")
        config.remove_option("user", "email")


def isolate_from_host_git_config(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    """Make the test see the unattended host the middleware runs on: git without an identity."""
    home_path: Path = tmp_path / "home"
    home_path.mkdir()
    monkeypatch.setenv("HOME", str(home_path))  # Linux path
    monkeypatch.setenv("USERPROFILE", str(home_path))  # Windows path
    monkeypatch.setenv("XDG_CONFIG_HOME", str(home_path / ".config"))
    monkeypatch.setenv("GIT_CONFIG_NOSYSTEM", "1")
    for identity_variable in ("GIT_AUTHOR_NAME", "GIT_AUTHOR_EMAIL", "GIT_COMMITTER_NAME", "GIT_COMMITTER_EMAIL"):
        monkeypatch.delenv(identity_variable, raising=False)


def test_commit_and_push_deletions_commits_without_a_configured_identity(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    # the deletion is rebased onto the moved remote, which git refuses to do without a committer
    clone_path, clone, origin = create_clone_with_origin(tmp_path)
    seed_path: Path = tmp_path / "seed"
    seed: git.Repo = git.Repo(seed_path)
    (seed_path / "new-export.csv").write_text("App ID,Log count\nAPP-3,3\n", encoding="utf-8")
    seed.git.add("new-export.csv")
    seed.index.commit("chore: add new log data export")
    seed.git.push("origin", "HEAD:refs/heads/main")
    remove_configured_identity(clone)
    isolate_from_host_git_config(tmp_path, monkeypatch)

    pushed: bool = commit_and_push_deletions(str(clone_path), [clone_path / "logs.csv"], COMMIT_MESSAGE, LOGGER)

    assert pushed
    assert clone.head.commit.committer.email == FALLBACK_COMMITTER_EMAIL
    assert clone.head.commit.committer.name == FALLBACK_COMMITTER_NAME
    assert "logs.csv" not in origin.head.commit.tree


def test_ensure_committer_identity_keeps_a_configured_one(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    _, clone, _ = create_clone_with_origin(tmp_path)
    isolate_from_host_git_config(tmp_path, monkeypatch)

    ensure_committer_identity(clone, LOGGER)

    with clone.config_reader() as config:
        assert config.get_value("user", "email") == "test@local"
        assert config.get_value("user", "name") == "FWO Test"


def test_rebase_onto_remote_reports_the_rebase_error_when_there_is_nothing_to_abort() -> None:
    repo_mock: Mock = Mock()
    repo_mock.active_branch.name = "main"
    repo_mock.git.diff.return_value = ""
    rebase_error: git.GitCommandError = git.GitCommandError("git rebase FETCH_HEAD", 1, b"could not apply the deletion")
    abort_error: git.GitCommandError = git.GitCommandError("git rebase --abort", 128, b"fatal: No rebase in progress?")

    def fail_rebase(*args: str, **_: object) -> None:
        raise abort_error if "--abort" in args else rebase_error

    repo_mock.git.rebase.side_effect = fail_rebase

    with pytest.raises(git.GitCommandError) as raised_error:
        rebase_onto_remote(repo_mock, LOGGER, {})

    # the reason the rebase failed must survive, the failing abort would otherwise replace it
    assert raised_error.value is rebase_error


def test_commit_and_push_deletions_replays_onto_a_moved_remote(tmp_path: Path) -> None:
    clone_path, clone, origin = create_clone_with_origin(tmp_path)
    # the exporter adds a new file after the import cloned the repository
    seed_path: Path = tmp_path / "seed"
    seed: git.Repo = git.Repo(seed_path)
    (seed_path / "new-export.csv").write_text("App ID,Log count\nAPP-3,3\n", encoding="utf-8")
    seed.git.add("new-export.csv")
    seed.index.commit("chore: add new log data export")
    seed.git.push("origin", "HEAD:refs/heads/main")

    pushed: bool = commit_and_push_deletions(str(clone_path), [clone_path / "logs.csv"], COMMIT_MESSAGE, LOGGER)

    assert pushed
    assert "logs.csv" not in origin.head.commit.tree
    assert "new-export.csv" in origin.head.commit.tree, "the export added meanwhile survives"
    assert clone.head.commit.message.strip() == COMMIT_MESSAGE


def append_to_exported_file(tmp_path: Path, file_name: str, line: str) -> None:
    """Let the exporter write more rows into a file which was already imported."""
    seed_path: Path = tmp_path / "seed"
    seed: git.Repo = git.Repo(seed_path)
    with (seed_path / file_name).open("a", encoding="utf-8") as exported_file:
        exported_file.write(line)
    seed.git.add(file_name)
    seed.index.commit("chore: append more log data")
    seed.git.push("origin", "HEAD:refs/heads/main")


def test_commit_and_push_deletions_keeps_a_file_written_to_after_the_import(tmp_path: Path) -> None:
    clone_path, _, origin = create_clone_with_origin(tmp_path)
    append_to_exported_file(tmp_path, "logs.csv", "APP-2,2\n")

    pushed: bool = commit_and_push_deletions(str(clone_path), [clone_path / "logs.csv"], COMMIT_MESSAGE, LOGGER)

    assert pushed, "a deterministic conflict must not stall the acknowledgement forever"
    assert "logs.csv" in origin.head.commit.tree, "the rows appended after the import are not deleted"
    log_data: bytes = cast("bytes", origin.head.commit.tree["logs.csv"].data_stream.read())
    assert "APP-2,2" in log_data.decode("utf-8")


def test_commit_and_push_deletions_deletes_the_untouched_files_beside_a_kept_one(tmp_path: Path) -> None:
    clone_path, _, origin = create_clone_with_origin(tmp_path)
    seed_path: Path = tmp_path / "seed"
    seed: git.Repo = git.Repo(seed_path)
    (seed_path / "second.csv").write_text("App ID,Log count\nAPP-9,9\n", encoding="utf-8")
    seed.git.add("second.csv")
    seed.index.commit("chore: add a second export")
    seed.git.push("origin", "HEAD:refs/heads/main")
    clone_path_repo: git.Repo = git.Repo(clone_path)
    clone_path_repo.git.pull("origin", "main")
    append_to_exported_file(tmp_path, "logs.csv", "APP-2,2\n")

    pushed: bool = commit_and_push_deletions(
        str(clone_path), [clone_path / "logs.csv", clone_path / "second.csv"], COMMIT_MESSAGE, LOGGER
    )

    assert pushed
    assert "logs.csv" in origin.head.commit.tree
    assert "second.csv" not in origin.head.commit.tree, "a file nobody wrote to is still acknowledged"


def test_commit_and_push_deletions_reports_only_the_files_it_removed(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    clone_path, _, _ = create_clone_with_origin(tmp_path)
    append_to_exported_file(tmp_path, "logs.csv", "APP-2,2\n")

    with caplog.at_level(logging.INFO, logger=LOGGER.name):
        pushed: bool = commit_and_push_deletions(str(clone_path), [clone_path / "logs.csv"], COMMIT_MESSAGE, LOGGER)

    assert pushed
    assert "deleted and pushed log data files: none" in caplog.text, "a kept file must not be reported as deleted"
    assert "keeping logs.csv" in caplog.text

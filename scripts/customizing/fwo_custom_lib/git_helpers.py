import logging
import os
import shutil
import stat
import tempfile
from pathlib import Path
from typing import Any
from urllib.parse import unquote, urlsplit, urlunsplit

import git

# git must fail instead of waiting for an answer nobody can give it: the scripts run unattended,
# so a credential prompt would block the calling import until the middleware is restarted.
# Configured credential helpers are switched off for the same reason, one of them could wait for
# an unlock which never comes, and ssh is told not to ask for a password or a host key either.
NON_INTERACTIVE_GIT_SETTINGS: dict[str, str] = {
    "GIT_TERMINAL_PROMPT": "0",
    "GIT_CONFIG_PARAMETERS": "'credential.helper='",
    "SSH_ASKPASS_REQUIRE": "never",
    # continuing a rebase would open an editor for the commit message and wait for it forever
    "GIT_EDITOR": "true",
}
DEFAULT_GIT_SSH_COMMAND: str = "ssh -o BatchMode=yes"

# committing and rebasing need a committer identity. The repository is cloned fresh in every run
# and an unattended host often has no identity configured at all, which would make git refuse to
# create the deletion commit and let every acknowledgement fail.
FALLBACK_COMMITTER_NAME: str = "FWO Log Data Import"
FALLBACK_COMMITTER_EMAIL: str = "log-data-import@fworch.local"


def parse_git_depth_arg(value: str) -> int:
    try:
        depth: int = int(value)
    except ValueError as err:
        raise ValueError(f"invalid git depth value: {value}") from err
    if depth <= 0:
        raise ValueError(f"invalid git depth value: {value}")
    return depth


def _remove_repo_target_path(repo_target_path: Path) -> None:
    if not repo_target_path.exists():
        return
    if repo_target_path.is_dir():
        shutil.rmtree(repo_target_path)
        return
    repo_target_path.unlink()


def cleanup_repo_target_dir(git_repo_target_dir: str) -> None:
    repo_target_path: Path = Path(git_repo_target_dir)
    _remove_repo_target_path(repo_target_path)


def split_repo_url_credentials(repo_url: str) -> tuple[str, str | None, str | None]:
    parsed_url = urlsplit(repo_url)
    sanitized_netloc = parsed_url.netloc.rsplit("@", 1)[-1]
    sanitized_url = urlunsplit(
        (parsed_url.scheme, sanitized_netloc, parsed_url.path, parsed_url.query, parsed_url.fragment)
    )
    username = unquote(parsed_url.username or "")
    password = unquote(parsed_url.password or "")
    if not username or not password:
        return sanitized_url, None, None

    return sanitized_url, username, password


def create_git_askpass_script(directory: str) -> str:
    script_path = Path(directory) / "git-askpass.sh"
    script_path.write_text(
        "#!/bin/sh\n"
        'case "$1" in\n'
        "*Username*|*username*) printf '%s\\n' \"$GIT_ASKPASS_USERNAME\" ;;\n"
        "*) printf '%s\\n' \"$GIT_ASKPASS_PASSWORD\" ;;\n"
        "esac\n",
        encoding="utf-8",
    )
    script_path.chmod(stat.S_IRUSR | stat.S_IWUSR | stat.S_IXUSR)
    return str(script_path)


def build_non_interactive_git_env() -> dict[str, str]:
    """Environment which makes git fail instead of waiting for input it cannot get."""
    env: dict[str, str] = {**os.environ, **NON_INTERACTIVE_GIT_SETTINGS}
    env["GIT_SSH_COMMAND"] = os.environ.get("GIT_SSH_COMMAND") or DEFAULT_GIT_SSH_COMMAND
    return env


def update_git_repo(
    repo_url: str,
    git_repo_target_dir: str,
    logger: logging.Logger,
    branch: str | None = None,
    depth: int | None = None,
) -> bool:
    repo_target_path: Path = Path(git_repo_target_dir)
    clone_url, git_username, git_password = split_repo_url_credentials(repo_url)
    try:
        git_any: Any = git
        _remove_repo_target_path(repo_target_path)
        repo_target_path.parent.mkdir(parents=True, exist_ok=True)

        clone_args: dict[str, str | int] = {}
        if branch:
            clone_args["branch"] = branch
        if depth is not None:
            clone_args["depth"] = depth
        if git_username is not None and git_password is not None:
            with tempfile.TemporaryDirectory() as askpass_dir:
                env: dict[str, str] = build_askpass_env(askpass_dir, git_username, git_password)
                git_any.Repo.clone_from(clone_url, git_repo_target_dir, env=env, **clone_args)
        else:
            # without credentials git would ask for them, which never terminates unattended
            git_any.Repo.clone_from(clone_url, git_repo_target_dir, env=build_non_interactive_git_env(), **clone_args)
        return True
    except Exception:
        _remove_repo_target_path(repo_target_path)
        logger.exception("could not clone/pull git repo from %s", clone_url)
        return False


def unshallow_repo(repo: Any, logger: logging.Logger, env: dict[str, str]) -> None:
    """Complete the history of a shallow clone, since servers reject pushes from one."""
    if repo.git.rev_parse("--is-shallow-repository").strip() != "true":
        return
    logger.info("completing shallow clone before pushing")
    repo.git.fetch("--unshallow", env=env)


def rebase_onto_remote(repo: Any, logger: logging.Logger, env: dict[str, str]) -> None:
    """
    Replay the local deletion on top of the remote branch.

    The exporter keeps writing to the log data repository, so the remote almost always moved on
    between cloning and acknowledging. Without this the push is rejected as non fast forward and
    the same data would be imported again in every following run.
    """
    branch: str = repo.active_branch.name
    repo.git.fetch("origin", branch, env=env)
    try:
        repo.git.rebase("FETCH_HEAD", env=env)
    except git.GitCommandError:
        if keep_files_changed_after_import(repo, logger, env):
            return
        abort_rebase(repo, logger)
        logger.exception("could not replay the log data deletion onto %s", branch)
        raise


def keep_files_changed_after_import(repo: Any, logger: logging.Logger, env: dict[str, str]) -> bool:
    """
    Resolve a conflicting deletion by keeping the file the exporter wrote to after it was imported.

    A CSV file which was appended to between cloning and acknowledging conflicts with its own
    deletion, and that conflict is reproduced by every following run: the pending import is reused
    instead of cloning again, so nothing about the situation ever changes and no new log data is
    imported. Deleting the file anyway would lose the rows appended after the import, so the file
    survives the acknowledgement and is imported again in the next run - a flow reported twice is
    merged with the stored one. Everything else is still deleted.
    """
    try:
        conflicting_files: list[str] = [
            file_name for file_name in repo.git.diff("--name-only", "--diff-filter=U").splitlines() if file_name
        ]
        if not conflicting_files:
            return False

        for file_name in conflicting_files:
            # inside a rebase 'ours' is the fetched state of the log data repository
            repo.git.checkout("--ours", "--", file_name)
            repo.git.add("--", file_name)
        logger.warning(
            "keeping %s: changed in the log data repository after it was imported, it is imported again next run",
            ", ".join(conflicting_files),
        )
        continue_rebase(repo, env)
        return True
    except git.GitCommandError:
        logger.exception("could not keep the log data files which changed after they were imported")
        return False


def continue_rebase(repo: Any, env: dict[str, str]) -> None:
    """Finish a rebase whose conflicts were resolved, skipping a deletion nothing is left of."""
    if repo.git.diff("--cached", "--name-only", "HEAD").strip():
        repo.git.rebase("--continue", env=env)
        return
    repo.git.rebase("--skip", env=env)


def abort_rebase(repo: Any, logger: logging.Logger) -> None:
    """Return the clone to the state before the rebase, without hiding why the rebase failed."""
    try:
        repo.git.rebase("--abort")
    except git.GitCommandError:
        # a rebase which never started cannot be aborted, and that error must not replace the
        # reason the rebase failed - a rejected fetch or an unreachable remote for instance
        logger.debug("no rebase to abort", exc_info=True)


def ensure_committer_identity(repo: Any, logger: logging.Logger) -> None:
    """Give the clone a committer identity unless the host provides one itself."""
    with repo.config_reader() as config:
        name: str = str(config.get_value("user", "name", "") or "")
        email: str = str(config.get_value("user", "email", "") or "")
    if name and email:
        return

    with repo.config_writer() as config:
        config.set_value("user", "name", name or FALLBACK_COMMITTER_NAME)
        config.set_value("user", "email", email or FALLBACK_COMMITTER_EMAIL)
    logger.info("no git committer identity configured, committing as %s", FALLBACK_COMMITTER_EMAIL)


def build_askpass_env(askpass_dir: str, git_username: str, git_password: str) -> dict[str, str]:
    """Environment which lets git read the credentials without a terminal prompt."""
    return {
        **build_non_interactive_git_env(),
        "GIT_ASKPASS": create_git_askpass_script(askpass_dir),
        "GIT_ASKPASS_USERNAME": git_username,
        "GIT_ASKPASS_PASSWORD": git_password,
    }


def push_deletion_commit(repo: Any, logger: logging.Logger, env: dict[str, str]) -> None:
    """
    Bring the clone into a pushable state and push the deletion commit.

    Every push runs through here, with or without credentials: a shallow clone and a remote which
    moved on since cloning make git reject the push either way.
    """
    unshallow_repo(repo, logger, env)
    rebase_onto_remote(repo, logger, env)
    repo.git.push("origin", "HEAD", env=env)


def report_pushed_deletions(repo_path: Path, relative_paths: list[str], logger: logging.Logger) -> None:
    """Report which files the acknowledgement removed, a file kept by a conflict is still there."""
    deleted_paths: list[str] = [file_name for file_name in relative_paths if not (repo_path / file_name).exists()]
    logger.info("deleted and pushed log data files: %s", ", ".join(deleted_paths) or "none")


def commit_and_push_deletions(
    git_repo_target_dir: str,
    files_to_delete: list[Path],
    commit_message: str,
    logger: logging.Logger,
    git_username: str | None = None,
    git_password: str | None = None,
) -> bool:
    """Delete tracked files, commit their removal and push it to origin."""
    try:
        repo: Any = git.Repo(git_repo_target_dir)
        relative_paths: list[str] = []
        repo_path: Path = Path(git_repo_target_dir).resolve()
        for file_path in files_to_delete:
            resolved_path: Path = file_path.resolve()
            relative_paths.append(str(resolved_path.relative_to(repo_path)))
            resolved_path.unlink(missing_ok=True)
        repo.git.add(update=True)
        ensure_committer_identity(repo, logger)
        if repo.is_dirty(index=True, working_tree=True, untracked_files=False):
            repo.index.commit(commit_message)
        if git_username is not None and git_password is not None:
            with tempfile.TemporaryDirectory() as askpass_dir:
                push_deletion_commit(repo, logger, build_askpass_env(askpass_dir, git_username, git_password))
        else:
            # without credentials git could still ask for them, which never terminates unattended
            push_deletion_commit(repo, logger, build_non_interactive_git_env())
        report_pushed_deletions(repo_path, relative_paths, logger)
        return True
    except Exception:
        logger.exception("could not commit and push log data file deletions")
        return False


def read_file_from_git_repo(
    repo_url: str,
    git_repo_target_dir: str,
    relative_file_name: str,
    logger: logging.Logger,
    branch: str | None = None,
    depth: int | None = None,
) -> str:
    file_as_text: str = ""
    absolute_target_file_name: str = f"{git_repo_target_dir}/{relative_file_name}"
    repo_target_path: Path = Path(git_repo_target_dir)

    try:
        repo_updated = update_git_repo(repo_url, git_repo_target_dir, logger, branch=branch, depth=depth)
        if repo_updated:
            try:
                with open(absolute_target_file_name, encoding="utf-8") as f:
                    file_as_text = f.read()
            except Exception:
                logger.exception("could not read file %s", absolute_target_file_name)
    finally:
        _remove_repo_target_path(repo_target_path)

    if not file_as_text:
        logger.info("no data loaded from file %s", absolute_target_file_name)

    return file_as_text

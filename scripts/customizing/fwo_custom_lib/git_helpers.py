import logging
import os
import shutil
import stat
import tempfile
from pathlib import Path
from typing import Any
from urllib.parse import unquote, urlsplit, urlunsplit

import git


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
                env = {
                    **os.environ,
                    "GIT_ASKPASS": create_git_askpass_script(askpass_dir),
                    "GIT_ASKPASS_USERNAME": git_username,
                    "GIT_ASKPASS_PASSWORD": git_password,
                    "GIT_TERMINAL_PROMPT": "0",
                }
                git_any.Repo.clone_from(clone_url, git_repo_target_dir, env=env, **clone_args)
        else:
            git_any.Repo.clone_from(clone_url, git_repo_target_dir, **clone_args)
        return True
    except Exception:
        _remove_repo_target_path(repo_target_path)
        logger.exception("could not clone/pull git repo from %s", clone_url)
        return False


def unshallow_repo(repo: Any, logger: logging.Logger, git_username: str, git_password: str) -> None:
    """Complete the history of a shallow clone, since servers reject pushes from one."""
    if repo.git.rev_parse("--is-shallow-repository").strip() != "true":
        return
    logger.info("completing shallow clone before pushing")
    with tempfile.TemporaryDirectory() as askpass_dir:
        repo.git.fetch("--unshallow", env=build_askpass_env(askpass_dir, git_username, git_password))


def rebase_onto_remote(repo: Any, logger: logging.Logger, env: dict[str, str] | None) -> None:
    """
    Replay the local deletion on top of the remote branch.

    The exporter keeps writing to the log data repository, so the remote almost always moved on
    between cloning and acknowledging. Without this the push is rejected as non fast forward and
    the same data would be imported again in every following run.
    """
    branch: str = repo.active_branch.name
    if env is None:
        repo.git.fetch("origin", branch)
    else:
        repo.git.fetch("origin", branch, env=env)
    try:
        repo.git.rebase("FETCH_HEAD")
    except git.GitCommandError:
        repo.git.rebase("--abort")
        logger.exception("could not replay the log data deletion onto %s", branch)
        raise


def build_askpass_env(askpass_dir: str, git_username: str, git_password: str) -> dict[str, str]:
    """Environment which lets git read the credentials without a terminal prompt."""
    return {
        **os.environ,
        "GIT_ASKPASS": create_git_askpass_script(askpass_dir),
        "GIT_ASKPASS_USERNAME": git_username,
        "GIT_ASKPASS_PASSWORD": git_password,
        "GIT_TERMINAL_PROMPT": "0",
    }


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
        if repo.is_dirty(index=True, working_tree=True, untracked_files=False):
            repo.index.commit(commit_message)
        if git_username is not None and git_password is not None:
            unshallow_repo(repo, logger, git_username, git_password)
            with tempfile.TemporaryDirectory() as askpass_dir:
                env: dict[str, str] = build_askpass_env(askpass_dir, git_username, git_password)
                rebase_onto_remote(repo, logger, env)
                repo.git.push("origin", "HEAD", env=env)
        else:
            rebase_onto_remote(repo, logger, None)
            repo.git.push("origin", "HEAD")
        logger.info("deleted and pushed log data files: %s", ", ".join(relative_paths))
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

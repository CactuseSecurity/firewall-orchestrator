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
    if not parsed_url.username and not parsed_url.password:
        return repo_url, None, None

    sanitized_netloc = parsed_url.netloc.rsplit("@", 1)[-1]
    sanitized_url = urlunsplit(
        (parsed_url.scheme, sanitized_netloc, parsed_url.path, parsed_url.query, parsed_url.fragment)
    )
    return sanitized_url, unquote(parsed_url.username or ""), unquote(parsed_url.password or "")


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

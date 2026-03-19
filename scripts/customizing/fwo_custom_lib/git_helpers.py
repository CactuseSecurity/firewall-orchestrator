import logging
from pathlib import Path
from typing import Any

import git


def parse_git_depth_arg(value: str) -> int:
    try:
        depth: int = int(value)
    except ValueError as err:
        raise ValueError(f"invalid git depth value: {value}") from err
    if depth <= 0:
        raise ValueError(f"invalid git depth value: {value}")
    return depth


def update_git_repo(
    repo_url: str,
    git_repo_target_dir: str,
    logger: logging.Logger,
    branch: str | None = None,
    depth: int | None = None,
) -> bool:
    try:
        git_any: Any = git
        if Path(git_repo_target_dir).exists():
            # If the repository already exists, open it and perform a pull
            repo: Any = git_any.Repo(git_repo_target_dir)
            if branch:
                repo.git.checkout(branch)
            origin: Any = repo.remotes.origin
            pull_args: dict[str, int] = {}
            if depth is not None:
                pull_args["depth"] = depth
            origin.pull(**pull_args)
        # clone the repo initially
        elif branch:
            clone_args: dict[str, str | int] = {"branch": branch}
            if depth is not None:
                clone_args["depth"] = depth
            repo = git_any.Repo.clone_from(repo_url, git_repo_target_dir, **clone_args)
        else:
            clone_args = {}
            if depth is not None:
                clone_args["depth"] = depth
            repo = git_any.Repo.clone_from(repo_url, git_repo_target_dir, **clone_args)
        return True
    except Exception:
        logger.exception("could not clone/pull git repo from %s", repo_url)
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

    repo_updated = update_git_repo(repo_url, git_repo_target_dir, logger, branch=branch, depth=depth)
    if repo_updated:
        try:
            with open(absolute_target_file_name, encoding="utf-8") as f:
                file_as_text = f.read()
        except Exception:
            logger.exception("could not read file %s", absolute_target_file_name)

    if not file_as_text:
        logger.info("no data loaded from file %s", absolute_target_file_name)

    return file_as_text

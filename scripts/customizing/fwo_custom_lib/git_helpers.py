import logging
import shutil
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


def update_git_repo(
    repo_url: str,
    git_repo_target_dir: str,
    logger: logging.Logger,
    branch: str | None = None,
    depth: int | None = None,
) -> bool:
    repo_target_path: Path = Path(git_repo_target_dir)
    try:
        git_any: Any = git
        _remove_repo_target_path(repo_target_path)
        repo_target_path.parent.mkdir(parents=True, exist_ok=True)

        clone_args: dict[str, str | int] = {}
        if branch:
            clone_args["branch"] = branch
        if depth is not None:
            clone_args["depth"] = depth
        git_any.Repo.clone_from(repo_url, git_repo_target_dir, **clone_args)
        return True
    except Exception:
        _remove_repo_target_path(repo_target_path)
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

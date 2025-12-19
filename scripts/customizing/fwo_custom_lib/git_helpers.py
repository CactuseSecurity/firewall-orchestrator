import logging
from pathlib import Path

import git


def update_git_repo(
    repo_url: str,
    git_repo_target_dir: str,
    logger: logging.Logger,
    branch: str | None = None,
) -> bool:
    try:
        if Path(git_repo_target_dir).exists():
            # If the repository already exists, open it and perform a pull
            repo: git.Repo = git.Repo(git_repo_target_dir)
            if branch:
                repo.git.checkout(branch)
            origin: git.Remote = repo.remotes.origin
            origin.pull()
        # clone the repo initially
        elif branch:
            repo = git.Repo.clone_from(repo_url, git_repo_target_dir, branch=branch)
        else:
            repo = git.Repo.clone_from(repo_url, git_repo_target_dir)
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
) -> str:
    file_as_text: str = ""
    absolute_target_file_name: str = f"{git_repo_target_dir}/{relative_file_name}"

    repo_updated = update_git_repo(repo_url, git_repo_target_dir, logger, branch=branch)
    if repo_updated:
        try:
            with open(absolute_target_file_name, encoding="utf-8") as f:
                file_as_text = f.read()
        except Exception:
            logger.exception("could not read file %s", absolute_target_file_name)

    if not file_as_text:
        logger.info("no data loaded from file %s", absolute_target_file_name)

    return file_as_text

import os
import traceback
import git


def update_git_repo(repo_url, git_repo_target_dir, logger, branch=None):
    try:
        if os.path.exists(git_repo_target_dir):
            # If the repository already exists, open it and perform a pull
            repo = git.Repo(git_repo_target_dir)
            if branch:
                repo.git.checkout(branch)
            origin = repo.remotes.origin
            origin.pull()
        else:
            # clone the repo initially
            clone_kwargs = {"branch": branch} if branch else {}
            repo = git.Repo.clone_from(repo_url, git_repo_target_dir, **clone_kwargs)
        return True
    except Exception:
        logger.warning("could not clone/pull git repo from " + repo_url + ", exception: " + str(traceback.format_exc()))
        return False


def read_file_from_git_repo(repo_url, git_repo_target_dir, relative_file_name, logger, branch=None):
    file_as_text = ""
    absolute_target_file_name = f"{git_repo_target_dir}/{relative_file_name}"

    repo_updated = update_git_repo(repo_url, git_repo_target_dir, logger, branch=branch)
    if repo_updated:
        try:
            with open(absolute_target_file_name, "r") as f:
                file_as_text = f.read()
        except Exception:
            logger.warning(f"could not read file {absolute_target_file_name}, exception: " + str(traceback.format_exc()))

    if not file_as_text:
        logger.info("no data loaded from file " + absolute_target_file_name)

    return file_as_text

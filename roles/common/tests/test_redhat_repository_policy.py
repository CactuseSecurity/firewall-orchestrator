from pathlib import Path
from typing import Any

import yaml


def load_tasks() -> list[dict[str, Any]]:
    task_file = Path(__file__).parents[1] / "tasks" / "redhat_preps.yml"
    return yaml.safe_load(task_file.read_text(encoding="utf-8"))


def find_task(tasks: list[dict[str, Any]], name: str) -> dict[str, Any]:
    for task in tasks:
        if task.get("name") == name:
            return task
        for block_name in ("block", "rescue", "always"):
            nested_tasks = task.get(block_name, [])
            if nested_tasks:
                try:
                    return find_task(nested_tasks, name)
                except LookupError:
                    pass
    raise LookupError(name)


def test_package_availability_is_checked_before_repository_changes() -> None:
    tasks = load_tasks()
    initial_check_index = next(
        index
        for index, task in enumerate(tasks)
        if task.get("name") == "check required RedHat packages before optional repository changes"
    )
    repository_change_index = next(
        index
        for index, task in enumerate(tasks)
        if task.get("name") == "optionally enable required RedHat repositories"
    )

    assert initial_check_index < repository_change_index


def test_existing_epel_repository_skips_epel_release_package_check() -> None:
    tasks = load_tasks()
    epel_install_block = find_task(tasks, "optionally install EPEL when no EPEL repository is enabled")

    assert epel_install_block["when"] == "not redhat_epel_repo_enabled | bool"
    assert find_task(epel_install_block["block"], "check if EPEL release package is installed")


def test_repository_changes_require_missing_packages_and_explicit_permission() -> None:
    tasks = load_tasks()
    repository_change_block = find_task(tasks, "optionally enable required RedHat repositories")

    assert "allowRepoChangesForRedhat | default(false) | bool" in repository_change_block["when"]
    assert any(
        "redhat_repo_policy_initial_package_checks.results" in condition
        for condition in repository_change_block["when"]
    )


def test_cache_refresh_requires_an_actual_repository_change() -> None:
    tasks = load_tasks()
    cache_refresh = find_task(tasks, "update operating system package cache after RedHat repository changes")

    assert cache_refresh["when"] == "redhat_repo_changes_made | bool"


def test_distributed_frontends_and_middleware_packages_are_prechecked() -> None:
    expression = find_task(load_tasks(), "define RedHat packages that may require external repository enablement")[
        "set_fact"
    ]["redhat_repo_policy_packages"]

    assert "groups['frontends']" in expression
    assert "dotnet-sdk-" in expression
    assert "chromium-headless" in expression
    assert "groups['middlewareserver']" in expression
    assert "python_venv_package_name" in expression

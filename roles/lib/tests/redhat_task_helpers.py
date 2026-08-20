"""Helpers shared by the tests of the RedHat package installation tasks of the lib role."""

from pathlib import Path
from typing import Any, cast

import yaml

REPO_ROOT = Path(__file__).parents[3]
TASK_DIR = Path(__file__).parents[1] / "tasks" / "os"

Task = dict[str, Any]

NESTED_SECTIONS = ("block", "rescue", "always")

# the errors a hardened RHEL 9 host answers with. the reported install failed with the first one,
# because the configured repos resolved the packages but could not deliver them, and then with the
# second one, because the retry refreshed the metadata of an unrelated third party repo
PACKAGE_DOWNLOAD_ERROR = (
    "Failed to download packages: aspnetcore-runtime-10.0-10.0.10-1.el9_8.x86_64: "
    "Cannot download, all mirrors were already tried without success"
)
REPO_METADATA_ERROR = (
    "Failed to download metadata for repo 'epel': Cannot download repomd.xml: "
    "Cannot download repodata/repomd.xml: All mirrors were tried"
)
MISSING_PACKAGE_ERROR = "No package dotnet-sdk-10.0 available."
TRANSIENT_ERROR = "Curl error (28): Timeout was reached for https://mirror.example/repodata"

EXPECTED_RETRIES = 3


def load_tasks(file_name: str) -> list[Task]:
    return cast("list[Task]", yaml.safe_load((TASK_DIR / file_name).read_text(encoding="utf-8")))


def load_group_vars() -> dict[str, Any]:
    group_vars_file = REPO_ROOT / "inventory" / "group_vars" / "all.yml"
    return cast("dict[str, Any]", yaml.safe_load(group_vars_file.read_text(encoding="utf-8")))


def nested_tasks(task: Task, section: str) -> list[Task]:
    return cast("list[Task]", task.get(section) or [])


def flatten(tasks: list[Task]) -> list[Task]:
    flattened: list[Task] = []
    for task in tasks:
        flattened.append(task)
        for section in NESTED_SECTIONS:
            flattened.extend(flatten(nested_tasks(task, section)))
    return flattened


def task_name(task: Task) -> str:
    return cast("str", task.get("name", ""))


def find_task(tasks: list[Task], name: str) -> Task:
    for task in flatten(tasks):
        if task_name(task) == name:
            return task
    raise LookupError(name)


def find_task_containing(tasks: list[Task], name_part: str) -> Task:
    for task in flatten(tasks):
        if name_part in task_name(task):
            return task
    raise LookupError(name_part)


def set_fact_expression(task: Task, fact_name: str) -> str:
    return cast("str", cast("dict[str, Any]", task["set_fact"])[fact_name])


def set_fact_of(task: Task) -> dict[str, Any]:
    return cast("dict[str, Any]", task.get("set_fact") or {})


def rescue_names(tasks: list[Task]) -> list[list[str]]:
    return [
        [task_name(rescue_task) for rescue_task in nested_tasks(task, "rescue")]
        for task in flatten(tasks)
        if nested_tasks(task, "rescue")
    ]


def command_of(task: Task) -> str:
    return " ".join(str(task.get("command", "")).split())


def assert_error_is_recorded(task: Task, error_fact: str, chain_fact: str) -> None:
    """Assert a rescue keeps the first error, appends to the chain and flattens a list message."""
    # the first error describes the problem, the later ones can be about a repository that has
    # nothing to do with the package being installed, so the first one and all of them must survive
    last_error = set_fact_expression(task, error_fact)
    assert "ansible_failed_result" in last_error
    # the dnf module reports msg as a list often enough, and a list leaks its python repr into
    # both the failure message and the pattern matching that selects the hints
    assert "flatten | join(' ')" in last_error
    assert "| default(" in set_fact_expression(task, error_fact.replace("last", "first"))
    assert chain_fact + " | default([])" in set_fact_expression(task, chain_fact)


def assert_retry_is_pinned_to_the_repos_offering_the_packages(
    tasks: list[Task], repoquery_part: str, retry_part: str, repos_fact: str
) -> None:
    """Assert the retry only uses the repos that offer the packages and drops just the rpms."""
    repoquery = find_task_containing(tasks, repoquery_part)
    assert "repoquery" in command_of(repoquery)
    assert "%{repoid}" in command_of(repoquery)
    assert repoquery["failed_when"] is False

    # "dnf clean all" takes the metadata of every repo with it, so a third party repo the host
    # cannot reach fails the retry - and every later dnf task - instead of the packages
    for task in flatten(tasks):
        assert "clean all" not in command_of(task)
    clean = find_task(tasks, "drop the cached rpms on RedHat")
    assert command_of(clean) == "dnf clean packages"
    assert clean["changed_when"] is True

    retry = find_task_containing(tasks, retry_part)
    dnf_args = cast("dict[str, Any]", retry["ansible.builtin.dnf"])
    assert repos_fact in str(dnf_args["enablerepo"])
    assert repos_fact in str(dnf_args["disablerepo"])
    # refreshing the metadata only helps against a stale point release cache, and it may only be
    # done for the pinned repos - a global refresh is what a broken third party repo fails on
    assert repos_fact in str(dnf_args["update_cache"])
    # a permanent refusal answers every attempt the same way and is not worth the retries
    assert "redhat_repo_transient_failure_pattern" in retry["until"]
    assert retry["retries"] == EXPECTED_RETRIES


def assert_hints_are_selected_by_the_error(expression: str, error_fact: str) -> None:
    """Assert a hint selection covers all three failure classes and the point release hint."""
    for hint_variable in (
        "redhat_repo_metadata_failure_hints",
        "redhat_repo_download_failure_hints",
        "redhat_repo_missing_package_hints",
        "redhat_repo_cache_retry_hint",
    ):
        assert hint_variable in expression
    assert error_fact in expression

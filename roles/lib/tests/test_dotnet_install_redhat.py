from pathlib import Path
from typing import Any, cast

import yaml

REPO_ROOT = Path(__file__).parents[3]
TASK_DIR = Path(__file__).parents[1] / "tasks" / "os"

Task = dict[str, Any]

NESTED_SECTIONS = ("block", "rescue", "always")


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


def set_fact_expression(task: Task, fact_name: str) -> str:
    return cast("str", cast("dict[str, Any]", task["set_fact"])[fact_name])


def rescue_names(tasks: list[Task]) -> list[list[str]]:
    return [
        [task_name(rescue_task) for rescue_task in nested_tasks(task, "rescue")]
        for task in flatten(tasks)
        if nested_tasks(task, "rescue")
    ]


def test_every_dotnet_rescue_records_the_dnf_error_first() -> None:
    # the final message can only name the real cause if no nested rescue loses it on the way
    task_files = ("install_dot_net.RedHat.yml", "install_dot_net.RedHat.fallback.yml")
    for file_name in task_files:
        for names in rescue_names(load_tasks(file_name)):
            assert names[0].startswith("remember the dotnet installation error"), (file_name, names)


def test_recorded_error_is_reported_instead_of_the_ansible_failed_result() -> None:
    for file_name in ("install_dot_net.RedHat.yml", "install_dot_net.RedHat.fallback.yml"):
        for task in flatten(load_tasks(file_name)):
            set_fact = cast("dict[str, Any]", task.get("set_fact") or {})
            if "dotnet_install_last_error" in set_fact:
                assert "ansible_failed_result" in set_fact_expression(task, "dotnet_install_last_error")
            elif "debug" in task:
                assert "ansible_failed_result" not in str(task["debug"])


def test_failure_message_names_the_dnf_error_and_selects_matching_hints() -> None:
    tasks = load_tasks("install_dot_net.RedHat.script.yml")
    message = find_task(tasks, "collect the dotnet installation failure message on RedHat")
    rendered = set_fact_expression(message, "dotnet_install_failure_msg")

    assert "dotnet_install_last_error" in rendered
    assert "redhat_repo_download_failure_hints" in rendered
    assert "redhat_repo_missing_package_hints" in rendered
    assert "dotnet_install_failure_is_download" in rendered


def test_download_hints_are_only_used_for_download_failures() -> None:
    tasks = load_tasks("install_dot_net.RedHat.script.yml")
    classification = find_task(tasks, "classify the dotnet installation failure on RedHat")
    expression = set_fact_expression(classification, "dotnet_install_failure_is_download")

    assert "dotnet_install_last_error" in expression
    assert "redhat_repo_download_failure_pattern" in expression


def test_hint_variables_are_defined_in_group_vars() -> None:
    group_vars = load_group_vars()

    assert isinstance(group_vars["redhat_repo_download_failure_pattern"], str)
    assert group_vars["redhat_repo_download_failure_hints"]
    assert group_vars["redhat_repo_missing_package_hints"]

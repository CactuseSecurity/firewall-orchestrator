import re

from redhat_task_helpers import (
    MISSING_PACKAGE_ERROR,
    PACKAGE_DOWNLOAD_ERROR,
    REPO_METADATA_ERROR,
    TRANSIENT_ERROR,
    assert_error_is_recorded,
    assert_hints_are_selected_by_the_error,
    assert_retry_is_pinned_to_the_repos_offering_the_packages,
    command_of,
    find_task,
    find_task_containing,
    flatten,
    load_group_vars,
    load_tasks,
    rescue_names,
    set_fact_expression,
    set_fact_of,
)

MAIN_FILE = "install_dot_net.RedHat.yml"
FALLBACK_FILE = "install_dot_net.RedHat.fallback.yml"
SCRIPT_FILE = "install_dot_net.RedHat.script.yml"
ATTEMPT_FILES = (MAIN_FILE, FALLBACK_FILE)

# one per installation attempt: the configured repos, the retry, and the microsoft repo
EXPECTED_ERROR_RECORDING_ATTEMPTS = 3

HINT_SELECTION_TASK = "collect the hints matching the dotnet installation failure on RedHat"


def test_every_dotnet_rescue_records_the_dnf_error_first() -> None:
    # the final message can only name the real cause if no nested rescue loses it on the way
    for file_name in ATTEMPT_FILES:
        for names in rescue_names(load_tasks(file_name)):
            assert names[0].startswith("remember the dotnet installation error"), (file_name, names)


def test_recorded_error_is_reported_instead_of_the_ansible_failed_result() -> None:
    for file_name in ATTEMPT_FILES:
        for task in flatten(load_tasks(file_name)):
            if "dotnet_install_last_error" in set_fact_of(task):
                assert "ansible_failed_result" in set_fact_expression(task, "dotnet_install_last_error")
            elif "debug" in task:
                assert "ansible_failed_result" not in str(task["debug"])


def test_every_attempt_keeps_the_first_error_and_appends_to_the_chain() -> None:
    recorded = 0
    for file_name in ATTEMPT_FILES:
        for task in flatten(load_tasks(file_name)):
            if "dotnet_install_last_error" not in set_fact_of(task):
                continue
            recorded += 1
            assert_error_is_recorded(task, "dotnet_install_last_error", "dotnet_install_error_chain")
    assert recorded == EXPECTED_ERROR_RECORDING_ATTEMPTS


def test_the_retry_is_pinned_and_never_drops_the_metadata_of_every_repo() -> None:
    assert_retry_is_pinned_to_the_repos_offering_the_packages(
        load_tasks(FALLBACK_FILE),
        repoquery_part="find the repos offering",
        retry_part="after dropping the cached rpms on RedHat",
        repos_fact="dotnet_retry_repos",
    )
    for file_name in (MAIN_FILE, SCRIPT_FILE):
        for task in flatten(load_tasks(file_name)):
            assert "clean all" not in command_of(task), file_name


def test_the_hints_are_selected_by_the_first_error() -> None:
    tasks = load_tasks(SCRIPT_FILE)
    classified = find_task(tasks, "select the dotnet installation error that describes the failure on RedHat")
    assert "dotnet_install_first_error" in set_fact_expression(classified, "dotnet_install_classified_error")

    classification = find_task(tasks, "classify the dotnet installation failure on RedHat")
    for fact in ("dotnet_install_failure_is_download", "dotnet_install_failure_is_metadata"):
        assert "dotnet_install_classified_error" in set_fact_expression(classification, fact)
    assert "redhat_repo_download_failure_pattern" in set_fact_expression(
        classification, "dotnet_install_failure_is_download"
    )
    assert "redhat_repo_metadata_failure_pattern" in set_fact_expression(
        classification, "dotnet_install_failure_is_metadata"
    )


def test_a_metadata_failure_gets_its_own_hints_and_names_the_repo() -> None:
    tasks = load_tasks(SCRIPT_FILE)
    classification = find_task(tasks, "classify the dotnet installation failure on RedHat")
    assert "regex_findall" in set_fact_expression(classification, "dotnet_install_failing_repo")

    hints = find_task(tasks, HINT_SELECTION_TASK)
    expression = set_fact_expression(hints, "dotnet_install_hints")
    assert_hints_are_selected_by_the_error(expression, "dotnet_install_failure_is_metadata")
    assert "dotnet_install_failing_repo" in expression


def test_the_point_release_hint_needs_a_retry_that_reached_the_packages() -> None:
    # the hint claims the cache was already dropped and retried, which is untrue when the retry
    # died on the metadata of another repo before it got to the dotnet packages
    reached = find_task(load_tasks(FALLBACK_FILE), "remember whether the dotnet retry reached the packages on RedHat")
    expression = set_fact_expression(reached, "dotnet_install_cache_retry_reached_packages")
    assert "redhat_repo_download_failure_pattern" in expression
    assert "is not search(redhat_repo_metadata_failure_pattern)" in expression

    hints = find_task(load_tasks(SCRIPT_FILE), HINT_SELECTION_TASK)
    assert "dotnet_install_cache_retry_reached_packages" in set_fact_expression(hints, "dotnet_install_hints")


def test_the_failure_message_quotes_every_attempt_and_the_resolved_urls() -> None:
    tasks = load_tasks(SCRIPT_FILE)
    urls = find_task_containing(tasks, "collect the download urls dnf resolved")
    assert "--url" in command_of(urls)
    assert urls["failed_when"] is False
    assert urls["changed_when"] is False
    assert "dotnet_install_failure_is_download | bool" in urls["when"]

    message = find_task(tasks, "collect the dotnet installation failure message on RedHat")
    rendered = set_fact_expression(message, "dotnet_install_failure_msg")
    assert "dotnet_install_error_chain" in rendered
    assert "dotnet_install_hints" in rendered
    assert "dotnet_download_urls_redhat.stdout_lines" in rendered


def test_hint_variables_are_defined_in_group_vars() -> None:
    group_vars = load_group_vars()

    for pattern_name in (
        "redhat_repo_download_failure_pattern",
        "redhat_repo_metadata_failure_pattern",
        "redhat_repo_transient_failure_pattern",
    ):
        assert isinstance(group_vars[pattern_name], str)
    assert group_vars["redhat_repo_download_failure_hints"]
    assert group_vars["redhat_repo_metadata_failure_hints"]
    assert group_vars["redhat_repo_missing_package_hints"]
    # the point release hint is added conditionally, so it may not be part of the fixed list
    assert isinstance(group_vars["redhat_repo_cache_retry_hint"], str)
    assert group_vars["redhat_repo_cache_retry_hint"] not in group_vars["redhat_repo_download_failure_hints"]


def test_the_shared_hints_name_no_single_package() -> None:
    # the same hints are used by the dotnet and the chromium dependency installation
    for hint in load_group_vars()["redhat_repo_missing_package_hints"]:
        assert "dotnet" not in hint.lower()


def test_the_patterns_tell_the_reported_errors_apart() -> None:
    group_vars = load_group_vars()
    download = group_vars["redhat_repo_download_failure_pattern"]
    metadata = group_vars["redhat_repo_metadata_failure_pattern"]
    transient = group_vars["redhat_repo_transient_failure_pattern"]

    assert re.search(download, PACKAGE_DOWNLOAD_ERROR)
    assert not re.search(metadata, PACKAGE_DOWNLOAD_ERROR)
    assert re.search(metadata, REPO_METADATA_ERROR)
    assert not re.search(download, MISSING_PACKAGE_ERROR)
    assert not re.search(metadata, MISSING_PACKAGE_ERROR)
    # a mirror that answers every attempt the same way must not be retried, a timeout may be
    assert not re.search(transient, PACKAGE_DOWNLOAD_ERROR)
    assert re.search(transient, TRANSIENT_ERROR)

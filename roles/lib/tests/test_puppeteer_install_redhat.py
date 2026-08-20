from redhat_task_helpers import (
    assert_error_is_recorded,
    assert_hints_are_selected_by_the_error,
    assert_retry_is_pinned_to_the_repos_offering_the_packages,
    find_task,
    flatten,
    load_tasks,
    rescue_names,
    set_fact_expression,
    set_fact_of,
)

PUPPETEER_FILE = "install_puppeteer.RedHat.yml"

# one per installation attempt: the configured repos and the retry
EXPECTED_ERROR_RECORDING_ATTEMPTS = 2

HINT_SELECTION_TASK = "collect the hints matching the chromium dependency failure on RedHat"


def test_every_chromium_rescue_records_the_dnf_error_first() -> None:
    # the final message can only name the real cause if the nested rescue does not lose it
    for names in rescue_names(load_tasks(PUPPETEER_FILE)):
        assert names[0].startswith("remember the chromium dependency installation error"), names


def test_every_attempt_keeps_the_first_error_and_appends_to_the_chain() -> None:
    recorded = 0
    for task in flatten(load_tasks(PUPPETEER_FILE)):
        if "chromium_install_last_error" not in set_fact_of(task):
            continue
        recorded += 1
        assert_error_is_recorded(task, "chromium_install_last_error", "chromium_install_error_chain")
    assert recorded == EXPECTED_ERROR_RECORDING_ATTEMPTS


def test_the_reported_error_is_the_recorded_one() -> None:
    for task in flatten(load_tasks(PUPPETEER_FILE)):
        if "debug" in task:
            assert "ansible_failed_result" not in str(task["debug"])


def test_the_retry_is_pinned_and_never_drops_the_metadata_of_every_repo() -> None:
    assert_retry_is_pinned_to_the_repos_offering_the_packages(
        load_tasks(PUPPETEER_FILE),
        repoquery_part="find the repos offering the chromium dependencies",
        retry_part="after dropping the cached rpms on RedHat",
        repos_fact="chromium_retry_repos",
    )


def test_the_hints_are_selected_by_the_first_error() -> None:
    tasks = load_tasks(PUPPETEER_FILE)
    classification = find_task(tasks, "classify the chromium dependency installation failure on RedHat")
    for fact in ("chromium_install_failure_is_download", "chromium_install_failure_is_metadata"):
        # the retry can fail on a repository that has nothing to do with the chromium packages
        assert "chromium_install_first_error" in set_fact_expression(classification, fact)
    assert "regex_findall" in set_fact_expression(classification, "chromium_install_failing_repo")

    expression = set_fact_expression(find_task(tasks, HINT_SELECTION_TASK), "chromium_install_hints")
    assert_hints_are_selected_by_the_error(expression, "chromium_install_failure_is_metadata")
    assert "chromium_install_failing_repo" in expression


def test_the_point_release_hint_needs_a_retry_that_reached_the_packages() -> None:
    # the hint claims the cache was already dropped and retried, which is untrue when the retry
    # died on the metadata of another repo before it got to the chromium packages
    expression = set_fact_expression(
        find_task(load_tasks(PUPPETEER_FILE), HINT_SELECTION_TASK), "chromium_install_hints"
    )
    assert "chromium_install_last_error is search(redhat_repo_download_failure_pattern)" in expression
    assert "chromium_install_last_error is not search(redhat_repo_metadata_failure_pattern)" in expression


def test_the_failure_message_quotes_every_attempt() -> None:
    failure = find_task(load_tasks(PUPPETEER_FILE), "fail when the chromium dependencies stay unavailable on RedHat")
    message = str(failure["fail"]["msg"])
    assert "chromium_install_error_chain" in message
    assert "chromium_install_hints" in message
    assert "platform_packages | join(', ')" in message

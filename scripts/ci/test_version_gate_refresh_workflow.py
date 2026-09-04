"""Behavioral tests for the Version gate refresh workflow trigger."""

from __future__ import annotations

import os
import stat
import subprocess
from pathlib import Path

import pytest

WORKFLOW_DIRECTORY = Path(__file__).parents[2] / ".github" / "workflows"
WORKFLOW_PATH = WORKFLOW_DIRECTORY / "version-gate-refresh.yml"
TAG_GUARD_WORKFLOW_PATH = WORKFLOW_DIRECTORY / "version-tag-guard.yml"
FAST_FORWARD_WORKFLOW_PATH = WORKFLOW_DIRECTORY / "fast-forward-main-to-release-tag.yml"
TRIGGER_STEP_NAME = "Decide whether open pull request gates need refresh"
RERUN_STEP_NAME = "Re-run the version gate for every open pull request"
PULL_REQUEST_LIMIT = 200
SCRIPT_INDENT = " " * 10


def read_workflow(path: Path = WORKFLOW_PATH) -> str:
    """Read the workflow from the repository root."""
    return path.read_text(encoding="utf-8")


def workflow_step_script(path: Path, step_name: str) -> str:
    """Extract a workflow step script so its decision branches can be exercised."""
    lines = read_workflow(path).splitlines()
    step_index = lines.index(f"      - name: {step_name}")
    run_index = lines.index("        run: |", step_index)
    script_lines: list[str] = []
    for line in lines[run_index + 1 :]:
        if line.startswith(SCRIPT_INDENT):
            script_lines.append(line[len(SCRIPT_INDENT) :])
        elif line:
            break
        else:
            script_lines.append("")
    return "\n".join(script_lines)


def write_executable(path: Path, content: str) -> None:
    """Create a fake command used to isolate workflow shell from GitHub services."""
    path.write_text(content, encoding="utf-8")
    path.chmod(path.stat().st_mode | stat.S_IXUSR)


def execute_refresh_loop(
    tmp_path: Path,
    pull_requests: tuple[tuple[int, str], ...] = (),
    workflow_runs: tuple[tuple[str, int, str], ...] = (),
    failed_run_ids: tuple[int, ...] = (),
    pull_request_count: int | None = None,
) -> tuple[subprocess.CompletedProcess[str], list[str]]:
    """Execute the refresh loop with mocked pull requests and workflow runs."""
    fake_bin = tmp_path / "bin"
    fake_bin.mkdir()
    api_log = tmp_path / "api-log"
    rerun_log = tmp_path / "rerun-log"
    write_executable(
        fake_bin / "gh",
        """#!/bin/sh
if [ "$1" = "pr" ]; then
    printf '[]\n'
elif [ "$1" = "api" ]; then
    request="$2"
    printf '%s\n' "$request" >> "$MOCK_API_LOG"
    head_sha="${request#*head_sha=}"
    head_sha="${head_sha%%&*}"
    printf '%s\n' "$MOCK_RUN_LINES" | awk -F '\t' -v sha="$head_sha" \
        '$1 == sha { print $2 "\t" $3; exit }'
elif [ "$1" = "run" ] && [ "$2" = "rerun" ]; then
    run_id="$3"
    printf '%s\n' "$run_id" >> "$MOCK_RERUN_LOG"
    case " $MOCK_FAILED_RUN_IDS " in
        *" $run_id "*) exit 1 ;;
    esac
fi
exit 0
""",
    )
    write_executable(
        fake_bin / "jq",
        """#!/bin/sh
if [ "$1" = "length" ]; then
    printf '%s\n' "$MOCK_PR_COUNT"
elif [ "$1" = "-r" ]; then
    if [ -n "$MOCK_PR_LINES" ]; then
        printf '%s\n' "$MOCK_PR_LINES"
    fi
fi
exit 0
""",
    )
    effective_pull_request_count = len(pull_requests) if pull_request_count is None else pull_request_count
    environment = os.environ.copy()
    environment.update(
        {
            "BASE_BRANCH": "develop",
            "GATE_WORKFLOW": "version-gate.yml",
            "GH_REPO": "CactuseSecurity/firewall-orchestrator",
            "GH_TOKEN": "test-token",
            "MOCK_API_LOG": str(api_log),
            "MOCK_FAILED_RUN_IDS": " ".join(str(run_id) for run_id in failed_run_ids),
            "MOCK_PR_COUNT": str(effective_pull_request_count),
            "MOCK_PR_LINES": "\n".join(f"{number} {head_sha}" for number, head_sha in pull_requests),
            "MOCK_RERUN_LOG": str(rerun_log),
            "MOCK_RUN_LINES": "\n".join(
                f"{head_sha}\t{run_id}\t{status}" for head_sha, run_id, status in workflow_runs
            ),
            "PATH": f"{fake_bin}:{environment['PATH']}",
            "PULL_REQUEST_LIMIT": str(PULL_REQUEST_LIMIT),
            "SINGLE_PR": "",
        }
    )
    completed = subprocess.run(  # noqa: S603
        ["/bin/bash", "-c", workflow_step_script(WORKFLOW_PATH, RERUN_STEP_NAME)],
        check=False,
        capture_output=True,
        cwd=tmp_path,
        env=environment,
        text=True,
    )
    rerun_ids = rerun_log.read_text(encoding="utf-8").splitlines() if rerun_log.exists() else []
    return (completed, rerun_ids)


def refresh_output_at_pr_count(tmp_path: Path, pull_request_count: int) -> subprocess.CompletedProcess[str]:
    """Execute the refresh script with a mocked number of open pull requests."""
    completed, _ = execute_refresh_loop(tmp_path, pull_request_count=pull_request_count)
    return completed


def refresh_decision(
    tmp_path: Path,
    event_name: str,
    ref_type: str,
    ref_name: str,
) -> str:
    """Execute the workflow decision script and return its run output."""
    output_path = tmp_path / "github-output"
    environment = os.environ.copy()
    environment.update(
        {
            "EVENT_NAME": event_name,
            "GITHUB_OUTPUT": str(output_path),
            "REF_NAME": ref_name,
            "REF_TYPE": ref_type,
        }
    )
    subprocess.run(  # noqa: S603
        ["/bin/bash", "-c", workflow_step_script(WORKFLOW_PATH, TRIGGER_STEP_NAME)],
        check=True,
        capture_output=True,
        env=environment,
        text=True,
    )
    return output_path.read_text(encoding="utf-8").strip()


def test_workflow_listens_for_develop_updates() -> None:
    """Keep the base-branch trigger that resolves stale gates after a version bump."""
    workflow = read_workflow()
    expected_trigger = "  push:\n    branches:\n      - develop\n    tags:"
    assert expected_trigger in workflow


@pytest.mark.parametrize(
    ("pull_request_count", "warning_expected"),
    [(PULL_REQUEST_LIMIT - 1, False), (PULL_REQUEST_LIMIT, True)],
)
def test_warns_when_open_pull_request_limit_is_reached(
    tmp_path: Path,
    pull_request_count: int,
    warning_expected: bool,
) -> None:
    """Warn that pull requests beyond the query cap may retain stale gates."""
    completed = refresh_output_at_pr_count(tmp_path, pull_request_count)

    assert completed.returncode == 0
    warning = (
        f"::warning::Open pull request query reached its limit of {PULL_REQUEST_LIMIT}; "
        "additional pull requests may exist and were not refreshed."
    )
    assert (warning in completed.stdout) is warning_expected


def test_completed_run_for_matching_head_sha_is_rerun(tmp_path: Path) -> None:
    """Match the pull request by head SHA and invoke the newest completed run."""
    completed, rerun_ids = execute_refresh_loop(
        tmp_path,
        pull_requests=((42, "head-a"),),
        workflow_runs=(("other-head", 800, "completed"), ("head-a", 900, "completed")),
    )

    assert completed.returncode == 0
    assert rerun_ids == ["900"]
    assert "PR #42: re-running version gate run 900." in completed.stdout
    assert completed.stderr == ""
    api_request = (tmp_path / "api-log").read_text(encoding="utf-8")
    assert "event=pull_request_target&head_sha=head-a&per_page=1" in api_request


def test_in_progress_run_is_left_to_finish(tmp_path: Path) -> None:
    """Do not restart a matching run that is already producing a fresh result."""
    completed, rerun_ids = execute_refresh_loop(
        tmp_path,
        pull_requests=((43, "head-b"),),
        workflow_runs=(("head-b", 901, "in_progress"),),
    )

    assert completed.returncode == 0
    assert rerun_ids == []
    assert "run 901 is in_progress, it will report a fresh result on its own" in completed.stdout


def test_unmatched_pull_requests_are_accumulated_and_fail(tmp_path: Path) -> None:
    """Count every missing run and fail after processing the complete pull request list."""
    completed, rerun_ids = execute_refresh_loop(
        tmp_path,
        pull_requests=((44, "missing-a"), (45, "missing-b")),
        workflow_runs=(("other-head", 902, "completed"),),
    )

    assert completed.returncode == 1
    assert rerun_ids == []
    assert "PR #44: no version gate run found for missing-a" in completed.stderr
    assert "PR #45: no version gate run found for missing-b" in completed.stderr
    assert "Could not refresh the version gate for 2 pull request(s)." in completed.stderr


def test_rerun_failure_is_accumulated_without_stopping_the_loop(tmp_path: Path) -> None:
    """Attempt later reruns before returning failure for a rejected rerun request."""
    completed, rerun_ids = execute_refresh_loop(
        tmp_path,
        pull_requests=((46, "head-c"), (47, "head-d")),
        workflow_runs=(("head-c", 903, "completed"), ("head-d", 904, "completed")),
        failed_run_ids=(903,),
    )

    assert completed.returncode == 1
    assert rerun_ids == ["903", "904"]
    assert "PR #46: could not re-run 903." in completed.stderr
    assert "PR #47: re-running version gate run 904." in completed.stdout
    assert "Could not refresh the version gate for 1 pull request(s)." in completed.stderr


@pytest.mark.parametrize(
    ("event_name", "ref_type", "ref_name"),
    [
        ("workflow_dispatch", "branch", "develop"),
        ("push", "branch", "develop"),
        ("push", "tag", "v9.4.6"),
        ("push", "tag", "9.4.6-dev"),
    ],
)
def test_refreshes_for_manual_develop_and_sealing_tag_events(
    tmp_path: Path,
    event_name: str,
    ref_type: str,
    ref_name: str,
) -> None:
    """Refresh whenever an event can make an existing gate verdict stale."""
    assert refresh_decision(tmp_path, event_name, ref_type, ref_name) == "run=true"


@pytest.mark.parametrize(
    "tag",
    ["v9.4.6-rc1", "v9.4.6-beta", "v09.4.6", "v9.04.6", "v9.4.06", "documentation-update"],
)
def test_skips_non_sealing_tags(tmp_path: Path, tag: str) -> None:
    """Do not refresh all pull requests for tags that leave versions open."""
    assert refresh_decision(tmp_path, "push", "tag", tag) == "run=false"


@pytest.mark.parametrize("tag", ["v09.4.6", "v9.04.6", "v9.4.06"])
def test_tag_guard_does_not_treat_zero_padded_tags_as_sealing(tag: str) -> None:
    """Keep malformed tags out of the sealing-tag ancestry path."""
    environment = os.environ.copy()
    environment["TAG_NAME"] = tag
    completed = subprocess.run(  # noqa: S603
        [
            "/bin/bash",
            "-c",
            workflow_step_script(TAG_GUARD_WORKFLOW_PATH, "Check that a sealing tag points at a released line"),
        ],
        check=True,
        capture_output=True,
        env=environment,
        text=True,
    )
    assert "does not seal a version" in completed.stdout


@pytest.mark.parametrize(
    ("tag", "expected"),
    [("v9.4.6", "stable=true"), ("9.4.6", "stable=true"), ("v09.4.6", "stable=false"), ("v9.04.6", "stable=false")],
)
def test_only_canonical_tags_can_fast_forward_main(tmp_path: Path, tag: str, expected: str) -> None:
    """Grant release-app access only to canonically formatted stable tags."""
    output_path = tmp_path / "github-output"
    environment = os.environ.copy()
    environment.update({"GITHUB_OUTPUT": str(output_path), "TAG_NAME": tag})
    subprocess.run(  # noqa: S603
        [
            "/bin/bash",
            "-c",
            workflow_step_script(FAST_FORWARD_WORKFLOW_PATH, "Validate stable release tag"),
        ],
        check=True,
        capture_output=True,
        env=environment,
        text=True,
    )
    assert output_path.read_text(encoding="utf-8").strip() == expected

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


def refresh_output_at_pr_count(tmp_path: Path, pull_request_count: int) -> subprocess.CompletedProcess[str]:
    """Execute the refresh script with a mocked number of open pull requests."""
    fake_bin = tmp_path / "bin"
    fake_bin.mkdir()
    write_executable(
        fake_bin / "gh",
        """#!/bin/sh
if [ "$1" = "pr" ]; then
    printf '[]\n'
fi
exit 0
""",
    )
    write_executable(
        fake_bin / "jq",
        """#!/bin/sh
if [ "$1" = "length" ]; then
    printf '%s\n' "$MOCK_PR_COUNT"
fi
exit 0
""",
    )
    environment = os.environ.copy()
    environment.update(
        {
            "BASE_BRANCH": "develop",
            "GATE_WORKFLOW": "version-gate.yml",
            "GH_REPO": "CactuseSecurity/firewall-orchestrator",
            "GH_TOKEN": "test-token",
            "MOCK_PR_COUNT": str(pull_request_count),
            "PATH": f"{fake_bin}:{environment['PATH']}",
            "PULL_REQUEST_LIMIT": str(PULL_REQUEST_LIMIT),
            "SINGLE_PR": "",
        }
    )
    return subprocess.run(  # noqa: S603
        ["/bin/bash", "-c", workflow_step_script(WORKFLOW_PATH, RERUN_STEP_NAME)],
        check=False,
        capture_output=True,
        cwd=tmp_path,
        env=environment,
        text=True,
    )


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

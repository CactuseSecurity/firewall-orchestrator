"""Behavioral tests for the Version gate refresh workflow trigger."""

from __future__ import annotations

import os
import subprocess
from pathlib import Path

import pytest

WORKFLOW_DIRECTORY = Path(__file__).parents[2] / ".github" / "workflows"
WORKFLOW_PATH = WORKFLOW_DIRECTORY / "version-gate-refresh.yml"
TAG_GUARD_WORKFLOW_PATH = WORKFLOW_DIRECTORY / "version-tag-guard.yml"
FAST_FORWARD_WORKFLOW_PATH = WORKFLOW_DIRECTORY / "fast-forward-main-to-release-tag.yml"
TRIGGER_STEP_NAME = "Decide whether open pull request gates need refresh"
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

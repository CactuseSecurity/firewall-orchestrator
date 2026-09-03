"""Behavioral tests for the Version gate refresh workflow trigger."""

from __future__ import annotations

import os
import subprocess
from pathlib import Path

import pytest

WORKFLOW_PATH = Path(__file__).parents[2] / ".github" / "workflows" / "version-gate-refresh.yml"
TRIGGER_STEP_NAME = "Decide whether open pull request gates need refresh"
SCRIPT_INDENT = " " * 10


def read_workflow() -> str:
    """Read the workflow from the repository root."""
    return WORKFLOW_PATH.read_text(encoding="utf-8")


def trigger_script() -> str:
    """Extract the trigger decision script so its branches can be exercised."""
    lines = read_workflow().splitlines()
    step_index = lines.index(f"      - name: {TRIGGER_STEP_NAME}")
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
        ["/bin/bash", "-c", trigger_script()],
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


@pytest.mark.parametrize("tag", ["v9.4.6-rc1", "v9.4.6-beta", "documentation-update"])
def test_skips_non_sealing_tags(tmp_path: Path, tag: str) -> None:
    """Do not refresh all pull requests for tags that leave versions open."""
    assert refresh_decision(tmp_path, "push", "tag", tag) == "run=false"

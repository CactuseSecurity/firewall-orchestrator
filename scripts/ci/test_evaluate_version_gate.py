"""Tests for merge-ref failure reporting in evaluate_version_gate.sh."""

from __future__ import annotations

import os
import stat
import subprocess
from pathlib import Path

import pytest

SCRIPT_PATH = Path(__file__).with_name("evaluate_version_gate.sh")


def write_executable(path: Path, content: str) -> None:
    """Create a small executable used to isolate the shell script from external services."""
    path.write_text(content, encoding="utf-8")
    path.chmod(path.stat().st_mode | stat.S_IXUSR)


def run_with_missing_merge_ref(
    tmp_path: Path,
    mergeable_state: str,
    *,
    gh_succeeds: bool = True,
) -> subprocess.CompletedProcess[str]:
    """Run the gate with fake commands that keep the merge ref unavailable."""
    fake_bin = tmp_path / "bin"
    fake_bin.mkdir()
    write_executable(fake_bin / "git", "#!/bin/sh\necho 'fatal: merge ref unavailable' >&2\nexit 1\n")
    write_executable(fake_bin / "sleep", "#!/bin/sh\nexit 0\n")
    gh_exit = 0 if gh_succeeds else 1
    write_executable(fake_bin / "gh", f"#!/bin/sh\necho '{mergeable_state}'\nexit {gh_exit}\n")

    environment = os.environ.copy()
    environment.update(
        {
            "GH_REPO": "CactuseSecurity/firewall-orchestrator",
            "GH_TOKEN": "test-token",
            "PATH": f"{fake_bin}:{environment['PATH']}",
        }
    )
    return subprocess.run(  # noqa: S603
        ["/bin/bash", str(SCRIPT_PATH), "42", "develop"],
        check=False,
        capture_output=True,
        env=environment,
        text=True,
    )


def test_confirmed_conflict_gets_conflict_guidance(tmp_path: Path) -> None:
    """Recommend conflict resolution only when GitHub confirms that state."""
    completed = run_with_missing_merge_ref(tmp_path, "CONFLICTING")

    assert completed.returncode == 1
    assert "GitHub reports merge conflicts" in completed.stderr
    assert "Resolve the merge conflicts" in completed.stderr


@pytest.mark.parametrize("mergeable_state", ["UNKNOWN", "MERGEABLE"])
def test_other_states_get_retry_guidance(tmp_path: Path, mergeable_state: str) -> None:
    """Keep transient or inconsistent merge-ref failures distinct from conflicts."""
    completed = run_with_missing_merge_ref(tmp_path, mergeable_state)

    assert completed.returncode == 1
    assert f"mergeability as '{mergeable_state}'" in completed.stderr
    assert "re-run the workflow" in completed.stderr
    assert "Resolve the merge conflicts" not in completed.stderr
    assert "fatal: merge ref unavailable" in completed.stderr


def test_mergeability_query_failure_gets_infrastructure_guidance(tmp_path: Path) -> None:
    """Explain when neither git nor the GitHub API can classify the failure."""
    completed = run_with_missing_merge_ref(tmp_path, "", gh_succeeds=False)

    assert completed.returncode == 1
    assert "query pull request mergeability" in completed.stderr
    assert "GitHub connectivity" in completed.stderr
    assert "Resolve the merge conflicts" not in completed.stderr

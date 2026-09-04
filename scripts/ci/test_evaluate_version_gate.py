"""Integration tests for evaluate_version_gate.sh."""

from __future__ import annotations

import os
import shlex
import shutil
import stat
import subprocess
from pathlib import Path

import pytest

SCRIPT_PATH = Path(__file__).with_name("evaluate_version_gate.sh")
VERSION_GATE_PATH = Path(__file__).with_name("version_gate.py")
BASE_CONFIGURATION = 'product_version: "9.4.5"\n'
MERGED_CONFIGURATION = 'product_version: "9.4.6"\n'
MOCKED_COMMAND_FAILURE_EXIT = 23
REVISION_HISTORY = """# Revision history

## 9.4.5 - 01.09.2026
- current version
"""
MERGED_REVISION_HISTORY = f"""{REVISION_HISTORY}
## 9.4.6
- proposed version
"""


def write_executable(path: Path, content: str) -> None:
    """Create a small executable used to isolate the shell script from external services."""
    path.write_text(content, encoding="utf-8")
    path.chmod(path.stat().st_mode | stat.S_IXUSR)


def git_executable() -> str:
    """Return the real Git executable used behind the test wrapper."""
    executable = shutil.which("git")
    if executable is None:
        pytest.fail("git is required for the shell integration tests")
    return executable


def run_git(repository: Path | None, arguments: list[str]) -> None:
    """Run a Git setup command for a temporary repository."""
    subprocess.run(  # noqa: S603
        [git_executable(), *arguments],
        check=True,
        capture_output=True,
        cwd=repository,
        text=True,
    )


def create_repository(
    tmp_path: Path,
    *,
    include_merge_ref: bool = True,
    version_is_bumped: bool = False,
    agents_pointer_change: bool = False,
    additional_change: bool = False,
    tags: tuple[str, ...] = (),
) -> Path:
    """Create a checkout and local bare origin containing the refs used by the gate."""
    remote = tmp_path / "origin.git"
    repository = tmp_path / "checkout"
    run_git(None, ["init", "--bare", str(remote)])
    run_git(None, ["init", "--initial-branch=develop", str(repository)])
    run_git(repository, ["config", "user.name", "Version gate test"])
    run_git(repository, ["config", "user.email", "version-gate@example.invalid"])
    run_git(repository, ["config", "commit.gpgsign", "false"])

    inventory = repository / "inventory" / "group_vars"
    documentation = repository / "documentation"
    implementation = repository / "scripts" / "ci"
    inventory.mkdir(parents=True)
    documentation.mkdir()
    implementation.mkdir(parents=True)
    (inventory / "all.yml").write_text(BASE_CONFIGURATION, encoding="utf-8")
    (documentation / "revision-history.md").write_text(REVISION_HISTORY, encoding="utf-8")
    shutil.copy2(VERSION_GATE_PATH, implementation / "version_gate.py")
    if agents_pointer_change:
        (repository / ".agents").write_text("old pointer\n", encoding="utf-8")

    run_git(repository, ["add", "."])
    run_git(repository, ["commit", "-m", "test fixture"])
    run_git(repository, ["remote", "add", "origin", str(remote)])
    run_git(repository, ["push", "origin", "HEAD:refs/heads/develop"])
    if version_is_bumped:
        (inventory / "all.yml").write_text(MERGED_CONFIGURATION, encoding="utf-8")
        (documentation / "revision-history.md").write_text(MERGED_REVISION_HISTORY, encoding="utf-8")
        run_git(repository, ["add", "."])
        run_git(repository, ["commit", "-m", "open next version"])
    if agents_pointer_change:
        (repository / ".agents").write_text("new pointer\n", encoding="utf-8")
        if additional_change:
            (repository / "README.md").write_text("unrelated change\n", encoding="utf-8")
        run_git(repository, ["add", "."])
        run_git(repository, ["commit", "-m", "advance agents pointer"])
    if include_merge_ref:
        run_git(repository, ["push", "origin", "HEAD:refs/pull/42/merge"])
    for tag in tags:
        run_git(repository, ["tag", tag])
        run_git(repository, ["push", "origin", f"refs/tags/{tag}"])
    return repository


def create_fake_commands(tmp_path: Path) -> Path:
    """Mock GitHub and delays while delegating ordinary Git calls to local test data."""
    fake_bin = tmp_path / "bin"
    fake_bin.mkdir()
    real_git = shlex.quote(git_executable())
    write_executable(
        fake_bin / "git",
        f"""#!/bin/sh
if [ "${{FAIL_BASE_FETCH:-false}}" = "true" ]; then
    case "$*" in
        *refs/heads/develop:refs/fwo/base*)
            echo "mocked base fetch failure" >&2
            exit {MOCKED_COMMAND_FAILURE_EXIT}
            ;;
    esac
fi
exec {real_git} "$@"
""",
    )
    write_executable(fake_bin / "sleep", "#!/bin/sh\nexit 0\n")
    write_executable(
        fake_bin / "gh",
        """#!/bin/sh
if [ "${GH_COMMAND_SUCCEEDS:-true}" != "true" ]; then
    exit 1
fi
printf '%s\n' "${GH_MERGEABLE_STATE:-UNKNOWN}"
""",
    )
    return fake_bin


def run_gate(
    tmp_path: Path,
    repository: Path,
    *,
    mergeable_state: str = "UNKNOWN",
    gh_succeeds: bool = True,
    fail_base_fetch: bool = False,
    pr_author: str = "",
    pr_head_ref: str = "",
    pr_head_repository: str = "",
) -> subprocess.CompletedProcess[str]:
    """Run the real shell gate against a local origin and mocked remote services."""
    fake_bin = create_fake_commands(tmp_path)
    environment = os.environ.copy()
    environment.update(
        {
            "FAIL_BASE_FETCH": str(fail_base_fetch).lower(),
            "GH_COMMAND_SUCCEEDS": str(gh_succeeds).lower(),
            "GH_MERGEABLE_STATE": mergeable_state,
            "GH_REPO": "CactuseSecurity/firewall-orchestrator",
            "GH_TOKEN": "test-token",
            "PATH": f"{fake_bin}:{environment['PATH']}",
            "PR_AUTHOR": pr_author,
            "PR_HEAD_REF": pr_head_ref,
            "PR_HEAD_REPOSITORY": pr_head_repository,
        }
    )
    return subprocess.run(  # noqa: S603
        ["/bin/bash", str(SCRIPT_PATH), "42", "develop"],
        check=False,
        capture_output=True,
        cwd=repository,
        env=environment,
        text=True,
    )


def run_with_missing_merge_ref(
    tmp_path: Path,
    mergeable_state: str,
    *,
    gh_succeeds: bool = True,
) -> subprocess.CompletedProcess[str]:
    """Run the gate with a local origin that has no pull request merge ref."""
    repository = create_repository(tmp_path, include_merge_ref=False)
    return run_gate(tmp_path, repository, mergeable_state=mergeable_state, gh_succeeds=gh_succeeds)


def test_open_version_passes_and_prints_verdict(tmp_path: Path) -> None:
    """Exercise ref fetching, file extraction, tag listing and verdict output."""
    repository = create_repository(tmp_path, version_is_bumped=True, tags=("v9.4.5",))

    completed = run_gate(tmp_path, repository)

    assert completed.returncode == 0
    assert completed.stdout.strip() == (
        "Version gate passed: version 9.4.5 is sealed, opening version 9.4.6; "
        "revision history adds text for version 9.4.6"
    )
    assert completed.stderr == ""


def test_sealed_version_fails_and_prints_verdict(tmp_path: Path) -> None:
    """Propagate a negative Python verdict through the shell command."""
    repository = create_repository(tmp_path, tags=("v9.4.5",))

    completed = run_gate(tmp_path, repository)

    assert completed.returncode == 1
    assert completed.stdout == ""
    assert "Version gate failed: version 9.4.5 is already sealed" in completed.stderr


def test_dependabot_pull_request_skips_revision_history(tmp_path: Path) -> None:
    """Recognize an upstream Dependabot identity and branch together."""
    repository = create_repository(tmp_path)

    completed = run_gate(
        tmp_path,
        repository,
        pr_author="dependabot[bot]",
        pr_head_ref="dependabot/pip/develop/cryptography-50.0.1",
        pr_head_repository="CactuseSecurity/firewall-orchestrator",
    )

    assert completed.returncode == 0
    assert "revision history is exempt for this automated pull request" in completed.stdout


@pytest.mark.parametrize(
    ("pr_author", "pr_head_ref"),
    [
        ("CactusAutomation", "automation/submodule_update"),
        ("github-actions[bot]", "bot/update-agents-submodule"),
    ],
)
def test_agents_pointer_pull_request_skips_revision_history(
    tmp_path: Path,
    pr_author: str,
    pr_head_ref: str,
) -> None:
    """Recognize both established agents-pointer automation mechanisms."""
    repository = create_repository(tmp_path, agents_pointer_change=True)

    completed = run_gate(
        tmp_path,
        repository,
        pr_author=pr_author,
        pr_head_ref=pr_head_ref,
        pr_head_repository="CactuseSecurity/firewall-orchestrator",
    )

    assert completed.returncode == 0
    assert "revision history is exempt for this automated pull request" in completed.stdout


@pytest.mark.parametrize(
    ("pr_author", "pr_head_ref", "pr_head_repository"),
    [
        ("developer", "dependabot/pip/develop/example", "CactuseSecurity/firewall-orchestrator"),
        ("dependabot[bot]", "feature/example", "CactuseSecurity/firewall-orchestrator"),
        ("dependabot[bot]", "dependabot/pip/develop/example", "contributor/firewall-orchestrator"),
    ],
)
def test_dependabot_exemption_cannot_be_selected_by_branch_name_alone(
    tmp_path: Path,
    pr_author: str,
    pr_head_ref: str,
    pr_head_repository: str,
) -> None:
    """Require the trusted author, branch prefix and upstream repository together."""
    repository = create_repository(tmp_path)

    completed = run_gate(
        tmp_path,
        repository,
        pr_author=pr_author,
        pr_head_ref=pr_head_ref,
        pr_head_repository=pr_head_repository,
    )

    assert completed.returncode == 1
    assert "add revision-history text" in completed.stderr


def test_agents_pointer_exemption_rejects_additional_changes(tmp_path: Path) -> None:
    """Require `.agents` to be the automated pull request's only changed path."""
    repository = create_repository(tmp_path, agents_pointer_change=True, additional_change=True)

    completed = run_gate(
        tmp_path,
        repository,
        pr_author="CactusAutomation",
        pr_head_ref="automation/submodule_update",
        pr_head_repository="CactuseSecurity/firewall-orchestrator",
    )

    assert completed.returncode == 1
    assert "add revision-history text" in completed.stderr


@pytest.mark.parametrize(
    ("pr_author", "pr_head_ref", "pr_head_repository"),
    [
        ("developer", "automation/submodule_update", "CactuseSecurity/firewall-orchestrator"),
        ("CactusAutomation", "feature/example", "CactuseSecurity/firewall-orchestrator"),
        ("CactusAutomation", "automation/submodule_update", "contributor/firewall-orchestrator"),
    ],
)
def test_agents_pointer_exemption_requires_exact_automation_identity(
    tmp_path: Path,
    pr_author: str,
    pr_head_ref: str,
    pr_head_repository: str,
) -> None:
    """Reject `.agents` changes that do not carry the complete trusted identity."""
    repository = create_repository(tmp_path, agents_pointer_change=True)

    completed = run_gate(
        tmp_path,
        repository,
        pr_author=pr_author,
        pr_head_ref=pr_head_ref,
        pr_head_repository=pr_head_repository,
    )

    assert completed.returncode == 1
    assert "add revision-history text" in completed.stderr


def test_base_fetch_failure_is_propagated(tmp_path: Path) -> None:
    """Fail immediately when a required remote Git command fails."""
    repository = create_repository(tmp_path)

    completed = run_gate(tmp_path, repository, fail_base_fetch=True)

    assert completed.returncode == MOCKED_COMMAND_FAILURE_EXIT
    assert "mocked base fetch failure" in completed.stderr
    assert "Version gate passed" not in completed.stdout


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
    assert "couldn't find remote ref refs/pull/42/merge" in completed.stderr


def test_mergeability_query_failure_gets_infrastructure_guidance(tmp_path: Path) -> None:
    """Explain when neither git nor the GitHub API can classify the failure."""
    completed = run_with_missing_merge_ref(tmp_path, "", gh_succeeds=False)

    assert completed.returncode == 1
    assert "query pull request mergeability" in completed.stderr
    assert "GitHub connectivity" in completed.stderr
    assert "Resolve the merge conflicts" not in completed.stderr

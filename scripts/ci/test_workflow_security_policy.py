"""
Policy tests that bound the accepted risk of building pull request code in the privileged
pull_request_target context (GHSA-3cwm-h5cm-r3f8).
"""

from __future__ import annotations

import re
from pathlib import Path

import pytest

WORKFLOW_DIRECTORY = Path(__file__).parents[2] / ".github" / "workflows"
SONAR_PR_WORKFLOW_PATH = WORKFLOW_DIRECTORY / "sonarcloud-pr.yml"
SONAR_BRANCH_WORKFLOW_PATH = WORKFLOW_DIRECTORY / "sonarcloud.yml"
TRUSTED_SOURCE_GATE_STEP = "      - name: Gate trusted PR sources\n"
PULL_REQUEST_CHECKOUT_STEP = "      - name: Checkout PR head\n"
ALLOWED_PULL_REQUEST_CODE_SECRETS = frozenset({"SONAR_TOKEN"})
PULL_REQUEST_HEAD_EXPRESSIONS = (
    "github.event.pull_request.head.sha",
    "github.event.pull_request.head.ref",
    "github.head_ref",
)
SECRET_PATTERN = re.compile(r"secrets\.(?P<name>[A-Za-z0-9_]+)")
CACHE_PATTERN = re.compile(r"^\s+(?:- )?uses: actions/cache[@/]|^\s+cache(?:-dependency-path)?:", re.MULTILINE)
USES_PATTERN = re.compile(r"^\s+(?:- )?uses: (?P<action>\S+)", re.MULTILINE)
PINNED_ACTION_PATTERN = re.compile(r"^[^@\s]+@[0-9a-f]{40}$")


def active_workflow_paths() -> list[Path]:
    """Return all workflows GitHub would run (disabled ones are not named *.yml)."""
    return sorted(WORKFLOW_DIRECTORY.glob("*.yml"))


def read_workflow(path: Path) -> str:
    """Read a workflow from the repository."""
    return path.read_text(encoding="utf-8")


def workflow_trigger_names(text: str) -> set[str]:
    """Collect the event names below the top-level 'on:' key."""
    triggers: set[str] = set()
    in_on_block = False
    for line in text.splitlines():
        if line.startswith("on:"):
            in_on_block = True
            continue
        if in_on_block and line and not line.startswith((" ", "#")):
            break
        match = re.match(r"^  (?P<event>[a-z_]+):", line)
        if in_on_block and match:
            triggers.add(match.group("event"))
    return triggers


def without_comments(text: str) -> str:
    """Drop full-line comments, so that explanations do not count as workflow content."""
    return "\n".join(line for line in text.splitlines() if not line.lstrip().startswith("#"))


def runs_pull_request_code_privileged(text: str) -> bool:
    """Tell whether a workflow runs on pull_request_target and references the pull request head."""
    content = without_comments(text)
    return "pull_request_target" in workflow_trigger_names(text) and any(
        expression in content for expression in PULL_REQUEST_HEAD_EXPRESSIONS
    )


def privileged_pull_request_workflows() -> list[Path]:
    """Return the workflows that build pull request code in the pull_request_target context."""
    return [path for path in active_workflow_paths() if runs_pull_request_code_privileged(read_workflow(path))]


def test_workflow_trigger_names_ignores_nested_keys() -> None:
    text = "on:\n  push:\n    branches:\n      - develop\n  pull_request:\n\njobs:\n  build:\n"
    assert workflow_trigger_names(text) == {"push", "pull_request"}


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("on:\n  pull_request_target:\njobs:\n  ref: ${{ github.event.pull_request.head.sha }}\n", True),
        ("on:\n  pull_request_target:\njobs:\n  # never use github.event.pull_request.head.sha\n", False),
        ("on:\n  pull_request:\njobs:\n  ref: ${{ github.event.pull_request.head.sha }}\n", False),
    ],
)
def test_runs_pull_request_code_privileged(text: str, expected: bool) -> None:
    assert runs_pull_request_code_privileged(text) is expected


def test_cache_pattern_detects_cache_action_and_setup_cache() -> None:
    assert CACHE_PATTERN.search("      - uses: actions/cache@v6\n")
    assert CACHE_PATTERN.search("        uses: actions/cache/restore@v6\n")
    assert CACHE_PATTERN.search("          cache: pip\n")
    assert not CACHE_PATTERN.search("      # No caches here\n")


def test_sonar_pull_request_workflow_is_the_only_privileged_pull_request_build() -> None:
    assert privileged_pull_request_workflows() == [SONAR_PR_WORKFLOW_PATH]


@pytest.mark.parametrize("workflow_path", privileged_pull_request_workflows(), ids=lambda path: path.name)
def test_privileged_pull_request_build_is_gated_and_read_only(workflow_path: Path) -> None:
    text = read_workflow(workflow_path)

    assert TRUSTED_SOURCE_GATE_STEP in text
    assert text.index(TRUSTED_SOURCE_GATE_STEP) < text.index(PULL_REQUEST_CHECKOUT_STEP)
    assert "permissions:\n  contents: read\n" in text
    assert ": write" not in without_comments(text)
    assert "persist-credentials: false" in text


@pytest.mark.parametrize("workflow_path", privileged_pull_request_workflows(), ids=lambda path: path.name)
def test_privileged_pull_request_build_uses_only_the_sonar_token(workflow_path: Path) -> None:
    secrets = set(SECRET_PATTERN.findall(without_comments(read_workflow(workflow_path))))

    assert secrets <= ALLOWED_PULL_REQUEST_CODE_SECRETS


@pytest.mark.parametrize("workflow_path", active_workflow_paths(), ids=lambda path: path.name)
def test_pull_request_target_workflows_without_pull_request_build_use_no_secrets(workflow_path: Path) -> None:
    text = read_workflow(workflow_path)
    if "pull_request_target" not in workflow_trigger_names(text) or runs_pull_request_code_privileged(text):
        return

    assert "allow-unsafe-pr-checkout" not in text
    assert not SECRET_PATTERN.search(without_comments(text))


@pytest.mark.parametrize("workflow_path", active_workflow_paths(), ids=lambda path: path.name)
def test_no_workflow_restores_caches_while_pull_request_code_runs_privileged(workflow_path: Path) -> None:
    if not privileged_pull_request_workflows():
        return

    assert not CACHE_PATTERN.search(without_comments(read_workflow(workflow_path))), (
        f"{workflow_path.name} uses a cache, which privileged pull request builds can poison"
    )


@pytest.mark.parametrize(
    "workflow_path", [SONAR_PR_WORKFLOW_PATH, SONAR_BRANCH_WORKFLOW_PATH], ids=lambda path: path.name
)
def test_sonar_workflows_pin_actions_to_commit_shas(workflow_path: Path) -> None:
    actions = USES_PATTERN.findall(without_comments(read_workflow(workflow_path)))

    assert actions
    for action in actions:
        assert PINNED_ACTION_PATTERN.match(action), f"{action} is not pinned to a full commit SHA"

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
    "github.event.pull_request.merge_commit_sha",
    "github.head_ref",
    "refs/pull/",
)
ALL_SECRETS = "*"
# a secret by name, with the dot or the index notation
SECRET_PATTERN = re.compile(r"secrets(?:\.(?P<name>[A-Za-z0-9_]+)|\[\s*['\"](?P<indexed>[A-Za-z0-9_]+)['\"]\s*\])")
EXPRESSION_PATTERN = re.compile(r"\$\{\{(?P<expression>.*?)\}\}", re.DOTALL)
WHOLE_SECRETS_CONTEXT_PATTERN = re.compile(r"\bsecrets\b(?!\s*[.\[])")
INHERITED_SECRETS_PATTERN = re.compile(r"^\s+secrets:\s*inherit\b", re.MULTILINE)
# the cache action (quoted or not) and every input or key about caching (cache, cache-jdk, enable-cache, ...)
CACHE_PATTERN = re.compile(
    r"^\s+(?:- )?uses:\s*['\"]?actions/cache[@/]|^\s+(?:- )?[A-Za-z_-]*cache[A-Za-z_-]*\s*:",
    re.MULTILINE | re.IGNORECASE,
)
USES_PATTERN = re.compile(r"^\s+(?:- )?uses:\s*['\"]?(?P<action>[^'\"\s]+)", re.MULTILINE)
PINNED_ACTION_PATTERN = re.compile(r"^[^@\s]+@[0-9a-f]{40}$")
WRITE_PERMISSION_PATTERN = re.compile(r":\s*['\"]?write(?:-all)?['\"]?\s*$", re.MULTILINE)
ON_KEY_PATTERN = re.compile(r"""^(?:on|'on'|"on")\s*:\s*(?P<inline>.*)$""")
EVENT_NAME_PATTERN = re.compile(r"[a-z_]+")


def active_workflow_paths() -> list[Path]:
    """Return all workflows GitHub would run (disabled ones are not named *.yml)."""
    return sorted(WORKFLOW_DIRECTORY.glob("*.yml"))


def read_workflow(path: Path) -> str:
    """Read a workflow from the repository."""
    return path.read_text(encoding="utf-8")


def workflow_trigger_names(text: str) -> set[str]:
    """
    Collect the event names of the top-level 'on' key.

    Covers the inline forms (on: push, on: [push, pull_request]), the quoted key and the block forms with event keys
    or list items; nested keys of an event (branches, types, ...) are not events.
    """
    triggers: set[str] = set()
    event_indent: int | None = None
    in_on_block = False
    for line in without_comments(text).splitlines():
        on_key = ON_KEY_PATTERN.match(line)
        if on_key:
            inline = on_key.group("inline").split("#", 1)[0]
            triggers.update(inline_event_names(inline))
            in_on_block = not inline.strip()
            continue
        if not in_on_block or not line.strip():
            continue
        if not line.startswith(" "):
            break
        indent = len(line) - len(line.lstrip())
        if event_indent is None:
            event_indent = indent
        if indent == event_indent:
            event = EVENT_NAME_PATTERN.match(line.strip().removeprefix("- ").strip())
            if event:
                triggers.add(event.group())
    return triggers


def inline_event_names(inline: str) -> set[str]:
    """Event names of an inline 'on' value: a scalar, a flow list or the top-level keys of a flow mapping."""
    value = inline.strip()
    if not value.startswith("{"):
        return set(EVENT_NAME_PATTERN.findall(value))
    names: set[str] = set()
    depth = 0
    key = ""
    for char in value:
        if char in "{[":
            depth += 1
        elif char in "}]":
            depth -= 1
        elif depth == 1 and char in ":,":
            names.update(EVENT_NAME_PATTERN.findall(key))
        elif depth == 1:
            key += char
            continue
        key = ""
    return names


def secret_names(text: str) -> set[str]:
    """Return the secrets a workflow uses; ALL_SECRETS if it passes on the whole secrets context."""
    content = without_comments(text)
    names = {match.group("name") or match.group("indexed") for match in SECRET_PATTERN.finditer(content)}
    whole_context = INHERITED_SECRETS_PATTERN.search(content) or any(
        WHOLE_SECRETS_CONTEXT_PATTERN.search(match.group("expression"))
        for match in EXPRESSION_PATTERN.finditer(content)
    )
    if whole_context:
        names.add(ALL_SECRETS)
    return names


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
    "text",
    [
        "on: pull_request_target\njobs:\n",
        "on: [push, pull_request_target]\njobs:\n",
        "'on':\n  pull_request_target:\n    types: [opened]\njobs:\n",
        '"on":\n  pull_request_target:\njobs:\n',
        "on:\n  - push\n  - pull_request_target\njobs:\n",
        "on: {pull_request_target: {types: [opened]}}\njobs:\n",
    ],
)
def test_workflow_trigger_names_covers_all_forms(text: str) -> None:
    assert "pull_request_target" in workflow_trigger_names(text)
    assert "types" not in workflow_trigger_names(text)


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("env:\n  TOKEN: ${{ secrets.SONAR_TOKEN }}\n", {"SONAR_TOKEN"}),
        ("env:\n  TOKEN: ${{ secrets['OTHER'] }}\n", {"OTHER"}),
        ("env:\n  ALL: ${{ toJSON(secrets) }}\n", {ALL_SECRETS}),
        ("jobs:\n  call:\n    uses: ./.github/workflows/x.yml\n    secrets: inherit\n", {ALL_SECRETS}),
        ("run: sudo ls /usr/local/fworch/etc/secrets/ca\n", set[str]()),
        ("# ${{ secrets.COMMENTED }}\n", set[str]()),
    ],
)
def test_secret_names_finds_every_access(text: str, expected: set[str]) -> None:
    assert secret_names(text) == expected


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


@pytest.mark.parametrize(
    "line",
    [
        "      - uses: actions/cache@v6\n",
        "        uses: actions/cache/restore@v6\n",
        '        uses: "actions/cache@v6"\n',
        "          cache: pip\n",
        "          cache-jdk: true\n",
        "          enable-cache: true\n",
        "          cache-dependency-path: requirements.txt\n",
    ],
)
def test_cache_pattern_detects_cache_action_and_cache_inputs(line: str) -> None:
    assert CACHE_PATTERN.search(line)


def test_cache_pattern_ignores_other_lines() -> None:
    assert not CACHE_PATTERN.search("      - name: Install scanner\n        run: dotnet tool update\n")


@pytest.mark.parametrize(
    ("line", "expected"),
    [
        ("permissions:\n  contents: write\n", True),
        ("permissions:\n  contents: 'write'\n", True),
        ("permissions: write-all\n", True),
        ("permissions:\n  contents: read\n", False),
    ],
)
def test_write_permission_pattern(line: str, expected: bool) -> None:
    assert bool(WRITE_PERMISSION_PATTERN.search(line)) is expected


def test_uses_pattern_reads_quoted_actions() -> None:
    assert USES_PATTERN.findall('      - uses: "actions/checkout@v7"\n') == ["actions/checkout@v7"]


def test_merge_ref_checkout_counts_as_pull_request_code() -> None:
    text = "on:\n  pull_request_target:\njobs:\n  ref: refs/pull/${{ github.event.pull_request.number }}/merge\n"
    assert runs_pull_request_code_privileged(text)


def test_sonar_pull_request_workflow_is_the_only_privileged_pull_request_build() -> None:
    assert privileged_pull_request_workflows() == [SONAR_PR_WORKFLOW_PATH]


@pytest.mark.parametrize("workflow_path", privileged_pull_request_workflows(), ids=lambda path: path.name)
def test_privileged_pull_request_build_is_gated_and_read_only(workflow_path: Path) -> None:
    text = read_workflow(workflow_path)

    assert TRUSTED_SOURCE_GATE_STEP in text
    assert text.index(TRUSTED_SOURCE_GATE_STEP) < text.index(PULL_REQUEST_CHECKOUT_STEP)
    assert "permissions:\n  contents: read\n" in text
    assert not WRITE_PERMISSION_PATTERN.search(without_comments(text))
    assert "persist-credentials: false" in text


@pytest.mark.parametrize("workflow_path", privileged_pull_request_workflows(), ids=lambda path: path.name)
def test_privileged_pull_request_build_uses_only_the_sonar_token(workflow_path: Path) -> None:
    assert secret_names(read_workflow(workflow_path)) <= ALLOWED_PULL_REQUEST_CODE_SECRETS


@pytest.mark.parametrize("workflow_path", active_workflow_paths(), ids=lambda path: path.name)
def test_pull_request_target_workflows_without_pull_request_build_use_no_secrets(workflow_path: Path) -> None:
    text = read_workflow(workflow_path)
    if "pull_request_target" not in workflow_trigger_names(text) or runs_pull_request_code_privileged(text):
        return

    assert "allow-unsafe-pr-checkout" not in text
    assert not secret_names(text)


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

"""
Policy tests that bound the accepted risk of building pull request code in the privileged
pull_request_target context (GHSA-3cwm-h5cm-r3f8) and keep every used action pinned (GHSA-8hf3-3hp5-gj32).

The workflows are read with a YAML parser, as GitHub reads them: comments, quoting, anchors and flow collections
cannot hide a trigger, a secret, a write permission or a checkout of the pull request head.
"""

from __future__ import annotations

import re
from pathlib import Path
from typing import TYPE_CHECKING, Any, cast

import pytest
import yaml

if TYPE_CHECKING:
    from collections.abc import Iterator

WORKFLOW_DIRECTORY = Path(__file__).parents[2] / ".github" / "workflows"
SONAR_PR_WORKFLOW_PATH = WORKFLOW_DIRECTORY / "sonarcloud-pr.yml"
SONAR_BRANCH_WORKFLOW_PATH = WORKFLOW_DIRECTORY / "sonarcloud.yml"
TRUSTED_SOURCE_GATE_STEP_NAME = "Gate trusted PR sources"
PULL_REQUEST_CHECKOUT_STEP_NAME = "Checkout PR head"
CHECKOUT_ACTION = "actions/checkout"
ALLOWED_PULL_REQUEST_CODE_SECRETS = frozenset({"SONAR_TOKEN"})
READ_ONLY_PERMISSIONS = {"contents": "read"}
WRITE_PERMISSION_VALUES = frozenset({"write", "write-all"})
PULL_REQUEST_HEAD_EXPRESSIONS = (
    "github.event.pull_request.head.sha",
    "github.event.pull_request.head.ref",
    "github.event.pull_request.merge_commit_sha",
    "github.head_ref",
    "refs/pull/",
)
# commands that fetch the pull request head without one of the expressions above
PULL_REQUEST_FETCH_PATTERN = re.compile(r"\bgh\s+pr\s+checkout\b|\bpull/[^/\n]+/(?:head|merge)\b")
ALL_SECRETS = "*"
# a secret by name, with the dot or the index notation
SECRET_PATTERN = re.compile(r"\bsecrets(?:\.(?P<name>[A-Za-z0-9_]+)|\[\s*['\"](?P<indexed>[A-Za-z0-9_]+)['\"]\s*\])")
# an index that is not a quoted name selects the secret at run time (secrets[matrix.name], secrets[format(...)])
DYNAMIC_SECRET_INDEX_PATTERN = re.compile(r"\bsecrets\s*\[(?!\s*['\"][A-Za-z0-9_]+['\"]\s*\])")
EXPRESSION_PATTERN = re.compile(r"\$\{\{(?P<expression>.*?)\}\}", re.DOTALL)
WHOLE_SECRETS_CONTEXT_PATTERN = re.compile(r"\bsecrets\b(?!\s*[.\[])")
# the value of an "if" key is an expression even without ${{ }}
CONDITION_KEY = "if"
# the cache action (and its restore / save sub actions) and every input or key about caching
CACHE_ACTION_PATTERN = re.compile(r"^actions/cache(?:[@/]|$)", re.IGNORECASE)
CACHE_KEY_PATTERN = re.compile(r"cache", re.IGNORECASE)
PINNED_ACTION_PATTERN = re.compile(r"^[^@\s]+@[0-9a-f]{40}$")
PINNED_DOCKER_ACTION_PATTERN = re.compile(r"^docker://[^@\s]+@sha256:[0-9a-f]{64}$")
LOCAL_ACTION_PREFIX = "./"
YAML_BOOL_TAG = "tag:yaml.org,2002:bool"


class WorkflowLoader(yaml.SafeLoader):
    """
    Safe loader with the booleans of YAML 1.2, which GitHub uses: only true and false are booleans.

    YAML 1.1 (the PyYAML default) also reads on, off, yes and no as booleans, which would turn the 'on' key into True.
    """


WorkflowLoader.yaml_implicit_resolvers = {
    first_char: [(tag, pattern) for tag, pattern in resolvers if tag != YAML_BOOL_TAG]
    for first_char, resolvers in yaml.SafeLoader.yaml_implicit_resolvers.items()
}
WorkflowLoader.add_implicit_resolver(  # pyright: ignore[reportUnknownMemberType]
    YAML_BOOL_TAG, re.compile(r"^(?:true|True|TRUE|false|False|FALSE)$"), list("tTfF")
)


def active_workflow_paths() -> list[Path]:
    """Return all workflows GitHub would run (disabled ones are not named *.yml)."""
    return sorted(WORKFLOW_DIRECTORY.glob("*.yml"))


def read_workflow(path: Path) -> str:
    """Read a workflow from the repository."""
    return path.read_text(encoding="utf-8")


def parse_workflow(text: str) -> dict[Any, Any]:
    """Parse a workflow; anything but a mapping (e.g. only comments) is an empty workflow."""
    document = yaml.load(text, Loader=WorkflowLoader)  # noqa: S506 - WorkflowLoader is a SafeLoader
    return cast("dict[Any, Any]", document) if isinstance(document, dict) else {}


def mapping_items(node: object) -> Iterator[tuple[object, object]]:
    """Yield the key and value of every mapping entry in the parsed workflow, at any depth (without recursion)."""
    pending: list[object] = [node]
    while pending:
        current = pending.pop()
        if isinstance(current, dict):
            for key, value in cast("dict[object, object]", current).items():
                yield key, value
                pending.append(value)
        elif isinstance(current, list):
            pending.extend(cast("list[object]", current))


def string_values(node: object) -> Iterator[tuple[object, str]]:
    """Yield every string value with the key it belongs to (None for list items and the document itself)."""
    if isinstance(node, str):
        yield None, node
    for key, value in mapping_items(node):
        if isinstance(value, str):
            yield key, value
        elif isinstance(value, list):
            yield from ((key, item) for item in cast("list[object]", value) if isinstance(item, str))


def workflow_trigger_names(text: str) -> set[str]:
    """Collect the event names of the top-level 'on' key: a scalar, a list or the keys of a mapping."""
    triggers: object = parse_workflow(text).get("on")
    if isinstance(triggers, str):
        return {triggers}
    if isinstance(triggers, list):
        return {trigger for trigger in cast("list[object]", triggers) if isinstance(trigger, str)}
    if isinstance(triggers, dict):
        return {trigger for trigger in cast("dict[object, object]", triggers) if isinstance(trigger, str)}
    return set()


def expressions(key: object, value: str) -> list[str]:
    """Return the expressions in a value: the ${{ }} parts, or the whole value of a condition."""
    found = [match.group("expression") for match in EXPRESSION_PATTERN.finditer(value)]
    if key == CONDITION_KEY:
        found.append(value)
    return found


def secret_names(text: str) -> set[str]:
    """Return the secrets a workflow uses; ALL_SECRETS if it can reach any secret."""
    workflow = parse_workflow(text)
    names: set[str] = set()
    for key, value in string_values(workflow):
        names.update(match.group("name") or match.group("indexed") for match in SECRET_PATTERN.finditer(value))
        if DYNAMIC_SECRET_INDEX_PATTERN.search(value) or any(
            WHOLE_SECRETS_CONTEXT_PATTERN.search(expression) for expression in expressions(key, value)
        ):
            names.add(ALL_SECRETS)
    if any(key == "secrets" and value == "inherit" for key, value in mapping_items(workflow)):
        names.add(ALL_SECRETS)
    return names


def write_permissions(text: str) -> list[str]:
    """Return the write permissions of a workflow and its jobs, as 'scope: value'."""
    found: list[str] = []
    for key, value in mapping_items(parse_workflow(text)):
        if key != "permissions":
            continue
        if isinstance(value, str) and value in WRITE_PERMISSION_VALUES:
            found.append(f"permissions: {value}")
        elif isinstance(value, dict):
            found.extend(
                f"{scope}: {level}"
                for scope, level in cast("dict[object, object]", value).items()
                if level in WRITE_PERMISSION_VALUES
            )
    return found


def references_pull_request_head(value: str) -> bool:
    """Tell whether a value names or fetches the pull request head."""
    return any(expression in value for expression in PULL_REQUEST_HEAD_EXPRESSIONS) or bool(
        PULL_REQUEST_FETCH_PATTERN.search(value)
    )


def runs_pull_request_code_privileged(text: str) -> bool:
    """Tell whether a workflow runs on pull_request_target and references the pull request head."""
    return "pull_request_target" in workflow_trigger_names(text) and any(
        references_pull_request_head(value) for _, value in string_values(parse_workflow(text))
    )


def uses_cache(text: str) -> bool:
    """Tell whether a workflow uses the cache action or any key or input about caching (cache, cache-jdk, ...)."""
    return any(
        (isinstance(key, str) and CACHE_KEY_PATTERN.search(key))
        or (key == "uses" and isinstance(value, str) and CACHE_ACTION_PATTERN.match(value.strip()))
        for key, value in mapping_items(parse_workflow(text))
    )


def used_actions(text: str) -> list[str]:
    """Return the actions and reusable workflows the steps and jobs of a workflow use."""
    return [value for key, value in mapping_items(parse_workflow(text)) if key == "uses" and isinstance(value, str)]


def is_pinned_action(action: str) -> bool:
    """Return whether an action is immutable: a full commit SHA, an image digest, or an action of this repository."""
    return (
        action.startswith(LOCAL_ACTION_PREFIX)
        or PINNED_ACTION_PATTERN.match(action) is not None
        or PINNED_DOCKER_ACTION_PATTERN.match(action) is not None
    )


def job_steps(text: str) -> list[list[dict[Any, Any]]]:
    """Return the steps of each job of a workflow."""
    jobs: object = parse_workflow(text).get("jobs")
    if not isinstance(jobs, dict):
        return []
    steps_per_job: list[list[dict[Any, Any]]] = []
    for job in cast("dict[object, object]", jobs).values():
        steps: object = cast("dict[object, object]", job).get("steps") if isinstance(job, dict) else None
        if isinstance(steps, list):
            steps_per_job.append([step for step in cast("list[object]", steps) if isinstance(step, dict)])
    return steps_per_job


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
        "on:\n  'pull_request_target':\n    types: [opened]\n",
        "on: ['push', \"pull_request_target\"]\n",
        "on: [\n  push,\n  pull_request_target\n]\n",
        "on: {\n  push: {},\n  pull_request_target: {types: [opened]}\n}\n",
        "x-triggers: &triggers\n  pull_request_target:\n    types: [opened]\non: *triggers\n",
        "x-trigger: &trigger pull_request_target\non: [push, *trigger]\n",
        "on: # the events\n  pull_request_target: # privileged\n",
    ],
)
def test_workflow_trigger_names_covers_all_forms(text: str) -> None:
    assert "pull_request_target" in workflow_trigger_names(text)
    assert "types" not in workflow_trigger_names(text)


def test_workflow_loader_keeps_on_and_yes_as_strings() -> None:
    assert parse_workflow("on: push\nkey: yes\nflag: false\n") == {"on": "push", "key": "yes", "flag": False}


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("env:\n  TOKEN: ${{ secrets.SONAR_TOKEN }}\n", {"SONAR_TOKEN"}),
        ("env:\n  TOKEN: ${{ secrets['OTHER'] }}\n", {"OTHER"}),
        ("env:\n  ALL: ${{ toJSON(secrets) }}\n", {ALL_SECRETS}),
        ("jobs:\n  call:\n    uses: ./.github/workflows/x.yml\n    secrets: inherit\n", {ALL_SECRETS}),
        ("run: sudo ls /usr/local/fworch/etc/secrets/ca\n", set[str]()),
        ("# ${{ secrets.COMMENTED }}\n", set[str]()),
        ("env:\n  TOKEN: ${{ secrets[matrix.name] }}\n", {ALL_SECRETS}),
        ("env:\n  TOKEN: ${{ secrets[format('{0}_TOKEN', 'SONAR')] }}\n", {ALL_SECRETS}),
        ("env:\n  TOKEN: ${{ secrets [ inputs.secret ] }}\n", {ALL_SECRETS}),
        ("steps:\n  - if: secrets.SONAR_TOKEN != ''\n", {"SONAR_TOKEN"}),
        ("steps:\n  - if: contains(toJSON(secrets), 'x')\n", {ALL_SECRETS}),
        ("x-env: &env\n  TOKEN: ${{ secrets.ANCHORED }}\nenv: *env\n", {"ANCHORED"}),
        ("env: {TOKEN: '${{ secrets.FLOW }}'}\n", {"FLOW"}),
        ("steps:\n  - run: echo 'no ${{ secrets.QUOTED }} hiding'\n", {"QUOTED"}),
        ("env:\n  TOKEN: ${{ secrets.SONAR_TOKEN }} # ${{ secrets.COMMENTED }}\n", {"SONAR_TOKEN"}),
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
        (
            "on: pull_request_target\njobs:\n  a:\n    steps:\n      - run: gh pr checkout ${{ github.event.number }}\n",
            True,
        ),
        (
            "on: pull_request_target\njobs:\n  a:\n    steps:\n      - run: git fetch origin pull/${{ github.event.number }}/head\n",
            True,
        ),
        ("on: pull_request_target\njobs:\n  a:\n    steps:\n      - run: git fetch origin develop\n", False),
    ],
)
def test_runs_pull_request_code_privileged(text: str, expected: bool) -> None:
    assert runs_pull_request_code_privileged(text) is expected


@pytest.mark.parametrize(
    "text",
    [
        "steps:\n  - uses: actions/cache@v6\n",
        "steps:\n  - uses: actions/cache/restore@v6\n",
        'steps:\n  - uses: "actions/cache@v6"\n',
        "steps:\n  - with:\n      cache: pip\n",
        "steps:\n  - with:\n      cache-jdk: true\n",
        "steps:\n  - with:\n      enable-cache: true\n",
        "steps:\n  - with:\n      cache-dependency-path: requirements.txt\n",
        "steps:\n  - {uses: actions/cache@v6}\n",
    ],
)
def test_uses_cache_detects_cache_action_and_cache_inputs(text: str) -> None:
    assert uses_cache(text)


def test_uses_cache_ignores_other_steps_and_comments() -> None:
    assert not uses_cache("steps:\n  - name: Install scanner # no cache: here\n    run: dotnet tool update\n")


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("permissions:\n  contents: write\n", ["contents: write"]),
        ("permissions:\n  contents: 'write'\n", ["contents: write"]),
        ("permissions: write-all\n", ["permissions: write-all"]),
        ("permissions:\n  contents: read\n", []),
        ("permissions:\n  pull-requests: write # comment\n", ["pull-requests: write"]),
        ("permissions: {contents: write}\n", ["contents: write"]),
        ("jobs:\n  a:\n    permissions: {contents: read, packages: write}\n", ["packages: write"]),
        ("permissions:\n  contents: read\n# permissions:\n#   contents: write\n", []),
    ],
)
def test_write_permissions(text: str, expected: list[str]) -> None:
    assert write_permissions(text) == expected


def test_used_actions_reads_quoted_actions_and_reusable_workflows() -> None:
    text = 'jobs:\n  a:\n    steps:\n      - uses: "actions/checkout@v7"\n  b:\n    uses: ./.github/workflows/x.yml\n'
    assert sorted(used_actions(text)) == ["./.github/workflows/x.yml", "actions/checkout@v7"]


def test_merge_ref_checkout_counts_as_pull_request_code() -> None:
    text = "on:\n  pull_request_target:\njobs:\n  ref: refs/pull/${{ github.event.pull_request.number }}/merge\n"
    assert runs_pull_request_code_privileged(text)


def test_sonar_pull_request_workflow_is_the_only_privileged_pull_request_build() -> None:
    assert privileged_pull_request_workflows() == [SONAR_PR_WORKFLOW_PATH]


@pytest.mark.parametrize("workflow_path", privileged_pull_request_workflows(), ids=lambda path: path.name)
def test_privileged_pull_request_build_is_gated_and_read_only(workflow_path: Path) -> None:
    text = read_workflow(workflow_path)
    checkout_jobs = [
        [step.get("name") for step in steps]
        for steps in job_steps(text)
        if any(step.get("name") == PULL_REQUEST_CHECKOUT_STEP_NAME for step in steps)
    ]
    checkout_steps = [
        step
        for steps in job_steps(text)
        for step in steps
        if str(step.get("uses", "")).startswith(CHECKOUT_ACTION + "@")
    ]

    assert checkout_jobs
    for step_names in checkout_jobs:
        assert TRUSTED_SOURCE_GATE_STEP_NAME in step_names[: step_names.index(PULL_REQUEST_CHECKOUT_STEP_NAME)]
    assert parse_workflow(text).get("permissions") == READ_ONLY_PERMISSIONS
    assert not write_permissions(text)
    assert checkout_steps
    for step in checkout_steps:
        assert cast("dict[object, object]", step.get("with", {})).get("persist-credentials") is False


@pytest.mark.parametrize("workflow_path", privileged_pull_request_workflows(), ids=lambda path: path.name)
def test_privileged_pull_request_build_uses_only_the_sonar_token(workflow_path: Path) -> None:
    assert secret_names(read_workflow(workflow_path)) <= ALLOWED_PULL_REQUEST_CODE_SECRETS


@pytest.mark.parametrize("workflow_path", active_workflow_paths(), ids=lambda path: path.name)
def test_pull_request_target_workflows_without_pull_request_build_use_no_secrets(workflow_path: Path) -> None:
    text = read_workflow(workflow_path)
    if "pull_request_target" not in workflow_trigger_names(text) or runs_pull_request_code_privileged(text):
        return

    assert all(key != "allow-unsafe-pr-checkout" for key, _ in mapping_items(parse_workflow(text)))
    assert not secret_names(text)


@pytest.mark.parametrize("workflow_path", active_workflow_paths(), ids=lambda path: path.name)
def test_no_workflow_restores_caches_while_pull_request_code_runs_privileged(workflow_path: Path) -> None:
    if not privileged_pull_request_workflows():
        return

    assert not uses_cache(read_workflow(workflow_path)), (
        f"{workflow_path.name} uses a cache, which privileged pull request builds can poison"
    )


@pytest.mark.parametrize(
    "workflow_path", [SONAR_PR_WORKFLOW_PATH, SONAR_BRANCH_WORKFLOW_PATH], ids=lambda path: path.name
)
def test_sonar_workflows_use_actions(workflow_path: Path) -> None:
    assert used_actions(read_workflow(workflow_path))


@pytest.mark.parametrize("workflow_path", active_workflow_paths(), ids=lambda path: path.name)
def test_workflows_pin_actions_to_commit_shas(workflow_path: Path) -> None:
    # a tag can be moved after review, a commit SHA cannot (GHSA-8hf3-3hp5-gj32)
    for action in used_actions(read_workflow(workflow_path)):
        assert is_pinned_action(action), (
            f"{workflow_path.name}: {action} is not pinned to a full commit SHA or image digest"
        )


@pytest.mark.parametrize(
    ("action", "expected"),
    [
        ("actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1", True),
        ("github/codeql-action/init@2892aa5e19bbd11bc0cff5427e3b750a04d9e3c2", True),
        ("./.github/actions/local", True),
        ("docker://alpine@sha256:" + "a" * 64, True),
        ("actions/checkout@v7", False),
        ("actions/checkout@v7.0.1", False),
        ("actions/checkout@3d3c42e", False),
        ("actions/checkout", False),
        ("docker://alpine:3.20", False),
    ],
)
def test_is_pinned_action(action: str, *, expected: bool) -> None:
    assert is_pinned_action(action) is expected

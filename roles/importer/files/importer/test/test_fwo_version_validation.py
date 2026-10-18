"""Exercise trusted version validation for contributor forks and upstream PRs."""

from __future__ import annotations

import pytest

from scripts import fwo_version_reservations as versions
from scripts.ci.version_gate import GateFiles, evaluate_gate, sealed_versions
from scripts.tests.test_fwo_version_reservations import LABEL, FakeGitHub, pull_request

OPEN_DEVELOP_VERSION = "9.7.1"
NEXT_VERSION = "9.7.2"
DEVELOP_UPGRADE_FILES = ["9.7.0.sql"]


@pytest.fixture
def version_client() -> FakeGitHub:
    client = FakeGitHub([pull_request(2, LABEL, "contributor/fork")], {"develop": "9.6.2", "sha-2": "9.6.3"})
    client.files_by_ref["sha-2"] = {
        versions.REVISION_HISTORY_FILE: "## 9.6.3 - 09.10.2026\n",
        f"{versions.UPGRADE_DIRECTORY}9.6.3.sql": "SELECT 1;\n",
    }
    return client


def test_validate_fork_publishes_success_on_current_head(version_client: FakeGitHub) -> None:
    arguments = versions.build_parser().parse_args(["validate", "--pull-request", "2"])
    assert versions.run_command(arguments, version_client, "upstream/repo") == {}
    assert version_client.posts == [
        ("/check-runs", {"name": "Validate FWO PR version", "head_sha": "sha-2", "status": "in_progress"})
    ]
    assert version_client.patches[0][0] == "/check-runs/123"
    assert version_client.patches[0][1]["conclusion"] == "success"


@pytest.mark.parametrize(
    ("files", "message"),
    [
        ({}, "Missing versioned file"),
        ({versions.REVISION_HISTORY_FILE: "## 9.6.3 - 09.10.2026\n"}, "Missing versioned file"),
        (
            {
                versions.REVISION_HISTORY_FILE: "## 9.6.3 - 09.10.2026\n## 999.0.0\n",
                f"{versions.UPGRADE_DIRECTORY}9.6.3.sql": "SELECT 1;",
            },
            "leftover 999.0.0",
        ),
        (
            {
                versions.REVISION_HISTORY_FILE: "## 9.6.3 - 09.10.2026\n",
                f"{versions.UPGRADE_DIRECTORY}9.6.3.sql": "SELECT 1;",
                f"{versions.UPGRADE_DIRECTORY}999.0.0.sql": "SELECT 2;",
            },
            "leftover 999.0.0",
        ),
    ],
)
def test_validate_fork_reports_invalid_content(version_client: FakeGitHub, files: dict[str, str], message: str) -> None:
    version_client.files_by_ref["sha-2"] = files
    with pytest.raises(versions.VersionError, match=message):
        versions.validate_pull_request(version_client, 2)
    assert version_client.patches[0][1]["conclusion"] == "failure"


def test_validate_fork_fails_on_extra_upgrade_script(version_client: FakeGitHub) -> None:
    version_client.changed_files = [{"filename": f"{versions.UPGRADE_DIRECTORY}9.6.4.sql", "status": "added"}]
    with pytest.raises(versions.VersionError, match="Unexpected upgrade script"):
        versions.validate_pull_request(version_client, 2)
    assert version_client.patches[0][1]["conclusion"] == "failure"


def test_validate_unlabelled_fork_checks_versioned_diff(version_client: FakeGitHub) -> None:
    version_client.pull_requests[0]["labels"] = []
    versions.validate_pull_request(version_client, 2)
    assert version_client.patches[-1][1]["conclusion"] == "success"
    version_client.changed_files = [{"filename": f"{versions.UPGRADE_DIRECTORY}9.6.3.sql", "status": "added"}]
    with pytest.raises(versions.VersionError, match="Add the versioned-change label"):
        versions.validate_pull_request(version_client, 2)
    assert version_client.patches[-1][1]["conclusion"] == "failure"


def test_validate_closed_pr_does_not_publish_check(version_client: FakeGitHub) -> None:
    version_client.pull_requests[0]["state"] = "closed"
    with pytest.raises(versions.VersionError, match="open pull requests"):
        versions.validate_pull_request(version_client, 2)
    assert version_client.posts == []


def test_fork_content_is_never_executed(version_client: FakeGitHub) -> None:
    # Shell syntax in Markdown and SQL is only written as data for the checker.
    content = "$(exit 91)\n`exit 92`\n"
    version_client.files_by_ref["sha-2"][versions.REVISION_HISTORY_FILE] += content
    version_client.files_by_ref["sha-2"][f"{versions.UPGRADE_DIRECTORY}9.6.3.sql"] += content
    versions.validate_pull_request(version_client, 2)
    assert version_client.patches[0][1]["conclusion"] == "success"


def develop_client(labels: list[versions.JsonObject], pr_version: str, tags: list[str]) -> FakeGitHub:
    """Returns a PR that adds the upgrade script of its version while develop is on OPEN_DEVELOP_VERSION."""
    client = FakeGitHub(
        [pull_request(2, labels)],
        {"develop": OPEN_DEVELOP_VERSION, "sha-2": pr_version},
        changed_files=[{"filename": f"{versions.UPGRADE_DIRECTORY}{pr_version}.sql", "status": "added"}],
        tags=tags,
    )
    client.files_by_ref["sha-2"] = {
        versions.REVISION_HISTORY_FILE: f"## {pr_version} - 18.10.2026\n",
        f"{versions.UPGRADE_DIRECTORY}{pr_version}.sql": "SELECT 1;\n",
    }
    return client


def gate_passes(pr_version: str, tags: list[str]) -> bool:
    """Evaluates the Version gate for the merge result of a PR built by develop_client."""
    added_file = f"{pr_version}.sql"
    files = GateFiles(merged_upgrade_files=[*DEVELOP_UPGRADE_FILES, added_file], changed_upgrade_files=[added_file])
    return evaluate_gate(pr_version, OPEN_DEVELOP_VERSION, sealed_versions(tags), files).ok


def test_change_to_develops_open_version_passes_validator_and_gate() -> None:
    tags = ["v9.7.0-dev"]
    client = develop_client([], OPEN_DEVELOP_VERSION, tags)
    versions.validate_pull_request(client, 2)
    assert client.patches[-1][1]["conclusion"] == "success"
    assert gate_passes(OPEN_DEVELOP_VERSION, tags)


@pytest.mark.parametrize("pr_version", [OPEN_DEVELOP_VERSION, NEXT_VERSION, versions.PLACEHOLDER_VERSION])
def test_reservation_while_develops_version_is_open_names_the_open_version(pr_version: str) -> None:
    client = develop_client(LABEL, pr_version, ["v9.7.0-dev"])
    with pytest.raises(versions.VersionError, match=r"9\.7\.1 is still open.*upgrade/9\.7\.1\.sql"):
        versions.validate_pull_request(client, 2)
    assert client.patches[-1][1]["conclusion"] == "failure"


def test_reservation_after_seal_passes_validator_and_gate() -> None:
    tags = ["v9.7.0-dev", f"v{OPEN_DEVELOP_VERSION}-dev"]
    client = develop_client(LABEL, NEXT_VERSION, tags)
    versions.validate_pull_request(client, 2)
    assert client.patches[-1][1]["conclusion"] == "success"
    assert gate_passes(NEXT_VERSION, tags)

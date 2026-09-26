import base64
import io
import urllib.error
from email.message import Message
from pathlib import Path
from typing import Any

import pytest

from scripts import fwo_version_reservations as versions
from scripts.fwo_version_reservations import JsonObject, Reservation, VersionError

REPOSITORY = "CactuseSecurity/firewall-orchestrator"
LABEL: list[JsonObject] = [{"name": "versioned-change"}]


class FakeGitHub:
    """Serves open PRs, product versions per ref, permissions, and changed files; records posts."""

    def __init__(
        self,
        pull_requests: list[JsonObject],
        versions_by_ref: dict[str, str],
        permission: str = "write",
        changed_files: list[JsonObject] | None = None,
    ) -> None:
        self.pull_requests = pull_requests
        self.versions_by_ref = versions_by_ref
        self.permission = permission
        self.changed_files = changed_files or []
        self.posts: list[tuple[str, JsonObject]] = []
        self.failing_refs: set[str] = set()

    def get(self, path: str) -> Any:
        if path.startswith("/contents/"):
            ref = path.split("?ref=")[1]
            content = f'product_version: "{self.versions_by_ref[ref]}"\n'.encode()
            return {"content": base64.b64encode(content).decode()}
        if path.startswith("/collaborators/"):
            return {"permission": self.permission}
        number = int(path.removeprefix("/pulls/"))
        return next(pull_request for pull_request in self.pull_requests if pull_request["number"] == number)

    def paginate(self, path: str) -> list[JsonObject]:
        return self.changed_files if path.endswith("/files") else self.pull_requests

    def post(self, path: str, body: JsonObject) -> None:
        if body["ref"] in self.failing_refs:
            raise urllib.error.HTTPError(path, 422, "No workflow on ref", Message(), io.BytesIO())
        self.posts.append((path, body))


def pull_request(number: int, labels: list[JsonObject], repository: str = REPOSITORY) -> JsonObject:
    return {
        "number": number,
        "labels": labels,
        "state": "open",
        "base": {"ref": "develop"},
        "head": {"sha": f"sha-{number}", "ref": f"branch-{number}", "repo": {"full_name": repository}},
    }


@pytest.mark.parametrize(
    ("left", "right", "expected"),
    [
        ("9.5.4", "9.5.6", -1),
        ("9.5.6", "9.5.4", 1),
        ("9.5.10", "9.5.9", 1),
        ("10.0.0", "9.9.9", 1),
        ("9.5.4", "9.5.4", 0),
    ],
)
def test_compare_versions_orders_numerically(left: str, right: str, expected: int) -> None:
    assert versions.compare_versions(left, right) == expected


def test_select_target_version_takes_greatest_reservation_regardless_of_order() -> None:
    assert versions.select_target_version("9.5.4", ["9.5.6", "9.5.5"], "patch") == "9.5.7"
    assert versions.select_target_version("9.5.4", ["9.5.5", "9.5.6"], "patch") == "9.5.7"
    assert versions.select_target_version("9.5.4", [], "patch") == "9.5.5"


def test_select_target_version_ignores_stale_and_other_line_reservations() -> None:
    assert versions.select_target_version("9.5.4", ["9.5.3", "9.6.0", "9.4.9"], "patch") == "9.5.5"


def test_select_target_version_starts_or_continues_next_line() -> None:
    assert versions.select_target_version("9.5.4", ["9.5.5"], "minor") == "9.6.0"
    assert versions.select_target_version("9.5.4", ["9.6.0"], "minor") == "9.6.1"
    assert versions.select_target_version("9.5.4", [], "major") == "10.0.0"


def test_find_reservation_conflict_rejects_duplicates_and_lower_open_reservations() -> None:
    open_reservations = [Reservation(7, "9.5.5")]
    assert "already reserved by PR #7" in (
        versions.find_reservation_conflict("9.5.5", "9.5.4", open_reservations) or ""
    )
    assert "PR #7 reserves 9.5.5 and must merge before 9.5.6" in (
        versions.find_reservation_conflict("9.5.6", "9.5.4", open_reservations) or ""
    )
    assert versions.find_reservation_conflict("9.5.6", "9.5.5", open_reservations) is None
    assert versions.find_reservation_conflict("9.5.5", "9.5.4", [Reservation(8, "9.5.6")]) is None


@pytest.mark.parametrize(
    ("body", "expected"),
    [
        ("/allocate-fwo-version", "patch"),
        ("/allocate-fwo-version minor", "minor"),
        ("/allocate-fwo-version major\n", "major"),
        ("/allocate-fwo-version 9.9.9", None),
        ("please /allocate-fwo-version", None),
        (None, None),
    ],
)
def test_parse_allocation_command_accepts_only_exact_commands(body: str | None, expected: str | None) -> None:
    assert versions.parse_allocation_command(body) == expected


def test_has_write_permission_reads_permission_string() -> None:
    assert versions.has_write_permission({"permission": "admin"})
    assert versions.has_write_permission({"permission": "write"})
    assert not versions.has_write_permission({"permission": "read"})
    assert not versions.has_write_permission({"user": {"permissions": {"admin": True}}})


def test_is_stale_version_flags_versions_not_above_develop() -> None:
    assert versions.is_stale_version("9.5.7", "9.6.0")
    assert versions.is_stale_version("9.6.0", "9.6.0")
    assert not versions.is_stale_version("9.6.1", "9.6.0")
    assert not versions.is_stale_version("999.0.0", "9.6.0")


def test_find_unlabelled_version_changes_detects_new_scripts_and_version_edits() -> None:
    changed_files: list[JsonObject] = [
        {"filename": "roles/database/files/upgrade/9.5.5.sql", "status": "added"},
        {"filename": "roles/database/files/upgrade/9.5.1.sql", "status": "modified"},
        {
            "filename": "inventory/group_vars/all.yml",
            "status": "modified",
            "patch": '-product_version: "9.5.4"\n+product_version: "9.5.5"',
        },
        {"filename": "README.md", "status": "modified"},
    ]
    assert versions.find_unlabelled_version_changes(changed_files) == [
        "roles/database/files/upgrade/9.5.5.sql (added)",
        "inventory/group_vars/all.yml (product_version changed)",
    ]
    other_edit: list[JsonObject] = [
        {"filename": "inventory/group_vars/all.yml", "status": "modified", "patch": "+other: 1"}
    ]
    assert versions.find_unlabelled_version_changes(other_edit) == []


def test_extract_product_version_accepts_quoted_and_unquoted_versions() -> None:
    assert versions.extract_product_version('a: 1\nproduct_version: "9.5.4"\n') == "9.5.4"
    assert versions.extract_product_version("product_version: 9.5.4\n") == "9.5.4"
    assert versions.extract_product_version('product_version: "9.5"\n') is None


def test_list_reservations_skips_current_unlabelled_and_placeholder_prs() -> None:
    client = FakeGitHub(
        [pull_request(1, LABEL), pull_request(2, LABEL), pull_request(3, []), pull_request(4, LABEL)],
        {"sha-1": "9.5.6", "sha-2": "999.0.0", "sha-3": "9.5.9", "sha-4": "9.5.5"},
    )
    assert versions.list_reservations(client, 4) == [Reservation(1, "9.5.6")]


def test_read_product_version_fails_without_valid_version() -> None:
    with pytest.raises(VersionError, match="No valid product_version on develop"):
        versions.read_product_version(FakeGitHub([], {"develop": "invalid"}), "develop")


def test_plan_allocation_selects_next_version() -> None:
    client = FakeGitHub(
        [pull_request(1, LABEL), pull_request(2, LABEL)], {"develop": "9.5.4", "sha-1": "9.5.5", "sha-2": "999.0.0"}
    )
    assert versions.plan_allocation(client, REPOSITORY, 2, "/allocate-fwo-version", "maintainer") == {
        "base_version": "9.5.4",
        "target_version": "9.5.6",
        "head_ref": "branch-2",
        "head_sha": "sha-2",
    }


def test_plan_allocation_reallocates_stale_version() -> None:
    client = FakeGitHub([pull_request(2, LABEL)], {"develop": "9.6.0", "sha-2": "9.5.7"})
    assert (
        versions.plan_allocation(client, REPOSITORY, 2, "/allocate-fwo-version", "maintainer")["target_version"]
        == "9.6.1"
    )


@pytest.mark.parametrize(
    ("client", "body", "message"),
    [
        (FakeGitHub([pull_request(2, LABEL)], {}), "/allocate-fwo-version 1.0.0", "Unsupported allocation command"),
        (
            FakeGitHub([pull_request(2, LABEL)], {}, permission="read"),
            "/allocate-fwo-version",
            "Only repository maintainers",
        ),
        (FakeGitHub([pull_request(2, [])], {}), "/allocate-fwo-version", "Add the versioned-change label"),
        (FakeGitHub([pull_request(2, LABEL, "fork/repo")], {}), "/allocate-fwo-version", "cannot push to a fork"),
        (
            FakeGitHub([pull_request(2, LABEL)], {"develop": "9.5.4", "sha-2": "9.5.5"}),
            "/allocate-fwo-version",
            "PR already reserves 9.5.5",
        ),
    ],
)
def test_plan_allocation_rejects_invalid_requests(client: FakeGitHub, body: str, message: str) -> None:
    with pytest.raises(VersionError, match=message):
        versions.plan_allocation(client, REPOSITORY, 2, body, "someone")


def test_plan_allocation_rejects_closed_pull_request() -> None:
    closed = pull_request(2, LABEL) | {"state": "closed"}
    with pytest.raises(VersionError, match="only supported for open pull requests"):
        versions.plan_allocation(FakeGitHub([closed], {}), REPOSITORY, 2, "/allocate-fwo-version", "maintainer")


def test_describe_pull_request_uses_head_for_pull_request_events() -> None:
    client = FakeGitHub([pull_request(2, LABEL)], {})
    assert versions.describe_pull_request(client, 2, "pull_request", "refs/pull/2/merge", "merge-sha") == {
        "number": "2",
        "head_sha": "sha-2",
        "versioned": "true",
    }


def test_describe_pull_request_validates_dispatched_ref() -> None:
    client = FakeGitHub([pull_request(2, [])], {})
    described = versions.describe_pull_request(client, 2, "workflow_dispatch", "refs/heads/branch-2", "new-sha")
    assert described == {"number": "2", "head_sha": "new-sha", "versioned": "false"}
    with pytest.raises(VersionError, match="is not the head of open PR #2"):
        versions.describe_pull_request(client, 2, "workflow_dispatch", "refs/heads/other", "new-sha")


def test_check_unlabelled_fails_on_new_upgrade_script() -> None:
    client = FakeGitHub(
        [], {}, changed_files=[{"filename": "roles/database/files/upgrade/9.5.5.sql", "status": "added"}]
    )
    with pytest.raises(VersionError, match="Add the versioned-change label"):
        versions.check_unlabelled(client, 2)
    versions.check_unlabelled(FakeGitHub([], {}), 2)


def test_check_order_enforces_lowest_reservation_first() -> None:
    client = FakeGitHub([pull_request(1, LABEL), pull_request(2, LABEL)], {"sha-1": "9.5.5", "sha-2": "9.5.6"})
    with pytest.raises(VersionError, match=r"must merge before 9\.5\.6"):
        versions.check_order(client, 2, "sha-2", "9.5.4")
    versions.check_order(client, 1, "sha-1", "9.5.4")


def test_requeue_dispatches_same_repository_prs_and_tolerates_failures(capsys: pytest.CaptureFixture[str]) -> None:
    client = FakeGitHub(
        [pull_request(1, LABEL), pull_request(2, LABEL, "fork/repo"), pull_request(3, []), pull_request(4, LABEL)],
        {},
    )
    client.failing_refs.add("branch-4")
    versions.requeue(client, REPOSITORY)
    assert client.posts == [
        (
            "/actions/workflows/validate-fwo-pr-version.yml/dispatches",
            {"ref": "branch-1", "inputs": {"pull_request_number": "1"}},
        )
    ]
    assert "::warning::Could not re-validate PR #4" in capsys.readouterr().out


def test_write_outputs_appends_to_github_output(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    output_file = tmp_path / "output"
    monkeypatch.setenv("GITHUB_OUTPUT", str(output_file))
    versions.write_outputs({"value": "9.5.4"})
    assert output_file.read_text(encoding="utf-8") == "value=9.5.4\n"


def test_write_outputs_prints_outside_github_actions(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    monkeypatch.delenv("GITHUB_OUTPUT", raising=False)
    versions.write_outputs({"value": "9.5.4"})
    assert capsys.readouterr().out == "value=9.5.4\n"


def test_run_command_dispatches_subcommands(monkeypatch: pytest.MonkeyPatch) -> None:
    client = FakeGitHub([pull_request(2, LABEL)], {"develop": "9.5.4", "sha-2": "999.0.0"})
    parser = versions.build_parser()
    assert versions.run_command(parser.parse_args(["base-version"]), client, REPOSITORY) == {"value": "9.5.4"}
    monkeypatch.setenv("COMMENT_BODY", "/allocate-fwo-version")
    monkeypatch.setenv("COMMENT_AUTHOR", "maintainer")
    planned = versions.run_command(parser.parse_args(["plan-allocation", "--pull-request", "2"]), client, REPOSITORY)
    assert planned["target_version"] == "9.5.5"
    dispatch = parser.parse_args(["dispatch-validation", "--pull-request", "2", "--ref", "branch-2"])
    assert versions.run_command(dispatch, client, REPOSITORY) == {}
    assert client.posts[0][1]["ref"] == "branch-2"


def test_main_reports_rule_violations(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]) -> None:
    monkeypatch.setenv("GITHUB_REPOSITORY", REPOSITORY)
    monkeypatch.setenv("GITHUB_TOKEN", "token")

    def get_invalid_content(_self: versions.GitHubApi, _path: str) -> JsonObject:
        return {"content": base64.b64encode(b"x: 1").decode()}

    monkeypatch.setattr(versions.GitHubApi, "get", get_invalid_content)
    assert versions.main(["base-version"]) == 1
    assert "::error::No valid product_version on develop." in capsys.readouterr().out


def test_github_api_follows_pagination_links(monkeypatch: pytest.MonkeyPatch) -> None:
    pages: dict[str, tuple[list[JsonObject], str]] = {
        "https://api.github.com/repos/o/r/pulls?state=open&per_page=100": (
            [{"number": 1}],
            '<https://next>; rel="next"',
        ),
        "https://next": ([{"number": 2}], ""),
    }
    api = versions.GitHubApi("token", "o/r", "https://api.github.com/")

    def serve_page(_method: str, url: str, _body: JsonObject | None = None) -> tuple[list[JsonObject], str]:
        return pages[url]

    monkeypatch.setattr(api, "_request", serve_page)
    assert api.paginate("/pulls?state=open") == [{"number": 1}, {"number": 2}]

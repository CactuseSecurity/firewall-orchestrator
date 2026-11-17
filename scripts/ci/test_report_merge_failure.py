"""Unit tests for the merge failure report of the FWO Test Install workflow."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

import pytest

from scripts.ci.report_merge_failure import (
    FailedJob,
    RunReference,
    build_comment,
    failed_jobs,
    find_merged_pull_request,
    main,
    report,
)

COMMIT_SHA = "3f5d1c70ea9d9c2c172cf6100ffbb87ff46d6260"
OTHER_SHA = "9fcd08bc8aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
REPOSITORY = "CactuseSecurity/firewall-orchestrator"
WORKFLOW_PATH = Path(__file__).parents[2] / ".github" / "workflows" / "test-install.yml"
RUN = RunReference(
    repository=REPOSITORY,
    commit_sha=COMMIT_SHA,
    run_id="43068352936",
    run_attempt="2",
    server_url="https://github.com",
)
PULLS_OF_COMMIT = [
    {"number": 5689, "merge_commit_sha": OTHER_SHA},
    {"number": 5666, "merge_commit_sha": COMMIT_SHA},
]
JOBS_OF_RUN = {
    "jobs": [
        {"name": "Test install on ubuntu-latest", "conclusion": "success", "html_url": "u1"},
        {"name": "Test install on ubuntu-22.04 (ansible-core 2.18)", "conclusion": "failure", "html_url": "u2"},
        {"name": "Python unit tests on Python 3.10", "conclusion": "timed_out", "html_url": "u3"},
        {"name": "Python unit tests on Python 3.12", "conclusion": "cancelled", "html_url": "u4"},
        {"name": "Report failed checks to the merged pull request", "conclusion": None, "html_url": "u5"},
    ]
}


@dataclass
class FakeGitHub:
    """Answers the report's GET requests from fixed data and records its POST requests."""

    responses: dict[str, object]
    posts: list[tuple[str, dict[str, str]]] = field(default_factory=list[tuple[str, dict[str, str]]])

    def get(self, path: str) -> object:
        return self.responses[path]

    def post(self, path: str, payload: dict[str, str]) -> None:
        self.posts.append((path, payload))


def fake_github(pulls: object = PULLS_OF_COMMIT) -> FakeGitHub:
    """Build a fake GitHub API holding the pull requests and jobs of RUN."""
    return FakeGitHub(
        responses={
            f"repos/{REPOSITORY}/commits/{COMMIT_SHA}/pulls": pulls,
            f"repos/{REPOSITORY}/actions/runs/43068352936/attempts/2/jobs?per_page=100": JOBS_OF_RUN,
        }
    )


class TestFindMergedPullRequest:
    def test_picks_the_pull_request_whose_merge_commit_it_is(self) -> None:
        assert find_merged_pull_request(PULLS_OF_COMMIT, COMMIT_SHA) == 5666  # noqa: PLR2004

    def test_open_pull_requests_containing_the_commit_do_not_count(self) -> None:
        assert find_merged_pull_request([{"number": 5689, "merge_commit_sha": OTHER_SHA}], COMMIT_SHA) is None

    @pytest.mark.parametrize("response", [None, {}, "x", [1, "x"], [{"merge_commit_sha": COMMIT_SHA}]])
    def test_unexpected_responses_yield_no_pull_request(self, response: object) -> None:
        assert find_merged_pull_request(response, COMMIT_SHA) is None


class TestFailedJobs:
    def test_lists_failed_and_timed_out_jobs_only(self) -> None:
        assert failed_jobs(JOBS_OF_RUN) == [
            FailedJob(name="Test install on ubuntu-22.04 (ansible-core 2.18)", url="u2"),
            FailedJob(name="Python unit tests on Python 3.10", url="u3"),
        ]

    @pytest.mark.parametrize("response", [None, [], {"jobs": None}, {"jobs": [1]}])
    def test_unexpected_responses_yield_no_jobs(self, response: object) -> None:
        assert failed_jobs(response) == []


class TestComment:
    def test_comment_names_commit_jobs_run_and_documentation(self) -> None:
        comment = build_comment(RUN, failed_jobs(JOBS_OF_RUN))
        assert "`3f5d1c70e`" in comment
        assert "- [Test install on ubuntu-22.04 (ansible-core 2.18)](u2)" in comment
        assert f"https://github.com/{REPOSITORY}/actions/runs/43068352936/attempts/2" in comment
        assert "test-install-workflow.md#checks-after-a-merge" in comment

    def test_comment_without_failed_jobs_points_to_the_run(self) -> None:
        assert "no failed job was reported" in build_comment(RUN, [])

    def test_documentation_section_exists(self) -> None:
        documentation = (Path(__file__).parents[2] / RUN.documentation_url().split("/blob/develop/")[1]).as_posix()
        document_path, section = documentation.split("#")
        assert "## Checks after a merge" in Path(document_path).read_text(encoding="utf-8")
        assert section == "checks-after-a-merge"


class TestReport:
    def test_comments_on_the_merged_pull_request(self) -> None:
        github = fake_github()
        assert report(github, RUN) == 5666  # noqa: PLR2004
        assert len(github.posts) == 1
        path, payload = github.posts[0]
        assert path == f"repos/{REPOSITORY}/issues/5666/comments"
        assert "Python unit tests on Python 3.10" in payload["body"]

    def test_commit_without_pull_request_posts_nothing(self) -> None:
        github = fake_github(pulls=[])
        assert report(github, RUN) is None
        assert github.posts == []

    def test_main_logs_a_warning_without_pull_request(self, capsys: pytest.CaptureFixture[str]) -> None:
        arguments = ["--repository", REPOSITORY, "--commit-sha", COMMIT_SHA, "--run-id", "43068352936"]
        assert main([*arguments, "--run-attempt", "2"], client=fake_github(pulls=[])) == 0
        assert "::warning title=Checks after the merge failed::" in capsys.readouterr().out

    def test_main_reports_the_pull_request(self, capsys: pytest.CaptureFixture[str]) -> None:
        arguments = ["--repository", REPOSITORY, "--commit-sha", COMMIT_SHA, "--run-id", "43068352936"]
        assert main([*arguments, "--run-attempt", "2"], client=fake_github()) == 0
        assert "pull request #5666" in capsys.readouterr().out


class TestWorkflow:
    def test_report_job_runs_only_for_failed_develop_pushes(self) -> None:
        workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
        job = workflow.split("  report-merge-failure:", 1)[1]
        assert "needs: [plan, test-install, python-unit-tests]" in job
        assert "github.event_name == 'push' && github.ref == 'refs/heads/develop'" in job
        assert "contains(needs.*.result, 'failure')" in job
        assert "pull-requests: write" in job

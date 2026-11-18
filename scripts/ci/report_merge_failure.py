#!/usr/bin/env python3
"""
Tell a merged pull request that the checks after its merge into develop failed.

The installs on Ubuntu 22.04 and 26.04 and the Python unit tests on every supported Python
version mostly run only after a merge, so their failures would otherwise only show in the
commit history of develop, which nobody watches. This comments on the pull request whose merge
produced the commit: its author and reviewers are subscribed to it and get notified.

The decisions live in side-effect free functions so that they can be unit tested. ``main``
wires them to the GitHub API through the gh command line client.

See documentation/developer-docs/github/test-install-workflow.md.
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from dataclasses import dataclass
from typing import Protocol, cast

FAILED_CONCLUSIONS = frozenset({"failure", "timed_out"})
WORKFLOW_DOCUMENTATION = "documentation/developer-docs/github/test-install-workflow.md"
DOCUMENTATION_SECTION = "checks-after-a-merge"
SHORT_SHA_LENGTH = 9


@dataclass(frozen=True)
class FailedJob:
    """A job of the run that failed, with the link to its log."""

    name: str
    url: str


@dataclass(frozen=True)
class RunReference:
    """The run whose failure is reported."""

    repository: str
    commit_sha: str
    run_id: str
    run_attempt: str
    server_url: str

    def url(self) -> str:
        """Return the web link of the run attempt."""
        return f"{self.server_url}/{self.repository}/actions/runs/{self.run_id}/attempts/{self.run_attempt}"

    def documentation_url(self) -> str:
        """Return the web link of the documentation section about these checks."""
        return f"{self.server_url}/{self.repository}/blob/develop/{WORKFLOW_DOCUMENTATION}#{DOCUMENTATION_SECTION}"


class GitHubClient(Protocol):
    """The GitHub REST calls the report needs."""

    def get(self, path: str) -> object:
        """Return the decoded JSON response of a GET request."""
        ...

    def post(self, path: str, payload: dict[str, str]) -> None:
        """Send a POST request with a JSON body."""
        ...


class GhCliClient:
    """GitHubClient backed by the gh command line client, which reads GH_TOKEN from the environment."""

    def get(self, path: str) -> object:
        """Return the decoded JSON response of a GET request."""
        command = ["gh", "api", path]
        completed = subprocess.run(command, check=True, capture_output=True, text=True)  # noqa: S603
        return json.loads(completed.stdout)

    def post(self, path: str, payload: dict[str, str]) -> None:
        """Send a POST request with a JSON body."""
        command = ["gh", "api", "--method", "POST", path, "--input", "-"]
        subprocess.run(command, check=True, capture_output=True, input=json.dumps(payload), text=True)  # noqa: S603


def as_records(value: object) -> list[dict[str, object]]:
    """Return the dictionaries of a decoded JSON list, ignoring anything else."""
    if not isinstance(value, list):
        return []
    items = cast("list[object]", value)
    return [cast("dict[str, object]", item) for item in items if isinstance(item, dict)]


def find_merged_pull_request(pull_requests: object, commit_sha: str) -> int | None:
    """
    Return the number of the pull request whose merge produced the commit.

    GitHub lists every pull request that contains the commit, including open ones that merged
    develop since. Only the one whose merge commit is this commit is responsible for it.
    """
    for pull_request in as_records(pull_requests):
        number = pull_request.get("number")
        if pull_request.get("merge_commit_sha") == commit_sha and isinstance(number, int):
            return number
    return None


def failed_jobs(jobs_response: object) -> list[FailedJob]:
    """Return the failed jobs of a run attempt, in the order GitHub lists them."""
    if not isinstance(jobs_response, dict):
        return []
    jobs = as_records(cast("dict[str, object]", jobs_response).get("jobs"))
    return [
        FailedJob(name=str(job.get("name", "")), url=str(job.get("html_url", "")))
        for job in jobs
        if job.get("conclusion") in FAILED_CONCLUSIONS
    ]


def build_comment(run: RunReference, jobs: list[FailedJob]) -> str:
    """Build the comment for the merged pull request."""
    job_lines = [f"- [{job.name}]({job.url})" for job in jobs] or ["- no failed job was reported, see the run"]
    introduction = (
        f"The merge commit `{run.commit_sha[:SHORT_SHA_LENGTH]}` of this pull request ran the checks that "
        "mostly run only after a merge (installs on every supported Ubuntu version, Python unit tests on "
        "every supported Python version), and these failed:"
    )
    request = (
        "Please check whether this pull request caused it. Until a later merge fixes it, develop is red. "
        f"See [the test install workflow documentation]({run.documentation_url()})."
    )
    lines = [
        "### Checks after the merge into develop failed",
        "",
        introduction,
        "",
        *job_lines,
        "",
        f"Run: {run.url()}",
        "",
        request,
    ]
    return "\n".join(lines)


def report(client: GitHubClient, run: RunReference) -> int | None:
    """Comment on the pull request that produced the commit; return its number, if there is one."""
    pull_requests = client.get(f"repos/{run.repository}/commits/{run.commit_sha}/pulls")
    number = find_merged_pull_request(pull_requests, run.commit_sha)
    if number is None:
        return None
    jobs_path = f"repos/{run.repository}/actions/runs/{run.run_id}/attempts/{run.run_attempt}/jobs?per_page=100"
    jobs = failed_jobs(client.get(jobs_path))
    client.post(f"repos/{run.repository}/issues/{number}/comments", {"body": build_comment(run, jobs)})
    return number


def build_parser() -> argparse.ArgumentParser:
    """Build the command line parser."""
    parser = argparse.ArgumentParser(description="Comment on the merged pull request of a failed develop run")
    parser.add_argument("--repository", required=True, help="owner/name")
    parser.add_argument("--commit-sha", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--run-attempt", required=True)
    parser.add_argument("--server-url", default="https://github.com")
    return parser


def main(argv: list[str] | None = None, client: GitHubClient | None = None) -> int:
    """Report the failed run to its pull request; a commit without one is only logged."""
    arguments = build_parser().parse_args(argv)
    run = RunReference(
        repository=arguments.repository,
        commit_sha=arguments.commit_sha,
        run_id=arguments.run_id,
        run_attempt=arguments.run_attempt,
        server_url=arguments.server_url,
    )
    number = report(client or GhCliClient(), run)
    if number is None:
        sys.stdout.write(
            f"::warning title=Checks after the merge failed::No pull request was merged as {run.commit_sha}, "
            f"so there is nothing to comment on. Run: {run.url()}\n"
        )
    else:
        sys.stdout.write(f"Reported the failed run to pull request #{number}.\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())

"""
Shared version-reservation logic for the FWO PR versioning workflows.

The workflows call the subcommands of this script; it depends only on the
Python standard library, so it runs on a plain GitHub runner. Tested by
scripts/tests/test_fwo_version_reservations.py.
"""

import argparse
import base64
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from typing import Any, Protocol

JsonObject = dict[str, Any]

VERSIONED_LABEL = "versioned-change"
PLACEHOLDER_VERSION = "999.0.0"
PRODUCT_VERSION_FILE = "inventory/group_vars/all.yml"
UPGRADE_DIRECTORY = "roles/database/files/upgrade/"
VALIDATOR_WORKFLOW = "validate-fwo-pr-version.yml"
BASE_BRANCH = "develop"
PAGE_SIZE = 100
REQUEST_TIMEOUT_SECONDS = 30
PRODUCT_VERSION_PATTERN = re.compile(r'^product_version:\s*"?([0-9]+\.[0-9]+\.[0-9]+)"?\s*$', re.MULTILINE)
PRODUCT_VERSION_CHANGE_PATTERN = re.compile(r"^[+-]product_version:", re.MULTILINE)
ALLOCATION_COMMAND_PATTERN = re.compile(r"^/allocate-fwo-version(?: (patch|minor|major))?$")
NEXT_LINK_PATTERN = re.compile(r'<([^>]+)>;\s*rel="next"')
WRITE_PERMISSIONS = ("admin", "write")
VERSION_ADDING_STATUSES = ("added", "renamed", "copied")


class VersionError(Exception):
    """A versioning rule is violated; the message is reported to the user."""


@dataclass(frozen=True)
class Reservation:
    number: int
    version: str


class GitHubClient(Protocol):
    def get(self, path: str) -> Any: ...

    def paginate(self, path: str) -> list[JsonObject]: ...

    def post(self, path: str, body: JsonObject) -> None: ...


class GitHubApi:
    """Minimal GitHub REST client for a single repository."""

    def __init__(self, token: str, repository: str, api_url: str) -> None:
        self._token = token
        self.repository = repository
        self._api_url = api_url.rstrip("/")

    def _request(self, method: str, url: str, body: JsonObject | None = None) -> tuple[Any, str]:
        data = json.dumps(body).encode() if body is not None else None
        request = urllib.request.Request(url, data=data, method=method)  # noqa: S310 - URL is built from the API base URL
        request.add_header("Authorization", f"Bearer {self._token}")
        request.add_header("Accept", "application/vnd.github+json")
        request.add_header("X-GitHub-Api-Version", "2022-11-28")
        with urllib.request.urlopen(request, timeout=REQUEST_TIMEOUT_SECONDS) as response:  # noqa: S310
            content = response.read()
            return (json.loads(content) if content else None), response.headers.get("Link", "")

    def _url(self, path: str) -> str:
        return f"{self._api_url}/repos/{self.repository}{path}"

    def get(self, path: str) -> Any:
        return self._request("GET", self._url(path))[0]

    def paginate(self, path: str) -> list[JsonObject]:
        separator = "&" if "?" in path else "?"
        url: str | None = f"{self._url(path)}{separator}per_page={PAGE_SIZE}"
        items: list[JsonObject] = []
        while url:
            page, link = self._request("GET", url)
            items.extend(page)
            match = NEXT_LINK_PATTERN.search(link)
            url = match.group(1) if match else None
        return items

    def post(self, path: str, body: JsonObject) -> None:
        self._request("POST", self._url(path), body)


def parse_version(version: str) -> list[int]:
    """Splits a major.minor.patch version into its numeric components."""
    return [int(part) for part in version.split(".")]


def compare_versions(left: str, right: str) -> int:
    """Returns -1, 0, or 1 when left is lower than, equal to, or greater than right."""
    left_parts = parse_version(left)
    right_parts = parse_version(right)
    return (left_parts > right_parts) - (left_parts < right_parts)


def release_line(version: str) -> str:
    """Returns the major.minor release line of a version."""
    major, minor, _ = parse_version(version)
    return f"{major}.{minor}"


def extract_product_version(content: str) -> str | None:
    """Extracts product_version from the content of inventory/group_vars/all.yml."""
    match = PRODUCT_VERSION_PATTERN.search(content)
    return match.group(1) if match else None


def parse_allocation_command(body: str | None) -> str | None:
    """Returns the requested bump for an allocation comment, or None for any other comment."""
    match = ALLOCATION_COMMAND_PATTERN.match((body or "").strip())
    if not match:
        return None
    return match.group(1) or "patch"


def has_write_permission(permission_response: JsonObject) -> bool:
    """Returns whether a collaborator permission response grants write access."""
    return permission_response.get("permission") in WRITE_PERMISSIONS


def is_versioned(pull_request: JsonObject) -> bool:
    """Returns whether a pull request carries the versioned-change label."""
    return any(label.get("name") == VERSIONED_LABEL for label in pull_request.get("labels", []))


def is_same_repository(pull_request: JsonObject, repository: str) -> bool:
    """Returns whether the pull request head branch lives in the given repository."""
    head_repository: JsonObject = pull_request["head"].get("repo") or {}
    return head_repository.get("full_name") == repository


def is_stale_version(version: str, base_version: str) -> bool:
    """Returns whether an allocated version is no longer above develop, e.g. after a release-line change."""
    return version != PLACEHOLDER_VERSION and compare_versions(version, base_version) <= 0


def release_line_start(base_version: str, bump: str) -> str | None:
    """Returns the first version of the line a minor or major bump starts, e.g. 9.6.0 for 9.5.4."""
    major, minor, _ = parse_version(base_version)
    if bump == "major":
        return f"{major + 1}.0.0"
    if bump == "minor":
        return f"{major}.{minor + 1}.0"
    return None


def select_target_version(base_version: str, reserved_versions: Sequence[str], bump: str) -> str:
    """
    Selects the next unreserved version for a bump. Only reservations on the
    target release line are considered; other lines cannot collide with it.
    """
    line_start = release_line_start(base_version, bump)
    target_line = release_line(line_start or base_version)
    candidates = [
        version
        for version in reserved_versions
        if release_line(version) == target_line and compare_versions(version, base_version) > 0
    ]
    if line_start is None:
        candidates.append(base_version)
    if not candidates:
        return line_start or base_version
    major, minor, patch = parse_version(max(candidates, key=parse_version))
    return f"{major}.{minor}.{patch + 1}"


def find_reservation_conflict(
    current_version: str, base_version: str, reservations: Sequence[Reservation]
) -> str | None:
    """
    Returns why the current version may not merge yet, or None. Another open PR
    may neither reserve the same version nor a lower, still unmerged one.
    """
    for reservation in reservations:
        if reservation.version == current_version:
            return f"Version {current_version} is already reserved by PR #{reservation.number}."
        if (
            compare_versions(reservation.version, base_version) > 0
            and compare_versions(reservation.version, current_version) < 0
        ):
            return f"PR #{reservation.number} reserves {reservation.version} and must merge before {current_version}."
    return None


def find_unlabelled_version_changes(changed_files: Sequence[JsonObject]) -> list[str]:
    """Returns the versioning-relevant changes of a PR: added upgrade scripts and product_version edits."""
    findings: list[str] = []
    for changed_file in changed_files:
        filename: str = changed_file["filename"]
        status: str = changed_file["status"]
        if filename.startswith(UPGRADE_DIRECTORY) and status in VERSION_ADDING_STATUSES:
            findings.append(f"{filename} ({status})")
        elif filename == PRODUCT_VERSION_FILE and PRODUCT_VERSION_CHANGE_PATTERN.search(
            changed_file.get("patch") or ""
        ):
            findings.append(f"{PRODUCT_VERSION_FILE} (product_version changed)")
    return findings


def read_product_version(client: GitHubClient, ref: str) -> str:
    """Reads product_version from inventory/group_vars/all.yml at a git ref."""
    data = client.get(f"/contents/{PRODUCT_VERSION_FILE}?ref={urllib.parse.quote(ref, safe='')}")
    version = extract_product_version(base64.b64decode(data["content"]).decode())
    if version is None:
        raise VersionError(f"No valid product_version on {ref}.")
    return version


def list_versioned_pull_requests(client: GitHubClient) -> list[JsonObject]:
    """Lists all open, labelled pull requests targeting develop."""
    return [
        pull_request
        for pull_request in client.paginate(f"/pulls?state=open&base={BASE_BRANCH}")
        if is_versioned(pull_request)
    ]


def list_reservations(client: GitHubClient, excluded_number: int) -> list[Reservation]:
    """Returns the allocated versions of all open, labelled PRs except one, skipping placeholders."""
    reservations: list[Reservation] = []
    for pull_request in list_versioned_pull_requests(client):
        if pull_request["number"] == excluded_number:
            continue
        version = read_product_version(client, pull_request["head"]["sha"])
        if version != PLACEHOLDER_VERSION:
            reservations.append(Reservation(pull_request["number"], version))
    return reservations


def dispatch_validation(client: GitHubClient, pull_request_number: int, ref: str) -> None:
    """Starts the validator on a PR branch, attaching its check run to the branch head."""
    client.post(
        f"/actions/workflows/{VALIDATOR_WORKFLOW}/dispatches",
        {"ref": ref, "inputs": {"pull_request_number": str(pull_request_number)}},
    )


def plan_allocation(
    client: GitHubClient, repository: str, pull_request_number: int, comment_body: str, comment_author: str
) -> dict[str, str]:
    """Authorizes an allocation command and returns the versions and branch to allocate on."""
    bump = parse_allocation_command(comment_body)
    if bump is None:
        raise VersionError("Unsupported allocation command.")
    if not has_write_permission(client.get(f"/collaborators/{urllib.parse.quote(comment_author)}/permission")):
        raise VersionError("Only repository maintainers may allocate FWO versions.")
    pull_request: JsonObject = client.get(f"/pulls/{pull_request_number}")
    if pull_request["state"] != "open" or pull_request["base"]["ref"] != BASE_BRANCH:
        raise VersionError("Version allocation is only supported for open pull requests targeting develop.")
    if not is_versioned(pull_request):
        raise VersionError("Add the versioned-change label before allocating a FWO version.")
    if not is_same_repository(pull_request, repository):
        raise VersionError(
            "Version allocation cannot push to a fork. A maintainer must create a same-repository branch."
        )

    base_version = read_product_version(client, BASE_BRANCH)
    current_version = read_product_version(client, pull_request["head"]["sha"])
    if current_version != PLACEHOLDER_VERSION and not is_stale_version(current_version, base_version):
        raise VersionError(
            f"PR already reserves {current_version}. Only the {PLACEHOLDER_VERSION} placeholder or a stale version can be allocated."
        )
    reserved = [reservation.version for reservation in list_reservations(client, pull_request_number)]
    target_version = select_target_version(base_version, reserved, bump)
    emit(f"develop is {base_version}; allocating {target_version} ({bump}) to PR #{pull_request_number}.")
    return {
        "base_version": base_version,
        "target_version": target_version,
        "head_ref": pull_request["head"]["ref"],
        "head_sha": pull_request["head"]["sha"],
    }


def describe_pull_request(
    client: GitHubClient, pull_request_number: int, event_name: str, ref: str, sha: str
) -> dict[str, str]:
    """Returns the PR number, the commit to validate, and whether the PR is labelled."""
    pull_request: JsonObject = client.get(f"/pulls/{pull_request_number}")
    if event_name == "workflow_dispatch":
        if (
            pull_request["state"] != "open"
            or pull_request["base"]["ref"] != BASE_BRANCH
            or f"refs/heads/{pull_request['head']['ref']}" != ref
        ):
            raise VersionError(
                f"Dispatched ref {ref} is not the head of open PR #{pull_request_number} targeting develop."
            )
        head_sha = sha  # a dispatched run validates the commit its check run is attached to
    else:
        head_sha = pull_request["head"]["sha"]
    return {
        "number": str(pull_request_number),
        "head_sha": head_sha,
        "versioned": str(is_versioned(pull_request)).lower(),
    }


def check_unlabelled(client: GitHubClient, pull_request_number: int) -> None:
    """Fails when an unlabelled PR changes versioned files, which would bypass the reservation."""
    findings = find_unlabelled_version_changes(client.paginate(f"/pulls/{pull_request_number}/files"))
    if findings:
        raise VersionError(f"Add the versioned-change label; this PR changes versioned files: {', '.join(findings)}.")


def check_order(client: GitHubClient, pull_request_number: int, head_sha: str, base_version: str) -> None:
    """Fails when another open PR reserves the same or a lower unmerged version."""
    current_version = read_product_version(client, head_sha)
    conflict = find_reservation_conflict(current_version, base_version, list_reservations(client, pull_request_number))
    if conflict:
        raise VersionError(conflict)


def requeue(client: GitHubClient, repository: str) -> None:
    """Re-runs the validator on every labelled same-repository PR."""
    for pull_request in list_versioned_pull_requests(client):
        if not is_same_repository(pull_request, repository):
            continue
        try:
            dispatch_validation(client, pull_request["number"], pull_request["head"]["ref"])
            emit(f"Re-validating PR #{pull_request['number']}.")
        except urllib.error.HTTPError as error:
            # The branch may predate the dispatchable workflow; a rebase fixes that.
            emit(f"::warning::Could not re-validate PR #{pull_request['number']}: {error}")


def emit(message: str) -> None:
    """Writes a log line or workflow command to the GitHub Actions log."""
    sys.stdout.write(f"{message}\n")


def write_outputs(outputs: dict[str, str]) -> None:
    """Writes step outputs to GITHUB_OUTPUT, or to stdout outside of GitHub Actions."""
    lines = "".join(f"{key}={value}\n" for key, value in outputs.items())
    output_file = os.environ.get("GITHUB_OUTPUT")
    if output_file:
        with open(output_file, "a", encoding="utf-8") as output:
            output.write(lines)
    else:
        sys.stdout.write(lines)


def build_parser() -> argparse.ArgumentParser:
    """Builds the command-line interface used by the workflows."""
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    plan = commands.add_parser("plan-allocation")
    plan.add_argument("--pull-request", type=int, required=True)
    describe = commands.add_parser("describe-pull-request")
    describe.add_argument("--pull-request", type=int, required=True)
    describe.add_argument("--event-name", required=True)
    describe.add_argument("--ref", required=True)
    describe.add_argument("--sha", required=True)
    unlabelled = commands.add_parser("check-unlabelled")
    unlabelled.add_argument("--pull-request", type=int, required=True)
    commands.add_parser("base-version")
    order = commands.add_parser("check-order")
    order.add_argument("--pull-request", type=int, required=True)
    order.add_argument("--head-sha", required=True)
    order.add_argument("--base-version", required=True)
    dispatch = commands.add_parser("dispatch-validation")
    dispatch.add_argument("--pull-request", type=int, required=True)
    dispatch.add_argument("--ref", required=True)
    commands.add_parser("requeue")
    return parser


def run_command(arguments: argparse.Namespace, client: GitHubClient, repository: str) -> dict[str, str]:
    """Runs one subcommand and returns its step outputs."""
    handlers: dict[str, Callable[[], dict[str, str] | None]] = {
        "plan-allocation": lambda: plan_allocation(
            client,
            repository,
            arguments.pull_request,
            os.environ.get("COMMENT_BODY", ""),
            os.environ.get("COMMENT_AUTHOR", ""),
        ),
        "describe-pull-request": lambda: describe_pull_request(
            client, arguments.pull_request, arguments.event_name, arguments.ref, arguments.sha
        ),
        "check-unlabelled": lambda: check_unlabelled(client, arguments.pull_request),
        "base-version": lambda: {"value": read_product_version(client, BASE_BRANCH)},
        "check-order": lambda: check_order(client, arguments.pull_request, arguments.head_sha, arguments.base_version),
        "dispatch-validation": lambda: dispatch_validation(client, arguments.pull_request, arguments.ref),
        "requeue": lambda: requeue(client, repository),
    }
    return handlers[arguments.command]() or {}


def main(argv: Sequence[str] | None = None) -> int:
    """Entry point; reports rule violations as GitHub Actions errors."""
    arguments = build_parser().parse_args(argv)
    repository = os.environ["GITHUB_REPOSITORY"]
    client = GitHubApi(
        os.environ["GITHUB_TOKEN"], repository, os.environ.get("GITHUB_API_URL", "https://api.github.com")
    )
    try:
        write_outputs(run_command(arguments, client, repository))
    except VersionError as error:
        emit(f"::error::{error}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())

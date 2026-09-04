#!/usr/bin/env python3
"""
Version gate logic for the Firewall Orchestrator versioning workflow.

A product version is *open* until a sealing tag for it exists. A pull request may only
merge onto an open version, and it may only open a new version once the previous one has
been sealed. Sealing tags are ``vX.Y.Z`` and ``vX.Y.Z-dev``; every other suffix such as
``-rc1`` or ``-beta`` marks a snapshot and does not seal a version.

The rules live in side-effect free functions so that they can be unit tested. ``main``
only wires command line arguments and files to those functions and prints a JSON verdict.

See documentation/developer-docs/versioning.md for the policy this file enforces.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

# Same product_version extraction as the Sonar workflows use, see .github/workflows/sonarcloud.yml.
PRODUCT_VERSION_PATTERN = re.compile(r'^product_version:\s*"?([^"\s]+)"?\s*$', re.MULTILINE)
VERSION_PATTERN = re.compile(r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")
SEALING_TAG_PATTERN = re.compile(r"^v?((?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*))(?:-dev)?$")
VERSION_TAG_PATTERN = re.compile(r"^v?(\d+\.\d+\.\d+)(?:-[0-9A-Za-z.-]+)?$")
REVISION_HISTORY_HEADING_PATTERN = re.compile(r"^##[ \t]+(.+?)[ \t]*$", re.MULTILINE)
REVISION_HISTORY_VERSION_PATTERN = re.compile(
    r"^((?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*))(?:[ \t]+.*)?$",
)
# GitHub truncates commit status descriptions, so keep them short enough to stay readable.
MAX_DESCRIPTION_LENGTH = 140


@dataclass(frozen=True)
class Verdict:
    """Result of a gate evaluation: whether it passes and why."""

    ok: bool
    reason: str

    def to_dict(self) -> dict[str, object]:
        """Return the verdict as a JSON serializable dictionary."""
        description = self.reason
        if len(description) > MAX_DESCRIPTION_LENGTH:
            description = description[: MAX_DESCRIPTION_LENGTH - 1] + "…"
        return {"ok": self.ok, "reason": self.reason, "description": description}


def parse_product_version(yaml_text: str) -> str:
    """Extract ``product_version`` from the content of inventory/group_vars/all.yml."""
    match = PRODUCT_VERSION_PATTERN.search(yaml_text)
    if match is None:
        raise ValueError("could not find product_version in inventory/group_vars/all.yml")
    return match.group(1)


def parse_version(text: str) -> tuple[int, int, int]:
    """Parse a ``major.minor.patch`` product version into a comparable tuple."""
    match = VERSION_PATTERN.match(text.strip())
    if match is None:
        raise ValueError(f"'{text}' is not a valid product version, expected major.minor.patch")
    return (int(match.group(1)), int(match.group(2)), int(match.group(3)))


def sealing_version(tag: str) -> str | None:
    """Return the version a tag seals, or None when the tag does not seal a version."""
    match = SEALING_TAG_PATTERN.match(tag.strip())
    return match.group(1) if match is not None else None


def tag_version(tag: str) -> str | None:
    """Return the numeric version of any version tag, including snapshots such as -rc1."""
    match = VERSION_TAG_PATTERN.match(tag.strip())
    return match.group(1) if match is not None else None


def sealed_versions(tags: list[str]) -> set[str]:
    """Collect the set of versions that are sealed by the given tags."""
    sealed: set[str] = set()
    for tag in tags:
        version = sealing_version(tag)
        if version is not None:
            sealed.add(version)
    return sealed


def last_revision_history_heading(markdown: str) -> str | None:
    """Return the text of the final level-two revision-history heading."""
    headings: list[str] = REVISION_HISTORY_HEADING_PATTERN.findall(markdown)
    return headings[-1] if headings else None


def revision_history_heading_version(heading: str) -> str | None:
    """Return the canonical version at the start of a revision-history heading."""
    match = REVISION_HISTORY_VERSION_PATTERN.match(heading)
    return match.group(1) if match is not None else None


def evaluate_gate(
    merged_version: str,
    base_version: str,
    sealed: set[str],
    revision_history: str,
) -> Verdict:
    """
    Decide whether a pull request may merge, given the version its merge result carries.

    merged_version is read from refs/pull/<n>/merge so that a pull request which does not
    touch all.yml automatically inherits the base version instead of being blocked.
    """
    try:
        merged = parse_version(merged_version)
        base = parse_version(base_version)
    except ValueError as error:
        return Verdict(ok=False, reason=str(error))

    if merged == base:
        if merged_version in sealed:
            return Verdict(
                ok=False,
                reason=(
                    f"version {merged_version} is already sealed by a release tag. "
                    f"Raise product_version in inventory/group_vars/all.yml to the next version."
                ),
            )
        return Verdict(ok=True, reason=f"version {merged_version} is still open")

    if merged < base:
        return Verdict(
            ok=False,
            reason=(
                f"product_version must not go backwards: {merged_version} is lower than "
                f"{base_version} on the base branch"
            ),
        )

    if base_version not in sealed:
        return Verdict(
            ok=False,
            reason=(
                f"version {base_version} has not been sealed yet. "
                f"Create tag v{base_version}-dev or v{base_version} before opening version {merged_version}."
            ),
        )

    if merged_version in sealed:
        return Verdict(
            ok=False,
            reason=f"version {merged_version} is already sealed by a release tag, choose a higher version",
        )

    last_heading = last_revision_history_heading(revision_history)
    last_documented_version = revision_history_heading_version(last_heading) if last_heading is not None else None
    if last_documented_version != merged_version:
        actual_heading = f"'## {last_heading}'" if last_heading is not None else "missing"
        return Verdict(
            ok=False,
            reason=(
                f"documentation/revision-history.md must end with a '## {merged_version}' heading "
                f"(last level-two heading is {actual_heading})"
            ),
        )

    return Verdict(ok=True, reason=f"version {base_version} is sealed, opening version {merged_version}")


def evaluate_open_version(version: str, sealed: set[str]) -> Verdict:
    """Decide whether a branch still sits on an open, unsealed version."""
    try:
        parse_version(version)
    except ValueError as error:
        return Verdict(ok=False, reason=str(error))

    if version in sealed:
        return Verdict(
            ok=False,
            reason=(
                f"version {version} is sealed by a release tag but is still set in "
                f"inventory/group_vars/all.yml. Raise product_version to the next version."
            ),
        )
    return Verdict(ok=True, reason=f"version {version} is still open")


def evaluate_tag(tag: str, tagged_version: str) -> Verdict:
    """Decide whether a version tag was created on a commit carrying the matching version."""
    version = tag_version(tag)
    if version is None:
        return Verdict(ok=True, reason=f"tag '{tag}' is not a version tag, nothing to validate")

    try:
        parsed_tag_version = parse_version(version)
        parsed_product_version = parse_version(tagged_version)
    except ValueError as error:
        return Verdict(ok=False, reason=str(error))

    if parsed_tag_version != parsed_product_version:
        return Verdict(
            ok=False,
            reason=(
                f"tag '{tag}' was created on a commit whose product_version is {tagged_version}. "
                f"A version tag must point at a commit carrying version {version}."
            ),
        )
    return Verdict(ok=True, reason=f"tag '{tag}' matches the product_version of its commit")


def read_version(literal: str | None, file_path: str | None) -> str:
    """Return a version given either as a literal or as the path to an all.yml file."""
    if literal is not None:
        return literal.strip()
    if file_path is None:
        raise ValueError("either the version or the path to an all.yml file is required")
    return parse_product_version(Path(file_path).read_text(encoding="utf-8"))


def read_tags(file_path: str | None) -> list[str]:
    """Read newline separated tags from a file, from stdin for '-', or from the local repository."""
    if file_path is None:
        completed = subprocess.run(
            ["git", "tag", "--list"],  # noqa: S607
            capture_output=True,
            text=True,
            check=True,
        )
        text = completed.stdout
    elif file_path == "-":
        text = sys.stdin.read()
    else:
        text = Path(file_path).read_text(encoding="utf-8")
    return [line.strip() for line in text.splitlines() if line.strip()]


def build_parser() -> argparse.ArgumentParser:
    """Build the command line parser with one subcommand per gate."""
    parser = argparse.ArgumentParser(description="Firewall Orchestrator version gate")
    subparsers = parser.add_subparsers(dest="command", required=True)
    tags_help = "newline separated tags, '-' for stdin, omitted for local git tags"

    gate = subparsers.add_parser("gate", help="evaluate a pull request merge result")
    gate.add_argument("--merged-version")
    gate.add_argument("--merged-file", help="all.yml as it looks on refs/pull/<n>/merge")
    gate.add_argument("--base-version")
    gate.add_argument("--base-file", help="all.yml as it looks on the base branch")
    gate.add_argument("--revision-history", help="revision-history.md as it looks on refs/pull/<n>/merge")
    gate.add_argument("--tags-file", help=tags_help)

    audit = subparsers.add_parser("check-open", help="check that a branch sits on an unsealed version")
    audit.add_argument("--version")
    audit.add_argument("--file", help="all.yml as it looks on the branch")
    audit.add_argument("--tags-file", help=tags_help)

    check_tag = subparsers.add_parser("check-tag", help="check that a version tag matches its commit")
    check_tag.add_argument("--tag", required=True)
    check_tag.add_argument("--version")
    check_tag.add_argument("--file", help="all.yml as it looks on the tagged commit")

    return parser


def run_command(arguments: argparse.Namespace) -> Verdict:
    """Dispatch a parsed command to the matching evaluation."""
    if arguments.command == "gate":
        revision_history = ""
        if arguments.revision_history is not None:
            revision_history = Path(arguments.revision_history).read_text(encoding="utf-8")
        return evaluate_gate(
            read_version(arguments.merged_version, arguments.merged_file),
            read_version(arguments.base_version, arguments.base_file),
            sealed_versions(read_tags(arguments.tags_file)),
            revision_history,
        )
    if arguments.command == "check-open":
        return evaluate_open_version(
            read_version(arguments.version, arguments.file),
            sealed_versions(read_tags(arguments.tags_file)),
        )
    return evaluate_tag(arguments.tag, read_version(arguments.version, arguments.file))


def main() -> int:
    """Evaluate the requested gate, print the verdict as JSON and return the exit code."""
    arguments = build_parser().parse_args()
    try:
        verdict = run_command(arguments)
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        verdict = Verdict(ok=False, reason=f"version gate could not be evaluated: {error}")

    sys.stdout.write(json.dumps(verdict.to_dict()) + "\n")
    return 0 if verdict.ok else 1


if __name__ == "__main__":
    sys.exit(main())

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
from collections import Counter
from dataclasses import dataclass, field
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
DIFF_HUNK_PATTERN = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@")
UPGRADE_FILE_PATTERN = re.compile(r"^((?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:\.(?:0|[1-9]\d*))?)\.sql$")
# What upgrade-database.yml reads as a version: the name without its extension, digits and dots.
VERSION_LIKE_UPGRADE_FILE_PATTERN = re.compile(r"^[0-9][0-9.]*\.sql$")
# GitHub truncates commit status descriptions, so keep them short enough to stay readable.
MAX_DESCRIPTION_LENGTH = 140
# major, minor and patch, the patch level being optional in a few old upgrade file names.
VERSION_PART_COUNT = 3


@dataclass(frozen=True)
class GateFiles:
    """
    The file inputs one gate evaluation reads.

    They travel together and all describe the same pull request: the revision history as the
    merge result carries it, its diff against the base branch, the upgrade file names in the
    merge result, and the upgrade file names the pull request adds or modifies. Each stays
    empty when a caller does not supply it, which keeps the rules that need it out of the way
    of callers that only test versions.
    """

    merged_revision_history: str = ""
    revision_history_diff: str = ""
    merged_upgrade_files: list[str] = field(default_factory=list[str])
    changed_upgrade_files: list[str] = field(default_factory=list[str])


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


def upgrade_file_version(file_name: str) -> tuple[int, int, int] | None:
    """
    Return the version an upgrade file name carries, or None when the name is not a version.

    Upgrade files are named after the version they upgrade to, with the patch level omitted
    on a few old ones (9.0.sql), which is read as .0 here.
    """
    match = UPGRADE_FILE_PATTERN.match(file_name)
    if match is None:
        return None
    major, minor, patch = [*match.group(1).split("."), "0"][:VERSION_PART_COUNT]
    return (int(major), int(minor), int(patch))


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


def last_revision_history_heading_entry(markdown: str) -> tuple[int, str] | None:
    """Return the line index and text of the final level-two revision-history heading."""
    lines = markdown.splitlines()
    for line_index in range(len(lines) - 1, -1, -1):
        match = REVISION_HISTORY_HEADING_PATTERN.fullmatch(lines[line_index])
        if match is not None:
            return (line_index, match.group(1))
    return None


def last_revision_history_heading(markdown: str) -> str | None:
    """Return the text of the final level-two revision-history heading."""
    entry = last_revision_history_heading_entry(markdown)
    return entry[1] if entry is not None else None


def revision_history_heading_version(heading: str) -> str | None:
    """Return the canonical version at the start of a revision-history heading."""
    match = REVISION_HISTORY_VERSION_PATTERN.match(heading)
    return match.group(1) if match is not None else None


def is_revision_history_content(line: str) -> bool:
    """Return whether a revision-history line carries text rather than a heading or a separator."""
    text = line.strip()
    return not text.startswith("#") and any(character.isalnum() for character in text)


def is_revision_history_heading(line: str) -> bool:
    """
    Return whether a revision-history line is a level-two section heading.

    The same pattern that locates the final section decides this, so a line counts as a
    heading exactly when it could be one: '### details' is a sub-heading, not a section.
    """
    return REVISION_HISTORY_HEADING_PATTERN.fullmatch(line) is not None


def parse_unified_diff(diff_text: str) -> tuple[list[tuple[int, str]], list[tuple[int, str]]]:
    """
    Split a unified diff into its added and removed lines.

    Both carry a line number in the new file: an added line its own, a removed line the
    position its hunk is anchored at, which is where the removed text sat relative to the
    new file. That lets a caller tell which part of the new file a change belongs to.
    """
    added_lines: list[tuple[int, str]] = []
    removed_lines: list[tuple[int, str]] = []
    # None marks "still in this file's header". Zero cannot serve as that marker: git writes
    # '@@ -1 +0,0 @@' for a deletion at the head of a file, so zero is a legal hunk start.
    line_number: int | None = None
    hunk_anchor = 0
    for line in diff_text.splitlines():
        hunk = DIFF_HUNK_PATTERN.match(line)
        if line.startswith("diff --git"):
            line_number = None
        elif hunk is not None:
            line_number = int(hunk.group(1))
            hunk_anchor = line_number
        elif line_number is None:
            continue  # header lines of the current file, before its first hunk
        elif line.startswith("+"):
            added_lines.append((line_number, line[1:]))
            line_number += 1
        elif line.startswith("-"):
            removed_lines.append((hunk_anchor, line[1:]))
        elif line.startswith(" "):
            line_number += 1
    return (added_lines, removed_lines)


def count_headings(diff_lines: list[tuple[int, str]]) -> int:
    """Count the level-two headings among the given diff lines."""
    return sum(1 for _, line in diff_lines if is_revision_history_heading(line))


def opens_final_section(
    added_lines: list[tuple[int, str]],
    removed_lines: list[tuple[int, str]],
    final_heading: str,
) -> bool:
    """
    Return whether the pull request created the final section rather than renaming its heading.

    Both tests compare heading *text*, never line numbers: added lines carry their own position
    while removed lines carry their hunk's, so any line inserted next to the heading would break
    a positional comparison. A rename replaces one heading with another and leaves the number of
    headings unchanged, while opening a section raises it.
    """
    final_heading_is_added = any(line.strip() == final_heading for _, line in added_lines)
    return final_heading_is_added and count_headings(added_lines) > count_headings(removed_lines)


def revision_history_has_final_section_addition(merged_markdown: str, revision_history_diff: str) -> bool:
    """
    Return whether the pull request adds non-heading text below the final level-two heading.

    The decision is taken from the diff of the pull request rather than from a comparison of
    the base and merged snapshots, because a new version section is a different section than
    the base's final one: its text may legitimately repeat wording of an earlier section.
    Lines are compared by their stripped text, so re-indenting or reordering existing entries
    cancels out instead of counting as an addition. That cancellation is scoped to the final
    section: text moved into it from an earlier section is text this section did not have.

    A section the pull request opens is decided from the merged file instead, because an entry
    that keeps its wording while moving under a newly inserted heading is a context line of the
    diff rather than an addition. Renaming an existing heading is not opening a section.
    """
    heading_entry = last_revision_history_heading_entry(merged_markdown)
    if heading_entry is None:
        return False

    heading_line_number = heading_entry[0] + 1
    merged_lines = merged_markdown.splitlines()
    added_lines, removed_lines = parse_unified_diff(revision_history_diff)
    if opens_final_section(added_lines, removed_lines, merged_lines[heading_entry[0]].strip()):
        # The pull request opened this section, so every line below the heading is text the
        # section did not have. Reading them from the merged file also catches the entries git
        # renders as context because they kept their wording while moving under the new heading.
        return any(is_revision_history_content(line) for line in merged_lines[heading_line_number:])

    added_below_heading = Counter(
        line.strip()
        for line_number, line in added_lines
        if line_number > heading_line_number and is_revision_history_content(line)
    )
    removed_below_heading = Counter(
        line.strip()
        for line_number, line in removed_lines
        if line_number >= heading_line_number and is_revision_history_content(line)
    )
    return any(count > removed_below_heading[text] for text, count in added_below_heading.items())


def evaluate_revision_history(
    merged_version: str,
    merged_revision_history: str,
    revision_history_diff: str,
) -> Verdict:
    """Check the final section version and that the pull request adds text beneath it."""
    last_heading = last_revision_history_heading(merged_revision_history)
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
    if not revision_history_has_final_section_addition(merged_revision_history, revision_history_diff):
        return Verdict(
            ok=False,
            reason=f"add revision-history text below the final '## {merged_version}' heading",
        )
    return Verdict(ok=True, reason=f"revision history adds text for version {merged_version}")


def is_version_like_upgrade_file(file_name: str) -> bool:
    """Return whether the upgrade play reads the file name as a version."""
    return VERSION_LIKE_UPGRADE_FILE_PATTERN.fullmatch(file_name) is not None


def non_canonical_upgrade_files(file_names: list[str]) -> list[str]:
    """Return the upgrade files the play reads as a version but the gate refuses to interpret."""
    return sorted(
        file_name
        for file_name in file_names
        if is_version_like_upgrade_file(file_name) and upgrade_file_version(file_name) is None
    )


def deleted_upgrade_files(changed_upgrade_files: list[str], merged_upgrade_files: list[str]) -> list[str]:
    """Return the upgrade scripts the pull request removes from the merge result."""
    remaining = set(merged_upgrade_files)
    return sorted(
        file_name for file_name in changed_upgrade_files if file_name.endswith(".sql") and file_name not in remaining
    )


def upgrade_files_above_version(file_names: list[str], version: tuple[int, int, int]) -> list[str]:
    """Return the upgrade files named above a version, which the upgrade play never selects."""
    return sorted(
        file_name
        for file_name in file_names
        if (file_version := upgrade_file_version(file_name)) is not None and file_version > version
    )


def upgrade_files_below_version(file_names: list[str], version: tuple[int, int, int]) -> list[str]:
    """Return the upgrade files named below a version, which installations on it skip."""
    return sorted(
        file_name
        for file_name in file_names
        if (file_version := upgrade_file_version(file_name)) is not None and file_version < version
    )


def evaluate_upgrade_files(
    merged_version: str,
    base_version: str,
    merged_upgrade_files: list[str],
    changed_upgrade_files: list[str],
) -> Verdict:
    """
    Decide whether the upgrade files of the merge result can still reach an installation.

    roles/database/tasks/upgrade-database.yml selects an upgrade file when its version is
    at least the version installed on the system and at most product_version. A file above
    product_version is therefore never selected, and a file below the version the base
    branch already carries is skipped by every installation which has taken that version.
    Both cases are silent: the upgrade play succeeds and the changes simply never arrive.

    The second case is the merge-order hazard between two pull requests: whichever opens the
    lower version and merges second keeps an upgrade file that no upgraded installation runs.
    It is judged over the upgrade files the pull request touches, not over the names it adds,
    because appending statements to an older file strands them exactly as adding one does -
    which is why versioning.md forbids modifying the upgrade script of an older version. Files
    the pull request deletes drop out, as they are not in the merge result.

    An upgrade script the pull request removes is refused as well: an installation older than
    its version can no longer run those operations, and the upgrade play reports nothing. The
    diff is taken without rename detection, so moving a released script counts as removing it.

    A name the play reads as a version but this gate does not, such as the zero-padded
    9.4.07.sql, is refused outright rather than interpreted: the play compares it loosely and
    would place it at 9.4.7, so leaving it unjudged hides exactly the two silent cases above.
    Only the files the pull request touches are held to that, which leaves the padded names
    this repository carries from its 5.1 releases alone.

    File names which do not carry a version at all are left to the upgrade play itself.
    """
    try:
        merged = parse_version(merged_version)
        base = parse_version(base_version)
    except ValueError as error:
        return Verdict(ok=False, reason=str(error))

    changed_in_merge_result = sorted(set(changed_upgrade_files) & set(merged_upgrade_files))

    above_product_version = upgrade_files_above_version(merged_upgrade_files, merged)
    if above_product_version:
        return Verdict(
            ok=False,
            reason=(
                f"upgrade file {', '.join(above_product_version)} is above product_version "
                f"{merged_version} and would never run. Raise product_version in "
                f"inventory/group_vars/all.yml or rename the file."
            ),
        )

    deleted = deleted_upgrade_files(changed_upgrade_files, merged_upgrade_files)
    if deleted:
        return Verdict(
            ok=False,
            reason=(
                f"upgrade file {', '.join(deleted)} is deleted, so an installation older than that "
                f"version can no longer run it. Upgrade scripts stay as they were released; put "
                f"any correction in {merged_version}.sql."
            ),
        )

    non_canonical = non_canonical_upgrade_files(changed_in_merge_result)
    if non_canonical:
        return Verdict(
            ok=False,
            reason=(
                f"upgrade file {', '.join(non_canonical)} is not named after a plain "
                f"major.minor.patch version, so the upgrade play and this gate would read it "
                f"differently. Name it {merged_version}.sql, without zero-padded components."
            ),
        )

    behind_base_version = upgrade_files_below_version(changed_in_merge_result, base)
    if behind_base_version:
        return Verdict(
            ok=False,
            reason=(
                f"upgrade file {', '.join(behind_base_version)} is below version {base_version} of the "
                f"base branch, so installations already on {base_version} would skip the change. "
                f"Put it in {merged_version}.sql instead."
            ),
        )

    return Verdict(ok=True, reason="every upgrade file can be selected")


def evaluate_version_lifecycle(merged_version: str, base_version: str, sealed: set[str]) -> Verdict:
    """
    Decide whether the merge result's product version is a legal successor of the base version.

    A passing verdict carries the reason the remaining gate rules append their own outcome to.
    Raises ValueError when either version is not a canonical major.minor.patch.
    """
    merged = parse_version(merged_version)
    base = parse_version(base_version)

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
    return Verdict(ok=True, reason=f"version {base_version} is sealed, opening version {merged_version}")


def evaluate_gate(
    merged_version: str,
    base_version: str,
    sealed: set[str],
    files: GateFiles,
    revision_history_required: bool = True,
) -> Verdict:
    """
    Decide whether a pull request may merge, given the version its merge result carries.

    merged_version is read from refs/pull/<n>/merge so that a pull request which does not
    touch all.yml automatically inherits the base version instead of being blocked.
    """
    try:
        version_verdict = evaluate_version_lifecycle(merged_version, base_version, sealed)
    except ValueError as error:
        return Verdict(ok=False, reason=str(error))
    if not version_verdict.ok:
        return version_verdict

    upgrade_file_verdict = evaluate_upgrade_files(
        merged_version,
        base_version,
        files.merged_upgrade_files,
        files.changed_upgrade_files,
    )
    if not upgrade_file_verdict.ok:
        return upgrade_file_verdict

    if not revision_history_required:
        return Verdict(
            ok=True,
            reason=f"{version_verdict.reason}; revision history is exempt for this automated pull request",
        )

    revision_history_verdict = evaluate_revision_history(
        merged_version,
        files.merged_revision_history,
        files.revision_history_diff,
    )
    if not revision_history_verdict.ok:
        return revision_history_verdict
    return Verdict(ok=True, reason=f"{version_verdict.reason}; {revision_history_verdict.reason}")


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


def read_names(file_path: str | None) -> list[str]:
    """Read newline separated file names, or none when no file is given."""
    if file_path is None:
        return []
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
    gate.add_argument(
        "--revision-history-diff",
        required=True,
        help="unified diff of revision-history.md from the base branch to refs/pull/<n>/merge",
    )
    gate.add_argument(
        "--skip-revision-history",
        action="store_true",
        help="skip revision-history validation for a caller-verified automated pull request",
    )
    gate.add_argument(
        "--upgrade-files",
        help="newline separated names of roles/database/files/upgrade on refs/pull/<n>/merge",
    )
    gate.add_argument(
        "--changed-upgrade-files",
        help="newline separated names in roles/database/files/upgrade the pull request adds or modifies",
    )
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
        merged_revision_history = ""
        if arguments.revision_history is not None:
            merged_revision_history = Path(arguments.revision_history).read_text(encoding="utf-8")
        return evaluate_gate(
            read_version(arguments.merged_version, arguments.merged_file),
            read_version(arguments.base_version, arguments.base_file),
            sealed_versions(read_tags(arguments.tags_file)),
            GateFiles(
                merged_revision_history=merged_revision_history,
                revision_history_diff=Path(arguments.revision_history_diff).read_text(encoding="utf-8"),
                merged_upgrade_files=read_names(arguments.upgrade_files),
                changed_upgrade_files=read_names(arguments.changed_upgrade_files),
            ),
            not arguments.skip_revision_history,
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

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
from dataclasses import dataclass, field
from pathlib import Path

# Same product_version extraction as the Sonar workflows use, see .github/workflows/sonarcloud.yml.
PRODUCT_VERSION_PATTERN = re.compile(r'^product_version:\s*"?([^"\s]+)"?\s*$', re.MULTILINE)
VERSION_PATTERN = re.compile(r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")
SEALING_TAG_PATTERN = re.compile(r"^v?((?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*))(?:-dev)?$")
VERSION_TAG_PATTERN = re.compile(r"^v?(\d+\.\d+\.\d+)(?:-[0-9A-Za-z.-]+)?$")
# Sealing is the one step of the lifecycle no workflow performs, so the verdict that waits for
# it names the procedure a human has to follow.
SEALING_DOCUMENTATION = "documentation/developer-docs/versioning.md#sealing-a-version"
UPGRADE_FILE_PATTERN = re.compile(r"^((?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:\.(?:0|[1-9]\d*))?)\.sql$")
# What a new upgrade script must be named. upgrade-database.yml globs every *.sql in the
# directory and compares its stem with the installed version, so a name that is not a full
# version breaks that comparison instead of being ignored.
CANONICAL_UPGRADE_FILE_PATTERN = re.compile(r"^(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.sql$")
# major, minor and patch, the patch level being optional in a few old upgrade file names.
VERSION_PART_COUNT = 3


@dataclass(frozen=True)
class GateFiles:
    """
    The file inputs one gate evaluation reads.

    They travel together and describe the same pull request: the upgrade file names in the
    merge result, and the upgrade file names the pull request adds or modifies. Each stays
    empty when a caller does not supply it, which keeps the rules that need it out of the way
    of callers that only test versions.
    """

    merged_upgrade_files: list[str] = field(default_factory=list[str])
    changed_upgrade_files: list[str] = field(default_factory=list[str])


@dataclass(frozen=True)
class Verdict:
    """Result of a gate evaluation: whether it passes and why."""

    ok: bool
    reason: str

    def to_dict(self) -> dict[str, object]:
        """Return the verdict as a JSON serializable dictionary."""
        return {"ok": self.ok, "reason": self.reason}


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


def non_canonical_upgrade_files(file_names: list[str]) -> list[str]:
    """Return the upgrade scripts that are not named after a full major.minor.patch version."""
    return sorted(
        file_name
        for file_name in file_names
        if file_name.endswith(".sql") and CANONICAL_UPGRADE_FILE_PATTERN.fullmatch(file_name) is None
    )


def deleted_upgrade_files(
    changed_upgrade_files: list[str],
    merged_upgrade_files: list[str],
    merged: tuple[int, int, int],
) -> list[str]:
    """
    Return the upgrade scripts the pull request removes that an installation may have run.

    A script named above product_version has never been selected by the upgrade play, so no
    installation can have run it and nothing is lost by removing it. Leaving it out here is
    what lets a pull request clear such a script from the base branch - which the rule that
    refuses every merge result carrying one would otherwise block forever.
    """
    remaining = set(merged_upgrade_files)
    return sorted(
        file_name
        for file_name in changed_upgrade_files
        if file_name.endswith(".sql")
        and file_name not in remaining
        and not ((file_version := upgrade_file_version(file_name)) is not None and file_version > merged)
    )


def describe_upgrade_files(file_names: list[str]) -> tuple[str, str, str]:
    """Return the subject, verb and pronoun a verdict needs to name one or several scripts."""
    if len(file_names) > 1:
        return (f"upgrade files {', '.join(file_names)}", "are", "them")
    return (f"upgrade file {file_names[0]}", "is", "it")


def deleted_upgrade_reason(deleted_files: list[str], merged_version: str) -> str:
    """
    Explain a refused deletion without naming a deleted script as the place to correct it.

    One deletion can hold both a released script and the open version's own, so the remedy is
    stated for both cases rather than chosen for the set: whichever script the author meant to
    undo, restoring comes first, which is what keeps the current version's script from being
    named as the remedy while it is itself being removed.
    """
    subject, verb, pronoun = describe_upgrade_files(deleted_files)
    return (
        f"{subject} {verb} deleted. Upgrade scripts are kept once merged, because an installation that "
        f"has already taken their version would otherwise never run them. Restore {pronoun}, then "
        f"empty the body of {merged_version}.sql to undo the current version's change, or put a "
        f"correction to an older script there."
    )


def upgrade_files_above_version(file_names: list[str], version: tuple[int, int, int]) -> list[str]:
    """Return the upgrade files named above a version, which the upgrade play never selects."""
    return sorted(
        file_name
        for file_name in file_names
        if (file_version := upgrade_file_version(file_name)) is not None and file_version > version
    )


def upgrade_files_the_base_has_passed(
    file_names: list[str],
    base: tuple[int, int, int],
    merged: tuple[int, int, int],
) -> list[str]:
    """
    Return the upgrade scripts an installation on the base version never runs again.

    The upgrade play selects a script whose version is above the installed one, so every script
    below the base version is already behind such an installation. The base version's own script
    counts as behind it as soon as the pull request raises the version: a bump is only permitted
    once the base version is sealed, and sealing is what says installations have taken it. While
    the version stays open that script is the one the change belongs in, so it is not judged here.
    """
    return sorted(
        file_name
        for file_name in file_names
        if (file_version := upgrade_file_version(file_name)) is not None
        and (file_version < base or (merged > base and file_version == base))
    )


def evaluate_upgrade_files(
    merged_version: str,
    base_version: str,
    merged_upgrade_files: list[str],
    changed_upgrade_files: list[str],
) -> Verdict:
    """
    Decide whether the upgrade files of the merge result can still reach an installation.

    roles/database/tasks/upgrade-database.yml selects an upgrade file when its version is at
    least the version installed on the system and at most product_version. A file above
    product_version is therefore never selected, and a file below the version the base branch
    already carries is skipped by every installation which has taken that version. Both cases
    are silent: the upgrade play succeeds and the changes simply never arrive.

    The second case is the merge-order hazard between two pull requests: whichever opens the
    lower version and merges second keeps an upgrade file that no upgraded installation runs.
    It is judged over the upgrade files the pull request touches, not over the names it adds,
    because appending statements to an older file strands them exactly as adding one does -
    which is why versioning.md forbids modifying the upgrade script of an older version.

    A script the pull request removes is refused as well: every installation older than its
    version loses those operations, and the upgrade play reports nothing. That holds for a
    script of the still open version too, which a colleague's installation may already have
    run, though such a script can still be emptied. The one exception is a script named above
    product_version: never selectable, so never run, so free to remove - and the only way to
    clear one that reached the base branch, since the first rule refuses every merge result
    still carrying it. The diff is taken without rename detection, so moving a released script
    counts as removing it.

    A script the pull request adds or modifies must be named after a full major.minor.patch
    version and sit directly in the upgrade directory. The play globs that one directory and
    compares each stem with the installed version, so a padded 9.4.07.sql lands at 9.4.7 where
    this gate reads no version at all, a patchless 9.0.sql is read differently by each, a
    readme.sql makes that comparison fail outright, and a script in a subdirectory is never
    globbed at all. Only touched files are held to this, which leaves the patchless and padded
    names this repository carries from earlier releases alone.
    """
    try:
        merged = parse_version(merged_version)
        base = parse_version(base_version)
    except ValueError as error:
        return Verdict(ok=False, reason=str(error))

    changed_in_merge_result = sorted(set(changed_upgrade_files) & set(merged_upgrade_files))

    above_product_version = upgrade_files_above_version(merged_upgrade_files, merged)
    if above_product_version:
        subject, verb, pronoun = describe_upgrade_files(above_product_version)
        return Verdict(
            ok=False,
            reason=(
                f"{subject} {verb} above product_version {merged_version} and would never run. "
                f"Raise product_version in inventory/group_vars/all.yml or rename {pronoun}."
            ),
        )

    deleted = deleted_upgrade_files(changed_upgrade_files, merged_upgrade_files, merged)
    if deleted:
        return Verdict(ok=False, reason=deleted_upgrade_reason(deleted, merged_version))

    non_canonical = non_canonical_upgrade_files(changed_in_merge_result)
    if non_canonical:
        subject, verb, _ = describe_upgrade_files(non_canonical)
        # The rule catches a wrong name and a wrong place, and a subdirectory script can carry a
        # correct name, so the diagnosis blames the pair rather than the name alone.
        scripts = "major.minor.patch.sql scripts" if len(non_canonical) > 1 else "a major.minor.patch.sql script"
        return Verdict(
            ok=False,
            reason=(
                f"{subject} {verb} not {scripts} directly in roles/database/files/upgrade/. The "
                f"upgrade play globs that one directory and compares each name with the installed "
                f"version, so put the change in {merged_version}.sql there."
            ),
        )

    behind_base_version = upgrade_files_the_base_has_passed(changed_in_merge_result, base, merged)
    if behind_base_version:
        subject, verb, _ = describe_upgrade_files(behind_base_version)
        return Verdict(
            ok=False,
            reason=(
                f"{subject} {verb} named for a version the base branch has already passed, so "
                f"installations already on {base_version} would skip the change. Put the change "
                f"in {merged_version}.sql instead."
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
                f"Create tag v{base_version}-dev or v{base_version} before opening version "
                f"{merged_version}. Sealing is a manual step: {SEALING_DOCUMENTATION} "
                f"gives the tag, release label and title to use."
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

    return Verdict(ok=True, reason=f"{version_verdict.reason}; {upgrade_file_verdict.reason}")


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
        text = Path(file_path).read_text(encoding="utf-8", errors="surrogateescape")
    return [line.strip() for line in text.splitlines() if line.strip()]


def read_names(file_path: str | None) -> list[str]:
    """
    Read NUL separated file names, or none when no file is given.

    The listings are asked of git NUL separated because it otherwise C-quotes any path
    holding a non-ASCII byte or a control character, and a quoted path carries neither the
    trailing '.sql' nor the version the upgrade rules judge it by. That leaves raw path bytes,
    which POSIX does not require to be UTF-8, so they are decoded the way the file system
    itself is read: an undecodable byte survives as a surrogate and reaches the naming rule
    instead of aborting the whole evaluation with a decode error. Names are taken verbatim, so
    a leading or trailing space in one stays part of it.
    """
    if file_path is None:
        return []
    text = Path(file_path).read_text(encoding="utf-8", errors="surrogateescape")
    return [name for name in text.split("\0") if name]


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
    gate.add_argument(
        "--upgrade-files",
        help="NUL separated names of roles/database/files/upgrade on refs/pull/<n>/merge",
    )
    gate.add_argument(
        "--changed-upgrade-files",
        help="NUL separated names in roles/database/files/upgrade the pull request adds or modifies",
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
        return evaluate_gate(
            read_version(arguments.merged_version, arguments.merged_file),
            read_version(arguments.base_version, arguments.base_file),
            sealed_versions(read_tags(arguments.tags_file)),
            GateFiles(
                merged_upgrade_files=read_names(arguments.upgrade_files),
                changed_upgrade_files=read_names(arguments.changed_upgrade_files),
            ),
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

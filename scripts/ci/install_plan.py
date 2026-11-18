#!/usr/bin/env python3
"""
Decide what one run of the FWO Test Install workflow tests.

A pull request installs on Ubuntu 24.04 and runs the Python unit tests on Python 3.11. The
checks that cost the most and find the least on a single pull request run after the merge
into develop or main: the installs on every supported Ubuntu version and the Python unit
tests on every supported Python version. Two kinds of pull request get part of that earlier,
because they are what those checks exist for:

- a pull request that changes Ansible files also installs on Ubuntu 22.04, the only supported
  system whose installer Python (3.10) gets ansible-core 2.18 instead of 2.19,
- a Dependabot pip pull request runs the Python unit tests on every supported version.

The rules live in side-effect free functions so that they can be unit tested. ``main`` only
reads the event from the command line and writes the plan as step outputs.

See documentation/developer-docs/github/test-install-workflow.md.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

# Branch protection requires this check name, so the Ubuntu 24.04 job keeps it.
REQUIRED_INSTALL_LABEL = "Test install on ubuntu-latest"
DEFAULT_INSTALL_OS = "ubuntu-24.04"
ANSIBLE_CORE_218_OS = "ubuntu-22.04"
NEWEST_INSTALL_OS = "ubuntu-26.04"
INSTALL_LABELS = {
    DEFAULT_INSTALL_OS: REQUIRED_INSTALL_LABEL,
    ANSIBLE_CORE_218_OS: "Test install on ubuntu-22.04 (ansible-core 2.18)",
    NEWEST_INSTALL_OS: "Test install on ubuntu-26.04",
}
ALL_INSTALL_OS = [ANSIBLE_CORE_218_OS, DEFAULT_INSTALL_OS, NEWEST_INSTALL_OS]
# The Python of RHEL/Rocky and Debian 12 installations, see scripts/install-ansible-from-venv.sh.
PULL_REQUEST_PYTHON_VERSIONS = ["3.11"]
ALL_PYTHON_VERSIONS = ["3.10", "3.11", "3.12", "3.13", "3.14"]
DEPENDABOT_PIP_BRANCH_PREFIX = "dependabot/pip/"
MERGED_BRANCH_REFS = frozenset({"refs/heads/develop", "refs/heads/main"})
# Files whose change can behave differently under ansible-core 2.18 and 2.19: playbooks, role
# tasks and variables, inventory, the pinned installer requirements and collections, the
# launcher scripts and helper playbooks in scripts/, and this workflow itself. Role files/ directories hold application code and
# are left out, see the workflow documentation for the full list and the reasons.
ANSIBLE_FILE_PATTERN = re.compile(
    r"^(?:"
    r"site\.yml"
    r"|ansible\.cfg"
    r"|requirements\.txt"
    r"|inventory/"
    r"|collections/"
    r"|callback_plugins/"
    r"|scripts/[^/]+\.(?:sh|yml)"
    r"|roles/[^/]+/(?:tasks|handlers|templates|defaults|vars|meta)/"
    r"|\.github/workflows/test-install\.yml"
    r"|scripts/ci/install_plan\.py"
    r")"
)


@dataclass(frozen=True)
class RunEvent:
    """The parts of the triggering event the plan depends on."""

    event_name: str
    ref: str
    head_ref: str = ""
    full_requested: bool = False
    changed_files: list[str] = field(default_factory=list[str])


@dataclass(frozen=True)
class Plan:
    """What one run tests, and why, for the job summary."""

    install_os: list[str]
    python_versions: list[str]
    reasons: list[str]

    def install_matrix(self) -> dict[str, list[dict[str, str]]]:
        """Return the install matrix in the form the workflow's strategy.matrix expects."""
        entries = [{"os": os_name, "label": INSTALL_LABELS[os_name]} for os_name in self.install_os]
        return {"include": entries}


def is_merged_run(event: RunEvent) -> bool:
    """Tell whether the run tests develop or main, or was started manually with the full plan."""
    if event.event_name == "push":
        return event.ref in MERGED_BRANCH_REFS
    return event.event_name == "workflow_dispatch" and event.full_requested


def ansible_files(changed_files: list[str]) -> list[str]:
    """Return the changed files that can behave differently under another ansible-core."""
    return [path for path in changed_files if ANSIBLE_FILE_PATTERN.match(path)]


def select_plan(event: RunEvent) -> Plan:
    """Select the installs and Python versions for one run."""
    if is_merged_run(event):
        return Plan(
            install_os=list(ALL_INSTALL_OS),
            python_versions=list(ALL_PYTHON_VERSIONS),
            reasons=["merged into develop or main, or started manually with the full plan: all checks"],
        )

    install_os = [DEFAULT_INSTALL_OS]
    python_versions = list(PULL_REQUEST_PYTHON_VERSIONS)
    reasons: list[str] = []

    changed_ansible_files = ansible_files(event.changed_files)
    if changed_ansible_files:
        install_os.insert(0, ANSIBLE_CORE_218_OS)
        reasons.append(f"changes Ansible files ({changed_ansible_files[0]}, ...): also installs on ubuntu-22.04")

    if event.event_name == "pull_request" and event.head_ref.startswith(DEPENDABOT_PIP_BRANCH_PREFIX):
        python_versions = list(ALL_PYTHON_VERSIONS)
        reasons.append("Dependabot pip update: Python unit tests on every supported version")

    if not reasons:
        reasons.append("pull request: install on ubuntu-24.04, Python unit tests on Python 3.11")
    return Plan(install_os=install_os, python_versions=python_versions, reasons=reasons)


def read_changed_files(file_path: str | None) -> list[str]:
    """Read the NUL separated output of git diff --name-only -z, or nothing without a file."""
    if not file_path:
        return []
    content = Path(file_path).read_bytes().decode("utf-8", errors="backslashreplace")
    return [name for name in content.split("\0") if name]


def render_outputs(plan: Plan) -> str:
    """Render the plan as GITHUB_OUTPUT lines."""
    lines = [
        f"install_matrix={json.dumps(plan.install_matrix(), separators=(',', ':'))}",
        f"python_versions={json.dumps(plan.python_versions, separators=(',', ':'))}",
    ]
    return "\n".join(lines) + "\n"


def render_summary(plan: Plan) -> str:
    """Render the plan as a Markdown job summary."""
    installs = ", ".join(INSTALL_LABELS[os_name] for os_name in plan.install_os)
    lines = [
        "### Test plan",
        "",
        f"- Installs: {installs}",
        f"- Python unit tests: {', '.join(plan.python_versions)}",
        *(f"- Reason: {reason}" for reason in plan.reasons),
        "",
    ]
    return "\n".join(lines)


def build_parser() -> argparse.ArgumentParser:
    """Build the command line parser."""
    parser = argparse.ArgumentParser(description="Select what one FWO Test Install run tests")
    parser.add_argument("--event-name", required=True)
    parser.add_argument("--ref", required=True)
    parser.add_argument("--head-ref", default="")
    parser.add_argument("--full", default="false", help="the workflow_dispatch input, 'true' or 'false'")
    parser.add_argument("--changed-files", help="NUL separated list of the files the pull request changes")
    parser.add_argument("--output", required=True, help="file the step outputs are appended to")
    parser.add_argument("--summary", help="file the Markdown summary is appended to")
    return parser


def main(argv: list[str] | None = None) -> int:
    """Write the plan of the current run as step outputs and as a job summary."""
    arguments = build_parser().parse_args(argv)
    event = RunEvent(
        event_name=arguments.event_name,
        ref=arguments.ref,
        head_ref=arguments.head_ref,
        full_requested=arguments.full == "true",
        changed_files=read_changed_files(arguments.changed_files),
    )
    plan = select_plan(event)
    with Path(arguments.output).open("a", encoding="utf-8") as output_file:
        output_file.write(render_outputs(plan))
    summary = render_summary(plan)
    if arguments.summary:
        with Path(arguments.summary).open("a", encoding="utf-8") as summary_file:
            summary_file.write(summary)
    sys.stdout.write(summary)
    return 0


if __name__ == "__main__":
    sys.exit(main())

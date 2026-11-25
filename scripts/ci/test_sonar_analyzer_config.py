"""
Policy tests for Sonar's C# analyzer in the build (documentation/developer-docs/github/sonarcloud-workflow.md).

roles/Directory.Build.props adds the SonarAnalyzer.CSharp package to Debug builds only, so that Release builds stay
without it. roles/SonarAnalyzer.globalconfig sets the level of each rule. The
compiler ignores lines it cannot read, and a rule with a misspelled level silently keeps its default level, so every
line must be a valid severity entry. The entries are sorted by rule number, so that each rule is listed only once.
"""

from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from pathlib import Path

import pytest

REPOSITORY_ROOT = Path(__file__).parents[2]
BUILD_PROPS_FILE = REPOSITORY_ROOT / "roles" / "Directory.Build.props"
GLOBAL_CONFIG_FILE_NAME = "SonarAnalyzer.globalconfig"
GLOBAL_CONFIG_FILE = REPOSITORY_ROOT / "roles" / GLOBAL_CONFIG_FILE_NAME
# the config lies next to Directory.Build.props, so that every project below roles/ finds it
GLOBAL_CONFIG_INCLUDE = f"$(MSBuildThisFileDirectory){GLOBAL_CONFIG_FILE_NAME}"
SONAR_PACKAGE = "SonarAnalyzer.CSharp"
DEBUG_CONDITION_PATTERN = re.compile(r"^\s*'\$\(Configuration\)'\s*==\s*'Debug'\s*$")
GLOBAL_CONFIG_HEADER = "is_global = true"
SEVERITY_LINE_PATTERN = re.compile(r"^dotnet_diagnostic\.S(?P<number>\d+)\.severity = (?P<level>[a-z]+)$")
# none is only allowed for rules that the SonarCloud profile does not contain (see the documentation)
ALLOWED_LEVELS = ("warning", "suggestion", "none")
COMMENT_PREFIXES = ("#", ";")


def read_severity_lines(text: str) -> list[str]:
    """Returns the lines of a global analyzer config without comments, blank lines and the is_global header."""
    lines = [line.strip() for line in text.splitlines()]
    entries = [line for line in lines if line and not line.startswith(COMMENT_PREFIXES)]
    if not entries or entries[0] != GLOBAL_CONFIG_HEADER:
        pytest.fail(f"{GLOBAL_CONFIG_FILE_NAME} must start with '{GLOBAL_CONFIG_HEADER}'")
    return entries[1:]


def invalid_severity_lines(lines: list[str]) -> list[str]:
    """Returns the lines that are no Sonar rule severity entry with an allowed level."""
    invalid_lines: list[str] = []
    for line in lines:
        match = SEVERITY_LINE_PATTERN.match(line)
        if match is None or match["level"] not in ALLOWED_LEVELS:
            invalid_lines.append(line)
    return invalid_lines


def rule_numbers(lines: list[str]) -> list[int]:
    """Returns the rule numbers of the severity entries in file order."""
    numbers: list[int] = []
    for line in lines:
        match = SEVERITY_LINE_PATTERN.match(line)
        if match is not None:
            numbers.append(int(match["number"]))
    return numbers


def find_sonar_item_group() -> ET.Element:
    """Returns the ItemGroup of Directory.Build.props that references the Sonar analyzer package."""
    project = ET.parse(BUILD_PROPS_FILE).getroot()  # noqa: S314 - a file of this repository, not untrusted data
    for item_group in project.iter("ItemGroup"):
        for package in item_group.iter("PackageReference"):
            if package.get("Include") == SONAR_PACKAGE:
                return item_group
    pytest.fail(f"{BUILD_PROPS_FILE.name} does not reference {SONAR_PACKAGE}")


def test_sonar_analyzer_runs_only_in_debug_builds() -> None:
    item_group = find_sonar_item_group()

    assert DEBUG_CONDITION_PATTERN.match(item_group.get("Condition", ""))


def test_sonar_analyzer_package_is_private() -> None:
    item_group = find_sonar_item_group()
    packages = [package for package in item_group.iter("PackageReference") if package.get("Include") == SONAR_PACKAGE]

    assert [package.get("PrivateAssets") for package in packages] == ["all"]


def test_sonar_analyzer_uses_the_global_config() -> None:
    item_group = find_sonar_item_group()
    config_files = [config.get("Include", "") for config in item_group.iter("GlobalAnalyzerConfigFiles")]

    assert config_files == [GLOBAL_CONFIG_INCLUDE]


def test_global_config_contains_only_valid_severity_entries() -> None:
    lines = read_severity_lines(GLOBAL_CONFIG_FILE.read_text(encoding="utf-8"))

    assert lines
    assert invalid_severity_lines(lines) == []


def test_global_config_lists_each_rule_once_in_ascending_order() -> None:
    numbers = rule_numbers(read_severity_lines(GLOBAL_CONFIG_FILE.read_text(encoding="utf-8")))

    assert numbers == sorted(set(numbers))


@pytest.mark.parametrize(
    ("line", "is_valid"),
    [
        ("dotnet_diagnostic.S1118.severity = warning", True),
        ("dotnet_diagnostic.S1118.severity = suggestion", True),
        ("dotnet_diagnostic.S1118.severity = none", True),
        ("dotnet_diagnostic.S1118.severity = suggestions", False),
        ("dotnet_diagnostic.S1118.severity = silent", False),
        ("dotnet_diagnostic.S1118.severity = error", False),
        ("dotnet_diagnostic.CA1822.severity = warning", False),
        ("dotnet_diagnostic.S1118.severity=warning", False),
    ],
)
def test_invalid_severity_lines(line: str, *, is_valid: bool) -> None:
    assert (invalid_severity_lines([line]) == []) is is_valid


def test_read_severity_lines_skips_comments_and_requires_the_header() -> None:
    text = f"# comment\n; comment\n{GLOBAL_CONFIG_HEADER}\n\ndotnet_diagnostic.S100.severity = warning\n"

    assert read_severity_lines(text) == ["dotnet_diagnostic.S100.severity = warning"]


def test_read_severity_lines_fails_without_the_header() -> None:
    with pytest.raises(pytest.fail.Exception):
        read_severity_lines("dotnet_diagnostic.S100.severity = warning\n")


def test_rule_numbers_keeps_the_file_order() -> None:
    lines = ["dotnet_diagnostic.S200.severity = warning", "dotnet_diagnostic.S100.severity = suggestion"]

    assert rule_numbers(lines) == [200, 100]

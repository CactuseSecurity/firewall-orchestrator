"""Tests for replace_ec2_apt_mirror.sh, which the FWO Test Install workflow runs before its first apt call."""

from __future__ import annotations

import shutil
import subprocess
from pathlib import Path

import pytest

SCRIPT_PATH = Path(__file__).with_name("replace_ec2_apt_mirror.sh")
FALLBACK_MIRRORS = "https://archive.ubuntu.com/ubuntu/\tpriority:2\nhttps://security.ubuntu.com/ubuntu/\tpriority:3\n"
CENTRAL_ARCHIVE_LIST = f"http://archive.ubuntu.com/ubuntu/\tpriority:1\n{FALLBACK_MIRRORS}"


def run_script(mirror_list: Path) -> subprocess.CompletedProcess[str]:
    """Run the script on the given mirror list."""
    bash = shutil.which("bash")
    if bash is None:
        pytest.fail("bash is required for the shell tests")
    return subprocess.run(  # noqa: S603
        [bash, str(SCRIPT_PATH), str(mirror_list)], check=False, capture_output=True, text=True
    )


@pytest.mark.parametrize("region", ["us-east-1", "us-east-2", "us-west-2"])
def test_replaces_the_ec2_mirror_of_every_region(tmp_path: Path, region: str) -> None:
    mirror_list = tmp_path / "apt-mirrors.txt"
    mirror_list.write_text(
        f"http://{region}.ec2.archive.ubuntu.com/ubuntu/\tpriority:1\n{FALLBACK_MIRRORS}", encoding="utf-8"
    )

    result = run_script(mirror_list)

    assert result.returncode == 0, result.stderr
    assert mirror_list.read_text(encoding="utf-8") == CENTRAL_ARCHIVE_LIST
    assert "Replaced the EC2 mirror" in result.stdout


def test_second_run_changes_nothing(tmp_path: Path) -> None:
    mirror_list = tmp_path / "apt-mirrors.txt"
    mirror_list.write_text(CENTRAL_ARCHIVE_LIST, encoding="utf-8")

    result = run_script(mirror_list)

    assert result.returncode == 0, result.stderr
    assert mirror_list.read_text(encoding="utf-8") == CENTRAL_ARCHIVE_LIST


@pytest.mark.parametrize(
    "mirror",
    ["azure.archive.ubuntu.com", "ec2.archive.ubuntu.com.example.invalid", "us-east-2xec2.archive.ubuntu.com"],
)
def test_keeps_other_mirrors(tmp_path: Path, mirror: str) -> None:
    mirror_list = tmp_path / "apt-mirrors.txt"
    content = f"http://{mirror}/ubuntu/\tpriority:1\n{FALLBACK_MIRRORS}"
    mirror_list.write_text(content, encoding="utf-8")

    result = run_script(mirror_list)

    assert result.returncode == 0, result.stderr
    assert mirror_list.read_text(encoding="utf-8") == content
    assert "stays unchanged" in result.stdout


def test_accepts_a_runner_without_mirror_list(tmp_path: Path) -> None:
    result = run_script(tmp_path / "missing.txt")

    assert result.returncode == 0, result.stderr
    assert "nothing to replace" in result.stdout

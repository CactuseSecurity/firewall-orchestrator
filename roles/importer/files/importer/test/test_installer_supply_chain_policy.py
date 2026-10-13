"""Regression tests for exact RPM signature-check targets in the installer policy (F6)."""

from types import ModuleType

import pytest

# The installer policy belongs to the source checkout and is not copied to deployed importer installations.
policy: ModuleType = pytest.importorskip(
    "scripts.ci.test_installer_supply_chain_policy", reason="Installer policy requires the source checkout"
)


@pytest.mark.parametrize(
    "command",
    [
        None,
        {},
        {"argv": []},
        {"cmd": None},
        {"argv": ["rpmkeys", "--checksig", "/opt/x.rpm.old"]},
        {"argv": ["rpmkeys", "--checksig", "/opt/x.rpm2"]},
        {"argv": ["rpmkeys", "--checksig", "/opt/x.rpm", "extra"]},
        {"argv": ["echo", "rpmkeys", "--checksig", "/opt/x.rpm"]},
        {"argv": ["rpmkeys --checksig /opt/x.rpm"]},
        {"cmd": "rpmkeys --checksig /opt/x.rpm.old"},
        {"cmd": "rpmkeys --checksig /opt/x.rpm2"},
        "rpmkeys --checksig /opt/x.rpm.old",
        "rpmkeys --checksig /opt/x.rpm2",
        "echo rpmkeys --checksig /opt/x.rpm",
        "rpmkeys --checksig /opt/other.rpm; echo /opt/x.rpm",
    ],
)
def test_signature_check_rejects_misleading_command_arguments(command: object) -> None:
    """A different package or a mention of the check command does not verify the downloaded RPM."""
    tasks: list[dict[str, object]] = [
        {"rpm_key": {"key": "https://example.com/key.asc", "fingerprint": "A" * 40}},
        {"command": command},
    ]

    assert not policy.verifies_package_signature(tasks, "/opt/x.rpm")


@pytest.mark.parametrize(
    ("command", "package_path"),
    [
        ({"argv": ["rpmkeys", "--checksig", "/opt/x.rpm"]}, "/opt/x.rpm"),
        ({"argv": ["rpmkeys", "--checksig", "/opt/x y.rpm"]}, "/opt/x y.rpm"),
        ({"cmd": "rpmkeys --checksig '/opt/x y.rpm'"}, "/opt/x y.rpm"),
        ('rpmkeys --checksig "/opt/x y.rpm"', "/opt/x y.rpm"),
        ("rpmkeys --checksig {{ lib_tmp_dir }}/{{ dotnet_rpm_name }}", "{{ lib_tmp_dir }}/{{ dotnet_rpm_name }}"),
        (
            {"cmd": 'rpmkeys --checksig "{{ lib_tmp_dir }}/{{ dotnet_rpm_name }}"'},
            "{{ lib_tmp_dir }}/{{ dotnet_rpm_name }}",
        ),
        ("rpmkeys --checksig {{ directory | default('/opt') }}/x.rpm", "{{ directory | default('/opt') }}/x.rpm"),
    ],
)
def test_signature_check_accepts_the_exact_package_argument(command: object, package_path: str) -> None:
    """Explicit argv and quoted/Jinja command forms retain the package path as a single argument."""
    tasks: list[dict[str, object]] = [
        {"rpm_key": {"key": "https://example.com/key.asc", "fingerprint": "A" * 40}},
        {"command": command},
    ]

    assert policy.verifies_package_signature(tasks, package_path)

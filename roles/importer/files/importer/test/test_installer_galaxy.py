"""Installer collection source configuration and shell entry point regression tests."""

from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path
from unittest.mock import Mock

import pytest
import yaml

from scripts import run_installer_galaxy as galaxy


@pytest.fixture
def galaxy_settings() -> dict[str, object]:
    return {
        "fwo_galaxy_server_list": ["artifactory"],
        "fwo_galaxy_servers": {"artifactory": {"url": "https://artifacts.example.test/api/ansible/remote/"}},
    }


def test_settings_translate_without_modifying_environment(galaxy_settings: dict[str, object]) -> None:
    environment = {"PATH": "/bin", "ANSIBLE_CONFIG": "ansible.cfg"}
    result = galaxy.build_galaxy_environment(galaxy_settings, environment)
    assert result["ANSIBLE_GALAXY_SERVER_LIST"] == "artifactory"
    assert result["ANSIBLE_GALAXY_SERVER_ARTIFACTORY_URL"] == "https://artifacts.example.test/api/ansible/remote/"
    assert result["ANSIBLE_CONFIG"] == "ansible.cfg"
    assert environment == {"PATH": "/bin", "ANSIBLE_CONFIG": "ansible.cfg"}


def test_environment_overrides_and_credentials_are_preserved(galaxy_settings: dict[str, object]) -> None:
    environment = {
        "ANSIBLE_GALAXY_SERVER_LIST": "other",
        "ANSIBLE_GALAXY_SERVER_ARTIFACTORY_URL": "https://override.example.test/",
        "ANSIBLE_GALAXY_SERVER_OTHER_URL": "https://other.example.test/",
        "ANSIBLE_GALAXY_SERVER_ARTIFACTORY_TOKEN": "test-credential",
    }
    assert galaxy.build_galaxy_environment(galaxy_settings, environment) == environment


def test_no_galaxy_settings_preserves_defaults() -> None:
    environment = {"ANSIBLE_GALAXY_SERVER_LIST": "existing"}
    assert galaxy.build_galaxy_environment({"fwo_endpoint_hostname": "fwo.example.test"}, environment) == environment


def test_multiple_servers_keep_priority_order() -> None:
    settings: dict[str, object] = {
        "fwo_galaxy_server_list": ["internal", "artifactory"],
        "fwo_galaxy_servers": {
            "artifactory": {"url": "https://artifacts.example.test/"},
            "internal": {"url": "https://internal.example.test/"},
        },
    }
    assert galaxy.build_galaxy_environment(settings, {})["ANSIBLE_GALAXY_SERVER_LIST"] == "internal,artifactory"


@pytest.mark.parametrize(
    ("server_list", "servers"),
    [
        (None, {}),
        ("artifactory", {}),
        ([], {}),
        (["artifactory"], None),
        (["artifactory"], []),
        (["artifactory"], {}),
        (["artifactory", "artifactory"], {}),
        ([42], {}),
        ([["nested"]], {}),
        (["Artifactory"], {}),
        (["bad-name"], {}),
        (["bad\nname"], {}),
        (["artifactory"], {42: {"url": "https://example.test/"}}),
        (["artifactory"], {"BAD": {"url": "https://example.test/"}}),
    ],
)
def test_invalid_server_configuration_rejected(server_list: object, servers: object) -> None:
    with pytest.raises(galaxy.InstallerSettingsError):
        galaxy.build_galaxy_environment({"fwo_galaxy_server_list": server_list, "fwo_galaxy_servers": servers}, {})


@pytest.mark.parametrize(
    "server",
    [
        None,
        "https://example.test/",
        {},
        {"url": "https://example.test/", "token": "test-credential"},
        {"url": None},
        {"url": 42},
        {"url": "https://example.test/no-trailing-slash"},
        {"url": "https://user:credential@example.test/"},
        {"url": "https://example.test/?token=credential"},
        {"url": "https://example.test/#fragment"},
        {"url": "ftp://example.test/"},
        {"url": "https:///"},
        {"url": "https://[invalid/"},
        {"url": "https://example.test:invalid/"},
        {"url": "https://example.test/\n"},
        {"url": "https://example.test/\x00/"},
        {"url": "https://example.test/path with space/"},
    ],
)
def test_invalid_url_rejected_without_echoing_input(server: object) -> None:
    with pytest.raises(galaxy.InstallerSettingsError) as error:
        galaxy.validate_server_url(server)
    assert "credential" not in str(error.value).replace("credentials", "")


def test_load_default_settings(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    settings_path = tmp_path / "settings.yml"
    monkeypatch.delenv("FWORCH_LOCAL_SETTINGS", raising=False)
    monkeypatch.setattr(galaxy, "k_default_settings_file", settings_path)
    assert galaxy.load_install_settings(None) == {}
    settings_path.write_text("fwo_endpoint_hostname: fwo.example.test\n", encoding="utf-8")
    assert galaxy.load_install_settings(None) == {"fwo_endpoint_hostname": "fwo.example.test"}


def test_load_explicit_settings(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    settings_path = tmp_path / "settings.yml"
    settings_path.write_text("fwo_endpoint_hostname: fwo.example.test\n", encoding="utf-8")
    monkeypatch.setenv("FWORCH_LOCAL_SETTINGS", str(settings_path))
    assert galaxy.load_install_settings(None) == {"fwo_endpoint_hostname": "fwo.example.test"}
    assert galaxy.load_install_settings(str(settings_path)) == {"fwo_endpoint_hostname": "fwo.example.test"}
    assert galaxy.load_install_settings("") == {}
    with pytest.raises(ValueError, match="does not exist"):
        galaxy.load_install_settings(str(tmp_path / "missing"))
    monkeypatch.setenv("FWORCH_LOCAL_SETTINGS", str(tmp_path / "missing"))
    with pytest.raises(ValueError, match="does not exist"):
        galaxy.load_install_settings(None)


@pytest.mark.parametrize("content", ["", "# only comments", "- a list", "invalid: [", "!!python/object:evil {}"])
def test_invalid_settings_file(tmp_path: Path, content: str) -> None:
    settings_path = tmp_path / "settings.yml"
    settings_path.write_text(content, encoding="utf-8")
    with pytest.raises(galaxy.InstallerSettingsError):
        galaxy.load_install_settings(str(settings_path))


def test_unreadable_settings_file(tmp_path: Path) -> None:
    with pytest.raises(ValueError, match="cannot be read"):
        galaxy.load_install_settings(str(tmp_path))


def test_invalid_unicode_settings_file(tmp_path: Path) -> None:
    settings_path = tmp_path / "settings.yml"
    settings_path.write_bytes(b"\xff")
    with pytest.raises(ValueError, match="valid YAML"):
        galaxy.load_install_settings(str(settings_path))


def test_missing_pyyaml_is_reported_clearly(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    settings_path = tmp_path / "settings.yml"
    settings_path.write_text("fwo_endpoint_hostname: fwo.example.test\n", encoding="utf-8")
    monkeypatch.setitem(sys.modules, "yaml", None)
    with pytest.raises(galaxy.InstallerSettingsError, match="needs PyYAML"):
        galaxy.load_install_settings(str(settings_path))


def test_absent_default_settings_need_no_pyyaml(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("FWORCH_LOCAL_SETTINGS", raising=False)
    monkeypatch.setattr(galaxy, "k_default_settings_file", tmp_path / "missing.yml")
    monkeypatch.setitem(sys.modules, "yaml", None)
    assert galaxy.load_install_settings(None) == {}


def test_main_passes_arguments_and_exit_status(monkeypatch: pytest.MonkeyPatch) -> None:
    runner = Mock(return_value=subprocess.CompletedProcess(args=[], returncode=7))
    monkeypatch.setattr(galaxy.subprocess, "run", runner)
    assert galaxy.main(["--settings", "", "collection", "install", "-r", "requirements.yml", "--force"]) == 7
    assert runner.call_args.args[0] == ["ansible-galaxy", "collection", "install", "-r", "requirements.yml", "--force"]
    assert runner.call_args.kwargs["check"] is False


def test_invalid_settings_stop_before_galaxy(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    runner = Mock()
    monkeypatch.setattr(galaxy.subprocess, "run", runner)
    assert galaxy.main(["--settings", str(tmp_path / "missing"), "collection", "install"]) == 1
    runner.assert_not_called()


def test_missing_galaxy_reports_failure(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(galaxy.subprocess, "run", Mock(side_effect=FileNotFoundError("ansible-galaxy missing")))
    assert galaxy.main(["--settings", "", "collection", "install"]) == 1


def make_installer_stubs(tmp_path: Path) -> dict[str, str]:
    """Keep entry point tests offline: stub every package manager and Galaxy executable."""
    bin_path = tmp_path / "bin"
    bin_path.mkdir()
    for executable in ["sudo", "pip", "ansible-playbook"]:
        stub = bin_path / executable
        stub.write_text("#!/bin/bash\nexit 0\n", encoding="utf-8")
        stub.chmod(0o755)
    (bin_path / "ansible").write_text("#!/bin/bash\necho 'ansible [core 2.19.7]'\n", encoding="utf-8")
    (bin_path / "ansible").chmod(0o755)
    (bin_path / "python3").write_text(
        '#!/bin/bash\nif [[ "$1" == "-m" && "$2" == "venv" ]]; then exit 0; fi\nexec "$REAL_PYTHON" "$@"\n',
        encoding="utf-8",
    )
    (bin_path / "python3").chmod(0o755)
    (bin_path / "python3.11").symlink_to(bin_path / "python3")
    (bin_path / "ansible-galaxy").write_text(
        f"#!{sys.executable}\nimport json, os, pathlib, sys\n"
        "pathlib.Path(os.environ['GALAXY_CAPTURE']).write_text(json.dumps({"
        "'servers': os.environ.get('ANSIBLE_GALAXY_SERVER_LIST'),"
        "'url': os.environ.get('ANSIBLE_GALAXY_SERVER_ARTIFACTORY_URL'), 'args': sys.argv[1:]}))\n",
        encoding="utf-8",
    )
    (bin_path / "ansible-galaxy").chmod(0o755)
    venv_path = tmp_path / "venv"
    (venv_path / "bin").mkdir(parents=True)
    (venv_path / "bin" / "activate").write_text("# Stub activation\n", encoding="utf-8")
    environment = {key: value for key, value in os.environ.items() if not key.startswith("ANSIBLE_GALAXY_")}
    environment.update(
        {
            "PATH": f"{bin_path}:{os.environ['PATH']}",
            "REAL_PYTHON": sys.executable,
            "FWORCH_INSTALLER_VENV": str(venv_path),
            "GALAXY_CAPTURE": str(tmp_path / "capture.json"),
        },
    )
    return environment


def make_launcher_repo(tmp_path: Path) -> Path:
    """Minimal repository layout the launcher needs: one pinned collection that is not installed yet."""
    working_path = tmp_path / "repo"
    (working_path / "collections").mkdir(parents=True)
    (working_path / "collections/requirements.yml").write_text(
        "collections:\n  - name: community.general\n    version: 11.4.2\n",
        encoding="utf-8",
    )
    return working_path


@pytest.mark.parametrize("entry_point", ["bootstrap", "launcher"])
def test_installer_entry_points_use_installation_sources(
    tmp_path: Path,
    galaxy_settings: dict[str, object],
    entry_point: str,
) -> None:
    repo_path = Path(galaxy.__file__).resolve().parent.parent
    environment = make_installer_stubs(tmp_path)
    settings_path = tmp_path / "settings.yml"
    settings_path.write_text(yaml.safe_dump(galaxy_settings), encoding="utf-8")
    environment["FWORCH_LOCAL_SETTINGS"] = str(settings_path)
    if entry_point == "bootstrap":
        command = ["bash", str(repo_path / "scripts/install-ansible-from-venv.sh")]
        working_path = repo_path
    else:
        working_path = make_launcher_repo(tmp_path)
        command = ["bash", str(repo_path / "scripts/run-playbook-with-sudo.sh"), "site.yml"]
    result = subprocess.run(command, cwd=working_path, env=environment, capture_output=True, text=True, check=False)  # noqa: S603 - offline stubs
    assert result.returncode == 0, result.stderr
    captured = json.loads(Path(environment["GALAXY_CAPTURE"]).read_text(encoding="utf-8"))
    assert captured["servers"] == "artifactory"
    assert captured["url"] == "https://artifacts.example.test/api/ansible/remote/"
    assert captured["args"] == [
        "collection",
        "install",
        "-r",
        "collections/requirements.yml",
        "-p",
        "collections",
        "--force",
    ]


def test_launcher_runs_galaxy_with_ansible_python(tmp_path: Path, galaxy_settings: dict[str, object]) -> None:
    """The python3 on the PATH may lack PyYAML (e.g. Ansible from pipx); Ansible's own interpreter has it."""
    repo_path = Path(galaxy.__file__).resolve().parent.parent
    environment = make_installer_stubs(tmp_path)
    bin_path = tmp_path / "bin"
    # Without site-packages, the python3 on the PATH cannot import PyYAML.
    (bin_path / "python3").write_text('#!/bin/bash\nexec "$REAL_PYTHON" -S "$@"\n', encoding="utf-8")
    (bin_path / "ansible").write_text(
        f"#!/bin/bash\necho 'ansible [core 2.19.7]'\necho '  python version = 3.x (main) [GCC] ({sys.executable})'\n",
        encoding="utf-8",
    )
    probe = subprocess.run(  # noqa: S603 - offline stub
        [str(bin_path / "python3"), "-c", "import yaml"], env=environment, capture_output=True, check=False
    )
    if probe.returncode == 0:
        pytest.skip("PyYAML is importable without site-packages in this interpreter")
    settings_path = tmp_path / "settings.yml"
    settings_path.write_text(yaml.safe_dump(galaxy_settings), encoding="utf-8")
    environment["FWORCH_LOCAL_SETTINGS"] = str(settings_path)
    command = ["bash", str(repo_path / "scripts/run-playbook-with-sudo.sh"), "site.yml"]
    working_path = make_launcher_repo(tmp_path)
    result = subprocess.run(command, cwd=working_path, env=environment, capture_output=True, text=True, check=False)  # noqa: S603 - offline stubs
    assert result.returncode == 0, result.stderr
    captured = json.loads(Path(environment["GALAXY_CAPTURE"]).read_text(encoding="utf-8"))
    assert captured["url"] == "https://artifacts.example.test/api/ansible/remote/"

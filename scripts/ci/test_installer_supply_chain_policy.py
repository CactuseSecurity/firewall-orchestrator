"""
Policy tests that keep installer downloads verifiable and SSH host keys checked (GHSA-8hf3-3hp5-gj32).

Container images in the inventory are pinned by digest and the running Hasura version is checked against the configured
one. In the role tasks and the playbooks in scripts/, downloaded scripts and .deb packages need a checksum, and
downloaded .rpm packages need a signature check against a signing key imported with a pinned fingerprint. Host key
checking is not switched off in the inventory or in ansible.cfg (host_key_checking, ssh_args). Downloads of other file types (e.g. the Chrome for
Testing archive, an accepted risk) are not covered.
"""

from __future__ import annotations

import configparser
import re
from pathlib import Path
from typing import TYPE_CHECKING, cast

import pytest
import yaml

if TYPE_CHECKING:
    from collections.abc import Iterator

REPOSITORY_ROOT = Path(__file__).parents[2]
INVENTORY_DIRECTORY = REPOSITORY_ROOT / "inventory"
GROUP_VARS_DIRECTORY = INVENTORY_DIRECTORY / "group_vars"
HOSTS_FILE = INVENTORY_DIRECTORY / "hosts.yml"
ANSIBLE_CONFIG_FILE = REPOSITORY_ROOT / "ansible.cfg"
ROLES_DIRECTORY = REPOSITORY_ROOT / "roles"
SCRIPTS_DIRECTORY = REPOSITORY_ROOT / "scripts"
HASURA_INSTALL_TASKS = ROLES_DIRECTORY / "api" / "tasks" / "hasura-install.yml"
IMAGE_KEY_SUFFIX = "_image"
IMAGE_DIGEST_KEY_SUFFIX = "_image_digest"
DIGEST_PATTERN = re.compile(r"^sha256:[0-9a-f]{64}$")
# an image reference ends with a literal digest or with the variable holding it
PINNED_IMAGE_PATTERN = re.compile(r"@(?:sha256:[0-9a-f]{64}|\{\{\s*[a-z0-9_]+_image_digest\s*\}\})$")
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
FINGERPRINT_PATTERN = re.compile(r"^[0-9A-F]{40}$")
# downloads that are executed or installed with root rights afterwards and therefore need a checksum
CHECKSUM_DOWNLOAD_PATTERN = re.compile(
    r"\.(?:sh|deb)\b|dotnet-install|cli-hasura|api_hasura_cli|releases/assets|_deb_name\b", re.IGNORECASE
)
# signed packages, which are verified by their signature instead
SIGNED_PACKAGE_DOWNLOAD_PATTERN = re.compile(r"\.rpm\b|_rpm_name\b", re.IGNORECASE)
UNPINNED_DOTNET_INSTALL_URL = "dot.net/v1/dotnet-install.sh"
NESTED_TASK_KEYS = ("block", "rescue", "always")
PLAY_TASK_KEYS = ("pre_tasks", "tasks", "post_tasks", "handlers")
PLAY_KEY = "hosts"
GET_URL_MODULES = ("get_url", "ansible.builtin.get_url")
RPM_KEY_MODULES = ("rpm_key", "ansible.builtin.rpm_key")
COMMAND_MODULES = ("command", "ansible.builtin.command", "shell", "ansible.builtin.shell")
SIGNATURE_CHECK_COMMAND = ("rpmkeys", "--checksig")
SSH_ARGS_KEYS = ("ansible_ssh_common_args", "ansible_ssh_extra_args", "ansible_ssh_args")
DISABLED_HOST_KEY_CHECKING_PATTERN = re.compile(
    r"StrictHostKeyChecking\s*(?:=\s*|\s+)[\"']?(?:no|off)\b|UserKnownHostsFile\s*(?:=\s*|\s+)[\"']?/dev/null",
    re.IGNORECASE,
)
DISABLED_HOST_KEY_CHECKING_CONFIG_PATTERN = re.compile(
    r"^\s*host_key_checking\s*=\s*(?:false|no|off|0)\s*$", re.IGNORECASE | re.MULTILINE
)
ANSIBLE_SSH_SECTION = "ssh_connection"
ANSIBLE_SSH_ARGS_OPTION = "ssh_args"
# a free-form key=value argument; the value may contain Jinja expressions and quoted parts with spaces
FREE_FORM_ARGUMENT_PATTERN = re.compile(r"(\w+)=((?:\{\{.*?\}\}|\{%.*?%\}|\"[^\"]*\"|'[^']*'|[^\s\"'{]|\{(?![{%]))+)")
COMMAND_ARGUMENT_PATTERN = re.compile(r"(?:\{\{.*?\}\}|\{%.*?%\}|\"[^\"]*\"|'[^']*'|[^\s\"'{]|\{(?![{%]))+")
HASURA_VERSION_URL_SUFFIX = "/v1/version"
HASURA_VERSION_VARIABLE = "api_hasura_version"


def group_vars() -> dict[str, object]:
    """Return the variables of all inventory group_vars files."""
    variables: dict[str, object] = {}
    for path in sorted(GROUP_VARS_DIRECTORY.glob("*.yml")):
        loaded: object = yaml.safe_load(path.read_text(encoding="utf-8"))
        if isinstance(loaded, dict):
            variables.update(cast("dict[str, object]", loaded))
    return variables


def iter_tasks(tasks: object) -> Iterator[dict[str, object]]:
    """Yield every task of a task list, including the tasks nested in block, rescue and always."""
    if not isinstance(tasks, list):
        return
    for task in cast("list[object]", tasks):
        if not isinstance(task, dict):
            continue
        typed_task = cast("dict[str, object]", task)
        yield typed_task
        for key in NESTED_TASK_KEYS:
            yield from iter_tasks(typed_task.get(key))


def iter_file_tasks(content: object) -> Iterator[dict[str, object]]:
    """Yield every task of a task file or of all plays of a playbook."""
    if not isinstance(content, list):
        return
    entries = cast("list[object]", content)
    plays = [cast("dict[str, object]", entry) for entry in entries if isinstance(entry, dict) and PLAY_KEY in entry]
    if not plays:
        yield from iter_tasks(entries)
        return
    for play in plays:
        for key in PLAY_TASK_KEYS:
            yield from iter_tasks(play.get(key))


def parse_free_form(arguments: str) -> dict[str, object]:
    """Return the key=value arguments of a module given in free form, e.g. 'url={{ base }}/x.deb dest=...'."""
    return {key: unquote(value) for key, value in FREE_FORM_ARGUMENT_PATTERN.findall(arguments)}


def unquote(value: str) -> str:
    """Return a free-form value without its quotes."""
    return re.sub(r"\"([^\"]*)\"|'([^']*)'", lambda match: match.group(1) or match.group(2) or "", value)


def module_arguments(task: dict[str, object], modules: tuple[str, ...]) -> dict[str, object] | None:
    """Return the arguments of the first of the given modules a task uses, in mapping or free form."""
    for module in modules:
        arguments = task.get(module)
        if isinstance(arguments, dict):
            return cast("dict[str, object]", arguments)
        if isinstance(arguments, str):
            return parse_free_form(arguments)
    return None


def command_arguments(task: dict[str, object]) -> list[str]:
    """Return separate command arguments, preserving spaces in argv, quoted paths and Jinja expressions."""
    for module in COMMAND_MODULES:
        command = task.get(module)
        if isinstance(command, dict):
            typed_command = cast("dict[str, object]", command)
            argv = typed_command.get("argv")
            if isinstance(argv, list):
                return [str(argument) for argument in cast("list[object]", argv)]
            command = typed_command.get("cmd", "")
        if isinstance(command, str):
            return [
                argument[1:-1] if argument.startswith(("'", '"')) and argument[-1] == argument[0] else argument
                for argument in COMMAND_ARGUMENT_PATTERN.findall(command)
            ]
    return []


def task_files() -> list[Path]:
    """Return the task files of all roles and the playbooks in scripts/."""
    return sorted([*ROLES_DIRECTORY.glob("*/tasks/**/*.yml"), *SCRIPTS_DIRECTORY.glob("*.yml")])


def file_tasks(path: Path) -> list[dict[str, object]]:
    """Return all tasks of a task file or playbook."""
    return list(iter_file_tasks(yaml.safe_load(path.read_text(encoding="utf-8"))))


def get_url_tasks() -> list[tuple[Path, dict[str, object]]]:
    """Return the arguments of every get_url task with the file defining it."""
    found: list[tuple[Path, dict[str, object]]] = []
    for path in task_files():
        for task in file_tasks(path):
            arguments = module_arguments(task, GET_URL_MODULES)
            if arguments is not None:
                found.append((path, arguments))
    return found


def download_target(arguments: dict[str, object]) -> str:
    """Return the url and destination of a download."""
    return f"{arguments.get('url', '')} {arguments.get('dest', '')}"


def needs_checksum(arguments: dict[str, object]) -> bool:
    """Return whether a download is executed or installed with root rights and is not a signed package."""
    return bool(CHECKSUM_DOWNLOAD_PATTERN.search(download_target(arguments)))


def is_signed_package_download(arguments: dict[str, object]) -> bool:
    """Return whether a download is a signed package that must be verified by its signature."""
    return bool(SIGNED_PACKAGE_DOWNLOAD_PATTERN.search(download_target(arguments)))


def verifies_package_signature(tasks: list[dict[str, object]], package_path: str) -> bool:
    """Require a pinned signing key and a dedicated rpmkeys --checksig invocation for the exact downloaded path."""
    imports_pinned_key = any(
        (arguments := module_arguments(task, RPM_KEY_MODULES)) is not None and bool(arguments.get("fingerprint"))
        for task in tasks
    )
    expected_arguments = [*SIGNATURE_CHECK_COMMAND, package_path]
    checks_package = any(command_arguments(task) == expected_arguments for task in tasks)
    return bool(package_path) and imports_pinned_key and checks_package


def ssh_argument_values(content: object) -> Iterator[tuple[str, str]]:
    """Yield every ssh argument variable with its value from inventory content, at any nesting depth."""
    if isinstance(content, dict):
        for key, value in cast("dict[object, object]", content).items():
            if key in SSH_ARGS_KEYS and isinstance(value, str):
                yield str(key), value
            yield from ssh_argument_values(value)
    elif isinstance(content, list):
        for item in cast("list[object]", content):
            yield from ssh_argument_values(item)


def test_iter_tasks_includes_nested_tasks() -> None:
    tasks: object = [
        {"name": "outer", "block": [{"name": "inner"}], "rescue": [{"name": "fallback", "always": [{"name": "deep"}]}]}
    ]

    assert [task["name"] for task in iter_tasks(tasks)] == ["outer", "inner", "fallback", "deep"]


def test_iter_file_tasks_reads_the_tasks_of_playbooks() -> None:
    playbook: object = [{"hosts": "all", "pre_tasks": [{"name": "first"}], "tasks": [{"name": "second"}]}]

    assert [task["name"] for task in iter_file_tasks(playbook)] == ["first", "second"]


def test_module_arguments_reads_free_form_arguments() -> None:
    task: dict[str, object] = {
        "get_url": "url={{ base_url }}/{{ dotnet_deb_name }} dest='/opt/x y.deb' checksum=\"sha256:{{ sum }}\" mode=0644"
    }

    assert module_arguments(task, GET_URL_MODULES) == {
        "url": "{{ base_url }}/{{ dotnet_deb_name }}",
        "dest": "/opt/x y.deb",
        "checksum": "sha256:{{ sum }}",
        "mode": "0644",
    }


@pytest.mark.parametrize(
    ("arguments", "expected"),
    [
        ({"url": "{{ dotnet_install_script_url }}", "dest": "{{ lib_tmp_dir }}/dotnet-install.sh"}, True),
        ({"url": "https://github.com/hasura/graphql-engine/releases/download/v1/cli-hasura-linux-amd64"}, True),
        ({"url": "https://packages.microsoft.com/config/debian/12/{{ dotnet_deb_name }}"}, True),
        ({"url": "https://example.com/tool.sh", "dest": "{{ lib_tmp_dir }}/tool.sh"}, True),
        ({"url": "https://example.com/data.json", "dest": "{{ lib_tmp_dir }}/data.json"}, False),
        ({"url": "https://example.com/release.rpm"}, False),
    ],
)
def test_needs_checksum(arguments: dict[str, object], *, expected: bool) -> None:
    assert needs_checksum(arguments) is expected


def test_verifies_package_signature_needs_a_pinned_key_and_a_check_of_the_package() -> None:
    package = "{{ lib_tmp_dir }}/x.rpm"
    pinned_key: dict[str, object] = {"rpm_key": {"key": "https://example.com/key.asc", "fingerprint": "A" * 40}}
    unpinned_key: dict[str, object] = {"rpm_key": {"key": "https://example.com/key.asc"}}
    signature_check: dict[str, object] = {"command": {"argv": ["rpmkeys", "--checksig", package]}}
    other_check: dict[str, object] = {"command": "rpmkeys --checksig /tmp/other.rpm"}
    suffix_check: dict[str, object] = {"command": {"argv": ["rpmkeys", "--checksig", package + ".old"]}}
    prefix_check: dict[str, object] = {"command": {"argv": ["rpmkeys", "--checksig", package + "2"]}}

    assert verifies_package_signature([pinned_key, signature_check], package)
    assert not verifies_package_signature([unpinned_key, signature_check], package)
    assert not verifies_package_signature([pinned_key], package)
    assert not verifies_package_signature([pinned_key, other_check], package)
    assert not verifies_package_signature([pinned_key, suffix_check], package)
    assert not verifies_package_signature([pinned_key, prefix_check], package)


@pytest.mark.parametrize(
    ("ssh_args", "expected"),
    [
        ("-o StrictHostKeyChecking=no", True),
        ("-o 'StrictHostKeyChecking no'", True),
        ("-o StrictHostKeyChecking=NO -o ControlMaster=auto", True),
        ("-o UserKnownHostsFile=/dev/null", True),
        ("-o StrictHostKeyChecking=off", True),
        ("-o StrictHostKeyChecking=accept-new", False),
        ("-o StrictHostKeyChecking=yes", False),
    ],
)
def test_disabled_host_key_checking_pattern(ssh_args: str, *, expected: bool) -> None:
    assert bool(DISABLED_HOST_KEY_CHECKING_PATTERN.search(ssh_args)) is expected


def test_container_images_are_pinned_by_digest() -> None:
    variables = group_vars()
    images = {key: value for key, value in variables.items() if key.endswith(IMAGE_KEY_SUFFIX)}

    assert images
    for key, value in images.items():
        assert isinstance(value, str), f"{key} is not a string"
        assert PINNED_IMAGE_PATTERN.search(value), f"{key} is not pinned by digest: {value}"


def test_image_digests_are_complete() -> None:
    digests = {key: value for key, value in group_vars().items() if key.endswith(IMAGE_DIGEST_KEY_SUFFIX)}

    assert digests
    for key, value in digests.items():
        assert isinstance(value, str), f"{key} is not a string"
        assert DIGEST_PATTERN.match(value), f"{key} is not a sha256 digest: {value}"


def test_running_hasura_version_is_checked_against_the_configured_version() -> None:
    tasks = file_tasks(HASURA_INSTALL_TASKS)

    reads_version = any(
        str((module_arguments(task, ("uri", "ansible.builtin.uri")) or {}).get("url", "")).endswith(
            HASURA_VERSION_URL_SUFFIX
        )
        for task in tasks
    )
    asserts_version = any(HASURA_VERSION_VARIABLE in str(task.get("assert", "")) for task in tasks)
    assert reads_version, "the installer must read the version of the running Hasura server"
    assert asserts_version, "the installer must compare the running Hasura version with api_hasura_version"


@pytest.mark.parametrize("variable", ["api_hasura_cli_sha256", "dotnet_ms_repo_package_sha256"])
def test_checksum_pins_are_sha256_digests(variable: str) -> None:
    checksums: object = group_vars().get(variable)

    assert isinstance(checksums, dict)
    typed_checksums = cast("dict[str, object]", checksums)
    assert typed_checksums
    for name, checksum in typed_checksums.items():
        assert isinstance(checksum, str), f"{variable}[{name}] is not a string"
        assert SHA256_PATTERN.match(checksum), f"{variable}[{name}] is not a sha256 hex digest"


def test_hasura_cli_checksums_cover_the_supported_architectures() -> None:
    checksums: object = group_vars().get("api_hasura_cli_sha256")

    assert isinstance(checksums, dict)
    assert set(cast("dict[str, object]", checksums)) >= {"amd64", "arm64"}


def test_signing_key_fingerprints_are_complete() -> None:
    variables = group_vars()
    epel_fingerprints: object = variables.get("epel_signing_key_fingerprints")
    microsoft_keys: object = variables.get("dotnet_ms_signing_keys")

    assert isinstance(epel_fingerprints, dict)
    assert isinstance(microsoft_keys, dict)
    typed_microsoft_keys = cast("dict[str, object]", microsoft_keys)
    assert set(typed_microsoft_keys) >= {"9", "10"}
    microsoft_fingerprints = [
        cast("dict[str, object]", key).get("fingerprint") if isinstance(key, dict) else None
        for key in typed_microsoft_keys.values()
    ]
    fingerprints = [*microsoft_fingerprints, *cast("dict[str, object]", epel_fingerprints).values()]
    for fingerprint in fingerprints:
        assert isinstance(fingerprint, str), f"{fingerprint} is not a string"
        assert FINGERPRINT_PATTERN.match(fingerprint), f"{fingerprint} is not a 40 digit upper case key fingerprint"


def test_executable_downloads_are_verified_by_checksum() -> None:
    downloads = [(path, arguments) for path, arguments in get_url_tasks() if needs_checksum(arguments)]

    assert downloads
    for path, arguments in downloads:
        assert arguments.get("checksum"), f"{path.relative_to(REPOSITORY_ROOT)}: {arguments.get('url')} has no checksum"


def test_signed_package_downloads_are_verified_by_signature() -> None:
    downloads = [(path, arguments) for path, arguments in get_url_tasks() if is_signed_package_download(arguments)]

    assert downloads
    for path, arguments in downloads:
        package_path = str(arguments.get("dest", ""))
        assert verifies_package_signature(file_tasks(path), package_path), (
            f"{path.relative_to(REPOSITORY_ROOT)} installs {package_path or arguments.get('url')} without checking its "
            "signature against a signing key imported with a pinned fingerprint"
        )


def test_dotnet_install_script_is_not_downloaded_from_the_moving_url() -> None:
    for path, arguments in get_url_tasks():
        assert UNPINNED_DOTNET_INSTALL_URL not in str(arguments.get("url", "")), (
            f"{path.relative_to(REPOSITORY_ROOT)} downloads the unpinned dotnet-install script"
        )


def test_ssh_host_key_checking_is_not_disabled() -> None:
    hosts: object = yaml.safe_load(HOSTS_FILE.read_text(encoding="utf-8"))
    ssh_arguments = [*ssh_argument_values(group_vars()), *ssh_argument_values(hosts)]

    assert ssh_arguments
    for key, value in ssh_arguments:
        assert not DISABLED_HOST_KEY_CHECKING_PATTERN.search(value), f"{key} disables host key checking: {value}"
    ansible_config_text = ANSIBLE_CONFIG_FILE.read_text(encoding="utf-8")
    assert not DISABLED_HOST_KEY_CHECKING_CONFIG_PATTERN.search(ansible_config_text), (
        "ansible.cfg disables host key checking"
    )
    ansible_config = configparser.ConfigParser(interpolation=None)
    ansible_config.read_string(ansible_config_text)
    ssh_args = ansible_config.get(ANSIBLE_SSH_SECTION, ANSIBLE_SSH_ARGS_OPTION, fallback="")
    assert not DISABLED_HOST_KEY_CHECKING_PATTERN.search(ssh_args), (
        f"ansible.cfg ssh_args disable host key checking: {ssh_args}"
    )

"""
Policy tests that keep installer downloads verifiable and SSH host keys checked (GHSA-8hf3-3hp5-gj32).

Container images are pinned by digest, executable downloads are checked against a checksum before they run, and
host key checking is never switched off, so a changed artifact or an impersonated host fails the installation.
"""

from __future__ import annotations

import re
from pathlib import Path
from typing import TYPE_CHECKING, cast

import pytest
import yaml

if TYPE_CHECKING:
    from collections.abc import Iterator

REPOSITORY_ROOT = Path(__file__).parents[2]
GROUP_VARS_DIRECTORY = REPOSITORY_ROOT / "inventory" / "group_vars"
ROLES_DIRECTORY = REPOSITORY_ROOT / "roles"
IMAGE_KEY_SUFFIX = "_image"
IMAGE_DIGEST_KEY_SUFFIX = "_image_digest"
DIGEST_PATTERN = re.compile(r"^sha256:[0-9a-f]{64}$")
# an image reference ends with a literal digest or with the variable holding it
PINNED_IMAGE_PATTERN = re.compile(r"@(?:sha256:[0-9a-f]{64}|\{\{\s*[a-z0-9_]+_image_digest\s*\}\})$")
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
# downloads that are executed afterwards and therefore need a checksum
EXECUTABLE_DOWNLOAD_PATTERN = re.compile(r"dotnet-install|cli-hasura|api_hasura_cli|releases/assets", re.IGNORECASE)
UNPINNED_DOTNET_INSTALL_URL = "dot.net/v1/dotnet-install.sh"
NESTED_TASK_KEYS = ("block", "rescue", "always")
GET_URL_MODULES = ("get_url", "ansible.builtin.get_url")
SSH_ARGS_KEY = "ansible_ssh_common_args"
DISABLED_HOST_KEY_CHECKING_PATTERN = re.compile(r"StrictHostKeyChecking\s*=\s*no\b", re.IGNORECASE)


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


def get_url_tasks() -> list[tuple[Path, dict[str, object]]]:
    """Return every get_url task of all roles with the file defining it."""
    found: list[tuple[Path, dict[str, object]]] = []
    for path in sorted(ROLES_DIRECTORY.glob("*/tasks/**/*.yml")):
        for task in iter_tasks(yaml.safe_load(path.read_text(encoding="utf-8"))):
            for module in GET_URL_MODULES:
                arguments = task.get(module)
                if isinstance(arguments, dict):
                    found.append((path, cast("dict[str, object]", arguments)))
    return found


def is_executable_download(arguments: dict[str, object]) -> bool:
    """Return whether a get_url call downloads something that is executed afterwards."""
    return bool(EXECUTABLE_DOWNLOAD_PATTERN.search(f"{arguments.get('url', '')} {arguments.get('dest', '')}"))


def test_iter_tasks_includes_nested_tasks() -> None:
    tasks: object = [
        {"name": "outer", "block": [{"name": "inner"}], "rescue": [{"name": "fallback", "always": [{"name": "deep"}]}]}
    ]

    assert [task["name"] for task in iter_tasks(tasks)] == ["outer", "inner", "fallback", "deep"]


@pytest.mark.parametrize(
    ("arguments", "expected"),
    [
        ({"url": "{{ dotnet_install_script_url }}", "dest": "{{ lib_tmp_dir }}/dotnet-install.sh"}, True),
        ({"url": "https://github.com/hasura/graphql-engine/releases/download/v1/cli-hasura-linux-amd64"}, True),
        ({"url": "https://example.com/data.json", "dest": "{{ lib_tmp_dir }}/data.json"}, False),
    ],
)
def test_is_executable_download(arguments: dict[str, object], *, expected: bool) -> None:
    assert is_executable_download(arguments) is expected


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


def test_hasura_cli_checksums_cover_the_supported_architectures() -> None:
    checksums: object = group_vars().get("api_hasura_cli_sha256")

    assert isinstance(checksums, dict)
    typed_checksums = cast("dict[str, object]", checksums)
    assert set(typed_checksums) >= {"amd64", "arm64"}
    for architecture, checksum in typed_checksums.items():
        assert isinstance(checksum, str), f"checksum for {architecture} is not a string"
        assert SHA256_PATTERN.match(checksum), f"checksum for {architecture} is not a sha256 hex digest"


def test_executable_downloads_are_verified_by_checksum() -> None:
    downloads = [(path, arguments) for path, arguments in get_url_tasks() if is_executable_download(arguments)]

    assert downloads
    for path, arguments in downloads:
        assert arguments.get("checksum"), f"{path.relative_to(REPOSITORY_ROOT)}: {arguments.get('url')} has no checksum"


def test_dotnet_install_script_is_not_downloaded_from_the_moving_url() -> None:
    for path, arguments in get_url_tasks():
        assert UNPINNED_DOTNET_INSTALL_URL not in str(arguments.get("url", "")), (
            f"{path.relative_to(REPOSITORY_ROOT)} downloads the unpinned dotnet-install script"
        )


def test_ssh_host_key_checking_is_not_disabled() -> None:
    ssh_args: object = group_vars().get(SSH_ARGS_KEY, "")

    assert isinstance(ssh_args, str)
    assert not DISABLED_HOST_KEY_CHECKING_PATTERN.search(ssh_args), f"{SSH_ARGS_KEY} disables host key checking"

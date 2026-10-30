"""Run Galaxy with this installation's collection sources, without changing the shell."""

# ruff: noqa: INP001 - installer scripts are also imported by the regression tests

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path
from typing import cast
from urllib.parse import urlsplit

import yaml

k_default_settings_file = Path("/usr/local/fworch/etc/fwo-install-settings.yml")
k_server_name_pattern = re.compile(r"[a-z][a-z0-9_]*")
k_first_printable_character = 32


class InstallerSettingsError(ValueError):
    """Invalid persisted installer input."""


def load_install_settings(settings_file: str | None) -> dict[str, object]:
    """Use the same settings location and missing-file policy as the launcher."""
    if settings_file == "":
        return {}
    requested_file = settings_file or os.environ.get("FWORCH_LOCAL_SETTINGS")
    settings_path = Path(requested_file) if requested_file else k_default_settings_file
    try:
        settings = yaml.safe_load(settings_path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        if not requested_file:
            return {}
        raise InstallerSettingsError("The requested installer settings file does not exist.") from None
    except OSError:
        raise InstallerSettingsError("The installer settings file cannot be read.") from None
    except (yaml.YAMLError, UnicodeError):
        # YAML errors can include source lines containing credentials or other input.
        raise InstallerSettingsError("The installer settings file must contain valid YAML.") from None
    if not isinstance(settings, dict):
        raise InstallerSettingsError("The installer settings file must contain a mapping.")
    return cast("dict[str, object]", settings)


def validate_server_url(server: object) -> str:
    """Accept only a literal Galaxy URL; credentials belong in the environment."""
    if not isinstance(server, dict) or set(cast("dict[object, object]", server)) != {"url"}:
        raise InstallerSettingsError(
            "Each fwo_galaxy_servers entry must contain only url; use environment variables for credentials."
        )
    url = cast("dict[str, object]", server)["url"]
    if not isinstance(url, str) or any(
        character.isspace() or ord(character) < k_first_printable_character for character in url
    ):
        raise InstallerSettingsError("Galaxy server URLs must be strings without whitespace or control characters.")
    try:
        parsed_url = urlsplit(url)
        valid_url = (
            parsed_url.scheme in {"http", "https"}
            and bool(parsed_url.hostname)
            and parsed_url.username is None
            and parsed_url.password is None
            and not parsed_url.query
            and not parsed_url.fragment
            and url.endswith("/")
        )
        # Accessing port also validates malformed or out-of-range port numbers.
        _ = parsed_url.port
    except ValueError:
        valid_url = False
    if not valid_url:
        raise InstallerSettingsError(
            "Galaxy URLs must be HTTP(S) URLs ending in /, without credentials, query strings or fragments."
        )
    return url


def validate_server_name(name: object) -> str:
    """Keep environment variable names unambiguous across Galaxy servers."""
    if not isinstance(name, str) or not k_server_name_pattern.fullmatch(name):
        raise InstallerSettingsError(
            "Galaxy server names must start with a lowercase letter and contain only a-z, 0-9 and _."
        )
    return name


def build_galaxy_environment(settings: dict[str, object], environment: dict[str, str]) -> dict[str, str]:
    """Translate persisted sources to Ansible variables, preserving explicit overrides."""
    galaxy_environment = environment.copy()
    if "fwo_galaxy_server_list" not in settings and "fwo_galaxy_servers" not in settings:
        return galaxy_environment
    server_list = settings.get("fwo_galaxy_server_list")
    servers = settings.get("fwo_galaxy_servers")
    if not isinstance(server_list, list) or not server_list or not isinstance(servers, dict):
        raise InstallerSettingsError("Set both a nonempty fwo_galaxy_server_list and a fwo_galaxy_servers mapping.")
    server_names = [validate_server_name(name) for name in cast("list[object]", server_list)]
    server_definitions = cast("dict[object, object]", servers)
    if len(set(server_names)) != len(server_names):
        raise InstallerSettingsError("fwo_galaxy_server_list must not contain duplicate names.")
    for server_name, server in server_definitions.items():
        name = validate_server_name(server_name)
        url = validate_server_url(server)
        galaxy_environment.setdefault(f"ANSIBLE_GALAXY_SERVER_{name.upper()}_URL", url)
    if any(name not in server_definitions for name in server_names):
        raise InstallerSettingsError("Every name in fwo_galaxy_server_list must have an entry in fwo_galaxy_servers.")
    galaxy_environment.setdefault("ANSIBLE_GALAXY_SERVER_LIST", ",".join(server_names))
    return galaxy_environment


def main(arguments: list[str] | None = None) -> int:
    """Validate configuration before starting Galaxy and preserve its exit status."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--settings", help="Installer settings file; an empty path disables settings loading")
    parser.add_argument("galaxy_arguments", nargs=argparse.REMAINDER)
    options = parser.parse_args(arguments)
    try:
        settings = load_install_settings(options.settings)
        environment = build_galaxy_environment(settings, dict(os.environ))
        command = ["ansible-galaxy", *options.galaxy_arguments]
        return subprocess.run(command, env=environment, check=False).returncode  # noqa: S603 - fixed installer executable, no shell
    except (ValueError, OSError) as error:
        print(f"Cannot run installer Galaxy: {error}", file=sys.stderr)  # noqa: T201 - CLI error
        return 1


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""Generate layered CycloneDX SBOM files for Firewall Orchestrator."""

from __future__ import annotations

import argparse
import json
import platform
import re
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from datetime import UTC, datetime
from pathlib import Path
from typing import TYPE_CHECKING, Any, cast

if TYPE_CHECKING:
    from collections.abc import Iterable, Sequence

CYCLONEDX_VERSION = "1.5"
DEFAULT_OUTPUT_DIR = Path("documentation/SBOM/generated")
REFERENCE_PLATFORM = "debian-testing"
REQUIREMENT_SPLIT_RE = re.compile(r"\s*(==|~=|!=|<=|>=|<|>|===)\s*")
PACKAGE_NAME_RE = re.compile(r"^[A-Za-z0-9_.-]+")
VERSION_REQUIREMENT_PARTS = 3


@dataclass(frozen=True)
class Component:
    """A CycloneDX component."""

    name: str
    version: str | None = None
    component_type: str = "library"
    purl: str | None = None
    properties: dict[str, str] = field(default_factory=dict)  # pyright: ignore[reportUnknownVariableType]

    def key(self) -> tuple[str, str, str]:
        return self.name, self.version or "", self.purl or ""

    def to_cyclonedx(self) -> dict[str, Any]:
        component: dict[str, Any] = {
            "type": self.component_type,
            "name": self.name,
            "bom-ref": self.purl or f"{self.component_type}:{self.name}:{self.version or 'unknown'}",
        }
        if self.version:
            component["version"] = self.version
        if self.purl:
            component["purl"] = self.purl
        if self.properties:
            component["properties"] = [{"name": key, "value": value} for key, value in sorted(self.properties.items())]
        return component


def now_timestamp() -> str:
    return datetime.now(UTC).replace(microsecond=0).isoformat()


def run_command(command: Sequence[str]) -> str:
    # Commands are fixed generator backends assembled by this script, not shell-expanded user input.
    result = subprocess.run(command, check=True, capture_output=True, text=True)  # noqa: S603
    return result.stdout


def write_bom(
    output_dir: Path, filename: str, name: str, components: Iterable[Component], properties: dict[str, str]
) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    sorted_components = sorted(components, key=lambda component: component.key())
    bom = {
        "bomFormat": "CycloneDX",
        "specVersion": CYCLONEDX_VERSION,
        "serialNumber": f"urn:uuid:{uuid.uuid4()}",
        "version": 1,
        "metadata": {
            "timestamp": now_timestamp(),
            "component": {"type": "application", "name": name},
            "properties": [{"name": key, "value": value} for key, value in sorted(properties.items())],
        },
        "components": [component.to_cyclonedx() for component in sorted_components],
    }
    target = output_dir / filename
    target.write_text(json.dumps(bom, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return target


def parse_requirement_line(line: str, source: str) -> Component | None:
    clean_line = line.split("#", 1)[0].strip()
    if not clean_line or clean_line.startswith(("-", "--")):
        return None
    clean_line = clean_line.split(";", 1)[0].strip()
    package_match = PACKAGE_NAME_RE.match(clean_line)
    if not package_match:
        return None
    name = package_match.group(0)
    version = None
    properties: dict[str, str] = {"fwo:source": source}
    requirement_parts = REQUIREMENT_SPLIT_RE.split(clean_line, maxsplit=1)
    if len(requirement_parts) == VERSION_REQUIREMENT_PARTS:
        operator = requirement_parts[1]
        spec_version = requirement_parts[2].split(",", 1)[0].strip()
        properties["fwo:requirement"] = clean_line
        if operator in {"==", "==="}:
            version = spec_version
    return Component(name=name, version=version, purl=build_pypi_purl(name, version), properties=properties)


def build_pypi_purl(name: str, version: str | None) -> str | None:
    normalized_name = name.replace("_", "-").lower()
    return f"pkg:pypi/{normalized_name}@{version}" if version else f"pkg:pypi/{normalized_name}"


def components_from_requirements(requirements_file: Path) -> list[Component]:
    if not requirements_file.exists():
        return []
    source = str(requirements_file)
    return [
        component
        for line in requirements_file.read_text(encoding="utf-8").splitlines()
        if (component := parse_requirement_line(line, source)) is not None
    ]


def components_from_csproj(repo_root: Path) -> list[Component]:
    components: list[Component] = []
    for csproj in sorted(repo_root.glob("roles/**/*.csproj")):
        # Project files are local repository inputs, not untrusted XML uploads.
        tree = ET.parse(csproj)  # noqa: S314
        for package_reference in tree.findall(".//PackageReference"):
            name = package_reference.attrib.get("Include") or package_reference.attrib.get("Update")
            version = package_reference.attrib.get("Version")
            if not version:
                version_node = package_reference.find("Version")
                version = version_node.text.strip() if version_node is not None and version_node.text else None
            if name:
                components.append(
                    Component(
                        name=name,
                        version=version,
                        purl=f"pkg:nuget/{name}@{version}" if version else f"pkg:nuget/{name}",
                        properties={"fwo:source": str(csproj)},
                    )
                )
    return components


def components_from_ansible_requirements(requirements_file: Path) -> list[Component]:
    if not requirements_file.exists():
        return []
    components: list[Component] = []
    current_name: str | None = None
    current_version: str | None = None
    for raw_line in requirements_file.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if line.startswith("- name:"):
            if current_name:
                components.append(ansible_component(current_name, current_version, requirements_file))
            current_name = line.split(":", 1)[1].strip().strip("\"'")
            current_version = None
        elif line.startswith("version:") and current_name:
            current_version = line.split(":", 1)[1].strip().strip("\"'")
    if current_name:
        components.append(ansible_component(current_name, current_version, requirements_file))
    return components


def ansible_component(name: str, version: str | None, source: Path) -> Component:
    namespace_name = name.replace(".", "/")
    return Component(
        name=name,
        version=version,
        component_type="library",
        purl=f"pkg:generic/ansible/{namespace_name}@{version}" if version else f"pkg:generic/ansible/{namespace_name}",
        properties={"fwo:source": str(source)},
    )


def components_from_dpkg() -> list[Component]:
    output = run_command(["dpkg-query", "-W", "-f=${binary:Package}\t${Version}\t${Architecture}\n"])
    components: list[Component] = []
    for line in output.splitlines():
        name, version, architecture = line.split("\t", 2)
        components.append(
            Component(
                name=name,
                version=version,
                component_type="operating-system",
                purl=f"pkg:deb/debian/{name}@{version}?arch={architecture}",
                properties={"fwo:architecture": architecture},
            )
        )
    return components


def os_release_properties() -> dict[str, str]:
    properties = {
        "fwo:reference-platform": REFERENCE_PLATFORM,
        "fwo:generator-host": platform.node(),
        "fwo:generator-system": platform.platform(),
    }
    os_release = Path("/etc/os-release")
    if os_release.exists():
        for line in os_release.read_text(encoding="utf-8").splitlines():
            if "=" in line:
                key, value = line.split("=", 1)
                properties[f"os-release:{key}"] = value.strip('"')
    return properties


def component_from_container_inspect(runtime: str, image_or_container: str) -> Component | None:
    try:
        output = run_command([runtime, "image", "inspect", image_or_container])
    except (FileNotFoundError, subprocess.CalledProcessError):
        try:
            output = run_command([runtime, "container", "inspect", image_or_container])
        except (FileNotFoundError, subprocess.CalledProcessError):
            return None
    data = cast("object", json.loads(output))
    if not isinstance(data, list) or not data:
        return None
    item = cast("object", data[0])
    if not isinstance(item, dict):
        return None
    typed_item = cast("dict[str, object]", item)
    repo_digests_raw: object = typed_item.get("RepoDigests") or []
    repo_digests: list[str] = []
    if isinstance(repo_digests_raw, list):
        repo_digests = [str(digest) for digest in cast("list[object]", repo_digests_raw)]
    image_id = str(typed_item.get("Id", ""))
    version = (
        repo_digests[0].split("@", 1)[1]
        if repo_digests and "@" in repo_digests[0]
        else image_id.removeprefix("sha256:")
    )
    return Component(
        name=image_or_container,
        version=version,
        component_type="container",
        purl=f"pkg:oci/{image_or_container}@{version}" if version else None,
        properties={"fwo:container-runtime": runtime},
    )


def source_boms(repo_root: Path, output_dir: Path, reference_platform: str) -> list[Path]:
    properties = {"fwo:mode": "source", "fwo:reference-platform": reference_platform}
    return [
        write_bom(
            output_dir,
            "fwo-dotnet.cdx.json",
            "Firewall Orchestrator .NET",
            components_from_csproj(repo_root),
            properties,
        ),
        write_bom(
            output_dir,
            "fwo-python-importer.cdx.json",
            "Firewall Orchestrator Python Importer",
            components_from_requirements(repo_root / "roles/importer/files/importer/requirements.txt"),
            properties,
        ),
        write_bom(
            output_dir,
            "fwo-python-scripts.cdx.json",
            "Firewall Orchestrator Python Scripts",
            components_from_requirements(repo_root / "scripts/requirements.txt")
            + components_from_requirements(repo_root / "requirements.txt"),
            properties,
        ),
        write_bom(
            output_dir,
            "fwo-ansible.cdx.json",
            "Firewall Orchestrator Ansible",
            components_from_ansible_requirements(repo_root / "collections/requirements.yml"),
            properties,
        ),
    ]


def installed_boms(output_dir: Path, reference_platform: str, container: str | None) -> list[Path]:
    properties = os_release_properties() | {"fwo:mode": "installed", "fwo:reference-platform": reference_platform}
    written = [
        write_bom(
            output_dir,
            "fwo-os-debian-testing.cdx.json",
            "Firewall Orchestrator Debian Testing Host",
            components_from_dpkg(),
            properties,
        )
    ]
    container_components = [
        component
        for runtime in ("podman", "docker")
        if container
        if (component := component_from_container_inspect(runtime, container)) is not None
    ]
    if container_components:
        written.append(
            write_bom(
                output_dir,
                "fwo-containers.cdx.json",
                "Firewall Orchestrator Containers",
                container_components,
                properties,
            )
        )
    return written


def merge_boms(output_dir: Path, bom_paths: Iterable[Path], reference_platform: str) -> Path:
    components_by_key: dict[tuple[str, str, str], Component] = {}
    for bom_path in bom_paths:
        if not bom_path.exists():
            continue
        bom = cast("object", json.loads(bom_path.read_text(encoding="utf-8")))
        if not isinstance(bom, dict):
            continue
        typed_bom = cast("dict[str, object]", bom)
        components: object = typed_bom.get("components", [])
        if not isinstance(components, list):
            continue
        for item_object in cast("list[object]", components):
            if not isinstance(item_object, dict):
                continue
            typed_item = cast("dict[str, object]", item_object)
            item_name = typed_item.get("name")
            if not isinstance(item_name, str):
                continue
            name = item_name
            item_properties: object = typed_item.get("properties", [])
            properties: dict[str, str] = {}
            if isinstance(item_properties, list):
                for prop_object in cast("list[object]", item_properties):
                    if not isinstance(prop_object, dict):
                        continue
                    prop = cast("dict[str, object]", prop_object)
                    prop_name = prop.get("name")
                    prop_value = prop.get("value")
                    if prop_name is not None and prop_value is not None:
                        properties[str(prop_name)] = str(prop_value)
            component = Component(
                name=name,
                version=str(typed_item["version"]) if "version" in typed_item else None,
                component_type=str(typed_item.get("type", "library")),
                purl=str(typed_item["purl"]) if "purl" in typed_item else None,
                properties=properties | {"fwo:merged-from": str(bom_path)},
            )
            components_by_key[component.key()] = component
    return write_bom(
        output_dir,
        "fwo-combined.cdx.json",
        "Firewall Orchestrator",
        components_by_key.values(),
        {"fwo:mode": "combined", "fwo:reference-platform": reference_platform},
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate Firewall Orchestrator CycloneDX SBOM files.")
    parser.add_argument("--mode", choices=["source", "installed", "all"], default="source")
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR)
    parser.add_argument("--reference-platform", default=REFERENCE_PLATFORM)
    parser.add_argument("--container", help="Hasura image or container name to inspect in installed mode")
    parser.add_argument("--merge", action="store_true", help="Write fwo-combined.cdx.json")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    written: list[Path] = []
    if args.mode in {"source", "all"}:
        written.extend(source_boms(args.repo_root, args.output_dir, args.reference_platform))
    if args.mode in {"installed", "all"}:
        written.extend(installed_boms(args.output_dir, args.reference_platform, args.container))
    if args.merge:
        written.append(merge_boms(args.output_dir, written, args.reference_platform))
    for path in written:
        sys.stdout.write(f"{path}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

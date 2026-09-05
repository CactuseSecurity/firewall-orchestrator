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
COMBINED_BOM_FILENAME = "fwo-combined.cdx.json"
DETAILS_DIR_NAME = "fwo-sbom-details"
REQUIREMENT_SPLIT_RE = re.compile(r"\s*(===|==|~=|!=|<=|>=|<|>)\s*")
PACKAGE_NAME_RE = re.compile(r"^[A-Za-z0-9_.-]+")
VERSION_REQUIREMENT_PARTS = 3
PROPERTY_SOURCE = "fwo:source"
PROPERTY_MODE = "fwo:mode"
PROPERTY_REFERENCE_PLATFORM = "fwo:reference-platform"

JsonObject = dict[str, object]


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


def json_object(value: Any) -> JsonObject | None:
    if not isinstance(value, dict):
        return None
    return cast("JsonObject", value)


def json_list(value: Any) -> list[object] | None:
    if not isinstance(value, list):
        return None
    return cast("list[object]", value)


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
    properties: dict[str, str] = {PROPERTY_SOURCE: source}
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
            component = component_from_package_reference(package_reference, csproj)
            if component is not None:
                components.append(component)
    return components


def component_from_package_reference(package_reference: ET.Element, csproj: Path) -> Component | None:
    name = package_reference.attrib.get("Include") or package_reference.attrib.get("Update")
    if not name:
        return None
    version = package_reference.attrib.get("Version") or package_reference_child_version(package_reference)
    return Component(
        name=name,
        version=version,
        purl=f"pkg:nuget/{name}@{version}" if version else f"pkg:nuget/{name}",
        properties={PROPERTY_SOURCE: str(csproj)},
    )


def package_reference_child_version(package_reference: ET.Element) -> str | None:
    version_node = package_reference.find("Version")
    return version_node.text.strip() if version_node is not None and version_node.text else None


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
        properties={PROPERTY_SOURCE: str(source)},
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
        PROPERTY_REFERENCE_PLATFORM: REFERENCE_PLATFORM,
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
    data: object = json.loads(output)
    items = json_list(data)
    if not items:
        return None
    item = json_object(items[0])
    if item is None:
        return None
    repo_digests_raw: object = item.get("RepoDigests") or []
    repo_digests: list[str] = []
    if (digest_items := json_list(repo_digests_raw)) is not None:
        repo_digests = [str(digest) for digest in digest_items]
    image_id = str(item.get("Id", ""))
    version = (
        repo_digests[0].split("@", 1)[1]
        if repo_digests and "@" in repo_digests[0]
        else image_id.removeprefix("sha256:")
    )
    if not version:
        return None
    return Component(
        name=image_or_container,
        version=version,
        component_type="container",
        purl=f"pkg:oci/{image_or_container}@{version}" if version else None,
        properties={"fwo:container-runtime": runtime},
    )


def source_boms(repo_root: Path, output_dir: Path, reference_platform: str) -> list[Path]:
    properties = {PROPERTY_MODE: "source", PROPERTY_REFERENCE_PLATFORM: reference_platform}
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
    properties = os_release_properties() | {PROPERTY_MODE: "installed", PROPERTY_REFERENCE_PLATFORM: reference_platform}
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
        for component in components_from_bom_path(bom_path):
            components_by_key[component.key()] = component
    return write_bom(
        output_dir,
        COMBINED_BOM_FILENAME,
        "Firewall Orchestrator",
        components_by_key.values(),
        {PROPERTY_MODE: "combined", PROPERTY_REFERENCE_PLATFORM: reference_platform},
    )


def components_from_bom_path(bom_path: Path) -> list[Component]:
    if not bom_path.exists():
        return []
    bom: object = json.loads(bom_path.read_text(encoding="utf-8"))
    typed_bom = json_object(bom)
    if typed_bom is None:
        return []
    components = json_list(typed_bom.get("components", []))
    if components is None:
        return []
    return [
        component
        for item_object in components
        if (item := json_object(item_object)) is not None
        if (component := component_from_bom_item(item, bom_path)) is not None
    ]


def component_from_bom_item(typed_item: JsonObject, bom_path: Path) -> Component | None:
    item_name = typed_item.get("name")
    if not isinstance(item_name, str):
        return None
    return Component(
        name=item_name,
        version=str(typed_item["version"]) if "version" in typed_item else None,
        component_type=str(typed_item.get("type", "library")),
        purl=str(typed_item["purl"]) if "purl" in typed_item else None,
        properties=properties_from_bom_item(typed_item) | {"fwo:merged-from": str(bom_path)},
    )


def properties_from_bom_item(typed_item: JsonObject) -> dict[str, str]:
    item_properties = typed_item.get("properties", [])
    properties = json_list(item_properties)
    if properties is None:
        return {}
    return {
        str(prop_name): str(prop_value)
        for prop_object in properties
        if (prop := json_object(prop_object)) is not None
        if (prop_name := prop.get("name")) is not None
        if (prop_value := prop.get("value")) is not None
    }


def existing_bom_paths(output_dir: Path) -> list[Path]:
    details_dir = output_dir / DETAILS_DIR_NAME
    if not details_dir.exists():
        return []
    return sorted(details_dir.glob("*.cdx.json"))


def merge_input_paths(output_dir: Path, written: Iterable[Path], include_existing: bool) -> list[Path]:
    paths_by_name = {path.name: path for path in written}
    if include_existing:
        paths_by_name.update({path.name: path for path in existing_bom_paths(output_dir)})
    return list(paths_by_name.values())


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate Firewall Orchestrator CycloneDX SBOM files.")
    parser.add_argument("--mode", choices=["source", "installed", "all"], default="source")
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR)
    parser.add_argument("--reference-platform", default=REFERENCE_PLATFORM)
    parser.add_argument("--container", help="Hasura image or container name to inspect in installed mode")
    parser.add_argument("--merge", action="store_true", help="Write fwo-combined.cdx.json")
    parser.add_argument("--merge-existing", action="store_true", help="Merge existing *.cdx.json files from output dir")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    details_output_dir = args.output_dir / DETAILS_DIR_NAME
    written: list[Path] = []
    if args.mode in {"source", "all"}:
        written.extend(source_boms(args.repo_root, details_output_dir, args.reference_platform))
    if args.mode in {"installed", "all"}:
        written.extend(installed_boms(details_output_dir, args.reference_platform, args.container))
    if args.merge:
        written.append(
            merge_boms(
                args.output_dir,
                merge_input_paths(args.output_dir, written, args.merge_existing),
                args.reference_platform,
            )
        )
    for path in written:
        sys.stdout.write(f"{path}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

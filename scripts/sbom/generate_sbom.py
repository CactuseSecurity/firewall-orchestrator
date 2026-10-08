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
from dataclasses import dataclass, field, replace
from datetime import datetime, timezone
from pathlib import Path
from typing import TYPE_CHECKING, Any, cast
from urllib.parse import quote

if TYPE_CHECKING:
    from collections.abc import Callable, Iterable, Sequence

CYCLONEDX_VERSION = "1.5"
DEFAULT_OUTPUT_DIR = Path("documentation/SBOM/generated")
REFERENCE_PLATFORM = "debian-testing"
COMBINED_BOM_FILENAME = "fwo-combined.cdx.json"
DETAILS_DIR_NAME = "fwo-sbom-details"
OS_RELEASE_PATH = Path("/etc/os-release")
REQUIREMENT_SPLIT_RE = re.compile(r"\s*(===|==|~=|!=|<=|>=|<|>)\s*")
PACKAGE_NAME_RE = re.compile(r"^[A-Za-z0-9_.-]+")
DISTRO_ID_INVALID_CHARS_RE = re.compile(r"[^a-z0-9._-]")
VERSION_REQUIREMENT_PARTS = 3
PROPERTY_SOURCE = "fwo:source"
PROPERTY_MODE = "fwo:mode"
PROPERTY_REFERENCE_PLATFORM = "fwo:reference-platform"
PROPERTY_REQUIREMENT = "fwo:requirement"
PROPERTY_MARKER = "fwo:marker"
PROPERTY_MERGED_FROM = "fwo:merged-from"
REQUIREMENT_MARKER_SEPARATOR = ";"
# CycloneDX scope of a requirement that only applies to some environments (PEP 508 marker), e.g. ansible 10.7.0
# for Python < 3.11 only: it is not installed everywhere, so it must not be reported as unconditionally required
SCOPE_OPTIONAL = "optional"
PROPERTY_VALUE_SEPARATOR = "; "
PURL_SAFE_CHARS = ":"
PURL_QUALIFIER_SAFE_CHARS = ":/"
# ${Package} instead of ${binary:Package}: the latter appends ":<arch>" on multiarch hosts, the arch goes
# into the purl qualifier instead. ${db:Status-Abbrev} is <desired><state><error>, e.g. "ii " or "rc ".
DPKG_QUERY_FORMAT = "${db:Status-Abbrev}\t${Package}\t${Version}\t${Architecture}\n"
DPKG_STATE_INDEX = 1
# not-installed (n) and config-files (c) leave no package files on disk; every other state - installed,
# unpacked, half-configured, half-installed, triggers-awaited/-pending - does, so those stay in the SBOM
DPKG_STATES_WITHOUT_FILES = frozenset({"n", "c"})
RPM_QUERY_FORMAT = "%{NAME}\t%|EPOCH?{%{EPOCH}}:{0}|\t%{VERSION}-%{RELEASE}\t%{ARCH}\n"
RPM_EMPTY_EPOCHS = frozenset({"", "0", "(none)"})
UNKNOWN_DISTRO_ID = "unknown"

JsonObject = dict[str, object]


@dataclass(frozen=True)
class Component:
    """A CycloneDX component."""

    name: str
    version: str | None = None
    component_type: str = "library"
    purl: str | None = None
    properties: dict[str, str] = field(default_factory=dict)  # pyright: ignore[reportUnknownVariableType]
    scope: str | None = None

    def key(self) -> tuple[str, str, str]:
        return self.name, self.version or "", self.purl or ""

    def bom_ref(self) -> str:
        return self.purl or f"{self.component_type}:{self.name}:{self.version or 'unknown'}"

    def to_cyclonedx(self) -> dict[str, Any]:
        component: dict[str, Any] = {
            "type": self.component_type,
            "name": self.name,
            "bom-ref": self.bom_ref(),
        }
        if self.version:
            component["version"] = self.version
        if self.purl:
            component["purl"] = self.purl
        if self.scope:
            component["scope"] = self.scope
        if self.properties:
            component["properties"] = [{"name": key, "value": value} for key, value in sorted(self.properties.items())]
        return component


def now_timestamp() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


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


def build_purl(
    purl_type: str, namespace: str | None, name: str, version: str | None, qualifiers: dict[str, str] | None = None
) -> str:
    """Build a package URL with percent-encoded components and sorted, non-empty qualifiers."""
    path = quote(name, safe=PURL_SAFE_CHARS)
    if namespace:
        path = f"{quote(namespace, safe=PURL_SAFE_CHARS)}/{path}"
    purl = f"pkg:{purl_type}/{path}"
    if version:
        purl = f"{purl}@{quote(version, safe=PURL_SAFE_CHARS)}"
    qualifier_text = "&".join(
        f"{key}={quote(value, safe=PURL_QUALIFIER_SAFE_CHARS)}"
        for key, value in sorted((qualifiers or {}).items())
        if value
    )
    return f"{purl}?{qualifier_text}" if qualifier_text else purl


def deduplicate_components(components: Iterable[Component]) -> list[Component]:
    """
    Collapse components sharing a bom-ref, which CycloneDX requires to be unique within a BOM.

    Property values of the collapsed components are kept, joined in sorted order. The collapsed component only
    keeps a scope all of them agree on, so one unconditional occurrence makes it required again.
    """
    merged: dict[str, tuple[Component, dict[str, set[str]], set[str | None]]] = {}
    for component in components:
        _, property_values, scopes = merged.setdefault(component.bom_ref(), (component, {}, set()))
        scopes.add(component.scope)
        for key, value in component.properties.items():
            property_values.setdefault(key, set()).add(value)
    return [
        replace(
            component,
            properties={key: PROPERTY_VALUE_SEPARATOR.join(sorted(values)) for key, values in property_values.items()},
            scope=next(iter(scopes)) if len(scopes) == 1 else None,
        )
        for component, property_values, scopes in merged.values()
    ]


def write_bom(
    output_dir: Path,
    filename: str,
    name: str,
    components: Iterable[Component],
    properties: dict[str, str],
    product_version: str | None = None,
) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    sorted_components = sorted(deduplicate_components(components), key=lambda component: component.key())
    bom = {
        "bomFormat": "CycloneDX",
        "specVersion": CYCLONEDX_VERSION,
        "serialNumber": f"urn:uuid:{uuid.uuid4()}",
        "version": 1,
        "metadata": {
            "timestamp": now_timestamp(),
            "component": Component(name=name, version=product_version, component_type="application").to_cyclonedx(),
            "properties": [{"name": key, "value": value} for key, value in sorted(properties.items())],
        },
        "components": [component.to_cyclonedx() for component in sorted_components],
    }
    target = output_dir / filename
    target.write_text(json.dumps(bom, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return target


def parse_requirement_line(line: str, source: str) -> Component | None:
    """
    Parse one requirements.txt line into a component.

    A PEP 508 environment marker is kept in the fwo:marker property and makes the component optional, because
    the requirement is only installed where the marker applies.
    """
    clean_line = line.split("#", 1)[0].strip()
    if not clean_line or clean_line.startswith("-"):
        return None
    clean_line, _, marker = (part.strip() for part in clean_line.partition(REQUIREMENT_MARKER_SEPARATOR))
    package_match = PACKAGE_NAME_RE.match(clean_line)
    if not package_match:
        return None
    name = package_match.group(0)
    version = None
    properties: dict[str, str] = {PROPERTY_SOURCE: source}
    if marker:
        properties[PROPERTY_MARKER] = marker
    requirement_parts = REQUIREMENT_SPLIT_RE.split(clean_line, maxsplit=1)
    if len(requirement_parts) == VERSION_REQUIREMENT_PARTS:
        operator = requirement_parts[1]
        spec_version = requirement_parts[2].split(",", 1)[0].strip()
        properties[PROPERTY_REQUIREMENT] = clean_line
        if operator in {"==", "==="}:
            version = spec_version
    return Component(
        name=name,
        version=version,
        purl=build_pypi_purl(name, version),
        properties=properties,
        scope=SCOPE_OPTIONAL if marker else None,
    )


def build_pypi_purl(name: str, version: str | None) -> str | None:
    normalized_name = name.replace("_", "-").lower()
    return f"pkg:pypi/{normalized_name}@{version}" if version else f"pkg:pypi/{normalized_name}"


def repo_relative_source(path: Path, repo_root: Path) -> str:
    """Return a repository-relative source path, so SBOMs do not depend on (or leak) the checkout location."""
    return path.relative_to(repo_root).as_posix()


def components_from_requirements(requirements_file: Path, repo_root: Path) -> list[Component]:
    if not requirements_file.exists():
        return []
    source = repo_relative_source(requirements_file, repo_root)
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
            component = component_from_package_reference(package_reference, repo_relative_source(csproj, repo_root))
            if component is not None:
                components.append(component)
    return components


def component_from_package_reference(package_reference: ET.Element, source: str) -> Component | None:
    name = package_reference.attrib.get("Include") or package_reference.attrib.get("Update")
    if not name:
        return None
    version = package_reference.attrib.get("Version") or package_reference_child_version(package_reference)
    return Component(
        name=name,
        version=version,
        purl=f"pkg:nuget/{name}@{version}" if version else f"pkg:nuget/{name}",
        properties={PROPERTY_SOURCE: source},
    )


def package_reference_child_version(package_reference: ET.Element) -> str | None:
    version_node = package_reference.find("Version")
    return version_node.text.strip() if version_node is not None and version_node.text else None


def components_from_ansible_requirements(requirements_file: Path, repo_root: Path) -> list[Component]:
    if not requirements_file.exists():
        return []
    source = repo_relative_source(requirements_file, repo_root)
    components: list[Component] = []
    current_name: str | None = None
    current_version: str | None = None
    for raw_line in requirements_file.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if line.startswith("- name:"):
            if current_name:
                components.append(ansible_component(current_name, current_version, source))
            current_name = line.split(":", 1)[1].strip().strip("\"'")
            current_version = None
        elif line.startswith("version:") and current_name:
            current_version = line.split(":", 1)[1].strip().strip("\"'")
    if current_name:
        components.append(ansible_component(current_name, current_version, source))
    return components


def ansible_component(name: str, version: str | None, source: str) -> Component:
    namespace_name = name.replace(".", "/")
    return Component(
        name=name,
        version=version,
        component_type="library",
        purl=f"pkg:generic/ansible/{namespace_name}@{version}" if version else f"pkg:generic/ansible/{namespace_name}",
        properties={PROPERTY_SOURCE: source},
    )


def read_os_release(os_release_path: Path = OS_RELEASE_PATH) -> dict[str, str]:
    if not os_release_path.exists():
        return {}
    values: dict[str, str] = {}
    for line in os_release_path.read_text(encoding="utf-8").splitlines():
        if "=" in line:
            key, value = line.split("=", 1)
            values[key.strip()] = value.strip().strip('"')
    return values


def os_distro_id(os_release: dict[str, str]) -> str:
    """Return the os-release ID, reduced to characters safe for purl namespaces and file names."""
    distro_id = DISTRO_ID_INVALID_CHARS_RE.sub("-", os_release.get("ID", "").lower())
    return distro_id or UNKNOWN_DISTRO_ID


def os_distro_qualifier(os_release: dict[str, str]) -> str:
    """Return the purl distro qualifier, e.g. debian-13, ubuntu-24.04 or debian-forky for Debian testing."""
    distro_version = os_release.get("VERSION_ID") or os_release.get("VERSION_CODENAME") or ""
    return f"{os_distro_id(os_release)}-{distro_version}" if distro_version else os_distro_id(os_release)


def components_from_dpkg(distro_id: str, distro: str) -> list[Component]:
    output = run_command(["dpkg-query", "-W", f"-f={DPKG_QUERY_FORMAT}"])
    components: list[Component] = []
    for line in output.splitlines():
        status, name, version, architecture = line.split("\t", 3)
        if status[DPKG_STATE_INDEX : DPKG_STATE_INDEX + 1] in DPKG_STATES_WITHOUT_FILES:
            continue
        components.append(
            Component(
                name=name,
                version=version,
                component_type="operating-system",
                purl=build_purl("deb", distro_id, name, version, {"arch": architecture, "distro": distro}),
                properties={"fwo:architecture": architecture},
            )
        )
    return components


def components_from_rpm(distro_id: str, distro: str) -> list[Component]:
    output = run_command(["rpm", "-qa", "--queryformat", RPM_QUERY_FORMAT])
    components: list[Component] = []
    for line in output.splitlines():
        name, epoch, version, architecture = line.split("\t", 3)
        qualifiers = {"arch": architecture, "distro": distro}
        if epoch not in RPM_EMPTY_EPOCHS:
            qualifiers["epoch"] = epoch
        components.append(
            Component(
                name=name,
                version=version,
                component_type="operating-system",
                purl=build_purl("rpm", distro_id, name, version, qualifiers),
                properties={"fwo:architecture": architecture},
            )
        )
    return components


OS_PACKAGE_BACKENDS: list[Callable[[str, str], list[Component]]] = [components_from_dpkg, components_from_rpm]


def run_os_package_backend(
    backend: Callable[[str, str], list[Component]], distro_id: str, distro: str
) -> list[Component] | None:
    """Run one package manager backend, returning None when its query tool is not installed."""
    try:
        return backend(distro_id, distro)
    except FileNotFoundError:
        return None


def components_from_os_packages(os_release: dict[str, str]) -> list[Component] | None:
    """Return the installed OS packages from the first available package manager, or None if there is none."""
    distro_id = os_distro_id(os_release)
    distro = os_distro_qualifier(os_release)
    for backend in OS_PACKAGE_BACKENDS:
        if (components := run_os_package_backend(backend, distro_id, distro)) is not None:
            return components
    return None


def os_release_properties(os_release: dict[str, str]) -> dict[str, str]:
    properties = {
        PROPERTY_REFERENCE_PLATFORM: REFERENCE_PLATFORM,
        "fwo:generator-host": platform.node(),
        "fwo:generator-system": platform.platform(),
    }
    properties.update({f"os-release:{key}": value for key, value in os_release.items()})
    return properties


def inspect_first_item(runtime: str, object_type: str, reference: str) -> JsonObject | None:
    try:
        output = run_command([runtime, object_type, "inspect", reference])
    except (FileNotFoundError, subprocess.CalledProcessError):
        return None
    items = json_list(json.loads(output))
    return json_object(items[0]) if items else None


def container_image_reference(container_item: JsonObject) -> str | None:
    """Return the image reference a container was created from (podman: ImageName, docker: Config.Image)."""
    image_name = container_item.get("ImageName")
    if isinstance(image_name, str) and image_name:
        return image_name
    config = json_object(container_item.get("Config"))
    config_image = config.get("Image") if config is not None else None
    return config_image if isinstance(config_image, str) and config_image else None


def split_image_reference(image_reference: str) -> tuple[str, str | None]:
    """Split an image reference into repository and tag, dropping any digest."""
    repository = image_reference.split("@", 1)[0]
    tag_separator = repository.rfind(":")
    if tag_separator > repository.rfind("/"):
        return repository[:tag_separator], repository[tag_separator + 1 :]
    return repository, None


def image_digest(image_item: JsonObject, repository: str) -> str:
    """Return the repository digest of an image, falling back to its local image ID."""
    digests = [str(digest) for digest in json_list(image_item.get("RepoDigests")) or [] if "@" in str(digest)]
    matching_digests = [digest for digest in digests if digest.split("@", 1)[0] == repository] or digests
    if matching_digests:
        return matching_digests[0].split("@", 1)[1]
    image_id = str(image_item.get("Id", ""))
    return image_id if not image_id or image_id.startswith("sha256:") else f"sha256:{image_id}"


def component_from_image_item(
    runtime: str, image_reference: str, image_item: JsonObject, properties: dict[str, str]
) -> Component | None:
    repository, tag = split_image_reference(image_reference)
    digest = image_digest(image_item, repository)
    if not digest:
        return None
    return Component(
        name=repository,
        version=digest,
        component_type="container",
        purl=build_purl(
            "oci", None, repository.rsplit("/", 1)[-1].lower(), digest, {"repository_url": repository, "tag": tag or ""}
        ),
        properties={"fwo:container-runtime": runtime} | properties,
    )


def component_from_container_inspect(runtime: str, image_or_container: str) -> Component | None:
    """Describe an image, or the image a container runs, as a component identified by its digest."""
    image_item = inspect_first_item(runtime, "image", image_or_container)
    if image_item is not None:
        return component_from_image_item(runtime, image_or_container, image_item, {})
    container_item = inspect_first_item(runtime, "container", image_or_container)
    if container_item is None:
        return None
    image_reference = container_image_reference(container_item)
    image_id = container_item.get("Image")
    if image_reference is None or not isinstance(image_id, str) or not image_id:
        return None
    image_item = inspect_first_item(runtime, "image", image_id)
    if image_item is None:
        return None
    return component_from_image_item(runtime, image_reference, image_item, {"fwo:container-name": image_or_container})


def source_boms(
    repo_root: Path, output_dir: Path, reference_platform: str, product_version: str | None = None
) -> list[Path]:
    properties = {PROPERTY_MODE: "source", PROPERTY_REFERENCE_PLATFORM: reference_platform}
    return [
        write_bom(
            output_dir,
            "fwo-dotnet.cdx.json",
            "Firewall Orchestrator .NET",
            components_from_csproj(repo_root),
            properties,
            product_version,
        ),
        write_bom(
            output_dir,
            "fwo-python-importer.cdx.json",
            "Firewall Orchestrator Python Importer",
            components_from_requirements(repo_root / "roles/importer/files/importer/requirements.txt", repo_root),
            properties,
            product_version,
        ),
        write_bom(
            output_dir,
            "fwo-python-scripts.cdx.json",
            "Firewall Orchestrator Python Scripts",
            components_from_requirements(repo_root / "scripts/requirements.txt", repo_root)
            + components_from_requirements(repo_root / "requirements.txt", repo_root),
            properties,
            product_version,
        ),
        write_bom(
            output_dir,
            "fwo-ansible.cdx.json",
            "Firewall Orchestrator Ansible",
            components_from_ansible_requirements(repo_root / "collections/requirements.yml", repo_root),
            properties,
            product_version,
        ),
    ]


def os_package_bom(
    output_dir: Path, os_release: dict[str, str], properties: dict[str, str], product_version: str | None = None
) -> Path | None:
    components = components_from_os_packages(os_release)
    if components is None:
        sys.stderr.write("Neither dpkg-query nor rpm is available, skipping the operating system SBOM layer.\n")
        return None
    distro_id = os_distro_id(os_release)
    return write_bom(
        output_dir,
        f"fwo-os-{distro_id}.cdx.json",
        f"Firewall Orchestrator {os_release.get('PRETTY_NAME') or distro_id} Host",
        components,
        properties,
        product_version,
    )


def installed_boms(
    output_dir: Path, reference_platform: str, container: str | None, product_version: str | None = None
) -> list[Path]:
    os_release = read_os_release()
    properties = os_release_properties(os_release) | {
        PROPERTY_MODE: "installed",
        PROPERTY_REFERENCE_PLATFORM: reference_platform,
    }
    written: list[Path] = []
    if (os_bom := os_package_bom(output_dir, os_release, properties, product_version)) is not None:
        written.append(os_bom)
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
                product_version,
            )
        )
    return written


def merge_boms(
    output_dir: Path, bom_paths: Iterable[Path], reference_platform: str, product_version: str | None = None
) -> Path:
    return write_bom(
        output_dir,
        COMBINED_BOM_FILENAME,
        "Firewall Orchestrator",
        [component for bom_path in bom_paths for component in components_from_bom_path(bom_path)],
        {PROPERTY_MODE: "combined", PROPERTY_REFERENCE_PLATFORM: reference_platform},
        product_version,
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
        properties=properties_from_bom_item(typed_item) | {PROPERTY_MERGED_FROM: bom_path.name},
        scope=str(typed_item["scope"]) if "scope" in typed_item else None,
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
    parser.add_argument("--product-version", help="Firewall Orchestrator version the SBOM describes, e.g. 9.5.10")
    parser.add_argument("--merge", action="store_true", help="Write fwo-combined.cdx.json")
    parser.add_argument("--merge-existing", action="store_true", help="Merge existing *.cdx.json files from output dir")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    details_output_dir = args.output_dir / DETAILS_DIR_NAME
    written: list[Path] = []
    if args.mode in {"source", "all"}:
        written.extend(source_boms(args.repo_root, details_output_dir, args.reference_platform, args.product_version))
    if args.mode in {"installed", "all"}:
        written.extend(
            installed_boms(details_output_dir, args.reference_platform, args.container, args.product_version)
        )
    if args.merge:
        written.append(
            merge_boms(
                args.output_dir,
                merge_input_paths(args.output_dir, written, args.merge_existing),
                args.reference_platform,
                args.product_version,
            )
        )
    for path in written:
        sys.stdout.write(f"{path}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

from __future__ import annotations

import json
import subprocess
import sys
import xml.etree.ElementTree as ET
from io import StringIO
from pathlib import Path
from typing import TYPE_CHECKING
from unittest.mock import patch

from scripts.sbom import generate_sbom

if TYPE_CHECKING:
    from collections.abc import Sequence


def test_parse_requirement_line_pinned_version() -> None:
    component = generate_sbom.parse_requirement_line("requests==2.32.0 # comment", "requirements.txt")

    assert component is not None
    assert component.name == "requests"
    assert component.version == "2.32.0"
    assert component.purl == "pkg:pypi/requests@2.32.0"


def test_parse_requirement_line_range_keeps_requirement_property() -> None:
    component = generate_sbom.parse_requirement_line("pydantic>=2.0,<3.0", "requirements.txt")

    assert component is not None
    assert component.name == "pydantic"
    assert component.version is None
    assert component.properties["fwo:requirement"] == "pydantic>=2.0,<3.0"


def test_parse_requirement_line_skips_non_packages() -> None:
    for line in ["", "# comment", "--index-url https://example.invalid", "@@@"]:
        assert generate_sbom.parse_requirement_line(line, "requirements.txt") is None


def test_components_from_ansible_requirements(tmp_path: Path) -> None:
    requirements = tmp_path / "requirements.yml"
    requirements.write_text(
        """
collections:
  - name: community.postgresql
    version: 3.10.0
  - name: ansible.posix
""",
        encoding="utf-8",
    )

    components = generate_sbom.components_from_ansible_requirements(requirements)

    assert [(component.name, component.version) for component in components] == [
        ("community.postgresql", "3.10.0"),
        ("ansible.posix", None),
    ]


def test_components_from_csproj_reads_package_references(tmp_path: Path) -> None:
    csproj = tmp_path / "roles/lib/files/FWO.Test/FWO.Test.csproj"
    csproj.parent.mkdir(parents=True)
    csproj.write_text(
        """
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
""",
        encoding="utf-8",
    )

    components = generate_sbom.components_from_csproj(tmp_path)

    assert len(components) == 1
    assert components[0].name == "Newtonsoft.Json"
    assert components[0].purl == "pkg:nuget/Newtonsoft.Json@13.0.3"


def test_component_from_package_reference_reads_child_version(tmp_path: Path) -> None:
    package_reference = ET.Element("PackageReference", {"Update": "Serilog"})
    version = ET.SubElement(package_reference, "Version")
    version.text = "4.0.0"

    component = generate_sbom.component_from_package_reference(package_reference, tmp_path / "test.csproj")

    assert component is not None
    assert component.name == "Serilog"
    assert component.version == "4.0.0"
    assert component.purl == "pkg:nuget/Serilog@4.0.0"


def test_component_from_package_reference_skips_missing_name(tmp_path: Path) -> None:
    package_reference = ET.Element("PackageReference", {"Version": "1.0.0"})

    assert generate_sbom.component_from_package_reference(package_reference, tmp_path / "test.csproj") is None


def test_component_to_cyclonedx_omits_optional_fields() -> None:
    component = generate_sbom.Component(name="package")

    assert component.to_cyclonedx() == {
        "type": "library",
        "name": "package",
        "bom-ref": "library:package:unknown",
    }


def test_run_command_returns_stdout() -> None:
    assert generate_sbom.run_command([sys.executable, "-c", "print('ok')"]) == "ok\n"


def test_components_from_requirements_handles_missing_and_markers(tmp_path: Path) -> None:
    requirements = tmp_path / "requirements.txt"
    requirements.write_text("Flask[async]===3.0.0; python_version > '3.11'\n", encoding="utf-8")

    missing_components = generate_sbom.components_from_requirements(tmp_path / "missing.txt")
    components = generate_sbom.components_from_requirements(requirements)

    assert missing_components == []
    assert len(components) == 1
    assert components[0].name == "Flask"
    assert components[0].version == "3.0.0"
    assert components[0].purl == "pkg:pypi/flask@3.0.0"


def test_components_from_ansible_requirements_handles_missing_file(tmp_path: Path) -> None:
    assert generate_sbom.components_from_ansible_requirements(tmp_path / "missing.yml") == []


def test_components_from_dpkg() -> None:
    def fake_run_command(_command: Sequence[str]) -> str:
        return "curl\t8.0.1-1\tamd64\npython3\t3.11.2-1\tall\n"

    with patch.object(generate_sbom, "run_command", fake_run_command):
        components = generate_sbom.components_from_dpkg()

    assert [(component.name, component.version) for component in components] == [
        ("curl", "8.0.1-1"),
        ("python3", "3.11.2-1"),
    ]
    assert components[0].purl == "pkg:deb/debian/curl@8.0.1-1?arch=amd64"


def test_component_from_container_inspect_prefers_image_digest() -> None:
    def fake_run_command(command: Sequence[str]) -> str:
        assert command == ["podman", "image", "inspect", "hasura/graphql-engine"]
        return json.dumps(
            [
                {
                    "Id": "sha256:local",
                    "RepoDigests": ["hasura/graphql-engine@sha256:repo"],
                }
            ]
        )

    with patch.object(generate_sbom, "run_command", fake_run_command):
        component = generate_sbom.component_from_container_inspect("podman", "hasura/graphql-engine")

    assert component is not None
    assert component.version == "sha256:repo"
    assert component.purl == "pkg:oci/hasura/graphql-engine@sha256:repo"


def test_component_from_container_inspect_falls_back_to_container() -> None:
    calls: list[list[str]] = []

    def fake_run_command(command: Sequence[str]) -> str:
        calls.append(list(command))
        if command[1] == "image":
            raise FileNotFoundError
        return json.dumps([{"Id": "sha256:container", "RepoDigests": []}])

    with patch.object(generate_sbom, "run_command", fake_run_command):
        component = generate_sbom.component_from_container_inspect("docker", "fwo-api")

    assert calls == [
        ["docker", "image", "inspect", "fwo-api"],
        ["docker", "container", "inspect", "fwo-api"],
    ]
    assert component is not None
    assert component.version == "container"


def test_component_from_container_inspect_skips_invalid_payload() -> None:
    for payload in ["[]", "[{}]", "{}"]:

        def fake_run_command(_command: Sequence[str], response: str = payload) -> str:
            return response

        with patch.object(generate_sbom, "run_command", fake_run_command):
            assert generate_sbom.component_from_container_inspect("podman", "missing") is None


def test_component_from_container_inspect_returns_none_when_runtime_fails() -> None:
    def fake_run_command(_command: Sequence[str]) -> str:
        raise FileNotFoundError

    with patch.object(generate_sbom, "run_command", fake_run_command):
        assert generate_sbom.component_from_container_inspect("podman", "missing") is None


def test_source_boms_writes_all_source_layers(tmp_path: Path) -> None:
    repo_root = tmp_path / "repo"
    output_dir = tmp_path / "out"
    (repo_root / "roles/lib/files/FWO.Test").mkdir(parents=True)
    (repo_root / "roles/importer/files/importer").mkdir(parents=True)
    (repo_root / "scripts").mkdir(parents=True)
    (repo_root / "collections").mkdir(parents=True)
    (repo_root / "roles/lib/files/FWO.Test/FWO.Test.csproj").write_text(
        '<Project><ItemGroup><PackageReference Include="Newtonsoft.Json" Version="13.0.3" /></ItemGroup></Project>',
        encoding="utf-8",
    )
    (repo_root / "roles/importer/files/importer/requirements.txt").write_text(
        "requests==2.32.0\n",
        encoding="utf-8",
    )
    (repo_root / "scripts/requirements.txt").write_text("PyYAML==6.0.2\n", encoding="utf-8")
    (repo_root / "collections/requirements.yml").write_text(
        "- name: community.postgresql\n  version: 3.10.0\n",
        encoding="utf-8",
    )

    paths = generate_sbom.source_boms(repo_root, output_dir, "debian-testing")

    assert [path.name for path in paths] == [
        "fwo-dotnet.cdx.json",
        "fwo-python-importer.cdx.json",
        "fwo-python-scripts.cdx.json",
        "fwo-ansible.cdx.json",
    ]
    assert all(path.exists() for path in paths)


def test_installed_boms_writes_os_and_container_layers(tmp_path: Path) -> None:
    def fake_component_from_container_inspect(runtime: str, container: str) -> generate_sbom.Component | None:
        return generate_sbom.Component(name=container, version=runtime) if runtime == "podman" else None

    with (
        patch.object(
            generate_sbom, "components_from_dpkg", lambda: [generate_sbom.Component(name="curl", version="8.0.1")]
        ),
        patch.object(generate_sbom, "component_from_container_inspect", fake_component_from_container_inspect),
    ):
        paths = generate_sbom.installed_boms(tmp_path, "debian-testing", "hasura")

    assert [path.name for path in paths] == [
        "fwo-os-debian-testing.cdx.json",
        "fwo-containers.cdx.json",
    ]


def test_installed_boms_skips_container_layer_without_components(tmp_path: Path) -> None:
    def fake_component_from_container_inspect(_runtime: str, _container: str) -> None:
        return None

    with (
        patch.object(generate_sbom, "components_from_dpkg", list),
        patch.object(generate_sbom, "component_from_container_inspect", fake_component_from_container_inspect),
    ):
        paths = generate_sbom.installed_boms(tmp_path, "debian-testing", None)

    assert [path.name for path in paths] == ["fwo-os-debian-testing.cdx.json"]


def test_write_and_merge_boms(tmp_path: Path) -> None:
    first = generate_sbom.write_bom(
        tmp_path,
        "first.cdx.json",
        "first",
        [generate_sbom.Component(name="requests", version="2.32.0", purl="pkg:pypi/requests@2.32.0")],
        {"fwo:mode": "test"},
    )
    second = generate_sbom.write_bom(
        tmp_path,
        "second.cdx.json",
        "second",
        [generate_sbom.Component(name="requests", version="2.32.0", purl="pkg:pypi/requests@2.32.0")],
        {"fwo:mode": "test"},
    )

    combined = generate_sbom.merge_boms(tmp_path, [first, second], "debian-testing")
    combined_data = json.loads(combined.read_text(encoding="utf-8"))

    assert combined_data["metadata"]["component"]["name"] == "Firewall Orchestrator"
    assert len(combined_data["components"]) == 1


def test_merge_input_paths_can_include_existing_output_files(tmp_path: Path) -> None:
    details_dir = tmp_path / "fwo-sbom-details"
    details_dir.mkdir()
    existing = details_dir / "fwo-dotnet.cdx.json"
    combined = tmp_path / "fwo-combined.cdx.json"
    written = details_dir / "fwo-os-debian-testing.cdx.json"
    existing.write_text("{}", encoding="utf-8")
    combined.write_text("{}", encoding="utf-8")
    written.write_text("{}", encoding="utf-8")

    paths = generate_sbom.merge_input_paths(tmp_path, [written], include_existing=True)

    assert [path.name for path in paths] == [
        "fwo-os-debian-testing.cdx.json",
        "fwo-dotnet.cdx.json",
    ]


def test_merge_input_paths_can_use_only_currently_written_files(tmp_path: Path) -> None:
    details_dir = tmp_path / "fwo-sbom-details"
    details_dir.mkdir()
    existing = details_dir / "fwo-dotnet.cdx.json"
    written = details_dir / "fwo-os-debian-testing.cdx.json"
    existing.write_text("{}", encoding="utf-8")
    written.write_text("{}", encoding="utf-8")

    paths = generate_sbom.merge_input_paths(tmp_path, [written], include_existing=False)

    assert paths == [written]


def test_components_from_bom_path_handles_invalid_boms(tmp_path: Path) -> None:
    missing = tmp_path / "missing.cdx.json"
    non_object = tmp_path / "non-object.cdx.json"
    no_components = tmp_path / "no-components.cdx.json"
    non_object.write_text("[]", encoding="utf-8")
    no_components.write_text(json.dumps({"components": "invalid"}), encoding="utf-8")

    assert generate_sbom.components_from_bom_path(missing) == []
    assert generate_sbom.components_from_bom_path(non_object) == []
    assert generate_sbom.components_from_bom_path(no_components) == []


def test_components_from_bom_path_reads_component_properties(tmp_path: Path) -> None:
    bom_path = tmp_path / "input.cdx.json"
    bom_path.write_text(
        json.dumps(
            {
                "components": [
                    "invalid",
                    {"type": "library"},
                    {
                        "type": "library",
                        "name": "requests",
                        "version": "2.32.0",
                        "purl": "pkg:pypi/requests@2.32.0",
                        "properties": [
                            {"name": "language", "value": "python"},
                            {"name": None, "value": "ignored"},
                            "invalid",
                        ],
                    },
                ]
            }
        ),
        encoding="utf-8",
    )

    components = generate_sbom.components_from_bom_path(bom_path)

    assert len(components) == 1
    assert components[0].name == "requests"
    assert components[0].properties == {
        "language": "python",
        "fwo:merged-from": str(bom_path),
    }


def test_properties_from_bom_item_skips_non_list_properties() -> None:
    assert generate_sbom.properties_from_bom_item({"properties": "invalid"}) == {}


def test_parse_args_reads_cli_options(tmp_path: Path) -> None:
    with patch.object(
        sys,
        "argv",
        [
            "generate_sbom.py",
            "--mode",
            "all",
            "--repo-root",
            str(tmp_path),
            "--output-dir",
            str(tmp_path / "out"),
            "--reference-platform",
            "ubuntu-2404",
            "--container",
            "hasura",
            "--merge",
            "--merge-existing",
        ],
    ):
        args = generate_sbom.parse_args()

    assert args.mode == "all"
    assert args.repo_root == tmp_path
    assert args.output_dir == tmp_path / "out"
    assert args.reference_platform == "ubuntu-2404"
    assert args.container == "hasura"
    assert args.merge is True
    assert args.merge_existing is True


def test_main_writes_selected_boms(tmp_path: Path) -> None:
    source_path = tmp_path / "fwo-sbom-details/source.cdx.json"
    combined_path = tmp_path / "combined.cdx.json"

    def fake_source_boms(_repo: Path, _output: Path, _platform: str) -> list[Path]:
        return [source_path]

    def fake_merge_boms(_output: Path, _paths: list[Path], _platform: str) -> Path:
        return combined_path

    def fake_merge_input_paths(_output: Path, paths: list[Path], _include_existing: bool) -> list[Path]:
        return list(paths)

    stdout = StringIO()
    with (
        patch.object(sys, "argv", ["generate_sbom.py", "--mode", "source", "--output-dir", str(tmp_path), "--merge"]),
        patch.object(sys, "stdout", stdout),
        patch.object(generate_sbom, "source_boms", fake_source_boms),
        patch.object(generate_sbom, "merge_boms", fake_merge_boms),
        patch.object(generate_sbom, "merge_input_paths", fake_merge_input_paths),
    ):
        assert generate_sbom.main() == 0

    assert stdout.getvalue() == f"{source_path}\n{combined_path}\n"


def test_main_writes_details_under_output_dir(tmp_path: Path) -> None:
    stdout = StringIO()
    with (
        patch.object(sys, "stdout", stdout),
        patch.object(
            sys,
            "argv",
            [
                "generate_sbom.py",
                "--mode",
                "source",
                "--repo-root",
                str(tmp_path),
                "--output-dir",
                str(tmp_path),
                "--merge",
            ],
        ),
    ):
        assert generate_sbom.main() == 0

    written_paths = {Path(line) for line in stdout.getvalue().splitlines()}
    assert tmp_path / "fwo-combined.cdx.json" in written_paths
    assert tmp_path / "fwo-sbom-details/fwo-dotnet.cdx.json" in written_paths
    assert tmp_path / "fwo-sbom-details/fwo-python-importer.cdx.json" in written_paths


def test_container_inspect_returns_none_when_inspect_commands_fail() -> None:
    def fake_run_command(_command: Sequence[str]) -> str:
        raise subprocess.CalledProcessError(1, ["podman"])

    with patch.object(generate_sbom, "run_command", fake_run_command):
        assert generate_sbom.component_from_container_inspect("podman", "missing") is None

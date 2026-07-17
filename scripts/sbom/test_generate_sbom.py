from __future__ import annotations

import json
from typing import TYPE_CHECKING

from scripts.sbom import generate_sbom

if TYPE_CHECKING:
    from pathlib import Path


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

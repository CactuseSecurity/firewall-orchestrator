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

    components = generate_sbom.components_from_ansible_requirements(requirements, tmp_path)

    assert [(component.name, component.version) for component in components] == [
        ("community.postgresql", "3.10.0"),
        ("ansible.posix", None),
    ]
    assert {component.properties["fwo:source"] for component in components} == {"requirements.yml"}


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
    assert components[0].properties["fwo:source"] == "roles/lib/files/FWO.Test/FWO.Test.csproj"


def test_component_from_package_reference_reads_child_version() -> None:
    package_reference = ET.Element("PackageReference", {"Update": "Serilog"})
    version = ET.SubElement(package_reference, "Version")
    version.text = "4.0.0"

    component = generate_sbom.component_from_package_reference(package_reference, "test.csproj")

    assert component is not None
    assert component.name == "Serilog"
    assert component.version == "4.0.0"
    assert component.purl == "pkg:nuget/Serilog@4.0.0"


def test_component_from_package_reference_skips_missing_name() -> None:
    package_reference = ET.Element("PackageReference", {"Version": "1.0.0"})

    assert generate_sbom.component_from_package_reference(package_reference, "test.csproj") is None


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

    missing_components = generate_sbom.components_from_requirements(tmp_path / "missing.txt", tmp_path)
    components = generate_sbom.components_from_requirements(requirements, tmp_path)

    assert missing_components == []
    assert len(components) == 1
    assert components[0].name == "Flask"
    assert components[0].version == "3.0.0"
    assert components[0].purl == "pkg:pypi/flask@3.0.0"
    assert components[0].properties == {
        "fwo:source": "requirements.txt",
        "fwo:marker": "python_version > '3.11'",
        "fwo:requirement": "Flask[async]===3.0.0",
    }
    assert components[0].scope == "optional"


def test_parse_requirement_line_keeps_mutually_exclusive_marker_requirements_apart() -> None:
    lines = [
        "ansible==10.7.0; python_version < '3.11'",
        "ansible==12.3.0; python_version >= '3.11'",
        "cryptography==50.0.2",
    ]

    components = [generate_sbom.parse_requirement_line(line, "requirements.txt") for line in lines]

    assert [
        (component.version, component.scope, component.properties.get("fwo:marker"))
        for component in components
        if component is not None
    ] == [
        ("10.7.0", "optional", "python_version < '3.11'"),
        ("12.3.0", "optional", "python_version >= '3.11'"),
        ("50.0.2", None, None),
    ]
    assert components[0] is not None
    assert components[0].to_cyclonedx()["scope"] == "optional"
    assert components[2] is not None
    assert "scope" not in components[2].to_cyclonedx()


def test_components_from_ansible_requirements_handles_missing_file(tmp_path: Path) -> None:
    assert generate_sbom.components_from_ansible_requirements(tmp_path / "missing.yml", tmp_path) == []


def test_components_from_dpkg() -> None:
    def fake_run_command(_command: Sequence[str]) -> str:
        return "ii \tcurl\t8.0.1-1\tamd64\nii \tpython3\t3.11.2-1\tall\nhi \tlibstdc++6\t1:14.2.0-19\tamd64\n"

    with patch.object(generate_sbom, "run_command", fake_run_command):
        components = generate_sbom.components_from_dpkg("ubuntu", "ubuntu-24.04")

    assert [(component.name, component.version) for component in components] == [
        ("curl", "8.0.1-1"),
        ("python3", "3.11.2-1"),
        ("libstdc++6", "1:14.2.0-19"),
    ]
    assert components[0].purl == "pkg:deb/ubuntu/curl@8.0.1-1?arch=amd64&distro=ubuntu-24.04"
    assert components[2].purl == "pkg:deb/ubuntu/libstdc%2B%2B6@1:14.2.0-19?arch=amd64&distro=ubuntu-24.04"


def test_components_from_dpkg_skips_only_packages_without_files_on_disk() -> None:
    def fake_run_command(_command: Sequence[str]) -> str:
        return (
            "rc \told-kernel\t6.1.0-1\tamd64\n"
            "un \tpurged\t\tamd64\n"
            "iU \tunpacked\t1.0\tamd64\n"
            "iF \thalf-configured\t1.0\tamd64\n"
            "iHR\thalf-installed\t1.0\tamd64\n"
            "iW \ttriggers-awaited\t1.0\tamd64\n"
            "it \ttriggers-pending\t1.0\tamd64\n"
            "ii \tcurl\t8.0.1-1\tamd64\n"
        )

    with patch.object(generate_sbom, "run_command", fake_run_command):
        components = generate_sbom.components_from_dpkg("debian", "debian-13")

    assert [component.name for component in components] == [
        "unpacked",
        "half-configured",
        "half-installed",
        "triggers-awaited",
        "triggers-pending",
        "curl",
    ]


def test_components_from_dpkg_keeps_arch_out_of_multiarch_package_names() -> None:
    def fake_run_command(command: Sequence[str]) -> str:
        assert "${Package}" in command[2]
        assert "${binary:Package}" not in command[2]
        return "ii \tlibc6\t2.41-12\tamd64\nii \tlibc6\t2.41-12\ti386\n"

    with patch.object(generate_sbom, "run_command", fake_run_command):
        components = generate_sbom.components_from_dpkg("debian", "debian-13")

    assert [component.purl for component in components] == [
        "pkg:deb/debian/libc6@2.41-12?arch=amd64&distro=debian-13",
        "pkg:deb/debian/libc6@2.41-12?arch=i386&distro=debian-13",
    ]


def test_components_from_rpm_reads_epoch_as_qualifier() -> None:
    def fake_run_command(command: Sequence[str]) -> str:
        assert command[:2] == ["rpm", "-qa"]
        return "bash\t0\t5.1.8-9.el9\tx86_64\nopenssl\t1\t3.2.2-6.el9_5\tx86_64\ngpg-pubkey\t(none)\t8483c65d-5ccc5b19\t(none)\n"

    with patch.object(generate_sbom, "run_command", fake_run_command):
        components = generate_sbom.components_from_rpm("rocky", "rocky-9.6")

    assert [component.purl for component in components] == [
        "pkg:rpm/rocky/bash@5.1.8-9.el9?arch=x86_64&distro=rocky-9.6",
        "pkg:rpm/rocky/openssl@3.2.2-6.el9_5?arch=x86_64&distro=rocky-9.6&epoch=1",
        "pkg:rpm/rocky/gpg-pubkey@8483c65d-5ccc5b19?arch=%28none%29&distro=rocky-9.6",
    ]


def test_components_from_os_packages_falls_back_to_rpm() -> None:
    calls: list[str] = []

    def fake_run_command(command: Sequence[str]) -> str:
        calls.append(command[0])
        if command[0] == "dpkg-query":
            raise FileNotFoundError
        return "bash\t0\t5.1.8-9.el9\tx86_64\n"

    with patch.object(generate_sbom, "run_command", fake_run_command):
        components = generate_sbom.components_from_os_packages({"ID": "rhel", "VERSION_ID": "9.6"})

    assert calls == ["dpkg-query", "rpm"]
    assert components is not None
    assert components[0].purl == "pkg:rpm/rhel/bash@5.1.8-9.el9?arch=x86_64&distro=rhel-9.6"


def test_components_from_os_packages_returns_none_without_package_manager() -> None:
    def fake_run_command(_command: Sequence[str]) -> str:
        raise FileNotFoundError

    with patch.object(generate_sbom, "run_command", fake_run_command):
        assert generate_sbom.components_from_os_packages({"ID": "alpine"}) is None


def test_os_package_bom_skips_layer_without_package_manager(tmp_path: Path) -> None:
    def no_os_packages(_os_release: dict[str, str]) -> list[generate_sbom.Component] | None:
        return None

    stderr = StringIO()
    with (
        patch.object(generate_sbom, "components_from_os_packages", no_os_packages),
        patch.object(sys, "stderr", stderr),
    ):
        assert generate_sbom.os_package_bom(tmp_path, {"ID": "alpine"}, {}) is None

    assert "skipping the operating system SBOM layer" in stderr.getvalue()
    assert list(tmp_path.iterdir()) == []


def test_os_package_bom_names_file_after_distribution(tmp_path: Path) -> None:
    def curl_os_package(_os_release: dict[str, str]) -> list[generate_sbom.Component]:
        return [generate_sbom.Component(name="curl")]

    os_release = {"ID": "ubuntu", "VERSION_ID": "24.04", "PRETTY_NAME": "Ubuntu 24.04.3 LTS"}
    with patch.object(generate_sbom, "components_from_os_packages", curl_os_package):
        path = generate_sbom.os_package_bom(tmp_path, os_release, {})

    assert path is not None
    assert path == tmp_path / "fwo-os-ubuntu.cdx.json"
    bom = json.loads(path.read_text(encoding="utf-8"))
    assert bom["metadata"]["component"]["name"] == "Firewall Orchestrator Ubuntu 24.04.3 LTS Host"


def test_os_distro_identity_from_os_release() -> None:
    assert generate_sbom.os_distro_id({"ID": "Debian"}) == "debian"
    assert generate_sbom.os_distro_id({"ID": "opensuse leap"}) == "opensuse-leap"
    assert generate_sbom.os_distro_id({}) == "unknown"
    assert generate_sbom.os_distro_qualifier({"ID": "debian", "VERSION_ID": "13"}) == "debian-13"
    assert generate_sbom.os_distro_qualifier({"ID": "debian", "VERSION_CODENAME": "forky"}) == "debian-forky"
    assert generate_sbom.os_distro_qualifier({"ID": "debian"}) == "debian"


def test_read_os_release_and_properties(tmp_path: Path) -> None:
    os_release_path = tmp_path / "os-release"
    os_release_path.write_text('ID=rocky\nVERSION_ID="9.6"\n\nPRETTY_NAME="Rocky Linux 9.6"\n', encoding="utf-8")

    os_release = generate_sbom.read_os_release(os_release_path)
    properties = generate_sbom.os_release_properties(os_release)

    assert os_release == {"ID": "rocky", "VERSION_ID": "9.6", "PRETTY_NAME": "Rocky Linux 9.6"}
    assert generate_sbom.read_os_release(tmp_path / "missing") == {}
    assert properties["os-release:VERSION_ID"] == "9.6"
    assert properties["fwo:reference-platform"] == "debian-testing"


def test_build_purl_encodes_parts_and_skips_empty_qualifiers() -> None:
    assert (
        generate_sbom.build_purl("deb", "debian", "g++", "4:14.2.0-1", {"arch": "amd64", "distro": ""})
        == "pkg:deb/debian/g%2B%2B@4:14.2.0-1?arch=amd64"
    )
    assert generate_sbom.build_purl("oci", None, "graphql-engine", None) == "pkg:oci/graphql-engine"


def test_component_from_container_inspect_prefers_image_digest() -> None:
    def fake_run_command(command: Sequence[str]) -> str:
        assert command == ["podman", "image", "inspect", "hasura/graphql-engine:v2.48.3"]
        return json.dumps(
            [
                {
                    "Id": "sha256:local",
                    "RepoDigests": ["mirror/graphql-engine@sha256:mirror", "hasura/graphql-engine@sha256:repo"],
                }
            ]
        )

    with patch.object(generate_sbom, "run_command", fake_run_command):
        component = generate_sbom.component_from_container_inspect("podman", "hasura/graphql-engine:v2.48.3")

    assert component is not None
    assert component.name == "hasura/graphql-engine"
    assert component.version == "sha256:repo"
    assert component.purl == "pkg:oci/graphql-engine@sha256:repo?repository_url=hasura/graphql-engine&tag=v2.48.3"


def test_component_from_container_inspect_resolves_image_of_podman_container() -> None:
    calls: list[list[str]] = []

    def fake_run_command(command: Sequence[str]) -> str:
        calls.append(list(command))
        if command[1:] == ["image", "inspect", "fwo-api"]:
            raise subprocess.CalledProcessError(125, list(command))
        if command[1] == "container":
            return json.dumps(
                [{"Id": "containerid", "Image": "imageid", "ImageName": "docker.io/hasura/graphql-engine:v2.48.3"}]
            )
        return json.dumps([{"Id": "imageid", "RepoDigests": ["docker.io/hasura/graphql-engine@sha256:digest"]}])

    with patch.object(generate_sbom, "run_command", fake_run_command):
        component = generate_sbom.component_from_container_inspect("podman", "fwo-api")

    assert calls == [
        ["podman", "image", "inspect", "fwo-api"],
        ["podman", "container", "inspect", "fwo-api"],
        ["podman", "image", "inspect", "imageid"],
    ]
    assert component is not None
    assert component.name == "docker.io/hasura/graphql-engine"
    assert component.version == "sha256:digest"
    assert component.purl == (
        "pkg:oci/graphql-engine@sha256:digest?repository_url=docker.io/hasura/graphql-engine&tag=v2.48.3"
    )
    assert component.properties == {"fwo:container-runtime": "podman", "fwo:container-name": "fwo-api"}


def test_component_from_container_inspect_resolves_docker_config_image_without_digest() -> None:
    def fake_run_command(command: Sequence[str]) -> str:
        if command[1:] == ["image", "inspect", "fwo-api"]:
            raise FileNotFoundError
        if command[1] == "container":
            return json.dumps([{"Image": "sha256:imageid", "Config": {"Image": "localhost:5000/graphql-engine"}}])
        return json.dumps([{"Id": "imageid", "RepoDigests": []}])

    with patch.object(generate_sbom, "run_command", fake_run_command):
        component = generate_sbom.component_from_container_inspect("docker", "fwo-api")

    assert component is not None
    assert component.name == "localhost:5000/graphql-engine"
    assert component.version == "sha256:imageid"
    assert component.purl == "pkg:oci/graphql-engine@sha256:imageid?repository_url=localhost:5000/graphql-engine"


def test_component_from_container_inspect_skips_container_without_image() -> None:
    def fake_run_command(command: Sequence[str]) -> str:
        if command[1:] == ["container", "inspect", "fwo-api"]:
            return json.dumps([{"Image": "imageid"}])
        raise subprocess.CalledProcessError(125, list(command))

    with patch.object(generate_sbom, "run_command", fake_run_command):
        assert generate_sbom.component_from_container_inspect("podman", "fwo-api") is None


def test_component_from_container_inspect_skips_unknown_container_image() -> None:
    def fake_run_command(command: Sequence[str]) -> str:
        if command[1:] == ["container", "inspect", "fwo-api"]:
            return json.dumps([{"Image": "imageid", "ImageName": "hasura/graphql-engine:v2"}])
        raise subprocess.CalledProcessError(125, list(command))

    with patch.object(generate_sbom, "run_command", fake_run_command):
        assert generate_sbom.component_from_container_inspect("podman", "fwo-api") is None


def test_split_image_reference() -> None:
    assert generate_sbom.split_image_reference("hasura/graphql-engine:v2@sha256:abc") == (
        "hasura/graphql-engine",
        "v2",
    )
    assert generate_sbom.split_image_reference("localhost:5000/graphql-engine") == (
        "localhost:5000/graphql-engine",
        None,
    )


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

    paths = generate_sbom.source_boms(repo_root, output_dir, "debian-testing", "9.5.10")

    assert [path.name for path in paths] == [
        "fwo-dotnet.cdx.json",
        "fwo-python-importer.cdx.json",
        "fwo-python-scripts.cdx.json",
        "fwo-ansible.cdx.json",
    ]
    assert all(path.exists() for path in paths)
    assert all(
        json.loads(path.read_text(encoding="utf-8"))["metadata"]["component"]["version"] == "9.5.10" for path in paths
    )
    sources = {
        prop["value"]
        for path in paths
        for component in json.loads(path.read_text(encoding="utf-8"))["components"]
        for prop in component["properties"]
        if prop["name"] == "fwo:source"
    }
    assert sources == {
        "roles/lib/files/FWO.Test/FWO.Test.csproj",
        "roles/importer/files/importer/requirements.txt",
        "scripts/requirements.txt",
        "collections/requirements.yml",
    }


def test_installed_boms_writes_os_and_container_layers(tmp_path: Path) -> None:
    def curl_os_package(_os_release: dict[str, str]) -> list[generate_sbom.Component]:
        return [generate_sbom.Component(name="curl", version="8.0.1")]

    def fake_component_from_container_inspect(runtime: str, container: str) -> generate_sbom.Component | None:
        return generate_sbom.Component(name=container, version=runtime) if runtime == "podman" else None

    with (
        patch.object(generate_sbom, "read_os_release", lambda: {"ID": "debian", "VERSION_CODENAME": "forky"}),
        patch.object(
            generate_sbom,
            "components_from_os_packages",
            curl_os_package,
        ),
        patch.object(generate_sbom, "component_from_container_inspect", fake_component_from_container_inspect),
    ):
        paths = generate_sbom.installed_boms(tmp_path, "debian-testing", "hasura", "9.5.10")

    assert [path.name for path in paths] == [
        "fwo-os-debian.cdx.json",
        "fwo-containers.cdx.json",
    ]
    assert all(
        json.loads(path.read_text(encoding="utf-8"))["metadata"]["component"]["version"] == "9.5.10" for path in paths
    )


def test_installed_boms_skips_container_layer_without_components(tmp_path: Path) -> None:
    def no_os_packages(_os_release: dict[str, str]) -> list[generate_sbom.Component]:
        return []

    def fake_component_from_container_inspect(_runtime: str, _container: str) -> None:
        return None

    with (
        patch.object(generate_sbom, "read_os_release", lambda: {"ID": "debian"}),
        patch.object(generate_sbom, "components_from_os_packages", no_os_packages),
        patch.object(generate_sbom, "component_from_container_inspect", fake_component_from_container_inspect),
    ):
        paths = generate_sbom.installed_boms(tmp_path, "debian-testing", None)

    assert [path.name for path in paths] == ["fwo-os-debian.cdx.json"]


def test_installed_boms_continues_without_os_layer(tmp_path: Path) -> None:
    def no_os_packages(_os_release: dict[str, str]) -> list[generate_sbom.Component] | None:
        return None

    with (
        patch.object(generate_sbom, "read_os_release", dict),
        patch.object(generate_sbom, "components_from_os_packages", no_os_packages),
        patch.object(sys, "stderr", StringIO()),
    ):
        assert generate_sbom.installed_boms(tmp_path, "debian-testing", None) == []


def test_write_bom_deduplicates_bom_refs_and_keeps_sources(tmp_path: Path) -> None:
    components = [
        generate_sbom.Component(
            name="Serilog", version="4.0.0", purl="pkg:nuget/Serilog@4.0.0", properties={"fwo:source": "b.csproj"}
        ),
        generate_sbom.Component(
            name="Serilog", version="4.0.0", purl="pkg:nuget/Serilog@4.0.0", properties={"fwo:source": "a.csproj"}
        ),
        generate_sbom.Component(name="PyYAML", purl="pkg:pypi/pyyaml", properties={"fwo:source": "a.txt"}),
        generate_sbom.Component(name="pyyaml", purl="pkg:pypi/pyyaml", properties={"fwo:source": "a.txt"}),
        generate_sbom.Component(name="unpinned"),
        generate_sbom.Component(name="unpinned"),
    ]

    bom_path = generate_sbom.write_bom(tmp_path, "dedup.cdx.json", "dedup", components, {})
    bom_components = json.loads(bom_path.read_text(encoding="utf-8"))["components"]

    assert [component["bom-ref"] for component in bom_components] == [
        "pkg:pypi/pyyaml",
        "pkg:nuget/Serilog@4.0.0",
        "library:unpinned:unknown",
    ]
    assert bom_components[1]["properties"] == [{"name": "fwo:source", "value": "a.csproj; b.csproj"}]
    assert bom_components[0]["properties"] == [{"name": "fwo:source", "value": "a.txt"}]


def test_write_bom_deduplication_keeps_scope_only_if_all_occurrences_agree(tmp_path: Path) -> None:
    components = [
        generate_sbom.Component(name="ansible", version="12.3.0", purl="pkg:pypi/ansible@12.3.0", scope="optional"),
        generate_sbom.Component(name="ansible", version="12.3.0", purl="pkg:pypi/ansible@12.3.0", scope="optional"),
        generate_sbom.Component(name="requests", version="2.34.2", purl="pkg:pypi/requests@2.34.2", scope="optional"),
        generate_sbom.Component(name="requests", version="2.34.2", purl="pkg:pypi/requests@2.34.2"),
    ]

    bom_path = generate_sbom.write_bom(tmp_path, "scope.cdx.json", "scope", components, {})
    bom_components = json.loads(bom_path.read_text(encoding="utf-8"))["components"]

    assert [(component["name"], component.get("scope")) for component in bom_components] == [
        ("ansible", "optional"),
        ("requests", None),
    ]


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

    combined = generate_sbom.merge_boms(tmp_path, [first, second], "debian-testing", "9.5.10")
    combined_data = json.loads(combined.read_text(encoding="utf-8"))

    assert combined_data["metadata"]["component"] == {
        "type": "application",
        "name": "Firewall Orchestrator",
        "version": "9.5.10",
        "bom-ref": "application:Firewall Orchestrator:9.5.10",
    }
    assert len(combined_data["components"]) == 1


def test_write_bom_omits_unknown_product_version(tmp_path: Path) -> None:
    bom_path = generate_sbom.write_bom(tmp_path, "unversioned.cdx.json", "unversioned", [], {})

    assert json.loads(bom_path.read_text(encoding="utf-8"))["metadata"]["component"] == {
        "type": "application",
        "name": "unversioned",
        "bom-ref": "application:unversioned:unknown",
    }


def test_merge_input_paths_can_include_existing_output_files(tmp_path: Path) -> None:
    details_dir = tmp_path / "fwo-sbom-details"
    details_dir.mkdir()
    existing = details_dir / "fwo-dotnet.cdx.json"
    combined = tmp_path / "fwo-combined.cdx.json"
    written = details_dir / "fwo-os-debian.cdx.json"
    existing.write_text("{}", encoding="utf-8")
    combined.write_text("{}", encoding="utf-8")
    written.write_text("{}", encoding="utf-8")

    paths = generate_sbom.merge_input_paths(tmp_path, [written], include_existing=True)

    assert [path.name for path in paths] == [
        "fwo-os-debian.cdx.json",
        "fwo-dotnet.cdx.json",
    ]


def test_merge_input_paths_can_use_only_currently_written_files(tmp_path: Path) -> None:
    details_dir = tmp_path / "fwo-sbom-details"
    details_dir.mkdir()
    existing = details_dir / "fwo-dotnet.cdx.json"
    written = details_dir / "fwo-os-debian.cdx.json"
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
                        "scope": "optional",
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
        "fwo:merged-from": "input.cdx.json",
    }
    assert components[0].scope == "optional"


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
            "--product-version",
            "9.5.10",
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
    assert args.product_version == "9.5.10"
    assert args.merge is True
    assert args.merge_existing is True


def test_main_writes_selected_boms(tmp_path: Path) -> None:
    source_path = tmp_path / "fwo-sbom-details/source.cdx.json"
    combined_path = tmp_path / "combined.cdx.json"

    product_versions: list[str | None] = []

    def fake_source_boms(_repo: Path, _output: Path, _platform: str, product_version: str | None) -> list[Path]:
        product_versions.append(product_version)
        return [source_path]

    def fake_merge_boms(_output: Path, _paths: list[Path], _platform: str, product_version: str | None) -> Path:
        product_versions.append(product_version)
        return combined_path

    def fake_merge_input_paths(_output: Path, paths: list[Path], _include_existing: bool) -> list[Path]:
        return list(paths)

    stdout = StringIO()
    with (
        patch.object(
            sys,
            "argv",
            [
                "generate_sbom.py",
                "--mode",
                "source",
                "--output-dir",
                str(tmp_path),
                "--product-version",
                "9.5.10",
                "--merge",
            ],
        ),
        patch.object(sys, "stdout", stdout),
        patch.object(generate_sbom, "source_boms", fake_source_boms),
        patch.object(generate_sbom, "merge_boms", fake_merge_boms),
        patch.object(generate_sbom, "merge_input_paths", fake_merge_input_paths),
    ):
        assert generate_sbom.main() == 0

    assert stdout.getvalue() == f"{source_path}\n{combined_path}\n"
    assert product_versions == ["9.5.10", "9.5.10"]


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

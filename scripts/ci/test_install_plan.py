"""Unit tests for the test plan of the FWO Test Install workflow, see scripts/ci/install_plan.py."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import pytest
import yaml

from scripts.ci.install_plan import (
    ALL_INSTALL_OS,
    ALL_PYTHON_VERSIONS,
    ANSIBLE_CORE_218_OS,
    DEFAULT_INSTALL_OS,
    INSTALL_LABELS,
    PULL_REQUEST_PYTHON_VERSIONS,
    REQUIRED_INSTALL_LABEL,
    Plan,
    RunEvent,
    ansible_files,
    is_merged_run,
    main,
    read_changed_files,
    render_outputs,
    render_summary,
    select_plan,
)

REPOSITORY_ROOT = Path(__file__).parents[2]
WORKFLOW_PATH = REPOSITORY_ROOT / ".github" / "workflows" / "test-install.yml"
ROLE_DIRECTORIES_WITH_ANSIBLE_CODE = ("tasks", "handlers", "templates", "defaults", "vars", "meta")
ANSIBLE_PATHS = (
    "site.yml",
    "ansible.cfg",
    "requirements.txt",
    "inventory/group_vars/all.yml",
    "collections/requirements.yml",
    "callback_plugins/fwo_role_durations.py",
    "scripts/run-playbook-with-sudo.sh",
    "scripts/install-ansible-from-venv.sh",
    "scripts/re-activate-ui.yml",
    "roles/lib/tasks/main.yml",
    "roles/ui/templates/fworch-ui.service.j2",
    "roles/api/handlers/main.yml",
    "roles/database/defaults/main.yml",
    "roles/common/vars/main.yml",
    "roles/common/meta/main.yml",
    ".github/workflows/test-install.yml",
    "scripts/ci/install_plan.py",
)
NON_ANSIBLE_PATHS = (
    "roles/ui/files/FWO.UI/Pages/Index.razor",
    "roles/importer/files/importer/requirements.txt",
    "roles/database/files/upgrade/9.9.3.sql",
    "documentation/revision-history.md",
    "scripts/customizing/test_customizing_api.py",
    "scripts/tests/test-sample-auth-data.sh",
    "my-site.yml",
    "roles/tasks/main.yml",
    ".github/workflows/sonarcloud-pr.yml",
)


def pull_request(changed_files: list[str] | None = None, head_ref: str = "feature/x") -> RunEvent:
    """Build the event of a pull request into develop."""
    return RunEvent(
        event_name="pull_request",
        ref="refs/pull/1/merge",
        head_ref=head_ref,
        changed_files=changed_files or [],
    )


def workflow_jobs() -> dict[str, Any]:
    """Return the jobs of the test install workflow as parsed YAML."""
    workflow: dict[str, Any] = yaml.safe_load(WORKFLOW_PATH.read_text(encoding="utf-8"))
    jobs: dict[str, Any] = workflow["jobs"]
    return jobs


class TestMergedRun:
    @pytest.mark.parametrize("ref", ["refs/heads/develop", "refs/heads/main"])
    def test_push_to_develop_or_main_is_a_merged_run(self, ref: str) -> None:
        assert is_merged_run(RunEvent(event_name="push", ref=ref))

    def test_push_to_another_branch_is_no_merged_run(self) -> None:
        assert not is_merged_run(RunEvent(event_name="push", ref="refs/heads/feature/x"))

    def test_manual_run_with_full_plan_is_a_merged_run(self) -> None:
        assert is_merged_run(RunEvent(event_name="workflow_dispatch", ref="refs/heads/x", full_requested=True))

    def test_manual_run_without_full_plan_is_no_merged_run(self) -> None:
        assert not is_merged_run(RunEvent(event_name="workflow_dispatch", ref="refs/heads/x"))

    def test_pull_request_is_no_merged_run(self) -> None:
        assert not is_merged_run(pull_request())


class TestAnsibleFiles:
    @pytest.mark.parametrize("path", ANSIBLE_PATHS)
    def test_ansible_paths_are_recognized(self, path: str) -> None:
        assert ansible_files([path]) == [path]

    @pytest.mark.parametrize("path", NON_ANSIBLE_PATHS)
    def test_other_paths_are_ignored(self, path: str) -> None:
        assert ansible_files([path]) == []

    def test_every_role_directory_with_ansible_code_is_recognized(self) -> None:
        role_directories = sorted(path for path in (REPOSITORY_ROOT / "roles").glob("*/*") if path.is_dir())
        for directory in role_directories:
            relative_path = f"{directory.relative_to(REPOSITORY_ROOT).as_posix()}/main.yml"
            expected = [relative_path] if directory.name in ROLE_DIRECTORIES_WITH_ANSIBLE_CODE else []
            assert ansible_files([relative_path]) == expected, directory


class TestSelectPlan:
    def test_merged_run_runs_everything(self) -> None:
        plan = select_plan(RunEvent(event_name="push", ref="refs/heads/develop"))
        assert plan.install_os == ALL_INSTALL_OS
        assert plan.python_versions == ALL_PYTHON_VERSIONS

    def test_plain_pull_request_installs_once_and_tests_python_3_11(self) -> None:
        plan = select_plan(pull_request(["roles/ui/files/FWO.UI/Pages/Index.razor"]))
        assert plan.install_os == [DEFAULT_INSTALL_OS]
        assert plan.python_versions == PULL_REQUEST_PYTHON_VERSIONS

    def test_pull_request_with_ansible_change_also_installs_with_ansible_core_2_18(self) -> None:
        plan = select_plan(pull_request(["documentation/x.md", "roles/lib/tasks/main.yml"]))
        assert plan.install_os == [ANSIBLE_CORE_218_OS, DEFAULT_INSTALL_OS]
        assert "roles/lib/tasks/main.yml" in plan.reasons[0]

    def test_dependabot_pip_pull_request_tests_every_python_version(self) -> None:
        plan = select_plan(pull_request(["scripts/requirements.txt"], head_ref="dependabot/pip/develop/x-1.2"))
        assert plan.install_os == [DEFAULT_INSTALL_OS]
        assert plan.python_versions == ALL_PYTHON_VERSIONS

    def test_dependabot_pip_update_of_installer_requirements_also_installs_with_ansible_core_2_18(self) -> None:
        plan = select_plan(pull_request(["requirements.txt"], head_ref="dependabot/pip/develop/ansible-12.4"))
        assert plan.install_os == [ANSIBLE_CORE_218_OS, DEFAULT_INSTALL_OS]
        assert plan.python_versions == ALL_PYTHON_VERSIONS
        assert len(plan.reasons) == 2  # noqa: PLR2004

    def test_other_dependabot_pull_request_keeps_python_3_11(self) -> None:
        plan = select_plan(pull_request(["roles/FWO.sln"], head_ref="dependabot/nuget/develop/x-1.2"))
        assert plan.python_versions == PULL_REQUEST_PYTHON_VERSIONS

    def test_manual_run_without_full_plan_installs_once(self) -> None:
        plan = select_plan(RunEvent(event_name="workflow_dispatch", ref="refs/heads/develop"))
        assert plan.install_os == [DEFAULT_INSTALL_OS]

    @pytest.mark.parametrize(
        "event",
        [
            pull_request(),
            pull_request(["site.yml"]),
            RunEvent(event_name="push", ref="refs/heads/develop"),
            RunEvent(event_name="workflow_dispatch", ref="refs/heads/develop"),
        ],
    )
    def test_every_plan_contains_the_required_install(self, event: RunEvent) -> None:
        labels = [entry["label"] for entry in select_plan(event).install_matrix()["include"]]
        assert REQUIRED_INSTALL_LABEL in labels


class TestRendering:
    def test_install_matrix_names_os_and_label(self) -> None:
        plan = Plan(install_os=[ANSIBLE_CORE_218_OS], python_versions=["3.11"], reasons=["r"])
        assert plan.install_matrix() == {"include": [{"os": "ubuntu-22.04", "label": INSTALL_LABELS["ubuntu-22.04"]}]}

    def test_outputs_are_single_line_json(self) -> None:
        plan = select_plan(RunEvent(event_name="push", ref="refs/heads/develop"))
        lines = render_outputs(plan).splitlines()
        assert [line.split("=", 1)[0] for line in lines] == ["install_matrix", "python_versions"]
        assert json.loads(lines[1].split("=", 1)[1]) == ALL_PYTHON_VERSIONS
        assert len(json.loads(lines[0].split("=", 1)[1])["include"]) == len(ALL_INSTALL_OS)

    def test_summary_lists_installs_versions_and_reasons(self) -> None:
        summary = render_summary(select_plan(pull_request(["site.yml"])))
        assert INSTALL_LABELS[ANSIBLE_CORE_218_OS] in summary
        assert "Python unit tests: 3.11" in summary
        assert "Reason: changes Ansible files (site.yml" in summary


class TestMain:
    def test_reads_nul_separated_changed_files(self, tmp_path: Path) -> None:
        changed_files = tmp_path / "changed"
        changed_files.write_bytes(b"site.yml\0docs/a b.md\0")
        assert read_changed_files(str(changed_files)) == ["site.yml", "docs/a b.md"]

    def test_no_changed_files_without_a_file(self) -> None:
        assert read_changed_files(None) == []

    def test_writes_outputs_and_summary(self, tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
        changed_files = tmp_path / "changed"
        changed_files.write_bytes(b"roles/lib/tasks/main.yml\0")
        output = tmp_path / "output"
        summary = tmp_path / "summary"
        exit_code = main(
            [
                "--event-name",
                "pull_request",
                "--ref",
                "refs/pull/1/merge",
                "--head-ref",
                "feature/x",
                "--changed-files",
                str(changed_files),
                "--output",
                str(output),
                "--summary",
                str(summary),
            ]
        )
        assert exit_code == 0
        assert '"os":"ubuntu-22.04"' in output.read_text(encoding="utf-8")
        assert "### Test plan" in summary.read_text(encoding="utf-8")
        assert "### Test plan" in capsys.readouterr().out

    def test_full_flag_of_a_manual_run(self, tmp_path: Path) -> None:
        output = tmp_path / "output"
        main(["--event-name", "workflow_dispatch", "--ref", "refs/heads/x", "--full", "true", "--output", str(output)])
        assert '"os":"ubuntu-26.04"' in output.read_text(encoding="utf-8")


class TestWorkflow:
    def test_workflow_runs_the_plan_script_and_uses_its_outputs(self) -> None:
        workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
        assert "python3 scripts/ci/install_plan.py" in workflow
        assert "fromJSON(needs.plan.outputs.install_matrix)" in workflow
        assert "fromJSON(needs.plan.outputs.python_versions)" in workflow

    def test_each_install_job_checks_the_version_itself(self) -> None:
        # In the install job rather than once in the plan job: "Re-run failed jobs" keeps the
        # outputs of the successful plan job, so a verdict stored there could never be refreshed.
        jobs = workflow_jobs()
        steps = jobs["test-install"]["steps"]
        version_step = next(step for step in steps if step.get("id") == "version")
        assert "scripts/ci/evaluate_version_gate.sh" in version_step["run"]
        assert "exit 1" in version_step["run"]
        assert "continue-on-error" not in version_step
        assert version_step["if"] == "${{ github.event_name == 'pull_request' }}"
        assert set(jobs["plan"]["outputs"]) == {"install_matrix", "python_versions"}
        assert "evaluate_version_gate.sh" not in str(jobs["plan"]["steps"])

    def test_version_check_runs_before_anything_is_installed(self) -> None:
        step_names = [str(step.get("name", step.get("uses", ""))) for step in workflow_jobs()["test-install"]["steps"]]
        assert step_names[0].startswith("actions/checkout@")
        assert step_names[1] == "Check the version before installing"

    def test_apt_mirror_is_replaced_before_the_installer_environment_uses_apt(self) -> None:
        step_names = [str(step.get("name", "")) for step in workflow_jobs()["test-install"]["steps"]]
        mirror_step = step_names.index("Replace the EC2 apt mirror")
        assert mirror_step < step_names.index("Set up the installer environment")

    def test_diagnostics_steps_skip_a_failed_version_check(self) -> None:
        steps = workflow_jobs()["test-install"]["steps"]
        diagnostics = [step for step in steps if str(step.get("name", "")).startswith("Collect ")]
        assert diagnostics
        for step in diagnostics:
            assert step["if"] == "${{ failure() && steps.version.outcome != 'failure' }}", step["name"]

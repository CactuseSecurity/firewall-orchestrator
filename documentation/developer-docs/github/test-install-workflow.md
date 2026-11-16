# CI: FWO Test Install workflow

This describes [`.github/workflows/test-install.yml`](../../../.github/workflows/test-install.yml):
what it tests on a pull request, what it tests after a merge, and why. The other pull request
checks live in their own workflows: the SonarCloud analysis, which also runs the C# unit tests
(see [sonarcloud-workflow.md](sonarcloud-workflow.md)), CodeQL, and the version gate (see
[version-gate-workflow.md](version-gate-workflow.md)).

## Overview: what runs when

A pull request gets the checks that find most problems cheaply. The expensive checks that
mostly find problems of one operating system or one Python version run after the merge into
develop, and on the pull requests they exist for.

| Job | Pull request | Pull request changing [Ansible files](#ansible-files) | Dependabot pip pull request | Merge into develop or main, manual full run |
|---|---|---|---|---|
| Determine test plan | ✓ | ✓ | ✓ | ✓ |
| Test install on ubuntu-latest (Ubuntu 24.04, ansible-core 2.19, **required**) | ✓ | ✓ | ✓ | ✓ |
| Test install on ubuntu-22.04 (ansible-core 2.18) | – | ✓ | ✓ if it changes `requirements.txt` | ✓ |
| Test install on ubuntu-26.04 | – | – | – | ✓ |
| Python unit tests | 3.11 | 3.11 | 3.10 - 3.14 | 3.10 - 3.14 |
| Python Code Check | ✓ | ✓ | ✓ | ✓ |
| Release build | ✓ | ✓ | ✓ | ✓ |
| Launcher guard regression tests | ✓ | ✓ | ✓ | ✓ |
| Report failed checks to the merged pull request | – | – | – | only after a failed merge into develop |

The C# unit tests are not part of this workflow: the SonarCloud workflows run them on every
pull request and every push to develop and main, because Sonar needs their coverage.

## What each job tests

| Job | Contents |
|---|---|
| Determine test plan | lists the files the pull request changes, selects the installs and Python versions (`scripts/ci/install_plan.py`, the reasons are in the job summary), and checks the version early, see [Version check before the installs](#version-check-before-the-installs) |
| Test install on ... | sets up the installer environment like an installation does, runs the sample LDAP seeding tests, installs FWO with demo data, runs the JWT refresh integration test, the Ansible integration tests, both passes of a CA rotation with their refusal checks, and a certificate reset upgrade with the integration tests, see [Install jobs](#install-jobs) |
| Python unit tests on Python X | installs the installer requirements (proves that the pinned Ansible installs on X) and the importer and scripts requirements, then runs the importer tests and all tests under `scripts` (customizing scripts, version gate, workflow policy) |
| Python Code Check | ruff, ruff format, pyright for the oldest (3.10) and the newest (3.14) supported Python, see [Python versions](#python-versions) |
| Release build | checks that the .NET SDK pins agree, then builds `roles/FWO.sln` in `Release`, see [Release build](#release-build) |
| Launcher guard regression tests | tests `scripts/run-playbook-with-sudo.sh` against a stubbed Ansible |
| Report failed checks to the merged pull request | comments on the merged pull request, see [Checks after a merge](#checks-after-a-merge) |

## Where each kind of test runs

| Test | Where | When |
|---|---|---|
| C# unit tests (`FWO.Test`, Debug) | SonarCloud PR, SonarCloud | every pull request, every push to develop and main |
| Importer and scripts unit tests | Python unit tests; also SonarCloud PR for coverage | Python 3.11 on every pull request, every supported version after a merge |
| Python API availability per version | pyright in Python Code Check | every pull request, 3.10 and 3.14 |
| Fresh install, JWT, integration tests, CA rotation, certificate reset upgrade | Test install on ... | Ubuntu 24.04 always, 22.04 and 26.04 as in the overview |
| Release configuration compiles | Release build | every run |
| Version gate | early: Determine test plan; binding: Version gate workflow | every pull request |
| Static analysis | SonarCloud PR, CodeQL | every pull request |

The unit tests are not run inside the installs any more (see [Install jobs](#install-jobs)).
`HtmlToPdfTest` skips itself whenever `GITHUB_ACTIONS` is set, so PDF generation is not exercised
by any workflow.

## The test plan

`scripts/ci/install_plan.py` decides, from the event and the changed files:

- **Merge into develop or main** (a `push`), or a manual run with **full** checked: every
  install, every Python version.
- **Pull request**: the Ubuntu 24.04 install and Python 3.11, plus
  - the Ubuntu 22.04 install when the pull request changes [Ansible files](#ansible-files),
  - every Python version when it comes from a `dependabot/pip/*` branch.
- **Manual run** without full: like a pull request without Ansible changes.

### Operating systems, Python and Ansible

The installer creates its environment with the system's own `python3`
(`scripts/install-ansible-from-venv.sh`; RHEL and Rocky use `python3.11`), and that Python
decides the pinned Ansible version in `requirements.txt`: ansible 10.7 (ansible-core 2.18) below
Python 3.11, ansible 12.3 (ansible-core 2.19) from 3.11 on. The importer and customizing venvs
use the same interpreter. The install jobs set up their environment the same way, so each one
tests what an installation on that system gets:

| Install job | Python | ansible-core | Customer systems with the same combination |
|---|---|---|---|
| ubuntu-22.04 | 3.10 | 2.18 | Ubuntu 22.04, the only supported system on ansible-core 2.18 |
| ubuntu-24.04 (`ubuntu-latest`) | 3.12 | 2.19 | Ubuntu 24.04 |
| ubuntu-26.04 | 3.14 | 2.19 | Ubuntu 26.04 |

Debian and RHEL/Rocky have no GitHub-hosted runners and are not installed in CI. Their Python
versions (3.11 on Debian 12, RHEL and Rocky, 3.13 on Debian 13) are covered by the Python unit
tests.

ansible-core 2.18 and 2.19 do not evaluate every `when` or `assert.that` the same way: 2.18 wraps
the expression as `{% if <expr> %}`, where Jinja's if-statement parser rejects a bare conditional
expression that 2.19 accepts. Such a defect reached a customer installation once. The Ubuntu
22.04 install with its integration tests is the only check that sees it, which is why it runs
on every pull request that changes Ansible files.

### Ansible files

A pull request changing one of these paths also installs on Ubuntu 22.04:

| Path | Why |
|---|---|
| `site.yml`, `ansible.cfg`, `scripts/*.yml` | playbooks and Ansible configuration |
| `roles/*/tasks/`, `handlers/`, `templates/`, `defaults/`, `vars/`, `meta/` | everything Ansible evaluates in a role |
| `inventory/` | variables and their templated defaults |
| `requirements.txt`, `collections/` | the pinned Ansible and collection versions |
| `callback_plugins/` | runs inside Ansible, on the installer's Python |
| `scripts/*.sh` | the launcher and installer environment scripts |
| `.github/workflows/test-install.yml`, `scripts/ci/install_plan.py` | this workflow and its plan |

`roles/*/files/` is left out: it holds the application code and files the installer only copies.
`scripts/ci/test_install_plan.py` checks every role directory of the repository against this list.

### Python versions

The supported versions are 3.10 - 3.14. A pull request runs the unit tests on 3.11 (the Python of
Debian 12, RHEL and Rocky installations) only, because the most common version problem is an API
that one version lacks, and pyright finds that on every pull request without running anything:

- `pyproject.toml` sets `pythonVersion = "3.10"`, the oldest supported version, so pyright
  reports an API that only newer versions provide (for example `datetime.UTC` or `typing.Self`,
  both 3.11).
- Python Code Check runs pyright a second time with `--pythonversion 3.14`, which reports
  modules and functions that newer versions removed.

The other differences, behavior of the standard library and wheels of dependencies, show in the
Python unit tests on every version, after the merge and on Dependabot pip pull requests.

## Version check before the installs

The `Determine test plan` job of a pull request runs `scripts/ci/evaluate_version_gate.sh`, the
same evaluation as the required `Gate pull request version` check. If the version cannot be
merged as it is, the install jobs fail in their first step instead of spending half an hour on
an install, and the pull request learns it from its own run. The installs fail rather than skip,
because GitHub counts a skipped required check as passed.

This copy runs the pull request's own script and is only an early signal. The binding gate runs
in [version-gate.yml](version-gate-workflow.md) from the base branch. The Python unit tests and
the other jobs run regardless.

## Checks after a merge

Every commit on develop and main gets a run of its own; nothing is skipped when merges follow
each other quickly. A failure therefore belongs to exactly one merge.

When a run of a push to develop fails in `Determine test plan`, an install or the Python unit
tests, the job `Report failed checks to the merged pull request`
(`scripts/ci/report_merge_failure.py`) comments on the pull request whose merge produced the
commit. The comment lists the failed jobs with links to their logs; the pull request's author
and reviewers get notified because they are subscribed to it. A commit that no pull request
merged, such as a direct push, gets a warning in the run instead.

Receiving such a comment means develop is red from that merge on:

1. Open the failed job from the comment and find the cause. Most failures after a merge are an
   operating system or Python version difference the pull request checks do not cover, see the
   [overview](#overview-what-runs-when).
2. Fix it in a new pull request. If the fix is not quick, revert the merge.
3. To run the failing checks on the fix before merging it, start the workflow manually on its
   branch (Actions, FWO Test Install, Run workflow) with **full** checked.

The job needs `pull-requests: write` for the comment and `actions: read` for the job list; it only
runs for pushes, so it never runs with pull request code.

## Install jobs

Each install job runs, in order:

1. the installer environment setup, `source scripts/install-ansible-from-venv.sh`, as an
   installation does it (see [Operating systems, Python and Ansible](#operating-systems-python-and-ansible))
2. the sample LDAP seeding regression tests (`scripts/tests/test-sample-auth-data.sh`), which stub
   `ldapmodify` and therefore need neither an installation nor an LDAP server
3. an install with `--skip-tags integrationtests,unittests_run`
4. the JWT refresh integration test, run from the installed test project so that the installer's
   `global.json` below `/usr/local/fworch` selects the SDK. From the checkout, `dotnet` would use
   the newest SDK of the runner image, which on Ubuntu 22.04 shares `/usr/share/dotnet` with the
   installer's `dotnet-install` fallback and includes a newer major version than FWO supports.
5. the Ansible `--tags integrationtests` cleanup step
6. pass 1 of a CA rotation,
   `-e "installation_mode=upgrade internalca_ca_rotation=prepare" --tags certificates,integrationtests`
7. a pass 2 that has to be refused: the pass-1 records are set aside, the run must be refused and
   the staged CA must still be staged
8. a regular upgrade over a simulated interrupted switch (a `previous-ca.crt` next to the staged
   CA), which has to be refused without touching the CA in force
9. pass 2 of a CA rotation,
   `-e "installation_mode=upgrade internalca_ca_rotation=activate" --tags certificates,integrationtests`
10. the refusals after a completed rotation: pass 2 repeated, pass 1 over a simulated incomplete
    switch, and the switch persisted in the installer settings file (`FWORCH_LOCAL_SETTINGS`);
    none of them may stage a CA
11. a certificate reset upgrade,
    `-e "installation_mode=upgrade internalca_reset_certificates=true"`, with
    `--skip-tags unittests_run`

`unittests_run` tags only the tasks of `roles/tests-unit` that run the C# and importer unit tests;
those run in their own jobs instead (see [Where each kind of test runs](#where-each-kind-of-test-runs)).
Skipping the tag still deploys the test project, which step 4 runs from. To run the unit tests on
an installed system, use `--tags unittests`, see
[install-for-testing.md](../../installer/install-for-testing.md).

Steps 6 to 10 are limited to the certificate play and the tests: a rotation restarts the services
caching its anchors and identities itself, so the tag-limited form is the one an operator may also
use. Steps 6 and 9 run the tests inside the same invocation, because they check the state that pass
has to leave behind. After pass 1 both CAs are trusted everywhere, proven by presenting a client
identity of the staged CA to the API, and Apache advertises both as client CAs. After pass 2 the
previous CA is trusted nowhere, and Apache advertises no retired CA any more - which shows that it
was reloaded, not only that the file changed. Steps 7, 8 and 10 are the negative counterparts: pass
2 must not switch while a host has not recorded pass 1, only pass 2 may continue an interrupted
switch, and a completed rotation can neither be repeated nor followed by a new one while a switch is
incomplete. Each refused run goes through `scripts/ci/expect-installer-refusal.sh`, which accepts the
failure only if the output contains that refusal's own message, so a run stopped by an unrelated
error does not count. The refusal of pass 2 with `--limit` is not covered, because the single-host
CI inventory has no host a limit could leave out.

Step 11 is a full run rather than a tag-limited one on purpose: `roles/tests-integration` asserts on
facts the reset itself produces - which CA was retired, which identities were reissued, which
customer-managed pairs were left untouched - and those exist only inside the invocation that
performed the rotation. It is also what exercises the force-regeneration paths in
`roles/internalCA`, and it proves every service still comes up against a CA anchor that changed
underneath it.

Failures are followed by diagnostics steps: middleware after the JWT test, certificates after the CA
rotation or the reset upgrade, and the UI log after any failure (a broken API websocket path only
shows up as a 503 from Apache).

Demo data is off by default since GHSA-3v53-q5h9-pvfh, but the tests rely on it, so every install
requests it with `-e add_demo_data=yes`.

## Release build

The `release-build` job builds `roles/FWO.sln` in the `Release` configuration on every run. The
installer publishes the middleware and the UI in `Release` (`dotnet_mode`), while the SonarCloud
workflows build `Debug`. As warnings are treated as errors, a warning that only occurs in
`Release` - for example a variable whose only use is inside `#if DEBUG` - would otherwise only
surface when an installation fails. The job needs no installation and reports such a break within
minutes.

Before it builds, the job checks that the .NET SDK pins agree. The
`sdk.version` in the repository's `global.json` and the `dotnet-version` of
`sonarcloud-pr.yml` must both match `dotnet_version` in
`inventory/group_vars/all.yml`, which the installer derives its own
`global.json` from. A mismatch fails the job with an error that names the
file to update.

## .NET SDK selection

GitHub-hosted runner images can ship a newer major .NET SDK than FWO
supports. `dotnet` picks the newest installed SDK unless a `global.json`
pins it (#5700). Every `actions/setup-dotnet` step in this workflow
therefore reads the version from the repository's `global.json`
(`global-json-file`), and the `dotnet` commands in the checkout use the
same file. The installer writes its own `global.json` below
`/usr/local/fworch`, so its builds and the JWT integration test on the
runner use that SDK as well.

`sonarcloud-pr.yml` keeps an explicit `dotnet-version` because it runs
on `pull_request_target`. The workflow file comes from the base branch
while the checkout comes from the pull request, so a pull request branch
without a `global.json` would fail in `setup-dotnet`.

## Job names and required checks

Branch protection requires a check named `Test install on ubuntu-latest`, so the Ubuntu 24.04
install keeps that name although it runs on the pinned `ubuntu-24.04` runner. Every plan contains
it. The other install jobs are not required. Renaming a required job needs the ruleset changed at
the same time, or every open pull request waits for a check that never reports.

All install jobs pin an explicit Ubuntu version rather than `ubuntu-latest`, so a change of what
`ubuntu-latest` points to on GitHub-hosted runners cannot silently change which versions are
tested.

## Runners and queueing

GitHub-hosted runners are shared by every workflow and pull request of the organization, and
GitHub has no job priority. A pull request run takes one install runner and a few short jobs; a
run after a merge takes three install runners and five short Python jobs.

- On pull requests a newer push cancels the running checks.
- On develop and main every commit gets its own run (see [Checks after a merge](#checks-after-a-merge)).

Dependabot pull requests are grouped (see `.github/dependabot.yml`): minor and patch updates of
one directory arrive as one pull request, major updates keep a pull request of their own.

No workflow uses a dependency cache: `sonarcloud-pr.yml` builds pull request code in the
privileged `pull_request_target` context, and `scripts/ci/test_workflow_security_policy.py`
forbids caches in every workflow while that is the case (GHSA-3cwm-h5cm-r3f8).

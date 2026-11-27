# CI: SonarCloud workflows

This describes the behavior of
[`.github/workflows/sonarcloud.yml`](../../../.github/workflows/sonarcloud.yml) and
[`.github/workflows/sonarcloud-pr.yml`](../../../.github/workflows/sonarcloud-pr.yml), which run the
SonarCloud analysis (C# via the SonarScanner for .NET, Python with coverage) of this repository.

Both workflows use the repository secret `SONAR_TOKEN` and the repository variables
`SONAR_PROJECT_KEY` and `SONAR_ORGANIZATION`.

Python coverage includes importer code, scripts and installer callbacks in `callback_plugins`.
The role-duration callback tests stub Ansible's callback base class, so they run without Ansible.
Do not add Ansible to `scripts/requirements.txt`: the installer deploys that file into the
customizing venv on every middleware host.

Both workflows are the only place in CI where the C# unit tests (`FWO.Test`, `Debug`) run: Sonar
needs their coverage, so the FWO Test Install workflow does not run them a second time (see
[test-install-workflow.md](test-install-workflow.md#where-each-kind-of-test-runs)). A failing test
fails the job before the analysis ends, so a red **SonarQube Analyze PR** is either a failed test or
a failed quality gate; the log of the "Build and analyze pull request" step shows which.

For a pull request from a fork whose owner is not in `SONAR_TRUSTED_FORK_OWNERS`, the job stops at
"Gate trusted PR sources", before the build, so no C# unit test runs for it before the merge. The
required check stays red for such a pull request, so a maintainer has to look at it in any case.
Running the tests for untrusted forks would need an unprivileged workflow of its own.

## Branch analysis (`sonarcloud.yml`)

Runs on pushes to `main` and `develop` and on manual dispatch. It analyzes the pushed branch and
uses the product version from `inventory/group_vars/all.yml` as the Sonar project version, so that
the "previous version" new code baseline advances with each release.

## Pull request analysis (`sonarcloud-pr.yml`)

Runs on `pull_request_target` (`opened`, `synchronize`, `reopened`, `ready_for_review`) and
decorates the pull request with the SonarCloud result. Its job **SonarQube Analyze PR** waits for
the quality gate and is a required status check for `develop`, so the gate (among others at least
80% coverage on new code and no new issues) is enforced before merging.

| Pull request source | Behavior |
| --- | --- |
| branch in this repository | analyzed |
| fork of an owner listed in the repository variable `SONAR_TRUSTED_FORK_OWNERS` | analyzed |
| any other fork | the job fails, so the pull request cannot be merged until a maintainer takes it over |

Like every `pull_request_target` workflow, it runs the workflow file of the default branch `main`, not the version
in the pull request or in `develop`. Changes to `sonarcloud-pr.yml` therefore take effect for pull requests only
once they have reached `main`.

### Rules enforced by the build

Rules that the code base is free of are raised to `warning` in the root [`.editorconfig`](../../../.editorconfig).
Because [`roles/Directory.Build.props`](../../../roles/Directory.Build.props) sets `TreatWarningsAsErrors` and
`EnforceCodeStyleInBuild`, a new finding fails the local build instead of showing up in SonarCloud later.

| Build rule | SonarCloud rule | Checks |
| --- | --- | --- |
| CA1822 | (imported) | members that can be `static` (private and internal members only) |
| CA1825, CA1829, CA1854, CA1860, CA1861, CA2263, SYSLIB1045 | (imported) | performance and modernization rules of the .NET analyzers |
| CA1052 | S1118 | classes with only static members are `static` or `sealed` |
| CA2201 | S112 | no `throw new Exception(...)` |
| CA2208 | S3928 | argument exceptions name a real parameter |
| IDE0044 | S2933 | fields that are only set in the constructor are `readonly` |
| IDE0051, IDE0052 | S1144, S4487 | no unused or never read private members |
| IDE0060 | S1172 | no unused parameters in non-public methods |
| IDE0028, IDE0037, IDE0059 | (imported) | collection expressions, inferred member names, no unnecessary assignments |

`EnforceCodeStyleInBuild` makes the build report every IDE rule whose default severity is "suggestion", and SonarCloud
imports these as `external_roslyn` issues. IDE0028, IDE0037 and IDE0059 are such rules, so they are locked as well.
Only add a rule there once all its findings are fixed, otherwise the build breaks. The build does not check Razor
markup and `@code` blocks, so SonarCloud still reports those findings there.

### Sonar rules in the build

[`roles/Directory.Build.props`](../../../roles/Directory.Build.props) also adds Sonar's own C# analyzer (NuGet package
`SonarAnalyzer.CSharp`) to every Debug build. [`roles/SonarAnalyzer.globalconfig`](../../../roles/SonarAnalyzer.globalconfig)
lists every rule of the SonarCloud quality profile "cactus c#" that the package contains, each at one of two levels:

| Level | Meaning |
| --- | --- |
| `warning` | The code base is free of findings. With `TreatWarningsAsErrors`, a new finding fails the build. |
| `suggestion` | The code base still has findings. The IDE shows them and SonarCloud reports them, but the build does not fail. |

With version 10.36 of the package, 266 rules started at `warning` and 193 at `suggestion`. Unlike the .NET analyzers
above, the Sonar analyzer also checks Razor markup and `@code` blocks. In test projects it reports only the rules that
SonarCloud applies to test code.

- **Raise a rule** once all its findings are fixed by changing its level to `warning`. `dotnet build` does not print
  suggestions. To list the findings of a rule, set it to `warning` and build with
  `dotnet build roles/FWO.sln -c Debug -p:TreatWarningsAsErrors=false`.
- **Keep the file in step with the profile.** Add a rule that is activated in "cactus c#". A rule that is deactivated
  there may be set to `none`, but never set a rule of the profile to `none` or `silent`. The SonarCloud workflows also
  build in Debug, so this file applies to their analysis as well, and a rule switched off here could disappear from
  SonarCloud.
- **Updating the package:** rules that are new in Sonar's default rule set are `warning` unless the file lists them. If
  the new version reports findings for such a rule, the build of the update pull request fails. Add the rule at
  `suggestion`.
- **SonarCloud analysis:** the SonarScanner removes every analyzer whose file name starts with `SonarAnalyzer` and runs
  its own version instead. It also switches `TreatWarningsAsErrors` off. So the analysis neither counts a finding twice
  nor fails on a `warning` rule.
- **Release builds** leave the analyzer out, for example the "Release build" job of the FWO Test Install workflow. The
  installer is not affected at all: it copies only the `files` directories of the roles to the target, without
  `roles/Directory.Build.props`. The analyzer runs only in the compiler and adds nothing to the built assemblies, so
  run time is unaffected.
- **Build time:** the analyzer makes a full Debug rebuild of `roles/FWO.sln` about 20 s slower (about 20 s to 40 s on
  a developer machine). `dotnet format` in the pre-commit hook takes about 20 s longer as well.
- **Rules that only SonarCloud checks:** 25 rules of the profile are not in the open-source package. Among them are the
  rules based on symbolic execution (for example S2259 null dereference, S2583 and S2589 conditions that are always
  true or false, S3655, S4158, S2222 locks), several security rules (S2053, S3329, S4347, S5773, S6781) and the Entity
  Framework rules (for example S8717, S8733, S9022). The same applies to the taint analysis
  (`roslyn.sonaranalyzer.security.cs`) and architecture (`csharparchitecture`) rules of the profile.

### Required Actions policy

From 2 December 2026, GitHub blocks workflows triggered by `pull_request_target` in public repositories by default.
The repository Actions policy **Allow PR_target** (*Settings → Actions → Policies*) exempts the two workflows that
need the event:

- Enforcement: active
- Rule: restrict events, allowed event `pull_request_target`
- Workflow paths: `.github/workflows/sonarcloud-pr.yml` and `.github/workflows/version-gate.yml`

The policy is a repository setting, not part of the code. Keep it in sync with the workflows:

- When you rename one of these workflows or add another `pull_request_target` workflow, add its path to the policy.
  Otherwise GitHub does not run it and its required check never reports. The blocked runs are listed under
  *Settings → Actions → Policy insights*.
- `pull_request_target` runs the workflow file of the default branch `main`, not the one on `develop`. After a
  rename, the policy must therefore allow both the old and the new path until the rename has reached `main`,
  normally with the next stable release. Remove the old path only then.
- [`scripts/ci/test_workflow_security_policy.py`](../../../scripts/ci/test_workflow_security_policy.py) fails when
  the workflows (`*.yml` and `*.yaml`) triggered by `pull_request_target` differ from
  `ACTIONS_POLICY_PULL_REQUEST_TARGET_WORKFLOWS`. That list describes the workflows of the checked-out branch, so
  it does not contain old paths that the policy still keeps for `main`. Update it together with the policy. The
  test cannot read the policy itself, so it only reminds you to change the policy.
- Keep the workflow path condition. The rule is an allow-list of events: without the condition, all other workflows
  could only be triggered by `pull_request_target`.
- To check the policy, push to a pull request and open *Policy insights*. Runs that the policy allows do not appear
  there. Only blocked runs, or runs that would be blocked in evaluate mode, are listed.

## Accepted residual risk (GHSA-3cwm-h5cm-r3f8)

The SonarScanner for .NET has to build and test the code between its `begin` and `end` steps, and
both steps need `SONAR_TOKEN`. To analyze pull requests from forks, `sonarcloud-pr.yml` therefore
checks out and builds the pull request head in the privileged `pull_request_target` context, where
the token is available.

[GHSA-3cwm-h5cm-r3f8](https://github.com/CactuseSecurity/firewall-orchestrator/security/advisories/GHSA-3cwm-h5cm-r3f8)
rates this Low (CVSS 3.1 2.7) and the risk is accepted under the following conditions. If one of them
no longer holds, the rating has to be reassessed.

1. `SONAR_TOKEN` is an organization token scoped to this public project with the Browse and
   Execute Analysis permissions only - no project or organization administration, no Administer
   Issues and no Administer Security Hotspots. A leaked token then only allows uploading forged
   analyses for this project. **Keep it that way when the token is replaced.**
2. `SONAR_TOKEN` is the only secret this workflow uses, and the workflow token is read-only
   (`contents: read`, checkout without persisted credentials).
3. No workflow restores caches. Pull request code running in the `pull_request_target` context must not
   be able to leave anything behind that a later run executes. Whether GitHub lets such runs write cache
   entries of the default branch (current runs only get read access) is not relied on.
4. The quality gate is not the only control before merging (tests, reviews and approval are
   required as well).

Remaining risks that are accepted:

- The trusted owner list checks the owner of the repository the pull request comes from
  (`pull_request.head.repo.owner`), not who opened it or the code it contains. Everyone with push access
  to a trusted owner's fork, and every trusted owner who takes over someone else's commits, runs that
  code with the token. Review external commits before taking them over into your fork, and do not give
  others push access to it.
- A compromised account of a trusted owner can obtain the token and upload forged analyses until
  the token is replaced.

All actions in both workflows are pinned to full commit SHAs (Dependabot keeps them up to date).

[`scripts/ci/test_workflow_security_policy.py`](../../../scripts/ci/test_workflow_security_policy.py)
enforces conditions 2 and 3 and the gating: `sonarcloud-pr.yml` must be the only workflow building
pull request code in the `pull_request_target` context, gate trusted sources before the checkout,
stay read-only and use no secret but `SONAR_TOKEN`; other `pull_request_target` workflows must use
no secrets; no workflow may use caches; and the Sonar workflows must pin their actions. Condition 1
cannot be checked from the repository and must be verified in SonarCloud whenever the token changes.
The test reads the workflows with a YAML parser, as GitHub does, so comments, quoting, anchors and flow
collections cannot hide a trigger, a secret, a write permission or a checkout of the pull request head
(including `gh pr checkout` and `git fetch … pull/<n>/head`). A secret selected at run time
(`secrets[matrix.name]`) counts as access to all secrets.

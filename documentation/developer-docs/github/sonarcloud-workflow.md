# CI: SonarCloud workflows

This describes the behavior of
[`.github/workflows/sonarcloud.yml`](../../../.github/workflows/sonarcloud.yml) and
[`.github/workflows/sonarcloud-pr.yml`](../../../.github/workflows/sonarcloud-pr.yml), which run the
SonarCloud analysis (C# via the SonarScanner for .NET, Python with coverage) of this repository.

Both workflows use the repository secret `SONAR_TOKEN` and the repository variables
`SONAR_PROJECT_KEY` and `SONAR_ORGANIZATION`.

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

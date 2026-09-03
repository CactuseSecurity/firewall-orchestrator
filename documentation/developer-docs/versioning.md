# Versioning policy

This document defines how Firewall Orchestrator versions are prepared and
released. It also describes the repository configuration that allows a stable
release tag to fast-forward `main` securely.

## When a new version is required

Create a new version when a change affects at least one of the following:

- The database model or stored data
- The internal LDAP structure
- Major product behavior or functionality
- An existing installation that requires an explicit upgrade step

Bug fixes and other releasable changes must also use a new version rather than
reusing an existing version number.

## Version format

Product versions use three numeric components:

```text
major.minor.patch
```

For example, `9.3.0` is stored as the product version. Release tags may
optionally have a `v` prefix, for example `v9.3.0` or `9.3.0`. The established
repository convention is to use the `v` prefix.

## Version lifecycle

A version is **open** while it is the version in
[`inventory/group_vars/all.yml`](../../inventory/group_vars/all.yml) and no sealing
tag for it exists. Work merges into an open version. Creating a sealing tag closes
the version: from that moment on nothing may merge onto it any more, and the next
change must raise `product_version`.

| Tag | Seals the version | Advances `main` | Meaning |
| --- | --- | --- | --- |
| `vX.Y.Z-dev` | yes | no | the version is finished on `develop` but was not released to a customer |
| `vX.Y.Z` | yes | yes | stable release |
| `vX.Y.Z-rc1`, `-beta`, `-alpha` | no | no | snapshot of an open version |

A snapshot tag is a marker, not a seal. Work keeps merging into `X.Y.Z` after
`vX.Y.Z-rc1`, so the final `X.Y.Z` will normally differ from its release candidate.

Because a sealing tag closes the version, a later stable `vX.Y.Z` can only be created
on the same commit as an earlier `vX.Y.Z-dev`: no other commit carries that version.

This lifecycle is enforced by the **Version gate** workflow, see
[Automated enforcement](#automated-enforcement) below. The typical sequence is:

1. The latest sealing tag is `v1.2.3-dev` and `all.yml` is on the open version `1.2.4`.
2. A small change merges: `all.yml` stays `1.2.4`, the revision history is extended.
   Optionally tag `v1.2.4-rc1` to mark a release candidate; `1.2.4` stays open.
3. Seal `1.2.4` by creating `v1.2.4-dev`, or `v1.2.4` when the version is released to a
   customer. Every open pull request still on `1.2.4` is now blocked and must raise the
   version.
4. The next change raises `all.yml` to `1.2.5` (or `1.3.0`, or `2.0.0`) and adds the
   matching revision history section. That bump is only accepted because `1.2.4` is
   sealed.

### Hotfixes

The model keeps a single linear version line on `develop`. A fix for a released `1.2.4`
therefore becomes `1.2.5`; maintaining a `1.2.4.x` line in parallel would require
release branches and additional rules and is currently not supported.

## Preparing a version

Before creating the release tag:

1. Set `product_version` in
   [`inventory/group_vars/all.yml`](../../inventory/group_vars/all.yml) to the
   new version without a `v` prefix.
2. Add the version and its changes to the end of
   [`documentation/revision-history.md`](../revision-history.md).
3. Update
   [`documentation/version-feature-overview.md`](../version-feature-overview.md)
   and [`documentation/feature-catalogue.md`](../feature-catalogue.md) when the
   release adds or changes documented product features.
4. Add all component-specific upgrade steps required for existing
   installations.
5. Complete the relevant formatting, build, unit-test, integration-test, and
   installer validation required by the changed components.
6. Ensure the release commit is a descendant of the current `main` commit.

Never reuse a released version number or modify an existing release tag to
point to another commit.

## Upgrade scripts

Add a new database upgrade script under `roles/database/files/upgrade/` when a
release changes the database or its stored data. Use the full product version
as the file name, including the patch component:

```text
roles/database/files/upgrade/9.3.0.sql
```

Do not modify upgrade scripts belonging to older versions. Every new upgrade
operation must be safe to execute repeatedly. Use guards such as
`IF NOT EXISTS` or `ON CONFLICT DO NOTHING` where appropriate.

For example:

```sql
INSERT INTO report_template
    (report_filter, report_template_name, report_template_comment,
     report_template_owner)
VALUES
    ('type=natrules and time=now ', 'Current NAT Rules', 'T0105', 0)
ON CONFLICT DO NOTHING;
```

LDAP and other component upgrades must follow the same rule: add a versioned,
idempotent upgrade step without changing the behavior of older upgrade steps.

For instructions on running an upgrade on an existing installation, see
[Upgrading FWO](installer/upgrading.md).

## Automated enforcement

The **Version gate** workflow
([`.github/workflows/version-gate.yml`](../../.github/workflows/version-gate.yml))
runs on every pull request that targets `develop`. Its single job,
`Gate pull request version`, is the required check, and it fails when:

- the version its merge result carries is already sealed by a release tag,
- it raises `product_version` while the previous version has no sealing tag yet,
- it raises `product_version` to an already sealed version, or lowers it,
- it raises `product_version` without adding the matching section to
  [`documentation/revision-history.md`](../revision-history.md).

The gate is evaluated on `refs/pull/<n>/merge`, so a pull request that does not touch
`all.yml` inherits the base branch version and is never blocked for being out of date.

The **Version gate refresh** workflow
([`.github/workflows/version-gate-refresh.yml`](../../.github/workflows/version-gate-refresh.yml))
re-runs that gate for every open pull request whenever a sealing tag is pushed. This is
what makes step 3 of the lifecycle bite: pull requests that were green on the now sealed
version turn red immediately, without needing a push to the pull request. The tag list is
read when the gate runs, so a re-run reaches a different, correct verdict.

The **Version tag guard** workflow
([`.github/workflows/version-tag-guard.yml`](../../.github/workflows/version-tag-guard.yml))
reports, after the fact, a version tag created on a commit carrying a different
`product_version`, a sealing tag outside the `develop`/`main` line, and a push to
`develop` that landed on an already sealed version.

One gap remains by design: a merge completed in the same moment a sealing tag is pushed
can still land on the version being sealed. The `develop` audit job reports it within
minutes; it cannot prevent it.

The rules live in [`scripts/ci/version_gate.py`](../../scripts/ci/version_gate.py) and are
unit tested in `scripts/ci/test_version_gate.py`. For the workflow mechanics see
[the version gate workflow documentation](github/version-gate-workflow.md).

## Repository release configuration

The following one-time configuration is required in the upstream GitHub
repository.

### GitHub App

Create a dedicated organization-owned GitHub App (here: `fwo-release-forwarder`) with the following scope:

- `Contents: Read and write`
- Installation access to the Firewall Orchestrator repository only
- Bypass access for every ruleset that would otherwise prevent it from
  updating `main`

The app must remain least-privileged and must not receive unrelated repository
or organization permissions.

### Actions variable and protected environment

Configure the repository variable `FWO_RELEASE_APP_CLIENT_ID` with the app's
client ID.

Create an Actions environment named `stable-release` with these settings:

- Under **Deployment branches and tags**, choose **Selected branches and tags**
- Add a **Tag** rule with the name pattern `*`
- Do not add a **Branch** rule
- Add `FWO_RELEASE_APP_PRIVATE_KEY` as an environment secret

Do not create a repository-level or organization-level
`FWO_RELEASE_APP_PRIVATE_KEY` that is accessible to this repository. A
repository-level secret would also be available to workflows pushed on ordinary
branches and would bypass the tag-only security boundary.

The one-maintainer release model does not require an environment reviewer. The
environment restricts credential access by ref type, while the tag ruleset
restricts who may create an eligible ref.

### Tag ruleset

Create an active repository tag ruleset with the following configuration:

- Target tag pattern: `*`
- Enable `Restrict creations`
- Allow only trusted release maintainers to bypass the creation restriction

Restricting tag updates and deletions is also recommended so that published
release tags remain immutable.

### Required status check

Configure branch protection for `develop` to require the check
`Gate pull request version`. That is the job name; the `Version gate` shown in front of
it in the pull request is only the workflow name. Without this, the Version gate reports
its verdict but does not prevent a merge.

Do not enable "Require branches to be up to date before merging" for the sake of the
gate. The gate already evaluates the merge result, so an out-of-date pull request is
judged by what merging it would actually produce.

The environment pattern and the tag ruleset are separate controls. The
environment prevents branch workflows from reading the private key. The tag
ruleset prevents ordinary repository writers from creating a tag that can use
the environment.

## Creating a stable release

1. Confirm that the release commit contains the intended version metadata,
   revision history, upgrade steps, and documentation.
2. Confirm that all required validation has passed.
3. Confirm that the release commit descends from `main`.
4. Confirm that the release commit's `product_version` equals the version you are
   about to tag. A tag on a commit carrying a different version seals the wrong
   version and is rejected by the **Version tag guard** workflow.
5. Create the stable tag on the release commit using the repository convention,
   for example `v9.3.0`.
6. Push the tag, or publish a GitHub Release that creates the tag.
7. Monitor the **Fast-forward main to release tag** Actions workflow.
8. Confirm that `main` and the stable release tag resolve to the same commit.
9. Confirm that the **Version gate** workflow re-evaluated the open pull requests.

Do not manually force-push `main`. If the workflow refuses the update, correct
the release ancestry or repository configuration instead of bypassing its
checks.

## Automatic fast-forward of main

The
[`fast-forward-main-to-release-tag.yml`](../../.github/workflows/fast-forward-main-to-release-tag.yml)
workflow runs when a tag is pushed. It:

1. Enters the tag-only `stable-release` environment.
2. Generates a short-lived installation token for the release GitHub App.
3. Accepts only stable numeric tags such as `v9.3.0` or `9.3.0`.
4. Resolves the tag and fetches the current `main` branch and tag history.
5. Exits successfully when `main` already points to the tagged commit.
6. Rejects a tag whose commit is older than or unrelated to `main`.
7. Fast-forwards `main` when the tagged commit descends from the current
   `main`.

The workflow never force-pushes. Concurrent runs are serialized so that two
release tags cannot race to update `main`.

## Workflow outcomes

| Condition | Result |
| --- | --- |
| Tag is not a stable numeric version | Workflow exits without changing `main` |
| `main` already points to the tag | Workflow succeeds without pushing |
| Tag is a descendant of `main` | Workflow fast-forwards `main` |
| Tag is older than or unrelated to `main` | Workflow fails without changing `main` |
| Environment, app, or ruleset configuration is missing | Authentication or push fails without changing `main` |

## Security model

The release GitHub App can bypass the rulesets protecting `main`, so its private
key must not be generally available to repository workflows.

The security boundary consists of all of the following:

- The app private key exists only in the tag-only `stable-release` environment.
- The workflow job explicitly uses the `stable-release` environment.
- The tag ruleset limits tag creation to trusted release maintainers.
- The app is installed only on this repository and has only the required
  contents permission.
- The workflow's built-in `GITHUB_TOKEN` remains read-only.
- Git verifies that every update to `main` is a fast-forward.

Changing `*` to a narrower tag name pattern can improve naming discipline, but
the essential authorization control is restricting who can create matching
tags.

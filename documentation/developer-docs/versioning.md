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

For example, `9.3.0` is stored as the product version. Stable release tags may
optionally have a `v` prefix, for example `v9.3.0` or `9.3.0`. The established
repository convention is to use the `v` prefix.

Pre-release suffixes such as `-dev`, `-rc1`, or `-beta` are not stable release
tags and do not advance `main`.

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

The environment pattern and the tag ruleset are separate controls. The
environment prevents branch workflows from reading the private key. The tag
ruleset prevents ordinary repository writers from creating a tag that can use
the environment.

## Creating a stable release

1. Confirm that the release commit contains the intended version metadata,
   revision history, upgrade steps, and documentation.
2. Confirm that all required validation has passed.
3. Confirm that the release commit descends from `main`.
4. Create the stable tag on the release commit using the repository convention,
   for example `v9.3.0`.
5. Push the tag, or publish a GitHub Release that creates the tag.
6. Monitor the **Fast-forward main to release tag** Actions workflow.
7. Confirm that `main` and the stable release tag resolve to the same commit.

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

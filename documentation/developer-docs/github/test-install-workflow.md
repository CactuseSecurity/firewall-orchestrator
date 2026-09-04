# CI: FWO Test Install workflow

This describes the behavior of [`.github/workflows/test-install.yml`](../../../.github/workflows/test-install.yml),
which runs the Ansible test install against either a single default
combination or a full OS x Python matrix, depending on the triggering
event.

## Triggers

The workflow runs on:

- `workflow_dispatch` (manual run, with a `full` checkbox input)
- `push` to `main` or `develop`
- `pull_request` (any branch)

## Matrix selection

A `setup` job inspects the triggering event and decides whether to run
the **full matrix** or the **minimal matrix**. It emits the chosen
matrix as JSON, which the `test-install` job consumes via `fromJSON()`.

The full matrix is selected when any of the following is true:

- the event is `workflow_dispatch` and the `full` input was checked
- the event is `push` to `main` or `develop`
- the event is `pull_request` from a `dependabot/pip/*` branch

Otherwise the minimal matrix is used. This covers `workflow_dispatch`
with `full` unchecked (the default), and any `pull_request` that isn't
from a `dependabot/pip/*` branch — including dependabot PRs for the
`nuget` and `github-actions` ecosystems, which intentionally only get
the minimal matrix.

| Matrix  | Combinations | Notes |
|---------|--------------|-------|
| Full    | `ubuntu-26.04`, `ubuntu-24.04`, `ubuntu-22.04` x Python `3.10`-`3.14` (15 combos) | `fail-fast: false`, so one failing combo does not cancel the rest |
| Minimal | `ubuntu-24.04` / Python `3.11` (1 combo) | matches the pre-merge `test-install.yml` behavior |

Both matrices pin an explicit Ubuntu version (`ubuntu-24.04`) rather
than `ubuntu-latest`, so a future change to what `ubuntu-latest` points
to on GitHub-hosted runners can't silently change which OS versions
this workflow actually tests.

## Integration tests

Each matrix entry carries an `integration` flag. Exactly one entry per
run has `integration: true`:

- in the full matrix, the `ubuntu-24.04` / Python `3.11` entry
- in the minimal matrix, its sole entry

Only that entry runs the JWT refresh integration test and the Ansible
`--tags integrationtests` cleanup step; every other entry runs a plain
install. This keeps the heavier integration flow from running on all
15 full-matrix combos.

## Job names

Each matrix entry also carries a `label`, used as the job's display
name. Both the minimal matrix's single job and the full matrix's
designated integration entry are labeled `Test install on
ubuntu-latest`, even though they run on the pinned `ubuntu-24.04`
runner — preserving the job name from the previous, separate
`test-install.yml` workflow, since branch protection requires a check
with that exact name to exist on every run, including full-matrix
runs. Other full matrix jobs are labeled with their OS and Python
version, e.g. `Test install on ubuntu-26.04 with Python 3.10`.

## Ruff required-version sync (Dependabot pip PRs)

Ruff's version is pinned twice: as `ruff==<version>` in
`roles/importer/files/importer/requirements.txt` and as
`tool.ruff.required-version` in `pyproject.toml`. If the two drift apart,
ruff refuses to run and the `python-code-check` job's `ruff check` step
fails. When Dependabot opens a pip PR that bumps ruff, only the
`requirements.txt` pin changes.

The `sync-ruff-required-version` job closes that gap automatically. It
runs only on `pull_request` events from `dependabot[bot]` on a
`dependabot/pip/*` branch. It reads the pinned ruff version from
`requirements.txt`, and if `required-version` in `pyproject.toml` differs,
rewrites it, commits, and pushes the fix back onto the Dependabot branch.
The job is idempotent — when the two already match, it does nothing.

The push is authenticated with a short-lived GitHub App installation
token minted by `actions/create-github-app-token`, scoped to
`contents: write` on this repository. An App token (rather than the
default `GITHUB_TOKEN`) is required so the sync commit re-triggers this
workflow. The re-triggered run supersedes the original via the workflow's
`concurrency` group (keyed on the PR number), so the fresh run — now with
matching versions — is the one that validates and reports status.

The App credentials are supplied as `vars.FWO_RUFF_SYNC_APP_ID` and the
**Dependabot** secret `FWO_RUFF_SYNC_APP_KEY` (Settings -> Secrets and
variables -> Dependabot; Dependabot-triggered runs cannot read regular
Actions secrets, so the private key must live in the Dependabot store).
The App must be installed on the repository with **Contents: Read and
write**. When the credentials are absent the `setup` job's
`has_ruff_sync_app` output is `false` and the job is skipped rather than
failing.

## Error handling in the matrix-selection script

The script sets `set -euo pipefail` and validates the generated matrix
JSON with `jq empty` before writing it to `GITHUB_OUTPUT`, failing the
step with a clear `::error::` message if the JSON is malformed. It also
logs an `::notice::` summarizing which matrix was selected and how many
combinations it contains.

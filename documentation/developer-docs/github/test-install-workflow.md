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

Each matrix entry carries an `integration` flag:

- in the full matrix, the `ubuntu-24.04` / Python `3.10` and `3.11`
  entries
- in the minimal matrix, both of its entries

The minimal matrix runs two, one per pinned Ansible generation, because
`requirements.txt` installs ansible-core 2.18 below Python 3.11 and 2.19
from 3.11 up, and the two do not evaluate a `when` or `assert.that` the
same way. Running `roles/tests-integration` on one generation only
cannot see that class of defect at all.

Only entries with the flag run the heavier flow; every other entry runs
a plain install, which keeps that flow off all 15 full-matrix combos.
The flagged entries run, in order:

1. an install with `--skip-tags integrationtests`
2. the JWT refresh integration test
3. the Ansible `--tags integrationtests` cleanup step
4. a certificate reset upgrade,
   `-e "installation_mode=upgrade internalca_reset_certificates=true"`

Step 4 is a full run rather than a tag-limited one on purpose:
`roles/tests-integration` asserts on facts the reset itself produces —
which CA was retired, which identities were reissued, which
customer-managed pairs were left untouched — and those exist only inside
the invocation that performed the rotation. It is also what exercises
the force-regeneration paths in `roles/internalCA`, and it proves every
service still comes up against a CA anchor that changed underneath it.
A failure there is followed by a certificate diagnostics step.

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

## Error handling in the matrix-selection script

The script sets `set -euo pipefail` and validates the generated matrix
JSON with `jq empty` before writing it to `GITHUB_OUTPUT`, failing the
step with a clear `::error::` message if the JSON is malformed. It also
logs an `::notice::` summarizing which matrix was selected and how many
combinations it contains.

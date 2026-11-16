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

1. the sample LDAP seeding regression tests
   (`scripts/tests/test-sample-auth-data.sh`), which stub `ldapmodify`
   and therefore need neither an installation nor an LDAP server
2. an install with `--skip-tags integrationtests`
3. the JWT refresh integration test
4. the Ansible `--tags integrationtests` cleanup step
5. pass 1 of a CA rotation,
   `-e "installation_mode=upgrade internalca_ca_rotation=prepare" --tags certificates,integrationtests`
6. a pass 2 that has to be refused: the pass-1 records are set aside,
   the run must be refused and the staged CA must still be staged
7. a regular upgrade over a simulated interrupted switch (a
   `previous-ca.crt` next to the staged CA), which has to be refused
   without touching the CA in force
8. pass 2 of a CA rotation,
   `-e "installation_mode=upgrade internalca_ca_rotation=activate" --tags certificates,integrationtests`
9. the refusals after a completed rotation: pass 2 repeated, pass 1 over
   a simulated incomplete switch, and the switch persisted in the
   installer settings file (`FWORCH_LOCAL_SETTINGS`); none of them may
   stage a CA
10. a certificate reset upgrade,
   `-e "installation_mode=upgrade internalca_reset_certificates=true"`

Steps 5 to 9 are limited to the certificate play and the tests: a
rotation restarts the services caching its anchors and identities
itself, so the tag-limited form is the one an operator may also use.
Steps 5 and 8 run the tests inside the same invocation, because they
check the state that pass has to leave behind. After pass 1 both CAs
are trusted everywhere, proven by presenting a client identity of the
staged CA to the API, and Apache advertises both as client CAs. After
pass 2 the previous CA is trusted nowhere, and Apache advertises no
retired CA any more - which shows that it was reloaded, not only that
the file changed. Steps 6, 7 and 9 are the negative counterparts: pass 2
must not switch while a host has not recorded pass 1, only pass 2 may
continue an interrupted switch, and a completed rotation can neither be
repeated nor followed by a new one while a switch is incomplete. Each
refused run goes through `scripts/ci/expect-installer-refusal.sh`, which
accepts the failure only if the output contains that refusal's own
message, so a run stopped by an unrelated error does not count. The
refusal of pass 2 with `--limit` is not covered, because the
single-host CI inventory has no host a limit could leave out. A failure
is followed by a certificate diagnostics step.

Step 10 is a full run rather than a tag-limited one on purpose:
`roles/tests-integration` asserts on facts the reset itself produces —
which CA was retired, which identities were reissued, which
customer-managed pairs were left untouched — and those exist only inside
the invocation that performed the rotation. It is also what exercises
the force-regeneration paths in `roles/internalCA`, and it proves every
service still comes up against a CA anchor that changed underneath it.
A failure there is followed by a certificate diagnostics step.

## Release build

The `release-build` job builds `roles/FWO.sln` in the `Release`
configuration on every run. The installer publishes the middleware and the
UI in `Release` (`dotnet_mode`), while the SonarCloud workflows build
`Debug`. As warnings are treated as errors, a warning that only occurs in
`Release` - for example a variable whose only use is inside `#if DEBUG` -
would otherwise only surface when an installation fails. The job needs no
installation and reports such a break within minutes.

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
`/usr/local/fworch`, so its builds and unit tests on the runner use that
SDK as well.

`sonarcloud-pr.yml` keeps an explicit `dotnet-version` because it runs
on `pull_request_target`. The workflow file comes from the base branch
while the checkout comes from the pull request, so a pull request branch
without a `global.json` would fail in `setup-dotnet`.

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

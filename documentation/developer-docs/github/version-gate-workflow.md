# CI: Version gate workflows

This describes the behavior of
[`.github/workflows/version-gate.yml`](../../../.github/workflows/version-gate.yml),
[`.github/workflows/version-gate-refresh.yml`](../../../.github/workflows/version-gate-refresh.yml)
and
[`.github/workflows/version-tag-guard.yml`](../../../.github/workflows/version-tag-guard.yml),
which enforce the rules defined in [Versioning policy](../versioning.md).

The rules themselves live in
[`scripts/ci/version_gate.py`](../../../scripts/ci/version_gate.py) so that they can be unit
tested; the workflows only feed it files and report its verdict.

## The rule that is enforced

A product version is **open** until a sealing tag for it exists. A pull request may only merge
onto an open version, and may only open a new version once the previous one has been sealed.

- Sealing tags: `vX.Y.Z` and `vX.Y.Z-dev` (the `v` prefix is optional)
- Snapshot tags such as `vX.Y.Z-rc1` or `vX.Y.Z-beta` do **not** seal a version

## Version gate workflow

Runs on `pull_request_target` for pull requests targeting `develop`
(`opened`, `synchronize`, `reopened`, `ready_for_review`, `edited`).

It contains exactly one job, **`Gate pull request version`**, and that job's check run *is*
the gate result. There is no commit status and no second job, so a pull request shows one
gate entry and nothing else. That single check run is what branch protection must require.

### What is compared

[`scripts/ci/evaluate_version_gate.sh`](../../../scripts/ci/evaluate_version_gate.sh) resolves
three inputs and hands them to the gate:

| Input | Source |
| --- | --- |
| merged version `V` | `product_version` in `inventory/group_vars/all.yml` at `refs/pull/<n>/merge` |
| base version `P` | `product_version` in the same file at the base branch tip |
| sealed versions | `git ls-remote --tags origin`, so no tag objects are fetched |

Reading `V` from the **merge result** rather than from the pull request head is deliberate. A
pull request that never touched `all.yml` inherits the base version automatically, so it is not
falsely blocked for being out of date, and only pull requests that actually change the version
are held to the bump rules. This also makes the "require branches to be up to date before
merging" branch protection setting unnecessary for the gate.

All three inputs are resolved when the job runs. Nothing is baked into the check run, which is
what lets a plain re-run produce a different, correct verdict later.

### Verdicts

| Case | Condition | Job |
| --- | --- | --- |
| any | `V` is a valid `major.minor.patch` | fails otherwise |
| `V == P` | no sealing tag for `V` exists | fails otherwise: bump `product_version` |
| `V != P` | `V > P` | fails otherwise: version must not go backwards |
| `V != P` | a sealing tag for `P` exists | fails otherwise: seal `P` first |
| `V != P` | no sealing tag for `V` exists | fails otherwise: choose a higher version |
| `V != P` | `documentation/revision-history.md` ends with a `## V - DD.MM.YYYY` section | fails otherwise |
| any | `refs/pull/<n>/merge` exists | fails otherwise: resolve the merge conflicts |

The revision history is only checked when the version changes, which keeps the check objective
and free of false positives.

### Security

The job runs under `pull_request_target` but holds a **read-only** token: it publishes
nothing, so it needs no write permission at all and the elevated context is worthless to a
fork. It checks out the **base** branch, never the pull request head, and reads pull request
content with `git show` as inert data. No fork code is executed and no
`allow-unsafe-pr-checkout` is used.

Checking out the base branch also means the gate logic itself comes from `develop`. A pull
request cannot edit `version_gate.py` to make itself pass.

## Version gate refresh workflow

Runs on any pushed tag, and on `workflow_dispatch` (optionally for a single `pr`).

Creating a sealing tag closes a version, which must block every open pull request that would
still merge onto it. Those pull requests get no event of their own, so their gate check run
would keep the conclusion it had before the tag existed. This workflow re-runs the Version gate
workflow for each of them with `gh run rerun`, which rewrites that same check run in place
rather than adding a second signal.

It skips tags that do not seal a version, needs no checkout at all, and matches pull requests to
runs by head SHA:

```bash
gh api --paginate ".../workflows/version-gate.yml/runs?event=pull_request_target" \
    --jq '.workflow_runs[] | [.head_sha, .id, .status] | @tsv'
```

Runs come back newest first, so the first line matching a pull request's head SHA is that pull
request's most recent gate run. Runs that are not yet `completed` are left alone, because they
will report a fresh result on their own.

A re-run replays the workflow file from the original run, but the checkout, the tag list and the
gate script are all resolved at run time, so the verdict is current even if the workflow YAML
has since changed. If a pull request cannot be refreshed at all — no run found, or the re-run
was rejected — the job fails loudly, because that pull request would otherwise keep a stale
green gate.

This workflow lives in its own file on purpose: a second job inside `version-gate.yml` would add
a permanently skipped entry to every pull request.

## Version tag guard workflow

Both jobs report after the fact. The tag or the merge already exists, and a workflow cannot undo
either; the point is that a mistake is noticed within minutes instead of at the next release.

**`validate-tag`** runs on any pushed tag. It evaluates with the gate logic taken from `develop`
rather than from the tagged commit, so tags on older commits are still checked. It fails when:

- a version tag points at a commit whose `product_version` differs from the tag, which would
  seal the wrong version and break the invariant for every later pull request;
- a sealing tag points at a commit that is contained in neither `develop` nor `main`.

Remediation is to delete the tag and recreate it on the correct commit. Note that this is only
possible while the tag ruleset permits it, and that a published release tag must never be moved.

**`audit-develop`** runs on every push to `develop` and fails when `develop`'s `product_version`
is already sealed. It catches the narrow race where a merge lands in the same moment a sealing
tag is pushed, and any direct push that bypassed the required check.

## Required repository configuration

- Branch protection on `develop` must require the check **`Gate pull request version`**.
  The name to enter is the job name; `Version gate` in front of it is only the workflow name.
- "Require branches to be up to date before merging" is not needed, see above.
- The existing tag ruleset (`Restrict creations`, release maintainers only) is what gives
  sealing tags their authority. Without it, anyone who can push a tag can seal a version.

No new secrets, apps or environments are required.

## Local use

The gate can be evaluated by hand from a checkout:

```bash
python3 scripts/ci/version_gate.py gate \
    --merged-version 9.4.6 --base-version 9.4.5 \
    --revision-history documentation/revision-history.md

python3 scripts/ci/version_gate.py check-open --file inventory/group_vars/all.yml

python3 scripts/ci/version_gate.py check-tag --tag v9.4.6 --file inventory/group_vars/all.yml
```

Each subcommand prints a JSON verdict and exits non-zero when the gate fails. With no
`--tags-file`, the tags of the local repository are used. Unit tests live in
[`scripts/ci/test_version_gate.py`](../../../scripts/ci/test_version_gate.py) and run with
`pytest -q scripts/ci`.

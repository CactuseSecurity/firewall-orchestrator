# CI: Version gate and version tag guard workflows

This describes the behavior of
[`.github/workflows/version-gate.yml`](../../../.github/workflows/version-gate.yml) and
[`.github/workflows/version-tag-guard.yml`](../../../.github/workflows/version-tag-guard.yml),
which enforce the rules defined in [Versioning policy](../versioning.md).

The rules themselves live in
[`scripts/ci/version_gate.py`](../../../scripts/ci/version_gate.py) so that they can be unit
tested; both workflows only feed it files and publish its verdict.

## The rule that is enforced

A product version is **open** until a sealing tag for it exists. A pull request may only merge
onto an open version, and may only open a new version once the previous one has been sealed.

- Sealing tags: `vX.Y.Z` and `vX.Y.Z-dev` (the `v` prefix is optional)
- Snapshot tags such as `vX.Y.Z-rc1` or `vX.Y.Z-beta` do **not** seal a version

## Version gate workflow

### Triggers

| Trigger | What it does |
| --- | --- |
| `pull_request_target` on `develop` (`opened`, `synchronize`, `reopened`, `ready_for_review`, `edited`) | evaluates that one pull request |
| `push` of a tag | re-evaluates every open pull request, but only when the tag seals a version |
| `workflow_dispatch` | re-evaluates every open pull request, or the single one given as the `pr` input |

Both producers write the same commit status context, **`fwo/version-gate`**, on the pull
request head commit. That is what makes the gate resistant to stale results: a sealing tag
created after a pull request last ran flips its status to red without any pull request event,
which is exactly the situation an ordinary required check would miss.

### What is compared

`scripts/ci/post_version_status.sh` resolves three inputs and hands them to the gate:

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

### Verdicts

| Case | Condition | Status |
| --- | --- | --- |
| any | `V` is a valid `major.minor.patch` | failure otherwise |
| `V == P` | no sealing tag for `V` exists | failure otherwise: bump `product_version` |
| `V != P` | `V > P` | failure otherwise: version must not go backwards |
| `V != P` | a sealing tag for `P` exists | failure otherwise: seal `P` first |
| `V != P` | no sealing tag for `V` exists | failure otherwise: choose a higher version |
| `V != P` | `documentation/revision-history.md` ends with a `## V - DD.MM.YYYY` section | failure otherwise |
| any | `refs/pull/<n>/merge` exists | failure otherwise: resolve the merge conflicts |

The revision history is only checked when the version changes, which keeps the check objective
and free of false positives.

When re-evaluating after a sealing tag, failing pull requests are the expected outcome, so the
loop does not abort and the job itself stays green. The verdict lives in the commit statuses.

### Security

The pull request job runs under `pull_request_target` purely to obtain `statuses: write` for
pull requests from forks. It checks out the **base** branch, never the pull request head, and
reads pull request content with `git show` as inert data. No fork code is executed, no
`allow-unsafe-pr-checkout` is used, and no secret beyond the job's own `GITHUB_TOKEN` is
available. The trusted-fork gate that `sonarcloud-pr.yml` needs is therefore not required here.

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
tag is pushed, and any direct push that bypassed the required status check.

## Required repository configuration

- Branch protection on `develop` must require the status check **`fwo/version-gate`**.
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

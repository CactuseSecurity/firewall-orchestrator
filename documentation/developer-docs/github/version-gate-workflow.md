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
the version and upgrade-file inputs before it invokes the gate:

| Input | Source |
| --- | --- |
| merged version `V` | `product_version` in `inventory/group_vars/all.yml` at `refs/pull/<n>/merge` |
| base version `P` | `product_version` in the same file at the base branch tip |
| merged upgrade files | names in `roles/database/files/upgrade/` at `refs/pull/<n>/merge` |
| changed upgrade files | names in that directory the pull request adds or modifies |
| sealed versions | `git ls-remote --tags origin`, so no tag objects are fetched |

Reading `V` from the **merge result** rather than from the pull request head is deliberate. A
pull request that never touched `all.yml` inherits the base version automatically, so it is not
falsely blocked for being out of date, and only pull requests that actually change the version
are held to the bump rules. This also makes the "require branches to be up to date before
merging" branch protection setting unnecessary for the gate.

All inputs are resolved when the job runs. Nothing is baked into the check run, which is
what lets a plain re-run produce a different, correct verdict later.

### Verdicts

| Case | Condition | Job |
| --- | --- | --- |
| any | `V` is a valid `major.minor.patch` | fails otherwise |
| `V == P` | no sealing tag for `V` exists | fails otherwise: bump `product_version` |
| `V != P` | `V > P` | fails otherwise: version must not go backwards |
| `V != P` | a sealing tag for `P` exists | fails otherwise: seal `P` first, see [Sealing a version](../versioning.md#sealing-a-version) |
| `V != P` | no sealing tag for `V` exists | fails otherwise: choose a higher version |
| any | no upgrade file is named above `V` | fails otherwise: it would never be selected |
| any | no upgrade file the pull request adds or modifies is named below `P` | fails otherwise: put the change in `V.sql` |
| any | every `.sql` file the pull request adds or modifies is named `major.minor.patch.sql` | fails otherwise: put the change in `V.sql` |
| any | the pull request deletes no upgrade file named at or below `V` | fails otherwise: restore it, then empty or correct `V.sql` |
| any | `refs/pull/<n>/merge` exists | fails otherwise: resolve confirmed conflicts or retry a transient failure |

The gate reads `documentation/revision-history.md` not at all. A pull request is still expected
to document its change there, but that is left to review rather than made a merge condition, so
no automation exemption is needed for Dependabot or `.agents` pointer pull requests either.

The upgrade-file rules follow the selection in
[`roles/database/tasks/upgrade-database.yml`](../../../roles/database/tasks/upgrade-database.yml),
which runs a script when its version is at least the installed version and at most
`product_version`. A script above `V` is never selected. A script the pull request adds *or
modifies* below `P` is skipped by every installation that has already taken `P` - the case where
another pull request opens a higher version and merges first, leaving this one with a file that no
upgraded installation runs. Both are silent at run time, which is why they are caught here.

Both upgrade-file inputs are read with a path-limited `git ls-tree` / `git diff`, so a missing
directory yields an empty listing while a real git failure stops the job. The rule is never
silently switched off by an unreadable listing.

Every `.sql` file the pull request adds or modifies must be named after a full
`major.minor.patch` version and sit directly in the upgrade directory. The play globs *every*
`*.sql` in that one directory and compares its stem with the installed version, so the name is
not a label this gate could ignore: a padded `9.4.07.sql` lands at `9.4.7` for the play while
this gate reads no version at all, a patchless `9.0.sql` is read differently by each,
`readme.sql` makes Ansible's comparison fail outright, and a script in a subdirectory is never
globbed. Both upgrade inputs are read recursively so that such a script is judged on its name
rather than mistaken for one the pull request deleted, and NUL separated so that a name git
would C-quote - anything holding a non-ASCII byte or a control character - reaches the rules as
itself rather than as a quoted path that matches none of them. Only touched files are held to
this, so the thirteen patchless and padded names this repository carries from its 5.1 to 9.3
releases stay as they are.

An upgrade script the pull request removes is refused too: every installation older than that
script's version loses those operations, and the upgrade play says nothing about it. That covers
the still open version's own script, which a colleague's installation may already have run. The
one exception is a script named above `product_version`: the play has never selected it, so no
installation can have run it, and removing it is the only way to clear one that reached the base
branch around the gate - the above-`V` rule refuses every merge result still carrying it. The
verdict asks for the scripts back first and then names both ways forward - empty the current
version's script rather than remove it, or correct an older one from the current version's
script - because one deletion can hold scripts of both kinds. The diff is taken with
`--no-renames`, so moving a released script is a deletion here rather than a rename that shows
only its new name.

The rules differ in what they look at. The above-`V` rule judges every upgrade file in the merge
result, because any of them being unreachable is a fact about the merge result rather than about
this pull request. The below-`P` and naming rules judge only the files the pull request adds or
modifies: appending statements to an older script strands them exactly as adding one does, which
comparing name listings cannot see, while a script the pull request leaves alone must keep its
name rather than be renamed by whoever touches the directory next. Names that do not carry a
version at all are left to the upgrade play, and a non-`.sql` file removed from the directory is
not treated as a deleted upgrade script.

The merge ref is fetched three times because GitHub computes it asynchronously. If all attempts
fail, the workflow queries the pull request's `mergeable` state. It reports merge conflicts only
when GitHub returns `CONFLICTING`; `UNKNOWN`, `MERGEABLE`, and query failures remain fail-closed
but ask for a retry because the ref may still be computing or its fetch may have failed.

### Security

The job runs under `pull_request_target` but holds a **read-only** token with contents and pull
request access: it publishes nothing and needs no write permission. It checks out the **base**
branch, never the pull request head, and reads pull request content with `git show` as inert data.
No fork code is executed and no
`allow-unsafe-pr-checkout` is used.

Checking out the base branch also means the gate logic itself comes from `develop`. A pull
request cannot edit `version_gate.py` to make itself pass.

## Version gate refresh workflow

Runs when `develop` advances, on any pushed tag, and on `workflow_dispatch` (optionally for a
single `pr`).

Advancing `develop` can change an open pull request's merge result and turn a stale failure into
a passing verdict. Creating a sealing tag closes a version and must turn any pull request that
would still merge onto it from green to red. Neither change sends the affected pull requests an
event of their own, so this workflow re-runs the Version gate workflow for each of them with
`gh run rerun`. That rewrites the same check run in place rather than adding a second signal.

It always refreshes after a push to `develop`, skips tags that do not seal a version, and needs no
checkout at all. For each open pull request it asks GitHub for only the newest gate run matching
that head SHA:

```bash
gh api ".../workflows/version-gate.yml/runs?event=pull_request_target&head_sha=$head_sha&per_page=1"
```

Filtering server-side keeps refresh cost bounded by the number of open pull requests rather than
the workflow's complete historical run count.

A run that is still `queued` or `in_progress` is not left alone. Nothing orders a gate run against
the push or tag that starts this refresh, so such a run may already have fetched the old base ref
or read the tag list before the seal, and no later event would re-run it. The refresh polls it
every `RUN_WAIT_SECONDS` and then re-runs it like any completed run. If it has not finished in
time, that pull request is counted as unrefreshed and the job fails, rather than its result being
taken as fresh.

`RUN_WAIT_BUDGET_SECONDS` bounds that waiting for the **whole loop**, not per pull request: the
loop is serial, so a per-pull-request budget would multiply by the number of open pull requests
and could outlast the job limit - and a cancelled job is the one outcome this loop is built to
avoid, because the pull requests it never reached would be neither refreshed nor named. The
budget counts the seconds the loop actually sleeps, not wall-clock time, so its own API traffic
does not spend it and the room to wait does not shrink as more pull requests are open. That is
also why the step refuses a `RUN_WAIT_SECONDS` below one second before it starts: nothing else
would advance the budget, and the wait would run until the job limit cancels it. Once the
budget is used up, the remaining unfinished runs are counted and named in one pass, and pull
requests whose run is already complete are still re-run.

The open pull request query is capped at 200 entries, which bounds the cost of the refresh loop.
Reaching that cap is accepted rather than treated as a failure: the job stays green and warns with
the number of open pull requests it did not refresh, counted through a single GraphQL
`pullRequests(states: OPEN).totalCount` query. Those pull requests keep their previous gate result
until an event of their own, or a manual **Version gate refresh** with their `pr` number, updates
it. When the count query itself fails, the warning names the cap without a number.

A re-run replays the workflow file from the original run, but the checkout, the tag list and the
gate script are all resolved at run time, so the verdict is current even if the workflow YAML
has since changed. If a pull request cannot be refreshed at all — the run query failed, no run
was found, or the re-run was rejected — the loop counts it and moves on to the next pull request,
and the job then fails loudly, because that pull request would otherwise keep a stale green gate.

This workflow lives in its own file on purpose: a second job inside `version-gate.yml` would add
a permanently skipped entry to every pull request.

### Security boundary

A tag-push run uses the workflow definition from the tagged commit. Because this workflow has
`actions: write` permission so that it can re-run gates, the repository tag ruleset described
below is a **mandatory security control**: it must target `*`, restrict tag creation, and allow
only trusted release maintainers to bypass that restriction. Restricting only version-shaped
tags is insufficient because the workflow receives every pushed tag before its tag-name check
runs. Without this ruleset, a repository writer could create a tag on a commit containing a
modified refresh workflow and execute it with `actions: write` permission.

## Version tag guard workflow

Both jobs report after the fact. The tag or the merge already exists, and a workflow cannot undo
either; the point is that a mistake is noticed within minutes instead of at the next release.

**`validate-tag`** runs on any pushed tag. It evaluates with the gate logic taken from `develop`
rather than from the tagged commit, so tags on older commits are still checked. It fails when:

- a version tag points at a commit whose `product_version` differs from the tag, which would
  seal the wrong version and break the invariant for every later pull request;
- a sealing tag points at a commit that is contained in neither `develop` nor `main`.

Remediation is to leave the tag where it is and move on to the next version: the version it
sealed cannot be reopened, so raise `product_version` and seal that one on the right commit.
Deleting or moving the tag is not a path anyone has - not even an organisation admin - because
the required tag-immutability ruleset blocks `deletion`, `update` and `non_fast_forward` with
no bypass actors, see [Required repository configuration](#required-repository-configuration).
In this repository that ruleset is `Disallow Tag Update or Delete`. A published release tag must
never be moved in any case.

**`audit-develop`** runs on every push to `develop` and fails when `develop`'s `product_version`
is already sealed. It catches the narrow race where a merge lands in the same moment a sealing
tag is pushed, and any direct push that bypassed the required check.

## Required repository configuration

- Branch protection on `develop` must require the check **`Gate pull request version`**.
  The name to enter is the job name; `Version gate` in front of it is only the workflow name.
- "Require branches to be up to date before merging" is not needed, see above.
- An active tag ruleset targeting `*` must enable `Restrict creations` and allow only trusted
  release maintainers to bypass it. This both gives sealing tags their authority and protects
  the tag-triggered refresh workflow's `actions: write` token. Without it, anyone who can push
  a tag can seal a version or execute a modified refresh workflow from a tagged commit.
- A **second** active tag ruleset targeting `*` must block `deletion`, `update` and
  `non_fast_forward`, with no bypass actors. It has to be separate from the creation ruleset
  because bypass actors are granted per ruleset, not per rule. A sealing tag is the record that a
  version is closed, so it has to be immutable, and the recovery advice above rests on it: it is
  why a tag on the wrong commit is answered by moving to the next version rather than by
  re-tagging.

No new secrets, apps or environments are required.

## Rollout

The repository's default branch is `main`. GitHub resolves `pull_request_target` and
`workflow_dispatch` workflows from the default branch, so merging these files only into
`develop` does not activate the pull request gate or its manual refresh entry point. They become
available after the workflow reaches `main`, normally when the next stable release tag
fast-forwards `main`.

Do not make **`Gate pull request version`** a required check until the workflow is on `main` and
every open pull request has produced its first gate run. The refresh workflow can only re-run an
existing run for a pull request's current head SHA; it cannot create that first run.

Activate the gate in this order:

1. **Before merging the workflow**, create the two mandatory tag rulesets described above: one
   targeting `*` that restricts tag creation and grants bypass only to trusted release
   maintainers, which protects the tag-triggered refresh workflow's `actions: write` permission
   from its first run, and a second one targeting `*` that blocks deletion, update and
   non-fast-forward with no bypass actors.
2. Merge the workflow into `develop`, but do not add the required status check yet. The refresh
   workflow may run for this `develop` push, but it cannot initialize pull request gates and may
   fail during this rollout phase. Re-triggering pull requests now does not help because the
   `pull_request_target` workflow is not yet on the default branch.
3. Before publishing the next stable release, configure the release GitHub App, repository
   variable, and tag-only `stable-release` environment described in the
   [repository release configuration](../versioning.md#repository-release-configuration).
4. Create the next stable `vX.Y.Z` tag on the release commit in `develop` and wait for
   **Fast-forward main to release tag** to complete. Confirm that `main` points to that commit and
   contains `.github/workflows/version-gate.yml` before continuing.
5. Open the next version in a small dedicated pull request that raises `product_version` and adds
   its revision-history section. The tag in step 4 sealed `develop`'s own version, so until that
   bump merges every open pull request that does not raise the version fails the gate. Those
   failures are genuine, and this is their single resolution rather than a per-pull-request fix.
6. Re-trigger every open pull request targeting `develop` by editing its title or description.
   The `edited` event creates its first **`Gate pull request version`** run. A new commit, reopen,
   or ready-for-review event also works.
7. Wait until every open pull request shows **`Gate pull request version`** for its current head
   SHA. Resolve genuine failures before continuing.
8. Manually run **Version gate refresh** with the `pr` input empty. Continue only when it refreshes
   every open pull request successfully, with no `no version gate run found` errors. Investigate
   the 200-pull-request limit warning before continuing if it appears.
9. Confirm that `develop` itself satisfies the upgrade-file rules, which the gate never checks
   for the base branch: no script in `roles/database/files/upgrade/` may be named above its
   `product_version`. Such a script fails every pull request, and the only in-repository exit is
   a pull request that deletes it.
10. Add **`Gate pull request version`** as a required check in the `develop` branch protection or
    ruleset. Do not enable "Require branches to be up to date before merging" for this gate.
11. Verify the required check with a pull request targeting `develop`: it must fail without a
    valid version bump onto a sealed version and pass once the pull request satisfies the
    documented rules.

The same limitation applies after GitHub deletes an old workflow run under the repository's
Actions retention policy. If an open pull request has no retained gate run for its current head
SHA, re-trigger it with one of the pull request events in step 6 before relying on the refresh
workflow again.

## Local use

The gate can be evaluated by hand from a checkout:

```bash
python3 scripts/ci/version_gate.py gate --merged-version 9.4.6 --base-version 9.4.5

python3 scripts/ci/version_gate.py check-open --file inventory/group_vars/all.yml

python3 scripts/ci/version_gate.py check-tag --tag v9.4.6 --file inventory/group_vars/all.yml
```

Each subcommand prints a JSON verdict and exits non-zero when the gate fails. With no
`--tags-file`, the tags of the local repository are used. Unit tests live in
[`scripts/ci/test_version_gate.py`](../../../scripts/ci/test_version_gate.py) and run with
`pytest -q scripts/ci`.

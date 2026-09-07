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
the version inputs, revision-history inputs, pull request identity and changed paths before it
invokes the gate:

| Input | Source |
| --- | --- |
| merged version `V` | `product_version` in `inventory/group_vars/all.yml` at `refs/pull/<n>/merge` |
| base version `P` | `product_version` in the same file at the base branch tip |
| merged revision history | `documentation/revision-history.md` at `refs/pull/<n>/merge` |
| revision-history diff | `git diff --unified=0` of that file, base tip to `refs/pull/<n>/merge` |
| merged upgrade files | names in `roles/database/files/upgrade/` at `refs/pull/<n>/merge` |
| changed upgrade files | names in that directory the pull request adds or modifies |
| sealed versions | `git ls-remote --tags origin`, so no tag objects are fetched |
| pull request identity | author, head branch and head repository from the trusted event payload |
| changed paths | the diff from the base tip to `refs/pull/<n>/merge` |

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
| `V != P` | a sealing tag for `P` exists | fails otherwise: seal `P` first |
| `V != P` | no sealing tag for `V` exists | fails otherwise: choose a higher version |
| any | no upgrade file is named above `V` | fails otherwise: it would never be selected |
| any | no upgrade file the pull request adds or modifies is named below `P` | fails otherwise: put the change in `V.sql` |
| any | every upgrade file the pull request adds or modifies carries a plain `major.minor.patch` name | fails otherwise: name it `V.sql` |
| non-automated | `documentation/revision-history.md` ends with a `## V` heading | fails otherwise |
| non-automated, section exists | the pull request adds text below that final heading | fails otherwise |
| non-automated, section opened | that new final section is not empty | fails otherwise |
| any | `refs/pull/<n>/merge` exists | fails otherwise: resolve confirmed conflicts or retry a transient failure |

A non-automated pull request that extends the existing final section must add at least one
non-empty, non-heading line below its heading; one that opens the final section must leave it
non-empty. That heading must contain the merged full `major.minor.patch` version, such as
`## 9.4.6`; a date or other trailing heading text is allowed but not required. A pull request
that keeps the version extends the existing final section, while a version bump adds a new final
section and text beneath it.

The addition is taken from the pull request's diff of the file rather than from a comparison of
the base and merged snapshots. A version bump creates a *different* final section than the base's,
so its text may legitimately repeat wording of an earlier section. Added and removed lines are
compared by their stripped text, so reordering or re-indenting entries of the final section
cancels out instead of counting as an addition. That cancellation covers the final section only:
an entry moved into it from an earlier section is text the section did not have, and counts.

A section the pull request *opens* is judged from the merged file rather than from the diff:
everything below a newly inserted final heading is text that section did not have, including an
entry that keeps its wording while moving under the new heading, which git renders as a context
line rather than as an addition. Renaming an existing heading is not opening a section, so a bump
that only rewrites the heading still fails, and a new heading with nothing beneath it fails too.

The cost of reading an opened section from the merged file is that its entries need not come from
the pull request. Inserting the new heading in the middle of the existing final section splits it
and leaves the trailing entries as context lines, which produces the same diff as moving an entry
under the new heading — `+` a blank line and `+` the heading, nothing else. The two cannot be told
apart, so accepting the reclassification the versioning lifecycle requires also accepts a bump
that only re-files existing entries. The gate enforces that an opened section is not empty, not
that its text is new.

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

A name the upgrade play reads as a version but this gate does not - `9.4.07.sql`, whose padding
Ansible's loose comparison places at `9.4.7` - is refused rather than interpreted, because leaving
it unjudged is what lets it slip past both rules above. Only files the pull request touches are
held to that, so the padded names carried over from the 5.1 releases stay as they are.

The rules differ in what they look at. The above-`V` rule judges every upgrade file in the merge
result, because any of them being unreachable is a fact about the merge result rather than about
this pull request. The below-`P` and naming rules judge only the files the pull request adds or
modifies: appending statements to an older script strands them exactly as adding one does, which
comparing name listings cannot see, while a script the pull request leaves alone must keep its
name rather than be renamed by whoever touches the directory next. Scripts the pull request
deletes are judged by neither, as they are not in the merge result, and names that do not carry a
version at all are left to the upgrade play.

The revision-history checks are waived for upstream Dependabot pull requests whose authenticated
author is `dependabot[bot]` and whose branch starts with `dependabot/`. They are also waived for
the two established `.agents` pointer automations: `CactusAutomation` on
`automation/submodule_update`, and `github-actions[bot]` on `bot/update-agents-submodule`. An
agents update qualifies only when `.agents` is its sole changed path. All of these automated pull
requests remain subject to the core version lifecycle rules.

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
the workflow's complete historical run count. Runs that are not yet `completed` are left alone,
because they will report a fresh result on their own.

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

Remediation is to delete the tag and recreate it on the correct commit. Note that this is only
possible while the tag ruleset permits it, and that a published release tag must never be moved.

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

1. **Before merging the workflow**, create the mandatory tag ruleset described above. It must
   target `*`, restrict tag creation, and grant bypass only to trusted release maintainers. This
   protects the tag-triggered refresh workflow's `actions: write` permission from its first run.
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
5. Re-trigger every open pull request targeting `develop` by editing its title or description.
   The `edited` event creates its first **`Gate pull request version`** run. A new commit, reopen,
   or ready-for-review event also works.
6. Wait until every open pull request shows **`Gate pull request version`** for its current head
   SHA. Resolve genuine failures before continuing.
7. Manually run **Version gate refresh** with the `pr` input empty. Continue only when it refreshes
   every open pull request successfully, with no `no version gate run found` errors. Investigate
   the 200-pull-request limit warning before continuing if it appears.
8. Add **`Gate pull request version`** as a required check in the `develop` branch protection or
   ruleset. Do not enable "Require branches to be up to date before merging" for this gate.
9. Verify the required check with a pull request targeting `develop`: it must fail without a valid
   revision-history addition and pass after the pull request satisfies the documented rules.

The same limitation applies after GitHub deletes an old workflow run under the repository's
Actions retention policy. If an open pull request has no retained gate run for its current head
SHA, re-trigger it with one of the pull request events in step 5 before relying on the refresh
workflow again.

## Local use

The gate can be evaluated by hand from a checkout:

```bash
git diff --unified=0 origin/develop HEAD -- documentation/revision-history.md >/tmp/fwo-revision-history.diff

python3 scripts/ci/version_gate.py gate \
    --merged-version 9.4.6 --base-version 9.4.5 \
    --revision-history documentation/revision-history.md \
    --revision-history-diff /tmp/fwo-revision-history.diff

python3 scripts/ci/version_gate.py check-open --file inventory/group_vars/all.yml

python3 scripts/ci/version_gate.py check-tag --tag v9.4.6 --file inventory/group_vars/all.yml
```

Each subcommand prints a JSON verdict and exits non-zero when the gate fails. With no
`--tags-file`, the tags of the local repository are used. Unit tests live in
[`scripts/ci/test_version_gate.py`](../../../scripts/ci/test_version_gate.py) and run with
`pytest -q scripts/ci`.

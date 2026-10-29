# Review of PR #5559: Rule Positioning Draft

**Review date:** 2026-10-27  
**Reviewed head:** `192b7cd2c156d3b6a61686d405ea0ce5a0c8068e`  
**Target branch:** `develop`  
**Pull request:** [CactuseSecurity/firewall-orchestrator#5559](https://github.com/CactuseSecurity/firewall-orchestrator/pull/5559)  
**Previous review:** [Round 1](https://github.com/CactuseSecurity/firewall-orchestrator/pull/5559#issuecomment-6440973661)

## Verdict

**Request changes.**

At the reviewed head, the PR has 12 open findings: 2 high, 6 medium, and 4 low. None of the previous F1-F10 findings were fixed, accepted, or deferred. This review adds F11 and F12.

The PR remains a draft, conflicted, and not mergeable.

## Findings

| ID | Severity | Status | Finding |
| --- | --- | --- | --- |
| F1 | high | still open | The migration remains in `9.8.1.sql` while `develop` is at 9.8.3. Existing 9.8.2/9.8.3 installations will never add `rule_position`, causing Hasura metadata application to fail. |
| F2 | high | still open | User roles can write arbitrary `rule_position` JSON, and one malformed known-type value can make every ticket query containing that task fail during deserialization. |
| F3 | medium | still open | General implementation-task updates still overwrite the server-calculated position with the client's stale or null copy. |
| F4 | medium | still open | The persisted algorithm is still the enum's implicit numeric position, so enum reordering silently changes stored meaning. |
| F5 | medium | still open | Positioning still performs synchronous, uncancelled, unbounded graph reads while holding the ticket action lock, repeated per implementation task. |
| F6 | medium | still open | Position deserialization still scans every type in every loaded assembly for each positioned task. |
| F7 | low | still open | The zone/customer strategy remains unregistered, and the new Phase 4 code adds another substantial inactive production path. |
| F8 | low | still open | Coding and formatting violations remain and increased: complexity, inline arrays, recursion/parameter limits, XML documentation, and whitespace. |
| F9 | low | still open | Customer requirements remain at repository root; agent/session notes and the new open-questions worksheet remain committed as product documentation. |
| F10 | low | still open | Tests continue asserting source text and version-specific filenames rather than behavior. |
| F11 | medium | new | The graph reader narrows PostgreSQL `BIGINT` rule IDs to C# `int`, so sufficiently large rule IDs make graph loading fail. |
| F12 | medium | new | The new ticket-network resolver rejects valid requests containing over 100 catalog group selectors instead of chunking them like the existing zone resolver. |

## Detailed Findings

### F1 (high): The upgrade path remains broken

The branch still declares product version 9.8.1 in [`inventory/group_vars/all.yml`](../../../inventory/group_vars/all.yml) and adds the column in [`roles/database/files/upgrade/9.8.1.sql`](../../../roles/database/files/upgrade/9.8.1.sql). Upstream `develop` is already at 9.8.3.

The current [version-gate check failed](https://github.com/CactuseSecurity/firewall-orchestrator/actions/runs/40569618972/job/122420042329), and GitHub reports the PR as conflicted.

The database upgrade task runs only scripts between the installed version and `product_version`. An installation already at 9.8.2 or 9.8.3 will not run a newly introduced 9.8.1 script. The Hasura metadata nevertheless exposes `rule_position`, so applying that metadata against an upgraded installation without the column fails.

Required changes:

- Rebase the branch on current `develop`.
- Move the migration into the next open upgrade version.
- Bump `product_version`.
- Update revision history and every version-specific test fixture.
- Test an upgrade beginning at the current `develop` version.

### F2 (high): Malformed stored positions can break ticket loading

The column is plain `jsonb`, and non-middleware roles can still write it. Invalid known-type values intentionally throw during deserialization, as demonstrated by `KnownPosition_InvalidAlgorithmFailsValidation` in [`RulePositionPersistenceTest.cs`](../../../roles/tests-unit/files/FWO.Test/RulePositionPersistenceTest.cs).

Because `rule_position` is included in the shared implementation-task fragments, one malformed row can make the complete GraphQL response fail to deserialize. Ticket lists, individual ticket reads, and workflow-action ticket resolution can all be affected until the database value is corrected.

Required changes:

- Make the middleware the only writer of `rule_position`.
- Remove the field from general implementation-task insert and update mutations.
- Remove the field from non-middleware Hasura insert/update permissions.
- Convert invalid stored values to `UnknownRulePosition` or `null` on read, with safe logging.
- Add a test proving that one malformed task does not prevent the remaining ticket response from loading.
- Add permission tests proving that user roles cannot write the field.

### F3 (medium): General updates overwrite calculated positions

The general mutations still carry `rule_position`:

- [`newImplementationTask.graphql`](../../../roles/common/files/fwo-api-calls/request/newImplementationTask.graphql)
- [`updateImplementationTask.graphql`](../../../roles/common/files/fwo-api-calls/request/updateImplementationTask.graphql)
- [`WfDbAccessImplTasks.cs`](../../../roles/lib/files/FWO.Services/Workflow/WfDbAccessImplTasks.cs)

After the middleware calculates and persists a position, an older UI copy can update another task field and send a stale or null `RulePosition`, clearing or replacing the calculated value.

The dedicated `updateImplementationTaskRulePosition` mutation should be the only writer. Add a regression test that calculates a position, performs an ordinary task update, and verifies that the stored position is unchanged.

### F4 (medium): Persisted algorithm values are unstable

`ProvisioningPositioningAlgorithm` members have no explicit numeric values, while the JSON converters persist `(int)value`. Inserting or reordering enum members therefore changes the meaning of already stored positions.

Give every persisted enum member an explicit stable number and add a test that pins those numbers, or store a stable symbolic name instead.

### F5 (medium): Graph loading is synchronous and unbounded

Positioning is awaited inside the workflow action while the ticket action lock is held. It passes `CancellationToken.None`, loads the complete linked rulebase graph without a size bound, and repeats the reads for each implementation task.

Large policies can therefore delay all other actions on the same ticket. The runtime impact has not been measured.

Recommended follow-up:

- Move calculation to a background queue after the action response, or introduce a strict timeout.
- Propagate the request cancellation token.
- Cache one fixed-import graph per management/gateway/import during a workflow operation.
- Define and enforce safe graph-size limits.

### F6 (medium): Type resolution repeatedly scans loaded assemblies

[`RulePositionTypeResolver.cs`](../../../roles/lib/files/FWO.Data/RulePositioning/RulePositionTypeResolver.cs) enumerates the loaded assemblies and their types for every position read. Deep copies serialize and deserialize positioned tasks, so this can repeat several times per ticket.

Build a static discriminator-to-type registry once, limited to the relevant assembly or explicit registrations.

### F7 (low): Substantial production code remains inactive

The production service collection registers the graph-only strategy, but not `CheckPointRulePositioningStrategy`. The test suite explicitly asserts that the customer strategy is absent.

The new Phase 4 network resolver, layer-guard reader, containment evaluator, and KfW recipe factory add another substantial inactive path. Either wire and validate this path in the PR that introduces it or move it to the PR that will activate it.

### F8 (low): Coding and formatting violations

The PR contains or adds:

- methods above the configured cognitive-complexity limit;
- an eight-parameter method where the limit is seven;
- recursive graph traversal without the documented bound;
- repeated inline collection expressions flagged by the repository guidelines;
- missing XML documentation on new public members;
- whitespace errors detected by `dotnet format --verify-no-changes` and `git diff --check`.

SonarCloud currently reports 45 open issues for the PR, including new complexity and nested-ternary findings.

### F9 (low): Review and session artifacts are stored as product documentation

The customer requirements Markdown and generated PDF remain at repository root. The work-package directory also contains `SESSION-HANDOFF.md`, `TODO.md`, an archived proposal, and the new open-questions worksheet.

Keep lasting technical documentation in the established documentation hierarchy. Remove generated duplicates and agent/session state. Confirm that the customer specification is authorized for publication in this public repository.

### F10 (low): Source-text contract tests are brittle

Several tests inspect the text of `Program.cs`, SQL scripts, and metadata files, including exact version-specific filenames. Formatting or moving a migration breaks these tests without changing runtime behavior, and the test installer must copy production source files solely to support them.

Prefer behavior tests that build the real service collection, resolve the registrations, apply the migration, and inspect the resulting schema and permissions.

### F11 (medium): PostgreSQL `BIGINT` rule IDs are narrowed to `int`

The database defines `rulebase_link.from_rule_id` as `BIGINT` in [`fworch-create-tables-firewall.sql`](../../../roles/database/files/sql/creation/fworch-create-tables-firewall.sql). However, `RulebaseGraphSnapshotReader.LinkWire.SourceRuleId` and `SourceRuleWire.Id` are nullable C# `int` values in [`RulebaseGraphSnapshotReader.cs`](../../../roles/lib/files/FWO.Services/RulePositioning/RulebaseGraphSnapshotReader.cs).

When a rule ID exceeds `Int32.MaxValue`, Newtonsoft deserialization fails and the reader maps the query to `QueryFailed`; positioning then reports a calculation failure.

Required changes:

- Change both wire properties to `long?`.
- Check all comparisons and collections for accidental narrowing.
- Add a GraphQL response test using a rule ID above `2_147_483_647`.

### F12 (medium): Group resolution rejects valid tickets above 100 selectors

[`CreationRuleNetworkInputResolver.cs`](../../../roles/lib/files/FWO.Compliance/RulePositioning/CreationRuleNetworkInputResolver.cs) returns `InvalidInput` when the combined number of IDs and names exceeds `FlowGroupResolutionParameters.MaxSelectors`, currently 100.

That constant limits one middleware call; it is not a documented ticket limit. The existing [`CreationRuleZoneResolver.cs`](../../../roles/lib/files/FWO.Compliance/RulePositioning/CreationRuleZoneResolver.cs) already handles the same constraint by chunking identifiers and names.

Apply the same chunking and result validation to the new resolver. Add tests for:

- 101 group IDs;
- a mixture of IDs and names exceeding 100;
- a missing or ambiguous group in a later chunk;
- cancellation between chunks.

The customer-specific strategy is not active yet, but this should be corrected before registration.

## Security Review

Trust boundaries inspected:

- the workflow action endpoint that triggers positioning;
- Hasura permissions on `request.impltask.rule_position`;
- stored JSON deserialization;
- fixed-import firewall graphs and layer-guard data;
- workflow ticket objects and catalog group expansion.

Results:

- Authentication and existing workflow phase authorization are unchanged.
- Typed GraphQL variables are used; no SQL or GraphQL text is assembled from user input.
- Type-name deserialization is not enabled.
- Diagnostics avoid echoing caught exception messages.
- GitGuardian passed and no new secret was identified.
- Authorization of `rule_position` writes remains unsafe under F2.
- Deserialization availability remains unsafe under F2.
- Resource consumption remains insufficiently bounded under F5.
- The installer and upgrade path remain broken under F1.

## Validation

| Validation | Result |
| --- | --- |
| `dotnet build --configuration Debug roles/FWO.sln --no-restore` | Passed with 0 warnings and 0 errors |
| `dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --no-build --configuration Debug` | Passed: 8,793 passed, 30 skipped, 0 failed |
| `dotnet format roles/FWO.sln --verify-no-changes --no-restore` | Failed with whitespace findings |
| `git diff --check Cactus/develop...HEAD` | Failed with five whitespace findings |
| GitHub version gate | Failed |
| SonarCloud | Failed; 45 open issues at the reviewed head |
| GitGuardian | Passed |

Two skipped unit tests cover actual `rule_position` database round trips, leaving that integration unverified.

Not validated:

- PostgreSQL/Hasura upgrade execution;
- real GraphQL round trips carrying `rule_position`;
- installer execution on a disposable VM;
- Check Point MDS/global-policy graphs;
- runtime cost on large policies;
- coverage percentage for the added code.

## Recommendations

### Fix before merge

1. F1: rebase and repair the versioned upgrade path.
2. F2: restrict writes and tolerate malformed stored data.
3. F3: remove `rule_position` from general task mutations.
4. F11: correct the `BIGINT`/`long` contract.
5. Resolve the merge conflicts, version gate, formatter failures, and Sonar quality gate.

### Fix before activating the customer-specific strategy

1. F12: chunk catalog group lookups.
2. Complete production registration and end-to-end tests under F7.
3. Validate the fixed-import guard query against real Check Point data.

### Reasonable follow-up issues

1. F5: move positioning outside the ticket lock and add cancellation, caching, and bounds.
2. F10: replace source-text assertions with behavior tests.

No maintainer has formally accepted or deferred these findings, so they remain open in this review.

## Scope and Ripple Checklist

Impacted Layers: Database/Hasura, GraphQL contracts, shared C# libraries, middleware workflow, installer/upgrade path, tests, documentation

Recommended Skills:

- `fwo-review-pr`
- `fwo-schema-change`
- `fwo-installer`
- `fwo-route-request`

Minimal Change Set:

- Move and version the `rule_position` migration correctly.
- Restrict `rule_position` writes and make reads resilient.
- Remove the field from general implementation-task updates.
- Correct graph rule-ID wire types.
- Chunk group-resolution requests.
- Resolve formatting and Sonar findings.
- Remove inactive or session-only documentation artifacts.

Ripple Checklist:

- [ ] Rebase on current `develop` and resolve workflow activation conflicts.
- [ ] Validate fresh installation and upgrade from 9.8.3.
- [ ] Apply Hasura metadata against the upgraded schema.
- [ ] Add malformed-position and write-denial tests.
- [ ] Add `BIGINT` rule-ID and multi-chunk group tests.
- [ ] Re-run formatter, build, unit tests, database integration tests, and CI.

Risk Notes:

- F1, F2, and F3 block merge.
- F11 should be fixed with the active graph reader.
- F12 must be fixed before activating the customer-specific strategy.
- F5 and F10 are reasonable follow-up issues, but no maintainer has formally deferred them.

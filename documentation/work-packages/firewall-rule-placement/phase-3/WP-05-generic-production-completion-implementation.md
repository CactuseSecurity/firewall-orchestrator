# Phase 3 WP-05 implementation — Generic production completion

## Status

Implemented and locally validated on 2026-10-19.

This package deliberately leaves the workflow caller unhooked. Phase 3 WP-06 separately plans and implements the exact workflow activation trigger after the workflow diagram is available.

## Goal

Implement and register one genuinely generic, graph-only Check Point rule-positioning capability plus an independently callable service that calculates and persists its semantic `RulePosition` for one gateway-specific implementation task. Leave that service unhooked from production workflow execution. Remove the speculative WP-04 renderer. The persisted semantic position is the terminal product output.

## Settled generic capability

The only supported production mapping in this package is:

| Target | Existing enum | Complete immutable recipe | Populated initial layer | Existing empty initial layer |
| --- | --- | --- | --- | --- |
| Check Point gateway, device type ID `9` | `ProvisioningPositioningAlgorithm.DefaultEndOfRulebase` | `EndOfBlock(CheckPointLayerScopeBlock(CheckPointLayerSelector.InitialLayer()))` | `Below` the final direct rule | `Bottom` of the initial layer |

This mapping is supported by the compiled `GatewayProvisioningSettings` default and the existing localized UI description of that default as “end of rulebase”; it is not inferred from the enum name alone.

The following values are explicitly unsupported by the graph-only catalog in WP-05:

- `Undefined`;
- `CheckPointInlineLayerPerZonePair`;
- `FortinetEndOfZone`;
- `CheckPointEndOfAppSection`;
- `CheckPointEndOfAppSectionDistinguishCommonServices`; and
- every unknown numeric enum value.

There is no fallback recipe. Unsupported values produce `PositioningMethodNotImplemented` through normal manager selection. MDS device type ID `13` is not a target gateway.

## Graph-only strategy and catalog

Do not retrofit the existing `CheckPointRulePositioningStrategy`. It remains the inactive Phase 4 zone-capable strategy and retains its designated-zone reader, creation-zone resolver, and customer-recipe catalog contract.

Add a separate graph-only catalog and a separate exact-capability strategy under `FWO.Compliance.RulePositioning.CheckPoint`:

- `ICheckPointGraphPositioningRecipeCatalog` exposes `SupportsAlgorithm` and a typed `SelectRecipe` for `RulePositioningBlockContext`/`CheckPointPositionValue`.
- `CheckPointGraphPositioningRecipeCatalog` advertises only `DefaultEndOfRulebase` and returns a newly constructed immutable recipe with the exact shape above.
- `CheckPointGraphRulePositioningStrategy` implements `IRulePositioningStrategy` and depends only on `IRulebaseGraphSnapshotReader` plus the graph catalog.

`CanHandle` returns true only for device type ID `9` and a catalog-supported algorithm. `CalculateAsync` must:

1. validate the existing `RulePositioningContext` invariants without issuing I/O;
2. read exactly one immutable graph for the effective management and gateway;
3. execute the selected recipe exactly once;
4. require a `CheckPointPositionValue` whose embedded algorithm equals the configured algorithm; and
5. translate graph, block, catalog, and unexpected failures to the existing safe public result categories consistently with the zone-capable strategy.

The generic strategy must never resolve or query a designated-zone matrix, Flow group, compliance zone, request element, or `CreationRuleZoneInput`. Add recording-fake assertions that these paths are absent, not merely unused in a happy-path test.

Register only `CheckPointGraphRulePositioningStrategy` as an `IRulePositioningStrategy`. Do not register `CheckPointRulePositioningStrategy` or its catalog. Preserve `RulePositioningManager` exact-one-match behavior, including rejection of duplicate matching registrations.

## Manager context correction

Change `RulePositioningManager` so one persisted gateway-specific implementation task is the calculation unit.

- `implementationTask.Id`, `implementationTask.ReqTaskId`, and `implementationTask.DeviceId` must be positive.
- `implementationTask.ReqTaskId` must equal `requestTask.Id`.
- `implementationTask.DeviceId` must equal `targetGateway.Id`.
- If the parent request has an explicit finite device list, that list must contain the implementation task's gateway.
- An empty parent list, an all-devices marker, or a workflow-expanded implicit selection must not invalidate a concrete implementation task.
- A combined or otherwise non-gateway implementation task with null/non-positive `DeviceId` remains inapplicable.
- The effective management remains `implementationTask.ManagementId ?? requestTask.ManagementId` and must agree with both the supplied management and the gateway's management.

This replaces the current assumption that the parent request itself must identify exactly one gateway. Retain every other existing context, task-type, settings, enum, strategy-selection, and result validation rule.

## Workflow caller and persistence boundary

Add `IRulePositioningWorkflowCaller` and `RulePositioningWorkflowCaller` in `FWO.Services.RulePositioning`. The interface exposes `Task<RulePositioningWorkflowOutcome> CalculateAndPersistAsync(WfReqTask requestTask, WfImplTask implementationTask, CancellationToken cancellationToken)`. It handles one implementation task per call and does not throw for completed calculation failures.

`RulePositioningWorkflowOutcome` contains a stable status (`NotApplicable`, `Stored`, `ClearedAfterFailure`, or `PersistenceFailed`) and the nullable existing `RulePositioningFailureCategory`. `NotApplicable` is reserved for a non-persisted/combined task or a task the finalized activation filter should never send; it performs no persistence. Every failure returned by an actual manager calculation follows the clear policy below.

The caller must:

1. reject non-persisted, combined, or internally inconsistent tasks before loading context;
2. load exactly the target gateway and its management through one narrow GraphQL read rather than relying on caller-supplied device objects;
3. derive effective management as `implTask.ManagementId ?? requestTask.ManagementId` and validate all ownership/identity relationships;
4. load effective `GatewayProvisioningSettings` with `ProvisioningSettingsManager.LoadLevelAsync<GatewayProvisioningSettings>(ProvisioningScopePath.ForGateway(management, gateway))`;
5. construct one `RulePositioningContext` and call `RulePositioningManager.CalculateAsync` exactly once; and
6. persist the result independently with the existing `updateImplementationTaskRulePosition` mutation, without constructing `WfDbAccess` and without triggering workflow state changes.

Add `roles/common/files/fwo-api-calls/device/getRulePositioningTarget.graphql` and expose it as `DeviceQueries.getRulePositioningTarget`. It accepts one positive gateway ID and returns exactly one device row with `dev_id`, `dev_name`, its `stm_dev_typ` ID/name/version, and its management's `mgm_id`, `mgm_name`, and `stm_dev_typ` ID/name/version. Map that response to `Device` and `Management`, require exactly one row, and require the returned management to match the effective management. Do not load policies, rule content, credentials, or unrelated device fields.

### Store, replace, clear, and cancellation policy

| Outcome | Persistence action | Workflow result |
| --- | --- | --- |
| Calculation success | Store the position, replacing any previous value | Continue normally |
| Any typed manager/strategy/catalog/graph failure | Write SQL/GraphQL `null`, clearing any stale value | Continue normally; report safe failure outcome |
| Unexpected non-cancellation exception before persistence | Attempt the same clear once | Continue normally if the clear succeeds; report safe failure outcome |
| Caller cancellation before the persistence mutation begins | Preserve the existing value and propagate cancellation | Existing workflow cancellation behavior |
| Cancellation requested after the mutation has begun | Await the mutation to a known result because the existing API mutation has no cancellation token | Continue/propagate according to the completed write result |
| Persistence mutation failure | Do not retry and do not roll back workflow work | Continue normally; report `PersistenceFailed` safely |

Perform a final cancellation check immediately before the mutation. A completed positioning failure must never leave a stale semantic position behind. Positioning is best-effort relative to the surrounding workflow: a positioning or persistence failure does not roll back or fail otherwise successful workflow processing.

Log only task IDs, the stable high-level outcome, and a fixed failure category. Do not log rule contents, object/group names, UIDs, credentials, raw GraphQL payloads, or exception text. Do not add an audit event; audit semantics remain a stakeholder follow-up.

## Deferred workflow activation boundary

Keep the caller unhooked in WP-05. Do not modify workflow production paths, add an event handler, invoke the caller from a controller/handler, or infer an activation signal.

WP-06 owns the later diagram review and must settle:

- the exact production method and point after a gateway-specific implementation task is durably persisted;
- how creation, automatic expansion, later target changes, and retries reach that point;
- whether `StateChangedByCreation` or another existing signal is authoritative; and
- the precise tests proving one call per applicable implementation task without duplicate calls.

The current leading candidate is a middleware-side post-persistence implementation-task creation event, but WP-05 must not adopt it. The UI transports `WorkflowActionParameters.StateChangedByCreation`, while `WorkflowController` currently reconstructs change tracking with `MarkStateChanged`, so the creation distinction may be lost.

## Dependency injection

In `roles/middleware/files/FWO.Middleware.Server/Program.cs`, register:

- `ICheckPointGraphPositioningRecipeCatalog` to `CheckPointGraphPositioningRecipeCatalog`;
- `CheckPointGraphRulePositioningStrategy` as the sole production `IRulePositioningStrategy`;
- `RulePositioningManager`;
- `ProvisioningSettingsManager`;
- `IRulePositioningWorkflowCaller` to `RulePositioningWorkflowCaller`.

Register all of these as singletons. They depend only on the existing singleton `ApiConnection`, immutable/stateless collaborators, and constructor-supplied strategy collections; no service may capture request-scoped mutable workflow state.

Keep the existing zone-reader/resolver registrations because compliance features use them. Registration alone must not connect them to positioning. Do not register the zone-capable strategy or its recipe catalog.

## Graph interpretation audit

Do not refactor `CheckPointRulebaseGraphInterpreter` into `RuleTreeBuilder` in this package.

The audit found no safe neutral seam: `RuleTreeBuilder` is mutable and report/presentation-oriented, uses cached construction, generates synthetic nodes/numbering, includes NAT structure, treats a missing initial rulebase as a warning/empty tree, and tolerates unresolved links. The positioning interpreter consumes one immutable snapshot, excludes NAT/display concerns, and deliberately fails missing, ambiguous, or malformed topology. Sharing it would weaken positioning guarantees or expand WP-05 substantially.

Retain the normalized `Rulebase`/`RulebaseLink` semantics as the common source of truth. Record future extraction only if a genuinely neutral immutable topology primitive emerges.

## Empty and invalid graph behavior

Add a representative reader fixture for an imported Check Point gateway whose initial access layer exists but has zero structural rules. Carry it through `RulebaseGraphSnapshotReader`, the graph-only strategy, and the production recipe; it must yield `Bottom` for that layer.

Keep these cases distinct:

- existing empty initial layer: success with `Bottom`;
- populated initial layer: success with `Below` the final direct rule;
- missing initial layer: `CalculationFailed` and stale-position clear;
- multiple/ambiguous initial layers: `CalculationFailed` and clear;
- malformed ownership/link/order/identity data: `CalculationFailed` and clear.

Do not synthesize a missing layer and do not fall back to another layer or scope.

## Renderer removal

Delete:

- `roles/lib/files/FWO.ExternalSystems/CheckPoint/CheckPointRulePositionRenderer.cs`; and
- `roles/tests-unit/files/FWO.Test/CheckPointRulePositionRendererTest.cs`.

Remove `CreateRequestStringDoesNotCallCheckPointClient` from `roles/tests-unit/files/FWO.Test/CheckPointTicketTest.cs`; repository history shows it was introduced only as a WP-04 renderer compatibility assertion. Retain the pre-existing rule-change rejection test and the Check Point execution gate. Do not add a replacement renderer or vendor request adapter.

Historical WP-04 documents and decisions remain as superseded evidence; active roadmap/handoff text must say the renderer was removed when implementation completes.

## Exact change set

### Add

- graph-only catalog interface and implementation under `roles/lib/files/FWO.Compliance/RulePositioning/CheckPoint/`;
- graph-only Check Point strategy in the same directory;
- workflow caller contract, implementation, and typed outcome under `roles/lib/files/FWO.Services/RulePositioning/`;
- `roles/common/files/fwo-api-calls/device/getRulePositioningTarget.graphql` and its `DeviceQueries` binding;
- focused catalog, strategy, caller, DI, and end-to-end graph-only tests; and
- the representative imported empty-policy fixture.

### Modify

- `roles/lib/files/FWO.Services/RulePositioning/RulePositioningManager.cs`;
- `roles/tests-unit/files/FWO.Test/RulePositioningManagerTest.cs`;
- `roles/tests-unit/files/FWO.Test/RulebaseGraphSnapshotReaderTest.cs`;
- `roles/middleware/files/FWO.Middleware.Server/Program.cs`;
- `roles/lib/files/FWO.Api.Client/Queries/DeviceQueries.cs`;
- `roles/tests-unit/files/FWO.Test/CheckPointTicketTest.cs`; and
- active roadmap, decision, handoff, and revision-history documentation after implementation.

### Delete

- `roles/lib/files/FWO.ExternalSystems/CheckPoint/CheckPointRulePositionRenderer.cs`;
- `roles/tests-unit/files/FWO.Test/CheckPointRulePositionRendererTest.cs`.

No database, Hasura metadata, importer, UI, UI-help, installer, LDAP, product-version, or provisioning-enum change is required.

## Required tests

- Catalog supports exactly `DefaultEndOfRulebase` and produces the exact recipe tree; every named unsupported and unknown value is rejected.
- Graph-only strategy handles only Check Point gateway type `9`, performs one graph read and one recipe execution, preserves the algorithm, and translates every failure safely.
- Tests prove no designated-zone query, creation-zone resolution, Flow-group expansion, compliance lookup, or request-element dependency occurs.
- Manager accepts a concrete gateway implementation task for explicit finite, empty, all-device, and workflow-expanded parent selection forms; it rejects mismatches, null/non-positive targets, invalid effective management, invalid algorithms, zero matches, and duplicate matches.
- Reader-to-strategy tests cover populated and representative imported empty initial layers plus missing, ambiguous, and malformed structures.
- Caller loads the exact gateway/management and effective settings, calls the manager once, stores/replaces success, clears every completed failure, preserves on pre-write cancellation, awaits an initiated write, and returns `PersistenceFailed` without retry or workflow rollback.
- Tests prove the registered caller remains unreferenced by workflow controllers/handlers and can only be invoked explicitly through its service interface.
- Unsupported vendors/configurations remain safe and existing non-positioning zone consumers continue working.
- DI has exactly one matching production strategy and no zone-capable strategy registration.
- Renderer source/test types are absent, the renderer-only ticket assertion is absent, the retained rule-change execution gate still passes, and no `CheckPointClient`, `add-access-rule`, publish, or installation call is reachable.
- Diagnostics contain only approved IDs/categories and no sensitive content.

## Validation

Run, in order:

1. focused catalog, strategy, manager, caller, graph reader, DI, retained Check Point gate, and compliance regression tests;
2. focused coverage with more than 80% line coverage for every new production class;
3. `dotnet format roles/FWO.sln` and confirm only intended formatting changes;
4. `dotnet build roles/FWO.sln --configuration Debug --no-restore`;
5. `dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --no-build --configuration Debug`; and
6. `git diff --check` plus searches proving renderer/client/zone exclusions.

No new browser, deployed middleware, database-schema, installer, or disposable-VM test is introduced by WP-05. The outstanding Phase 1 disposable-VM fresh-install and `9.7.2` to `9.7.3` upgrade gates remain separately required through the approved installation-test workflow.

### Validation evidence

Validated locally on 2026-10-19:

- focused WP-05 selection: 82 passed, zero failed;
- focused Coverlet line coverage: graph-only strategy 100%, graph-only catalog 100%, workflow outcome 100%, manager 100%, and workflow caller 95.55%;
- `dotnet format roles/FWO.sln --no-restore`: completed successfully, with unrelated formatter-only changes removed;
- `dotnet build roles/FWO.sln --configuration Debug --no-restore`: succeeded with zero warnings and zero errors;
- complete unit suite: 7,931 passed, 29 skipped, zero failed; and
- `git diff --check` and production exclusion searches: passed.

## Acceptance criteria

- `DefaultEndOfRulebase` for Check Point gateway type `9` calculates and persists the exact semantic end-of-initial-layer position.
- Unsupported capabilities fail safely without fallback and clear stale positions after completed attempts.
- Existing empty layers produce `Bottom`; missing, ambiguous, and malformed layers fail explicitly.
- Generic calculation has no designated-zone, Flow-group, request-element, or compliance-zone prerequisite.
- The calculation/persistence caller is registered and independently testable but has no production workflow invoker.
- Positioning and persistence remain best-effort relative to workflow success.
- The persisted position is the final output.
- The WP-04 renderer is removed; no vendor request body can be rendered and no firewall API, publish, or installation action is reachable.
- All prescribed tests, format, build, coverage, and exclusion checks pass.

## Out of scope

- Customer-document analysis and customer-specific recipes.
- Zone-input loading or mapping, which belongs to Phase 4.
- Any replacement for the removed WP-04 renderer.
- Check Point API execution, publish, or policy installation.
- New settings, enums, schema fields, UI, UI help, audit events, or installer/version changes.
- Resolving the separate Phase 1 VM validation debt.

# Phase 3 WP-06 implementation — Workflow activation

## Status

Implemented and locally validated on 2026-10-20.

The implementer needs only this brief plus `.agents/AGENTS.md`, the Phase 3 roadmap, `DECISIONS.md`, and `SESSION-HANDOFF.md`. Do not reopen the workflow diagram or the customer document.

## Goal

Invoke the WP-05 `IRulePositioningWorkflowCaller` once for each newly created, durably persisted, applicable gateway-specific access-rule implementation task. Keep positioning best-effort and preserve every existing workflow result and state action.

## Authoritative activation seam

Use the implementation-task creation event already emitted by `WfDbAccess.AddImplTaskToDb` through `ActionHandler.DoStateChangeActions`.

The exact sequence is:

1. `AddImplTaskToDb` inserts the implementation task and assigns its positive ID.
2. It persists implementation elements and comments and writes the existing change-history entry.
3. It calls `MarkCreatedStateChanged` and passes both the created task and its effective creating `WfReqTask` to `DoStateChangeActions`.
4. A UI-hosted handler delegates the event to `WorkflowController.Actions`; it never calculates locally.
5. `WorkflowController` validates and claims the persisted transition, restores creation-aware state tracking, and invokes middleware `DoStateChangeActions`.
6. A middleware-hosted handler records the state execution, filters applicability, awaits `CalculateAndPersistAsync`, contains all activation exceptions, resets change tracking, and continues existing on-set/on-leave actions.

Place activation after `RecordStateChangeExecution` and before `ResetStateChanged`/`PerformStateActions`. This provides the durable task ID and complete nested data, lets the existing claim suppress UI replays, and ensures a later configured state-action failure cannot prevent the positioning attempt.

Do not invoke the caller directly from a Razor component, `WfHandler` creation branch, controller action branch, renderer, or vendor client.

## Caller resolution and contract correction

`Program.cs` already registers `IRulePositioningWorkflowCaller` and assigns the built middleware provider to `FWO.Services.ServiceProvider.Services`. Keep that registration unchanged.

Extend `ActionHandler` with an optional `IRulePositioningWorkflowCaller` constructor argument for focused tests. When no override is supplied and `useInMwServer` is true, resolve the existing singleton from `FWO.Services.ServiceProvider.Services`. Never resolve or run it in a UI-hosted handler.

The hook must filter before calculation. Make the existing pure `RulePositioningWorkflowCaller.IsApplicable` predicate public through `IRulePositioningWorkflowCaller` and reuse it in both the hook and `CalculateAndPersistAsync`. Do not copy or weaken the predicate and do not change context loading, calculation, persistence, clearing, or typed outcomes.

## Creating request-task context

Change `WfDbAccess.AddImplTaskToDb` to require the creating `WfReqTask` before its optional stored-ticket argument. Update every production caller:

- `WfHandler.AddImplTask` passes `ActReqTask`;
- `CreateGenericImplTask` passes its `reqTask`;
- `CreateAccessImplTask` passes its `reqTask`; and
- `CreateObjectImplTask` passes its `reqTask`.

Pass that request task as an optional final argument to `ActionHandler.DoStateChangeActions`. Middleware-owned automatic creation therefore retains the exact transient merged-bundle context.

For a delegated UI event, no request object crosses the middleware boundary. Add `WfHandler.ResolveRulePositioningRequestTask(WfImplTask)` in `WfHandlerImplTasks.cs`:

- find the persisted parent by `ReqTaskId` in `ActTicket`, with `ActReqTask`/`TicketList` only as safe fallbacks;
- when bundling is enabled and the parent has a non-empty `FlowBundleId`, collect every member of that bundle from the loaded ticket and reuse `CreateMergedBundleRequestTask`, preserving the creation-time device union; and
- otherwise return the persisted parent unchanged.

The hook uses the explicit creating task when supplied and this resolver otherwise. Reject a supplied parent whose ID does not equal `implementationTask.ReqTaskId` rather than calculating with mismatched context.

## Middleware creation marker

Change `WorkflowController.MarkStateChanged` to accept `StateChangedByCreation`:

- for a creation request, call `MarkCreatedStateChanged(newStateId)` so the marker survives reconstruction;
- otherwise preserve the existing old-state/reset/new-state behavior; and
- reject a request that sets `StateChangedByCreation` while `OldStateId != 0` in `ValidatePersistedStateTransition`.

The hook may depend on the marker only in middleware. The source marker is produced after a successful database insert; the controller still verifies the persisted current state and claims the action execution before the event reaches the hook. No browser/Razor branch calls positioning.

## Applicability

Call `CalculateAndPersistAsync` only when all existing WP-05 checks pass:

- request-task ID and implementation-task ID are positive;
- `implementationTask.ReqTaskId == requestTask.Id`;
- `implementationTask.DeviceId` is a concrete positive gateway ID;
- `implementationTask.ManagementId ?? requestTask.ManagementId` is positive;
- both task types are `WfTaskType.access`;
- request action and implementation action are `RequestAction.create`;
- a finite explicit effective parent gateway list contains the implementation-task gateway; empty/all-device/implicitly expanded selection remains valid; and
- `requestTask.OnManagement`, when present, agrees with `requestTask.ManagementId`.

Do not add a state-ID check. The creation marker is the state condition, and workflow state IDs are configuration data.

The following never start calculation:

- an insert that did not return a positive ID;
- a combined/all-devices implementation task with no concrete gateway;
- object, generic, rule-modify, rule-delete, or other unsupported task types;
- access-rule update/delete operations;
- ordinary state changes of an existing implementation task;
- implementation-task metadata/target updates; and
- an explicit action request that is not a persisted creation transition.

Vendor type, configured positioning algorithm, graph availability, and calculation success remain caller/manager concerns. Once the cheap task-shape predicate accepts the task, those conditions produce the existing typed stored/cleared/persistence-failed outcomes.

## Duplicate, bundle, and retry semantics

- One successful `AddImplTaskToDb` insert produces one creation event for its new ID.
- Automatic per-gateway expansion calls the seam once per newly inserted task. Existing gateway tasks are skipped by current creation logic.
- A bundle creates tasks only under the existing merged parent; each created task gets one call with the merged gateway selection. Bundle siblings do not cause additional calls.
- The existing `request.state_change_execution` claim rejects a repeated UI/controller transition before middleware `DoStateChangeActions`, so double submission does not recalculate.
- Direct middleware creation does not delegate and therefore executes only its local creation event.
- `Stored`, `ClearedAfterFailure`, and `PersistenceFailed` do not schedule a retry. A caller exception also does not retry.
- Deleting and recreating a task creates a new database ID and therefore a new creation event and attempt.
- Updating a task, including changing gateway or management, does not activate or clear positioning in WP-06.
- Add no retry endpoint, state action type, button, background job, attempt column, or outbox.

The guarantee is one attempt per accepted creation event during normal process execution. Do not claim crash-durable exactly-once delivery; that would require separately planned persisted delivery state.

## Failure, logging, and cancellation

`RulePositioningWorkflowCaller` remains responsible for logging its typed outcome. It logs only implementation-task ID, `RulePositioningWorkflowStatus`, and fixed failure category. The hook must not duplicate successful/typed-outcome logs.

Call automatic post-commit activation with `CancellationToken.None`. Once creation is durable, a disconnected client must not cancel the attempt or prevent later workflow actions. This choice does not change the caller's standalone cancellation contract from WP-05.

Wrap only the hook invocation boundary:

- contain `OperationCanceledException` and every other unexpected exception;
- log one fixed warning/error containing only the implementation-task ID and no exception object, message, rule content, object/group name, UID, payload, or credential; and
- continue to reset change tracking and run all configured state actions.

If the middleware caller service or parent request task cannot be resolved, log the same safe operational failure and continue. Do not display a UI error, add an alert/audit event, change the workflow result, or throw back into `AddImplTaskToDb`.

## Exact production change set

### Shared rule-positioning contract

- `roles/lib/files/FWO.Services/RulePositioning/IRulePositioningWorkflowCaller.cs`
  - expose the pure applicability query on the interface.
- `roles/lib/files/FWO.Services/RulePositioning/RulePositioningWorkflowCaller.cs`
  - make the existing applicability predicate implement that interface member;
  - keep `CalculateAndPersistAsync` using the same predicate; and
  - make no other caller/manager/persistence change.

### Shared workflow

- `roles/lib/files/FWO.Services/Workflow/ActionHandler.cs`
  - resolve/inject the caller only for middleware execution;
  - accept optional creation-parent context in `DoStateChangeActions`;
  - add the single creation-event activation helper at the settled ordering point; and
  - contain failures without changing existing state-action execution.
- `roles/lib/files/FWO.Services/Workflow/WfDbAccessImplTasks.cs`
  - require and forward the creating request task after successful durable persistence.
- `roles/lib/files/FWO.Services/Workflow/WfHandlerImplTasks.cs`
  - update manual/generic/access creation call sites;
  - add delegated-event parent/bundle reconstruction; and
  - reuse the existing merged-bundle helper.
- `roles/lib/files/FWO.Services/Workflow/WfHandlerImplTaskTargets.cs`
  - update the object-task creation call site; object tasks remain filtered before calculation.

### Middleware

- `roles/middleware/files/FWO.Middleware.Server/Controllers/WorkflowController.cs`
  - preserve `StateChangedByCreation` while reconstructing the persisted transition; and
  - reject a creation marker with a nonzero old state.

Do not change `Program.cs`; its WP-05 caller registration and service-provider assignment are already sufficient.

## Required tests

### `RulePositioningWorkflowCallerTest.cs`

- Replace `ProductionWorkflow_DoesNotInvokeCallerBeforeWp06` with tests for the exposed pure applicability contract.
- Cover valid persisted creation; zero/mismatched IDs; null/zero gateway; missing management; access versus object/generic task; create versus update/delete; finite parent selection mismatch; all-device/empty selection; and inconsistent `OnManagement`.
- Retain all WP-05 calculation, persistence, clearing, and cancellation tests unchanged.

### `ActionHandlerTest.cs`

- A middleware creation event for one applicable persisted task calls the fake caller exactly once with the same task ID and supplied request task.
- A UI-hosted event delegates and never calls a locally injected caller.
- A non-creation state change never calls it.
- Non-persisted, combined, unsupported task/action, object, update, and delete shapes never call `CalculateAndPersistAsync`.
- `Stored`, `ClearedAfterFailure`, and `PersistenceFailed` all allow a following configured state action to execute and leave the workflow method successful.
- An unexpected exception and an `OperationCanceledException` from the fake caller are contained and still allow the following state action.
- The hook passes a non-cancellable token and records no sensitive exception text.

### `WfDbAccessTest.cs`

- Update direct `AddImplTaskToDb` calls for the required parent argument.
- Prove the hook sees a positive stored ID and the exact parent only after insert/nested persistence succeeds.
- Prove a null/missing insert result produces no calculation call.
- Preserve existing comments, history, and returned-ID coverage.

### `WfHandlerImplTasksTest.cs` and `WfHandlerImplTaskTargetsTest.cs`

- Per-gateway automatic expansion produces one calculation call per new concrete gateway task.
- Re-running expansion with existing tasks produces no additional call.
- Combined/all-device task creation produces no call.
- A multi-request bundle supplies the merged gateway list and invokes once per actual persisted task, not once per bundle member.
- Object-management expansion remains uninvoked.
- Manual `AddImplTask` reaches the same seam once.

### `WorkflowMiddlewareUnitTest.cs`

- `MarkStateChanged` preserves ordinary old-state tracking.
- `MarkStateChanged` restores `StateChangedByCreation` for `0 -> initial-state` reconstruction.
- Validation rejects a creation marker with nonzero old state.
- Retain the existing state-execution claim/replay tests and add an assertion that a refused duplicate never reaches the activation helper/caller.

Use a small recording `IRulePositioningWorkflowCaller` fake in the test assembly. Do not make graph/settings fixtures part of workflow-hook tests; WP-05 already covers the real caller.

## Validation

Run from the repository root:

```powershell
dotnet format roles/FWO.sln
dotnet build --configuration Debug roles/FWO.sln
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj
git diff --check
```

Run focused tests for `RulePositioningWorkflowCallerTest`, `ActionHandlerTest`, `WfDbAccessTest`, `WfHandlerImplTasksTest`, `WfHandlerImplTaskTargetsTest`, and `WorkflowMiddlewareUnitTest` before the complete suite. Collect Coverlet line coverage for new production branches and keep it above 80%.

Run exclusion searches proving that WP-06 added none of the following to the activation path:

- `CreationRuleZoneInput`, `CreationRuleZoneResolver`, designated-zone/matrix or Flow-group resolution;
- a Check Point renderer, `CheckPointClient`, `add-access-rule`, publish, or policy installation;
- a new REST route, Razor/UI control, retry job, state-action enum, schema/Hasura mutation, setting, audit event, installer/version file; or
- customer-document identifiers or customer-specific recipes.

No UI help update or disposable-VM installer test is required because this package changes no user-facing control and no installation artifact.

## Implementation evidence

- The middleware-only creation hook runs after state-execution recording and before change tracking is reset or configured state actions execute.
- Direct creation forwards its exact request task; delegated creation reconstructs the persisted parent and merges bundle gateway selections from the loaded ticket.
- Focused tests for the six required fixtures passed: 395 passed, zero failed.
- Focused Coverlet results exceeded 80% for every new production seam: activation 91.2% line / 94.4% branch, durable creation 90.5% / 92.9%, delegated parent resolution 100% / 94.4%, and applicability plus middleware marker methods 100% / 100%.
- `dotnet format roles/FWO.sln --no-restore` completed successfully; unrelated formatter-only changes were removed.
- `dotnet build --configuration Debug roles/FWO.sln` completed with zero warnings and zero errors.
- The complete unit suite passed: 7,977 passed, 29 skipped, zero failed.
- `git diff --check` passed. Added-production-line exclusion searches found no zone/matrix/Flow-group resolution, renderer/vendor execution, endpoint/UI/retry/schema/setting/audit/installer/version, or customer-specific additions.

## Acceptance criteria

- The authoritative middleware creation event invokes WP-05 exactly once for each applicable persisted gateway-specific implementation task in normal execution.
- Manual creation, automatic device expansion, bundles, monitoring, configured state actions, and middleware/external workflow creation use the same seam.
- Unsupported, combined, non-persisted, update/delete/object, and ordinary state-change cases never start calculation.
- Repeated controller requests and repeated expansion do not duplicate calls.
- Positioning outcomes, cancellation, and unexpected exceptions never roll back or fail successful workflow work.
- The hook contains no calculation, graph-loading, settings-loading, persistence, customer, zone, rendering, or firewall-execution logic.

## Out of scope

- Recalculation or clearing after target, management, rule-content, or metadata edits.
- Manual/automatic retry after a completed or interrupted attempt.
- Crash-durable delivery/outbox semantics.
- Changes to WP-05 recipes, strategies, graph loading, persistence mutation, or stale-position policy.
- Customer-document analysis, zone-dependent positioning, rendering, vendor API calls, publish, or installation.
- UI, help, endpoint, schema, Hasura, setting, enum, audit, installer, or version changes.

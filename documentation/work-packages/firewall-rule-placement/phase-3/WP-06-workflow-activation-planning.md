# Phase 3 WP-06 planning — Workflow activation

## Status

Planning completed on 2026-10-20. The workflow diagram and repository paths identify one post-persistence creation event, and the paired implementation brief is ready.

This planning session changed documentation only. It did not implement the production hook and did not inspect `FWO_Regel_Platzierung_Regelwerk.md`.

## Planning objective

Connect the completed WP-05 `IRulePositioningWorkflowCaller` to one authoritative server-side workflow event so every applicable, durably persisted gateway-specific implementation task receives exactly one best-effort calculation/persistence attempt.

Do not reopen WP-05 recipe, strategy, context-loading, persistence, clearing, cancellation, or renderer decisions unless repository evidence proves the implemented contract unusable. Do not inspect `FWO_Regel_Platzierung_Regelwerk.md`; customer behavior belongs to Phase 4.

## Required input

- The user's workflow diagram.
- The implemented WP-05 caller contract and validation evidence.
- Current implementation-task creation, automatic expansion, update, retry, controller, and state-change paths.

## Required discovery

- Trace every path that can durably create a gateway-specific implementation task for a new access-rule request.
- Determine whether all relevant paths converge after persistence or need one neutral domain/service seam.
- Establish whether activation occurs only on initial creation or also after target/management changes, recreation, or an explicit retry.
- Verify whether `WorkflowActionParameters.StateChangedByCreation` survives the middleware boundary and is authoritative. The current repository evidence shows `WorkflowController.MarkStateChanged` may reconstruct state without preserving that distinction.
- Define exact applicability filtering before calling WP-05: persisted positive implementation-task ID, concrete gateway ID, supported task operation/type, and any necessary state condition.
- Define duplicate-call/idempotency behavior for UI retries, repeated controller requests, bundled tasks, automatic expansion, and partial workflow failures.
- Confirm where the caller's typed result is logged and that failure remains best-effort without rolling back workflow work.
- Identify exact production and test files. Prefer one server-side integration point; do not scatter calls among UI branches.

## Decisions the planner must settle

1. The exact server-side method/event that invokes the caller after durable implementation-task persistence.
2. Every creation/update/retry path that should and should not invoke it.
3. Applicability filtering and one-call-per-task guarantees.
4. Behavior when workflow retries after task persistence or after a positioning/persistence failure.
5. Whether an explicit manual retry capability is needed; default to no new endpoint or UI unless the diagram demonstrates an existing authorized retry path.
6. Exact logging, cancellation, and exception containment at the hook.
7. Focused regression tests for manual creation, automatic device expansion, bundles, duplicate submissions, unsupported tasks, and workflow success despite positioning failure.

## Fixed boundaries

- Reuse `IRulePositioningWorkflowCaller`; do not duplicate context loading, calculation, or persistence in workflow code.
- Positioning remains best-effort. Its outcome cannot fail or roll back otherwise successful workflow persistence/state changes.
- Invoke only after the implementation task has a durable positive ID.
- Do not change the generic recipe, registered strategy, graph reader, persistence mutation, or stale-position policy.
- Do not add customer-specific logic, zone resolution, rendering, `CheckPointClient`, publish, or policy installation.
- Do not add schema, Hasura metadata, settings, enum, installer, version, UI, or audit changes unless the diagram proves one indispensable; route any such discovery separately before implementation.
- No production hook may depend on client/UI-only state.

## Diagram and repository findings

The supplied `kfw-workflow-fw-request.drawio (1).pdf` places vendor-dependent rule-position calculation in the automatic planning phase. It separately shows one implementation task per gateway for access requests (or per management for object requests), persistence of management/gateway/position references, later plan display, later optional human modification, and vendor conversion/API calls only in the automatic implementation phase. It shows no manual positioning retry. Initial implementation-task creation is therefore the WP-06 event; later edits, rendering, execution, and review are not.

Every persisted implementation-task creation path converges on `WfDbAccess.AddImplTaskToDb`: manual planner creation, access-task automatic expansion, generic fallback creation, and object-task target-management expansion. That method assigns the positive ID, persists nested data and history, calls `MarkCreatedStateChanged`, and invokes `ActionHandler.DoStateChangeActions`.

`DoStateChangeActions` is the neutral event seam. A UI-hosted handler delegates the persisted transition to middleware, while a middleware-hosted handler executes it locally. One hook there covers manual UI creation, monitoring creation, configured `CreateImplTasks` actions, ticket creation actions, and external-request/internal-work progression without adding calls to UI branches.

`WorkflowActionParameters.StateChangedByCreation` already crosses the middleware contract, but `WorkflowController.MarkStateChanged` currently discards it. The implementation must preserve it. The existing `request.state_change_execution` claim rejects repeated middleware action requests before the hook and requires no schema change.

Bundled creation uses a transient request task from `CreateMergedBundleRequestTask`. Direct middleware creation must pass that exact parent into the event; delegated UI creation must reconstruct the same bundle view from the fully loaded ticket. Using only the first persisted bundle member can reject a gateway selected by another bundle member.

The WP-05 caller already logs safe typed outcomes. Its private applicability predicate is the only mandatory contract correction: the workflow hook must filter unsupported shapes before calculation without duplicating that policy.

## Settled decisions

1. `ActionHandler.DoStateChangeActions` is the sole activation event. It invokes a focused middleware-only helper after durable task/nested-data persistence and state-execution bookkeeping, but before configurable on-set/on-leave actions.
2. `WfDbAccess.AddImplTaskToDb` receives the creating `WfReqTask` explicitly and passes it with the creation event. All production call sites use this common route.
3. `IRulePositioningWorkflowCaller` exposes its existing pure applicability check. The hook calls `CalculateAndPersistAsync` only when that check succeeds.
4. Applicability remains the WP-05 policy: matching positive IDs, concrete positive gateway, positive effective management, access task on both objects, request/implementation action `create`, compatible parent gateway selection, and consistent request management.
5. Creation state is the only state condition; no workflow state ID is hard-coded.
6. Bundled parents use the existing merged-bundle semantics. Each created implementation-task ID produces one attempt.
7. Replayed UI/controller requests are rejected by the existing state-execution claim. Re-running expansion skips existing tasks. Recreating a deleted task with a new ID produces a new attempt.
8. Automatic post-commit activation uses `CancellationToken.None`. Completed outcomes and any unexpected/cancellation exception at the hook never fail task creation or later state actions; the caller's standalone cancellation contract remains unchanged.
9. No completed failure is retried automatically, and WP-06 adds no endpoint, button, action type, job, attempt record, or outbox.
10. Later implementation-task edits do not activate positioning. Target-change recalculation/clearing and manual retry require a separate product decision because the update path has no authoritative creation event and the diagram defers modify-use-case validation.

## Change triage

Impacted Layers: Shared Libraries, Middleware Server, Unit Tests, Documentation

Recommended Skills:
- `fwo-route-request`

Minimal Change Set:
- `roles/lib/files/FWO.Services/RulePositioning/IRulePositioningWorkflowCaller.cs`
- `roles/lib/files/FWO.Services/RulePositioning/RulePositioningWorkflowCaller.cs`
- `roles/lib/files/FWO.Services/Workflow/ActionHandler.cs`
- `roles/lib/files/FWO.Services/Workflow/WfDbAccessImplTasks.cs`
- `roles/lib/files/FWO.Services/Workflow/WfHandlerImplTasks.cs`
- `roles/lib/files/FWO.Services/Workflow/WfHandlerImplTaskTargets.cs`
- `roles/middleware/files/FWO.Middleware.Server/Controllers/WorkflowController.cs`
- focused files under `roles/tests-unit/files/FWO.Test/`
- active Phase 3 roadmap, decision, TODO, and handoff documents

Ripple Checklist:
- [ ] Preserve `StateChangedByCreation` across the middleware boundary.
- [ ] Reuse one applicability predicate in the hook and caller.
- [ ] Preserve merged bundle context in direct and delegated creation.
- [ ] Prove one call per new persisted gateway task and no call for excluded shapes.
- [ ] Prove replay, failure, and exception containment semantics.
- [ ] Run formatting, build, full unit tests, coverage, and exclusion searches.

Risk Notes:
- No schema, Hasura, installer, auth, UI, settings, enum, version, audit, or upgrade change is planned.
- Exactly-once means one attempt for each accepted creation event in normal process execution. Crash-durable delivery would require an outbox/attempt record and is intentionally outside this schema-free package.
- A target changed after creation can make its stored position obsolete; WP-06 records rather than silently solves that separate modify/retry concern.

## Required planning output

Replace every placeholder in `WP-06-workflow-activation-implementation.md` with a self-contained implementation brief naming the exact hook, files, sequence, applicability rules, duplicate/retry behavior, tests, and validation. Update the phase roadmap, decision log, TODO ledger, and session handoff. Mark the implementation brief ready only when no trigger question remains.

## Planning acceptance

- The diagram and repository paths identify one authoritative server-side activation seam.
- Every applicable creation route is covered exactly once, with explicit retry semantics.
- Unsupported/combined/non-persisted tasks never invoke the caller.
- Positioning failure cannot alter the successful workflow result.
- The implementer needs neither the diagram nor additional product decisions.

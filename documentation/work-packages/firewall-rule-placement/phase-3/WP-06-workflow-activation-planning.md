# Phase 3 WP-06 planning — Workflow activation

## Status

Waiting for the workflow diagram. This planning brief authorizes repository discovery and documentation changes only; do not implement production code in the planning session.

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

## Required planning output

Replace every placeholder in `WP-06-workflow-activation-implementation.md` with a self-contained implementation brief naming the exact hook, files, sequence, applicability rules, duplicate/retry behavior, tests, and validation. Update the phase roadmap, decision log, TODO ledger, and session handoff. Mark the implementation brief ready only when no trigger question remains.

## Planning acceptance

- The diagram and repository paths identify one authoritative server-side activation seam.
- Every applicable creation route is covered exactly once, with explicit retry semantics.
- Unsupported/combined/non-persisted tasks never invoke the caller.
- Positioning failure cannot alter the successful workflow result.
- The implementer needs neither the diagram nor additional product decisions.


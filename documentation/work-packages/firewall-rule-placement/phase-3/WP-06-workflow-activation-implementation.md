# Phase 3 WP-06 implementation — Workflow activation

## Status

Draft — workflow diagram and separate planning session required.

Do not implement this package until the paired planner has resolved every placeholder and marked this brief `Ready for implementation`.

## Goal

Invoke the WP-05 `IRulePositioningWorkflowCaller` from one authoritative server-side post-persistence workflow seam for each applicable gateway-specific implementation task, while keeping positioning best-effort.

## Exact activation seam

To be supplied by the WP-06 planner after reviewing the workflow diagram and tracing every implementation-task creation path. Name the exact production method, event timing, and why it covers all intended paths without duplicates.

## Applicability and retry behavior

To be supplied by the WP-06 planner. Define creation/update/retry scope, filtering, idempotency, duplicate submissions, partial failures, bundled tasks, and automatic target expansion.

## Failure and observability boundary

The hook must await the caller, record only its safe typed outcome, and allow otherwise successful workflow processing to continue for `Stored`, `ClearedAfterFailure`, `NotApplicable`, and `PersistenceFailed`. Caller cancellation follows the surrounding request's established cancellation behavior. Do not log sensitive rule/object content or raw exceptions.

## Exact change set

To be supplied by the WP-06 planner. Changes must be limited to the authoritative middleware/workflow hook, its focused tests, and active documentation unless discovery proves a mandatory shared-library correction.

## Required tests

The finalized brief must enumerate focused tests proving:

- one caller invocation after each applicable task is durably persisted;
- coverage of manual creation, automatic gateway expansion, and any diagram-confirmed creation paths;
- no invocation for non-persisted, combined, unsupported-operation, update/delete/object, or otherwise inapplicable tasks;
- no duplicate invocation from UI retries, controller reconstruction, bundles, or repeated state processing;
- settled retry behavior after caller or persistence failure;
- workflow success is retained for every completed caller outcome; and
- safe logging and cancellation behavior.

## Validation

The finalized brief must include focused workflow/caller tests, `dotnet format roles/FWO.sln`, a Debug solution build, the complete unit suite, more than 80% line coverage for new production code, `git diff --check`, and exclusion searches proving no zone, renderer, Check Point API, publish, or installation path was introduced.

## Acceptance criteria

- The authoritative workflow event invokes WP-05 exactly once for every applicable persisted gateway-specific implementation task.
- All excluded task shapes remain unhooked.
- Positioning and persistence failures never roll back or fail successful workflow work.
- The hook contains no duplicated calculation/persistence logic and introduces no customer or firewall-execution behavior.

## Out of scope

- Changes to WP-05 recipe, strategy, graph loading, or persistence policy.
- Customer-document analysis, zone-dependent positioning, or Phase 4 recipes.
- Vendor request rendering or firewall execution.
- New UI, endpoint, schema, setting, enum, audit event, installer, or version change unless separately planned and approved.

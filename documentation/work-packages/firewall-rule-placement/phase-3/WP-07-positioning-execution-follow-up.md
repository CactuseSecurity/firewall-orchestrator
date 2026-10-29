# WP-07 — Positioning execution follow-up

Status: follow-up required after PR 5559; deliberately not part of the activation fix.

## Problem

Automatic rule positioning currently runs synchronously while the middleware holds the per-ticket workflow-action lock. Each implementation task has an independent 30-second positioning timeout, so a multi-task action can retain the lock across several attempts. The Check Point layer-guard query also has no explicit cardinality bounds on nested rule associations and flattened group members.

## Required outcome

- Move expensive positioning outside the per-ticket action lock without losing an already-durable creation event. Use a durable queue or outbox if in-process execution cannot guarantee delivery.
- Define retry, idempotency, stale-position, and crash-recovery behavior before changing the trigger.
- Add explicit bounds for the layer-guard associations and flattened group-member collections, with a typed fail-closed result when a bound is exceeded.
- Measure representative large policies and tickets containing multiple applicable implementation tasks.

## Acceptance criteria

- A slow positioning calculation does not prevent unrelated actions on the same ticket for the duration of the calculation.
- Every durable applicable creation is calculated exactly once or remains durably retryable after cancellation, process failure, or restart.
- Query and in-memory work have documented upper bounds.
- Load tests cover large guard collections and multi-task tickets without weakening the existing 30-second per-calculation safety bound until a replacement budget is explicitly approved.


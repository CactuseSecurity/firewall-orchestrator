# WP-03 — Resolve exactly one source and destination zone

## Agent mission

Turn one access implementation task into the validated zone pair required for Check Point Section naming.

## Existing seam

`ComplianceZoneService.ResolveZonesForObjectsAsync` already resolves object trees against the designated compliance-zone matrix. Existing implementation-task data exposes source and destination elements separately.

## Scope

- Convert source and destination elements from a `WfImplTask`/parent `WfReqTask` into two independent zone-resolution requests.
- Reuse the existing group-aware resolver.
- Require exactly one unique active source zone and one unique active destination zone after expansion.
- Return stable zone identifiers and display names for WP-01.
- Map no match, uncovered ranges, multiple matches, unavailable matrix, and invalid task content to `ZoneResolutionFailed` with a safe detail.
- Allow identical source and destination zones.

## Constraints

The specification treats exact-one-zone as an upstream guarantee, but this adapter must validate it at the placement boundary. It must never choose the first result. Keep it callable from middleware/application code; do not depend on Razor or `MiddlewareClient` in the core service.

## Tests

Cover one/one, same-zone, no source, no destination, multiple source, multiple destination, nested groups, and an uncovered range. Verify that errors identify the failed side without exposing the complete object graph.

## Acceptance criteria

- Successful output always has exactly one source and destination zone.
- The resulting Check Point Section name is deterministic.
- Ambiguity fails before any Check Point policy read.
- Existing compliance-zone callers remain unchanged.

## Out of scope

Do not alter zone definitions, Security Matrix order, path analysis, or future vendor mapping.

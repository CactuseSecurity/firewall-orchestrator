# Phase 3 WP-01 planning - Production snapshot reader

## Status

Planning completed on 2026-10-16. The paired implementation was completed and locally validated on 2026-10-18.

## Planning objective

Produce a self-contained implementation brief for loading the normalized rulebase data belonging to the latest successfully completed import for one management and gateway, then constructing one immutable `RulebaseGraphSnapshot`.

This is a planning-only session. Do not change production code. Do not inspect `FWO_Regel_Platzierung_Regelwerk.md`.

## Required reading

1. `.agents/AGENTS.md`
2. `documentation/work-packages/firewall-rule-placement/SESSION-HANDOFF.md`
3. the roadmap and `DECISIONS.md`
4. Phase 2 WP-04
5. `phase-3/README.md`
6. this planning brief

Use `fwo-route-request`. Use `fwo-schema-change` only if the plan proves that an existing GraphQL contract cannot provide the required data and a schema or metadata change is genuinely necessary.

## Repository discovery

Identify and record:

- the existing GraphQL queries and data models that load `import_control`, `Rulebase`, `RulebaseLink`, and structural rule identity/order data;
- the canonical fields that distinguish a successfully completed import from failed or in-progress imports;
- how imported rows are scoped to one import, management, and gateway;
- how removed or historical rows and links are excluded;
- the appropriate owning service and dependency-injection boundary;
- cancellation and `ApiConnection` conventions; and
- the narrowest existing test seams for captured queries and returned data.

## Decisions the planner must settle

1. The reader interface, result type, and owning project.
2. The exact query or queries and how they guarantee that all graph data belongs to the same completed import.
3. The ordering rule for choosing the latest successful import, including deterministic tie handling.
4. The precise meaning of “successful and completed” in the current schema.
5. Failure behavior for no successful import, contradictory ownership, incomplete returned data, query failures, and cancellation.
6. Whether the reader returns normalized entities for snapshot construction or returns the constructed snapshot.
7. How safe diagnostics are propagated to WP-03 without exposing rule contents or credentials.
8. The exact unit and integration-test boundary.

Do not add import-age validation, automatic import, a fingerprint, an importer call, a Check Point API call, or customer-specific scope logic.

## Required planning output

Update `WP-01-production-snapshot-reader-implementation.md` so that it contains:

- no unresolved `TBD` items;
- exact files and public/internal contracts;
- the complete query and filtering behavior;
- typed success/failure and cancellation behavior;
- construction and validation ownership;
- concrete tests and fixtures;
- validation commands; and
- explicit non-goals.

Append architectural decisions to `DECISIONS.md`, update the phase status and `SESSION-HANDOFF.md`, and mark the implementation brief ready only when an implementer can execute it without reading this file.

## Planning acceptance

- The plan proves one calculation cannot mix data from different imports.
- The reader has one unambiguous management/gateway scope.
- Missing and malformed data fail explicitly.
- The plan reuses the existing schema unless a documented blocker requires otherwise.
- The customer document remains unopened.

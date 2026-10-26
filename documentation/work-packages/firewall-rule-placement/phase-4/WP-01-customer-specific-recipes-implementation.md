# Phase 4 WP-01 implementation — Customer-specific recipes

## Status

Draft — Phase 4 planning is in progress and expert answers are still required.

Do not implement this package until the paired planner has replaced every draft section and marked it `Ready for implementation`.

## Goal

Add isolated, side-effect-free customer-specific calculation blocks and activation-neutral recipe construction without changing the validated graph-only product, workflow activation, persistence, semantic-output boundary, or external-execution boundary.

This package does not choose which enum triggers the customer behavior, register the zone-capable strategy, or invoke customer positioning from production workflow code. Those concerns belong to a separate later selection/activation plan.

## Normative customer behavior

The current settled zone-input behavior is:

| Input | Customer calculation behavior |
| --- | --- |
| Source and destination both have `Complete` coverage and exactly one distinct persisted designated-matrix zone | Continue to canonical Section-name construction. |
| Several objects/ranges on one side all resolve completely to the same one persisted zone | Continue; cardinality concerns distinct resolved zones, not raw object count. |
| Source and destination resolve to the same persisted zone | Continue; intra-zone placement is valid. |
| Missing/empty source or destination | Fail safely; do not construct a recipe. |
| `Partial` or `None` coverage on either side | Fail safely; do not discard uncovered ranges. |
| Zero or multiple distinct zones on either side | Fail safely; do not choose the first zone. The possible customer exception for multiple source zones remains an expert question. |
| Persisted automatic Internet zone supplied by the designated matrix | Treat as an eligible persisted zone. |
| `SyntheticInternetFallback` | Fail safely because it is not a persisted designated-matrix zone. |
| Referenced Flow group is absent from the Flow catalog | Preserve the current `GroupUnresolved` failure. Pre-catalog sibling-group support requires a later explicit workflow/input decision. |

The final planner must specify exact typed failure results and complete the functional-target rows after EQ-02 is answered.

## Recipe construction and activation boundary

To be supplied by the Phase 4 planner as complete activation-neutral recipe trees and explicit zone-input requirements.

No customer enum mapping or production registration is part of this package. Direct tests must prove calculation behavior independently. Generic capabilities remain zone-independent.

## Additional block contracts

To be supplied by the Phase 4 planner. Existing exact ordinal selectors retain their behavior. Customer-required normalized matching is additive and accepts an explicit parent-layer scope.

## Exact change set

To be supplied by the Phase 4 planner. Generic activation and persistence files remain unchanged. Production DI registration is excluded.

## Required tests

The final brief must enumerate acceptance scenarios for every settled customer behavior and recipe branch, all additional blocks, zone-set mapping and precedence, empty/missing/ambiguous scopes, cancellation, safe failures, and regression coverage proving the generic Phase 3 capability remains unchanged and zone-independent.

Tests must also prove that no customer strategy or enum mapping is registered.

## Validation

The finalized brief must provide focused commands, repository formatting/build/unit-test commands, relevant integration checks, and more than 80% line coverage for new production code.

## Acceptance criteria

- Every approved customer scenario produces the specified semantic position or typed safe failure.
- Customer calculation primitives extend rather than modify existing structural behavior.
- Customer calculation is directly testable but remains inactive in production.
- Zone readers and resolvers are not newly activated by this package.
- No renderer, Check Point client, publish, or installation action is activated.

## Out of scope

- Redesigning generic workflow activation or persistence.
- Customer enum mapping, strategy registration, or workflow selection/activation.
- Reintroducing the removed request renderer or another vendor-payload adapter.
- Executing, publishing, or installing Check Point policy changes.
- Unproven schema, UI, auth, installer, or existing-rule semantic changes.

# Phase 4 WP-02 planning: customer positioning activation

## Goal

Expose one behavior-based setting, `CheckPointLayerGuardEndOfZonePairSection`, and connect the completed KfW Check Point calculation to the existing durable implementation-task creation workflow.

## Scope

- Freeze persisted positioning-algorithm ordinals `0` through `6` and add value `7` with wire name `cpLayerGuardEndOfZonePairSection`.
- Offer the setting at global, device-type, management, and gateway scopes with localized labels and updated help.
- Load the complete ticket only after the new algorithm is resolved, retain a positive ordered task slice for the effective management, and keep the caller-supplied merged Rule A parent.
- Register the ticket-aware range resolver, fixed-import guard reader, customer factory/catalog, and zone-capable Check Point strategy alongside the graph-only generic strategy.
- Preserve exact capability separation: generic `DefaultEndOfRulebase` remains graph-only; the customer strategy supports only Check Point device type `9` plus the new enum.
- Reuse semantic-position persistence and the existing creation trigger. No renderer, firewall API operation, schema, Hasura metadata, REST, or GraphQL contract is added.

## Failure policy

There is no algorithm fallback or retry. Missing or malformed ticket state, graph, guard, matrix, zone, catalog, or recipe failures follow the existing typed best-effort path and clear a stale position. Caller cancellation still propagates before persistence.

## Validation

Cover enum stability and wire serialization, UI scope visibility and localization, algorithm-gated ticket loading, management filtering, bundled parents, complete orchestration order, exact import propagation, non-overlapping strategy capabilities, failure containment, formatting, build, complete unit tests, coverage, and installer validation.

`product_version` remains `9.8.1` and `9.8.1.sql` remains unchanged: the release line is open, no schema changes, and text additions are deployed by rerunning `fworch-texts.sql`.

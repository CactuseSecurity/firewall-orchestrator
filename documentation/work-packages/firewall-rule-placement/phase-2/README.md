# Phase 2 — Composable Check Point positioning blocks

## Goal

Build a production-ready, type-safe block composition framework and a minimal structural Check Point block library. Recipes remain in memory and are not selected or invoked by production workflows in this phase.

The customer document is deliberately outside this phase. Its analysis, customer-specific recipes, enum-to-recipe mapping, and production strategy wiring move to Phase 3.

## Work packages

1. [WP-01 — Generic composition contracts and executor](WP-01-composition-contracts.md)
2. [WP-02 — Immutable Check Point snapshot and semantic position](WP-02-check-point-snapshot-and-position.md)
3. [WP-03 — Structural blocks and composed acceptance scenarios](WP-03-structural-blocks.md)
4. [WP-04 — Generalize the snapshot to a rulebase graph](WP-04-rulebase-graph-snapshot.md)

WP-01 defines the reusable execution seam. WP-02 and WP-03 supplied the first Check Point-specific structural model and behavior without adding I/O. WP-04 replaces that initial model with an immutable graph derived from normalized `Rulebase` and `RulebaseLink` data while preserving the existing Check Point blocks and semantic position. Phase 2 is implemented and locally validated. The combined acceptance scenarios prove `EndOf(InitialLayer())` and `BeginningOf(Section(InitialLayer(), name-or-uid))` as in-memory compositions.

## Boundaries

- Keep `ProvisioningPositioningAlgorithm` unchanged.
- Preserve Phase 1 manager and persistence behavior.
- Load no policy data from GraphQL or importer models yet.
- Register and invoke no production `CheckPointRulePositioningStrategy`.
- Add no settings, UI, database, Hasura, workflow, or vendor API request changes.
- Do not inspect `FWO_Regel_Platzierung_Regelwerk.md`; that begins only in Phase 3.
- Phase 1 remains open pending its previously required disposable-VM validation.
- Do not expose existing-rule source, destination, service, action, zone, or other semantics until a concrete block requires them.
- Keep production loading out of Phase 2; WP-04 accepts already-loaded normalized entities.

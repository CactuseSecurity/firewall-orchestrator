# Phase 3 WP-03 planning - Check Point strategy orchestration

## Status

Planning complete on 2026-10-18. The paired implementation brief is self-contained and marked `Ready for implementation`.

The settled design adds one shared cancellation-aware designated-zone-matrix reader, uses an injected catalog as the sole algorithm-capability and recipe-selection source, handles only Check Point R8x target device type ID `9`, and keeps the strategy, catalog implementation, customer mappings, and workflow caller absent from production registration.

## Planning objective

Produce the self-contained implementation brief for a production-quality but inactive `CheckPointRulePositioningStrategy` that orchestrates the WP-01 reader, WP-02 zone input, recipe selection, and the Phase 2 executor.

This session plans only. Do not change production code, register the strategy, activate a caller, or inspect `FWO_Regel_Platzierung_Regelwerk.md`.

## Required reading

Read the authoritative roadmap material, the completed WP-01 and WP-02 implementation briefs and evidence, the Phase 1 manager contracts, all Phase 2 contracts, `phase-3/README.md`, and this file.

## Repository discovery

Inspect and record:

- the exact `IRulePositioningStrategy` capability and result contracts;
- how device types identify Check Point managements/gateways;
- the Phase 2 `CheckPointPositioningContext`, block results, recipe, and executor;
- how WP-01 and WP-02 expose success, failure, and cancellation;
- dependency-injection conventions for collections and keyed/selected services;
- the safest recipe-catalog seam for later enum/zone mappings; and
- test patterns for manager selection, orchestration order, short-circuiting, and cancellation.

## Decisions the planner must settle

1. Constructor dependencies and service lifetimes.
2. The recipe catalog/selector contract WP-05 will populate.
3. Whether `CanHandle` is based on a populated catalog, an explicit supported-algorithm set, or another exact capability source.
4. Translation from loader, zone-resolution, recipe-selection, and block failures into Phase 1 `RulePositioningResult` categories and safe diagnostics.
5. The order of operations and short-circuit behavior.
6. How to guarantee exactly one snapshot load and one recipe execution per calculation.
7. How algorithm identity is preserved in the returned `CheckPointRulePosition`.
8. How the strategy is tested without adding customer mappings or registering it in production.

Do not infer that any current enum maps to `EndOf(InitialLayer())` or another Phase 2 example.

## Required planning output

Make `WP-03-check-point-strategy-implementation.md` fully executable: name exact files, contracts, failure translations, tests, validation, and inactive registration state. Remove every placeholder before marking it ready.

Append decisions and update the phase status and handoff.

## Planning acceptance

- The strategy has no vendor switch in `RulePositioningManager`.
- Customer mappings are injected behind an explicit seam and remain absent.
- No incomplete strategy can be selected in production.
- All child failures and cancellation have deterministic behavior.
- The customer document remains unopened.

# Phase 3 WP-05 planning - Customer recipes and production activation

## Status

Blocked until WP-01 through WP-04 implementation and validation are complete.

## Planning objective

Analyze the customer rule-placement document for the first time and turn it into a self-contained final implementation brief covering additional reusable blocks, complete recipes, enum mappings, production strategy registration, and the approved workflow caller.

This is the only Phase 3 planning session authorized to inspect `FWO_Regel_Platzierung_Regelwerk.md`. It remains a planning session: do not change production code.

## Required reading

Read the authoritative roadmap and decision material, all completed Phase 3 implementation briefs and evidence, the current workflow/provisioning/Check Point execution path, `phase-3/README.md`, this file, and then the customer document.

Use `fwo-route-request`. Route any newly proven schema, installer, RBAC, or UI impact through its specialized skill before finalizing the implementation brief.

## Customer analysis output

Extract and record without interpretation gaps:

- each supported placement scenario and its inputs;
- source/destination zone-pair behavior;
- layer, inline-layer, section, and boundary selection rules;
- application versus common-service behavior;
- empty, missing, duplicated, or ambiguous scope behavior;
- precedence between rules and any explicit fallback behavior;
- examples that become executable acceptance tests; and
- contradictions, missing information, or terms requiring user/customer clarification.

Do not silently resolve ambiguities. Stop and obtain a decision when two materially different implementations remain possible.

## Decisions the planner must settle

1. The complete recipe for every supported existing `ProvisioningPositioningAlgorithm` value.
2. Which enum values remain unsupported for Check Point and the exact resulting failure.
3. Every additional block required beyond the Phase 2 structural library.
4. Exact zone-input-to-recipe/scope mapping and precedence.
5. Whether any existing-rule semantic field is concretely required; if so, route the expanded data contract before implementation.
6. The production recipe-catalog contents and strategy capability set.
7. The exact dependency-injection registration.
8. The approved workflow caller, trigger, context-loading ownership, and transaction/error boundary.
9. When a successful position is stored and when a stale position is cleared after failure.
10. Where WP-04 rendering is invoked and whether external execution remains separately controlled.
11. Logging, safe diagnostics, observability, and the unresolved audit boundary.
12. UI/help, configuration, integration-test, and disposable-environment impact.

## Required planning output

Replace the draft portions of `WP-05-customer-recipes-and-activation-implementation.md` with:

- a normative behavior table derived from the customer document;
- complete recipe trees and enum mappings;
- exact block contracts;
- exact files and dependency-injection changes;
- the complete workflow sequence, persistence/clearing policy, and renderer handoff;
- failure and cancellation behavior;
- concrete unit, integration, and acceptance scenarios;
- required documentation and specialized skills; and
- validation commands.

The implementation brief must contain all information the implementer needs without requiring them to reopen the customer document or this planning brief. Append decisions and update the phase status and handoff.

## Planning acceptance

- Every customer statement is represented by a decision, recipe, test, or explicit exclusion.
- Every supported enum has exactly one complete recipe.
- No ambiguity is delegated to the implementer.
- Registration cannot make an incomplete algorithm selectable.
- The workflow preserves calculation/persistence separation and safe failure handling.
- The implementation brief is self-contained and marked ready.

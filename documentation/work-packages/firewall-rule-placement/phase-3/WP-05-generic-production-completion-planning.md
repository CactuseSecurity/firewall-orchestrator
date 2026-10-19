# Phase 3 WP-05 planning — Generic production completion

## Status

Planning completed. The paired implementation brief is ready. Workflow activation was split into Phase 3 WP-06 so WP-05 can be implemented with its caller deliberately unhooked.

## Planning objective

Turn the implemented generic infrastructure into a usable semantic-positioning product without customer-specific behavior or firewall execution. The product boundary is a calculated `RulePosition` persisted on the applicable gateway-specific implementation task for a later placer.

Do not inspect `FWO_Regel_Platzierung_Regelwerk.md`. Customer-specific analysis belongs exclusively to Phase 4 WP-01.

## Required discovery

- Identify the exact callable service that has enough information to construct one `RulePositioningContext` per gateway-specific implementation task. Defer selection of its production workflow trigger to WP-06.
- Establish how the caller selects the applicable implementation task and effective management/configuration.
- Confirm at least one genuinely generic existing `ProvisioningPositioningAlgorithm` and its exact recipe semantics. Do not infer behavior from the enum name.
- Design the generic activation path so it loads the rulebase graph but never loads a designated zone matrix or invokes creation-rule zone resolution. The existing zone-input services remain inactive preparation for Phase 4.
- Define success persistence, replacement, and explicit stale-position clearing behavior for every failure category and cancellation.
- Verify existing empty initial layers produce a valid top/bottom anchor and genuinely missing initial layers fail explicitly.
- Audit the TODO items that can make generic activation unsafe or needlessly duplicative, especially graph interpretation and creation-rule zone resolution.
- Identify every WP-04 renderer production/test artifact and documentation dependency that Phase 3 WP-05 must remove.
- Confirm that calculation and persistence require no zone input, Check Point client, renderer, login, endpoint, publish, or policy installation.

## Decisions the planner must settle

1. The exact generic enum-to-recipe mapping and the enum values that remain unsupported.
2. The graph-only recipe tree and the minimal strategy/catalog shape that keeps all zone dependencies outside the generic runtime path.
3. Whether to refactor the existing zone-dependent Check Point strategy into separable orchestration or add a distinct generic strategy, while preserving exact-one-match manager behavior.
4. The concrete callable service, transaction boundary, and one-task-per-gateway operation. The production workflow trigger belongs to WP-06.
5. Dependency-injection registration for only the generic catalog, strategy, manager, and caller. Do not register zone-input services for positioning.
6. Store, replace, preserve, and clear behavior for success, typed failures, unexpected failures, and cancellation.
7. Behavior when the imported policy has an empty initial layer versus no positionable layer.
8. Safe observability and the still-unresolved audit boundary.
9. Exact removal scope for `CheckPointRulePositionRenderer`, its dedicated tests, and renderer-only compatibility assertions.
10. Integration, installer, UI-help, and backward-compatibility test requirements.

## Fixed boundaries

- The final output is the persisted semantic `RulePosition`; rendering is not part of the activated path.
- Generic recipes accept no zone input and must not depend on designated-zone configuration, Flow-group expansion, or compliance-zone resolution.
- Do not register `IDesignatedZoneMatrixReader` or `ICreationRuleZoneResolver` as positioning dependencies for the generic path; preserve their Phase 4 implementation without activating it.
- Remove `CheckPointRulePositionRenderer` and its renderer-specific tests; do not invoke, register, or retain it as dormant product code.
- Do not call `CheckPointClient`, add or execute `add-access-rule`, publish, install, or weaken the existing rule-change execution gate.
- Do not add customer mappings, customer blocks, new enum values, or behavior inferred from the customer document.
- Preserve calculation/persistence separation and exact strategy selection.
- Unsupported configurations remain safe and inactive without fallback.

## Required planning output

Replace every draft section in `WP-05-generic-production-completion-implementation.md` with a decision-complete implementation brief. Include the graph-only caller sequence, recipe/catalog contract, generic-only registrations, renderer-removal list, persistence policy, files, tests, validation, and exclusions. Explicitly prohibit production workflow invocation in WP-05 and route that work to WP-06. Update the roadmap, decision log, TODO status, and handoff.

## Planning acceptance

- The generic product has at least one confirmed usable capability.
- Generic calculation succeeds without zone configuration or zone queries.
- The implementer does not need the customer document or make product decisions.
- Persisted semantic output is the explicit terminal boundary.
- Empty-policy and missing-layer behavior are covered.
- The speculative renderer is removed and no vendor rendering or firewall execution can be reached.

# Session handoff

Last updated: 2026-10-19

## Current status

Phase 1 and Phase 2 remain implemented on the rebased `Cactus/develop` baseline. Phase 1 local validation passes, but the mandatory disposable-VM fresh-install and `9.7.2` to `9.7.3` upgrade gates remain pending. Phase 2 is implemented and locally validated.

Phase 3 WP-01 through WP-04 are implemented and locally validated. WP-05 has been redefined as generic production completion and is ready for a separate planning session; no WP-05 production code has been implemented.

Phase 4 is reserved for the later customer-specific extension. The customer document `FWO_Regel_Platzierung_Regelwerk.md` was not inspected and must remain unopened throughout Phase 3.

## Settled product boundary

- Phase 3 ends at a calculated and persisted semantic `RulePosition` on the applicable gateway-specific implementation task.
- Phase 3 WP-05 must confirm at least one genuinely generic graph-only enum-to-recipe mapping, register the calculation path, connect the approved workflow caller, and define exact persistence/clearing behavior.
- Generic recipes consume no zone input. The implemented designated-zone reader, creation-rule zone resolver, and zone-capable orchestration remain inactive until Phase 4.
- Phase 3 WP-05 removes `CheckPointRulePositionRenderer` and its dedicated tests; do not replace it with another vendor-payload adapter.
- Do not invoke `CheckPointClient`, execute `add-access-rule`, publish, install policy, or weaken the existing Check Point rule-change gate.
- Phase 4 WP-01 opens the customer document and adds only missing customer-specific blocks, recipes, and enum mappings on top of the finished generic product.

These decisions are recorded in D-033 through D-036. Open review and validation items are tracked in `documentation/work-packages/firewall-rule-placement/TODO.md`.

## Implemented WP-04 — scheduled for removal

- Add one internal static `CheckPointRulePositionRenderer` in `FWO.ExternalSystems.CheckPoint`; it receives an existing request `JsonNode`, one nullable `RulePosition`, and the explicit expected positioning algorithm.
- Render every non-null valid position with `layer: <LayerUid>` and explicit `position`: `"top"`, `"bottom"`, `{ "above": "<rule-uid>" }`, or `{ "below": "<rule-uid>" }`.
- Ordered and inline access layers use the same `layer` UID field. Never use `inline-layer` to select an insertion target.
- Null position deep-clones the request unchanged. Unknown/wrong-vendor, malformed, and algorithm-mismatched positions fail with fixed safe `ConfigException` messages.
- Rendering deep-clones the body, replaces only `layer` and `position`, remains idempotent, and never mutates the persisted position.
- Do not inspect or select `WfReqTask.ImplementationTasks`, call a client, add an endpoint, register a service, or connect the renderer to the workflow. Phase 3 WP-05 removes the renderer.

Implementation added the pure internal renderer and focused tests, plus a `CheckPointTicketTest` regression proving that `CreateRequestString` makes no client call. Validation on 2026-10-19 passed with 86 focused tests, a warning-free Debug solution build, 7,917 passed and 29 skipped full-suite tests, and 97.95% renderer line coverage (96.77% branch coverage). `dotnet format roles/FWO.sln` completed successfully; unrelated formatter-only changes were removed.

The historical implementation decision is recorded in D-032. D-036 supersedes its retention: WP-05 removes the renderer and its dedicated tests. The original evidence remains in:

`documentation/work-packages/firewall-rule-placement/phase-3/WP-04-semantic-position-rendering-implementation.md`

## Next action

Run a separate planning session for Phase 3 WP-05. Read `.agents/AGENTS.md`, the roadmap and decision log, this handoff, the TODO ledger, and `phase-3/WP-05-generic-production-completion-planning.md`. Do not inspect the customer document. Make the paired implementation brief self-contained before implementation begins.

The planner must settle the confirmed graph-only generic capability, exact recipe, zone-free strategy/catalog shape, caller, generic-only registration, implementation-task selection, persistence policy, empty-policy behavior, renderer removal, tests, and validation. It must preserve the semantic-output-only boundary and prohibit zone loading, request rendering, or firewall execution.

All changes remain unstaged and uncommitted unless the user explicitly requests otherwise.

Separately, execute the remaining Phase 1 VM acceptance gates when the required FWO VM integration is available. Do not run the installation manually.

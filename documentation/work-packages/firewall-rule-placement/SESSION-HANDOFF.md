# Session handoff

Last updated: 2026-10-14

## Current status

Phase 1 is implemented and rebased onto `Cactus/develop` at `ed03a8529`. A recovery pointer exists at `backup/rule-positioning-before-rebase-20261012`. The original five branch commits were replayed without merging or pushing.

The schema change now targets the open `9.7.1` release: `request.impltask.rule_position` is nullable in the creation schema, the guarded migration is appended to `roles/database/files/upgrade/9.7.1.sql`, `product_version` remains `9.7.1`, and revision history is recorded under `9.7.1`. The obsolete feature addition of `9.5.11.sql` was removed.

Phase 1 is not yet complete. All available local gates pass, but the required disposable-VM fresh-install and upgrade scenarios were not executed because the FWO VM tools were not available in the session. That pending status is unchanged.

Phase 2 is implemented and locally validated. It provides composable in-memory positioning blocks, an immutable vendor-neutral `RulebaseGraphSnapshot` projected from already-loaded normalized `Rulebase` and `RulebaseLink` entities, a semantic Check Point position, and structural layer/section boundary blocks. WP-04 removed the earlier public Check Point-specific snapshot types without a compatibility shim.

Phase 2 still does not register or invoke a production strategy. Customer-document analysis and all production wiring are deferred to Phase 3; `FWO_Regel_Platzierung_Regelwerk.md` was not inspected.

## Implemented contracts

- `RulePosition`, `UnknownRulePosition`, `RulePositioningContext`, and typed successes/failures are present.
- Both supported JSON serializers preserve known positions and round-trip unknown payloads.
- `IRulePositioningStrategy` and `RulePositioningManager.CalculateAsync` use exact strategy selection, safe validation failures, and cancellation propagation.
- No production positioning strategy is registered.
- `WfImplTask.RulePosition` is nullable and deep-copied.
- Hasura permissions, GraphQL reads/inserts/updates, and the narrow store/replace/clear operation carry `rule_position` without workflow side effects.
- `IPositioningBlock<TContext,TOutput>`, immutable `PositioningRecipe`, typed block results, and the stateless generic executor support nested and multi-child recipes.
- `RulebaseGraphSnapshot` defensively copies rulebase, link, and structural rule primitives; validates management/gateway ownership, unique identities, native order, and graph endpoints; and exposes no existing-rule semantics.
- Check Point graph interpretation resolves policy containers, ordered/domain layer chains, section chains, inline layers, and direct-rule/section interleaving while rejecting ambiguous topology.
- `CheckPointRulePosition` persists semantic top/bottom/above/below anchors through both JSON serializers and the existing polymorphic position contract.
- Initial/name/UID layer selection, name/UID section selection, `BeginningOf`, and `EndOf` are implemented as side-effect-free structural blocks.
- No enum-to-recipe mapping, policy loader, production strategy, dependency-injection registration, workflow caller, setting, UI, or Check Point request renderer was added.

## Completed Phase 2 WP-04

- The immutable single-calculation boundary now matches the normalized rulebase/link graph rather than a Check Point-only policy model.
- Construction accepts already-loaded `Rulebase` and `RulebaseLink` collections and performs no GraphQL, database, importer, or vendor API access.
- Existing-rule source, destination, service, action, zone, NAT, and other semantics remain outside the graph.
- Existing Check Point scope selection, beginning/end behavior, failure categories, and `CheckPointRulePosition` remain unchanged.
- `RuleTreeBuilder` remains unchanged and is not used as the positioning model; focused parity tests cover initial, policy, ordered/domain, section, and inline relationships.
- Phase 3 will resolve the new rule's zone pair, map it to customer recipes, and load the graph from the latest successfully completed import regardless of age. No maximum-age policy, automatic re-import, or fingerprint is required.

## Validation evidence

- Phase 2 focused tests: 70 passed, 0 failed. The WP-04 contract/structural subset has 59 passing tests; the focused set including unchanged `RuleTreeBuilder` parity has 82 passing tests.
- Complete unit suite: 7,436 passed, 29 skipped, 0 failed.
- Debug solution build: succeeded with 0 warnings and 0 errors.
- WP-04 production-file line coverage: 93.46% (657/703 executable lines).
- Changed C# files: targeted `dotnet format` completed successfully.
- Phase 2 tests cover recipe validation/execution, cancellation and concurrency, safe diagnostics, graph copying/invariants, Check Point link interpretation and ambiguity rejection, exact scope selection, empty/populated boundaries, both serializers, polymorphic deep copy, and composed acceptance examples.
- Phase 1's earlier local validation evidence remains valid; its disposable-VM acceptance gates are still pending.
- VM installation validation was not executed because this session exposes none of the required FWO MCP VM tools (`FwoListVirtualMachineTemplates`, clone/lifecycle tools, or `FwoTestInstallation`).
- No changes were staged, committed, or pushed.

## Remaining acceptance gates

Use the FWO VM integration only; do not run installation manually.

1. Fresh-install the rebased branch on a disposable supported VM.
2. Upgrade a disposable `9.7.0` installation to `9.7.1`.
3. Rerun `9.7.1.sql` to prove migration idempotency.
4. Verify existing implementation tasks load with `rule_position = NULL`.
5. Exercise known and unknown position load/store, replacement, and clearing through Hasura.
6. Delete every disposable clone.

If all VM checks pass, mark Phase 1 complete in this handoff, the roadmap, and `phase-1/README.md`. If any check fails, keep Phase 1 open and record the failing stage and evidence.

## Next action

Phase 2 is complete. Do not start Phase 3 or inspect `FWO_Regel_Platzierung_Regelwerk.md` until the user explicitly starts Phase 3. At that point, read the active roadmap and Phase 3 plan before analyzing the customer document or adding production loading, recipes, mapping, strategy registration, or workflow wiring.

Separately, run the remaining Phase 1 VM acceptance gates when the FWO VM tools are available.

Suggested bootstrap prompt:

> Read `.agents/AGENTS.md`, then `documentation/work-packages/firewall-rule-placement/SESSION-HANDOFF.md`, `README.md`, `DECISIONS.md`, and the active work packages in roadmap order. Treat them as authoritative. Phase 2 including WP-04 is implemented and locally validated. Do not begin Phase 3 or inspect the customer document unless Phase 3 is explicitly requested.

No changes were staged, committed, or pushed automatically.

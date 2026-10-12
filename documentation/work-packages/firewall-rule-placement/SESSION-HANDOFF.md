# Session handoff

Last updated: 2026-10-12

## Current status

Phase 1 is implemented and rebased onto `Cactus/develop` at `ed03a8529`. A recovery pointer exists at `backup/rule-positioning-before-rebase-20261012`. The original five branch commits were replayed without merging or pushing.

The schema change now targets the open `9.7.1` release: `request.impltask.rule_position` is nullable in the creation schema, the guarded migration is appended to `roles/database/files/upgrade/9.7.1.sql`, `product_version` remains `9.7.1`, and revision history is recorded under `9.7.1`. The obsolete feature addition of `9.5.11.sql` was removed.

Phase 1 is not yet complete. All available local gates pass, but the required disposable-VM fresh-install and upgrade scenarios were not executed because the FWO VM tools were not available in the session. Phase 2 remains deferred, and `FWO_Regel_Platzierung_Regelwerk.md` has not been analyzed.

## Implemented contracts

- `RulePosition`, `UnknownRulePosition`, `RulePositioningContext`, and typed successes/failures are present.
- Both supported JSON serializers preserve known positions and round-trip unknown payloads.
- `IRulePositioningStrategy` and `RulePositioningManager.CalculateAsync` use exact strategy selection, safe validation failures, and cancellation propagation.
- No production positioning strategy is registered.
- `WfImplTask.RulePosition` is nullable and deep-copied.
- Hasura permissions, GraphQL reads/inserts/updates, and the narrow store/replace/clear operation carry `rule_position` without workflow side effects.

## Validation evidence

- Focused positioning tests: 78 passed, 0 failed.
- Complete unit suite: 7,366 passed, 29 skipped, 0 failed.
- Debug solution build: succeeded with 0 warnings and 0 errors.
- New production-file line coverage: 91.77% (301/328 executable lines).
- Changed C# files: `dotnet format --verify-no-changes` passed.
- Full solution formatter: blocked only by unrelated upstream whitespace findings in `UiDisplayImplementationTaskTest.cs` and `UiDisplayRequestTaskTest.cs`.
- Hasura metadata parses as JSON; focused tests verify role boundaries, nullable schema, GraphQL payloads, known/unknown serialization, deep copies, exact strategy selection, cancellation, and store/replace/clear behavior.
- No commit or push was made after the rebase. Documentation and the small post-rebase test adjustment are intentionally unstaged.

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

Run the remaining VM acceptance gates in a session that exposes the FWO VM tools. After they pass and Phase 1 is marked complete, request explicit authorization before starting Phase 2. Do not analyze `FWO_Regel_Platzierung_Regelwerk.md` until that authorization is given.

Suggested commit after the user reviews the unstaged closeout edits: `docs: update phase 1 validation handoff`.

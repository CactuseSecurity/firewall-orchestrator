# Firewall rule positioning roadmap

## Authority and current status

This directory is the repository source of truth for firewall rule positioning work. Active planning documents override chat history and any material under `archive/`.

Phase 1 is implemented and locally validated on the current `Cactus/develop` baseline. It remains open only because the mandatory disposable-VM fresh-install and `9.7.0` to `9.7.1` upgrade checks could not be run in the current session. Phase 2 is intentionally deferred.

| Phase | Status | Purpose |
| --- | --- | --- |
| Phase 1 | Implemented; VM validation pending | Build a calculation-only, cross-vendor positioning foundation and independent position persistence. |
| Phase 2 | Deferred | Analyze the customer rule-positioning document and build the Check Point strategy from small helpers. |
| Previous proposal | Archived | Historical context only; it is not an alternative active plan. |

## Reading order

New sessions must read these files in order:

1. `.agents/AGENTS.md`
2. `documentation/work-packages/firewall-rule-placement/SESSION-HANDOFF.md`
3. `documentation/work-packages/firewall-rule-placement/README.md`
4. `documentation/work-packages/firewall-rule-placement/DECISIONS.md`
5. `documentation/work-packages/firewall-rule-placement/phase-1/README.md`
6. `documentation/work-packages/firewall-rule-placement/phase-1/WP-01-contracts-and-serialization.md`
7. `documentation/work-packages/firewall-rule-placement/phase-1/WP-02-positioning-manager.md`
8. `documentation/work-packages/firewall-rule-placement/phase-1/WP-03-position-persistence.md`

Read `phase-2/README.md` only when checking the deferred boundary. Do not use the archived proposal to make current decisions. Do not analyze `FWO_Regel_Platzierung_Regelwerk.md` before Phase 2 is explicitly started.

Use this bootstrap prompt for a new planning session:

> Read `.agents/AGENTS.md`, then `documentation/work-packages/firewall-rule-placement/SESSION-HANDOFF.md`, `README.md`, `DECISIONS.md`, and the active phase work packages in the order listed there. Treat those files as the source of truth. The archived proposal is historical only. Do not analyze `FWO_Regel_Platzierung_Regelwerk.md` until phase 2. Continue from the “Next action” in `SESSION-HANDOFF.md`.

## Roadmap boundary

Phase 1 handles one gateway-specific implementation task per calculation. The caller must load all required context, call the positioning manager, and decide independently whether to persist a successful position or clear a stale one after failure.

Phase 1 includes:

- shared polymorphic position contracts and forward-compatible JSON handling;
- a vendor-neutral manager that validates context and selects exactly one registered strategy;
- test-only strategies proving that different vendors can coexist;
- nullable implementation-task persistence for a calculated position;
- a dedicated write/clear operation that has no workflow side effects; and
- unit, coverage, build, fresh-install, and upgrade validation.

Phase 1 excludes:

- the customer-specific positioning algorithm;
- production Check Point or Fortigate positioning strategies;
- Check Point or Fortigate communication;
- automatic workflow triggers or planning activation;
- UI or plan presentation;
- executable firewall changes;
- position-change auditing; and
- vendor API conversion.

## Work packages

| Work package | Outcome |
| --- | --- |
| [WP-01](phase-1/WP-01-contracts-and-serialization.md) | Shared contracts, safe failures, polymorphic serialization, and copy semantics. |
| [WP-02](phase-1/WP-02-positioning-manager.md) | Context validation and exact registered-strategy selection without vendor switches. |
| [WP-03](phase-1/WP-03-position-persistence.md) | Nullable JSONB persistence propagated through schema, Hasura, GraphQL, and `WfImplTask`. |

## Dependency graph

```text
WP-01 contracts and serialization
  |---> WP-02 positioning manager
  `---> WP-03 position persistence

WP-02 + WP-03
  `---> independent calculation/persistence acceptance tests
         `---> full Phase 1 validation
```

WP-02 and WP-03 may proceed in parallel after WP-01 stabilizes. Phase 1 is not complete until all three packages and their combined acceptance checks pass.

## Fixed assumptions

- Existing hierarchical provisioning configuration and `ProvisioningPositioningAlgorithm` are reused unchanged.
- Management is derived from the parent request task and gateway relationship. No duplicate `management_id` is added to the implementation task.
- Only new access-rule tasks are supported in Phase 1. Modification, deletion, object, and unknown task types are declined safely.
- Calculation and persistence are separate operations.
- Production calls return a safe not-implemented result while no production strategy is registered.
- Safe diagnostics contain neither credentials nor complete rule contents.
- The schema migration is part of the open `9.7.1` upgrade and leaves `product_version` unchanged (D-015).

## Session protocol

At the end of every planning session:

1. Append a new entry to `DECISIONS.md` if a decision was added, changed, superseded, or marked for reconsideration. Do not rewrite earlier decision entries.
2. Replace `SESSION-HANDOFF.md` with a short current recovery point.
3. Commit only those planning-document changes as the final recovery checkpoint. Do not include unrelated working-tree changes.

## Historical material

The previous WP-00 through WP-08 proposal is preserved under `archive/previous-proposal/`. It remains useful as history, but it must not compete with this roadmap or be treated as approved scope.

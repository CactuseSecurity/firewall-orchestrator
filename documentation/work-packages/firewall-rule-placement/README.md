# Firewall rule positioning roadmap

## Authority and current status

This directory is the repository source of truth for firewall rule positioning work. Active planning documents override chat history and any material under `archive/`.

Phase 1 is implemented and locally validated on the current `Cactus/develop` baseline. It remains open until the mandatory disposable-VM fresh-install and `9.7.2` to `9.7.3` upgrade checks pass. Phase 2 is implemented: it adds the in-memory composition framework, an immutable graph derived from normalized rulebases and links, the semantic Check Point position, and the minimal structural Check Point block library without activating production positioning. Phase 3 planning is now structured into five paired planning/implementation work packages; implementation has not started.

| Phase | Status | Purpose |
| --- | --- | --- |
| Phase 1 | Implemented; VM validation pending | Build a calculation-only, cross-vendor positioning foundation and independent position persistence. |
| Phase 2 | Implemented and locally validated | Build generic composition contracts, a reusable immutable structural graph, semantic positions, and structural blocks. |
| Phase 3 | Planning structured; implementation not started | Add production inputs and rendering first, then analyze the customer document in the final work package and activate complete recipes. |
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
9. `documentation/work-packages/firewall-rule-placement/phase-2/README.md`
10. the four Phase 2 work packages in the order listed there
11. `documentation/work-packages/firewall-rule-placement/phase-3/README.md`
12. the active Phase 3 planning or implementation brief selected there

Do not use the archived proposal to make current decisions. Do not inspect or analyze `FWO_Regel_Platzierung_Regelwerk.md` until the Phase 3 WP-05 planning session.

Use this bootstrap prompt for a new planning session:

> Read `.agents/AGENTS.md`, then `documentation/work-packages/firewall-rule-placement/SESSION-HANDOFF.md`, `README.md`, `DECISIONS.md`, and the active phase work packages in the order listed there. Treat those files as the source of truth. The archived proposal is historical only. Do not analyze `FWO_Regel_Platzierung_Regelwerk.md` until the Phase 3 WP-05 planning session. Continue from the “Next action” in `SESSION-HANDOFF.md`.

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

Phase 2 adds only in-memory, side-effect-free building blocks:

- generic typed recipe nodes, immutable recipe construction, and a cancellation-aware executor;
- stable block failures that retain their originating block kind;
- a defensively copied structural graph for one management and gateway derived from normalized rulebases and links;
- a persistable semantic Check Point position using top, bottom, above, or below anchors;
- exact layer and section scope selection; and
- beginning/end boundary resolution for populated and empty scopes.

Phase 2 does not add a strategy, policy loader, dependency-injection registration, workflow caller, setting, UI, vendor request renderer, or enum-to-recipe mapping.

## Work packages

| Work package | Outcome |
| --- | --- |
| [WP-01](phase-1/WP-01-contracts-and-serialization.md) | Shared contracts, safe failures, polymorphic serialization, and copy semantics. |
| [WP-02](phase-1/WP-02-positioning-manager.md) | Context validation and exact registered-strategy selection without vendor switches. |
| [WP-03](phase-1/WP-03-position-persistence.md) | Nullable JSONB persistence propagated through schema, Hasura, GraphQL, and `WfImplTask`. |
| [Phase 2 WP-01](phase-2/WP-01-composition-contracts.md) | Generic recipe contracts, validation, typed results, and executor. |
| [Phase 2 WP-02](phase-2/WP-02-check-point-snapshot-and-position.md) | Immutable Check Point snapshot and persisted semantic position. |
| [Phase 2 WP-03](phase-2/WP-03-structural-blocks.md) | Structural scopes, boundary blocks, and composed acceptance scenarios. |
| [Phase 2 WP-04](phase-2/WP-04-rulebase-graph-snapshot.md) | Replace the Check Point-specific snapshot with an immutable normalized rulebase graph before Phase 3. |
| [Phase 3 WP-01](phase-3/WP-01-production-snapshot-reader-planning.md) | Plan and implement loading the latest successful imported graph. |
| [Phase 3 WP-02](phase-3/WP-02-zone-pair-resolution-planning.md) | Plan and implement creation-rule zone resolution without customer mapping. |
| [Phase 3 WP-03](phase-3/WP-03-check-point-strategy-planning.md) | Plan and implement inactive Check Point strategy orchestration. |
| [Phase 3 WP-04](phase-3/WP-04-semantic-position-rendering-planning.md) | Plan and implement semantic anchor rendering without workflow activation. |
| [Phase 3 WP-05](phase-3/WP-05-customer-recipes-and-activation-planning.md) | Analyze the customer document, finalize recipes and mappings, and activate the production path. |

## Dependency graph

```text
WP-01 contracts and serialization
  |---> WP-02 positioning manager
  `---> WP-03 position persistence

WP-02 + WP-03
  `---> independent calculation/persistence acceptance tests
         `---> full Phase 1 validation

Phase 2 WP-01 composition contracts
  |---> Phase 2 WP-02 snapshot and position
  `---> Phase 2 WP-03 structural blocks
         `---> composed in-memory acceptance tests

Phase 2 WP-02 + WP-03
  `---> Phase 2 WP-04 rulebase graph snapshot

Phase 2 including WP-04
  |---> Phase 3 WP-01 production snapshot reader --.
  `---> Phase 3 WP-02 zone resolution ------------+--> WP-03 strategy
                                                       `--> WP-04 renderer
                                                              `--> WP-05 customer recipes and activation
```

WP-02 and WP-03 may proceed in parallel after WP-01 stabilizes. Phase 1 is not complete until all three packages and their combined acceptance checks pass.

## Fixed assumptions

- Existing hierarchical provisioning configuration and `ProvisioningPositioningAlgorithm` are reused unchanged.
- Effective management is `implTask.ManagementId ?? requestTask.ManagementId`; it must agree with the supplied management and gateway relationship.
- Only new access-rule tasks are supported in Phase 1. Modification, deletion, object, and unknown task types are declined safely.
- Calculation and persistence are separate operations.
- Production calls return a safe not-implemented result while no production strategy is registered.
- Safe diagnostics contain neither credentials nor complete rule contents.
- The schema migration is part of the open `9.7.3` upgrade and leaves `product_version` unchanged (D-026).
- Phase 2 recipes are in-memory only and use one caller-supplied immutable snapshot.
- The final Phase 2 snapshot is a vendor-neutral projection of normalized `Rulebase` and `RulebaseLink` structure; Check Point blocks interpret that graph.
- Existing-rule semantics remain excluded until a concrete block needs them; structural rule identity and order remain available for anchors.
- Phase 3 production loading accepts the latest successfully completed import regardless of age, without a freshness threshold or fingerprint requirement.
- Existing empty scopes are valid; missing, ambiguous, and malformed scopes fail explicitly.
- The existing positioning enum remains unchanged and no value maps to a recipe before the Phase 3 WP-05 implementation.

## Session protocol

At the end of every planning session:

1. Append a new entry to `DECISIONS.md` if a decision was added, changed, superseded, or marked for reconsideration. Do not rewrite earlier decision entries.
2. Replace `SESSION-HANDOFF.md` with a short current recovery point.
3. Leave changes unstaged and uncommitted unless the user explicitly requests a recovery commit.

## Historical material

The previous WP-00 through WP-08 proposal is preserved under `archive/previous-proposal/`. It remains useful as history, but it must not compete with this roadmap or be treated as approved scope.

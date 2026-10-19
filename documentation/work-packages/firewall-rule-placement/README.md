# Firewall rule positioning roadmap

## Authority and current status

This directory is the repository source of truth for firewall rule positioning work. Active planning documents override chat history and any material under `archive/`.

Phase 1 is implemented and locally validated on the current `Cactus/develop` baseline. It remains open until the mandatory disposable-VM fresh-install and `9.7.2` to `9.7.3` upgrade checks pass. Phase 2 is implemented: it adds the in-memory composition framework, an immutable graph derived from normalized rulebases and links, the semantic Check Point position, and the minimal structural Check Point block library without activating production positioning. Phase 3 WP-01 through WP-04 are implemented and locally validated; WP-05 will complete a graph-only generic semantic-positioning product and remove the speculative renderer. The implemented zone-input services are inactive for positioning until Phase 4, which is reserved for customer-document analysis and customer-specific recipes.

| Phase | Status | Purpose |
| --- | --- | --- |
| Phase 1 | Implemented; VM validation pending | Build a calculation-only, cross-vendor positioning foundation and independent position persistence. |
| Phase 2 | Implemented and locally validated | Build generic composition contracts, a reusable immutable structural graph, semantic positions, and structural blocks. |
| Phase 3 | WP-01 through WP-04 implemented; WP-05 ready for planning | Complete a graph-only generic path that persists semantic positions, remove the renderer, and leave zone input inactive. |
| Phase 4 | Deferred until Phase 3 is complete | Analyze the customer document and extend the finished generic product with customer-only zone input, required blocks, recipes, and enum mappings. |
| Previous proposal | Archived | Historical context only; it is not an alternative active plan. |

## Reading order

New sessions must read these files in order:

1. `.agents/AGENTS.md`
2. `documentation/work-packages/firewall-rule-placement/SESSION-HANDOFF.md`
3. `documentation/work-packages/firewall-rule-placement/README.md`
4. `documentation/work-packages/firewall-rule-placement/DECISIONS.md`
5. `documentation/work-packages/firewall-rule-placement/TODO.md`
6. `documentation/work-packages/firewall-rule-placement/phase-1/README.md`
7. `documentation/work-packages/firewall-rule-placement/phase-1/WP-01-contracts-and-serialization.md`
8. `documentation/work-packages/firewall-rule-placement/phase-1/WP-02-positioning-manager.md`
9. `documentation/work-packages/firewall-rule-placement/phase-1/WP-03-position-persistence.md`
10. `documentation/work-packages/firewall-rule-placement/phase-2/README.md`
11. the four Phase 2 work packages in the order listed there
12. `documentation/work-packages/firewall-rule-placement/phase-3/README.md`
13. the active Phase 3 planning or implementation brief selected there
14. `documentation/work-packages/firewall-rule-placement/phase-4/README.md` only after Phase 3 is complete

Do not use the archived proposal to make current decisions. Do not inspect or analyze `FWO_Regel_Platzierung_Regelwerk.md` until the Phase 4 WP-01 planning session.

Use this bootstrap prompt for a new planning session:

> Read `.agents/AGENTS.md`, then `documentation/work-packages/firewall-rule-placement/SESSION-HANDOFF.md`, `README.md`, `DECISIONS.md`, and the active phase work packages in the order listed there. Treat those files as the source of truth. The archived proposal is historical only. Do not analyze `FWO_Regel_Platzierung_Regelwerk.md` until the Phase 4 WP-01 planning session. Continue from the “Next action” in `SESSION-HANDOFF.md`.

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
| [Phase 3 WP-02](phase-3/WP-02-zone-pair-resolution-planning.md) | Implemented early as inactive Phase 4 preparation; it is not an input to generic recipes. |
| [Phase 3 WP-03](phase-3/WP-03-check-point-strategy-planning.md) | Implemented a zone-capable inactive seam that WP-05 must isolate from the graph-only generic path. |
| [Phase 3 WP-04](phase-3/WP-04-semantic-position-rendering-planning.md) | Implemented experimentally; scheduled for removal in WP-05 under D-036. |
| [Phase 3 WP-05](phase-3/WP-05-generic-production-completion-planning.md) | Complete graph-only generic calculation and persistence, keep zone input inactive, and remove the renderer. |
| [Phase 4 WP-01](phase-4/WP-01-customer-specific-recipes-planning.md) | Analyze the customer document and add customer-only zone usage plus the required blocks, recipes, and enum mappings. |

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
  `---> Phase 3 WP-01 production snapshot reader
         `--> Phase 3 WP-05 graph-only recipe, activation, and persistence

Phase 3 WP-02 zone input + zone-capable portion of WP-03
  `--> inactive until Phase 4 WP-01 customer extension

Phase 3 WP-04 renderer
  `--> removed by Phase 3 WP-05
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
- The implemented creation-rule zone resolution expands existing request-visible Flow groups and reuses established compliance-zone behavior, but it is inactive for positioning until Phase 4 and is not a generic dependency.
- Existing empty scopes are valid; missing, ambiguous, and malformed scopes fail explicitly.
- Phase 3 ends at calculated and persisted semantic output. It does not invoke the Check Point renderer, client, publish, or policy installation.
- Phase 3 WP-05 removes the WP-04 renderer and its dedicated tests; a future placer owns any conversion contract.
- Generic recipes use the imported rulebase graph only and never load zone input.
- Phase 3 WP-05 must confirm at least one genuinely generic existing enum mapping; it must not infer semantics from an enum name.
- Customer-specific enum mappings and blocks are deferred to Phase 4 WP-01.

## Session protocol

At the end of every planning session:

1. Append a new entry to `DECISIONS.md` if a decision was added, changed, superseded, or marked for reconsideration. Do not rewrite earlier decision entries.
2. Replace `SESSION-HANDOFF.md` with a short current recovery point.
3. Leave changes unstaged and uncommitted unless the user explicitly requests a recovery commit.

## Historical material

The previous WP-00 through WP-08 proposal is preserved under `archive/previous-proposal/`. It remains useful as history, but it must not compete with this roadmap or be treated as approved scope.

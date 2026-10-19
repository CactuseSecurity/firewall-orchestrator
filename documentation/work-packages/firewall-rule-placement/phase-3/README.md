# Phase 3 — Generic production completion

## Status

WP-01 through WP-04 are implemented and locally validated. WP-05 is ready for its separate planning session. No WP-05 production code has been implemented.

Phase 3 now ends with a graph-only generic product that calculates and persists semantic rule positions. It does not consume zone input, analyze the customer document, render vendor request bodies, or execute firewall changes. The previously implemented zone services do not participate in positioning until Phase 4, and WP-05 removes the WP-04 renderer.

Phase 3 is split into five work packages. Each work package has two deliberately separate sessions:

1. a planning session that performs repository discovery, settles decisions, and makes the paired implementation brief self-contained; and
2. an implementation session that reads only the settled implementation brief and implements its scope.

The implementation session does not need to read the planning brief. A work package may not enter implementation while its implementation brief is marked `Draft - planning required` or still contains unresolved decision placeholders.

## Work packages and order

| Work package | Status | Planning brief | Implementation brief | Outcome |
| --- | --- | --- | --- | --- |
| WP-01 | Implemented and locally validated | [Plan the production snapshot reader](WP-01-production-snapshot-reader-planning.md) | [Implemented production snapshot reader](WP-01-production-snapshot-reader-implementation.md) | Load one immutable gateway graph from the latest successfully completed import. |
| WP-02 | Implemented early for Phase 4 | [Plan creation-rule zone resolution](WP-02-zone-pair-resolution-planning.md) | [Implemented creation-rule zone resolution](WP-02-zone-pair-resolution-implementation.md) | Produce customer-ready zone input, but remain outside the generic Phase 3 runtime path. |
| WP-03 | Implemented; generic isolation required | [Completed Check Point strategy plan](WP-03-check-point-strategy-planning.md) | [Implemented Check Point strategy](WP-03-check-point-strategy-implementation.md) | Supply an inactive zone-capable seam; WP-05 must provide or extract a graph-only generic path. |
| WP-04 | Implemented; removal approved | [Historical rendering plan](WP-04-semantic-position-rendering-planning.md) | [Historical renderer implementation](WP-04-semantic-position-rendering-implementation.md) | Remove the speculative renderer and its dedicated tests in WP-05. |
| WP-05 | Ready for planning | [Plan generic production completion](WP-05-generic-production-completion-planning.md) | [Implement generic production completion](WP-05-generic-production-completion-implementation.md) | Add a graph-only generic recipe/catalog mapping, register calculation/persistence, keep zones inactive, and remove the renderer. |

WP-01 is the production input for the graph-only generic path. WP-02 and the zone-capable portion of WP-03 were implemented early but are inputs only to Phase 4 customer algorithms. WP-04 is scheduled for removal. WP-05 is the final Phase 3 package and must keep zone services inactive.

## Two-session protocol

### Planning session

The planner reads the planning brief and the authoritative roadmap material. The planner performs read-only discovery of the relevant production code and tests, resolves every decision listed in the planning brief, and updates the paired implementation brief with:

- settled behavior and failure semantics;
- exact production files and contracts to add or change;
- exact tests and fixtures;
- required documentation and specialized skills;
- validation commands; and
- explicit exclusions.

The planner does not implement production code. At the end of the session, the planner:

1. marks the implementation brief `Ready for implementation` only when it is self-contained and has no blocking placeholders;
2. updates this phase status and `SESSION-HANDOFF.md`;
3. appends any new or changed architectural decision to `DECISIONS.md`; and
4. leaves all changes unstaged and uncommitted unless the user explicitly requests otherwise.

### Implementation session

The implementer reads `.agents/AGENTS.md`, the authoritative roadmap and decision log, `SESSION-HANDOFF.md`, and the one implementation brief being executed. The implementer does not need the paired planning brief and must not reopen decisions already settled in the implementation brief unless repository evidence makes the brief impossible or unsafe.

The implementer applies only that package, adds its tests and documentation, runs the prescribed validation, records evidence in the implementation brief and handoff, and does not begin the next package.

## Customer-document boundary

Do not inspect or analyze `FWO_Regel_Platzierung_Regelwerk.md` anywhere in Phase 3.

The document is opened for the first time in the Phase 4 WP-01 planning session. That planner compares the customer behavior with the finished generic product and adds only the missing blocks, recipes, enum mappings, exceptional cases, and acceptance examples.

Do not infer customer behavior or enum mappings from enum names, the archived proposal, or Phase 2's structural examples.

## Fixed Phase 3 boundaries

- The intended path supports creation of new access rules only.
- The new rule's workflow elements supply the source and destination inputs.
- Production loading uses the latest successfully completed FWO import regardless of age.
- Do not add a freshness threshold, automatic re-import, or snapshot fingerprint.
- A calculation uses one immutable `RulebaseGraphSnapshot` for one management and gateway.
- Existing-rule source, destination, service, action, zone, NAT, and other semantic fields remain excluded throughout Phase 3.
- Generic recipes consume the rulebase graph only; they never consume `CreationRuleZoneInput`.
- Designated-zone loading, Flow-group expansion, compliance-zone resolution, and zone-dependent recipe selection do not participate in positioning until Phase 4. Existing non-positioning consumers remain unchanged.
- Calculation, position persistence, Check Point request rendering, and external execution remain separate responsibilities.
- WP-05 may register and invoke only calculation and semantic-position persistence.
- WP-05 removes `CheckPointRulePositionRenderer` and its dedicated tests and must not introduce a replacement vendor-payload adapter.
- WP-05 must not invoke `CheckPointClient`, `add-access-rule`, publish, or policy installation.
- The existing Check Point rule-change execution gate remains unchanged.
- No new positioning enum value, setting, schema field, or UI is assumed.

## Dependency graph

```text
WP-01 production snapshot reader
  `--> WP-05 graph-only generic recipe, registration, caller, persistence

WP-02 zone input + zone-capable WP-03 orchestration
  `--> inactive until Phase 4 customer-specific recipes

WP-04 renderer
  `--> removed by WP-05
```

## Phase acceptance

Phase 3 is complete only when:

- every implementation brief is marked complete with validation evidence;
- at least one confirmed generic Check Point positioning enum maps explicitly to one complete recipe and unsupported values fail safely;
- the production generic path loads exactly one graph, performs no zone/matrix/Flow-group work, and performs no vendor communication;
- the approved workflow caller calculates and persists or clears positions according to the settled policy;
- the persisted `RulePosition` is the final Phase 3 product output;
- the renderer and its dedicated tests are removed;
- no zone service participates in generic positioning, and no Check Point client, publish, installation, incomplete strategy, or customer mapping is active;
- focused tests, the full unit suite, formatting, the Debug build, and more than 80% coverage for new production code pass; and
- any applicable UI help, integration, and disposable-environment checks are complete.

# Phase 3 — Customer recipes and production wiring

## Status

WP-01 and WP-02 are implemented and locally validated. WP-03 is ready for its separate planning session. WP-04 and WP-05 remain in their documented blocking states; no WP-03 or later production code has been implemented.

Phase 3 is split into five work packages. Each work package has two deliberately separate sessions:

1. a planning session that performs repository discovery, settles decisions, and makes the paired implementation brief self-contained; and
2. an implementation session that reads only the settled implementation brief and implements its scope.

The implementation session does not need to read the planning brief. A work package may not enter implementation while its implementation brief is marked `Draft - planning required` or still contains unresolved decision placeholders.

## Work packages and order

| Work package | Status | Planning brief | Implementation brief | Outcome |
| --- | --- | --- | --- | --- |
| WP-01 | Implemented and locally validated | [Plan the production snapshot reader](WP-01-production-snapshot-reader-planning.md) | [Implemented production snapshot reader](WP-01-production-snapshot-reader-implementation.md) | Load one immutable gateway graph from the latest successfully completed import. |
| WP-02 | Implemented and locally validated | [Plan creation-rule zone resolution](WP-02-zone-pair-resolution-planning.md) | [Implemented creation-rule zone resolution](WP-02-zone-pair-resolution-implementation.md) | Resolve the new rule's source and destination workflow elements and existing Flow groups into safe ordered zone sets. |
| WP-03 | Ready for planning | [Plan the Check Point strategy](WP-03-check-point-strategy-planning.md) | [Implement the Check Point strategy](WP-03-check-point-strategy-implementation.md) | Orchestrate snapshot loading, zone resolution, recipe selection, and recipe execution without customer mappings or registration. |
| WP-04 | Blocked on WP-03 | [Plan semantic-position rendering](WP-04-semantic-position-rendering-planning.md) | [Implement semantic-position rendering](WP-04-semantic-position-rendering-implementation.md) | Render semantic Check Point anchors into the existing Check Point request representation without activating a workflow. |
| WP-05 | Blocked on WP-01 through WP-04 | [Plan customer recipes and activation](WP-05-customer-recipes-and-activation-planning.md) | [Implement customer recipes and activation](WP-05-customer-recipes-and-activation-implementation.md) | Analyze the customer document, add the required recipes and mappings, register the strategy, and connect the approved caller. |

Complete the packages in order. WP-01 and WP-02 describe independent inputs, but their approved contracts are both prerequisites for WP-03. WP-04 may be planned and implemented after WP-03. WP-05 is the final package and depends on all four earlier packages.

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

Do not inspect or analyze `FWO_Regel_Platzierung_Regelwerk.md` during WP-01 through WP-04.

The document is opened for the first time in the WP-05 planning session. That planner converts the customer behavior into explicit recipes, enum mappings, exceptional cases, and acceptance examples in the WP-05 implementation brief. The WP-05 implementer receives the resulting requirements and does not need to repeat the customer-document analysis.

Do not infer customer behavior or enum mappings from enum names, the archived proposal, or Phase 2's structural examples.

## Fixed Phase 3 boundaries

- The intended path supports creation of new access rules only.
- The new rule's workflow elements supply the source and destination inputs.
- Production loading uses the latest successfully completed FWO import regardless of age.
- Do not add a freshness threshold, automatic re-import, or snapshot fingerprint.
- A calculation uses one immutable `RulebaseGraphSnapshot` for one management and gateway.
- Existing-rule source, destination, service, action, zone, NAT, and other semantic fields remain excluded unless WP-05 identifies a concrete comparison that requires one.
- Calculation, position persistence, Check Point request rendering, and external execution remain separate responsibilities.
- No production strategy is registered and no workflow caller is activated before WP-05.
- No new positioning enum value, setting, schema field, or UI is assumed.

## Dependency graph

```text
WP-01 production snapshot reader ----.
                                      +--> WP-03 Check Point strategy --> WP-04 semantic renderer
WP-02 creation-rule zone resolution -'                                         |
                                                                                `--> WP-05 customer recipes,
                                                                                     mapping, registration,
                                                                                     and workflow activation
```

## Phase acceptance

Phase 3 is complete only when:

- every implementation brief is marked complete with validation evidence;
- every supported Check Point positioning enum maps explicitly to one complete recipe and unsupported values fail safely;
- the production strategy loads exactly one graph and resolves the new rule's positioning input without vendor communication inside calculation blocks;
- semantic positions are rendered correctly into Check Point requests;
- the approved workflow caller calculates and persists or clears positions according to the settled policy;
- no incomplete strategy or customer mapping is active;
- focused tests, the full unit suite, formatting, the Debug build, and more than 80% coverage for new production code pass; and
- any applicable UI help, integration, and disposable-environment checks are complete.

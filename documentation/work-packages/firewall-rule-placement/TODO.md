# Firewall rule positioning follow-up ledger

This file tracks unresolved review, validation, and product-boundary work. An item is not approved implementation scope merely because it appears here; move it into a decision-complete work package before coding.

## Phase 3 WP-05 implementation work (complete)

- [x] Audit `CheckPointRulebaseGraphInterpreter` against `RuleTreeBuilder`. No safe neutral seam exists: the builder is mutable, cached, report-oriented, synthetic-node/NAT aware, and tolerant where positioning deliberately fails. Keep the immutable strict interpreter separate in WP-05.
- [x] Add a representative imported empty Check Point policy fixture. The production reader, graph-only strategy, and complete recipe produce `Bottom` for an existing empty initial layer, while a missing initial layer fails explicitly.
- [x] Generic recipes do not consume zone input. The designated-zone reader, creation-rule zone resolver, and zone-dependent orchestration are inactive Phase 4 preparation and must not be required or registered by the generic Phase 3 path (D-035).
- [x] Confirm the generic baseline mapping: Check Point gateway type `9` plus `DefaultEndOfRulebase` maps exactly to `EndOfBlock(CheckPointLayerScopeBlock(CheckPointLayerSelector.InitialLayer()))`; every other existing/unknown enum is unsupported in WP-05.
- [x] Define the graph-only strategy/catalog, caller, implementation-task selection, and store/replace/preserve/clear policy. Positioning is best-effort and all completed non-cancellation failures clear stale state.
- [x] Product boundary resolved: persisted semantic output is terminal; WP-05 removes `CheckPointRulePositionRenderer`, and no replacement renderer, `CheckPointClient`, `add-access-rule`, publish, or policy installation is part of the feature (D-033 and D-036).
- [x] Remove the dormant WP-04 renderer and its dedicated tests during Phase 3 WP-05. A future placer must define and own any required conversion contract rather than inheriting this speculative adapter (D-036).
- [x] Correct or remove active documentation that describes customer analysis or renderer activation as Phase 3 WP-05 work. Historical decision entries remain append-only and are superseded by D-033 and D-034.

## Before Phase 3 WP-06 implementation

- [ ] Review the workflow diagram and settle the exact post-persistence implementation-task trigger in a separate WP-06 planning session. WP-05 implements and registers the caller but deliberately leaves it unhooked.

## Validation and stakeholder follow-up

- [ ] Run the outstanding Phase 1 disposable-VM fresh-install and `9.7.2` to `9.7.3` upgrade acceptance gates through the approved installation-test workflow.
- [ ] Consult the audit stakeholders about rule-position change events, actor, payload, retention, and clearing semantics before adding auditing.

## Phase 4 customer-extension checks

- [ ] Open `FWO_Regel_Platzierung_Regelwerk.md` only in Phase 4 WP-01 planning.
- [ ] Audit `CreationRuleZoneResolver` for duplicated workflow-element filtering, Flow-group lookup, address validation, range deduplication, zone validation/cloning, and coverage classification before customer activation. Retain existing `GetNwObjectElements`, `ComplianceCheck.ParseIpRange`, `ComplianceZoneResolver`, and `IFlowGroupResolver` ownership.
- [ ] Confirm whether Flow groups created by another task in the same ticket must resolve before they become available in the Flow catalog.
- [ ] Compare every customer requirement with existing generic blocks before adding another block.
- [ ] Add existing-rule semantic data only if a concrete customer rule requires comparison with existing rule contents.
- [ ] Preserve generic activation and persistence; customer work should extend the catalog rather than replace the production path.

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

## Phase 3 WP-06 implementation work (complete)

- [x] Review the workflow diagram and settle the exact post-persistence implementation-task trigger in a separate WP-06 planning session. The selected seam is the middleware implementation-task creation event emitted through `ActionHandler.DoStateChangeActions` after `WfDbAccess.AddImplTaskToDb` assigns the durable ID.
- [x] Implement and validate the decision-complete `phase-3/WP-06-workflow-activation-implementation.md` brief.

## Validation and stakeholder follow-up

- [x] Run the Phase 1 disposable-VM fresh-install and sequential `9.8.0` to `9.8.1` upgrade acceptance gates through the approved installation-test workflow. Accepted on 2026-10-23 for commit `c85931e0f`; both Ansible runs exited 0, the upgrade executed `9.8.1.sql`, and the installed C#, importer, and integration suites passed.
- [ ] Consult the audit stakeholders about rule-position change events, actor, payload, retention, and clearing semantics before adding auditing.
- [ ] Decide in a later modify/retry work package whether changing a persisted implementation task's gateway, management, or relevant rule content clears or recalculates its position, and whether an authorized manual retry is needed. WP-06 activates creation only.

## Phase 4 customer-extension checks

- [ ] Open `FWO_Regel_Platzierung_Regelwerk.md` only in Phase 4 WP-01 planning.
- [ ] Audit `CreationRuleZoneResolver` for duplicated workflow-element filtering, Flow-group lookup, address validation, range deduplication, zone validation/cloning, and coverage classification before customer activation. Retain existing `GetNwObjectElements`, `ComplianceCheck.ParseIpRange`, `ComplianceZoneResolver`, and `IFlowGroupResolver` ownership.
- [ ] Confirm whether Flow groups created by another task in the same ticket must resolve before they become available in the Flow catalog.
- [ ] Compare every customer requirement with existing generic blocks before adding another block.
- [ ] Add existing-rule semantic data only if a concrete customer rule requires comparison with existing rule contents.
- [ ] Preserve generic activation and persistence; customer work should extend the catalog rather than replace the production path.

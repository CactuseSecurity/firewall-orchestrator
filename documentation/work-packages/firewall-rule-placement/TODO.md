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

- [x] Open `FWO_Regel_Platzierung_Regelwerk.md` only in Phase 4 WP-01 planning.
- [x] Confirm that multiple distinct source or destination zones are never valid. Multiple objects remain valid only when they resolve completely to the same single persisted zone (D-051).
- [x] Confirm functional-target semantics: select the unique layer whose source and destination guards completely contain the corresponding requested IPs; zero or multiple matches fail (D-049).
- [x] Confirm same-ticket Flow-group semantics: pre-catalog groups must resolve through workflow effective/mock objects; Flow-catalog absence alone must not fail them (D-050).
- [x] Confirm that the KfW algorithm considers only guarded inline layers; the main layer is never a target or fallback (D-052).
- [x] Confirm that the ticket's recursively resolved final intended group state overrides older Flow-catalog membership; protocol-order violations may fail (D-053).
- [x] Confirm the address-family boundary: KfW scenarios are IPv4, while reusable FWO normalization and containment must remain IPv4/IPv6 aware (D-054).
- [x] Confirm that KfW guards use normal source/destination fields, `Any` is common and required, and exactly one guard completely contains each new customer rule; do not generalize that guarantee (D-055).
- [x] Set the guard-object trust boundary: consume every object through its normalized fixed-import FWO representation, without a KfW allow-list or positioning workaround for importer defects (D-056).
- [x] Confirm the three-state containment result for IP-less imported objects: `Contained`, `NotContained`, or `Indeterminate`; an indeterminate competing guard prevents unique selection (D-057).
- [x] Limit WP-01 to KfW Check Point `SECTION_NATIVE`; defer Fortinet `SECTION_LABEL` to a separate future work package beyond the current sprint and quarter (D-058).
- [x] Define the smallest historically consistent immutable layer-guard projection, supported guard terms, containment rules, and typed zero/ambiguous/indeterminate failures (D-060/D-061).
- [x] Define the authoritative workflow effective/mock object projection for same-ticket groups, including action folding, identity correlation, nesting/cycles, catalog precedence, and broader-ticket scope (D-059).
- [x] Evaluate `FlowDbCreator.BuildFlowCreationPayloads` and `FlowPayloadMerger`. They are Flow-write/bundle projections and do not implement recursive authoritative final state; WP-01 uses a separate immutable resolver and invokes no Flow persistence.
- [x] Audit `CreationRuleZoneResolver`. WP-01 introduces one canonical range input and a normalized-input zone overload while retaining `ComplianceCheck.ParseIpRange`, `ComplianceZoneResolver`, and `IFlowGroupResolver` ownership.
- [x] Compare every customer requirement with existing generic blocks. Reuse `CheckPointSectionScopeBlock` through an additive normalized mode and reuse `EndOfBlock`; add only the proven guard scope block.
- [x] Add existing-rule semantics only for inline parent guard rules through a separate fixed-import projection; no unrelated existing-rule fields enter the structural graph.
- [x] Preserve generic activation and persistence. WP-01 is directly testable but adds no mapping, registration, workflow selection, or persistence change.
- [x] Implement the decision-complete Phase 4 WP-01 brief in its four ordered slices and record validation evidence.
- [x] Plan and implement customer enum mapping, strategy registration, and algorithm-gated workflow selection in Phase 4 WP-02.

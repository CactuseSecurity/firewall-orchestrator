# Firewall rule positioning follow-up ledger

This file tracks unresolved review, validation, and product-boundary work. An item is not approved implementation scope merely because it appears here; move it into a decision-complete work package before coding.

## Before Phase 3 WP-05 implementation

- [ ] Audit `CheckPointRulebaseGraphInterpreter` against reusable lower-level `RuleTreeBuilder` graph logic. Preserve the immutable positioning boundary, but remove duplicated topology handling if an existing neutral seam can be reused safely.
- [ ] Add a representative imported empty Check Point policy fixture. Distinguish an existing initial layer with zero rules, which must support a top/bottom anchor, from a genuinely missing initial layer, which must fail explicitly.
- [x] Generic recipes do not consume zone input. The designated-zone reader, creation-rule zone resolver, and zone-dependent orchestration are inactive Phase 4 preparation and must not be required or registered by the generic Phase 3 path (D-035).
- [ ] Confirm the generic baseline algorithm and exact existing enum mapping without relying on the enum name.
- [ ] Define the exact workflow caller, applicable gateway-specific implementation-task selection, and store/replace/preserve/clear policy.
- [x] Product boundary resolved: persisted semantic output is terminal; WP-05 removes `CheckPointRulePositionRenderer`, and no replacement renderer, `CheckPointClient`, `add-access-rule`, publish, or policy installation is part of the feature (D-033 and D-036).
- [x] Remove the dormant WP-04 renderer and its dedicated tests during Phase 3 WP-05. A future placer must define and own any required conversion contract rather than inheriting this speculative adapter (D-036).
- [x] Correct or remove active documentation that describes customer analysis or renderer activation as Phase 3 WP-05 work. Historical decision entries remain append-only and are superseded by D-033 and D-034.

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

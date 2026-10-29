# Phase 4 WP-01 planning — Customer-specific recipes

## Status

Planning complete. Phase 3 WP-05 and WP-06 are implemented and validated, the customer document has been analyzed, and the implementation brief is decision-complete through D-061.

Customer calculation primitives are specified in the ready implementation brief. Enum mapping, production strategy registration, and workflow selection of the customer algorithm are deferred to a separate future plan. Settled behavior and the Check Point-only vendor scope are collected in [EXPERT-QUESTIONS.md](EXPERT-QUESTIONS.md).

## Planning objective

Analyze the Check Point `SECTION_NATIVE` requirements in `FWO_Regel_Platzierung_Regelwerk.md`, compare them with the finished generic positioning product, and produce a self-contained implementation brief for isolated KfW Check Point calculation blocks and activation-neutral recipe construction. Fortinet `SECTION_LABEL` is accounted for by explicit deferral under D-058.

Preserve the existing exact-match structural behavior. Customer normalization and selection behavior must be additive, whether represented by a separate block or by an explicit mode whose existing mode remains unchanged.

Do not map the customer behavior to a `ProvisioningPositioningAlgorithm`, register the zone-capable strategy, or connect it to the workflow in this package. A later selection/activation plan owns those decisions.

This completed planning brief authorized documentation changes only. Production work begins from the paired ready implementation brief.

## Customer analysis output

Extract and record without interpretation gaps:

- every supported placement scenario and input;
- source/destination zone-set behavior and precedence;
- layer, inline-layer, section, and boundary selection;
- application versus common-service behavior as unresolved or excluded when the customer document does not define it;
- every Fortinet statement as explicitly deferred rather than implemented or silently omitted;
- empty, missing, duplicated, partial, or ambiguous scope behavior;
- explicit fallback behavior, if any;
- examples that become executable acceptance tests; and
- contradictions or missing information requiring user/customer clarification.

Do not silently resolve ambiguity and do not infer behavior from enum names or the archived proposal.

## Required discovery

- Compare every customer requirement with existing generic blocks before proposing a new block.
- Determine whether existing-rule semantics are concretely required; if so, route the expanded data contract before implementation.
- Define an activation-neutral customer recipe/factory seam that can later be mapped to an exact capability without changing its calculation semantics.
- Keep the designated-zone reader, creation-rule zone resolver, and zone-capable strategy unregistered for positioning in this package.
- Define an immutable effective/mock object projection for same-ticket groups and its precedence over Flow-catalog state. The customer workflow requires pre-catalog groups to resolve.
- Evaluate reuse or extraction of the pure ticket snapshot and bundle-merging logic around `FlowDbCreator.BuildFlowCreationPayloads` and `FlowPayloadMerger`; do not call Flow persistence from positioning.
- Extend the customer calculation input with historically consistent layer-guard source/destination semantics. Select the unique functional layer by full source/destination IP coverage; do not infer it from a name or choose the first candidate.
- Reuse the imported inline link's source-rule identity to associate a guard with its inline layer, while extending the fixed-import graph projection only with the guard address semantics proven necessary.
- Apply the D-056/D-057 imported-object trust and three-state containment rules without a customer object allow-list.
- Verify that later activation can reuse the existing generic persistence path unchanged.

## Required planning output

Finalize `WP-01-customer-specific-recipes-implementation.md` with a normative behavior table, activation-neutral recipe construction, additional block contracts, explicit unresolved-input boundaries, files, tests, validation, and exclusions. Append decisions and update the phase status, TODO ledger, and handoff.

## Planning acceptance

- Every customer statement is accounted for.
- No existing generic behavior is duplicated in a customer-specific block.
- Customer calculation behavior can be invoked directly in tests without an enum mapping or production registration.
- Existing exact-match blocks and the graph-only generic capability remain unchanged.
- Domain ambiguity is either resolved or represented as an explicit input/failure boundary; it is never guessed by the implementer.
- The later selection/activation plan can opt into zone loading only for the customer capability.
- The semantic-output-only and no-firewall-execution boundaries remain intact.
- The removed WP-04 renderer is not recreated.

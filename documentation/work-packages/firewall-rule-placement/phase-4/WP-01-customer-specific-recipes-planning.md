# Phase 4 WP-01 planning — Customer-specific recipes

## Status

Planning in progress. Phase 3 WP-05 and WP-06 are implemented and validated, and the customer document has now been analyzed.

Customer calculation primitives will be planned first. Enum mapping, production strategy registration, and workflow selection of the customer algorithm are deferred to a separate future plan. Open domain questions are collected in [EXPERT-QUESTIONS.md](EXPERT-QUESTIONS.md).

## Planning objective

Analyze `FWO_Regel_Platzierung_Regelwerk.md`, compare its requirements with the finished generic positioning product, and produce a self-contained implementation brief for isolated customer-specific calculation blocks and activation-neutral recipe construction.

Preserve the existing exact-match structural behavior. Customer normalization and selection behavior must be additive, whether represented by a separate block or by an explicit mode whose existing mode remains unchanged.

Do not map the customer behavior to a `ProvisioningPositioningAlgorithm`, register the zone-capable strategy, or connect it to the workflow in this package. A later selection/activation plan owns those decisions.

This is a planning-only session. Do not change production code.

## Customer analysis output

Extract and record without interpretation gaps:

- every supported placement scenario and input;
- source/destination zone-set behavior and precedence;
- layer, inline-layer, section, and boundary selection;
- application versus common-service behavior as unresolved or excluded when the customer document does not define it;
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
- Revisit same-ticket Flow-group resolution if the customer scenarios require pre-catalog groups.
- Design customer blocks to accept an explicit functional parent scope; do not guess an initial or inline layer while the functional-target source is unresolved.
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

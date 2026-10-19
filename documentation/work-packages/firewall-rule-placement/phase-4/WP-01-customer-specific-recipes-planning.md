# Phase 4 WP-01 planning — Customer-specific recipes

## Status

Blocked until Phase 3 WP-05 and WP-06 are implemented and validated.

## Planning objective

Analyze `FWO_Regel_Platzierung_Regelwerk.md` for the first time, compare its requirements with the finished generic positioning product, and produce a self-contained implementation brief for only the missing customer-specific blocks, recipes, and enum mappings.

This is a planning-only session. Do not change production code.

## Customer analysis output

Extract and record without interpretation gaps:

- every supported placement scenario and input;
- source/destination zone-set behavior and precedence;
- layer, inline-layer, section, and boundary selection;
- application versus common-service behavior;
- empty, missing, duplicated, partial, or ambiguous scope behavior;
- explicit fallback behavior, if any;
- examples that become executable acceptance tests; and
- contradictions or missing information requiring user/customer clarification.

Do not silently resolve ambiguity and do not infer behavior from enum names or the archived proposal.

## Required discovery

- Compare every customer requirement with existing generic blocks before proposing a new block.
- Determine whether existing-rule semantics are concretely required; if so, route the expanded data contract before implementation.
- Confirm which existing enum values represent customer behavior and which remain generic or unsupported.
- Define how customer capabilities opt into the already-implemented designated-zone reader and creation-rule zone resolver without adding zone dependencies to generic capabilities.
- Revisit same-ticket Flow-group resolution if the customer scenarios require pre-catalog groups.
- Verify that all customer recipes use the existing generic activation and persistence path unchanged.

## Required planning output

Finalize `WP-01-customer-specific-recipes-implementation.md` with a normative behavior table, complete recipe trees, exact enum mappings, additional block contracts, files, tests, validation, and exclusions. Append decisions and update the phase status, TODO ledger, and handoff.

## Planning acceptance

- Every customer statement is accounted for.
- No existing generic behavior is duplicated in a customer-specific block.
- Every supported customer enum has exactly one complete recipe.
- Zone input is loaded only for customer capabilities that explicitly require it.
- No ambiguity or activation decision is delegated to the implementer.
- The semantic-output-only and no-firewall-execution boundaries remain intact.
- The removed WP-04 renderer is not recreated.

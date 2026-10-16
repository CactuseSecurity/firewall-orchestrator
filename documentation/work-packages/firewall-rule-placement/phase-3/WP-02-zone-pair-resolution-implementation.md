# Phase 3 WP-02 implementation - Creation-rule zone resolution

## Status

Draft - planning required.

Do not implement this work package until the WP-02 planning session supplies the settled contract and marks this brief `Ready for implementation`.

## Goal

Add a reusable, cancellation-aware service that derives the positioning zone input for a new access rule from its source and destination workflow elements and caller-loaded network-zone data.

## Fixed requirements

- Support only creation of new access rules.
- Use the workflow elements already present in the Phase 1 calculation context.
- Reuse established IP/range/group and network-zone behavior where compatible.
- Keep the output independent of customer recipes and Check Point section names.
- Return explicit safe failures for invalid, unsupported, unresolved, or ambiguous inputs according to the settled WP-02 design.
- Do not query a firewall or compare the new rule with existing rules.
- Do not inspect the customer document, map enums to recipes, register a strategy, persist a position, or activate a workflow.

## Settled implementation design

The WP-02 planner must replace this paragraph with the exact input/output types, participating workflow elements, resolution algorithm, data-loading ownership, cardinality rules, and failure mapping.

## Exact change set

To be supplied by the WP-02 planner.

## Required tests

The final plan must cover, as applicable:

- one source and one destination zone;
- groups and multiple workflow elements;
- normalized IPv4 and IPv6 hosts, networks, and ranges;
- unresolved addresses and missing zone data;
- overlapping or multiple zone matches;
- Internet/dynamic/domain object behavior selected by the planner;
- irrelevant element fields and actions;
- unsupported task types;
- safe diagnostics and cancellation; and
- immutability of the returned positioning input.

The planner must name exact tests and expected results.

## Validation

The planner must add a focused test command. Implementation also requires formatting, the Debug solution build, the complete unit suite, and more than 80% line coverage for new production code.

## Acceptance criteria

- The service deterministically produces the settled zone input for supported new-rule tasks.
- Invalid or unresolved input cannot silently fall back to a different zone.
- No customer placement behavior is embedded in the resolver.
- No external firewall or workflow side effect occurs.

## Out of scope

- Import/snapshot loading.
- Customer zone-to-scope mapping.
- Recipe construction or execution.
- Check Point request rendering.
- Strategy registration and workflow activation.
